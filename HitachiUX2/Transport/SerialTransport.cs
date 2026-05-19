using System.IO.Ports;

namespace HitachiUX2.Transport;

public sealed class SerialTransport : ITransport
{
    private readonly SerialPort _port;

    public SerialTransport(
        string   portName,
        int      baudRate = 4800,
        int      dataBits = 8,
        Parity   parity   = Parity.None,
        StopBits stopBits = StopBits.One)
    {
        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
    }

    public void Connect() => _port.Open();

    public void Send(byte[] data) => _port.Write(data, 0, data.Length);

    public byte? ReceiveByte(TimeSpan timeout)
    {
        _port.ReadTimeout = (int)timeout.TotalMilliseconds;
        try   { return (byte)_port.ReadByte(); }
        catch (TimeoutException) { return null; }
    }

    public void Close()
    {
        if (_port.IsOpen)
            _port.Close();
    }

    public void Dispose() => Close();

    public byte[]? Exchange(byte[] data) =>
        throw new NotSupportedException("Serial transport does not support Exchange.");
}
