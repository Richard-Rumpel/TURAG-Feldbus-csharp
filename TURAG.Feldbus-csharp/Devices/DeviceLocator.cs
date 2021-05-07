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
    /// Helper class implementing functions for device discovery and enumeration.
    /// </summary>
    public class DeviceLocator : BaseDevice
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="busAbstraction">Bus to work on.</param>
        public DeviceLocator(TransportAbstraction busAbstraction) : base(busAbstraction)
        {
        }


        /// <summary>
        /// Pings any device which was not assigned a valid bus address. This function should only 
        /// be called when it can be assumed that only one device is reachable on the bus, for instance
        /// after calling ResetAllBusAddresses(). The device responds by returning its UUID.
        /// </summary>
        /// <param name="uuid">Returns the UUID of the device which responded.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SendBroadcastPing(out uint uuid)
        {
            ErrorCode errorCode;
            (errorCode, uuid) = SendBroadcastPingAsyncInternal(sync: true).GetAwaiter().GetResult();
            return errorCode;
        }

        /// <summary>
        /// Pings any device which was not assigned a valid bus address. This function should only 
        /// be called when it can be assumed that only one device is reachable on the bus, for instance
        /// after calling ResetAllBusAddresses(). The device responds by returning its UUID.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and the UUID of the device which 
        /// responded to the request.</returns>
#if __DOXYGEN__
        public Task<Tuple<ErrorCode, uint>> SendBroadcastPingAsync()
#else
        public Task<(ErrorCode, uint)> SendBroadcastPingAsync()
#endif
        {
            return SendBroadcastPingAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode, uint uuid)> SendBroadcastPingAsyncInternal(bool sync)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);

            var result = sync ? TransceiveBroadcast(request, 4) : await TransceiveBroadcastAsync(request, 4);

            if (result.Success)
            {
                return (result.TransportError, result.Response.ReadUInt32());
            }
            else
            {
                return (result.TransportError, 0);
            }
        }


        /// <summary>
        /// Pings the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the device to ping.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SendUuidPing(uint uuid)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);
            request.Write(uuid);

            return TransceiveBroadcast(request, 0).TransportError;
        }

        /// <summary>
        /// Pings the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the device to ping.</param> 
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public async Task<ErrorCode> SendUuidPingAsync(uint uuid)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);
            request.Write(uuid);

            return (await TransceiveBroadcastAsync(request, 0)).TransportError;
        }


        /// <summary>
        /// Returns the bus address of the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <param name="busAddress">Returns the bus address of the device with the given UUID.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode ReceiveBusAddress(uint uuid, out int busAddress)
        {
            ErrorCode errorCode;
            (errorCode, busAddress) = ReceiveBusAddressAsyncInternal(uuid, sync: true).GetAwaiter().GetResult();
            return errorCode;
        }

        /// <summary>
        /// Returns the bus address of the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and the bus address of the device 
        /// with the given UUID.</returns>
#if __DOXYGEN__
        public Task<Tuple<ErrorCode, int>> ReceiveBusAddressAsync(uint uuid)
#else
        public Task<(ErrorCode, int)> ReceiveBusAddressAsync(uint uuid)
#endif
        {
            return ReceiveBusAddressAsyncInternal(uuid, sync: false);
        }

        private async Task<(ErrorCode, int busAddress)> ReceiveBusAddressAsyncInternal(uint uuid, bool sync)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);
            request.Write(uuid);
            request.Write((byte)0x00);

            var result = sync ? TransceiveBroadcast(request, 1) : await TransceiveBroadcastAsync(request, 1);

            if (result.Success)
            {
                return (result.TransportError, result.Response.ReadByte());
            }
            else
            {
                return (result.TransportError, 0);
            }
        }


        /// <summary>
        /// Sets the bus address of the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <param name="busAddress">Bus address to assign to the specified device.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SetBusAddress(uint uuid, int busAddress)
        {
            return SetBusAddressAsyncInternal(uuid, busAddress, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the bus address of the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <param name="busAddress">Bus address to assign to the specified device.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> SetBusAddressAsync(uint uuid, int busAddress)
        {
            return SetBusAddressAsyncInternal(uuid, busAddress, sync: false);
        }

        private async Task<ErrorCode> SetBusAddressAsyncInternal(uint uuid, int busAddress, bool sync)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);
            request.Write(uuid);
            request.Write((byte)0x00);
            request.Write((byte)busAddress);

            var result = sync ? TransceiveBroadcast(request, 1) : await TransceiveBroadcastAsync(request, 1);

            if (result.Success)
            {
                if (result.Response.ReadByte() == 1)
                {
                    return ErrorCode.Success;
                }
                else
                {
                    return ErrorCode.DeviceRejectedBusAddress;
                }
            }
            else
            {
                return result.TransportError;
            }
        }


        /// <summary>
        /// Resets the bus address of the specified device.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode ResetBusAddress(uint uuid)
        {
            return ResetBusAddressAsyncInternal(uuid, sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sets the bus address of the device with the given UUID.
        /// </summary>
        /// <param name="uuid">UUID of the addressed device.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> ResetBusAddressAsync(uint uuid)
        {
            return ResetBusAddressAsyncInternal(uuid, sync: false);
        }

        private async Task<ErrorCode> ResetBusAddressAsyncInternal(uint uuid, bool sync)
        {
            var request = new BusRequest();
            request.Write((byte)0x00);
            request.Write((byte)0x00);
            request.Write(uuid);
            request.Write((byte)0x01);

            var result = sync ? TransceiveBroadcast(request, 0) : await TransceiveBroadcastAsync(request, 0);

            return result.TransportError;
        }


        /// <summary>
        /// Enable bus neighbours of available devices.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode EnableBusNeighbours()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x01);

            return SendBroadcast(broadcast);
        }

        /// <summary>
        /// Enable bus neighbours of available devices.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> EnableBusNeighboursAsync()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x01);

            return SendBroadcastAsync(broadcast);
        }


        /// <summary>
        /// Disable bus neighbours of available devices.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode DisableBusNeighbours()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x02);

            return SendBroadcast(broadcast);
        }

        /// <summary>
        /// Disable bus neighbours of available devices.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> DisableBusNeighboursAsync()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x02);

            return SendBroadcastAsync(broadcast);
        }


        /// <summary>
        /// Resets the bus address of all available devices.
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode ResetAllBusAddresses()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x03);

            return SendBroadcast(broadcast);
        }

        /// <summary>
        /// Resets the bus address of all available devices.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<ErrorCode> ResetAllBusAddressesAsync()
        {
            var broadcast = new BusRequest();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x03);

            return SendBroadcastAsync(broadcast);
        }


        /// <summary>
        /// Pings devices on the bus within the given range of bus addresses.
        /// The list of devices which gave an answer is returned as a result.
        /// </summary>
        /// <param name="busAddresses">Returns the list of valid addresses.</param>
        /// <param name="firstAdress">First address to query.</param>
        /// <param name="lastAddress">Last address to query.</param>
        /// <param name="stopOnMissingDevice">Specifies whether to stop the search on the first missing device.</param>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode ScanBusAddresses(out IList<int> busAddresses, int firstAdress = 1, int lastAddress = 127, bool stopOnMissingDevice = false)
        {
            (var error, var result) = ScanBusAddressesAsyncInternal(firstAdress, lastAddress, stopOnMissingDevice, sync: true).GetAwaiter().GetResult();
            busAddresses = result;
            return error;
        }

        /// <summary>
        /// Pings devices on the bus within the given range of bus addresses.
        /// The list of devices which gave an answer is returned as a result.
        /// </summary>
        /// <param name="firstAdress">First address to query.</param>
        /// <param name="lastAddress">Last address to query.</param>
        /// <param name="stopOnMissingDevice">Specifies whether to stop the search on the first missing device.</param>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and the 
        /// list of valid bus addresses.</returns>
#if __DOXYGEN__
        public Task<Tuple<ErrorCode, IList<int>>> ScanBusAddressesAsync(int firstAdress = 1, int lastAddress = 127, bool stopOnMissingDevice = false)
#else
        public Task<(ErrorCode, IList<int>)> ScanBusAddressesAsync(int firstAdress = 1, int lastAddress = 127, bool stopOnMissingDevice = false)
#endif
        {
            return ScanBusAddressesAsyncInternal(firstAdress, lastAddress, stopOnMissingDevice, sync: false);
        }

        private async Task<(ErrorCode, IList<int>)> ScanBusAddressesAsyncInternal(int firstAdress, int lastAddress, bool stopOnMissingDevice, bool sync)
        {
            if (firstAdress < 1 || lastAddress > 127)
            {
                return (ErrorCode.InvalidArgument, new List<int>());
            }

            int address = firstAdress;
            List<int> addresses = new List<int>();

            while (address <= lastAddress)
            {
                Device dev = new Device(address, BusAbstraction);
                var error = sync ? dev.SendPing() : await dev.SendPingAsync();

                if (error == ErrorCode.Success)
                {
                    addresses.Add(address);
                }
                else if (stopOnMissingDevice)
                {
                    break;
                }

                ++address;
            }

            return (ErrorCode.Success, addresses);
        }


        /// <summary>
        /// Assigns new bus addresses to all available nodes starting with 1 and returns the list 
        /// of UUIDs. This function requires each node to be able to disconnect its neighbours from the bus, otherwise it will
        /// fail. The returned list of UUIDs resembles the physical order of the devices in the bus.
        /// </summary>
        /// <param name="uuids">Returns the list of detected UUIDs.</param>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode EnumerateDevicesSequentially(out IList<uint> uuids)
        {
            (var error, var devices, _) = EnumerateBusNodesAsyncInternal(useSequentialSearch: true, useBinarySearch: false, sync: true).GetAwaiter().GetResult();
            uuids = devices;
            return error;
        }

        /// <summary>
        /// Assigns new bus addresses to all available nodes starting with 1 and returns the list 
        /// of UUIDs. This function requires each node to be able to disconnect its neighbours from the bus, otherwise it will
        /// fail. The returned list of UUIDs resembles the physical order of the devices in the bus.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call and the 
        /// list of detected UUIDs.</returns>
#if __DOXYGEN__
        public async Task<Tuple<ErrorCode, IList<uint>>> EnumerateDevicesSequentiallyAsync()
#else
        public async Task<(ErrorCode, IList<uint>)> EnumerateDevicesSequentiallyAsync()
#endif
        {
            (var error, var devices, _) = await EnumerateBusNodesAsyncInternal(useSequentialSearch: true, useBinarySearch: false, sync: false);
            return (error, devices);
        }

        /// <summary>
        /// Assigns new bus addresses to all available nodes starting with 1 and returns the list of UUIDs and a flag indicating
        /// whether the returned list of UUIDs resembles the physical order of the devices in the bus. Depending on the parameter values
        /// it will utilize only sequential search, only binary search or sequential search first and binary search as a fallback.
        /// </summary>
        /// <param name="useSequentialSearch">Indicates whether to use sequential search utilizing neighbour disabling for device discovery.</param>
        /// <param name="useBinarySearch">Indicates whether to use binary UUID search for device discovery.</param>
        /// <param name="sync"></param>
        /// <returns></returns>
        private async Task<(ErrorCode, IList<uint>, bool)> EnumerateBusNodesAsyncInternal(bool useSequentialSearch, bool useBinarySearch, bool sync)
        {
            if (!useSequentialSearch && !useBinarySearch)
            {
                return (ErrorCode.InvalidArgument, new List<uint>(), false);
            }

            // TODO: if useSequentialSearch == false, then
            // directly return result of a binary only search function
            //if (useSequentialSearch == false)
            //{
            //    return // todo
            //}

            // Thus we can assume here that useSequentialSearch == true
            // and useBinarySearch is true or false

            ErrorCode error;
            var devices = new List<uint>();
            bool deviceOrderKnown = true;

            error = sync ? ResetAllBusAddresses() : await ResetAllBusAddressesAsync();
            if (error != ErrorCode.Success)
            {
                return (error, new List<uint>(), false);
            }

            error = sync ? DisableBusNeighbours() : await DisableBusNeighboursAsync();
            if (error != ErrorCode.Success)
            {
                return (error, new List<uint>(), false);
            }

            int nextBusAddress = 1;
            uint uuid;

            while (true)
            {
                if (sync)
                {
                    error = SendBroadcastPing(out uuid);
                }
                else
                {
                    (error, uuid) = await SendBroadcastPingAsync();
                }

                if (error != ErrorCode.Success)
                {
                    // if the broadcast ping fails it can mean that either there are no more devices
                    // or more than one device tried to respond. If enabled, we fall back to binary search.
                    // TODO
                    //if (useBinarySearch)
                    //{
                    // here we also need to set deviceOrderKnown to false, if we found devices using the binary search.
                    //}
                    //else
                    //{
                    // otherwise return the current result
                    return (ErrorCode.Success, devices, deviceOrderKnown);
                    //}
                }
                else
                {
                    error = sync ? SetBusAddress(uuid, nextBusAddress) : await SetBusAddressAsync(uuid, nextBusAddress);

                    if (error != ErrorCode.Success)
                    {
                        return (error, devices, deviceOrderKnown);
                    }

                    devices.Add(uuid);
                    ++nextBusAddress;
                }


                error = sync ? EnableBusNeighbours() : await EnableBusNeighboursAsync();

                if (error != ErrorCode.Success)
                {
                    return (error, devices, deviceOrderKnown);
                }
            }
        }
    }
}
