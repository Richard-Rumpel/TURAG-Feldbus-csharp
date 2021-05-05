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
        /// </summary>
        /// <returns>Error code describing the result of the call.</returns>
        public ErrorCode SendBroadcastPing(out uint uuid)
        {
            var result = SendBroadcastPingAsyncInternal(sync: true).GetAwaiter().GetResult();
            uuid = result.Item2
        }

        /// <summary>
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call.</returns>
        public Task<(ErrorCode, uint)> SendBroadcastPingAsync()
        {
            return SendBroadcastPingAsyncInternal(sync: false);
        }


        private async Task<(ErrorCode, uint uuid)> SendBroadcastPingAsyncInternal(bool sync)
        {
            BusBroadcast request = new BusBroadcast();
            request.Write((byte)0x00);
            request.Write((byte)0x00);

            return (await TransceiveBroadcastAsync(request, 4)).TransportError;

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
        /// </summary>
        /// <param name="uuid"></param>
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
        /// </summary>
        /// <param name="uuid"></param> 
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


        /*
         bool Device::receiveBusAddress(uint32_t uuid, unsigned* busAddress) {
    struct Value {
        uint8_t key;
        uint32_t uuid;
        uint8_t key2;
    } _packed;

    Broadcast<Value> request;

    request.id = 0x00;
    request.data.key = 0x00;
    request.data.uuid = uuid;
    request.data.key2 = 0x00;

    Response<uint8_t> response;

    if (!transceive(request, &response)) {
        return false;
    }

    *busAddress = response.data;
    return true;
}

bool Device::setBusAddress(uint32_t uuid, unsigned busAddress) {
    struct Value {
        uint8_t key;
        uint32_t uuid;
        uint8_t key2;
        uint8_t busAddress;
    } _packed;

    struct Value2 {
        uint8_t key;
        uint32_t uuid;
        uint8_t key2;
        uint16_t busAddress;
    } _packed;

    if (myAddressLength == 1) {
        Broadcast<Value> request;

        request.id = 0x00;
        request.data.key = 0x00;
        request.data.uuid = uuid;
        request.data.key2 = 0x00;
        request.data.busAddress = busAddress & 0xFF;

        Response<uint8_t> response;

        if (!transceive(request, &response)) {
            return false;
        }

        return response.data == 1;
    } else {
        Broadcast<Value2> request;

        request.id = 0x00;
        request.data.key = 0x00;
        request.data.uuid = uuid;
        request.data.key2 = 0x00;
        request.data.busAddress = busAddress & 0xFFFF;

        Response<uint8_t> response;

        if (!transceive(request, &response)) {
            return false;
        }
        return response.data == 1;
    }
}

bool Device::reseBusAddress(uint32_t uuid) {
    struct Value {
        uint8_t key;
        uint32_t uuid;
        uint8_t key2;
    } _packed;

    Broadcast<Value> request;

    request.id = 0x00;
    request.data.key = 0x00;
    request.data.uuid = uuid;
    request.data.key2 = 0x01;

    Response<> response;

    return transceive(request, &response);
}

bool Device::enableBusNeighbors(void) {
    Broadcast<uint8_t> request;
    request.id = 0x00;
    request.data = 0x01;

    return transceive(request);
}
bool Device::disableBusNeighbors(void) {
    Broadcast<uint8_t> request;
    request.id = 0x00;
    request.data = 0x02;

    return transceive(request);
}

bool Device::resetAllBusAddresses(void) {
    Broadcast<uint8_t> request;
    request.id = 0x00;
    request.data = 0x03;

    return transceive(request);
}
        */


        /// <summary>
        /// Attempts to scan the bus for devices and re-assigns the bus addresses from 1 to n.
        /// It returns a list of uuids in the order of the assigned bus addresses and a flag
        /// hinting whether the order of the queried ids could be verified.
        /// </summary>
        /// <returns></returns>
        static public ErrorCode EnumerateDevices(out IList<uint> uuids, bool busOrderKnown)
        {

        }

        static private BusBroadcast ActivateBus(bool enable)
        {
            var broadcast = new BusBroadcast();
            broadcast.Write
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
