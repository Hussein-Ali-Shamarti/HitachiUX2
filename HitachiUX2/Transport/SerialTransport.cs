using System.IO.Ports;

namespace HitachiUX2.Transport;

/// <summary>
/// RS-232C seriell kommunikasjon via USB-til-seriell-adapter.
/// Krever NuGet-pakken: System.IO.Ports
/// </summary>
public sealed class SerialTransport : ITransport
{
    private readonly SerialPort _port;

    /// <param name="portName">Portnavn, f.eks. "COM3".</param>
    /// <param name="baudRate">Baud rate (standard: 4800).</param>
    /// <param name="dataBits">Databiter, 7 eller 8 (standard: 8).</param>
    /// <param name="parity">Paritet (standard: None).</param>
    /// <param name="stopBits">Stoppbiter (standard: One).</param>
    public SerialTransport(
        string   portName,
        int      baudRate = 4800,
        int      dataBits = 8,
        Parity   parity   = Parity.None,
        StopBits stopBits = StopBits.One)
    {
        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
    }

    /// <inheritdoc/>
    public void Connect() => _port.Open();

    /// <inheritdoc/>
    public void Send(byte[] data) => _port.Write(data, 0, data.Length);

    /// <inheritdoc/>
    public byte? ReceiveByte(TimeSpan timeout)
    {
        _port.ReadTimeout = (int)timeout.TotalMilliseconds;
        try
        {
            return (byte)_port.ReadByte();
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        if (_port.IsOpen)
            _port.Close();
    }

    /// <inheritdoc/>
    public void Dispose() => Close();
}
