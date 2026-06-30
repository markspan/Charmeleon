namespace Charmeleon
{
    /// <summary>
    /// Runtime state of one electrode on the head map.
    /// Pure data â€” drawn directly by the parent form in a single OnPaint pass,
    /// which gives true transparency with no overlapping control rectangles.
    /// </summary>
    public class ElectrodeState
    {
        public string LabelText      { get; set; } = "";
        public int    HardwareChannel{ get; set; } = 0;
        public bool   IsActive       { get; set; } = true;
        public int    Value          { get; set; } = 0;   // 0-255 kOhm
        public Point  Center         { get; set; }
        internal bool Editing        { get; set; } = false;

        // ---- Shared static state (one set per process) ----

        public static bool    viewHWChannel = false;
        public static int     maxChannel    = 64;
        public static int     ScaledSize    = 65;

        public static Color[] ColorMap = File.Exists("Resources/heat.map")
            ? LoadColorMapFromFile("Resources/heat.map")
            : GenerateColorMap();

        public static void ApplyScale(int size) =>
            ScaledSize = Math.Max(24, size);

        static Color[] GenerateColorMap()
        {
            var c = new Color[256];
            c[0] = Color.FromArgb(0, 1, 254, 0);
            for (int i = 1; i < 256; i++)
                c[i] = Color.FromArgb(255, i, 255 - i, 0);
            return c;
        }

        static Color[] LoadColorMapFromFile(string path)
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
