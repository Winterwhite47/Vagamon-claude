using System.Globalization;

namespace Hushlands.Core
{
    /// <summary>Hex colour parsing ("#RRGGBB"), engine-free.</summary>
    public static class Palette
    {
        public static bool TryParse(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (string.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#') return false;
            if (!int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            r = ((v >> 16) & 0xFF) / 255f;
            g = ((v >> 8) & 0xFF) / 255f;
            b = (v & 0xFF) / 255f;
            return true;
        }
    }
}
