namespace Charmeleon
{
    /// <summary>
    /// Common interface for impedance-measurement backends (EEGO and TMSi).
    /// Implementations must be safe to call from the UI thread at ~3 Hz.
    /// </summary>
    public interface IImpedanceDriver : IDisposable
    {
        /// <summary>Number of impedance channels available from the device.</summary>
        int ChannelCount { get; }

        /// <summary>
        /// Returns the most recent impedance per channel in kOhm (0-255 range).
        /// Returns cached values if no new data has arrived yet.
        /// </summary>
        double[] GetImpedancesKOhm();
    }
}
