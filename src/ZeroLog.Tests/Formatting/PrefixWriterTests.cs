using System;
using System.Globalization;
using System.Threading;
using NUnit.Framework;
using ZeroLog.Configuration;
using ZeroLog.Formatting;
using ZeroLog.Tests.Support;

namespace ZeroLog.Tests.Formatting;

[TestFixture]
public class PrefixWriterTests
{
    [Test]
    [TestCase("", "")]
    [TestCase("foo", "foo")]
    [TestCase("%date", "2020-01-02")]
    [TestCase("%time", "03:04:05.0060000")]
    [TestCase("%level", "INFO")]
    [TestCase("%logger", "TestLog")]
    [TestCase("foo %level bar %logger baz", "foo INFO bar TestLog baz")]
    [TestCase("%level %level", "INFO INFO")]
    [TestCase("%LEVEL", "INFO")]
    [TestCase("%{level}", "INFO")]
    [TestCase("%{ level  }", "INFO")]
    [TestCase("foo%{level}Bar", "fooINFOBar")]
    [TestCase("foo%{level}", "fooINFO")]
    [TestCase("foo%level", "fooINFO")]
    [TestCase("%{level}Bar", "INFOBar")]
    [TestCase("%threads", "%threads")]
    [TestCase("%{level}%{logger}", "INFOTestLog")]
    [TestCase("%FOO", "%FOO")]
    [TestCase("%{foo}", "%{foo}")]
    [TestCase("%foo%bar", "%foo%bar")]
    [TestCase("%foo%bar%level%baz", "%foo%barINFO%baz")]
    [TestCase("<%foo>%bar|", "<%foo>%bar|")]
    public void should_write_prefix(string pattern, string expectedResult)
    {
        var prefixWriter = new PrefixWriter(pattern);

        var logMessage = new LogMessage("Foo");
        logMessage.Initialize(new Log("TestLog"), LogLevel.Info);
        logMessage.Timestamp = new DateTime(2020, 01, 02, 03, 04, 05, 06);

        var result = GetResult(prefixWriter, logMessage);
        result.ShouldEqual(expectedResult);
    }

    [Test, RequiresThread]
    public void should_write_thread_name()
    {
        Thread.CurrentThread.Name = "Hello";

        var prefixWriter = new PrefixWriter("%thread world!");

        var logMessage = new LogMessage("Foo");
        logMessage.Initialize(null, LogLevel.Info);

        var result = GetResult(prefixWriter, logMessage);
        result.ShouldEqual("Hello world!");
    }

    [Test, RequiresThread]
    public void should_write_thread_id()
    {
        var prefixWriter = new PrefixWriter("%thread");

        var logMessage = new LogMessage("Foo");
        logMessage.Initialize(null, LogLevel.Info);

        var result = GetResult(prefixWriter, logMessage);
        result.ShouldEqual(Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
    }

    [Test, RequiresThread]
    public void should_write_zero_when_no_thread_provided()
    {
        var prefixWriter = new PrefixWriter("%thread");

        var logMessage = new LogMessage("Foo");

        var result = GetResult(prefixWriter, logMessage);
        result.ShouldEqual("0");
    }

    private static string GetResult(PrefixWriter prefixWriter, LogMessage logMessage)
    {
        var buffer = new char[256];
        var formattedLogMessage = new LoggedMessage(256, ZeroLogConfiguration.Default);
        formattedLogMessage.SetMessage(logMessage);
        prefixWriter.WritePrefix(formattedLogMessage, buffer, out var charsWritten);
        return buffer.AsSpan(0, charsWritten).ToString();
    }
}
