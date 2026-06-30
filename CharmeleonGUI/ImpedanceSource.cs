using System.Globalization;
using System.Text;

namespace Charmeleon
{
    /// <summary>
    /// Thread-safe snapshot of the current electrode state, serialised to JSON for
    /// the Web View stream. Head-map electrodes carry a polar position (angle,
    /// radius); AUX channels (Left/Right/Top/Bottom) carry a fractional screen
    /// position (x, y). All numbers are formatted with InvariantCulture so the
    /// JSON is valid regardless of the machine's locale.
    /// </summary>
    static class ImpedanceSource
    {
        record ElectrodeSnap(
            string Label, double KOhm, bool Active,
            double? Angle, double? Radius, double? X, double? Y);

        static ElectrodeSnap[] _state = [];
        static readonly object _lock  = new();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void Update(
            double[] kOhm,
            Dictionary<string, ElectrodeState> electrodes,
            Dictionary<string, (double Angle, double Radius)> positions,
            Dictionary<string, (double X, double Y)> auxFractions)
        {
            var list = new List<ElectrodeSnap>(electrodes.Count);
            foreach (var (name, el) in electrodes)
            {
                int ch     = el.HardwareChannel - 1;
                double val = (ch >= 0 && ch < kOhm.Length) ? kOhm[ch] : -1;
                bool active = el.IsActive && val >= 0;
                double snapVal = active ? val : -1;

                if (positions.TryGetValue(name, out var p))
                    list.Add(new ElectrodeSnap(el.LabelText, snapVal, active, p.Angle, p.Radius, null, null));
                else if (auxFractions.TryGetValue(name, out var f))
                    list.Add(new ElectrodeSnap(el.LabelText, snapVal, active, null, null, f.X, f.Y));
                // else: electrode has no position, skip it.
            }
            lock (_lock) { _state = [.. list]; }
        }

        public static string GetJson()
        {
            ElectrodeSnap[] snap;
            lock (_lock) { snap = _state; }

            if (snap.Length == 0) return "{\"electrodes\":[]}";

            var sb = new StringBuilder("{\"electrodes\":[", snap.Length * 64);
            for (int i = 0; i < snap.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var e = snap[i];
                sb.Append("{\"label\":\"").Append(e.Label).Append('"')
                  .Append(",\"kOhm\":").Append((e.Active ? e.KOhm : -1).ToString("F1", Inv))
                  .Append(",\"active\":").Append(e.Active ? "true" : "false");
                if (e.Angle.HasValue && e.Radius.HasValue)
                    sb.Append(",\"angle\":").Append(e.Angle.Value.ToString("F2", Inv))
                      .Append(",\"radius\":").Append(e.Radius.Value.ToString("F3", Inv));
                else if (e.X.HasValue && e.Y.HasValue)
                    sb.Append(",\"x\":").Append(e.X.Value.ToString("F4", Inv))
                      .Append(",\"y\":").Append(e.Y.Value.ToString("F4", Inv));
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Returns Charmeleon's 256-entry colour table as a JSON array of "#rrggbb"
        /// strings, so Web View can colour electrodes identically (including a
        /// custom Resources/heat.map if one is present).
        /// </summary>
        public static string GetColorMapJson()
        {
            var cm = ElectrodeState.ColorMap;
            var sb = new StringBuilder("[", cm.Length * 12);
            for (int i = 0; i < cm.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append($"\"#{cm[i].R:X2}{cm[i].G:X2}{cm[i].B:X2}\"");
            }
            sb.Append(']');
            return sb.ToString();
        }
    }
}
