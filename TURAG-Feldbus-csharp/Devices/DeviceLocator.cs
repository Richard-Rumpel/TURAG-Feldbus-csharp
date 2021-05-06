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
    public abstract class DeviceLocator : BaseDevice
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
        public Task<(ErrorCode, uint)> SendBroadcastPingAsync()
        {
            return SendBroadcastPingAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode, uint uuid)> SendBroadcastPingAsyncInternal(bool sync)
        {
            BusBroadcast request = new BusBroadcast();
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
            BusBroadcast request = new BusBroadcast();
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
            BusBroadcast request = new BusBroadcast();
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
        public Task<(ErrorCode, int)> ReceiveBusAddressAsync(uint uuid)
        {
            return ReceiveBusAddressAsyncInternal(uuid, sync: false);
        }

        private async Task<(ErrorCode, int busAddress)> ReceiveBusAddressAsyncInternal(uint uuid, bool sync)
        {
            BusBroadcast request = new BusBroadcast();
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
            BusBroadcast request = new BusBroadcast();
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
            BusBroadcast request = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
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
            BusBroadcast broadcast = new BusBroadcast();
            broadcast.Write((byte)0x00);
            broadcast.Write((byte)0x03);

            return SendBroadcastAsync(broadcast);
        }


        /// <summary>
        /// Attempts to scan the bus for devices and re-assigns the bus addresses from 1 to n.
        /// It returns a list of uuids in the order of the assigned bus addresses and a flag
        /// hinting whether the order of the queried ids could be verified.
        /// </summary>
        /// <returns></returns>
        static public ErrorCode EnumerateDevices(out IList<uint> uuids, bool busOrderKnown)
        {

        }




        /// <summary>
        /// Pings devices on the bus, starting with a given address, until no response is received. 
        /// The list of devices which gave an answer is returned as a result.
        /// </summary>
        /// <param name="startAdress">First address to query.</param>
        /// <param name="busAbstraction">bus to work on.</param>
        /// <returns>List of valid addresses.</returns>
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
                if (await dev.SendPingAsync() != ErrorCode.Success)
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





    }
}
