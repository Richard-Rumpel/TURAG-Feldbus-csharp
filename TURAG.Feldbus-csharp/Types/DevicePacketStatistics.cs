namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Contains information about the number of successful and failed transmissions
    /// of the device with its bus host.
    /// </summary>
    public class DevicePacketStatistics
    {
        internal DevicePacketStatistics(uint correct, uint overflow, uint lost, uint chksum_error)
        {
            NoError = correct;
            BufferOverflow = overflow;
            LostPackets = lost;
            ChecksumError = chksum_error;
        }

        /// <summary>
        /// Number of successful transmissions.
        /// </summary>
        public uint NoError { get; }

        /// <summary>
        /// Number of packets which had to be discarded because they 
        /// didn't fit in the input buffer.
        /// </summary>
        public uint BufferOverflow { get; }

        /// <summary>
        /// number of packets which were not processed when a new packet
        /// was written to the input buffer.
        /// </summary>
        public uint LostPackets { get; }

        /// <summary>
        /// Number of packets which were discarded due to a checksum mismatch.
        /// </summary>
        public uint ChecksumError { get; }
    }
}
