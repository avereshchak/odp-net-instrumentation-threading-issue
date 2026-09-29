# Reproducing an ODP.NET OpenTelemetry Metrics Race

This project reproduces a concurrency bug in `Oracle.ManagedDataAccess`'s built-in
OpenTelemetry instrumentation: several `Oracle.ManagedDataAccess.Client.OracleMetricsCollection`
methods (e.g. `GetNumActiveConnectionPoolsGroups`, `GetNumberOfFreeConnections`,
`GetNumberOfPooledConnections`) read/write shared **static** `Dictionary<>` fields with no
locking. When multiple threads observe metrics concurrently (as OpenTelemetry does whenever
more than one `MeterListener`/`MeterProvider` is active) while connection pools are being
created/destroyed, these dictionaries get corrupted and throw.

Look into a decompiled [OracleMetricsCollection.cs](OdpThreadingIssue/decompiled/OracleMetricsCollection.cs), 
there you'll find that `Dictionary<,>` is being actively used for storing metrics data.

## Prerequisites

- .NET 10 SDK
- A local Oracle database reachable at `127.0.0.1:1521/FREEPDB1`, user `system`,
  password `YourPassword123` (see below for the quickest way to get one).

That's it — the test itself needs nothing special. Any Oracle instance works, local or
remote, as long as the connection string in `OdpInstrumentationTests.cs` matches it.

## Step-by-step: create the local Oracle database

Using [Podman Desktop](https://podman-desktop.io/) or [Docker Desktop](https://www.docker.com/products/docker-desktop/),
run Oracle's official, free "lite" Oracle Database Free container image from Oracle's
container registry. The commands below use `podman`; if you have Docker Desktop instead,
just replace `podman` with `docker` everywhere — the image and all flags are identical.

### 1. Start the container engine (Podman only)

Docker Desktop starts its engine automatically. Podman Desktop needs its Linux VM started
once per machine reboot:

```powershell
podman machine start
```

If it says the machine is already running, that's fine — continue to the next step.

### 2. Run the Oracle container

Unlike most images on Oracle's container registry, `database/free` is publicly pullable
without logging in or creating an Oracle account — anonymous `podman pull`/`podman run`
works directly:

```powershell
podman run -d -p 127.0.0.1:1521:1521 -e ORACLE_PWD=YourPassword123 --name oracle-free container-registry.oracle.com/database/free:latest-lite
```

What each part of this command does:

| Part | Meaning |
|---|---|
| `-d` | Run in the background ("detached"), so your terminal isn't blocked. |
| `-p 127.0.0.1:1521:1521` | Publish container port `1521` (Oracle's default listener port) explicitly on the IPv4 loopback (`127.0.0.1`) on your machine, so tools like this test can connect to `127.0.0.1:1521`. Binding explicitly to the IPv4 address (rather than a bare `-p 1521:1521`, which can resolve ambiguously) avoids cases where WSL2/Podman port forwarding ends up reachable only over IPv6 (`[::1]:1521`) and not IPv4. |
| `-e ORACLE_PWD=YourPassword123` | Environment variable that sets the password for the Oracle `system`/`sys` admin accounts on first startup. **This must be `ORACLE_PWD`** for this official image — note this is the opposite of the community `gvenzl/oracle-free` image, which instead required `ORACLE_PASSWORD` (and silently ignored `ORACLE_PWD`). Using the wrong variable name here silently starts the container with no password set, so the connection just fails with an auth error later. |
| `--name oracle-free` | Friendly name so you can refer to the container later (e.g. `podman logs oracle-free`) instead of a random ID. |
| `container-registry.oracle.com/database/free:latest-lite` | The container image: Oracle's official, free Oracle Database Free 23ai build, "lite" variant (smaller image, faster startup, no Oracle Database options like Java/Multimedia preinstalled — none of which this repro needs). No separate license purchase needed for development/testing use. |

You only need to run this once. The container and its data persist across reboots as long
as you don't delete it (see "Cleaning up" below).

### 3. Wait for the database to finish initializing

First startup takes 1-2 minutes while Oracle creates its data files. Watch the logs and
wait for the ready banner:

```powershell
podman logs -f oracle-free
```

You'll see output ending with something like:

```
#########################
DATABASE IS READY TO USE!
#########################
```

Once you see that, press `Ctrl+C` to stop following the logs (the database keeps running
in the background — this only stops watching its output).

### 4. Verify the database is reachable

```powershell
Test-NetConnection -ComputerName 127.0.0.1 -Port 1521
```

Look for `TcpTestSucceeded : True` in the output. If it says `False`, see
"Troubleshooting" below before running the test.

That's the entire setup — no manual Oracle init-parameter tuning, no container
resource/cgroup configuration, and no networking mode changes are required for a normal
repro run on a healthy Podman/Docker installation.

## Running the repro

```powershell
dotnet test --filter "FullyQualifiedName=OdbThreadingIssue.Test1.ConcurrentMetricsCollection_DoesNotThrowOrHang" --logger "console;verbosity=detailed"
```

The test hammers ODP.NET's OpenTelemetry observable instruments from many threads at once via
`MeterListener.RecordObservableInstruments()` while connection pools continuously churn in the
background. On an affected ODP.NET version, it fails within well under a minute with an
`ArgumentException` or `InvalidOperationException` thrown from inside `OracleMetricsCollection`, e.g.:

```
System.ArgumentException: An item with the same key has already been added. Key: ...
   at Oracle.ManagedDataAccess.Client.OracleMetricsCollection.GetNumActiveConnectionPoolsGroups()
```

or

```
System.InvalidOperationException: Operations that change non-concurrent collections must
have exclusive access. A concurrent update was performed on this collection and corrupted
its state.
   at Oracle.ManagedDataAccess.Client.OracleMetricsCollection.GetNumberOfPooledConnections()
```

If the test instead hangs or times out via the built-in watchdog (see `ListenerHeartbeatTimeout`
/ `ConnectionHeartbeatTimeout` in `OdpInstrumentationTests.cs`), that's also a valid (different) symptom of the
same underlying lack of synchronization, and worth reporting as such.

## Troubleshooting

- **`ORA-12541: No listener` / `ORA-50201`**: the container isn't up yet, or isn't reachable
  on port 1521.
  - Confirm the container is actually running: `podman ps` should list `oracle-free` with
    a status like `Up 5 minutes`. If it's missing entirely, check `podman ps -a` — if it
    shows `Exited`, start it again with `podman start oracle-free`.
  - Confirm the port is reachable: `Test-NetConnection -ComputerName 127.0.0.1 -Port 1521`.
  - Confirm the database has actually finished initializing (see step 3 above) — connecting
    too early during first-time startup also produces this error.
- **Login/authentication failures on first connection**: double check the container was
  created with `-e ORACLE_PWD=YourPassword123` and that the connection string in
  `OdpInstrumentationTests.cs` uses the matching password.
- **`ORA-00020: exceeded maximum number of processes`** (often surfaced to the .NET client
  as `ORA-12537: TNS:connection closed` or `ORA-50201`): only likely if you run the test
  repeatedly/for a long time without restarting the container in between. If you hit this,
  either restart the container (`podman restart oracle-free`) between runs, or raise the
  database's `processes` init parameter once (requires a container restart to take effect):

  ```powershell
  podman exec oracle-free bash -c "printf 'alter system set processes=1500 scope=spfile;\nexit;\n' | sqlplus -s system/YourPassword123@127.0.0.1:1521/FREE"
  podman restart oracle-free
  ```

- **Windows + WSL2 users only** — if `127.0.0.1` doesn't connect but the container is
  confirmed running, some WSL/Podman networking setups forward the Windows-side listener
  only on the IPv6 loopback (`[::1]:1521`) rather than IPv4. Re-publish the container port
  explicitly as `-p 127.0.0.1:1521:1521` (see step 2) and confirm with
  `Test-NetConnection -ComputerName 127.0.0.1 -Port 1521`.
- **`podman run` fails with `unauthorized` / `denied` pulling
  `container-registry.oracle.com/database/free:latest-lite`**: this repository is normally
  pullable anonymously, so this is unexpected. It can happen if a previous failed/expired
  `podman login container-registry.oracle.com` session left stale (invalid) cached
  credentials for that registry. Log out and retry:

  ```powershell
  podman logout container-registry.oracle.com
  podman pull container-registry.oracle.com/database/free:latest-lite
  ```
- **Podman-specific container start errors mentioning `cgroup`/`pids controller`**: this
  is an unrelated, machine-specific Podman/WSL configuration issue, not something caused
  by this repro. Restarting the Podman machine (`podman machine stop` then
  `podman machine start`) resolves it in most cases; if not, recreating the machine
  (`podman machine rm` then `podman machine init` then `podman machine start`) will.

## Cleaning up

Stop and permanently delete the container and its data:

```powershell
podman rm -f oracle-free
```

To just stop it temporarily (keeping its data so you can `podman start oracle-free`
again later without waiting for re-initialization):

```powershell
podman stop oracle-free
```

