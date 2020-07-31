using System;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;
using TURAG.Feldbus.Util;

namespace TURAG.Feldbus.Transport
{
    public abstract class TransportAbstraction
    {
        /// <summary>
        /// Calculates time required to transport data on the bus.
        /// </summary>
        /// <param name="byteCount"></param>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public static double DataDuration(int byteCount, int baudrate)
        {
            // 10 symbols per byte
            return (double)byteCount * 10 / baudrate;
        }

        /// <summary>
        /// Calculates time until a device received and processed a broadcast.
        /// </summary>
        /// <param name="dataSize"></param>
        /// <param name="processingTime"></param>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public static double BroadcastDuration(int dataSize, double processingTime, int baudrate)
        {
            // data + packet delimiter + processing time
            return DataDuration(dataSize, baudrate) + 15.0 / baudrate + processingTime;
        }

        /// <summary>
        /// Calculates time until a device received, processed and answered a packet.
        /// </summary>
        /// <param name="requestSize"></param>
        /// <param name="responseSize"></param>
        /// <param name="processingTime"></param>
        /// <param name="baudrate"></param>
        /// <returns></returns>
        public static double PacketDuration(int requestSize, int responseSize, double processingTime, int baudrate)
        {
            // request + packet delimiter + processing time + response
            return DataDuration(requestSize, baudrate) + 15.0 / baudrate + processingTime + DataDuration(responseSize, baudrate);
        }



        public TransportAbstraction()
        {
        }

        internal (ErrorCode, byte[]) Transceive(int address, byte[] transmitData, int requestedBytes)
        {
            using (busLock.Lock())
            {
                return TransceiveAsyncInternal(address, transmitData, requestedBytes, sync: true).GetAwaiter().GetResult();
            }
        }

        internal async Task<(ErrorCode, byte[])> TransceiveAsync(int address, byte[] transmitData, int requestedBytes)
        {
            using (await busLock.LockAsync())
            {
                return await TransceiveAsyncInternal(address, transmitData, requestedBytes, sync: false);
            }
        }

        internal ErrorCode Transmit(int address, byte[] transmitData)
        {
            using (busLock.Lock())
            {
                return TransmitAsyncInternal(address, transmitData, sync: true).GetAwaiter().GetResult();
            }
        }

        internal async Task<ErrorCode> TransmitAsync(int address, byte[] transmitData)
        {
            using (await busLock.LockAsync())
            {
                return await TransmitAsyncInternal(address, transmitData, sync: false);
            }
        }


#if !__DOXYGEN__
        private protected virtual async Task<(ErrorCode, byte[])> TransceiveAsyncInternal(int address, byte[] transmitData, int requestedBytes, bool sync)
        {
            // clear buffer of any old data
            if (sync)
            {
                DoClearBuffer();
            }
            else
            {
                await DoClearBufferAsync();
            }


            byte[] transmitBuffer = AddAddressAndChecksum(transmitData, address);
            (bool transceiveSuccess, byte[] receiveBuffer) = sync ?
                DoTransceive(transmitBuffer, requestedBytes + 2) :
                await DoTransceiveAsync(transmitBuffer, requestedBytes + 2);

            // assume transmission to be successful
            TransmitCount += transmitBuffer.Length;
            if (!transceiveSuccess)
            {
                return (ErrorCode.TransportReceptionError, new byte[0]);
            }
            ReceiveCount += receiveBuffer.Length;

            var (crcCorrect, receivedData) = CheckCrcAndExtractData(receiveBuffer);

            if (!crcCorrect)
            {
                return (ErrorCode.TransportChecksumError, new byte[0]);
            }

            return (ErrorCode.Success, receivedData);
        }

        private protected virtual async Task<ErrorCode> TransmitAsyncInternal(int address, byte[] transmitData, bool sync)
        {
            byte[] transmitBuffer = new byte[transmitData.Length + 2];
            Array.Copy(transmitData, 0, transmitBuffer, 1, transmitData.Length);

            transmitBuffer[0] = (byte)address;
            transmitBuffer[transmitBuffer.Length - 1] = CRC8.Calculate(transmitBuffer, 0, transmitBuffer.Length - 1);

            bool success;
            if (sync)
            {
                success = DoTransmit(transmitBuffer);
            }
            else
            {
                success = await DoTransmitAsync(transmitBuffer);
            }

            if (!success)
            {
                return ErrorCode.TransportTransmissionError;
            }

            TransmitCount += transmitBuffer.Length;

            return ErrorCode.Success;
        }
#endif

        /// <summary>
        /// Puts the address in the front and the correct checksum at the end of the supplied
        /// data array.
        /// </summary>
        /// <param name="data">Data array.</param>
        /// <param name="address">Target device address.</param>
        /// <returns>Complete data frame.</returns>
        protected byte[] AddAddressAndChecksum(byte[] data, int address)
        {
            byte[] transmitBuffer = new byte[data.Length + 2];
            Array.Copy(data, 0, transmitBuffer, 1, data.Length);

            transmitBuffer[0] = (byte)address;
            transmitBuffer[transmitBuffer.Length - 1] = CRC8.Calculate(transmitBuffer, 0, transmitBuffer.Length - 1);

            return transmitBuffer;
        }

        /// <summary>
        /// Checks crc of received frame and returns the data part.
        /// </summary>
        /// <param name="receiveBuffer">Receive buffer.</param>
        /// <returns>Tuple containing the crc status and the data array.</returns>
#if __DOXYGEN__
        protected Tuple<bool, byte[]> CheckCrcAndExtractData(byte[] receiveBuffer)
#else
        protected (bool, byte[]) CheckCrcAndExtractData(byte[] receiveBuffer)
#endif

        {
            if (!CRC8.Check(receiveBuffer, 0, receiveBuffer.Length - 1, receiveBuffer[receiveBuffer.Length - 1]))
            {
                return (false, new byte[0]);
            }

            byte[] receiveBytes = new byte[receiveBuffer.Length - 2];
            Array.Copy(receiveBuffer, 1, receiveBytes, 0, receiveBytes.Length);
            return (true, receiveBytes);
        }


        /// <summary>
        /// Transmits the given data on the transport channel.
        /// </summary>
        /// <param name="data">Data to transmit.</param>
        /// <returns>True if transmission was successful, false otherwise.</returns>
        protected abstract bool DoTransmit(byte[] data);

        /// <summary>
        /// Asynchronously transmits the given data on the transport channel.
        /// </summary>
        /// <param name="data">Data to transmit.</param>
        /// <returns>A task representing the asynchronous operation. Contains 
        /// true if transmission was successful, false otherwise.</returns>
        protected abstract Task<bool> DoTransmitAsync(byte[] data);

        /// <summary>
        /// Transmits to and afterwards receives data from the transport channel.
        /// </summary>
        /// <param name="data">Data to transmit.</param>
        /// <param name="bytesRequested">Number of bytes to receive.</param>
        /// <returns>True if transmission was successful and the requested number
        /// of bytes were received, false otherwise.</returns>
#if __DOXYGEN__
        protected abstract Tuple<bool, byte[]> DoTransceive(byte[] data, int bytesRequested);
#else
        protected abstract (bool, byte[]) DoTransceive(byte[] data, int bytesRequested);
#endif

        /// <summary>
        /// Asynchronously transmits to and afterwards receives data from the transport channel.
        /// </summary>
        /// <param name="data">Data to transmit.</param>
        /// <param name="bytesRequested">Number of bytes to receive.</param>
        /// <returns>A task representing the asynchronous operation. Contains 
        /// true if transmission was successful and the requested number
        /// of bytes were received, false otherwise.</returns>
#if __DOXYGEN__
        protected abstract Task<Tuple<bool, byte[]>> DoTransceiveAsync(byte[] data, int bytesRequested);
#else
        protected abstract Task<(bool, byte[])> DoTransceiveAsync(byte[] data, int bytesRequested);
#endif

        /// <summary>
        /// Clears the input buffer of the transport channel.
        /// </summary>
        /// <returns>True if the buffer was successfully cleared, false otherwise.</returns>
        protected abstract bool DoClearBuffer();

        /// <summary>
        /// Asynchronously clears the input buffer of the transport channel.
        /// </summary>
        /// <returns>A task representing the asynchronous operation. Contains 
        /// true if the buffer was successfully cleared, false otherwise.</returns>
        protected abstract Task<bool> DoClearBufferAsync();



        /*
        private string ByteArrayToString(byte[] bytes)
        {
            var sb = new StringBuilder("byte[] { ");
            foreach (var b in bytes)
            {
                sb.Append(b + ", ");
            }
            sb.Append("}");
            return sb.ToString();
        }*/



        public int TransmitCount { get; protected set; } = 0;
        public int ReceiveCount { get; protected set; } = 0;

        public void ResetCounters()
        {
            TransmitCount = 0;
            ReceiveCount = 0;
        }

        private readonly AsyncLock busLock = new AsyncLock();
    }
}
