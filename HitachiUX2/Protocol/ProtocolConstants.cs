namespace HitachiUX2.Protocol;

public static class ProtocolConstants
{
    public const byte ENQ  = 0x05;
    public const byte ACK  = 0x06;
    public const byte NAK  = 0x15;
    public const byte STX  = 0x02;
    public const byte ETX  = 0x03;
    public const byte DLE  = 0x10;
    public const byte ESC2 = 0x1F;

    // Remote operation commands (section 1.3.9)
    public const byte RemoteInkStart   = 0x31;
    public const byte RemoteInkStop    = 0x32;
    public const byte RemoteDeflectOn  = 0x33;
    public const byte RemoteDeflectOff = 0x34;
    public const byte RemoteErrorReset = 0x35;

    // Online/offline (section 1.3.8)
    public const byte OnlineCmd  = 0x31;
    public const byte OfflineCmd = 0x32;

    // ESC2 command headers (section 1.4.2)
    public const byte HdrPrintRecall   = 0x20;
    public const byte HdrPrintRegister = 0x21;
    public const byte HdrOnline        = 0x24;
    public const byte HdrRemoteOp      = 0x25;
    public const byte HdrTimeCtrl      = 0x26;
    public const byte HdrItemDelete    = 0x2B;
    public const byte HdrItemCount     = 0x7B;
    public const byte HdrCountReset    = 0x29;

    public const int MaxFrameSize = 3000;

    // Item 1 = 0x31, item 2 = 0x32, ... (ASCII digit encoding)
    private static readonly byte[] ItemCodes =
    [
        0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A,
        0x3B, 0x3C, 0x3D, 0x3E, 0x3F, 0x40, 0x41, 0x42, 0x43, 0x44,
        0x45, 0x46, 0x47, 0x48, 0x49, 0x4A, 0x4B, 0x4C, 0x4D, 0x4E,
        0x4F, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
        0x59, 0x5A, 0x5B, 0x5C, 0x5D, 0x5E, 0x5F, 0x60, 0x61, 0x62,
    ];

    public static byte GetItemCode(int itemNumber)
    {
        if (itemNumber < 1 || itemNumber > ItemCodes.Length)
            throw new ArgumentOutOfRangeException(
                nameof(itemNumber),
                $"Item number must be 1–{ItemCodes.Length}, got {itemNumber}.");

        return ItemCodes[itemNumber - 1];
    }

    public static byte CalculateBcc(byte[] data)
    {
        byte result = 0;
        foreach (var b in data)
            result ^= b;
        return result;
    }
}
