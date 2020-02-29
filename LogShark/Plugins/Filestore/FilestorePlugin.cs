using System.Collections.Generic;
using System.Text.RegularExpressions;
using LogShark.Containers;
using LogShark.Extensions;
using LogShark.Plugins.Shared;
using LogShark.Writers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LogShark.Plugins.Filestore
{
    public class FilestorePlugin : IPlugin
    {
        private static readonly DataSetInfo OutputInfo = new DataSetInfo("Filestore", "Filestore");
        private static readonly List<LogType> ConsumedLogTypesStatic = new List<LogType>{ LogType.Filestore };

        public IList<LogType> ConsumedLogTypes => ConsumedLogTypesStatic;
        public string Name => "Filestore";

        private IWriter<FilestoreEvent> _writer;
        private IProcessingNotificationsCollector _processingNotificationsCollector;

        private readonly Regex _regex = 
            new Regex(@"^
                        (?<ts>\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}.\d{3})\s
                        (?<ts_offset>.+?)\s
                        (?<thread>.*?)\s+
                        (?<sev>[A-Z]+)(\s+)
                        :\s
                        (?<class>.*?)\s-\s
                        (?<message>(.|\n)*)",
            RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace | RegexOptions.Compiled);
        
        public void Configure(IWriterFactory writerFactory, IConfiguration pluginConfig, IProcessingNotificationsCollector processingNotificationsCollector, ILoggerFactory loggerFactory)
        {
            _writer = writerFactory.GetWriter<FilestoreEvent>(OutputInfo);
            _processingNotificationsCollector = processingNotificationsCollector;
        }

        public void ProcessLogLine(LogLine logLine, LogType logType)
        {
            var @event = ParseEvent(logLine);
            
            if (@event == null)
            {
                _processingNotificationsCollector.ReportError("Failed to parse Filestore event from log line", logLine, nameof(FilestorePlugin));
                return;
            }
            
            _writer.AddLine(@event);
        }

        public SinglePluginExecutionResults CompleteProcessing()
        {
            var writerStatistics = _writer.Close();
            return new SinglePluginExecutionResults(writerStatistics);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }

        private FilestoreEvent ParseEvent(LogLine logLine)
        {
            var match = logLine.LineContents.CastToStringAndRegexMatch(_regex);
            if (match == null || !match.Success)
            {
                return null;
            }
            
            return new FilestoreEvent(
                logLine: logLine,
                timestamp: TimestampParsers.ParseJavaLogsTimestamp(match.GetString("ts")), 
                severity: match.GetString("sev"),
                message: match.GetString("message"),
                @class: match.GetString("class")
            );
        }
    }
}