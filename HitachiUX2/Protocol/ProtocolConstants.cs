namespace HitachiUX2.Protocol;

/// <summary>
/// Byte-konstanter fra Hitachi UX2 Communication User's Manual (Serial), seksjon 1.3 og 1.4.
/// Disse bytene brukes til å bygge meldinger som sendes til skriveren.
/// </summary>
public static class ProtocolConstants
{
    // ── Grunnleggende kontrollbytes ────────────────────────────────

    /// <summary>Enquiry — PC ber om tillatelse til å sende.</summary>
    public const byte ENQ = 0x05;

    /// <summary>Acknowledge — skriveren er klar / melding mottatt OK.</summary>
    public const byte ACK = 0x06;

    /// <summary>Negative ACK — feil eller skriveren er ikke klar.</summary>
    public const byte NAK = 0x15;

    /// <summary>Start of Text — markerer starten på en melding.</summary>
    public const byte STX = 0x02;

    /// <summary>End of Text — markerer slutten på en melding.</summary>
    public const byte ETX = 0x03;

    /// <summary>Data Link Escape — etterfølges av et item-nummer.</summary>
    public const byte DLE = 0x10;

    /// <summary>Escape 2 — prefiks for spesialkommandoer.</summary>
    public const byte ESC2 = 0x1F;

    // ── Remote operation (seksjon 1.3.9) ──────────────────────────

    public const byte RemoteInkStart    = 0x31; // Start blekksprøyting
    public const byte RemoteInkStop     = 0x32; // Stopp blekksprøyting
    public const byte RemoteDeflectOn   = 0x33; // Defleksjonsspenning PÅ
    public const byte RemoteDeflectOff  = 0x34; // Defleksjonsspenning AV
    public const byte RemoteErrorReset  = 0x35; // Nullstill feil

    // ── Online/Offline-kommandoer (seksjon 1.3.8) ─────────────────

    public const byte OnlineCmd  = 0x31;
    public const byte OfflineCmd = 0x32;

    // ── Headere for ESC2-kommandoer (seksjon 1.4.2) ───────────────

    public const byte HdrPrintRecall   = 0x20; // Hent lagret jobb
    public const byte HdrPrintRegister = 0x21; // Lagre jobb
    public const byte HdrOnline        = 0x24; // Online/offline-kontroll
    public const byte HdrRemoteOp      = 0x25; // Fjernoperasjon
    public const byte HdrTimeCtrl      = 0x26; // Tidskontroll
    public const byte HdrItemDelete    = 0x2B; // Slett print-item
    public const byte HdrItemCount     = 0x7B; // Antall-item-modus
    public const byte HdrCountReset    = 0x29; // Teller-reset

    // ── Maks meldingsstørrelse ─────────────────────────────────────

    /// <summary>Maks antall bytes per melding inkl. STX og ETX.</summary>
    public const int MaxFrameSize = 3000;

    // ── Item-nummer til protokollbyte (seksjon 1.3.2-2) ───────────
    // Item 1 = 0x31, item 2 = 0x32, osv.

    private static readonly byte[] ItemCodes =
    [
        0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, // 1–10
        0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44, // 11–20
        0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E, // 21–30
        0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, // 31–40
        0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62, // 41–50
    ];

    /// <summary>
    /// Returnerer protokollbyten for et gitt item-nummer (1-basert).
    /// </summary>
    /// <param name="itemNumber">Item-nummer, 1–50.</param>
    /// <returns>Protokollbyte som sendes til skriveren.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Kastes hvis item-nummer er utenfor område.</exception>
    public static byte GetItemCode(int itemNumber)
    {
        if (itemNumber < 1 || itemNumber > ItemCodes.Length)
            throw new ArgumentOutOfRangeException(
                nameof(itemNumber),
                $"Item-nummer må være 1–{ItemCodes.Length}, fikk {itemNumber}."
            );

        return ItemCodes[itemNumber - 1];
    }

    /// <summary>
    /// Beregner BCC (Block Check Character) ved å XOR-e alle bytes.
    /// Brukes kun hvis BCC-kontrollsum er aktivert på skriveren.
    /// </summary>
    public static byte CalculateBcc(byte[] data)
    {
        byte result = 0;
        foreach (var b in data)
            result ^= b;
        return result;
    }
}
