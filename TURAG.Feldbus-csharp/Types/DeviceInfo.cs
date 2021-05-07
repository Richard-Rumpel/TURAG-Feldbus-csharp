namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Class containing basic information about the device.
    /// </summary>
    public class DeviceInfo
    {
        internal DeviceInfo(int deviceProtocolId, int deviceTypeId, int crcType, bool statisticsAvailable, uint uuid, int uptimeFrequency)
        {
            DeviceProtocolId = deviceProtocolId;
            DeviceTypeId = deviceTypeId;
            CrcType = crcType;
            StatisticsAvailable = statisticsAvailable;
            Uuid = uuid;
            UptimeFrequency = uptimeFrequency;
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
        /// UUID of the device.
        /// </summary>
        public uint Uuid { get; }

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
            int dataByte = response.ReadByte();
            LegacyTypePacket = (dataByte & 0x08) != 0 ? false : true;
            CrcType = dataByte & 0x07;
            StatisticsAvailable = (dataByte & 0x80) != 0 ? true : false;

            if (LegacyTypePacket)
            {
                BufferSize = response.ReadUInt16();
                response.ReadUInt16(); // reserved bytes
                NameLength = response.ReadByte();
                VersionInfoLength = response.ReadByte();
            }
            else
            {
                ExtendedDeviceInfoLength = response.ReadUInt16();
                Uuid = response.ReadUInt32();
            }

            UptimeFrequency = response.ReadUInt16();
        }

        public int DeviceProtocolId { get; }

        public int DeviceTypeId { get; }

        public int CrcType { get; }

        public bool StatisticsAvailable { get; }

        public bool LegacyTypePacket { get; }

        public int UptimeFrequency { get; }


        // only for legacy packet

        public int BufferSize { get; }

        public int NameLength { get; }

        public int VersionInfoLength { get; }


        // only for newer packet

        public uint Uuid { get; }

        public int ExtendedDeviceInfoLength { get; }
    }
}
