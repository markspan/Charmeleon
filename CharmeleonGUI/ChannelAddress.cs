using System.Text.RegularExpressions;

namespace Charmeleon
{
    /// <summary>The kinds of amplifier channel an electrode can be wired to.</summary>
    public enum ChannelKind
    {
        /// <summary>A referential EEG channel (TMSi EXG, eego reference); numbered by HardwareChannel.</summary>
        Eeg,
        /// <summary>A bipolar channel, BIP1, BIP2, ...</summary>
        Bipolar,
        /// <summary>An auxiliary channel, AUX1, AUX2, ...</summary>
        Auxiliary,
        /// <summary>Any other channel (digital, sawtooth, ...), which a montage does not name.</summary>
        Other,
    }

    /// <summary>
    /// An amplifier channel as a montage names it: a kind and a 1-based number among the channels of that
    /// kind, in the amplifier's own order. EEG channels are written as their number ("32"), the others as
    /// prefix and number ("BIP1", "AUX2"); 0 is "no channel".
    /// </summary>
    public readonly record struct ChannelAddress(ChannelKind Kind, int Number)
    {
        static readonly Regex _pattern = new(@"^\s*(?:(BIP|AUX)\s*)?0*(\d{1,3})\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>No channel: the electrode is not wired.</summary>
        public static ChannelAddress None => new(ChannelKind.Eeg, 0);

        /// <summary>True when this names a channel at all.</summary>
        public bool IsAssigned => Number > 0;

        /// <summary>"BIP" or "AUX" for those kinds, empty for EEG.</summary>
        public static string Prefix(ChannelKind kind) => kind switch
        {
            ChannelKind.Bipolar => "BIP",
            ChannelKind.Auxiliary => "AUX",
            _ => "",
        };

        /// <summary>Parses "32", "BIP1", "aux 2"; false for anything else.</summary>
        public static bool TryParse(string? text, out ChannelAddress address)
        {
            address = None;
            var match = _pattern.Match(text ?? "");
            if (!match.Success || !int.TryParse(match.Groups[2].Value, out int number)) return false;
            var kind = match.Groups[1].Value.ToUpperInvariant() switch
            {
                "BIP" => ChannelKind.Bipolar,
                "AUX" => ChannelKind.Auxiliary,
                _ => ChannelKind.Eeg,
            };
            // BIP0 means nothing; plain 0 is "no channel"
            if (kind != ChannelKind.Eeg && number == 0) return false;
            address = new ChannelAddress(kind, number);
            return true;
        }

        /// <summary>"32", "BIP1", ...: how the channel label and a montage's Channel field show it.</summary>
        public override string ToString() => Prefix(Kind) + Number;
    }

    /// <summary>
    /// The channels an amplifier has, by kind; what a montage records under "Amplifier" so that a driver
    /// can tell when a montage was made for another amplifier.
    /// </summary>
    /// <param name="Type">The amplifier's name, e.g. "TMSi Refa" or "eego EE-225".</param>
    public sealed record AmplifierLayout(string Type, int EegChannels, int BipolarChannels, int AuxiliaryChannels)
    {
        /// <summary>The layout assumed without an amplifier (demo mode), which bounds nothing in practice.</summary>
        public static AmplifierLayout Demo { get; } = new("", 64, 8, 8);

        /// <summary>Number of channels of <paramref name="kind"/>.</summary>
        public int Count(ChannelKind kind) => kind switch
        {
            ChannelKind.Eeg => EegChannels,
            ChannelKind.Bipolar => BipolarChannels,
            ChannelKind.Auxiliary => AuxiliaryChannels,
            _ => 0,
        };

        /// <summary>True when the amplifier has the channel <paramref name="address"/> names.</summary>
        public bool Has(ChannelAddress address) => address.IsAssigned && address.Number <= Count(address.Kind);

        /// <summary>"64 EEG, 4 BIP and 4 AUX channels".</summary>
        public string Describe() => $"{EegChannels} EEG, {BipolarChannels} BIP and {AuxiliaryChannels} AUX channels";
    }
}
