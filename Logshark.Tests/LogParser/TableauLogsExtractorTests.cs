using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using LogShark.LogParser;
using LogShark.LogParser.Containers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LogShark.Tests.LogParser
{
    public class TableauLogsExtractorTests : InvariantCultureTestsBase, IDisposable
    {
        private const string UnzippedTestSet = "TestData/TableauLogsExtractorTest";
        private const string ZippedTestSet = "TestData/TableauLogsExtractorTest.zip";
        private const string TempDir = "TestTemp";

        private readonly ILogger _logger = new NullLoggerFactory().CreateLogger<TableauLogsExtractor>();
        private readonly ProcessingNotificationsCollector _processingNotificationsCollector;

        public TableauLogsExtractorTests()
        {
            DeleteTempDir();
            _processingNotificationsCollector = new ProcessingNotificationsCollector(10);
        }

        [Fact]
        public void EvaluateUnzipped()
        {
            Directory.Exists(TempDir).Should().Be(false);

            using (var extractor = new TableauLogsExtractor(UnzippedTestSet, TempDir, _processingNotificationsCollector, _logger))
            {
                var expectedParts = new HashSet<LogSetInfo>
                {
                    new LogSetInfo(UnzippedTestSet, string.Empty, false, UnzippedTestSet),
                    new LogSetInfo($"{UnzippedTestSet}/worker1.zip", "worker1", true, UnzippedTestSet),
                    new LogSetInfo($"{UnzippedTestSet}/localhost/tabadminagent_0.20181.18.0404.16052600117725665315795.zip", "localhost/tabadminagent_0.20181.18.0404.16052600117725665315795", true, UnzippedTestSet)
                };

                extractor.LogSetParts.Should().BeEquivalentTo(expectedParts);
                Directory.Exists(TempDir).Should().Be(false);
            }
            _processingNotificationsCollector.TotalErrorsReported.Should().Be(0);
        }

        [Fact]
        public void EvaluateZipped()
        {
            Directory.Exists(TempDir).Should().Be(false);
            
            using (var extractor = new TableauLogsExtractor(ZippedTestSet, TempDir, _processingNotificationsCollector, _logger))
            {
                var expectedParts = new HashSet<LogSetInfo>
                {
                    new LogSetInfo(ZippedTestSet, string.Empty, true, ZippedTestSet),
                    new LogSetInfo($"{TempDir}/NestedZipFiles/worker1.zip", "worker1", true, ZippedTestSet),
                    new LogSetInfo($"{TempDir}/NestedZipFiles/tabadminagent_0.20181.18.0404.16052600117725665315795.zip", "localhost/tabadminagent_0.20181.18.0404.16052600117725665315795", true, ZippedTestSet)
                };

                extractor.LogSetParts.Should().BeEquivalentTo(expectedParts);
                Directory.Exists(TempDir).Should().Be(true);
                
                File.Exists($"{TempDir}/NestedZipFiles/worker1.zip").Should().Be(true);
                File.Exists($"{TempDir}/NestedZipFiles/tabadminagent_0.20181.18.0404.16052600117725665315795.zip").Should().Be(true);
            }

            Directory.Exists($"{TempDir}/NestedZipFiles").Should().Be(false);
            Directory.Exists(TempDir).Should().Be(true);
            _processingNotificationsCollector.TotalErrorsReported.Should().Be(0);
        }

        [Fact]
        public void FileOrDirDoNotExist()
        {
            const string badFileName = "IDoNotExist.zip";
            File.Exists(badFileName).Should().Be(false);
            
            Action testAction = () => new TableauLogsExtractor(badFileName, TempDir, _processingNotificationsCollector, _logger);

            testAction.Should().Throw<ArgumentException>();
            _processingNotificationsCollector.TotalErrorsReported.Should().Be(0);
        }
        
        public void Dispose()
        {
            DeleteTempDir();
        }

        private static void DeleteTempDir()
        {
            if (Directory.Exists(TempDir))
            {
                Directory.Delete(TempDir, true);
            }
        }
    }
}