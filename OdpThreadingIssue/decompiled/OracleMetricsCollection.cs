// Decompiled with JetBrains decompiler
// Type: Oracle.ManagedDataAccess.Client.OracleMetricsCollection
// Assembly: Oracle.ManagedDataAccess, Version=23.1.0.0, Culture=neutral, PublicKeyToken=89b483f429c47342
// MVID: E51F038D-4C4D-49DA-8D41-00BB298A6229
// Assembly location: C:\dev\Prototypes\HangingOdpNet\HangingOdpNet\bin\Debug\net10.0\Oracle.ManagedDataAccess.dll

using OracleInternal.ConnectionPool;
using OracleInternal.ServiceObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;

#nullable disable
namespace Oracle.ManagedDataAccess.Client;

internal class OracleMetricsCollection
{
  private static Meter Meter = new Meter("Oracle.ManagedDataAccess.Core");
  private static OracleMetricsInstrument[] m_countersList = new OracleMetricsInstrument[16 /*0x10*/];
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfActiveConnectionPools;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfInactiveConnectionPools;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfActiveConnectionPoolGroups;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfInactiveConnectionPoolGroups;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfActiveConnections;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfFreeConnections;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfPooledConnections;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfNonPooledConnections;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfActiveHardConnections;
  private static IDictionary<string, (OracleMetricsLevel, long)> numberOfActiveSoftConnections;

  static OracleMetricsCollection() => OracleMetricsCollection.InitializeCounters();

  internal static bool IsCounterEnabled(OraclePerfParams.CounterIndex iCounter)
  {
    return OracleMetricsCollection.m_countersList[(int) iCounter].Enabled;
  }

  internal static void Increment(
    OraclePerfParams.CounterIndex iCounter,
    string poolName,
    string dbInstanceName)
  {
    OracleMetricsInstrument counters = OracleMetricsCollection.m_countersList[(int) iCounter];
    if (counters == null)
      return;
    string instanceName = OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, poolName, dbInstanceName);
    string[] strArray = counters.DoesLevelExists(instanceName) ? ((IEnumerable<Tuple<string, OracleMetricsLevel>>) OracleMetricsCollection.CreateLevelNames(instanceName)).Select<Tuple<string, OracleMetricsLevel>, string>((Func<Tuple<string, OracleMetricsLevel>, string>) (t => t.Item1)).ToArray<string>() : ((IEnumerable<Tuple<string, OracleMetricsLevel>>) OracleMetricsCollection.CreateCounterLevel(iCounter, instanceName)).Select<Tuple<string, OracleMetricsLevel>, string>((Func<Tuple<string, OracleMetricsLevel>, string>) (t => t.Item1)).ToArray<string>();
    counters.Increment(strArray);
  }

  internal static void Decrement(
    OraclePerfParams.CounterIndex iCounter,
    string poolName,
    string dbInstanceName)
  {
    OracleMetricsInstrument counters = OracleMetricsCollection.m_countersList[(int) iCounter];
    if (counters == null)
      return;
    string instanceName = OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, poolName, dbInstanceName);
    string[] strArray = counters.DoesLevelExists(instanceName) ? ((IEnumerable<Tuple<string, OracleMetricsLevel>>) OracleMetricsCollection.CreateLevelNames(instanceName)).Select<Tuple<string, OracleMetricsLevel>, string>((Func<Tuple<string, OracleMetricsLevel>, string>) (t => t.Item1)).ToArray<string>() : ((IEnumerable<Tuple<string, OracleMetricsLevel>>) OracleMetricsCollection.CreateCounterLevel(iCounter, instanceName)).Select<Tuple<string, OracleMetricsLevel>, string>((Func<Tuple<string, OracleMetricsLevel>, string>) (t => t.Item1)).ToArray<string>();
    counters.Decrement(strArray);
  }

  private static Tuple<string, OracleMetricsLevel>[] CreateCounterLevel(
    OraclePerfParams.CounterIndex iCount,
    string pfcInstanceName)
  {
    OracleMetricsInstrument counters = OracleMetricsCollection.m_countersList[(int) iCount];
    Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(pfcInstanceName);
    foreach (Tuple<string, OracleMetricsLevel> levelName in levelNames)
    {
      if (!string.IsNullOrEmpty(levelName.Item1))
        counters.AddCounterLevel(levelName);
      else
        break;
    }
    return levelNames;
  }

  private static Tuple<string, OracleMetricsLevel>[] CreateLevelNames(string pfcInstanceName)
  {
    Tuple<string, OracleMetricsLevel>[] levelNames = new Tuple<string, OracleMetricsLevel>[3];
    int num1 = pfcInstanceName.IndexOf(']');
    if (num1 != -1)
    {
      string str1 = pfcInstanceName.Substring(0, num1 + 1);
      levelNames[0] = new Tuple<string, OracleMetricsLevel>(str1, OracleMetricsLevel.AppDomain);
      int num2 = pfcInstanceName.IndexOf(']', num1 + 1);
      if (num2 != -1)
      {
        string str2 = pfcInstanceName.Substring(0, num2 + 1);
        levelNames[1] = new Tuple<string, OracleMetricsLevel>(str2, OracleMetricsLevel.ConnectionPool);
      }
    }
    levelNames[2] = new Tuple<string, OracleMetricsLevel>(pfcInstanceName, OracleMetricsLevel.DbInstance);
    return levelNames;
  }

  private static void InitializeCounters()
  {
    Tuple<string, OracleMetricsLevel> levelName = new Tuple<string, OracleMetricsLevel>(OraclePerfParams.m_appDomainPfcInstanceName, OracleMetricsLevel.AppDomain);
    OracleMetricsInstrument metricsInstrument1 = new OracleMetricsInstrument("odp.hard_connects", OracleMetricsCollection.Meter, CounterType.RatePerSecond);
    metricsInstrument1.AddCounterLevel(levelName);
    OracleMetricsCollection.m_countersList[0] = metricsInstrument1;
    OracleMetricsInstrument metricsInstrument2 = new OracleMetricsInstrument("odp.hard_disconnects", OracleMetricsCollection.Meter, CounterType.RatePerSecond);
    metricsInstrument2.AddCounterLevel(levelName);
    OracleMetricsCollection.m_countersList[1] = metricsInstrument2;
    OracleMetricsCollection.m_countersList[2] = new OracleMetricsInstrument("odp.soft_connects", OracleMetricsCollection.Meter, CounterType.RatePerSecond);
    OracleMetricsCollection.m_countersList[2].AddCounterLevel(levelName);
    OracleMetricsCollection.m_countersList[3] = new OracleMetricsInstrument("odp.soft_disconnects", OracleMetricsCollection.Meter, CounterType.RatePerSecond);
    OracleMetricsCollection.m_countersList[3].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[4] = new OracleMetricsInstrument("odp.number_of_active_connection_pools", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C0\u003E__GetNumActiveConnectionPools ?? (OracleMetricsCollection.\u003C\u003EO.\u003C0\u003E__GetNumActiveConnectionPools = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumActiveConnectionPools)));
    OracleMetricsCollection.m_countersList[4].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[5] = new OracleMetricsInstrument("odp.number_of_active_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C1\u003E__GetNumberOfActiveConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C1\u003E__GetNumberOfActiveConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfActiveConnections)));
    OracleMetricsCollection.m_countersList[5].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[6] = new OracleMetricsInstrument("odp.number_of_free_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C2\u003E__GetNumberOfFreeConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C2\u003E__GetNumberOfFreeConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfFreeConnections)));
    OracleMetricsCollection.m_countersList[6].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[7] = new OracleMetricsInstrument("odp.number_of_inactive_connection_pools", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C3\u003E__GetNumInActiveConnectionPools ?? (OracleMetricsCollection.\u003C\u003EO.\u003C3\u003E__GetNumInActiveConnectionPools = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumInActiveConnectionPools)));
    OracleMetricsCollection.m_countersList[7].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[8] = new OracleMetricsInstrument("odp.number_of_non_pooled_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C4\u003E__GetNumberOfNonPooledConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C4\u003E__GetNumberOfNonPooledConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfNonPooledConnections)));
    OracleMetricsCollection.m_countersList[8].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[9] = new OracleMetricsInstrument("odp.number_of_pooled_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C5\u003E__GetNumberOfPooledConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C5\u003E__GetNumberOfPooledConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfPooledConnections)));
    OracleMetricsCollection.m_countersList[9].AddCounterLevel(levelName);
    OracleMetricsCollection.m_countersList[10] = new OracleMetricsInstrument("odp.number_of_reclaimed_connections", OracleMetricsCollection.Meter, CounterType.Counts);
    OracleMetricsCollection.m_countersList[10].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[12] = new OracleMetricsInstrument("odp.number_of_active_hard_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C6\u003E__GetNumberOfActiveHardConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C6\u003E__GetNumberOfActiveHardConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfActiveHardConnections)));
    OracleMetricsCollection.m_countersList[12].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[13] = new OracleMetricsInstrument("odp.number_of_active_soft_connections", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C7\u003E__GetNumberOfActiveSoftConnections ?? (OracleMetricsCollection.\u003C\u003EO.\u003C7\u003E__GetNumberOfActiveSoftConnections = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumberOfActiveSoftConnections)));
    OracleMetricsCollection.m_countersList[13].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[14] = new OracleMetricsInstrument("odp.number_of_active_connection_pool_groups", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C8\u003E__GetNumActiveConnectionPoolsGroups ?? (OracleMetricsCollection.\u003C\u003EO.\u003C8\u003E__GetNumActiveConnectionPoolsGroups = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumActiveConnectionPoolsGroups)));
    OracleMetricsCollection.m_countersList[14].AddCounterLevel(levelName);
    // ISSUE: reference to a compiler-generated field
    // ISSUE: reference to a compiler-generated field
    OracleMetricsCollection.m_countersList[15] = new OracleMetricsInstrument("odp.number_of_inactive_connection_pool_groups", OracleMetricsCollection.Meter, CounterType.Counts, valuesProvider: OracleMetricsCollection.\u003C\u003EO.\u003C9\u003E__GetNumInactiveConnectionPoolsGroups ?? (OracleMetricsCollection.\u003C\u003EO.\u003C9\u003E__GetNumInactiveConnectionPoolsGroups = new Func<IDictionary<string, (OracleMetricsLevel, long)>>(OracleMetricsCollection.GetNumInactiveConnectionPoolsGroups)));
    OracleMetricsCollection.m_countersList[15].AddCounterLevel(levelName);
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumActiveConnectionPools()
  {
    if (OracleMetricsCollection.numberOfActiveConnectionPools == null)
      OracleMetricsCollection.numberOfActiveConnectionPools = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfActiveConnectionPools.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfActiveConnectionPools.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfActiveConnectionPools.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        if (oraclePool.m_cpListPR.Count > 0)
        {
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            (OracleMetricsLevel, long) activeConnectionPool = OracleMetricsCollection.numberOfActiveConnectionPools[tuple.Item1];
            OracleMetricsCollection.numberOfActiveConnectionPools[tuple.Item1] = (activeConnectionPool.Item1, activeConnectionPool.Item2 + 1L);
          }
        }
      }
    }
    return OracleMetricsCollection.numberOfActiveConnectionPools;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumInActiveConnectionPools()
  {
    if (OracleMetricsCollection.numberOfInactiveConnectionPools == null)
      OracleMetricsCollection.numberOfInactiveConnectionPools = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfInactiveConnectionPools.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfInactiveConnectionPools.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfInactiveConnectionPools.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        if (oraclePool.m_cpListPR.Count == 0)
        {
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            (OracleMetricsLevel, long) inactiveConnectionPool = OracleMetricsCollection.numberOfInactiveConnectionPools[tuple.Item1];
            OracleMetricsCollection.numberOfInactiveConnectionPools[tuple.Item1] = (inactiveConnectionPool.Item1, inactiveConnectionPool.Item2 + 1L);
          }
        }
      }
    }
    return OracleMetricsCollection.numberOfInactiveConnectionPools;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumActiveConnectionPoolsGroups()
  {
    if (OracleMetricsCollection.numberOfActiveConnectionPoolGroups == null)
      OracleMetricsCollection.numberOfActiveConnectionPoolGroups = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfActiveConnectionPoolGroups.Clear();
    List<OraclePoolManager> values = OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues();
    long num = 0;
    foreach (OraclePoolManager oraclePoolManager in values)
    {
      if (oraclePoolManager.m_pmListPR.Count > 0 && oraclePoolManager.m_pmListCP.Count > 0)
        ++num;
    }
    OracleMetricsCollection.numberOfActiveConnectionPoolGroups.Add(OraclePerfParams.m_appDomainPfcInstanceName, (OracleMetricsLevel.AppDomain, num));
    return OracleMetricsCollection.numberOfActiveConnectionPoolGroups;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumInactiveConnectionPoolsGroups()
  {
    if (OracleMetricsCollection.numberOfInactiveConnectionPoolGroups == null)
      OracleMetricsCollection.numberOfInactiveConnectionPoolGroups = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfInactiveConnectionPoolGroups.Clear();
    List<OraclePoolManager> values = OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues();
    long num = 0;
    foreach (OraclePoolManager oraclePoolManager in values)
    {
      if (oraclePoolManager.m_pmListPR.Count == 0 && oraclePoolManager.m_pmListCP.Count > 0)
        ++num;
    }
    OracleMetricsCollection.numberOfInactiveConnectionPoolGroups.Add(OraclePerfParams.m_appDomainPfcInstanceName, (OracleMetricsLevel.AppDomain, num));
    return OracleMetricsCollection.numberOfInactiveConnectionPoolGroups;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfActiveConnections()
  {
    if (OracleMetricsCollection.numberOfActiveConnections == null)
      OracleMetricsCollection.numberOfActiveConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfActiveConnections.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      if ((oraclePoolManager.m_pmListCP == null || oraclePoolManager.m_pmListCP.Count == 0) && oraclePoolManager.m_pmListPR.Count > 0)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePoolManager.m_pmListPR[0].m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfActiveConnections.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfActiveConnections.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        long count = (long) oraclePoolManager.m_pmListPR.Count;
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          (OracleMetricsLevel, long) activeConnection = OracleMetricsCollection.numberOfActiveConnections[tuple.Item1];
          OracleMetricsCollection.numberOfActiveConnections[tuple.Item1] = (activeConnection.Item1, activeConnection.Item2 + count);
        }
      }
      else
      {
        foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
        {
          Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            if (!OracleMetricsCollection.numberOfActiveConnections.ContainsKey(tuple.Item1))
              OracleMetricsCollection.numberOfActiveConnections.Add(tuple.Item1, (tuple.Item2, 0L));
          }
          long num = (long) (oraclePool.m_cpListPR.Count - oraclePool.m_cpQueuePR.Count);
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            (OracleMetricsLevel, long) activeConnection = OracleMetricsCollection.numberOfActiveConnections[tuple.Item1];
            OracleMetricsCollection.numberOfActiveConnections[tuple.Item1] = (activeConnection.Item1, activeConnection.Item2 + num);
          }
        }
      }
    }
    return OracleMetricsCollection.numberOfActiveConnections;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfFreeConnections()
  {
    if (OracleMetricsCollection.numberOfFreeConnections == null)
      OracleMetricsCollection.numberOfFreeConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfFreeConnections.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfFreeConnections.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfFreeConnections.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        long count = (long) oraclePool.m_cpQueuePR.Count;
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          (OracleMetricsLevel, long) ofFreeConnection = OracleMetricsCollection.numberOfFreeConnections[tuple.Item1];
          OracleMetricsCollection.numberOfFreeConnections[tuple.Item1] = (ofFreeConnection.Item1, ofFreeConnection.Item2 + count);
        }
      }
    }
    return OracleMetricsCollection.numberOfFreeConnections;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfPooledConnections()
  {
    if (OracleMetricsCollection.numberOfPooledConnections == null)
      OracleMetricsCollection.numberOfPooledConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfPooledConnections.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfPooledConnections.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfPooledConnections.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        long count = (long) oraclePool.m_cpListPR.Count;
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          (OracleMetricsLevel, long) pooledConnection = OracleMetricsCollection.numberOfPooledConnections[tuple.Item1];
          OracleMetricsCollection.numberOfPooledConnections[tuple.Item1] = (pooledConnection.Item1, pooledConnection.Item2 + count);
        }
      }
    }
    return OracleMetricsCollection.numberOfPooledConnections;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfNonPooledConnections()
  {
    if (OracleMetricsCollection.numberOfNonPooledConnections == null)
      OracleMetricsCollection.numberOfNonPooledConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfNonPooledConnections.Clear();
    List<OraclePoolManager> values = OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues();
    int num1 = 0;
    int num2 = 0;
    foreach (OraclePoolManager oraclePoolManager in values)
    {
      num1 += oraclePoolManager.m_pmListPR.Count;
      if (oraclePoolManager.m_pmListCP != null)
      {
        foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP?.m_list)
        {
          if (oraclePool.m_cpListPR != null)
            num2 += oraclePool.m_cpListPR.Count;
        }
      }
    }
    long num3 = (long) (num1 - num2);
    OracleMetricsCollection.numberOfNonPooledConnections.Add(OraclePerfParams.m_appDomainPfcInstanceName, (OracleMetricsLevel.AppDomain, num3));
    return OracleMetricsCollection.numberOfNonPooledConnections;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfActiveHardConnections()
  {
    if (OracleMetricsCollection.numberOfActiveHardConnections == null)
      OracleMetricsCollection.numberOfActiveHardConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfActiveHardConnections.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      if ((oraclePoolManager.m_pmListCP == null || oraclePoolManager.m_pmListCP.Count == 0) && oraclePoolManager.m_pmListPR.Count > 0)
      {
        if (!OracleMetricsCollection.numberOfActiveHardConnections.ContainsKey(OraclePerfParams.m_appDomainPfcInstanceName))
          OracleMetricsCollection.numberOfActiveHardConnections.Add(OraclePerfParams.m_appDomainPfcInstanceName, (OracleMetricsLevel.AppDomain, 0L));
        (OracleMetricsLevel, long) activeHardConnection = OracleMetricsCollection.numberOfActiveHardConnections[OraclePerfParams.m_appDomainPfcInstanceName];
        OracleMetricsCollection.numberOfActiveHardConnections[OraclePerfParams.m_appDomainPfcInstanceName] = (activeHardConnection.Item1, activeHardConnection.Item2 + (long) oraclePoolManager.m_pmListPR.Count);
      }
      else
      {
        foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
        {
          Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            if (!OracleMetricsCollection.numberOfActiveHardConnections.ContainsKey(tuple.Item1))
              OracleMetricsCollection.numberOfActiveHardConnections.Add(tuple.Item1, (tuple.Item2, 0L));
          }
          long count = (long) oraclePool.m_cpListPR.Count;
          foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
          {
            (OracleMetricsLevel, long) activeHardConnection = OracleMetricsCollection.numberOfActiveHardConnections[tuple.Item1];
            OracleMetricsCollection.numberOfActiveHardConnections[tuple.Item1] = (activeHardConnection.Item1, activeHardConnection.Item2 + count);
          }
        }
      }
    }
    return OracleMetricsCollection.numberOfActiveHardConnections;
  }

  internal static IDictionary<string, (OracleMetricsLevel, long)> GetNumberOfActiveSoftConnections()
  {
    if (OracleMetricsCollection.numberOfActiveSoftConnections == null)
      OracleMetricsCollection.numberOfActiveSoftConnections = (IDictionary<string, (OracleMetricsLevel, long)>) new Dictionary<string, (OracleMetricsLevel, long)>();
    else
      OracleMetricsCollection.numberOfActiveSoftConnections.Clear();
    foreach (OraclePoolManager oraclePoolManager in OracleConnectionDispenser<OraclePoolManager, OraclePool, OracleConnectionImpl>.m_htPM.GetValues())
    {
      foreach (OraclePool oraclePool in oraclePoolManager.m_pmListCP.m_list)
      {
        Tuple<string, OracleMetricsLevel>[] levelNames = OracleMetricsCollection.CreateLevelNames(OracleCounterUtility.CreateInstanceName(OraclePerfParams.m_appDomainPfcInstanceName, oraclePoolManager.ConnectionString.m_poolName, oraclePool.m_instanceName));
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          if (!OracleMetricsCollection.numberOfActiveSoftConnections.ContainsKey(tuple.Item1))
            OracleMetricsCollection.numberOfActiveSoftConnections.Add(tuple.Item1, (tuple.Item2, 0L));
        }
        long num = (long) (oraclePool.m_cpListPR.Count - oraclePool.m_cpQueuePR.Count);
        foreach (Tuple<string, OracleMetricsLevel> tuple in levelNames)
        {
          (OracleMetricsLevel, long) activeSoftConnection = OracleMetricsCollection.numberOfActiveSoftConnections[tuple.Item1];
          OracleMetricsCollection.numberOfActiveSoftConnections[tuple.Item1] = (activeSoftConnection.Item1, activeSoftConnection.Item2 + num);
        }
      }
    }
    return OracleMetricsCollection.numberOfActiveSoftConnections;
  }
}
