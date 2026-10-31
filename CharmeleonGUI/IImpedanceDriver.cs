namespace Charmeleon
{
    /// <summary>
    /// Common interface for impedance-measurement backends (EEGO and TMSi).
    /// Implementations must be safe to call from the UI thread at ~3 Hz.
    /// </summary>
    public interface IImpedanceDriver : IDisposable
    {
        /// <summary>The amplifier's channels by kind, which bound the channels a montage can use.</summary>
        AmplifierLayout Layout { get; }

        /// <summary>
        /// The channel each value of <see cref="GetImpedancesKOhm"/> belongs to, in the same order. A channel
        /// that is not listed has no impedance measurement (e.g. eego's bipolar channels).
        /// </summary>
        IReadOnlyList<ChannelAddress> ImpedanceChannels { get; }

        /// <summary>
        /// Returns the most recent impedance per channel of <see cref="ImpedanceChannels"/>, in kOhm
        /// (0-255 range). Returns cached values if no new data has arrived yet.
        /// </summary>
        double[] GetImpedancesKOhm();
    }
}
