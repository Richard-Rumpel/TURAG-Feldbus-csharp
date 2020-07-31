namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Contains information about the number of successful and failed transmissions
    /// of the host with the bus device.
    /// </summary>
    public class HostStatistics
    {
        internal HostStatistics(uint checksumErrors, uint noAnswer, uint missingData, uint transmitErrors, uint noErrors)
        {
            ChecksumErrors = checksumErrors;
            NoAnswer = noAnswer;
            MissingData = missingData;
            TransmitErrors = transmitErrors;
            NoErrors = noErrors;
        }

        /// <summary>
        /// Number of packets which were discarded due to a checksum mismatch.
        /// </summary>
        public uint ChecksumErrors { get; }

        /// <summary>
        /// Number of transmissions which were unanswered by the device.
        /// </summary>
        public uint NoAnswer { get; }

        /// <summary>
        /// Number of transmissions whose response was shorter than requested.
        /// </summary>
        public uint MissingData { get; }

        /// <summary>
        /// Number of transmissions which failed due to errors during transmission.
        /// </summary>
        public uint TransmitErrors { get; }

        /// <summary>
        /// Number of successful transmissions.
        /// </summary>
        public uint NoErrors { get; }
    }
}
