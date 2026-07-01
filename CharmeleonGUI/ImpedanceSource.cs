using System.Text.Json;
using System.Text.Json.Serialization;

namespace Charmeleon
{
    /// <summary>
    /// Thread-safe snapshot of the current electrode state, serialised to JSON for
    /// the Web View stream. Head-map electrodes carry a polar position
    /// (angle, radius); AUX channels (Left/Right/Top/Bottom) carry a fractional
    /// screen position (x, y). The UI thread calls <see cref="Update"/> a few times
    /// per second; the web server thread reads <see cref="GetJson"/> once per second.
    /// </summary>
    static class ImpedanceSource
    {
        /// <summary>One electrode in the snapshot. Null position fields are omitted from the JSON.</summary>
        record ElectrodeSnap(
            string Label, double KOhm, bool Active,
            double? Angle, double? Radius, double? X, double? Y);

        static ElectrodeSnap[] _state = [];
        static readonly object _lock = new();

        static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>
        /// Rebuilds the snapshot from the current electrode set. Inactive electrodes
        /// and those without a live channel report a kOhm of -1; electrodes with no
        /// position are skipped.
        /// </summary>
        public static void Update(
            double[] kOhm,
            Dictionary<string, ElectrodeState> electrodes,
            Dictionary<string, (double Angle, double Radius)> positions,
            Dictionary<string, (double X, double Y)> auxFractions)
        {
            var list = new List<ElectrodeSnap>(electrodes.Count);
            foreach (var (name, el) in electrodes)
            {
                int ch = el.HardwareChannel - 1;
                double val = (ch >= 0 && ch < kOhm.Length) ? kOhm[ch] : -1;
                bool active = el.IsActive && val >= 0;
                double snap = active ? val : -1;

                if (positions.TryGetValue(name, out var p))
                    list.Add(new ElectrodeSnap(el.LabelText, snap, active, p.Angle, p.Radius, null, null));
                else if (auxFractions.TryGetValue(name, out var f))
                    list.Add(new ElectrodeSnap(el.LabelText, snap, active, null, null, f.X, f.Y));
                // else: electrode has no position, skip it.
            }
            lock (_lock) { _state = [.. list]; }
        }

        /// <summary>The current snapshot as <c>{"electrodes":[...]}</c>.</summary>
        public static string GetJson()
        {
            ElectrodeSnap[] snap;
            lock (_lock) { snap = _state; }
            return JsonSerializer.Serialize(new { electrodes = snap }, _jsonOpts);
        }

        /// <summary>
        /// Returns the head map's 256-entry colour table as a JSON array of
        /// "#rrggbb" strings, so the Web View colours electrodes identically
        /// (including a custom <c>Resources/heat.map</c> if one is present).
        /// </summary>
        public static string GetColorMapJson()
        {
            var cm = HeadMapView.ColorMap;
            var hex = new string[cm.Length];
            for (int i = 0; i < cm.Length; i++)
                hex[i] = $"#{cm[i].R:X2}{cm[i].G:X2}{cm[i].B:X2}";
            return JsonSerializer.Serialize(hex);
        }
    }
}
