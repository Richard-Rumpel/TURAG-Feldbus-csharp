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
    /// Generic device implementing the basic protocol, which is supported by all %TURAG %Feldbus devices.
    /// This class can be used for device discovery and enumeration. 
    /// </summary>
    public class Device : BaseDevice
    {
        private enum CommandKey : byte
        {
            GetDeviceName = 0x00,
            GetUptimeCounter = 0x01,
            GetVersioninfo = 0x02,
            GetNumberOfCorrectPackets = 0x03,
            GetNumberOfBufferOverflows = 0x04,
            GetNumberOfLostPackets = 0x05,
            GetNumberOfChecksumFailures = 0x06,
            GetAllPacketCounters = 0x07,
            ResetPacketCounters = 0x08
        };

        private static readonly string defaultDeviceName = "Feldbus Device";

        /// <summary>
        /// Creates a new instance. Initialize() should be called before calling any
        /// other method.
        /// </summary>
        /// <param name="address">Bus address of the device.</param>
        /// <param name="busAbstraction">Bus to work on.</param>
        public Device(int address, TransportAbstraction busAbstraction) : base(busAbstraction)
        {
            this.Address = address;
            this.BusAbstraction = busAbstraction;
            this.Info = null;
        }


        /// <summary>
        /// Returns the bus address of the device.
        /// </summary>
        public int Address { get; }

        /// <summary>
        /// Name of the device. This is a shortcut to ExtendedInfo.DeviceName. 
        /// A call to RetrieveExtendedDeviceInfo() or RetrieveExtendedDeviceInfoAsync() is required 
        /// before usage, otherwise a default string will be returned.
        /// </summary>
        public string Name
        {
            get
            {
                if (ExtendedInfo == null)
                {
                    return defaultDeviceName;
                }
                else
                {
                    return ExtendedInfo.DeviceName;
                }
            }
        }

        /// <summary>
        /// Returns the device information. Call Initialize() or InitializeAsync() before usage.
        /// Returns null if neither was called or their execution failed.
        /// </summary>
        public DeviceInfo Info { get; private set; }

        private InternalDeviceInfoPacket InternalDeviceInfo { get; set; }

        /// <summary>
        /// Returns the extended device information. Call RetrieveExtendedDeviceInfo() or 
        /// RetrieveExtendedDeviceInfoAsync() before usage.
        /// Returns null if neither was called or their execution failed.
        /// </summary>
        public ExtendedDeviceInfo ExtendedInfo { get; private set; }

        /// <summary>
        /// Initializes the object by retrieving the device information
        /// structure. Should be called before further usage of the class. Overriding
        /// classes have to call the base implementation.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public virtual ErrorCode Initialize()
        {
            return RetrieveDeviceInfo();
        }

        /// <summary>
        /// Initializes the object by retrieving the device information
        /// structure. Should be called before further usage of the class. Overriding
        /// classes have to call the base implementation.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public virtual Task<ErrorCode> InitializeAsync()
        {
            return RetrieveDeviceInfoAsync();
        }



        /// <summary>
        /// Checks device availability by sending a ping packet. A ping packet
        /// is the shortest possible payload.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SendPing()
        {
            BusRequest request = new BusRequest();
            return Transceive(request, 0).TransportError;
        }

        /// <summary>
        /// Checks device availability by sending a ping packet. A ping packet
        /// is the shortest possible payload.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public async Task<ErrorCode> SendPingAsync()
        {
            BusRequest request = new BusRequest();
            return (await TransceiveAsync(request, 0)).TransportError;
        }

        /// <summary>
        /// Retrieves the device information from the device. This function is called
        /// in the context of Initialize(). After a successful call the device info is available from
        /// the Info property.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        private ErrorCode RetrieveDeviceInfo()
        {
            return RetrieveDeviceInfoAsync(sync: true).GetAwaiter().GetResult();
        }

        private Task<ErrorCode> RetrieveDeviceInfoAsync()
        {
            return RetrieveDeviceInfoAsync(sync: false);
        }

        private async Task<ErrorCode> RetrieveDeviceInfoAsync(bool sync)
        {
            if (Info == null)
            {
                BusRequest request = new BusRequest();
                request.Write((byte)0);  // device info command

                BusTransceiveResult result = sync ? Transceive(request, 11) : await TransceiveAsync(request, 11);

                if (result.Success)
                {
                    InternalDeviceInfo = new InternalDeviceInfoPacket(result.Response);
                    Info = new DeviceInfo(InternalDeviceInfo);
                }

                return result.TransportError;
            }

            return ErrorCode.Success;
        }

        /// <summary>
        /// Retrieves extended information about the device. After a successful call this data is available from
        /// the ExtendedInfo property.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode RetrieveExtendedDeviceInfo()
        {
            return RetrieveExtendedDeviceInfoAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieves extended information about the device. After a successful call this data is available from
        /// the ExtendedInfo property.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> RetrieveExtendedDeviceInfoAsync()
        {
            return RetrieveExtendedDeviceInfoAsyncInternal(sync: false);
        }

        private async Task<ErrorCode> RetrieveExtendedDeviceInfoAsyncInternal(bool sync)
        {
            if (ExtendedInfo == null)
            {
                ErrorCode deviceInfoError = sync ? RetrieveDeviceInfo() : await RetrieveDeviceInfoAsync();
                if (deviceInfoError != ErrorCode.Success)
                {
                    return deviceInfoError;
                }

                (ErrorCode nameError, string deviceName) = sync ?
                    RetrieveString(CommandKey.GetDeviceName, InternalDeviceInfo.NameLength) :
                    await RetrieveStringAsync(CommandKey.GetDeviceName, InternalDeviceInfo.NameLength);
                if (nameError != ErrorCode.Success)
                {
                    return nameError;
                }

                (ErrorCode versioninfoError, string versionInfo) = sync ?
                    RetrieveString(CommandKey.GetVersioninfo, InternalDeviceInfo.VersionInfoLength) :
                    await RetrieveStringAsync(CommandKey.GetVersioninfo, InternalDeviceInfo.VersionInfoLength);
                if (versioninfoError != ErrorCode.Success)
                {
                    return versioninfoError;
                }

                ExtendedInfo = new ExtendedDeviceInfo(deviceName, versionInfo);
            }

            return ErrorCode.Success;
        }


        /// <summary>
        /// Retrieves the transmission statistics of the bus device.
        /// </summary>
        /// <param name="statistics">Statistics received from the device.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode RetrieveDeviceStatistics(out DevicePacketStatistics statistics)
        {
            ErrorCode error;
            (error, statistics) = RetrieveDeviceStatisticsAsyncInternal(sync: true).GetAwaiter().GetResult();
            return error;
        }

        /// <summary>
        /// Retrieves the transmission statistics of the bus device.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and an instance
        /// of the class DevicePacketStatistics containing the data received from the device..</returns>
#if __DOXYGEN__
        public Task<Tuple<ErrorCode, DevicePacketStatistics>> RetrieveDeviceStatisticsAsync()
#else
        public Task<(ErrorCode, DevicePacketStatistics)> RetrieveDeviceStatisticsAsync()
#endif
        {
            return RetrieveDeviceStatisticsAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode, DevicePacketStatistics)> RetrieveDeviceStatisticsAsyncInternal(bool sync)
        {
            ErrorCode deviceInfoError = sync ? RetrieveDeviceInfo() : await RetrieveDeviceInfoAsync();
            if (deviceInfoError != ErrorCode.Success)
            {
                return (deviceInfoError, null);
            }

            if (Info.StatisticsAvailable == false)
            {
                return (ErrorCode.DeviceStatisticsNotSupported, null);
            }

            BusRequest request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)CommandKey.GetAllPacketCounters);

            BusTransceiveResult result = sync ? Transceive(request, 16) : await TransceiveAsync(request, 16);

            if (result.Success)
            {
                DevicePacketStatistics statistics = new DevicePacketStatistics(
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32());

                return (ErrorCode.Success, statistics);
            }
            else
            {
                return (result.TransportError, null);
            }
        }

        /// <summary>
        /// Retrieves the time since power-up from the device.
        /// </summary>
        /// <param name="uptime">Uptime of the device in seconds.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode RetrieveUptime(out double uptime)
        {
            ErrorCode error;
            (error, uptime) = RetrieveUptimeAsyncInternal(sync: true).GetAwaiter().GetResult();
            return error;
        }

        /// <summary>
        /// Retrieves the time since power-up from the device.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and
        /// the uptime of the device in seconds..</returns>
#if __DOXYGEN__
        public Task<Tuple<ErrorCode, double>> RetrieveUptimeAsync()
#else
        public Task<(ErrorCode, double)> RetrieveUptimeAsync()
#endif
        {
            return RetrieveUptimeAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode, double)> RetrieveUptimeAsyncInternal(bool sync)
        {
            ErrorCode deviceInfoError = sync ? RetrieveDeviceInfo() : await RetrieveDeviceInfoAsync();
            if (deviceInfoError != ErrorCode.Success)
            {
                return (deviceInfoError, Double.NaN);
            }

            if (Info.UptimeFrequency == 0.0)
            {
                return (ErrorCode.DeviceUptimeNotSupported, Double.NaN);
            }

            BusRequest request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)CommandKey.GetUptimeCounter);

            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

            if (result.Success)
            {
                return (ErrorCode.Success, (double)result.Response.ReadUInt32() / Info.UptimeFrequency);
            }
            else
            {
                return (result.TransportError, Double.NaN);
            }
        }

        private (ErrorCode, string) RetrieveString(CommandKey command, int stringLength)
        {
            return RetrieveStringAsyncInternal((byte)command, stringLength, sync: true).GetAwaiter().GetResult();
        }

        private Task<(ErrorCode, string)> RetrieveStringAsync(CommandKey command, int stringLength)
        {
            return RetrieveStringAsyncInternal((byte)command, stringLength, sync: false);
        }

        private async Task<(ErrorCode, string)> RetrieveStringAsyncInternal(byte command, int stringLength, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write((byte)0);
            request.Write(command);

            BusTransceiveResult result = sync ? Transceive(request, stringLength) : await TransceiveAsync(request, stringLength);

            if (!result.Success)
            {
                return (result.TransportError, null);
            }
            else
            {
                return (result.TransportError, Encoding.UTF8.GetString(result.Response.ReadBytes(stringLength)));
            }
        }

        /// <summary>
        /// Transmit a request to the device and attempt to receive a response.
        /// Use this function to implement higher-level communication functions in sub classes.
        /// </summary>
        /// <param name="request">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>An object containing an error code and the received data.</returns>
        protected BusTransceiveResult Transceive(BusRequest request, int responseSize = 0)
        {
            return Transceive(Address, request, responseSize);
        }

        /// <summary>
        /// Transmit a request to the device and attempt to receive a response.
        /// Use this function to implement higher-level communication functions in sub classes.
        /// </summary>
        /// <param name="request">Request containing the packet data, excluding address and checksum.</param>
        /// <param name="responseSize">Expected response data size, not counting address and checksum.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an object containing an error code and the received data.</returns>
        protected Task<BusTransceiveResult> TransceiveAsync(BusRequest request, int responseSize = 0)
        {
            return TransceiveAsync(Address, request, responseSize);
        }
    }
}
