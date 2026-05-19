using System.Text;
using HitachiUX2.Exceptions;
using HitachiUX2.Protocol;
using HitachiUX2.Transport;

namespace HitachiUX2;

public sealed class HitachiUX2Printer : IDisposable
{
    private readonly ITransport _transport;
    private readonly bool       _useEnq;
    private readonly bool       _useBcc;

    private static readonly TimeSpan AckTimeout    = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EnqTimeout    = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PingTimeout   = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StartupDelay  = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ShutdownDelay = TimeSpan.FromSeconds(1);

    public HitachiUX2Printer(
        string host,
        int    port           = 44818,
        bool   useEnq         = false,
        bool   useBcc         = false,
        int    connectTimeout = 5000)
    {
        _transport = new EtherNetIPTransport(host, port, TimeSpan.FromMilliseconds(connectTimeout));
        _useEnq    = useEnq;
        _useBcc    = useBcc;
    }

    public HitachiUX2Printer(
        string portName,
        bool   useSerial,
        int    baudRate = 4800,
        bool   useEnq   = false,
        bool   useBcc   = false)
    {
        if (!useSerial)
            throw new ArgumentException("Set useSerial to true for serial connections.", nameof(useSerial));
        _transport = new SerialTransport(portName, baudRate);
        _useEnq    = useEnq;
        _useBcc    = useBcc;
    }

    public void Connect()    => _transport.Connect();
    public void Disconnect() => _transport.Close();
    public void Dispose()    => _transport.Dispose();

    // Closes and reopens the transport — needed after the printer drops the TCP session.
    public void Reconnect()
    {
        try { _transport.Close(); } catch { }
        _transport.Connect();
    }

    private bool IsEtherNetIP => _transport is EtherNetIPTransport;

    private void SendRaw(byte[] data) => _transport.Send(data);

    private byte? ReceiveByte(TimeSpan timeout) => _transport.ReceiveByte(timeout);

    private void EnqAck()
    {
        SendRaw([ProtocolConstants.ENQ]);
        var response = ReceiveByte(EnqTimeout);
        switch (response)
        {
            case null:
                throw new PrinterTimeoutException("No response to ENQ.");
            case ProtocolConstants.NAK:
                throw new PrinterOfflineException("Printer returned NAK to ENQ — may be offline or busy.");
            case not ProtocolConstants.ACK:
                throw new PrinterException($"Unexpected ENQ response: 0x{response:X2}");
        }
    }

    private void WaitAck(string context = "")
    {
        var response = ReceiveByte(AckTimeout);
        var ctx      = string.IsNullOrEmpty(context) ? "" : $" ({context})";
        switch (response)
        {
            case null:
                throw new PrinterTimeoutException($"Timeout waiting for ACK{ctx}.");
            case ProtocolConstants.NAK:
                throw new PrinterNakException($"Printer returned NAK{ctx}.");
            case not ProtocolConstants.ACK:
                throw new PrinterException($"Expected ACK, got 0x{response:X2}{ctx}.");
        }
    }

    // Serial protocol frame: [ENQ → ACK]  STX payload ETX [BCC]  → ACK
    private void Transmit(byte[] payload, string context = "")
    {
        if (payload.Length + 2 > ProtocolConstants.MaxFrameSize)
            throw new ArgumentException(
                $"Payload too large: {payload.Length + 2} bytes (max {ProtocolConstants.MaxFrameSize}).");

        var frame = new List<byte> { ProtocolConstants.STX };
        frame.AddRange(payload);
        frame.Add(ProtocolConstants.ETX);

        if (_useBcc)
            frame.Add(ProtocolConstants.CalculateBcc([.. frame]));

        if (_useEnq)
            EnqAck();

        SendRaw([.. frame]);
        WaitAck(context);
    }

    public void GoOnline()  => Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrOnline, ProtocolConstants.OnlineCmd],  "GoOnline");
    public void GoOffline() => Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrOnline, ProtocolConstants.OfflineCmd], "GoOffline");

    private void RemoteOp(byte command, string name) =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrRemoteOp, command], name);

    public void InkStart()      => RemoteOp(ProtocolConstants.RemoteInkStart,   "InkStart");
    public void InkStop()       => RemoteOp(ProtocolConstants.RemoteInkStop,    "InkStop");
    public void DeflectionOn()  => RemoteOp(ProtocolConstants.RemoteDeflectOn,  "DeflectionOn");
    public void DeflectionOff() => RemoteOp(ProtocolConstants.RemoteDeflectOff, "DeflectionOff");
    public void ResetError()    => RemoteOp(ProtocolConstants.RemoteErrorReset,  "ResetError");

    public void SendPrintContent(Dictionary<int, string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ArgumentException("items cannot be empty.");

        if (IsEtherNetIP)
        {
            SendPrintContentCip(items);
            return;
        }

        var payload = new List<byte>();
        foreach (var (itemNumber, text) in items.OrderBy(x => x.Key))
        {
            payload.Add(ProtocolConstants.DLE);
            payload.Add(ProtocolConstants.GetItemCode(itemNumber));
            if (!string.IsNullOrEmpty(text))
                payload.AddRange(Encoding.ASCII.GetBytes(text));
        }

        Transmit([.. payload], $"SendPrintContent[{string.Join(",", items.Keys)}]");
    }

    // EtherNet/IP path: set active item (class 0x7A, attr 0x66), then write text (class 0x67, attr 0x71).
    private void SendPrintContentCip(Dictionary<int, string> items)
    {
        foreach (var (itemNumber, text) in items.OrderBy(x => x.Key))
        {
            byte[] setItem =
            [
                0x32, 0x03,
                0x20, 0x7A, 0x24, 0x01, 0x30, 0x66,
                (byte)itemNumber
            ];
            SendRaw(setItem);
            WaitAck($"SetItemNumber[{itemNumber}]");

            var textBytes = string.IsNullOrEmpty(text) ? [] : Encoding.ASCII.GetBytes(text);
            var sendText  = new byte[8 + textBytes.Length + 1];
            sendText[0] = 0x32; sendText[1] = 0x03;
            sendText[2] = 0x20; sendText[3] = 0x67;
            sendText[4] = 0x24; sendText[5] = 0x01;
            sendText[6] = 0x30; sendText[7] = 0x71;
            Array.Copy(textBytes, 0, sendText, 8, textBytes.Length);
            sendText[^1] = 0x00; // null-terminated
            SendRaw(sendText);
            WaitAck($"SendText[{itemNumber}]");
        }
    }

    public void PrintText(string text, int item = 1) =>
        SendPrintContent(new() { [item] = text });

    public void RecallPrintData(int jobNumber)
    {
        if (jobNumber < 1 || jobNumber > 2000)
            throw new ArgumentOutOfRangeException(nameof(jobNumber), "Job number must be 1–2000.");

        if (IsEtherNetIP)
        {
            // EtherNet/IP: service 0x34, class 0x66, instance 0x01, attr 0x64, job as LE uint16
            byte[] req =
            [
                0x34, 0x03,
                0x20, 0x66, 0x24, 0x01, 0x30, 0x64,
                (byte)(jobNumber & 0xFF), (byte)(jobNumber >> 8)
            ];
            SendRaw(req);
            WaitAck($"RecallPrintData#{jobNumber}");
            return;
        }

        var jobBytes = Encoding.ASCII.GetBytes(jobNumber.ToString("D4"));
        var payload  = new byte[] { ProtocolConstants.ESC2, ProtocolConstants.HdrPrintRecall, 0x31 }
                       .Concat(jobBytes).ToArray();

        Transmit(payload, $"RecallPrintData#{jobNumber}");
    }

    public void RegisterPrintData(int jobNumber, string? jobName = null)
    {
        if (jobNumber < 1 || jobNumber > 2000)
            throw new ArgumentOutOfRangeException(nameof(jobNumber), "Job number must be 1–2000.");
        if (jobName?.Length > 12)
            throw new ArgumentException("Job name max 12 characters.", nameof(jobName));

        var jobBytes = Encoding.ASCII.GetBytes(jobNumber.ToString("D4"));
        var payload  = new List<byte> { ProtocolConstants.ESC2, ProtocolConstants.HdrPrintRegister, 0x31 };
        payload.AddRange(jobBytes);

        if (!string.IsNullOrEmpty(jobName))
        {
            payload.AddRange([ProtocolConstants.ESC2, ProtocolConstants.HdrPrintRegister, 0x32]);
            payload.AddRange(Encoding.ASCII.GetBytes(jobName));
        }

        Transmit([.. payload], $"RegisterPrintData#{jobNumber}");
    }

    public void SetTime(DateTime? dateTime = null)
    {
        var dt      = dateTime ?? DateTime.Now;
        var timeStr = Encoding.ASCII.GetBytes(dt.ToString("yyMMddHHmmss"));
        var payload = new byte[] { ProtocolConstants.ESC2, ProtocolConstants.HdrTimeCtrl, 0x31 }
                      .Concat(timeStr).ToArray();

        Transmit(payload, "SetTime");
    }

    public void SyncTime() => SetTime(DateTime.Now);

    public void DeletePrintItems() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrItemDelete, 0x31], "DeletePrintItems");

    public void ResetCount() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrCountReset, 0x35], "ResetCount");

    public bool Ping()
    {
        try
        {
            SendRaw([ProtocolConstants.ENQ]);
            return ReceiveByte(PingTimeout) == ProtocolConstants.ACK;
        }
        catch { return false; }
    }

    // Fixed user pattern {X/N} — class 0x6B, attr 0x64. dotMatrixSize 5 = 7×10 dots.
    // Returns null if the pattern does not exist on the printer.
    public byte[]? GetUserPattern(byte patternNumber, byte dotMatrixSize = 5)
    {
        if (!IsEtherNetIP)
            throw new NotSupportedException("GetUserPattern requires EtherNet/IP.");

        byte[] req =
        [
            0x33, 0x03,
            0x20, 0x6B, 0x24, 0x01, 0x30, 0x64,
            dotMatrixSize, patternNumber
        ];
        return _transport.Exchange(req);
    }

    // Free user pattern {Z/N} — class 0x6B, attr 0x65.
    // Returns null if the pattern does not exist on the printer.
    public byte[]? GetFreePattern(byte patternNumber)
    {
        if (!IsEtherNetIP)
            throw new NotSupportedException("GetFreePattern requires EtherNet/IP.");

        byte[] req =
        [
            0x33, 0x03,
            0x20, 0x6B, 0x24, 0x01, 0x30, 0x65,
            patternNumber
        ];
        return _transport.Exchange(req);
    }

    public void StartupSequence()
    {
        GoOnline();
        Thread.Sleep(StartupDelay);
        InkStart();
        Thread.Sleep(StartupDelay);
        DeflectionOn();
    }

    public void ShutdownSequence()
    {
        DeflectionOff();
        Thread.Sleep(ShutdownDelay);
        InkStop();
        Thread.Sleep(ShutdownDelay);
        GoOffline();
    }
}
