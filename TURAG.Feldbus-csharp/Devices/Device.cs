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
    /// This class can be used for device discovery and enumeration. It needs to be initialized by calling
    /// Initialize() or InitializeAsync().
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
            ResetPacketCounters = 0x08,
            GetUuid = 0x09,
            GetExtendedDeviceInfo = 0x0A,
            GetStaticStorageCapacity = 0x0B,
            ReadFromStaticStorage = 0x0C,
            WriteToStaticStorage = 0x0D
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
        /// Returns whether the device was initialized by a successful call
        /// to Initialize() oder InitializeAsync().
        /// </summary>
        public virtual bool Initialized
        {
            get => Info != null;
        }

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
        /// A textual representation of Info and ExtendedInfo.
        /// </summary>
        public string DeviceInfoText { get; private set; }

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
        /// is the shortest possible payload. This function may safely be used 
        /// before calling Initialize().
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SendPing()
        {
            BusRequest request = new BusRequest();
            return Transceive(request, 0).TransportError;
        }

        /// <summary>
        /// Checks device availability by sending a ping packet. A ping packet
        /// is the shortest possible payload. This function may safely be used 
        /// before calling InitializeAsync().
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

                    if (InternalDeviceInfo.LegacyTypePacket)
                    {
                        // legacy packets do not contain the uuid, so we have to request it 
                        // separately. For backwards compatibility we fail silently though.
                        // Unfortunately, have no real way of indicating that the UUID is unknown.
                        // 0000-0000 should look suspicious enough anyway.
                        request = new BusRequest();
                        request.Write((byte)0);  // get uuid command
                        request.Write((byte)CommandKey.GetUuid); 
                        result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

                        uint uuid = result.Success ? result.Response.ReadUInt32() : 0;

                        Info = new DeviceInfo(
                            InternalDeviceInfo.DeviceProtocolId,
                            InternalDeviceInfo.DeviceTypeId,
                            InternalDeviceInfo.CrcType,
                            InternalDeviceInfo.StatisticsAvailable,
                            uuid,
                            InternalDeviceInfo.UptimeFrequency);
                    }
                    else
                    {
                        Info = new DeviceInfo(
                            InternalDeviceInfo.DeviceProtocolId,
                            InternalDeviceInfo.DeviceTypeId,
                            InternalDeviceInfo.CrcType,
                            InternalDeviceInfo.StatisticsAvailable,
                            InternalDeviceInfo.Uuid,
                            InternalDeviceInfo.UptimeFrequency);
                    }

                    UpdateDeviceInfoString();
                    return ErrorCode.Success;
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

                if (InternalDeviceInfo.LegacyTypePacket)
                {
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

                    ExtendedInfo = new ExtendedDeviceInfo(deviceName, versionInfo, InternalDeviceInfo.BufferSize);
                    UpdateDeviceInfoString();
                }
                else
                {
                    BusRequest request = new BusRequest();
                    request.Write((byte)0x00);  // extended device info command
                    request.Write((byte)CommandKey.GetExtendedDeviceInfo);  

                    BusTransceiveResult result = sync ? 
                        Transceive(request, InternalDeviceInfo.ExtendedDeviceInfoLength) : 
                        await TransceiveAsync(request, InternalDeviceInfo.ExtendedDeviceInfoLength);
                    if (!result.Success)
                    {
                        return result.TransportError;
                    }

                    result.Response.ReadByte();
                    int nameLength = result.Response.ReadByte();
                    int versionLength = result.Response.ReadByte();
                    int bufferSize = result.Response.ReadUInt16();

                    var deviceName = result.Response.ReadBytes(nameLength);
                    var versionInfo = result.Response.ReadBytes(versionLength);

                    ExtendedInfo = new ExtendedDeviceInfo(
                        Encoding.UTF8.GetString(deviceName), 
                        Encoding.UTF8.GetString(versionInfo), 
                        bufferSize);
                    UpdateDeviceInfoString();
                }
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
        /// <returns>An error code describing the result of the call and an instance
        /// of the class DevicePacketStatistics containing the data received from the device..</returns>
#if __DOXYGEN__
        public ValueTuple<ErrorCode errorCode, DevicePacketStatistics devicePacketStatistics> RetrieveDeviceStatistics()
#else
        public (ErrorCode errorCode, DevicePacketStatistics devicePacketStatistics) RetrieveDeviceStatistics()
#endif
        {
            return RetrieveDeviceStatisticsAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieves the transmission statistics of the bus device.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and an instance
        /// of the class DevicePacketStatistics containing the data received from the device..</returns>
        public Task<(ErrorCode errorCode, DevicePacketStatistics devicePacketStatistics)> RetrieveDeviceStatisticsAsync()
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
        /// <returns>An error code describing the result of the call and
        /// the uptime of the device in seconds.</returns>
#if __DOXYGEN__
        public ValueTuple<ErrorCode errorCode, double uptime> RetrieveUptime()
#else
        public (ErrorCode errorCode, double uptime) RetrieveUptime()
#endif
        {
            return RetrieveUptimeAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Retrieves the time since power-up from the device.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and
        /// the uptime of the device in seconds.</returns>
        public Task<(ErrorCode errorCode, double uptime)> RetrieveUptimeAsync()
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
        /// Return the capacity of the static data storage and its page size. 
        /// For write operations valid values of the address offset are limited to multiples of the page size.
        /// If the supplied data is shorter than a multiple of the page size, the remaining data of the last 
        /// should be assumed to be deleted. 
        /// </summary>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode ReadStaticStorageCapacityAndPageSize(out int storageCapacity, out int pageSize)
        {
            ErrorCode error;
            (error, storageCapacity, pageSize) = ReadStaticStorageCapacityAndPageSizeAsyncInternal(sync: true).GetAwaiter().GetResult();
            return error;
        }

        /// <summary>
        /// Return the capacity of the static data storage and its page size. 
        /// For write operations valid values of the address offset are limited to multiples of the page size. 
        /// If the supplied data is shorter than a multiple of the page size, the remaining data of the last 
        /// should be assumed to be deleted. 
        /// </summary>
        /// <returns>An error code describing the result of the call, the static storage capacity and
        /// its page size.</returns>
        public (ErrorCode, int, int) ReadStaticStorageCapacityAndPageSize()
        {
            return ReadStaticStorageCapacityAndPageSizeAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Return the capacity of the static data storage and its page size. 
        /// For write operations valid values of the address offset are limited to multiples of the page size. 
        /// If the supplied data is shorter than a multiple of the page size, the remaining data of the last 
        /// should be assumed to be deleted. 
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call, the static storage capacity and
        /// its page size.</returns>
        public Task<(ErrorCode, int, int)> ReadStaticStorageCapacityAndPageSizeAsync()
        {
            return ReadStaticStorageCapacityAndPageSizeAsyncInternal(sync: false);
        }

        int _staticStorageCapacity = -1;
        int _staticStoragePageSize = -1;

        private async Task<(ErrorCode, int, int)> ReadStaticStorageCapacityAndPageSizeAsyncInternal(bool sync)
        {
            if (_staticStorageCapacity == -1)
            {
                BusRequest request = new BusRequest();
                request.Write((byte)0);
                request.Write((byte)CommandKey.GetStaticStorageCapacity);

                BusTransceiveResult result = sync ? Transceive(request, 6) : await TransceiveAsync(request, 6);

                if (!result.Success)
                {
                    return (result.TransportError, 0, 0);
                }

                _staticStorageCapacity = (int)result.Response.ReadUInt32();
                _staticStoragePageSize = (int)result.Response.ReadUInt16();
            }

            return (ErrorCode.Success, _staticStorageCapacity, _staticStoragePageSize);
        }

        /// <summary>
        /// Reads UTF8-encoded string from the static storage. The end of the string is detected
        /// by reading chunks of data with the specified size until a \0-character has been found.
        /// If no \0-character is found before the maximum number of bytes have been read, the string
        /// read so far is returned.
        /// </summary>
        /// <param name="readChunkSize">Size of chunks to read.</param>
        /// <param name="maxReadSize">Maximum data size to read.</param>
        /// <returns>A task representing the asynchronous operation containing the read string or null on error.</returns>
        public string ReadStringFromStaticStorage(int readChunkSize = 256, int maxReadSize = 65536)
        {
            return ReadStringFromStaticStorageAsyncInternal(readChunkSize, maxReadSize, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads UTF8-encoded string from the static storage. The end of the string is detected
        /// by reading chunks of data with the specified size until a \0-character has been found.
        /// If no \0-character is found before the maximum number of bytes have been read, the string
        /// read so far is returned.
        /// </summary>
        /// <param name="readChunkSize">Size of chunks to read.</param>
        /// <param name="maxReadSize">Maximum data size to read.</param>
        /// <returns>The read string or null on error.</returns>
        public Task<string> ReadStringFromStaticStorageAsync(int readChunkSize = 256, int maxReadSize = 65536)
        {
            return ReadStringFromStaticStorageAsyncInternal(readChunkSize, maxReadSize, sync: false);
        }

        private async Task<string> ReadStringFromStaticStorageAsyncInternal(int readChunkSize, int maxReadSize, bool sync)
        {
            var readCapacityResult = sync ? ReadStaticStorageCapacityAndPageSize() : await ReadStaticStorageCapacityAndPageSizeAsync();
            if (readCapacityResult.Item1 != ErrorCode.Success)
            {
                return null;
            }

            var retrieveExtendedDeviceInfoResult = sync ? RetrieveExtendedDeviceInfo() : await RetrieveExtendedDeviceInfoAsync();
            if (retrieveExtendedDeviceInfoResult != ErrorCode.Success)
            {
                return null;
            }

            maxReadSize = Math.Min(_staticStorageCapacity, maxReadSize);
            int maxReadChunkSize = Math.Min(readChunkSize, ExtendedInfo.BufferSize - 1);
            int offset = 0;
            bool foundNull = false;

            var readData = new List<byte>();

            while (offset < maxReadSize && !foundNull)
            {
                //dont read beyond storage capacity
                int readSize = Math.Min(maxReadChunkSize, _staticStorageCapacity - offset);

                (ErrorCode error, byte[] readDataChunk) = sync ? 
                    ReadDataFromStaticStorage((uint)offset, (uint)readSize) : 
                    await ReadDataFromStaticStorageAsync((uint)offset, (uint)readSize);

                if (error != ErrorCode.Success)
                {
                    return null;
                }

                for (int i = 0; i < readDataChunk.Length; ++i)
                {
                    if (readDataChunk[i] == 0)
                    {
                        foundNull = true;
                        break;
                    }
                    else
                    {
                        readData.Add(readDataChunk[i]);
                    }
                }

                offset += readSize;
            }

            return new UTF8Encoding().GetString(readData.ToArray());
        }

        /// <summary>
        /// Reads data from the static storage.
        /// </summary>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="size">Amount of data to return.</param>
        /// <param name="readData">Contains the read data.</param>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode ReadDataFromStaticStorage(uint offset, uint size, out byte[] readData)
        {
            ErrorCode error;
            (error, readData) = ReadDataFromStaticStorageAsyncInternal(offset, size, sync: true).GetAwaiter().GetResult();
            return error;
        }

        /// <summary>
        /// Reads data from the static storage.
        /// </summary>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="size">Amount of data to return.</param>
        /// <returns>A tuple containing an error code describing the result of the call
        /// and the read data.</returns>
#if __DOXYGEN__
        public ValueTuple<ErrorCode, byte[]> ReadDataFromStaticStorage(uint offset, uint size)
#else
        public (ErrorCode error, byte[] data) ReadDataFromStaticStorage(uint offset, uint size)
#endif
        {
            return ReadDataFromStaticStorageAsyncInternal(offset, size, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Reads data from the static storage.
        /// </summary>
        /// <param name="offset">Offset to start reading from.</param>
        /// <param name="size">Amount of data to return.</param>
        /// <returns>A task representing the asynchronous operation containing 
        /// an error code describing the result of the call
        /// and the read data.</returns>
        public Task<(ErrorCode error, byte[] data)> ReadDataFromStaticStorageAsync(uint offset, uint size)
        {
            return ReadDataFromStaticStorageAsyncInternal(offset, size, sync: false);
        }

        private async Task<(ErrorCode, byte[])> ReadDataFromStaticStorageAsyncInternal(uint offset, uint size, bool sync)
        {
            var retrieveExtendedDeviceInfoResult = sync ? RetrieveExtendedDeviceInfo() : await RetrieveExtendedDeviceInfoAsync();
            if (retrieveExtendedDeviceInfoResult != ErrorCode.Success)
            {
                return (retrieveExtendedDeviceInfoResult, Array.Empty<byte>());
            }

            var readCapacityResult = sync ? ReadStaticStorageCapacityAndPageSize() : await ReadStaticStorageCapacityAndPageSizeAsync();
            if (readCapacityResult.Item1 != ErrorCode.Success)
            {
                return (readCapacityResult.Item1, Array.Empty<byte>());
            }

            if (offset + size > _staticStorageCapacity)
            {
                return (ErrorCode.DeviceStaticStorageAddressSizeError, Array.Empty<byte>());
            }

            int maxReadSize = Math.Min(256, ExtendedInfo.BufferSize - 1);

            var readData = new List<byte>();

            while (size > 0)
            {
                int readSize = (int)Math.Min(size, maxReadSize);

                BusRequest request = new BusRequest();
                request.Write((byte)0);
                request.Write((byte)CommandKey.ReadFromStaticStorage);
                request.Write((uint)offset);
                request.Write((ushort)readSize);

                BusTransceiveResult result = sync ? Transceive(request, readSize + 1) : await TransceiveAsync(request, readSize + 1);

                if (!result.Success)
                {
                    return (result.TransportError, Array.Empty<byte>());
                }
                else
                {
                    var errorCode = result.Response.ReadByte();
                    if (errorCode == 1)
                    {
                        return (ErrorCode.DeviceStaticStorageAddressSizeError, Array.Empty<byte>());
                    }
                    else if (errorCode != 0)
                    {
                        return (ErrorCode.DeviceStaticStorageWriteError, Array.Empty<byte>());
                    }

                    var readDataPortion = result.Response.ReadBytes(readSize);

                    for (int i = 0; i < readDataPortion.Length; ++i)
                    {
                        readData.Add(readDataPortion[i]);
                    }

                    offset += (uint)readSize;
                    size -= (uint)readSize;
                }
            }

            return (ErrorCode.Success, readData.ToArray());
        }

        /// <summary>
        /// Writes a UTF8 encoded string the static storage, marking its end with a \0-character.
        /// The string is always written to the beginning of the static storage.
        /// If the supplied does not fit into the static storage it is truncated.
        /// </summary>
        /// <param name="data">The string to write.</param>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode WriteStringToStaticStorage(string data)
        {
            return WriteStringToStaticStorageAsyncInternal(data, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes a UTF8 encoded string the static storage, marking its end with a \0-character.
        /// The string is always written to the beginning of the static storage.
        /// If the supplied does not fit into the static storage it is truncated.
        /// </summary>
        /// <param name="data">The string to write.</param>
        /// <returns>A task representing the asynchronous operation containing 
        /// an error code describing the result of the call.</returns>
        public Task<ErrorCode> WriteStringToStaticStorageAsync(string data)
        {
            return WriteStringToStaticStorageAsyncInternal(data, sync: false);
        }

        private async Task<ErrorCode> WriteStringToStaticStorageAsyncInternal(string data, bool sync)
        {
            var readCapacityResult = sync ? ReadStaticStorageCapacityAndPageSize() : await ReadStaticStorageCapacityAndPageSizeAsync();
            if (readCapacityResult.Item1 != ErrorCode.Success)
            {
                return readCapacityResult.Item1;
            }

            int maxStringLength = _staticStorageCapacity - 1;

            if (data.Length > maxStringLength)
            {
                data = data.Substring(0, maxStringLength);
            }

            var stringBuffer = new UTF8Encoding().GetBytes(data + "\0");

            if (sync)
            {
                return WriteDataToStaticStorage(0, stringBuffer);
            }
            else
            {
                return await WriteDataToStaticStorageAsync(0, stringBuffer);
            }
        }

        /// <summary>
        /// Writes data to the static storage. Valid values of the address offset are limited to multiples of the page size.
        /// If the supplied data is shorter than a multiple of the page size, the remaining data of the last 
        /// should be assumed to be deleted. Call ReadStaticStorageCapacityAndPageSize() to determine the maximum data length
        /// and the page size.
        /// </summary>
        /// <param name="offset">Offset to write the data to.</param>
        /// <param name="data">Data to write.</param>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode WriteDataToStaticStorage(uint offset, byte[] data)
        {
            return WriteDataToStaticStorageAsyncInternal(offset, data, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Writes data to the static storage. Valid values of the address offset are limited to multiples of the page size.
        /// If the supplied data is shorter than a multiple of the page size, the remaining data of the last 
        /// should be assumed to be deleted. Call ReadStaticStorageCapacityAndPageSizeAsync() to determine the maximum data length
        /// and the page size.
        /// </summary>
        /// <param name="offset">Offset to write the data to.</param>
        /// <param name="data">Data to write.</param>
        /// <returns>A task representing the asynchronous operation containing 
        /// an error code describing the result of the call.</returns>
        public Task<ErrorCode> WriteDataToStaticStorageAsync(uint offset, byte[] data)
        {
            return WriteDataToStaticStorageAsyncInternal(offset, data, sync: false);
        }

        private async Task<ErrorCode> WriteDataToStaticStorageAsyncInternal(uint offset, byte[] data, bool sync)
        {
            var retrieveExtendedDeviceInfoResult = sync ? RetrieveExtendedDeviceInfo() : await RetrieveExtendedDeviceInfoAsync();
            if (retrieveExtendedDeviceInfoResult != ErrorCode.Success)
            {
                return retrieveExtendedDeviceInfoResult;
            }

            var readCapacityResult = sync ? ReadStaticStorageCapacityAndPageSize() : await ReadStaticStorageCapacityAndPageSizeAsync();
            if (readCapacityResult.Item1 != ErrorCode.Success)
            {
                return readCapacityResult.Item1;
            }

            // dont exceed capacity and offset must be at a page boundary
            if (offset + data.Length > _staticStorageCapacity || (_staticStoragePageSize > 1 && offset % _staticStoragePageSize != 0))
            {
                return ErrorCode.DeviceStaticStorageAddressSizeError;
            }

            int maxWriteSize = ExtendedInfo.BufferSize - 6;

            // writing data in multiple packets works only if we can write at least one whole page at a time.
            if (maxWriteSize < _staticStoragePageSize && data.Length > maxWriteSize)
            {
                return ErrorCode.DeviceStaticStorageAddressSizeError;
            }

            int writeDataOffset = 0;
            int dataToWrite = data.Length;


            while (dataToWrite > 0)
            {
                int writeSize = (int)Math.Min(dataToWrite, _staticStoragePageSize);
                byte[] writeBuffer = new byte[writeSize];
                Array.Copy(data, writeDataOffset, writeBuffer, 0, writeSize);

                BusRequest request = new BusRequest();
                request.Write((byte)0);
                request.Write((byte)CommandKey.WriteToStaticStorage);
                request.Write((uint)offset);
                request.Write(writeBuffer);

                BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

                if (!result.Success)
                {
                    return result.TransportError;
                }
                else
                {
                    var errorCode = result.Response.ReadByte();
                    if (errorCode == 1)
                    {
                        return ErrorCode.DeviceStaticStorageAddressSizeError;
                    }
                    else if (errorCode != 0)
                    {
                        return ErrorCode.DeviceStaticStorageWriteError;
                    }

                    offset += (uint)writeSize;
                    writeDataOffset += writeSize;
                    dataToWrite -= writeSize;
                }
            }

            return ErrorCode.Success;
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

        private void UpdateDeviceInfoString()
        {
            var sb = new StringBuilder();

            if (ExtendedInfo != null)
            {
                sb.AppendLine("Class type: " + GetType().FullName);
                sb.AppendLine("Address: " + Address);
                sb.AppendLine("DeviceName: " + ExtendedInfo.DeviceName);
                sb.AppendLine("VersionInfo: " + ExtendedInfo.VersionInfo);
                sb.AppendLine("DeviceProtocolId: " + Info.DeviceProtocolId);
                sb.AppendLine("DeviceTypeId: " + Info.DeviceTypeId);
                sb.AppendLine("CrcType: " + Info.CrcType);
                sb.AppendLine("StatisticsAvailable: " + Info.StatisticsAvailable);
                sb.AppendLine("Uuid: " + FormatUuid(Info.Uuid));
                sb.AppendLine("UptimeFrequency: " + Info.UptimeFrequency);
                sb.AppendLine("UptimeAvailable: " + Info.UptimeAvailable);
                sb.AppendLine("BufferSize: " + ExtendedInfo.BufferSize);
            }
            else if (Info != null)
            {
                sb.AppendLine("Class type: " + GetType().FullName);
                sb.AppendLine("Address: " + Address);
                sb.AppendLine("DeviceProtocolId: " + Info.DeviceProtocolId);
                sb.AppendLine("DeviceTypeId: " + Info.DeviceTypeId);
                sb.AppendLine("CrcType: " + Info.CrcType);
                sb.AppendLine("StatisticsAvailable: " + Info.StatisticsAvailable);
                sb.AppendLine("Uuid: " + FormatUuid(Info.Uuid));
                sb.AppendLine("UptimeFrequency: " + Info.UptimeFrequency);
                sb.AppendLine("UptimeAvailable: " + Info.UptimeAvailable);
                sb.AppendLine("call RetrieveExtendedDeviceInfo() or RetrieveExtendedDeviceInfoAsync() to get more information.");
            }
            else
            {
                sb.AppendLine("Class type: " + GetType().FullName);
                sb.AppendLine("Address: " + Address);
                sb.AppendLine("call RetrieveDeviceInfo() or RetrieveDeviceInfo() to get more information.");
            }

            DeviceInfoText = sb.ToString();
        }
    }
}
