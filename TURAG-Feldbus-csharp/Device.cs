using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;

namespace TURAG.Feldbus
{
    public class Device
    {
        public class Request : BinaryWriter
        {
            public Request() : base(new MemoryStream())
            {
            }

            public byte[] GetByteArray()
            {
                MemoryStream stream = (MemoryStream)BaseStream;
                byte[] data = new byte[stream.Length];
                Array.Copy(stream.GetBuffer(), data, stream.Length);
                return data;
            }
        }

        public class Broadcast : Request
        {
        }

        public class Response : BinaryReader
        {
            public Response(int size = 0) : base(new MemoryStream(size))
            {

            }

            public long Capacity { get => ((MemoryStream)BaseStream).Capacity; }

            public void Fill(byte[] data)
            {
                ((MemoryStream)BaseStream).Write(data, 0, data.Length);
                ((MemoryStream)BaseStream).Seek(0, SeekOrigin.Begin);
            }
        }

        public class TransceiveResult
        {
            public TransceiveResult(bool success, Response response)
            {
                this.success = success;
                this.response = response;
            }

            public bool Success => success;
            public Response Response => response;

            readonly bool success;
            readonly Response response;
        }

        public class DeviceInfo
        {
            public DeviceInfo(Response response)
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

        public class SlaveStatistics
        {
            public SlaveStatistics(UInt32 correct, UInt32 overflow, UInt32 lost, UInt32 chksum_error)
            {
                NoError = correct;
                BufferOverflow = overflow;
                LostPackets = lost;
                ChecksumError = chksum_error;
            }

            public UInt32 NoError { get; }
            public UInt32 BufferOverflow { get; }
            public UInt32 LostPackets { get; }
            public UInt32 ChecksumError { get; }
        }

        public class MasterStatistics
        {
            public MasterStatistics (Device device)
            {
                this.device = device;
            }

            public UInt32 ChecksumErrors => device.checksumErrors;
            public UInt32 NoAnswer => device.noAnswer;
            public UInt32 MissingData => device.missingData;
            public UInt32 TransmitErrors => device.transmitErrors;
            public UInt32 NoErrors => device.successfulTransmissions;

            readonly Device device;
        }


        public Device(int address, TransportAbstraction busAbstraction)
        {
            this.Address = address;
            this.BusAbstraction = busAbstraction;
            this.Info = null;
            this.Statistics = new MasterStatistics(this);
        }

        public TransportAbstraction BusAbstraction { get; set; }

        public int Address { get; }

        /// <summary>
        /// Name of the device. A call to Call InitializeAsync() is required 
        /// before usage, but a valid string will always be returned nonetheless.
        /// </summary>
        public string Name
        {
            get
            {
                if (Info == null)
                {
                    return "???";
                }
                else
                {
                    return Info.Name;
                }
            }
        }

        public MasterStatistics Statistics { get; }

        /// <summary>
        /// Device information. Call InitializeAsync() or GetDeviceInfoAsync() before usage:
        /// Returns null if neither of those functions were called or their execution failed.
        /// </summary>
        public DeviceInfo Info { get; private set; }

        /// <summary>
        /// Initializes the object. Should be called before usage. Overriding
        /// classes have to call the base implementation.
        /// </summary>
        /// <returns>True on success, false otherwise.</returns>
        public virtual async Task<bool> InitializeAsync()
        {
            if (!fullyInitialized)
            {
                if (await GetDeviceInfoAsync() == null)
                {
                    return false;
                }

                string deviceName = await ReceiveStringAsync(0x00, Info.NameLength);
                if (deviceName == null)
                {
                    return false;
                }

                string versionInfo = await ReceiveStringAsync(0x02, Info.VersioninfoLength);
                if (versionInfo == null)
                {
                    return false;
                }

                Info = new DeviceInfo(Info, deviceName, versionInfo);
                fullyInitialized = true;
            }

            return true;
        }


        public async Task<bool> SendPingAsync()
        {
            Request request = new Request();
            return (await TransceiveAsync(request, 0)).Success;
        }
        public bool SendPing()
        {
            Request request = new Request();
            return Transceive(request, 0).Success;
        }

        /// <summary>
        /// Returns the device information. This function can be used for device identification
        /// if only protocol and device IDs are required to save a complete initialization.
        /// </summary>
        /// <returns>The device info on success, null otherwise.</returns>
        public async Task<DeviceInfo> GetDeviceInfoAsync()
        {
            if (Info == null)
            {
                Request request = new Request();
                request.Write((byte)0);  // device info command

                TransceiveResult result = await TransceiveAsync(request, 11);

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

        public async Task<SlaveStatistics> ReceiveSlaveStatisticsAsync()
        {
            DeviceInfo info = await GetDeviceInfoAsync();
            if (!info.StatisticsAvailable)
            {
                return null;
            }

            Request request = new Request();
            request.Write((byte)0x00);
            request.Write((byte)0x07);

            TransceiveResult result = await TransceiveAsync(request, 16);

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

        public async Task<double> ReceiveUptimeAsync()
        {
            DeviceInfo info = await GetDeviceInfoAsync();
            if (info.UptimeFrequency == 0.0)
            {
                return Double.NaN;
            }

            Request request = new Request();
            request.Write((byte)0x00);
            request.Write((byte)0x01);

            TransceiveResult result = await TransceiveAsync(request, 4);

            if (!result.Success)
            {
                return Double.NaN;
            }
            else
            {
                return (double)result.Response.ReadUInt32() / info.UptimeFrequency;
            }
        }

        private async Task<string> ReceiveStringAsync(byte command, int stringLength)
        {
            Request request = new Request();
            request.Write((byte)0);
            request.Write(command);

            TransceiveResult result = await TransceiveAsync(request, stringLength);

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


        protected TransceiveResult Transceive(Request request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(request, responseSize, sync: true).GetAwaiter().GetResult();
        }
        protected Task<TransceiveResult> TransceiveAsync(Request request, int responseSize = 0)
        {
            return TransceiveAsyncInternal(request, responseSize, sync: false);
        }
        private async Task<TransceiveResult> TransceiveAsyncInternal(Request request, int responseSize, bool sync)
        {
            int attempts = 3;
            Response response = new Response(responseSize);

            while (attempts > 0)
            {
                Tuple<BusTransceiveResult, byte[]> result;
                if (sync)
                {
                    result = BusAbstraction.Transceive(Address, request.GetByteArray(), responseSize);
                }
                else
                {
                    result = await BusAbstraction.TransceiveAsync(Address, request.GetByteArray(), responseSize);
                }

                BusTransceiveResult transceiveStatus = result.Item1;
                byte[] receiveBuffer = result.Item2;

                switch (transceiveStatus)
                {
                    case BusTransceiveResult.Success:
                        response.Fill(receiveBuffer);
                        ++successfulTransmissions;
                        return new TransceiveResult(true, response);

                    case BusTransceiveResult.ChecksumError:
                        ++checksumErrors;
                        break;

                    case BusTransceiveResult.ReceptionError:
                        if (receiveBuffer.Length == 0)
                        {
                            ++noAnswer;
                        }
                        else
                        {
                            ++missingData;
                        }
                        break;

                    case BusTransceiveResult.TransmissionError:
                        ++transmitErrors;
                        break;
                }

                attempts--;
            }

            //L.C(this).Debug("!!!!!!!!!!! Transceive 3 attempts failed!");
            return new TransceiveResult(false, response);
        }


        protected bool SendBroadcast(Broadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: true).GetAwaiter().GetResult();
        }
        protected Task<bool> SendBroadcastAsync(Broadcast broadcast)
        {
            return SendBroadcastAsyncInternal(broadcast, sync: false);
        }
        private async Task<bool> SendBroadcastAsyncInternal(Broadcast broadcast, bool sync)
        {
            int attempts = 3;

            while (attempts > 0)
            {
                BusTransceiveResult result = await BusAbstraction.TransmitAsync(Address, broadcast.GetByteArray());

                switch (result)
                {
                    case BusTransceiveResult.Success:
                        ++successfulTransmissions;
                        return true;

                    case BusTransceiveResult.TransmissionError:
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
