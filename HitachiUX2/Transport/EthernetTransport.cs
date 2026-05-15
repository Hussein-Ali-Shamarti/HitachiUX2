using System.Net.Sockets;

namespace HitachiUX2.Transport;

/// <summary>
/// Ethernet Tunnel-kommunikasjon over TCP/IP.
/// Bruker samme byte-protokoll som RS-232C, men over LAN.
/// Standard IP: 192.168.0.250, standard port: 1024.
/// </summary>
public sealed class EthernetTransport : ITransport
{
    private readonly string _host;
    private readonly int    _port;
    private readonly TimeSpan _connectTimeout;

    private TcpClient?    _client;
    private NetworkStream? _stream;

    /// <param name="host">Skriverens IP-adresse.</param>
    /// <param name="port">TCP-port (standard: 1024).</param>
    /// <param name="connectTimeout">Tidsavbrudd for tilkobling.</param>
    public EthernetTransport(string host, int port, TimeSpan connectTimeout)
    {
        _host           = host;
        _port           = port;
        _connectTimeout = connectTimeout;
    }

    /// <inheritdoc/>
    public void Connect()
    {
        _client = new TcpClient();

        // Koble til med tidsavbrudd
        var task = _client.ConnectAsync(_host, _port);
        if (!task.Wait(_connectTimeout))
            throw new TimeoutException(
                $"Tilkoblingen til {_host}:{_port} tok for lang tid."
            );

        _stream = _client.GetStream();
    }

    /// <inheritdoc/>
    public void Send(byte[] data)
    {
        EnsureConnected();
        _stream!.Write(data, 0, data.Length);
    }

    /// <inheritdoc/>
    public byte? ReceiveByte(TimeSpan timeout)
    {
        EnsureConnected();
        _stream!.ReadTimeout = (int)timeout.TotalMilliseconds;

        try
        {
            var value = _stream.ReadByte();
            return value == -1 ? null : (byte)value;
        }
        catch (IOException)
        {
            // Tidsavbrudd eller tilkoblingen ble brutt
            return null;
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        _stream?.Close();
        _client?.Close();
        _stream = null;
        _client = null;
    }

    /// <inheritdoc/>
    public void Dispose() => Close();

    private void EnsureConnected()
    {
        if (_stream is null)
            throw new InvalidOperationException(
                "Ikke tilkoblet. Kall Connect() først."
            );
    }
}
