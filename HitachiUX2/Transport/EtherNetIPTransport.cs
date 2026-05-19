using System.Net.Sockets;

namespace HitachiUX2.Transport;

// EtherNet/IP explicit messaging (UCMM) over TCP port 44818.
// Session flow: RegisterSession → SendRRData (one per command) → UnRegisterSession.
// CIP request layout (Hitachi UX2 manual):
//   [service][pathSize=3][0x20,classId,0x24,instanceId,0x30,attrId][data]
// Send() synthesises ACK (0x06) or NAK (0x15) from the CIP response so callers
// can use the same ReceiveByte() path as the serial transport.
public sealed class EtherNetIPTransport : ITransport
{
    private const ushort CmdRegisterSession   = 0x0065;
    private const ushort CmdUnRegisterSession = 0x0066;
    private const ushort CmdSendRRData        = 0x006F;
    private const ushort CpfNullAddress       = 0x0000;
    private const ushort CpfUnconnectedData   = 0x00B2;
    private const int    EncapHeaderSize       = 24;

    private readonly string   _host;
    private readonly int      _port;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _ioTimeout;

    private TcpClient?        _client;
    private NetworkStream?    _stream;
    private uint              _sessionHandle;
    private readonly Queue<byte> _rxBuffer = new();

    public EtherNetIPTransport(
        string   host,
        int      port           = 44818,
        TimeSpan connectTimeout = default,
        TimeSpan ioTimeout      = default)
    {
        _host           = host;
        _port           = port;
        _connectTimeout = connectTimeout == default ? TimeSpan.FromSeconds(5) : connectTimeout;
        _ioTimeout      = ioTimeout      == default ? TimeSpan.FromSeconds(5) : ioTimeout;
    }

    public void Connect()
    {
        _client = new TcpClient();
        if (!_client.ConnectAsync(_host, _port).Wait(_connectTimeout))
            throw new TimeoutException($"EtherNet/IP: connection to {_host}:{_port} timed out.");

        _stream              = _client.GetStream();
        _stream.ReadTimeout  = (int)_ioTimeout.TotalMilliseconds;
        _stream.WriteTimeout = (int)_ioTimeout.TotalMilliseconds;

        RegisterSession();
    }

    public void Send(byte[] data)
    {
        EnsureConnected();
        _rxBuffer.Clear();

        var  pkt = BuildSendRRData(data);
        _stream!.Write(pkt, 0, pkt.Length);

        var  resp = ReadEncapResponse();
        bool ok   = CipStatusOk(resp);
        _rxBuffer.Enqueue(ok ? (byte)0x06 : (byte)0x15);
    }

    public byte? ReceiveByte(TimeSpan timeout) =>
        _rxBuffer.Count > 0 ? _rxBuffer.Dequeue() : null;

    // Sends a CIP request and returns the raw response payload bytes.
    // Returns null on encap or CIP error (e.g. pattern not found).
    public byte[]? Exchange(byte[] data)
    {
        EnsureConnected();
        _rxBuffer.Clear();
        var pkt = BuildSendRRData(data);
        _stream!.Write(pkt, 0, pkt.Length);
        return ExtractCipData(ReadEncapResponse());
    }

    private static byte[]? ExtractCipData(byte[] resp)
    {
        if (resp.Length < EncapHeaderSize) return null;
        if (BitConverter.ToUInt32(resp, 8) != 0) return null;

        int pos = EncapHeaderSize + 8;
        while (pos + 4 <= resp.Length)
        {
            ushort typeId  = BitConverter.ToUInt16(resp, pos);
            ushort itemLen = BitConverter.ToUInt16(resp, pos + 2);
            pos += 4;
            if (typeId == CpfUnconnectedData && itemLen >= 4)
            {
                if (resp[pos + 2] != 0x00) return null;     // CIP general status
                int extBytes  = resp[pos + 3] * 2;           // extended status words → bytes
                int dataStart = pos + 4 + extBytes;
                int dataLen   = itemLen - 4 - extBytes;
                if (dataLen <= 0) return [];
                var result = new byte[dataLen];
                Array.Copy(resp, dataStart, result, 0, dataLen);
                return result;
            }
            pos += itemLen;
        }
        return null;
    }

    public void Close()
    {
        if (_stream is not null && _sessionHandle != 0)
        {
            try { UnRegisterSession(); } catch { }
        }
        _stream?.Close();
        _client?.Close();
        _stream        = null;
        _client        = null;
        _sessionHandle = 0;
        _rxBuffer.Clear();
    }

    public void Dispose() => Close();

    public bool IsConnected => _stream is not null && _sessionHandle != 0;

    private void RegisterSession()
    {
        byte[] regData = [0x01, 0x00, 0x00, 0x00];
        var    pkt     = BuildEncapPacket(CmdRegisterSession, sessionHandle: 0, regData);
        _stream!.Write(pkt, 0, pkt.Length);

        var  resp   = ReadEncapResponse();
        if (resp.Length < EncapHeaderSize)
            throw new IOException("EtherNet/IP: RegisterSession response too short.");

        uint status = BitConverter.ToUInt32(resp, 8);
        if (status != 0)
            throw new IOException($"EtherNet/IP: RegisterSession failed, status=0x{status:X8}.");

        _sessionHandle = BitConverter.ToUInt32(resp, 4);
    }

    private void UnRegisterSession()
    {
        var pkt = BuildEncapPacket(CmdUnRegisterSession, _sessionHandle, []);
        _stream!.Write(pkt, 0, pkt.Length);
    }

    private byte[] BuildSendRRData(byte[] printerData)
    {
        int cpfLen = 4 + 2 + 2 + 4 + 4 + printerData.Length;
        var cpf    = new byte[cpfLen];
        int pos    = 0;

        pos += 4;                                              // Interface Handle = 0
        cpf[pos++] = 0x0A; cpf[pos++] = 0x00;                 // Timeout = 10 s
        cpf[pos++] = 0x02; cpf[pos++] = 0x00;                 // Item Count = 2

        WriteLE16(cpf, pos, CpfNullAddress);             pos += 2;
        WriteLE16(cpf, pos, 0);                          pos += 2;
        WriteLE16(cpf, pos, CpfUnconnectedData);         pos += 2;
        WriteLE16(cpf, pos, (ushort)printerData.Length); pos += 2;
        Array.Copy(printerData, 0, cpf, pos, printerData.Length);

        return BuildEncapPacket(CmdSendRRData, _sessionHandle, cpf);
    }

    private static byte[] BuildEncapPacket(ushort command, uint sessionHandle, byte[] data)
    {
        var buf = new byte[EncapHeaderSize + data.Length];
        WriteLE16(buf, 0, command);
        WriteLE16(buf, 2, (ushort)data.Length);
        WriteLE32(buf, 4, sessionHandle);
        Array.Copy(data, 0, buf, EncapHeaderSize, data.Length);
        return buf;
    }

    // Returns true only when both the encap status (offset 8) and the CIP general
    // status inside the CPF Unconnected Data item are zero.
    private static bool CipStatusOk(byte[] resp)
    {
        if (resp.Length < EncapHeaderSize) return false;
        if (BitConverter.ToUInt32(resp, 8) != 0) return false;

        int pos = EncapHeaderSize + 8;
        while (pos + 4 <= resp.Length)
        {
            ushort typeId  = BitConverter.ToUInt16(resp, pos);
            ushort itemLen = BitConverter.ToUInt16(resp, pos + 2);
            pos += 4;
            if (typeId == CpfUnconnectedData && itemLen >= 4)
                return resp[pos + 2] == 0x00;
            pos += itemLen;
        }
        return false;
    }

    private byte[] ReadEncapResponse()
    {
        var    header = ReadExact(EncapHeaderSize);
        ushort len    = BitConverter.ToUInt16(header, 2);
        if (len == 0)
            return header;

        var full = new byte[EncapHeaderSize + len];
        Array.Copy(header, full, EncapHeaderSize);
        ReadExact(len).CopyTo(full, EncapHeaderSize);
        return full;
    }

    private byte[] ReadExact(int count)
    {
        var buf = new byte[count];
        int rcv = 0;
        while (rcv < count)
        {
            int n = _stream!.Read(buf, rcv, count - rcv);
            if (n == 0)
                throw new IOException("EtherNet/IP: connection closed by remote host.");
            rcv += n;
        }
        return buf;
    }

    private static void WriteLE16(byte[] buf, int offset, ushort value)
    {
        buf[offset]     = (byte)(value & 0xFF);
        buf[offset + 1] = (byte)(value >> 8);
    }

    private static void WriteLE32(byte[] buf, int offset, uint value)
    {
        buf[offset]     = (byte)(value         & 0xFF);
        buf[offset + 1] = (byte)((value >>  8) & 0xFF);
        buf[offset + 2] = (byte)((value >> 16) & 0xFF);
        buf[offset + 3] = (byte)((value >> 24) & 0xFF);
    }

    private void EnsureConnected()
    {
        if (_stream is null)
            throw new InvalidOperationException("Not connected. Call Connect() first.");
    }
}
