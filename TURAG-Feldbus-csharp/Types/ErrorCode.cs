using System.ComponentModel;

namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Error codes used by all classes in this library. Use 
    /// Devices.Device.ErrorString() to get the description of an error
    /// code.
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// No Error.
        /// </summary>
        [Description("No Error")]
        Success,

        /// <summary>
        /// Unspecified Error.
        /// </summary>
        [Description("Unspecified Error")]
        Unspecified,


        /// <summary>
        /// A checksum mismatch was detected in the received packet.
        /// </summary>
        [Description("A checksum mismatch was detected in the received packet.")]
        TransportChecksumError,

        /// <summary>
        /// An error occured when receiving the response.
        /// </summary>
        [Description("An error occured when receiving the response.")]
        TransportReceptionError,

        /// <summary>
        /// An error occured when sending the request/broadcast.
        /// </summary>
        [Description("An error occured when sending the request/broadcast.")]
        TransportTransmissionError,


        /// <summary>
        /// This device does not support querying its transmission statistics.
        /// </summary>
        [Description("This device does not support querying its transmission statistics.")]
        DeviceStatisticsNotSupported,

        /// <summary>
        /// This device does not support querying its uptime.
        /// </summary>
        [Description("This device does not support querying its uptime.")]
        DeviceUptimeNotSupported,
    }
}
