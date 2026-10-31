namespace Charmeleon
{
    /// <summary>How Charmeleon was started: the montage to open and what to do with it on closing.</summary>
    /// <param name="Montage">Montage (.json) to open instead of the default 10/20 layout; null for none.</param>
    /// <param name="SaveOnClose">
    /// Save changes to <paramref name="Montage"/> without asking when the window closes. For a program that
    /// starts Charmeleon and closes it again, such as the OpenViBE UG acquisition server on Play.
    /// </param>
    /// <param name="ResultFile">
    /// Where to write, on closing, which montage file Charmeleon ended with and whether it was saved; null
    /// for nowhere. See <see cref="MainGUI.WriteResult"/>.
    /// </param>
    /// <param name="Amplifier">
    /// "eego" or "tmsi" to open only that kind of amplifier, as the program that started Charmeleon uses; null
    /// to try eego, then TMSi.
    /// </param>
    /// <param name="Device">The amplifier to open: a TMSi device name or an eego serial number; null for the first one.</param>
    /// <param name="Protocol">How to reach a TMSi amplifier: USB, WiFi, Network or Bluetooth; null for USB.</param>
    public sealed record StartOptions(string? Montage, bool SaveOnClose, string? ResultFile,
        string? Amplifier = null, string? Device = null, string? Protocol = null);
}
