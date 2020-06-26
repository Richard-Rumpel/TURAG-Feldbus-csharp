namespace TURAG.Feldbus.Types
{
    public class DeviceInfo
    {
        public DeviceInfo(BusResponse response)
        {
            DeviceProtocolId = response.ReadByte();
            DeviceTypeId = response.ReadByte();
            int dummy = response.ReadByte();
            CrcType = dummy & 0x07;
            StatisticsAvailable = (dummy & 0x80) != 0 ? true : false;
            BufferSize = response.ReadUInt16();
            response.ReadUInt16(); // reserved bytes
            NameLength = response.ReadByte();
            Name = "???";
            VersioninfoLength = response.ReadByte();
            VersionInfo = "???";
            UptimeFrequency = response.ReadUInt16();
        }

        public DeviceInfo(DeviceInfo info, string name, string versionInfo)
        {
            UptimeFrequency = info.UptimeFrequency;
            BufferSize = info.BufferSize;
            DeviceProtocolId = info.DeviceProtocolId;
            CrcType = info.CrcType;
            NameLength = info.NameLength;
            Name = name;
            VersioninfoLength = info.VersioninfoLength;
            VersionInfo = versionInfo;
            StatisticsAvailable = info.StatisticsAvailable;
        }

        public int UptimeFrequency { get; }

        public bool UptimeAvailable => UptimeFrequency != 0;

        public int BufferSize { get; }

        public int DeviceProtocolId { get; }

        public int DeviceTypeId { get; }

        public int CrcType { get; }

        public int NameLength { get; }

        public string Name { get; }

        public int VersioninfoLength { get; }

        public string VersionInfo { get; }

        public bool StatisticsAvailable { get; }
    }
}
