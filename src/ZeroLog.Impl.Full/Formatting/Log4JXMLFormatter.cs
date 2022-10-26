using System.Xml;
using System;

namespace ZeroLog.Formatting;

/// <summary>
/// Formatter of messages into the Log4J XML format
/// </summary>
/// <remarks>
/// Logging an exception will allocate.
/// </remarks>
public sealed class Log4JXMLFormatter : Formatter
{
    const string msgHeaderPart = "<log4j:event logger=\"";
    const string msgLevelPart = "\" level=\"";
    const string msgThreadPart = "\" thread=\"";
    const string msgTimestampPart = "\" timestamp=\"";
    const string msgMidPart = "\"><log4j:message>";
    const string endMessageTag = "</log4j:message>";
    const string openThrowableTag = "<log4j:throwable>";
    const string endThrowableTag = "</log4j:throwable>";
    const string endEventTag = "</log4j:event>";

    private void WriteSafeString(ReadOnlySpan<char> text)
    {
        Span<char> span = stackalloc char[text.Length];
        var cnt = 0;
        for (int i = 0; i < text.Length; ++i)
        {
            if (XmlConvert.IsXmlChar(text[i]))
            {
                span[cnt++] = text[i];
            }
        }
        Write(span[..cnt]);
    }
    private void Write(long value)
    {
        var span = GetRemainingBuffer();
        value.TryFormat(span, out int charsWritten);
        AdvanceBy(charsWritten);
    }

    private static string LevelString(LogLevel level)
    {
        switch(level)
        {
            case LogLevel.Trace: return "TRACE";
            case LogLevel.Debug: return "DEBUG";
            case LogLevel.Info: return "INFO";
            case LogLevel.Warn: return "WARN";
            case LogLevel.Error: return "ERROR";
            case LogLevel.Fatal: return "FATAL";
            case LogLevel.None: return "NONE";
            default: throw new ArgumentOutOfRangeException(nameof(level),"Unexpected value of LogLevel");
        }
    }

    /// <inheritdoc/>
    protected override void WriteMessage(LoggedMessage message)
    {
        Write(msgHeaderPart);
        Write(message.LoggerName ?? "");
        Write(msgLevelPart);
        Write(LevelString(message.Level));
        Write(msgThreadPart);
        Write(message.Thread?.ManagedThreadId ?? 0);
        Write(msgTimestampPart);
        Write((new System.DateTimeOffset(message.Timestamp)).ToUnixTimeMilliseconds());
        Write(msgMidPart);
        WriteSafeString(message.Message);
        Write(endMessageTag);
        if (message.Exception != null)
        {
            Write(openThrowableTag);
            // This allocates, but there's no better way to get the details.
            WriteSafeString(message.Exception.ToString());
            Write(endThrowableTag);
        }
        Write(endEventTag);
    }
}
