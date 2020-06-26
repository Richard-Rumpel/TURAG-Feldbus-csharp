using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Transport
{
    public class MemoryTransport : TransportAbstractionExt
    {
        public MemoryTransport()
        {
            transmitBuffer = new Queue<byte[]>();
            receiveBuffer = new Queue<byte>();
        }


        protected override bool DoClearBuffer()
        {
            transmitBuffer.Clear();
            receiveBuffer.Clear();
            return true;
        }

        protected override Task<bool> DoClearBufferAsync()
        {
            return Task.FromResult(DoClearBuffer());
        }

        // Parses the injected receive data in the context of a Device implementation.
        protected override Tuple<bool, byte[]> DoReceive(int bytesRequested)
        {
            int dataToReturn;
            bool result;
            if (bytesRequested >= 0 && bytesRequested <= receiveBuffer.Count)
            {
                dataToReturn = bytesRequested;
                result = true;
            }
            else
            {
                dataToReturn = receiveBuffer.Count;
                if (bytesRequested < 0)
                {
                    result = true;
                }
                else
                {
                    result = false;
                }
            }

            byte[] data = new byte[dataToReturn];
            for (int i = 0; i < dataToReturn; ++i)
            {
                data[i] = receiveBuffer.Dequeue();
            }
            return Tuple.Create(result, data);
        }

        // Parses the injected receive data in the context of a Device implementation.
        protected override Task<Tuple<bool, byte[]>> DoReceiveAsync(int bytesRequested)
        {
            return Task.FromResult(DoReceive(bytesRequested));
        }

        // This function is used to inject the previously received data into the response buffer,
        // Called directly.
        public void AddToReceiveBuffer(byte[] data)
        {
            foreach (byte c in data)
            {
                receiveBuffer.Enqueue(c);
            }
        }


        // Transmits data into the transmit buffer.
        // Called indirectly from Device implementations.
        protected override bool DoTransmit(byte[] data)
        {
            transmitBuffer.Enqueue(data);
            return true;
        }
        // Transmits data into the transmit buffer.
        // Called indirectly from Device implementations.
        protected override Task<bool> DoTransmitAsync(byte[] data)
        {
            return Task.FromResult(DoTransmit(data));
        }
        // Used to retrieve data from the transmit buffer. Called directly.
        public IList<byte[]> TakeTransmitBuffer()
        {
            IList<byte[]> result = transmitBuffer.ToList();
            transmitBuffer = new Queue<byte[]>();
            return result;
        }


        protected override Tuple<bool, byte[]> DoTransceive(byte[] data, int bytesRequested)
        {
            // this function doesn't really make sense for this class, because how could we put the
            // response data into the response buffer?
            //Transmit(data);
            //return Receive(bytesRequested, timeoutMs);

            throw new NotImplementedException("Transceive is not supported for the MemoryTransceiver class.");
        }
        protected override Task<Tuple<bool, byte[]>> DoTransceiveAsync(byte[] data, int bytesRequested)
        {
            // this function doesn't really make sense for this class, because how could we put the
            // response data into the response buffer?
            //Transmit(data);
            //return ReceiveAsync(bytesRequested, timeoutMs);

            throw new NotImplementedException("Transceive is not supported for the MemoryTransceiver class.");
        }


        private Queue<byte[]> transmitBuffer;
        private readonly Queue<byte> receiveBuffer;
    }
}
