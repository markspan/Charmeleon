using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Charmeleon
{
    /// <summary>The contents of a montage file: its electrodes, in file order, and the amplifier it was made for.</summary>
    /// <param name="Electrodes">Each electrode under its key (its position's name, e.g. "Cz" or "Left").</param>
    /// <param name="Amplifier">The amplifier the montage was saved with, when the file says.</param>
    /// <param name="Problems">Entries that could not be read, each as a sentence.</param>
    public sealed record Montage(
        IReadOnlyList<KeyValuePair<string, ElectrodeControlData>> Electrodes,
        AmplifierLayout? Amplifier,
        IReadOnlyList<string> Problems);

    /// <summary>
    /// Reads and writes montage files. A montage is a JSON object of entries keyed by electrode, plus,
    /// since Charmeleon 2.2, an "Amplifier" entry that describes the amplifier it was saved with:
    /// <code>
    /// { "Cz":        { "IsActive": true, "LabelText": "Cz", "HardwareChannel": 16, "Angle": 0, "Radius": 0 },
    ///   "Right":     { "IsActive": true, "LabelText": "HEOG", "HardwareChannel": 0, "Channel": "BIP1", "X": 0.85, "Y": 0.72 },
    ///   "Amplifier": { "Type": "TMSi Refa", "EEGChannels": 64, "BipolarChannels": 4, "AuxiliaryChannels": 4 } }
    /// </code>
    /// The OpenViBE UG acquisition server reads the same files (its CharmeleonMontage reader).
    /// </summary>
    public static class MontageFile
    {
        /// <summary>Key of the amplifier entry, which is not an electrode.</summary>
        const string AmplifierKey = "Amplifier";

        static readonly JsonSerializerOptions _readOptions = new() { PropertyNameCaseInsensitive = true };

        static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

        /// <summary>Reads the montage in <paramref name="path"/>.</summary>
        /// <exception cref="IOException">The file can not be read.</exception>
        /// <exception cref="InvalidDataException">The file is not a montage.</exception>
        public static Montage Load(string path)
        {
            JsonDocument document;
            try { document = JsonDocument.Parse(File.ReadAllText(path)); }
            catch (JsonException ex) { throw new InvalidDataException($"{Path.GetFileName(path)} is not valid JSON: {ex.Message}", ex); }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new InvalidDataException($"{Path.GetFileName(path)} is not a montage (expected a JSON object).");

                var electrodes = new List<KeyValuePair<string, ElectrodeControlData>>();
                var problems = new List<string>();
                AmplifierLayout? amplifier = null;
                foreach (var entry in document.RootElement.EnumerateObject())
                {
                    if (entry.Value.ValueKind != JsonValueKind.Object) continue;

                    if (entry.Name == AmplifierKey && !HasProperty(entry.Value, "LabelText"))
                    {
                        amplifier = ReadAmplifier(entry.Value);
                        continue;
                    }

                    try
                    {
                        var data = entry.Value.Deserialize<ElectrodeControlData>(_readOptions);
                        if (data != null) electrodes.Add(new(entry.Name, data));
                    }
                    catch (JsonException ex) { problems.Add($"{entry.Name} could not be read ({ex.Message})."); }
                }
                return new Montage(electrodes, amplifier, problems);
            }
        }

        /// <summary>
        /// Writes a montage to <paramref name="path"/>, through a temporary file, so that a reader (the
        /// acquisition server) never sees half a file and a failed write leaves the old file as it was.
        /// </summary>
        /// <param name="amplifier">Recorded as the "Amplifier" entry; null to leave it out (demo mode).</param>
        public static void Save(string path, IEnumerable<KeyValuePair<string, ElectrodeControlData>> electrodes, AmplifierLayout? amplifier)
        {
            var root = new JsonObject();
            foreach (var (key, data) in electrodes)
                root[key] = JsonSerializer.SerializeToNode(data, _writeOptions);
            if (amplifier != null)
            {
                root[AmplifierKey] = new JsonObject
                {
                    ["Type"] = amplifier.Type,
                    ["EEGChannels"] = amplifier.EegChannels,
                    ["BipolarChannels"] = amplifier.BipolarChannels,
                    ["AuxiliaryChannels"] = amplifier.AuxiliaryChannels,
                };
            }

            string temporary = path + ".saving";
            File.WriteAllText(temporary, root.ToJsonString(_writeOptions));
            File.Move(temporary, path, overwrite: true);
        }

        static bool HasProperty(JsonElement element, string name)
            => element.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        static AmplifierLayout ReadAmplifier(JsonElement element)
        {
            string type = "";
            int eeg = 0, bipolar = 0, auxiliary = 0;
            foreach (var p in element.EnumerateObject())
            {
                if (p.NameEquals("Type") && p.Value.ValueKind == JsonValueKind.String) type = p.Value.GetString() ?? "";
                else if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out int n))
                {
                    if (p.NameEquals("EEGChannels")) eeg = n;
                    else if (p.NameEquals("BipolarChannels")) bipolar = n;
                    else if (p.NameEquals("AuxiliaryChannels")) auxiliary = n;
                }
            }
            return new AmplifierLayout(type, eeg, bipolar, auxiliary);
        }
    }
}
