using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Devices
{
    /// <summary>
    /// Abstract base class which implements mainly utility and transport functions for use in sub classes.
    /// </summary>
    public abstract class BaseDevice
    {
        /// <summary>
        /// Feldbus broadcast address.
        /// </summary>
        public const int BroadcastAddress = 0x00;


        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="busAbstraction">Bus to work on.</param>
        public BaseDevice(TransportAbstraction busAbstraction)
        {
            this.BusAbstraction = busAbstraction;
        }

        /// <summary>
        /// Gets or sets the transport mechanism used for bus communication.
        /// </summary>
        public TransportAbstraction BusAbstraction { get; set; }


        /// <summary>
        /// Returns the transmission statistics of the bus host with this device.
        /// </summary>
        public HostPacketStatistics HostStatistics
        {
            get
            {
                return new HostPacketStatistics(checksumErrors, noAnswer, missingData, transmitErrors, successfulTransmissions);
            }
        }


        /// <summary>
        /// Transmit a request to the given address and attempt to receive a response.
        /// </summary>
        /// <param name="address">Address to send the packet to.</param>
        /// <param name="request">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>An object containing an error code and the received data.</returns>
        protected BusTransceiveResult Transceive(int address, BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(address, request, responseSize, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Transmit a request to the given address and attempt to receive a response.
        /// </summary>
        /// <param name="address">Address to send the packet to.</param>
        /// <param name="request">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an object containing an error code and the received data.</returns>
        protected Task<BusTransceiveResult> TransceiveAsync(int address, BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(address, request, responseSize, sync: false);
        }

        private async Task<BusTransceiveResult> TransceiveAsyncInternal(int address, BusRequest request, int responseSize, bool sync)
        {
            int attempts = 3;
            BusResponse response = new BusResponse(responseSize);
            Types.ErrorCode transceiveStatus = Types.ErrorCode.TransportTransmissionError;

            while (attempts > 0)
            {
                (ErrorCode, byte[]) result = sync ?
                    BusAbstraction.Transceive(address, request.GetByteArray(), responseSize) :
                    await BusAbstraction.TransceiveAsync(address, request.GetByteArray(), responseSize);


                transceiveStatus = result.Item1;
                byte[] receiveBuffer = result.Item2;

                switch (transceiveStatus)
                {
                    case Types.ErrorCode.Success:
                        response.Fill(receiveBuffer);
                        ++successfulTransmissions;
                        return new BusTransceiveResult(Types.ErrorCode.Success, response);

                    case Types.ErrorCode.TransportChecksumError:
                        ++checksumErrors;
                        break;

                    case Types.ErrorCode.TransportReceptionError:
                        if (receiveBuffer.Length == 0)
                        {
                            ++noAnswer;
                        }
                        else
                        {
                            ++missingData;
                        }
                        break;

                    case Types.ErrorCode.TransportTransmissionError:
                        ++transmitErrors;
                        break;
                }

                attempts--;
            }

            return new BusTransceiveResult(transceiveStatus, response);
        }


        /// <summary>
        /// Transmit a broadcast and attempt to receive a response. This function is equivalent to calling
        /// Transceive(), passing BroadcastAddress as the target address.
        /// </summary>
        /// <param name="broadcastRequest">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>An object containing an error code and the received data.</returns>
        protected BusTransceiveResult TransceiveBroadcast(BusRequest broadcastRequest, int responseSize = 0)
        {
            return TransceiveAsyncInternal(BroadcastAddress, broadcastRequest, responseSize, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Transmit a broadcast and attempt to receive a response. This function is equivalent to calling
        /// TransceiveAsync(), passing BroadcastAddress as the target address.
        /// </summary>
        /// <param name="broadcastRequest">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an object containing an error code and the received data.</returns>
        protected Task<BusTransceiveResult> TransceiveBroadcastAsync(BusRequest broadcastRequest, int responseSize = 0)
        {
            return TransceiveAsyncInternal(BroadcastAddress, broadcastRequest, responseSize, sync: false);
        }


        /// <summary>
        /// Transmits a broadcast.
        /// </summary>
        /// <param name="broadcast">Broadcast containing the packet data, excluding address and checksum.</param>
        /// <returns>Error code describing the result of the call.</returns>
        protected ErrorCode SendBroadcast(BusRequest broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Transmits a broadcast.
        /// </summary>
        /// <param name="broadcast">Broadcast containing the packet data, excluding address and checksum.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        protected Task<ErrorCode> SendBroadcastAsync(BusRequest broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: false);
        }

        private async Task<ErrorCode> SendBroadcastAsyncInternal(BusRequest broadcast, bool sync)
        {
            int attempts = 3;

            while (attempts > 0)
            {
                ErrorCode result = sync ?
                    BusAbstraction.Transmit(BroadcastAddress, broadcast.GetByteArray()) :
                    await BusAbstraction.TransmitAsync(BroadcastAddress, broadcast.GetByteArray());

                switch (result)
                {
                    case ErrorCode.Success:
                        ++successfulTransmissions;
                        return ErrorCode.Success;

                    case ErrorCode.TransportTransmissionError:
                        ++transmitErrors;
                        break;
                }

                attempts--;
            }

            return ErrorCode.TransportTransmissionError;
        }


        /// <summary>
        /// Returns the description of the supplied error code.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <returns>String representing the given error code.</returns>
        static public string ErrorString(ErrorCode error)
        {
            string name = error.ToString();
            string description = error.GetAttributeOfType<DescriptionAttribute>().Description;
            return name + ": " + description;
        }


        /// <summary>
        /// Returns a string representation of the given UUID in the form of XXXX-XXXX.
        /// </summary>
        /// <param name="uuid">UUID to format.</param>
        /// <returns>Formatted string.</returns>
        static public string FormatUuid(uint uuid)
        {
            return String.Format("{0:X4}-{1:X4}",
                (uuid >> 16) & 0xFFFF,
                uuid & 0xFFFF);
        }

        private uint successfulTransmissions = 0;
        private uint checksumErrors = 0;
        private uint noAnswer = 0;
        private uint missingData = 0;
        private uint transmitErrors = 0;

    }

    internal static class EnumHelper
    {
        /// <summary>
        /// Gets an attribute on an enum field value
        /// </summary>
        /// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
        /// <param name="enumVal">The enum value</param>
        /// <returns>The attribute of type T that exists on the enum value</returns>
        /// <example><![CDATA[string desc = myEnumVariable.GetAttributeOfType<DescriptionAttribute>().Description;]]></example>
        public static T GetAttributeOfType<T>(this Enum enumVal) where T : System.Attribute
        {
            var type = enumVal.GetType();
            var memInfo = type.GetMember(enumVal.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }
    }
}
