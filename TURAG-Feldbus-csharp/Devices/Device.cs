using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Devices
{
    public class Device
    {
        public Device(int address, TransportAbstraction busAbstraction)
        {
            this.Address = address;
            this.BusAbstraction = busAbstraction;
            this.Info = null;
        }

        public TransportAbstraction BusAbstraction { get; set; }

        public int Address { get; }

        /// <summary>
        /// Name of the device. A call to Initialize() is required 
        /// before usage, but a valid string will always be returned nonetheless.
        /// </summary>
        public string Name
        {
            get
            {
                if (Info == null)
                {
                    return "uninitialized";
                }
                else
                {
                    return Info.Name;
                }
            }
        }

        public MasterStatistics Statistics
        {
            get
            {
                return new MasterStatistics(checksumErrors, noAnswer, missingData, transmitErrors, successfulTransmissions);
            }
        }

        /// <summary>
        /// Device information. Call Initialize() or GetDeviceInfo() before usage:
        /// Returns null if neither of those functions were called or their execution failed.
        /// </summary>
        public DeviceInfo Info { get; private set; }

        /// <summary>
        /// Initializes the object. Should be called before usage. Overriding
        /// classes have to call the base implementation.
        /// </summary>
        /// <returns>True on success, false otherwise.</returns>
        public virtual bool Initialize()
        {
            return InitializeAsyncInternal(sync: false).GetAwaiter().GetResult();
        }

        public virtual Task<bool> InitializeAsync()
        {
            return InitializeAsyncInternal(sync: false);
        }

        private async Task<bool> InitializeAsyncInternal(bool sync)
        {
            if (!fullyInitialized)
            {
                if ((sync ? GetDeviceInfo() : await GetDeviceInfoAsync()) == null)
                {
                    return false;
                }

                string deviceName = sync ? ReceiveString(0x00, Info.NameLength) : await ReceiveStringAsync(0x00, Info.NameLength);
                if (deviceName == null)
                {
                    return false;
                }

                string versionInfo = sync ? ReceiveString(0x02, Info.VersioninfoLength) : await ReceiveStringAsync(0x02, Info.VersioninfoLength);
                if (versionInfo == null)
                {
                    return false;
                }

                Info = new DeviceInfo(Info, deviceName, versionInfo);
                fullyInitialized = true;
            }

            return true;
        }

        /// <summary>
        /// Checks device availability by sending a ping packet.
        /// </summary>
        /// <returns>True if the device responded, false otherwise.</returns>
        public bool SendPing()
        {
            BusRequest request = new BusRequest();
            return Transceive(request, 0).Success;
        }

        public async Task<bool> SendPingAsync()
        {
            BusRequest request = new BusRequest();
            return (await TransceiveAsync(request, 0)).Success;
        }

        /// <summary>
        /// Returns the device information. This function can be used for device identification
        /// if only protocol and device IDs are required to save a complete initialization.
        /// </summary>
        /// <returns>The device info on success, null otherwise.</returns>
        public DeviceInfo GetDeviceInfo()
        {
            return GetDeviceInfoAsync(sync: true).GetAwaiter().GetResult();
        }

        public Task<DeviceInfo> GetDeviceInfoAsync()
        {
            return GetDeviceInfoAsync(sync: false);
        }


        private async Task<DeviceInfo> GetDeviceInfoAsync(bool sync)
        {
            if (Info == null)
            {
                BusRequest request = new BusRequest();
                request.Write((byte)0);  // device info command

                BusTransceiveResult result = sync ? Transceive(request, 11) : await TransceiveAsync(request, 11);

                if (!result.Success)
                {
                    return null;
                }
                else
                {
                    Info = new DeviceInfo(result.Response);
                }
            }

            return Info;
        }

        /// <summary>
        /// Reads the the package statistics of the slave device.
        /// </summary>
        /// <returns>Instance holding the statistics on success,
        /// null otherwise.</returns>
        public SlaveStatistics ReceiveSlaveStatistics()
        {
            return ReceiveSlaveStatisticsAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public Task<SlaveStatistics> ReceiveSlaveStatisticsAsync()
        {
            return ReceiveSlaveStatisticsAsyncInternal(sync: false);
        }

        private async Task<SlaveStatistics> ReceiveSlaveStatisticsAsyncInternal(bool sync)
        {
            DeviceInfo info = sync ? GetDeviceInfo() : await GetDeviceInfoAsync();
            if (!info.StatisticsAvailable)
            {
                return null;
            }

            BusRequest request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x07);

            BusTransceiveResult result = sync ? Transceive(request, 16) : await TransceiveAsync(request, 16);

            if (!result.Success)
            {
                return null;
            }
            else
            {
                SlaveStatistics statistics = new SlaveStatistics(
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32(),
                    result.Response.ReadUInt32());

                return statistics;
            }
        }

        /// <summary>
        /// Reads the time since power-up from the device.
        /// </summary>
        /// <returns>Uptime of the device in seconds.</returns>
        public double ReceiveUptime()
        {
            return ReceiveUptimeAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public Task<double> ReceiveUptimeAsync()
        {
            return ReceiveUptimeAsyncInternal(sync: false);
        }

        private async Task<double> ReceiveUptimeAsyncInternal(bool sync)
        {
            DeviceInfo info = sync ? GetDeviceInfo() : await GetDeviceInfoAsync();
            if (info.UptimeFrequency == 0.0)
            {
                return Double.NaN;
            }

            BusRequest request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x01);

            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

            if (!result.Success)
            {
                return Double.NaN;
            }
            else
            {
                return (double)result.Response.ReadUInt32() / info.UptimeFrequency;
            }
        }

        private Task<string> ReceiveStringAsync(byte command, int stringLength)
        {
            return ReceiveStringAsyncInternal(command, stringLength, sync: false);
        }

        private string ReceiveString(byte command, int stringLength)
        {
            return ReceiveStringAsyncInternal(command, stringLength, sync: true).GetAwaiter().GetResult();
        }

        private async Task<string> ReceiveStringAsyncInternal(byte command, int stringLength, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write((byte)0);
            request.Write(command);

            BusTransceiveResult result = sync ? Transceive(request, stringLength) : await TransceiveAsync(request, stringLength);

            if (!result.Success)
            {
                return null;
            }
            else
            {
                return Encoding.UTF8.GetString(result.Response.ReadBytes(stringLength));
            }
        }


        static public async Task<List<int>> ScanGaplessBusAsync(int startAdress, TransportAbstraction busAbstraction)
        {
            if (startAdress < 1 || startAdress > 127)
            {
                return new List<int>();
            }

            int address = startAdress;
            List<int> result = new List<int>();

            while (true)
            {
                Device dev = new Device(address, busAbstraction);
                if (!await dev.SendPingAsync())
                {
                    break;
                }
                else
                {
                    result.Add(address);
                    ++address;
                }
            }

            return result;
        }


        protected BusTransceiveResult Transceive(BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(request, responseSize, sync: true).GetAwaiter().GetResult();
        }
        protected Task<BusTransceiveResult> TransceiveAsync(BusRequest request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(request, responseSize, sync: false);
        }
        private async Task<BusTransceiveResult> TransceiveAsyncInternal(BusRequest request, int responseSize, bool sync)
        {
            int attempts = 3;
            BusResponse response = new BusResponse(responseSize);

            while (attempts > 0)
            {
                Tuple<TransportErrorCode, byte[]> result;
                if (sync)
                {
                    result = BusAbstraction.Transceive(Address, request.GetByteArray(), responseSize);
                }
                else
                {
                    result = await BusAbstraction.TransceiveAsync(Address, request.GetByteArray(), responseSize);
                }

                TransportErrorCode transceiveStatus = result.Item1;
                byte[] receiveBuffer = result.Item2;

                switch (transceiveStatus)
                {
                    case TransportErrorCode.Success:
                        response.Fill(receiveBuffer);
                        ++successfulTransmissions;
                        return new BusTransceiveResult(true, response);

                    case TransportErrorCode.ChecksumError:
                        ++checksumErrors;
                        break;

                    case TransportErrorCode.ReceptionError:
                        if (receiveBuffer.Length == 0)
                        {
                            ++noAnswer;
                        }
                        else
                        {
                            ++missingData;
                        }
                        break;

                    case TransportErrorCode.TransmissionError:
                        ++transmitErrors;
                        break;
                }

                attempts--;
            }

            //L.C(this).Debug("!!!!!!!!!!! Transceive 3 attempts failed!");
            return new BusTransceiveResult(false, response);
        }


        protected bool SendBroadcast(BusBroadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: true).GetAwaiter().GetResult();
        }
        protected Task<bool> SendBroadcastAsync(BusBroadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: false);
        }
        private async Task<bool> SendBroadcastAsyncInternal(BusBroadcast broadcast, bool sync)
        {
            int attempts = 3;

            while (attempts > 0)
            {
                TransportErrorCode result = await BusAbstraction.TransmitAsync(Address, broadcast.GetByteArray());

                switch (result)
                {
                    case TransportErrorCode.Success:
                        ++successfulTransmissions;
                        return true;

                    case TransportErrorCode.TransmissionError:
                        ++transmitErrors;
                        break;
                }

                attempts--;
            }

            return false;
        }

        private uint successfulTransmissions = 0;
        private uint checksumErrors = 0;
        private uint noAnswer = 0;
        private uint missingData = 0;
        private uint transmitErrors = 0;
        private bool fullyInitialized = false;
    }
}
