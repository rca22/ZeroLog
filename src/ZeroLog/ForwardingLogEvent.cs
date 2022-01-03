﻿using System;
using System.Text.Formatting;
using System.Threading;
using ZeroLog.Appenders;

namespace ZeroLog
{
    internal partial class ForwardingLogEvent : IInternalLogEvent
    {
        private readonly IInternalLogEvent _logEventToAppend;
        private readonly Log _log;

        public Level Level => _logEventToAppend.Level;
        public DateTime Timestamp => default;
        public Thread? Thread => null;
        public string Name => _logEventToAppend.Name;
        public IAppender[] Appenders => _log.Appenders;

        public ForwardingLogEvent(IInternalLogEvent logEventToAppend, Log log)
        {
            _logEventToAppend = logEventToAppend;
            _log = log;
        }

        public void Initialize(Level level, Log log, LogEventArgumentExhaustionStrategy argumentExhaustionStrategy)
        {
        }

        public ILogEvent Append(string? s) => this;
        public ILogEvent AppendAsciiString(byte[]? bytes, int length) => this;
        public unsafe ILogEvent AppendAsciiString(byte* bytes, int length) => this;
        public ILogEvent AppendAsciiString(ReadOnlySpan<byte> bytes) => this;
        public ILogEvent AppendAsciiString(ReadOnlySpan<char> chars) => this;
        public ILogEvent AppendKeyValue(string key, string? value) => this;
        public ILogEvent AppendKeyValueAscii(string key, byte[]? bytes, int length) => this;
        public unsafe ILogEvent AppendKeyValueAscii(string key, byte* bytes, int length) => this;
        public ILogEvent AppendKeyValueAscii(string key, ReadOnlySpan<byte> bytes) => this;
        public ILogEvent AppendKeyValueAscii(string key, ReadOnlySpan<char> chars) => this;

        public ILogEvent AppendKeyValue<T>(string key, T value)
            where T : struct, Enum
        {
            return this;
        }

        public ILogEvent AppendKeyValue<T>(string key, T? value)
            where T : struct, Enum
        {
            return this;
        }

        public ILogEvent AppendEnum<T>(T value)
            where T : struct, Enum
        {
            return this;
        }

        public ILogEvent AppendEnum<T>(T? value)
            where T : struct, Enum
        {
            return this;
        }

        public void Log()
        {
            _log.Enqueue(_logEventToAppend);
        }

        public void WriteToStringBuffer(StringBuffer stringBuffer, KeyValuePointerBuffer keyValuePointerBuffer)
        {
        }

        public void SetTimestamp(DateTime timestamp)
        {
        }

        public void WriteToStringBufferUnformatted(StringBuffer stringBuffer)
        {
        }

        public bool IsPooled => false;
    }
}
