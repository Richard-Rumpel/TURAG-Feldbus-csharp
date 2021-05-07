using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// %Transport which stores transmitted data into and consumes received data from a memory buffer.
    /// This mechanism is useful when transmission and reception cannot be executed in a single step.
    /// Can only be used with transport modes Transmit or Receive.
    /// </summary>
    public class MemoryTransport : TransportAbstractionExt
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="mode">Transmission mode to use.</param>
        public MemoryTransport(TransmissionMode mode = TransmissionMode.Normal) : base(mode)
        {
            transmitBuffer = new Queue<(byte[], int)>();
            receiveBuffer = new Queue<byte>();
        }


        /// <summary>
        /// Injects the previously received data into the response buffer.
        /// This function needs to be called prior to using the transport in 
        /// Receive mode.
        /// </summary>
        /// <param name="data">Data received on the bus.</param>
        public void AddToReceiveBuffer(byte[] data)
        {
            foreach (byte c in data)
            {
                receiveBuffer.Enqueue(c);
            }
        }


        /// <summary>
        /// Returns the transmit buffer, containing the packets needed to send on the bus.
        /// This function needs to be used after using this transport in Transmit mode to send the
        /// data on the actual physical transport.
        /// </summary>
        /// <returns>List containing pairs of transmit buffers and expected response sizes.</returns>
        public IList<(byte[], int)> TakeTransmitBuffer()
        {
            IList<(byte[], int)> result = transmitBuffer.ToList();
            transmitBuffer = new Queue<(byte[], int)>();
            return result;
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


        // for broadcasts, when no response is expected.
        protected override bool DoTransmit(byte[] data)
        {
            if (Mode == TransmissionMode.TransmitOnly)
            {
                transmitBuffer.Enqueue((data, 0));
            }
            return true;
        }

        // Transmits data into the transmit buffer.
        // Called indirectly from Device implementations.
        protected override Task<bool> DoTransmitAsync(byte[] data)
        {
            return Task.FromResult(DoTransmit(data));
        }


#if __DOXYGEN__
        protected override Tuple<bool, byte[]> DoTransceive(byte[] data, int bytesRequested)
#else
        protected override (bool, byte[]) DoTransceive(byte[] transmitData, int bytesRequested)
#endif
        {
            switch (Mode)
            {
                case TransmissionMode.Normal:
                    // doesn't make sense, as we cannot fill the receive buffer
                    throw new NotImplementedException();

                case TransmissionMode.TransmitOnly:
                    transmitBuffer.Enqueue((transmitData, bytesRequested));
                    return (true, new byte[0]);

                case TransmissionMode.ReceiveOnly:
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
                    return (result, data);
            }

            return (false, null);
        }

#if __DOXYGEN__
        protected override Task<Tuple<bool, byte[]>> DoTransceiveAsync(byte[] data, int bytesRequested)
#else
        protected override Task<(bool, byte[])> DoTransceiveAsync(byte[] data, int bytesRequested)
#endif
        {
            return Task.FromResult(DoTransceive(data, bytesRequested));
        }


        private Queue<(byte[], int)> transmitBuffer;
        private readonly Queue<byte> receiveBuffer;
    }
}
