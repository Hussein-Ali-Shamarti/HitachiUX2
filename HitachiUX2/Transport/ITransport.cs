namespace HitachiUX2.Transport;

public interface ITransport : IDisposable
{
    void Connect();
    void Send(byte[] data);
    byte? ReceiveByte(TimeSpan timeout); // null on timeout
    void Close();
    byte[]? Exchange(byte[] data);       // returns CIP response bytes, null on error
}
