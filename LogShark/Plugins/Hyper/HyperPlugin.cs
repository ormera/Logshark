﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LogShark.Containers;
using LogShark.Plugins.Hyper.Model;
using LogShark.Plugins.Shared;
using LogShark.Writers;
using LogShark.Writers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LogShark.Plugins.Hyper
{
    public class HyperPlugin : IPlugin
    {
        private static readonly List<LogType> ConsumedLogTypesInternal = new List<LogType> { LogType.Hyper };
        public IList<LogType> ConsumedLogTypes => ConsumedLogTypesInternal;

        public string Name => "Hyper";

        private static readonly DataSetInfo HyperErrorOutputInfo = new DataSetInfo("Hyper", "HyperErrors");
        private static readonly DataSetInfo HyperQueryOutputInfo = new DataSetInfo("Hyper", "HyperQueries");

        private IWriter<HyperError> _hyperErrorWriter;
        private IWriter<HyperEvent> _hyperQueryWriter;
        private IProcessingNotificationsCollector _processingNotificationsCollector;

        private readonly HashSet<string> _eventTypes = new HashSet<string>() { "query-end", "query-end-cancelled", "connection-startup-begin", "connection-startup-end", "cancel-request-received", "connection-close-request",
            "dbregistry-load", "dbregistry-release", "query-result-sent", "tcp-ip-client-allowed", "tcp-ip-client-rejected", "query-plan-slow", "query-plan-spooling", "query-plan-cancelled",
            "startup-info", "resource-stats" };
        
        public void Configure(IWriterFactory writerFactory, IConfiguration pluginConfig, IProcessingNotificationsCollector processingNotificationsCollector, ILoggerFactory loggerFactory)
        {
            _hyperErrorWriter = writerFactory.GetWriter<HyperError>(HyperErrorOutputInfo);
            _hyperQueryWriter = writerFactory.GetWriter<HyperEvent>(HyperQueryOutputInfo);
            _processingNotificationsCollector = processingNotificationsCollector;
        }

        public void ProcessLogLine(LogLine logLine, LogType logType)
        {
            if (!(logLine.LineContents is NativeJsonLogsBaseEvent baseEvent))
            {
                var errorMessage = $"Was not able to cast line contents as {nameof(NativeJsonLogsBaseEvent)}";
                _processingNotificationsCollector.ReportError(errorMessage, logLine, nameof(HyperPlugin));
                return;
            }

            WriteHyperErrorIfMatch(logLine, baseEvent);
            WriteHyperQueryIfMatch(logLine, baseEvent);
        }

        private void WriteHyperErrorIfMatch(LogLine logLine, NativeJsonLogsBaseEvent jsonEvent)
        {
            if (jsonEvent.Severity != "error" && jsonEvent.Severity != "fatal")
            {
                return;
            }
            
            var valueString = GetHyperErrorValueString(jsonEvent.EventPayload);
            if (valueString == null)
            {
                _processingNotificationsCollector.ReportError("Failed to parse event payload message", logLine, nameof(HyperPlugin));
                return;
            }
            
            var hyperError = new HyperError()
            {
                FileName = logLine.LogFileInfo.FileName,
                FilePath = logLine.LogFileInfo.FilePath,
                Key = jsonEvent.EventType,
                Line = logLine.LineNumber,
                ProcessId = jsonEvent.ProcessId,
                RequestId = jsonEvent.RequestId,
                SessionId = jsonEvent.SessionId,
                Severity = jsonEvent.Severity,
                Site = jsonEvent.Site,
                ThreadId = jsonEvent.ThreadId,
                Timestamp = jsonEvent.Timestamp,
                User = jsonEvent.Username,
                Value = valueString,
                Worker = logLine.LogFileInfo.Worker,
            };
                
            _hyperErrorWriter.AddLine(hyperError);
        }

        private static string GetHyperErrorValueString(JToken value)
        {
            if (value == null)
            {
                return null;
            }
            
            var valueStrings = value
                .ToObject<Dictionary<string, object>>()
                .Select(kvp => $"{kvp.Key}: {kvp.Value}");
            return string.Join(Environment.NewLine, valueStrings);
        }

        private void WriteHyperQueryIfMatch(LogLine logLine, NativeJsonLogsBaseEvent jsonEvent)
        {
            if (!_eventTypes.Contains(jsonEvent.EventType))
            {
                return;
            }
            
            var payload = jsonEvent.EventPayload;
            var hyperQuery = new HyperEvent()
            {
                FileName = logLine.LogFileInfo.FileName,
                FilePath = logLine.LogFileInfo.FilePath,
                Line = logLine.LineNumber,

                Key = jsonEvent.EventType,
                ProcessId = jsonEvent.ProcessId,
                RequestId = jsonEvent.RequestId,
                SessionId = jsonEvent.SessionId,
                Severity = jsonEvent.Severity,
                Site = jsonEvent.Site,
                ThreadId = jsonEvent.ThreadId,
                Timestamp = jsonEvent.Timestamp,
                User = jsonEvent.Username,
                Worker = logLine.LogFileInfo.Worker,

                // *-end, *-release
                Elapsed = payload["elapsed"]?.ToObject<double>() ?? default(double?),

                // query-end, query-end-cancelled
                ClientSessionId = payload["client-session-id"]?.ToString(),
                Columns = payload["cols"]?.ToObject<double>() ?? default(double?),
                CopyDataSize = payload["copydata-size"]?.ToObject<int>() ?? default(int?),
                CopyDataTime = payload["copydata-time"]?.ToObject<double>() ?? default(double?),
                ExclusiveExecution = payload["ExclusiveExecution"]?.ToObject<bool>() ?? default(bool?),
                LockAcquisitionTime = payload["lock-acquisition-time"]?.ToObject<double>() ?? default(double?),
                PeakResultBufferMemoryMb = payload["peak-result-buffer-memory-mb"]?.ToObject<double>() ?? default(double?),
                PeakTransactionMemoryMb = payload["peak-transaction-memory-mb"]?.ToObject<double>() ?? default(double?),
                PlanCacheHitCount = payload["plan-cache-hit-count"]?.ToObject<double>() ?? default(double?),
                PlanCacheStatus = payload["plan-cache-status"]?.ToString(),
                QueryCompilationTime =
                    payload["compilation-time"]?.ToObject<double>()
                    ?? payload["query-compilation-time"]?.ToObject<double>()
                    ?? default(double?),
                QueryExecutionTime =
                    payload["execution-time"]?.ToObject<double>()
                    ?? payload["query-execution-time"]?.ToObject<double>()
                    ?? default(double?),
                QueryParsingTime =
                    payload["parsing-time"]?.ToObject<double>()
                    ?? payload["query-parsing-time"]?.ToObject<double>()
                    ?? default(double?),
                QueryTrunc = payload["query-trunc"]?.ToString(),
                ResultSizeMb = payload["result-size-mb"]?.ToObject<double>() ?? default(double?),
                Rows = payload["rows"]?.ToObject<double>() ?? default(double?),
                Spooling = payload["spooling"]?.ToObject<bool>() ?? default(bool?),
                StatementId = payload["statement-id"]?.ToString(),
                TimeToSchedule = payload["time-to-schedule"]?.ToObject<double>() ?? default(double?),
                TransactionId = payload["transaction-id"]?.ToString(),
                TransactionVisibleId = payload["transaction-visible-id"]?.ToString(),
                ExecThreadsCpuTime = payload["exec-threads"]?["cpu-time"]?.ToObject<double>() ?? default(double?),
                ExecThreadsWaitTime = payload["exec-threads"]?["wait-time"]?.ToObject<double>() ?? default(double?),
                ExecThreadsTotalTime = payload["exec-threads"]?["total-time"]?.ToObject<double>() ?? default(double?),
                StorageAccessTime = payload["exec-threads"]?["storage"]?["access-time"]?.ToObject<double>() ?? default(double?),
                StorageAccessCount = payload["exec-threads"]?["storage"]?["access-count"]?.ToObject<int>() ?? default(int?),
                StorageAccessBytes = payload["exec-threads"]?["storage"]?["access-bytes"]?.ToObject<long>() ?? default(long?),
                StorageWriteTime = payload["exec-threads"]?["storage"]?["write-time"]?.ToObject<double>() ?? default(double?),
                StorageWriteCount = payload["exec-threads"]?["storage"]?["write-count"]?.ToObject<int>() ?? default(int?),
                StorageWriteBytes = payload["exec-threads"]?["storage"]?["write-bytes"]?.ToObject<long>() ?? default(long?),

                // connection-startup-begin
                DbUser = payload["db-user"]?.ToString(),
                Options = payload["options"]?.ToString(),

                // connection-startup-end
                ElapsedInterpretOptions = payload["elapsed-interpret-options"]?.ToObject<double>() ?? default(double?),
                ElapsedCheckUser = payload["elapsed-check-user"]?.ToObject<double>() ?? default(double?),
                ElapsedCheckAuthentication = payload["elapsed-check-authentication"]?.ToObject<double>() ?? default(double?),
                HaveCred = payload["have-cred"]?.ToObject<bool>() ?? default(bool?),
                CredName = payload["cred-name"]?.ToString(),

                // cancel-request-received
                Id = payload["id"]?.ToObject<int>() ?? default(int?),
                Secret = payload["secret"]?.ToObject<long>() ?? default(long?),

                // connection-close-request
                Reason = payload["reason"]?.ToString(),

                // dbregistry-*
                NewRefCount = payload["new-ref-count"]?.ToObject<int>() ?? default(int?),
                Error = payload["error"]?.ToString(),

                // dbregistry-load
                CanonicalPath = payload["canonical-path"]?.ToString(),
                PathGiven = payload["path-given"]?.ToString(),
                ElapseRegistryInsert = payload["elapsed-registry-insert"]?.ToObject<double>() ?? default(double?),
                AlreadyLoaded = payload["already-loaded"]?.ToObject<bool>() ?? default(bool?),
                Reopen = payload["reopen"]?.ToObject<bool>() ?? default(bool?),
                LoadSuccess = payload["load-success"]?.ToObject<bool>() ?? default(bool?),
                DatabaseUuid = payload["database-uuid"]?.ToString(),

                // dbregistry-release
                Saved = payload["saved"]?.ToObject<bool>() ?? default(bool?),
                FailedOnLoad = payload["failed-on-load"]?.ToObject<bool>() ?? default(bool?),
                WasUnloaded = payload["was-unloaded"]?.ToObject<bool>() ?? default(bool?),
                WasDropped = payload["was-dropped"]?.ToObject<bool>() ?? default(bool?),
                ElapsedSave = payload["elapsed-save"]?.ToObject<double>() ?? default(double?),
                Closed = payload["closed"]?.ToObject<bool>() ?? default(bool?),
                ElapsedRegistryClose = payload["elapsed-registry-close"]?.ToObject<double>() ?? default(double?),

                // query-result-sent
                Success = payload["success"]?.ToObject<bool>() ?? default(bool?),
                TimeSinceQueryEnd = payload["time-since-query-end"]?.ToObject<double>() ?? default(double?),
                TransferredVolumeMb = payload["transferred-volume-mb"]?.ToObject<double>() ?? default(double?),

                // tcp-ip-client-allowed, tcp-ip-client-rejected
                RemoteAddress = payload["remote-address"]?.ToString(),

                // query-plan-slow, query-plan-spooling, query-plan-cancelled
                Plan = payload["plan"]?.ToString(),

                // startup-info
                CommandLine = payload["command-line"]?.ToString(),
                ServerVersion = payload["server-version"]?.ToString(),
                BuildVersion = payload["build-version"]?.ToString(),
                BuildType = payload["build-type"]?.ToString(),
                BuildCpuFeatures = payload["build-cpu-features"]?.ToString(),
                NetworkThreads = payload["network-threads"]?.ToObject<int>() ?? default(int?),
                ParentPid = payload["parent-pid"]?.ToObject<int>() ?? default(int?),
                MinProtocolVersion = payload["min-protocol-version"]?.ToObject<int>() ?? default(int?),
                MaxProtocolVersion = payload["max-protocol-version"]?.ToObject<int>() ?? default(int?),

                // resource-stats
                VirtualTotalMb = payload["memory"]?["virtual"]?["total-mb"]?.ToObject<long>() ?? default(long?),
                VirtualSystemMb = payload["memory"]?["virtual"]?["system-mb"]?.ToObject<long>() ?? default(long?),
                VirtualProcessMb = payload["memory"]?["virtual"]?["process-mb"]?.ToObject<long>() ?? default(long?),
                PhysicalTotalMb = payload["memory"]?["physical"]?["total-mb"]?.ToObject<long>() ?? default(long?),
                PhysicalSystemMb = payload["memory"]?["physical"]?["system-mb"]?.ToObject<long>() ?? default(long?),
                PhysicalProcessMb = payload["memory"]?["physical"]?["process-mb"]?.ToObject<long>() ?? default(long?),
                GlobalCurrentMb = payload["mem-tracker"]?["global"]["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalPeakMb = payload["mem-tracker"]?["global"]["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalNetworkReadbufferCurrentMb = payload["mem-tracker"]?["global_network_readbuffer"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalNetworkReadbufferPeakMb = payload["mem-tracker"]?["global_network_readbuffer"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalNetworkWriteBufferCurrentMb = payload["mem-tracker"]?["global_network_writebuffer"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalNetworkWriteBufferPeakMb = payload["mem-tracker"]?["global_network_writebuffer"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalStringpoolCurrentMb = payload["mem-tracker"]?["global_stringpool"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalStringpoolPeakMb = payload["mem-tracker"]?["global_stringpool"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalTransactionsCurrentMb = payload["mem-tracker"]?["global_transactions"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalTransactionsPeakMb = payload["mem-tracker"]?["global_transactions"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalLockedCurrentMb = payload["mem-tracker"]?["global_locked"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalLockedPeakMb = payload["mem-tracker"]?["global_locked"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalTupleDataCurrentMb = payload["mem-tracker"]?["global_tuple_data"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalTupleDataPeakMb = payload["mem-tracker"]?["global_tuple_data"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalPlanCacheCurrentMb = payload["mem-tracker"]?["global_plan_cache"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalPlanCachePeakMb = payload["mem-tracker"]?["global_plan_cache"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalExternalTableCacheCurrentMb = payload["mem-tracker"]?["global_external_table_cache"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalExternalTableCachePeakMb = payload["mem-tracker"]?["global_external_table_cache"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskNetworkReadbufferCurrentMb = payload["mem-tracker"]?["global_disk_network_readbuffer"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskNetworkReadbufferPeakMb = payload["mem-tracker"]?["global_disk_network_readbuffer"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskNetworkWritebufferCurrentMb = payload["mem-tracker"]?["global_disk_network_writebuffer"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskNetworkWritebufferPeakMb = payload["mem-tracker"]?["global_disk_network_writebuffer"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskStringpoolCurrentMb = payload["mem-tracker"]?["global_disk_stringpool"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskStringpoolPeakMb = payload["mem-tracker"]?["global_disk_stringpool"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskTransactionCurrentMb = payload["mem-tracker"]?["global_disk_transaction"]?["current-mb"]?.ToObject<long>() ?? default(long?),
                GlobalDiskTransactionPeakMb = payload["mem-tracker"]?["global_disk_transaction"]?["peak-mb"]?.ToObject<long>() ?? default(long?),
            };

            _hyperQueryWriter.AddLine(hyperQuery);
        }

        public SinglePluginExecutionResults CompleteProcessing()
        {
            var writersLineCounts = new List<WriterLineCounts>
            {
                _hyperErrorWriter.Close(),
                _hyperQueryWriter.Close()
            };
            return new SinglePluginExecutionResults(writersLineCounts);
        }

        public void Dispose()
        {
            _hyperErrorWriter?.Dispose();
            _hyperQueryWriter?.Dispose();
        }
    }
}
