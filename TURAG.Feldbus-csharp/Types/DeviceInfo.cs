namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Class containing basic information about the device.
    /// </summary>
    public class DeviceInfo
    {
        internal DeviceInfo(InternalDeviceInfoPacket info)
        {
            DeviceProtocolId = info.DeviceProtocolId;
            DeviceTypeId = info.DeviceTypeId;
            CrcType = info.CrcType;
            StatisticsAvailable = info.StatisticsAvailable;
            BufferSize = info.BufferSize;
            UptimeFrequency = info.UptimeFrequency;
        }

        /// <summary>
        /// Protocol ID.
        /// </summary>
        public int DeviceProtocolId { get; }

        /// <summary>
        /// Device type ID.
        /// </summary>
        public int DeviceTypeId { get; }

        /// <summary>
        /// CRC mode used by the device.
        /// </summary>
        public int CrcType { get; }

        /// <summary>
        /// Whether the device supports querying its transmission statistics. If 
        /// this field equals false, calls to RetrieveDeviceStatistics() or RetrieveDeviceStatisticsAsync()
        /// will fail.
        /// </summary>
        public bool StatisticsAvailable { get; }

        /// <summary>
        /// Buffer size of the device, defining boundaries on the maximum packet size it can
        /// process.
        /// </summary>
        public int BufferSize { get; }

        /// <summary>
        /// Frequency of the uptime counter of the device.
        /// </summary>
        public int UptimeFrequency { get; }

        /// <summary>
        /// Returns whether the device maintains an uptime counter. If this field equals false,
        /// calls to RetrieveUptime() or RetrieveUptimeAsync() will fail.
        /// </summary>
        public bool UptimeAvailable
        {
            get
            {
                return UptimeFrequency != 0;
            }
        }
    }

    internal class InternalDeviceInfoPacket
    {
        public InternalDeviceInfoPacket(BusResponse response)
        {
            DeviceProtocolId = response.ReadByte();
            DeviceTypeId = response.ReadByte();
            int dummy = response.ReadByte();
            CrcType = dummy & 0x07;
            StatisticsAvailable = (dummy & 0x80) != 0 ? true : false;
            BufferSize = response.ReadUInt16();
            response.ReadUInt16(); // reserved bytes
            NameLength = response.ReadByte();
            VersionInfoLength = response.ReadByte();
            UptimeFrequency = response.ReadUInt16();
        }

        public int DeviceProtocolId { get; }
        public int DeviceTypeId { get; }
        public int CrcType { get; }
        public bool StatisticsAvailable { get; }
        public int BufferSize { get; }
        public int NameLength { get; }
        public int VersionInfoLength { get; }
        public int UptimeFrequency { get; }
    }
}
