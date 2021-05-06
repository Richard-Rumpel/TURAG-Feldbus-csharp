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
    /// </summary>
    public abstract class BaseDevice
    {
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


        protected BusTransceiveResult Transceive(int address, BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(address, request, responseSize, sync: true).GetAwaiter().GetResult();
        }
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


        protected BusTransceiveResult TransceiveBroadcast(BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(BroadcastAddress, request, responseSize, sync: true).GetAwaiter().GetResult();
        }
        protected Task<BusTransceiveResult> TransceiveBroadcastAsync(BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(BroadcastAddress, request, responseSize, sync: false);
        }


        protected ErrorCode SendBroadcast(BusBroadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: true).GetAwaiter().GetResult();
        }
        protected Task<ErrorCode> SendBroadcastAsync(BusBroadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: false);
        }
        private async Task<ErrorCode> SendBroadcastAsyncInternal(BusBroadcast broadcast, bool sync)
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
