using System.Text.Json.Serialization;

namespace Charmeleon
{
    /// <summary>
    /// Serialisable snapshot of one electrode's configuration, used for montage
    /// save/load (JSON). Head-map electrodes carry Angle/Radius; AUX markers carry
    /// X/Y (fractional screen position). The unused pair is null for each kind.
    /// </summary>
    public class ElectrodeControlData
    {
        /// <summary>Whether the electrode is in use for the recording.</summary>
        public bool IsActive { get; set; }

        /// <summary>Electrode name (for example "Cz").</summary>
        public required string LabelText { get; set; }

        /// <summary>Amplifier hardware channel (1-based; 0 = unassigned): the n-th EEG channel.</summary>
        public int HardwareChannel { get; set; }

        /// <summary>Head-map polar angle in degrees (90 = front, 0 = right, -90 = back); null for AUX.</summary>
        public double? Angle { get; set; }

        /// <summary>Head-map polar radius in ring units (0 = centre, 5 = rim); null for AUX.</summary>
        public double? Radius { get; set; }

        /// <summary>AUX fractional screen X (0-1); null for head-map electrodes.</summary>
        public double? X { get; set; }

        /// <summary>AUX fractional screen Y (0-1); null for head-map electrodes.</summary>
        public double? Y { get; set; }

        /// <summary>
        /// A channel other than EEG, as "BIP1", "AUX2", ...; it takes precedence over
        /// <see cref="HardwareChannel"/>, which is then 0 so that older readers leave the electrode out
        /// instead of putting it on an EEG channel. Null (and not written) for EEG. Since 2.2.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Channel { get; set; }

        /// <summary>
        /// Angle from the vertex (Cz) in degrees on a spherical head, when known exactly; readers fall
        /// back to 22.5 x <see cref="Radius"/>. Null (and not written) when not known. Since 2.2.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Theta { get; set; }

        /// <summary>
        /// Azimuth in degrees on a spherical head (0 = right, 90 = front, as <see cref="Angle"/>), when
        /// known exactly; readers fall back to <see cref="Angle"/>. Null (and not written) when not known.
        /// Since 2.2.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Phi { get; set; }
    }
}
