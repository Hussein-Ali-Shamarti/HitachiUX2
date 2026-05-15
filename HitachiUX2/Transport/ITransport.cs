namespace HitachiUX2.Transport;

/// <summary>
/// Felles grensesnitt for alle transportlag (Ethernet og Seriell).
/// Gjør det enkelt å bytte mellom tilkoblingstyper uten å endre SDK-logikken.
/// </summary>
public interface ITransport : IDisposable
{
    /// <summary>Åpner tilkoblingen.</summary>
    void Connect();

    /// <summary>Sender bytes til skriveren.</summary>
    void Send(byte[] data);

    /// <summary>
    /// Mottar én byte fra skriveren.
    /// Returnerer null hvis tidsavbrudd inntreffer.
    /// </summary>
    byte? ReceiveByte(TimeSpan timeout);

    /// <summary>Lukker tilkoblingen.</summary>
    void Close();
}
