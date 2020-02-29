using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using LogShark.Extensions;
using Xunit;

namespace LogShark.Tests.Extensions
{
    public class StringExtensionsTests : InvariantCultureTestsBase
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(@"log.txt", "worker0")]
        [InlineData(@"Log/log.txt", "worker0")]
        [InlineData(@"httpd/access.2018_07_23_00_00_00.log", "worker0")]
        [InlineData(@"worker3.zip/httpd/access.2018_07_23_00_00_00.log", "worker3")]
        [InlineData(@"worker3/httpd/access.2018_07_23_00_00_00.log", "worker3")]
        [InlineData(@"worker1/vizqlserver/logs/backgrounder_0-0_2018_07_23_06_58_20.txt", "worker1")]
        [InlineData(@"localhost/tabadminagent_0.20181.18.0404.16052600117725665315795/logs/backgrounder/backgrounder_node1-1.log", "localhost")]
        [InlineData(@"my-awesome-server/tabadminagent_0.20181.18.0404.16052600117725665315795/logs/backgrounder/backgrounder_node1-1.log", "my-awesome-server")]
        [InlineData(@"my-awesome-server.domain.com/tabadminagent_0.20181.18.0404.16052600117725665315795/logs/backgrounder/backgrounder_node1-1.log", "my-awesome-server.domain.com")]
        [InlineData(@"node1\filestore_0.20182.18.0627.22302895224363938766334\logs\filestore.log", "node1")]
        [InlineData(@"node2/backgrounder_0.20182.18.0627.22306436150448756480580/logs/backgrounder_node2-1.log.2018-08-08", "node2")]
        public void GetWorkerIdFromFilePath(string input, string expectedResult)
        {
            var result = input.GetWorkerIdFromFilePath();
            result.Should().Be(expectedResult);
        }

        [Theory]
        [InlineData(@"test/folder\folder1\file.log", @"test/folder", @"folder1/file.log")] // This is how it often looks in Windows
        [InlineData(@"test\folder\inner", "test", @"folder/inner")]
        [InlineData(@"test/folder/inner", "test", @"folder/inner")]
        [InlineData(@"test\folder\inner", @"test\folder", @"inner")]
        [InlineData(@"test/folder/inner", @"test\folder", @"test/folder/inner")] // doesn't start with root path
        [InlineData(@"test\folder\inner", @"test/folder", @"test/folder/inner")] // doesn't start with root path
        [InlineData(@"test/folder/inner", @"test/folder", @"inner")]
        [InlineData(@"test\folder\innerFile.txt", @"test\folder", @"innerFile.txt")]
        [InlineData(@"test\folder\inner\file.txt", @"test\folder", @"inner/file.txt")]
        [InlineData(@"otherTest\otherFolder\inner", @"test\folder", @"otherTest/otherFolder/inner")]
        public void NormalizePath(string input, string rootPath, string expectedResult)
        {
            var result = input.NormalizePath(rootPath);
            result.Should().Be(expectedResult);
        }
        
        [Theory]
        [InlineData("abc", "def", null, true, false)]
        [InlineData("abc", "def", "", true, false)]
        [InlineData("abc", "def", "ghi", true, false)]
        [InlineData("bc", "def", "abc", false, false)]
        [InlineData("abc", "ef", "def", false, true)]
        public void GetRegexMatchAndMoveCorrectRegexUpFront(string regexPattern1, string regexPattern2, string input, bool isNull, bool wereSwapped)
        {
            var regex1 = new Regex(regexPattern1);
            var regex2 = new Regex(regexPattern2);
            var regexList = new List<Regex> {regex1, regex2};
            var expectedRegexList = wereSwapped 
                ? new List<Regex> {regex2, regex1}
                : new List<Regex> {regex1, regex2};
            
            var match = input?.GetRegexMatchAndMoveCorrectRegexUpFront(regexList);
            
            if (isNull)
            {
                match.Should().BeNull();
            }
            else
            {
                match.Success.Should().Be(true);
            }

            regexList.Should().Equal(expectedRegexList);
        }

        [Theory]
        [InlineData("\"test\"", "test")]
        [InlineData("test", "test")]
        [InlineData("\"test\"\"", "test\"")]
        [InlineData("\"test", "test")]
        [InlineData("test\"", "test")]
        [InlineData("t\"es\"t", "t\"es\"t")]
        public void TrimSurroundingDoubleQuotes(string input, string expected)
        {
            var actual = input.TrimSurroundingDoubleQuotes();
            actual.Should().Be(expected);
        }
    }
}