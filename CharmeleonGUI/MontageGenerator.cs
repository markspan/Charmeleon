using System.Text.Json;

namespace Charmeleon
{
    /// <summary>
    /// Generates electrode layout JSON files from hardcoded position data,
    /// with no dependency on pre-existing resource files.
    /// </summary>
    static class MontageGenerator
    {
        // ------------------------------------------------------------------ //
        //  10-20 system: standard 64-channel Waveguard positions
        //  Format: (name, angle_deg, radius)  angle: 90=front 0=right -90=back
        // ------------------------------------------------------------------ //
        static readonly (string Name, double Angle, double Radius)[] Positions1020 =
        [
            ("Nz",   90,   5),    ("FPz",  90,   4),    ("AFz",  90,   3),
            ("Fz",   90,   2),    ("FCz",  90,   1),    ("Cz",    0,   0),
            ("CPz", -90,   1),    ("Pz",  -90,   2),    ("POz", -90,   3),
            ("Oz",  -90,   4),    ("Iz",  -90,   5),
            ("T7",  180,   4),    ("C5",  180,   3),    ("C3",  180,   2),
            ("C1",  180,   1),    ("C2",    0,   1),    ("C4",    0,   2),
            ("C6",    0,   3),    ("T8",    0,   4),    ("A2",    0,   5),
            ("A1",  180,   5),
            ("F9",  144,   5),    ("FT9", 162,   5),    ("TP9", 198,   5),
            ("P9",  216,   5),    ("F10",  36,   5),    ("FT10", 18,   5),
            ("TP10",-18,   5),    ("P10", -36,   5),
            ("Fp1", 108,   4),    ("AF7", 126,   4),    ("F7",  144,   4),
            ("FT7", 162,   4),    ("Fp2",  72,   4),    ("AF8",  54,   4),
            ("F8",   36,   4),    ("FT8",  18,   4),
            ("O1", -108,   4),    ("PO7",-126,   4),    ("P7", -144,   4),
            ("TP7",-162,   4),    ("O2",  -72,   4),    ("PO8", -54,   4),
            ("P8",  -36,   4),    ("TP8", -18,   4),
            ("AF4",  68,  3.2),   ("F6",   43,  3.1),   ("F4",   55,  2.5),
            ("F2",   70, 2.15),   ("AF3", 112,  3.2),   ("F5",  137,  3.1),
            ("F3",  125,  2.5),   ("F1",  110, 2.15),
            ("PO4", -68,  3.1),   ("P6",  -43,  3.1),   ("P4",  -55, 2.45),
            ("P2",  -70, 2.15),   ("PO3",-112,  3.1),   ("P5", -137,  3.1),
            ("P3", -125, 2.45),   ("P1", -110, 2.15),
            ("FC5", 158, 3.05),   ("FC3", 151,  2.1),   ("FC1", 135, 1.35),
            ("CP5",-158, 3.05),   ("CP3",-151,  2.1),   ("CP1",-135, 1.35),
            ("FC6",  22, 3.05),   ("FC4",  29,  2.1),   ("FC2",  45, 1.35),
            ("CP6", -22, 3.05),   ("CP4", -29,  2.1),   ("CP2", -45, 1.35),
            ("PO9",-126,   5),    ("PO10",-54,   5),    ("I1", -105,   5),
            ("I2",  -75,   5),
        ];

        // ------------------------------------------------------------------ //
        //  Equidistant Duke layout: 3-D positions from ANT Waveguard .elc
        //  Projection: azimuthal equidistant from crown pole (centroid of
        //  4Z/5Z/5L/5R), back-half stretched by a factor 1.112 for visual balance.
        // ------------------------------------------------------------------ //
        static readonly (string Name, double X, double Y, double Z)[] Equidistant3D =
        [
            ("0Z",   87.8245,   0,      13.6565), ("1Z",   76.9711,   0,      47.3542),
            ("2Z",   51.6656,   0,      77.9432), ("3Z",   18.6706,   0,      95.5387),
            ("4Z",  -21.8995,   0,      99.3952), ("1L",   83.3535,  27.6597, 16.9347),
            ("1R",   83.3535, -27.6597, 16.9347), ("1LB",  68.4562,  50.3075, 33.4834),
            ("1RB",  68.4562, -50.3075, 33.4834), ("2L",   64.7129,  30.2248, 57.8036),
            ("2R",   64.7129, -30.2248, 57.8036), ("3L",   36.3866,  33.2276, 81.5533),
            ("3R",   36.3866, -33.2276, 81.5533), ("4L",   -1.0257,  34.6423, 93.2723),
            ("4R",   -1.0257, -34.6423, 93.2723), ("1LC",  65.7062,  63.5721,  3.1967),
            ("1RC",  65.7062, -63.5721,  3.1967), ("2LB",  41.6311,  78.0534, 33.0701),
            ("2RB",  41.6311, -78.0534, 33.0701), ("1LA",  22.8709,  68.2678, 64.5920),
            ("1RA",  22.8709, -68.2678, 64.5920), ("1LD",  49.2067,  71.9482,-33.0758),
            ("1RD",  49.2067, -71.9482,-33.0758), ("2LC",  30.7762,  90.1660, -6.2986),
            ("2RC",  30.7762, -90.1660, -6.2986), ("3LB",  -2.1167,  93.2051, 34.3010),
            ("3RB",  -2.1167, -93.2051, 34.3010), ("3LC", -15.8599,  98.8822, -9.7362),
            ("3RC", -15.8599, -98.8822, -9.7362), ("2LD",  10.4223,  86.3764,-44.1832),
            ("2RD",  10.4223, -86.3764,-44.1832), ("3RD", -38.0710, -77.5329,-55.6841),
            ("3LD", -38.0710,  77.5329,-55.6841), ("9Z", -103.3483,   0,     -36.2438),
            ("8Z", -110.2933,   0,      -3.4175), ("7Z", -105.4146,   0,      31.4901),
            ("6Z",  -86.7818,   0,      64.8139), ("5Z",  -58.3466,   0,      87.9404),
            ("10L", -94.5802,  22.2922,-48.4815), ("10R", -94.5802, -22.2922,-48.4815),
            ("9L", -104.5412,  26.0469,-20.6850), ("9R", -104.5412, -26.0469,-20.6850),
            ("8L", -105.1909,  30.1789, 10.1373), ("8R", -105.1909, -30.1789, 10.1373),
            ("7L",  -92.1323,  33.1928, 47.3398), ("7R",  -92.1323, -33.1928, 47.3398),
            ("6L",  -72.4086,  33.8624, 70.9695), ("6R",  -72.4086, -33.8624, 70.9695),
            ("5L",  -39.4855,  35.5221, 88.9334), ("5R",  -39.4855, -35.5221, 88.9334),
            ("4LD", -74.5152,  52.5999,-55.0679), ("4RD", -74.5152, -52.5999,-55.0679),
            ("5LC", -89.1414,  57.3593,-21.5492), ("5RC", -89.1414, -57.3593,-21.5492),
            ("5LB", -87.4983,  61.2348, 17.2156), ("5RB", -87.4983, -61.2348, 17.2156),
            ("3LA", -65.5805,  60.7839, 57.1816), ("3RA", -65.5805, -60.7839, 57.1816),
            ("2LA", -26.7349,  70.2637, 69.1606), ("2RA", -26.7349, -70.2637, 69.1606),
            ("4LC", -60.2154,  84.4588,-17.8386), ("4RC", -60.2154, -84.4588,-17.8386),
            ("4LB", -49.8487,  88.0386, 25.6631), ("4RB", -49.8487, -88.0386, 25.6631),
        ];

        // ------------------------------------------------------------------ //
        //  Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns 10-20 layout entries with consecutive hardware channels (1, 2, 3, ...)
        /// in position order, so a freshly created layout is immediately usable.
        /// </summary>
        public static Dictionary<string, ElectrodeControlData> Generate1020()
        {
            return Positions1020
                .Select((p, i) => (p, i))
                .ToDictionary(
                    t => t.p.Name,
                    t => new ElectrodeControlData
                    {
                        LabelText       = t.p.Name,
                        HardwareChannel = t.i + 1,
                        IsActive        = true,
                        Angle           = t.p.Angle,
                        Radius          = t.p.Radius
                    });
        }

        /// <summary>Returns equidistant layout entries projected from the cap crown.</summary>
        public static Dictionary<string, ElectrodeControlData> GenerateEquidistant()
        {
            // Crown pole = centroid of 4Z/5Z/5L/5R on unit sphere
            var crownNames = new[] { "4Z", "5Z", "5L", "5R" };
            var pos = Equidistant3D.ToDictionary(e => e.Name, e => (e.X, e.Y, e.Z));

            (double x, double y, double z) pole = (0, 0, 0);
            foreach (var n in crownNames)
            {
                var (x, y, z) = pos[n];
                pole = (pole.x + x, pole.y + y, pole.z + z);
            }
            pole = Norm((pole.x / 4, pole.y / 4, pole.z / 4));

            // Reference directions in plane perpendicular to pole
            var front = Perp((1.0, 0.0, 0.0), pole);
            var left  = Perp((0.0, 1.0, 0.0), pole);

            // Project each electrode
            var raw = new List<(string Name, double Angle, double Radius)>();
            foreach (var (name, x, y, z) in Equidistant3D)
            {
                var q     = Norm((x, y, z));
                double d  = Math.Max(-1.0, Math.Min(1.0, Dot(pole, q)));
                double colat = (180.0 / Math.PI) * Math.Acos(d);
                var perp = Sub(q, Scale(pole, d));
                double fa = Dot(perp, front);
                double la = Dot(perp, left);
                double az = (180.0 / Math.PI) * Math.Atan2(la, fa);
                double kAngle = 90.0 + az;
                if (kAngle > 180.0) kAngle -= 360.0;
                raw.Add((name, kAngle, colat));
            }

            double maxColat = raw.Max(r => r.Radius);
            double backStretch = 1.112;

            // Consecutive hardware channels (1, 2, 3, ...) in position order so a
            // freshly created layout is immediately usable.
            return raw.Select((r, i) => (r, i)).ToDictionary(
                t => t.r.Name,
                t =>
                {
                    double radius = t.r.Radius / maxColat * 5.0;
                    if (Math.Sin(t.r.Angle * Math.PI / 180.0) < 0)
                        radius *= backStretch;
                    return new ElectrodeControlData
                    {
                        LabelText       = t.r.Name,
                        HardwareChannel = t.i + 1,
                        IsActive        = true,
                        Angle           = Math.Round(t.r.Angle, 2),
                        Radius          = Math.Round(radius, 3)
                    };
                });
        }

        /// <summary>Serialises a layout dict to a JSON file chosen by the user.</summary>
        public static string? SaveToFile(Dictionary<string, ElectrodeControlData> layout)
        {
            using var dlg = new SaveFileDialog
            {
                Filter      = "JSON Files (*.json)|*.json",
                Title       = "Save Layout",
                DefaultExt  = "json"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            File.WriteAllText(dlg.FileName,
                JsonSerializer.Serialize(layout, new JsonSerializerOptions { WriteIndented = true }));
            return dlg.FileName;
        }

        // ------------------------------------------------------------------ //
        //  Vector helpers
        // ------------------------------------------------------------------ //
        static (double x, double y, double z) Norm((double x, double y, double z) v)
        {
            double r = Math.Sqrt(v.x*v.x + v.y*v.y + v.z*v.z);
            return (v.x/r, v.y/r, v.z/r);
        }
        static double Dot((double x,double y,double z) a, (double x,double y,double z) b)
            => a.x*b.x + a.y*b.y + a.z*b.z;
        static (double x,double y,double z) Scale((double x,double y,double z) v, double s)
            => (v.x*s, v.y*s, v.z*s);
        static (double x,double y,double z) Sub((double x,double y,double z) a,(double x,double y,double z) b)
            => (a.x-b.x, a.y-b.y, a.z-b.z);
        static (double x,double y,double z) Perp((double x,double y,double z) v,(double x,double y,double z) p)
            => Norm(Sub(v, Scale(p, Dot(v, p))));
    }
}
