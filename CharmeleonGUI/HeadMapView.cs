namespace Charmeleon
{
    /// <summary>
    /// Process-wide view configuration for the head map, shared by the main form's
    /// painting and by the Web View snapshot. Kept separate from
    /// <see cref="ElectrodeState"/> (which is per-electrode data) so the two
    /// concerns do not sit on one class.
    /// </summary>
    static class HeadMapView
    {
        /// <summary>
        /// When true, each circle shows the electrode name and its hardware channel
        /// number (montage-editing view) instead of the live impedance value.
        /// </summary>
        public static bool ShowChannels { get; set; }

        /// <summary>
        /// Highest hardware channel that may be assigned to an electrode: the
        /// amplifier's channel count, or 64 in demo mode.
        /// </summary>
        public static int MaxChannel { get; set; } = 64;

        /// <summary>Current on-screen electrode diameter in pixels (from the last scale pass).</summary>
        public static int ElectrodeSize { get; private set; } = 65;

        /// <summary>
        /// 256-entry impedance colour table (green = low, red = high). Loaded from
        /// <c>Resources/heat.map</c> if present, otherwise a built-in green-to-red ramp.
        /// </summary>
        public static Color[] ColorMap { get; } = File.Exists("Resources/heat.map")
            ? LoadColorMap("Resources/heat.map")
            : GenerateColorMap();

        /// <summary>Sets the electrode diameter, clamped to a sensible minimum.</summary>
        public static void SetElectrodeSize(int size) => ElectrodeSize = Math.Max(24, size);

        /// <summary>Built-in fallback ramp: index 0 transparent-ish green, then green to red.</summary>
        static Color[] GenerateColorMap()
        {
            var c = new Color[256];
            c[0] = Color.FromArgb(0, 1, 254, 0);
            for (int i = 1; i < 256; i++)
                c[i] = Color.FromArgb(255, i, 255 - i, 0);
            return c;
        }

        /// <summary>Reads a 256-line "r,g,b" colour table; missing/short lines stay default.</summary>
        static Color[] LoadColorMap(string path)
        {
            var c     = new Color[256];
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < Math.Min(lines.Length, 256); i++)
            {
                var parts = lines[i].Split(',');
                if (parts.Length == 3 &&
                    int.TryParse(parts[0], out int r) &&
                    int.TryParse(parts[1], out int g) &&
                    int.TryParse(parts[2], out int b))
                    c[i] = Color.FromArgb(255, r, g, b);
            }
            return c;
        }
    }
}
