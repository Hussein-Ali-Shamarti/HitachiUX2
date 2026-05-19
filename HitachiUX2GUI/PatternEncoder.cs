using System.Drawing;

namespace HitachiUX2GUI;

// Renders a column-packed dot matrix pattern as a preview bitmap.
// Each column is 2 bytes (16 bits), MSB = top dot, 1 = print dot.
public static class PatternEncoder
{
    public const int DotHeight = 16;

    public static Bitmap Preview(byte[] data, int scale = 6)
    {
        int cols = data.Length / 2;
        var bmp  = new Bitmap(Math.Max(cols * scale, 1), DotHeight * scale);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White);

        for (int c = 0; c < cols; c++)
        {
            ushort bits = (ushort)((data[c * 2] << 8) | data[c * 2 + 1]);
            for (int r = 0; r < DotHeight; r++)
            {
                if ((bits & (0x8000 >> r)) != 0)
                    g.FillRectangle(Brushes.Black,
                        c * scale, r * scale, scale - 1, scale - 1);
            }
        }
        return bmp;
    }
}
