using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Oracle.ManagedDataAccess.Client;

namespace OdpThreadingIssue;

[TestClass]
public sealed class OdpInstrumentationTests
{
    private const string BaseConnectionString =
        // NOTE: The Oracle container's port must be published on the IPv4 loopback
        // (127.0.0.1:1521), e.g. `podman run -p 127.0.0.1:1521:1521 ...`. Using the literal
        // IPv4 address avoids ambiguous "localhost" resolution between IPv4/IPv6.
        "User Id=system;Password=YourPassword123;Data Source=127.0.0.1:1521/FREEPDB1;";

    // How long a listener worker is allowed to go without making progress before it's
    // considered hung. Listener workers do no I/O (just RecordObservableInstruments()),
    // so any stall here is a strong signal of an actual deadlock/hang, not DB contention.
    private static readonly TimeSpan ListenerHeartbeatTimeout = TimeSpan.FromSeconds(15);

    // Connection workers do real DB I/O (logons), which can legitimately take longer
    // under heavy concurrent load against a small local container. Give them more slack
    // before treating a stall as a hang.
    private static readonly TimeSpan ConnectionHeartbeatTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan WatchdogPollInterval = TimeSpan.FromSeconds(1);

    // Raw System.Diagnostics.Metrics listeners, one per worker thread (see
    // CreateMeterListeners for why this many and why raw MeterListener is used).
    private const int MeterListenerCount = 32;

    // Number of concurrent "connection churn" workers used during the stress phase.
    // Kept modest and throttled (see MaxConcurrentLogons) so we don't turn this into a
    // pure DB-logon-storm benchmark - the actual race target is the listener threads
    // above colliding on the shared static dictionaries; the connection workers just need
    // to keep slowly adding new pool entries so the table never stops changing.
    private const int ConnectionWorkerCount = 4;

    // Caps how many logons can be in flight at once across all connection workers, so the
    // steady-state churn during the stress phase doesn't overwhelm the local DB
    // container/proxy.
    private const int MaxConcurrentLogons = 8;

    [TestMethod]
    public void ConcurrentMetricsCollection_DoesNotThrowOrHang()
    {
        VerifyDatabaseIsReachable();

        using var meterListeners = new DisposableList<MeterListener>(CreateMeterListeners());

        using var cts = new CancellationTokenSource();
        var exceptions = new ConcurrentQueue<Exception>();
        var heartbeats = new ConcurrentDictionary<string, DateTime>();
        var tasks = new List<Task>();

        StartListenerWorkers(meterListeners, cts, exceptions, heartbeats, tasks);

        using var logonThrottle = new SemaphoreSlim(MaxConcurrentLogons);
        StartConnectionChurnWorkers(logonThrottle, cts, exceptions, heartbeats, tasks);

        var stalledWorkerName = RunWatchdog(cts, exceptions, heartbeats);

        // Give workers a chance to unwind after cancellation. If a worker is genuinely
        // hung (stuck inside ODP.NET/OTel with no way to observe cancellation), this wait
        // will time out too - which corroborates the watchdog's hang detection above.
        var allWorkersExited = Task.WhenAll(tasks).Wait(TimeSpan.FromSeconds(30));

        AssertNoHangOrException(stalledWorkerName, allWorkersExited, exceptions);
    }

    // Preflight: verify the Oracle database is actually reachable before starting the
    // stress test. Without this, a completely absent/unreachable database can surface
    // as connection errors (e.g. ORA-12541, or ORA-50000 once pool-checkout waits start
    // timing out under the retry storm below) that get misreported as "reproduced the
    // ODP.NET concurrency bug", when the real problem is environmental: there's no
    // database to connect to. Fail fast here with an unambiguous message instead.
    private static void VerifyDatabaseIsReachable()
    {
        try
        {
            using var preflightConnection = new OracleConnection($"{BaseConnectionString}Connection Timeout=15;");
            preflightConnection.Open();
        }
        catch (Exception ex)
        {
            Assert.Fail(
                "Could not connect to the Oracle database before starting the stress test - this is an " +
                "environment/connectivity problem, not a reproduction of the ODP.NET metrics concurrency bug. " +
                "See readme.md for setup instructions (container creation, port publishing, processes limit, etc.). " +
                $"Underlying error: {ex}");
        }
    }

    // Raw System.Diagnostics.Metrics listeners, one per worker thread. Going through
    // MeterProvider.ForceFlush() adds a lot of OpenTelemetry SDK overhead per call
    // (aggregation, export pipeline), which makes each Clear()+Add() window inside
    // ODP.NET's observable callbacks too short/rare to collide even at high call rates.
    // Calling MeterListener.RecordObservableInstruments() directly is exactly what the
    // OTel SDK does under the hood, but with far less overhead per call, letting us
    // invoke the same static callbacks millions of times from many threads.
    private static List<MeterListener> CreateMeterListeners()
    {
        var listeners = new List<MeterListener>();
        for (var i = 0; i < MeterListenerCount; i++)
        {
            var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name.StartsWith("Oracle.ManagedDataAccess", StringComparison.Ordinal))
                {
                    l.EnableMeasurementEvents(instrument);
                }
            };
            listener.SetMeasurementEventCallback<long>(static (_, _, _, _) => { });
            listener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            listener.SetMeasurementEventCallback<int>(static (_, _, _, _) => { });
            listener.Start();
            listeners.Add(listener);
        }

        return listeners;
    }

    // Listener workers: call RecordObservableInstruments() in a tight loop, with no
    // delay, so many threads invoke ODP.NET's shared static callbacks concurrently.
    // They run until an exception is thrown or the watchdog cancels them.
    private static void StartListenerWorkers(
        IReadOnlyList<MeterListener> listeners,
        CancellationTokenSource cts,
        ConcurrentQueue<Exception> exceptions,
        ConcurrentDictionary<string, DateTime> heartbeats,
        List<Task> tasks)
    {
        for (var i = 0; i < listeners.Count; i++)
        {
            var listener = listeners[i];
            var workerName = $"listener-{i}";
            heartbeats[workerName] = DateTime.UtcNow;
            tasks.Add(Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        listener.RecordObservableInstruments();
                        heartbeats[workerName] = DateTime.UtcNow;
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                        cts.Cancel();
                        return;
                    }
                }
            }));
        }
    }

    // Connection churn workers: continuously add new distinct pools (throttled) to
    // mutate ODP.NET's internal pool tables while the listener workers enumerate it.
    private void StartConnectionChurnWorkers(
        SemaphoreSlim logonThrottle,
        CancellationTokenSource cts,
        ConcurrentQueue<Exception> exceptions,
        ConcurrentDictionary<string, DateTime> heartbeats,
        List<Task> tasks)
    {
        for (var w = 0; w < ConnectionWorkerCount; w++)
        {
            var workerId = w;
            var workerName = $"connection-{workerId}";
            heartbeats[workerName] = DateTime.UtcNow;
            tasks.Add(Task.Run(() => RunConnectionChurnWorker(workerId, workerName, logonThrottle, cts, exceptions, heartbeats)));
        }
    }

    private void RunConnectionChurnWorker(
        int workerId,
        string workerName,
        SemaphoreSlim logonThrottle,
        CancellationTokenSource cts,
        ConcurrentQueue<Exception> exceptions,
        ConcurrentDictionary<string, DateTime> heartbeats)
    {
        long iteration = 0;
        while (!cts.IsCancellationRequested)
        {
            try
            {
                var connectionString = BuildDistinctPoolConnectionString(workerId, iteration++);

                logonThrottle.Wait(cts.Token);
                try
                {
                    ExecuteProbeQuery(connectionString);
                }
                finally
                {
                    logonThrottle.Release();
                }

                heartbeats[workerName] = DateTime.UtcNow;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (OracleException ex) when (IsTransientNetworkError(ex))
            {
                // Expected noise from the local test container/proxy under heavy
                // concurrent connect/disconnect load - not the bug we're stressing
                // for. Back off briefly and keep churning. Deliberately NOT updating
                // the heartbeat here: if the database becomes persistently
                // unreachable (e.g. the container is stopped mid-run), these retries
                // would otherwise mask that as continuous "progress" forever instead
                // of surfacing it as a watchdog stall.
                Thread.Sleep(50);
            }
            catch (Exception ex)
            {
                exceptions.Enqueue(ex);
                cts.Cancel();
                return;
            }
        }
    }

    // "Connection Timeout" is a recognized ODP.NET attribute. ODP.NET keys connection
    // pools off the full connection string, so varying this value forces a distinct
    // pool to be created instead of reusing an existing one. The value space here is
    // never reset/wrapped - pool entries are left to accumulate for the whole run,
    // continuing to grow the pool-manager table as the run progresses.
    private static string BuildDistinctPoolConnectionString(int workerId, long iteration)
    {
        var timeoutSeconds = 15 + (int)((workerId * 1_000_000L + iteration) % 100_000);
        return $"{BaseConnectionString}Connection Timeout={timeoutSeconds};Max Pool Size=1;Min Pool Size=0;Incr Pool Size=1;";
    }

    private static void ExecuteProbeQuery(string connectionString)
    {
        using var connection = new OracleConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM DUAL";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
        }
    }

    // Watchdog: run until either a worker throws (exceptions non-empty) or a worker
    // stalls (its heartbeat stops advancing), instead of racing for a fixed duration.
    // Returns the name of the stalled worker, or null if the watchdog exited because a
    // worker threw an exception instead.
    private static string? RunWatchdog(
        CancellationTokenSource cts,
        ConcurrentQueue<Exception> exceptions,
        ConcurrentDictionary<string, DateTime> heartbeats)
    {
        while (!cts.IsCancellationRequested && exceptions.IsEmpty)
        {
            Thread.Sleep(WatchdogPollInterval);

            var now = DateTime.UtcNow;
            var stalled = heartbeats.FirstOrDefault(kvp =>
            {
                var timeout = kvp.Key.StartsWith("listener-", StringComparison.Ordinal)
                    ? ListenerHeartbeatTimeout
                    : ConnectionHeartbeatTimeout;
                return now - kvp.Value > timeout;
            });
            if (stalled.Key != null)
            {
                cts.Cancel();
                return stalled.Key;
            }
        }

        return null;
    }

    private static void AssertNoHangOrException(string? stalledWorkerName, bool allWorkersExited, ConcurrentQueue<Exception> exceptions)
    {
        if (stalledWorkerName != null)
        {
            Assert.Fail(
                $"Worker '{stalledWorkerName}' stopped making progress - possible hang in ODP.NET metrics instrumentation." +
                (allWorkersExited ? string.Empty : " Worker(s) also failed to exit after cancellation."));
        }

        Assert.IsTrue(allWorkersExited, "Worker(s) did not exit after cancellation was requested - possible hang in ODP.NET metrics collection.");

        if (!exceptions.IsEmpty)
        {
            Assert.Fail(
                "Reproduced concurrency issue in ODP.NET metrics instrumentation: " +
                string.Join(Environment.NewLine, exceptions.Select(e => e.ToString())));
        }
    }

    private static bool IsTransientNetworkError(OracleException ex)
    {
        // ORA-12541: no listener / ORA-50201: OCI communication failure / ORA-50000: pool
        // connection request timed out (governed by the "Connection Timeout" attribute,
        // which this test repurposes as a distinct-pool key - under heavy logon contention
        // with Max Pool Size=1, a checkout can legitimately exceed that timeout). These
        // surface occasionally under heavy local connect/disconnect churn against a
        // containerized database and are not related to the static Dictionary<> race
        // we're targeting.
        return ex.Number is 12541 or 50201 or 12170 or 3113 or 3114 or 50000;
    }

    // Small helper so a List<T> of disposable listeners can be disposed together via
    // `using`, without changing CreateMeterListeners()'s return type to something less
    // convenient to build incrementally.
    private sealed class DisposableList<T>(List<T> items) : List<T>(items), IDisposable
        where T : IDisposable
    {
        public void Dispose()
        {
            foreach (var item in this)
            {
                item.Dispose();
            }
        }
    }
}
