namespace Charmeleon
{
    /// <summary>
    /// Runtime state of one electrode on the head map: its label, the amplifier
    /// channel it is wired to, whether it is in use, its latest impedance and its
    /// on-screen position. Drawn directly by the form in a single OnPaint pass,
    /// which gives true transparency with no overlapping control rectangles.
    /// Process-wide view settings live on <see cref="HeadMapView"/>.
    /// </summary>
    public class ElectrodeState
    {
        /// <summary>Electrode name shown on the map (for example "Cz").</summary>
        public string LabelText { get; set; } = "";

        /// <summary>
        /// Number of the amplifier channel this electrode is wired to, among the channels of its
        /// <see cref="Kind"/> (1-based; 0 = unassigned). For EEG this is the montage's HardwareChannel.
        /// </summary>
        public int HardwareChannel { get; set; }

        /// <summary>The kind of channel the electrode is wired to: EEG unless set to a BIP or AUX channel.</summary>
        public ChannelKind Kind { get; set; } = ChannelKind.Eeg;

        /// <summary>The channel the electrode is wired to, as kind and number.</summary>
        public ChannelAddress Address
        {
            get => new(Kind, HardwareChannel);
            set { Kind = value.Kind; HardwareChannel = value.Number; }
        }

        /// <summary>False when the amplifier measures no impedance on the electrode's channel.</summary>
        public bool HasImpedance { get; set; } = true;

        /// <summary>Whether this electrode is in use for the current recording.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Most recent impedance, clamped to the 0-255 kOhm colour range.</summary>
        public int Value { get; set; }

        /// <summary>On-screen centre of the electrode circle.</summary>
        public Point Center { get; set; }

        /// <summary>True while the inline channel-number editor is open over this electrode's channel label.</summary>
        internal bool Editing { get; set; }
    }
}
