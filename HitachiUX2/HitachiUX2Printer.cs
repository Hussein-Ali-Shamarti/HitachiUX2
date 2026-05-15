using System.Text;
using HitachiUX2.Exceptions;
using HitachiUX2.Protocol;
using HitachiUX2.Transport;

namespace HitachiUX2;

/// <summary>
/// SDK for Hitachi UX2-D160W industriell inkjet-skriver.
///
/// Støtter Ethernet (Tunnel) og RS-232C seriell kommunikasjon.
/// Alle kommandoer er basert på den offisielle Hitachi UX2
/// Communication User's Manual (Serial), rev. N.
///
/// Eksempel — Ethernet:
/// <code>
/// using var skriver = new HitachiUX2Printer("192.168.0.250");
/// skriver.Connect();
/// skriver.SendPrintContent(new() { [2] = "12.05.26" });
/// </code>
///
/// Eksempel — Seriell:
/// <code>
/// using var skriver = new HitachiUX2Printer("COM3", useSerial: true);
/// skriver.Connect();
/// skriver.SendPrintContent(new() { [1] = "LOT A123" });
/// </code>
/// </summary>
public sealed class HitachiUX2Printer : IDisposable
{
    private readonly ITransport _transport;
    private readonly bool       _useEnq;
    private readonly bool       _useBcc;

    private static readonly TimeSpan AckTimeout     = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EnqTimeout      = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PingTimeout     = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan StartupDelay    = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ShutdownDelay   = TimeSpan.FromSeconds(1);

    // ── Konstruktører ──────────────────────────────────────────────

    /// <summary>
    /// Oppretter en Ethernet (Tunnel) tilkobling til skriveren.
    /// </summary>
    /// <param name="host">Skriverens IP-adresse.</param>
    /// <param name="port">TCP-port (standard: 1024).</param>
    /// <param name="useEnq">Send ENQ-handshake før sending (standard: false).</param>
    /// <param name="useBcc">Legg til BCC-kontrollsum (standard: false).</param>
    /// <param name="connectTimeout">Tidsavbrudd for tilkobling (standard: 5 sek).</param>
    public HitachiUX2Printer(
        string host,
        int    port           = 1024,
        bool   useEnq         = false,
        bool   useBcc         = false,
        int    connectTimeout = 5000)
    {
        _transport = new EthernetTransport(host, port, TimeSpan.FromMilliseconds(connectTimeout));
        _useEnq    = useEnq;
        _useBcc    = useBcc;
    }

    /// <summary>
    /// Oppretter en RS-232C seriell tilkobling til skriveren.
    /// </summary>
    /// <param name="portName">Portnavn, f.eks. "COM3".</param>
    /// <param name="useSerial">Må være <c>true</c> for å velge seriell transport.</param>
    /// <param name="baudRate">Baud rate (standard: 4800).</param>
    /// <param name="useEnq">Send ENQ-handshake før sending (standard: false).</param>
    /// <param name="useBcc">Legg til BCC-kontrollsum (standard: false).</param>
    public HitachiUX2Printer(
        string portName,
        bool   useSerial,
        int    baudRate = 4800,
        bool   useEnq   = false,
        bool   useBcc   = false)
    {
        if (!useSerial)
            throw new ArgumentException("useSerial må være true for seriell tilkobling.", nameof(useSerial));
        _transport = new SerialTransport(portName, baudRate);
        _useEnq    = useEnq;
        _useBcc    = useBcc;
    }

    // ── Tilkoblingsadministrasjon ──────────────────────────────────

    /// <summary>Åpner tilkoblingen til skriveren.</summary>
    public void Connect() => _transport.Connect();

    /// <summary>Lukker tilkoblingen til skriveren.</summary>
    public void Disconnect() => _transport.Close();

    /// <inheritdoc/>
    public void Dispose() => _transport.Dispose();

    // ── Lavnivå-protokoll ──────────────────────────────────────────

    private void SendRaw(byte[] data) => _transport.Send(data);

    private byte? ReceiveByte(TimeSpan timeout) => _transport.ReceiveByte(timeout);

    /// <summary>
    /// Sender ENQ og venter på ACK.
    /// </summary>
    private void EnqAck()
    {
        SendRaw([ProtocolConstants.ENQ]);
        var response = ReceiveByte(EnqTimeout);

        switch (response)
        {
            case null:
                throw new PrinterTimeoutException(
                    "Ingen respons på ENQ. Sjekk kabel/nettverk og at skriveren er på.");
            case ProtocolConstants.NAK:
                throw new PrinterOfflineException(
                    "Skriveren svarte NAK på ENQ. Den kan være offline, opptatt eller i feiltilstand.");
            case not ProtocolConstants.ACK:
                throw new PrinterException(
                    $"Uventet respons på ENQ: 0x{response:X2}");
        }
    }

    /// <summary>
    /// Venter på ACK etter at STX...ETX-rammen er sendt.
    /// </summary>
    private void WaitAck(string context = "")
    {
        var response = ReceiveByte(AckTimeout);
        var ctx      = string.IsNullOrEmpty(context) ? "" : $" ({context})";

        switch (response)
        {
            case null:
                throw new PrinterTimeoutException($"Tidsavbrudd ved venting på ACK{ctx}.");
            case ProtocolConstants.NAK:
                throw new PrinterNakException(
                    $"Skriveren svarte NAK — data kan være ugyldig eller skriveren er opptatt{ctx}.");
            case not ProtocolConstants.ACK:
                throw new PrinterException(
                    $"Uventet byte 0x{response:X2} når ACK var forventet{ctx}.");
        }
    }

    /// <summary>
    /// Fullstendig sendeprosedyre:
    ///   [ENQ → ACK]  STX nyttelast ETX [BCC]  → ACK
    /// </summary>
    /// <param name="payload">Bytes som plasseres mellom STX og ETX.</param>
    /// <param name="context">Etikett for feilmeldinger.</param>
    private void Transmit(byte[] payload, string context = "")
    {
        // Valider størrelse
        if (payload.Length + 2 > ProtocolConstants.MaxFrameSize)
            throw new ArgumentException(
                $"Nyttelast for stor: {payload.Length + 2} bytes (maks {ProtocolConstants.MaxFrameSize}).");

        // Bygg ramme: STX + nyttelast + ETX [+ BCC]
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

    // ── Online / Offline (seksjon 1.3.8) ──────────────────────────

    /// <summary>Setter skriveren i online-modus (klar til å skrive ut).</summary>
    public void GoOnline() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrOnline, ProtocolConstants.OnlineCmd], "GoOnline");

    /// <summary>Setter skriveren i offline-modus.</summary>
    public void GoOffline() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrOnline, ProtocolConstants.OfflineCmd], "GoOffline");

    // ── Fjernoperasjon (seksjon 1.3.9) ────────────────────────────

    private void RemoteOp(byte command, string name) =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrRemoteOp, command], name);

    /// <summary>Starter blekksprøyting.</summary>
    public void InkStart()    => RemoteOp(ProtocolConstants.RemoteInkStart,   "InkStart");

    /// <summary>Stopper blekksprøyting.</summary>
    public void InkStop()     => RemoteOp(ProtocolConstants.RemoteInkStop,    "InkStop");

    /// <summary>Slår defleksjonsspenning PÅ.</summary>
    public void DeflectionOn()  => RemoteOp(ProtocolConstants.RemoteDeflectOn,  "DeflectionOn");

    /// <summary>Slår defleksjonsspenning AV.</summary>
    public void DeflectionOff() => RemoteOp(ProtocolConstants.RemoteDeflectOff, "DeflectionOff");

    /// <summary>Nullstiller gjeldende skriverfeil.</summary>
    public void ResetError()  => RemoteOp(ProtocolConstants.RemoteErrorReset,  "ResetError");

    // ── Sende print-innhold (seksjon 1.3.2) ───────────────────────

    /// <summary>
    /// Sender tekst til ett eller flere print-item.
    /// </summary>
    /// <param name="items">
    /// Dictionary med item-nummer (1-basert) og tilhørende tekst.
    /// Eksempel: <c>new() { [1] = "LOT A123", [2] = "12.05.26" }</c>
    /// </param>
    /// <remarks>
    /// Item som ikke er inkludert i dictionary-en endres ikke på skriveren.
    /// Tekst må være ASCII (0x20–0x7E). Ingen æ, ø, å.
    /// Tom streng sletter teksten i det aktuelle item-et.
    /// </remarks>
    public void SendPrintContent(Dictionary<int, string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
            throw new ArgumentException("items kan ikke være tomt.");

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

    /// <summary>
    /// Snarvei for å sende én tekststreng til ett print-item.
    /// </summary>
    /// <param name="text">ASCII-tekst som skal skrives ut.</param>
    /// <param name="item">Print-item-nummer (standard: 1).</param>
    public void PrintText(string text, int item = 1) =>
        SendPrintContent(new() { [item] = text });

    // ── Hente lagret jobb (seksjon 1.3.3) ─────────────────────────

    /// <summary>
    /// Laster inn en lagret print-jobb fra skriveren.
    /// </summary>
    /// <param name="jobNumber">Jobbnummer, 1–2000.</param>
    public void RecallPrintData(int jobNumber)
    {
        if (jobNumber < 1 || jobNumber > 2000)
            throw new ArgumentOutOfRangeException(nameof(jobNumber), "Jobbnummer må være 1–2000.");

        var jobBytes = Encoding.ASCII.GetBytes(jobNumber.ToString("D4"));
        var payload  = new byte[] { ProtocolConstants.ESC2, ProtocolConstants.HdrPrintRecall, 0x31 }
                       .Concat(jobBytes).ToArray();

        Transmit(payload, $"RecallPrintData#{jobNumber}");
    }

    // ── Lagre jobb (seksjon 1.3.4) ────────────────────────────────

    /// <summary>
    /// Lagrer gjeldende print-data som en jobb på skriveren.
    /// </summary>
    /// <param name="jobNumber">Registreringsnummer 1–2000.</param>
    /// <param name="jobName">Jobbnavn, maks 12 ASCII-tegn (valgfritt).</param>
    public void RegisterPrintData(int jobNumber, string? jobName = null)
    {
        if (jobNumber < 1 || jobNumber > 2000)
            throw new ArgumentOutOfRangeException(nameof(jobNumber), "Jobbnummer må være 1–2000.");
        if (jobName?.Length > 12)
            throw new ArgumentException("Jobbnavn kan maks være 12 tegn.", nameof(jobName));

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

    // ── Tidskontroll (seksjon 1.3.6) ──────────────────────────────

    /// <summary>
    /// Setter skriverens interne klokke.
    /// </summary>
    /// <param name="dateTime">Dato/tid. Null gir nåværende systemtid.</param>
    public void SetTime(DateTime? dateTime = null)
    {
        var dt       = dateTime ?? DateTime.Now;
        var timeStr  = Encoding.ASCII.GetBytes(dt.ToString("yyMMddHHmmss"));
        var payload  = new byte[] { ProtocolConstants.ESC2, ProtocolConstants.HdrTimeCtrl, 0x31 }
                       .Concat(timeStr).ToArray();

        Transmit(payload, "SetTime");
    }

    /// <summary>Synkroniserer skriverens klokke med systemtiden.</summary>
    public void SyncTime() => SetTime(DateTime.Now);

    // ── Slette print-item (seksjon 1.3.12) ────────────────────────

    /// <summary>
    /// Sletter alle print-item unntatt det første.
    /// Item 1 beholdes alltid (per protokollspesifikasjon).
    /// Må sendes alene.
    /// </summary>
    public void DeletePrintItems() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrItemDelete, 0x31], "DeletePrintItems");

    // ── Teller-reset (seksjon 1.3.11-4) ───────────────────────────

    /// <summary>
    /// Nullstiller alle tellerblokker til forhåndsinnstilte verdier.
    /// Må sendes alene.
    /// </summary>
    public void ResetCount() =>
        Transmit([ProtocolConstants.ESC2, ProtocolConstants.HdrCountReset, 0x35], "ResetCount");

    // ── Ping ───────────────────────────────────────────────────────

    /// <summary>
    /// Tester kommunikasjon ved å sende ENQ og sjekke for ACK.
    /// </summary>
    /// <returns>True hvis skriveren svarte med ACK.</returns>
    public bool Ping()
    {
        try
        {
            SendRaw([ProtocolConstants.ENQ]);
            var response = ReceiveByte(PingTimeout);
            return response == ProtocolConstants.ACK;
        }
        catch
        {
            return false;
        }
    }

    // ── Høynivå sekvenser ──────────────────────────────────────────

    /// <summary>
    /// Full oppstart: online → blekk start → defleksjon på.
    /// </summary>
    public void StartupSequence()
    {
        GoOnline();
        Thread.Sleep(StartupDelay);
        InkStart();
        Thread.Sleep(StartupDelay);
        DeflectionOn();
    }

    /// <summary>
    /// Sikker nedstenging: defleksjon av → blekk stopp → offline.
    /// </summary>
    public void ShutdownSequence()
    {
        DeflectionOff();
        Thread.Sleep(ShutdownDelay);
        InkStop();
        Thread.Sleep(ShutdownDelay);
        GoOffline();
    }
}
