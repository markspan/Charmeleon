namespace Charmeleon
{
    /// <summary>Serialisable snapshot of an electrode configuration, used for configuration save/load.</summary>
    public class ElectrodeControlData
    {
        public bool   IsActive        { get; set; }
        public required string LabelText { get; set; }
        public int    HardwareChannel { get; set; }
        public double? Angle          { get; set; }
        public double? Radius         { get; set; }
        public double? X              { get; set; }
        public double? Y              { get; set; }
    }
}
