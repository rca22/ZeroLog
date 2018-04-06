using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Formatting;
using System.Threading;
using ExtraConstraints;
using ZeroLog.Appenders;
using ZeroLog.Utils;

namespace ZeroLog
{
    internal unsafe partial class LogEvent : IInternalLogEvent
    {
        private const int _stringCapacity = 10;

        private readonly List<string> _strings = new List<string>(_stringCapacity);
        private readonly List<IntPtr> _argPointers = new List<IntPtr>(_stringCapacity);
        private Log _log;

        protected readonly byte* _startOfBuffer;
        protected readonly byte* _endOfBuffer;
        protected byte* _dataPointer;

        public LogEvent(BufferSegment bufferSegment)
        {
            _startOfBuffer = bufferSegment.Data;
            _dataPointer = bufferSegment.Data;
            _endOfBuffer = bufferSegment.Data + bufferSegment.Length;
        }

        public Level Level { get; private set; }
        public DateTime Timestamp { get; private set; }
        public int ThreadId { get; private set; }
        public string Name => _log.Name;
        public IList<IAppender> Appenders => _log.Appenders;
        public virtual bool IsPooled => true;

        public void Initialize(Level level, Log log)
        {
            Timestamp = SystemDateTime.UtcNow;
            Level = level;
            _log = log;
            _strings.Clear();
            _argPointers.Clear();
            _dataPointer = _startOfBuffer;
            ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormat(string format)
        {
            if (!HasEnoughBytes(1))
                return;

            AppendArgumentType(ArgumentType.FormatString);
            AppendString(format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendGeneric<T>(T arg)
        {
            // Some remarks here:
            // - The JIT knows the type of "arg" at runtime and will be able the remove useless branches for each
            //   struct specific jitted version of this method.
            // - Since a jitted version of this method will be shared for all reference types, the optimisation
            //   we just mentionned earlier can't occur. That's why we put the test against string at the top.
            // - Casting to "object" then to the desired value type will force the C# compiler to emit boxing and 
            //   unboxing IL opcodes, but the JIT is smart enough to prevent the actual boxing/unboxing from happening.

            if (typeof(T) == typeof(string))
                Append((string)(object)arg);

            else if (typeof(T) == typeof(bool))
                Append((bool)(object)arg);

            else if (typeof(T) == typeof(byte))
                Append((byte)(object)arg);

            else if (typeof(T) == typeof(char))
                Append((char)(object)arg);

            else if (typeof(T) == typeof(short))
                Append((short)(object)arg);

            else if (typeof(T) == typeof(int))
                Append((int)(object)arg);

            else if (typeof(T) == typeof(long))
                Append((long)(object)arg);

            else if (typeof(T) == typeof(float))
                Append((float)(object)arg);

            else if (typeof(T) == typeof(double))
                Append((double)(object)arg);

            else if (typeof(T) == typeof(decimal))
                Append((decimal)(object)arg);

            else if (typeof(T) == typeof(Guid))
                Append((Guid)(object)arg);

            else if (typeof(T) == typeof(DateTime))
                Append((DateTime)(object)arg);

            else if (typeof(T) == typeof(TimeSpan))
                Append((TimeSpan)(object)arg);

            else if (typeof(T).IsEnum)
                AppendEnumInternal(arg);

            else
                throw new NotSupportedException($"Type {typeof(T)} is not supported ");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogEvent Append(string s)
        {
            if (!HasEnoughBytes(sizeof(ArgumentType)))
                return this;

            AppendArgumentType(ArgumentType.String);
            AppendString(s);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogEvent AppendAsciiString(byte[] bytes, int length)
        {
            var remainingBytes = (int)(_endOfBuffer - _dataPointer);
            remainingBytes -= sizeof(ArgumentType) + sizeof(byte);
            if (remainingBytes <= 0)
                return this;

            length = Math.Min(length, remainingBytes);

            AppendArgumentType(ArgumentType.AsciiString);
            AppendInt32(length);
            AppendBytes(bytes, length);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogEvent AppendAsciiString(byte* bytes, int length)
        {
            var remainingBytes = (int)(_endOfBuffer - _dataPointer);
            remainingBytes -= sizeof(ArgumentType) + sizeof(byte);
            if (remainingBytes <= 0)
                return this;

            length = Math.Min(length, remainingBytes);

            AppendArgumentType(ArgumentType.AsciiString);
            AppendInt32(length);
            AppendBytes(bytes, length);
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogEvent AppendEnum<[EnumConstraint] T>(T value)
            where T : struct
        {
            return AppendEnumInternal(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ILogEvent AppendEnum<[EnumConstraint] T>(T? value)
            where T : struct
        {
            if (value == null)
            {
                if (HasEnoughBytes(sizeof(ArgumentType)))
                    AppendArgumentType(ArgumentType.Null);

                return this;
            }

            return AppendEnumInternal(value.GetValueOrDefault());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ILogEvent AppendEnumInternal<T>(T value)
        {
            if (!HasEnoughBytes(sizeof(ArgumentType) + sizeof(EnumArg)))
                return this;

            AppendArgumentType(ArgumentType.Enum);
            *(EnumArg*)_dataPointer = new EnumArg(TypeUtil.GetTypeHandle<T>(), EnumCache.ToUInt64(value));
            _dataPointer += sizeof(EnumArg);
            return this;
        }

        public void Log()
        {
            _log.Enqueue(this);
        }

        public void WriteToStringBuffer(StringBuffer stringBuffer)
        {
            var endOfData = _dataPointer;
            var dataPointer = _startOfBuffer;

            while (dataPointer < endOfData)
            {
                stringBuffer.Append(ref dataPointer, StringView.Empty, _strings, _argPointers);
            }

            Debug.Assert(dataPointer == endOfData, "Buffer over-read");
        }

        public void WriteToStringBufferUnformatted(StringBuffer stringBuffer)
        {
            var endOfData = _dataPointer;
            var dataPointer = _startOfBuffer;

            while (dataPointer < endOfData)
            {
                AppendArgumentToStringBufferUnformatted(stringBuffer, ref dataPointer);

                if (dataPointer < endOfData)
                    stringBuffer.Append(", ");
            }

            Debug.Assert(dataPointer == endOfData, "Buffer over-read");
        }

        private void AppendArgumentToStringBufferUnformatted(StringBuffer stringBuffer, ref byte* dataPointer)
        {
            var argument = *dataPointer;
            dataPointer += sizeof(ArgumentType);

            var argumentType = (ArgumentType)(argument & ArgumentTypeMask.ArgumentType);

            var hasFormatSpecifier = (argument & ArgumentTypeMask.FormatSpecifier) != 0;
            if (hasFormatSpecifier)
                dataPointer += sizeof(byte); // Skip it

            switch (argumentType)
            {
                case ArgumentType.String:
                    stringBuffer.Append('"');
                    stringBuffer.Append(_strings[*dataPointer]);
                    stringBuffer.Append('"');
                    dataPointer += sizeof(byte);
                    break;

                case ArgumentType.AsciiString:
                    var length = *(int*)dataPointer;
                    dataPointer += sizeof(int);
                    stringBuffer.Append('"');
                    stringBuffer.Append(new AsciiString(dataPointer, length));
                    stringBuffer.Append('"');
                    dataPointer += length;
                    break;

                case ArgumentType.Boolean:
                    stringBuffer.Append(*(bool*)dataPointer);
                    dataPointer += sizeof(bool);
                    break;

                case ArgumentType.Byte:
                    stringBuffer.Append(*dataPointer, StringView.Empty);
                    dataPointer += sizeof(byte);
                    break;

                case ArgumentType.Char:
                    stringBuffer.Append('\'');
                    stringBuffer.Append(*(char*)dataPointer);
                    stringBuffer.Append('\'');
                    dataPointer += sizeof(char);
                    break;

                case ArgumentType.Int16:
                    stringBuffer.Append(*(short*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(short);
                    break;

                case ArgumentType.Int32:
                    stringBuffer.Append(*(int*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(int);
                    break;

                case ArgumentType.Int64:
                    stringBuffer.Append(*(long*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(long);
                    break;

                case ArgumentType.Single:
                    stringBuffer.Append(*(float*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(float);
                    break;

                case ArgumentType.Double:
                    stringBuffer.Append(*(double*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(double);
                    break;

                case ArgumentType.Decimal:
                    stringBuffer.Append(*(decimal*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(decimal);
                    break;

                case ArgumentType.Guid:
                    stringBuffer.Append(*(Guid*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(Guid);
                    break;

                case ArgumentType.DateTime:
                    stringBuffer.Append(*(DateTime*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(DateTime);
                    break;

                case ArgumentType.TimeSpan:
                    stringBuffer.Append(*(TimeSpan*)dataPointer, StringView.Empty);
                    dataPointer += sizeof(TimeSpan);
                    break;

                case ArgumentType.FormatString:
                    stringBuffer.Append('"');
                    stringBuffer.Append(_strings[*dataPointer]);
                    stringBuffer.Append('"');
                    dataPointer += sizeof(byte);
                    break;

                case ArgumentType.Enum:
                    var enumArg = (EnumArg*)dataPointer;
                    dataPointer += sizeof(EnumArg);
                    enumArg->AppendTo(stringBuffer);
                    break;

                case ArgumentType.Null:
                    stringBuffer.Append("null");
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasEnoughBytes(int requestedBytes)
            => _dataPointer + requestedBytes <= _endOfBuffer;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendArgumentType(ArgumentType argumentType)
        {
            _argPointers.Add(new IntPtr(_dataPointer));
            *_dataPointer = (byte)argumentType;
            _dataPointer += sizeof(byte);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AppendArgumentTypeWithFormat(ArgumentType argumentType)
        {
            _argPointers.Add(new IntPtr(_dataPointer));
            *_dataPointer = (byte)((byte)argumentType | ArgumentTypeMask.FormatSpecifier);
            _dataPointer += sizeof(byte);
        }

        private void AppendString(string value)
        {
            *_dataPointer = (byte)_strings.Count;
            _dataPointer += sizeof(byte);
            _strings.Add(value);
        }

        private void AppendBoolean(bool b)
        {
            *(bool*)_dataPointer = b;
            _dataPointer += sizeof(bool);
        }

        private void AppendByte(byte b)
        {
            *_dataPointer = b;
            _dataPointer += sizeof(byte);
        }

        private void AppendBytes(byte[] bytes, int length)
        {
            fixed (byte* b = bytes)
            {
                for (var i = 0; i < length; i++)
                {
                    *_dataPointer = b[i];
                    _dataPointer += sizeof(byte);
                }
            }
        }

        private void AppendBytes(byte* bytes, int length)
        {
            for (var i = 0; i < length; i++)
            {
                *_dataPointer = bytes[i];
                _dataPointer += sizeof(byte);
            }
        }

        private void AppendChar(char c)
        {
            *(char*)_dataPointer = c;
            _dataPointer += sizeof(char);
        }

        private void AppendInt16(short s)
        {
            *(short*)_dataPointer = s;
            _dataPointer += sizeof(short);
        }

        private void AppendInt32(int i)
        {
            *(int*)_dataPointer = i;
            _dataPointer += sizeof(int);
        }

        private void AppendInt64(long l)
        {
            *(long*)_dataPointer = l;
            _dataPointer += sizeof(long);
        }

        private void AppendSingle(float f)
        {
            *(float*)_dataPointer = f;
            _dataPointer += sizeof(float);
        }

        private void AppendDouble(double d)
        {
            *(double*)_dataPointer = d;
            _dataPointer += sizeof(double);
        }

        private void AppendDecimal(decimal d)
        {
            *(decimal*)_dataPointer = d;
            _dataPointer += sizeof(decimal);
        }

        private void AppendGuid(Guid g)
        {
            *(Guid*)_dataPointer = g;
            _dataPointer += sizeof(Guid);
        }

        private void AppendDateTime(DateTime dt)
        {
            *(DateTime*)_dataPointer = dt;
            _dataPointer += sizeof(DateTime);
        }

        private void AppendTimeSpan(TimeSpan ts)
        {
            *(TimeSpan*)_dataPointer = ts;
            _dataPointer += sizeof(TimeSpan);
        }

        public void SetTimestamp(DateTime timestamp)
        {
            Timestamp = timestamp;
        }
    }
}
