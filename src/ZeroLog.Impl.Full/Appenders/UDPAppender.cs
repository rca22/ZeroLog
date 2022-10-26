using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using ZeroLog.Formatting;

namespace ZeroLog.Appenders;

/// <summary>
/// Class for appenders which write via UDP to a local port.
/// Default formatter is Log4J XML format for writing to viewers such as Log4View.
/// </summary>
public class UDPAppender : Appender
{
    private byte[] _byteBuffer = Array.Empty<byte>();

    private Encoding _encoding = Encoding.UTF8;
    private Formatter? _formatter;
	private UdpClient? _udpClient;

    /// <summary>
    /// The encoding to use when writing to the stream.
    /// </summary>
    protected internal Encoding Encoding
    {
        get => _encoding;
        set
        {
            _encoding = value;
            UpdateEncodingSpecificData();
        }
    }

    /// <summary>
    /// The formatter to use to convert log messages to text.
    /// </summary>
    public Formatter Formatter
    {
        get => _formatter ??= new Log4JXMLFormatter();
        init => _formatter = value;
    }

    /// <summary>
    /// Initializes a new instance of the stream appender.
    /// </summary>
    protected UDPAppender(int port)
    {
		if (port <= 0)
			throw new ArgumentException("Expected positive port number", nameof(port));
        var endPoint = new IPEndPoint(IPAddress.Loopback, port);
        _udpClient = new UdpClient()
        {
            ExclusiveAddressUse = false,
            DontFragment = true
        };
        _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udpClient.Connect(endPoint);
        UpdateEncodingSpecificData();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        _udpClient?.Dispose();
        _udpClient = null;
        base.Dispose();
    }

    /// <inheritdoc/>
    public override void WriteMessage(LoggedMessage message)
    {
        if (_udpClient is null)
            return;

        var chars = Formatter.FormatMessage(message);
        var byteCount = _encoding.GetBytes(chars, _byteBuffer);
        _udpClient.Send(_byteBuffer, byteCount);
    }

    private void UpdateEncodingSpecificData()
    {
        var maxBytes = _encoding.GetMaxByteCount(LogManager.OutputBufferSize);

        if (_byteBuffer.Length < maxBytes)
            _byteBuffer = GC.AllocateUninitializedArray<byte>(maxBytes);
    }
}
