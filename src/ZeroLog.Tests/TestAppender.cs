using System.Collections.Generic;
using System.Threading;
using ZeroLog.Appenders;
using ZeroLog.Formatting;

namespace ZeroLog.Tests;

public class TestAppender : Appender
{
    private readonly bool _captureLoggedMessages;
    private int _messageCount;
    private ManualResetEventSlim _signal;
    private int _messageCountTarget;

    public List<string> LoggedMessages { get; } = new();
    public int FlushCount { get; private set; }
    public bool IsDisposed { get; private set; }

    public ManualResetEventSlim WaitOnWriteEvent { get; set; }

    public TestAppender(bool captureLoggedMessages)
    {
        _captureLoggedMessages = captureLoggedMessages;
    }

    public ManualResetEventSlim SetMessageCountTarget(int expectedMessageCount)
    {
        _signal = new ManualResetEventSlim(false);
        _messageCount = 0;
        _messageCountTarget = expectedMessageCount;
        return _signal;
    }

    public override void WriteMessage(LoggedMessage message)
    {
        if (_captureLoggedMessages)
            LoggedMessages.Add(message.ToString());

        if (++_messageCount == _messageCountTarget)
            _signal.Set();

        WaitOnWriteEvent?.Wait();
    }

    public override void Flush()
    {
        base.Flush();

        ++FlushCount;
    }

    public override void Dispose()
    {
        base.Dispose();

        IsDisposed = true;
    }
}
