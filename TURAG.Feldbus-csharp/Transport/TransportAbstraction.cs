using System;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;
using TURAG.Feldbus.Util;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// Abstract base class for transport implementations.
    /// </summary>
    public abstract class TransportAbstraction
    {
        /// <summary>
        /// Calculates the time generally required to transport data on the bus in one direction. 
        /// </summary>
        /// <param name="byteCount">Number of bytes to transmit.</param>
        /// <param name="baudrate">Baudrate used in the bus.</param>
        /// <returns>Required time to transmit the data in seconds.</returns>
        public static double DataDuration(int byteCount, int baudrate)
        {
            // 10 symbols per byte
            return (double)byteCount * 10 / baudrate;
        }

        /// <summary>
        /// Caculates the time needed for a device to detect the end of a packet.
        /// </summary>
        /// <param name="baudrate">Baudrate used in the bus.</param>
        /// <returns>Required time to detect the end of a packet in seconds.</returns>
        public static double PacketDetectionTimeout(int baudrate)
        {
            return 15.0 / baudrate;
        }

        /// <summary>
        /// Calculates the time required until a device received and processed a broadcast.
        /// </summary>
        /// <param name="dataSize">Number of bytes to transmit.</param>
        /// <param name="processingTime">Expected processing time of the device for this broadcast.</param>
        /// <param name="baudrate">Baudrate used in the bus.</param>
        /// <returns>Sum of time required to send the data and process the broadcast in the device.</returns>
        public static double BroadcastDuration(int dataSize, double processingTime, int baudrate)
        {
            // data + packet delimiter + processing time
            return DataDuration(dataSize, baudrate) + PacketDetectionTimeout(baudrate) + processingTime;
        }

        /// <summary>
        /// Calculates the time required until a device received, processed and answered a packet.
        /// </summary>
        /// <param name="requestSize">Number of bytes to transmit.</param>
        /// <param name="responseSize">Number of bytes to receive.</param>
        /// <param name="processingTime">Expected processing time of the device for this packet.</param>
        /// <param name="baudrate">Baudrate used in the bus.</param>
        /// <returns>Sum of time required to send the data, process it in the device and return the response.</returns>
        public static double PacketDuration(int requestSize, int responseSize, double processingTime, int baudrate)
        {
            // request + packet delimiter + processing time + response
            return DataDuration(requestSize, baudrate) + PacketDetectionTimeout(baudrate) + processingTime + DataDuration(responseSize, baudrate);
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
        /// <summary>
        /// Transceives data.
        /// </summary>
        /// <param name="address">Target address.</param>
        /// <param name="transmitData">Transmit data exckuding address and achecksum.</param>
        /// <param name="requestedBytes">Expected return size, excluding checksum and address.</param>
        /// <param name="sync"></param>
        /// <returns>Received data, excluding address and checksum.</returns>
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
            byte[] transmitBuffer = AddAddressAndChecksum(transmitData, address);

            bool success = sync ? DoTransmit(transmitBuffer) : await DoTransmitAsync(transmitBuffer);

            if (!success)
            {
                return ErrorCode.TransportTransmissionError;
            }

            TransmitCount += transmitBuffer.Length;

            return ErrorCode.Success;
        }

        /// <summary>
        /// Puts the address in the front and the correct checksum at the end of the supplied
        /// data array.
        /// </summary>
        /// <param name="data">Data array.</param>
        /// <param name="address">Target device address.</param>
        /// <returns>Complete data frame.</returns>
        private protected byte[] AddAddressAndChecksum(byte[] data, int address)
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
        private protected (bool, byte[]) CheckCrcAndExtractData(byte[] receiveBuffer)
        {
            if (!CRC8.Check(receiveBuffer, 0, receiveBuffer.Length - 1, receiveBuffer[receiveBuffer.Length - 1]))
            {
                return (false, new byte[0]);
            }

            byte[] receiveBytes = new byte[receiveBuffer.Length - 2];
            Array.Copy(receiveBuffer, 1, receiveBytes, 0, receiveBytes.Length);
            return (true, receiveBytes);
        }
#endif


        /// <summary>
        /// Transmits the given data on the transport channel.
        /// </summary>
        /// <param name="data">Raw data frame to transmit (including address and checksum).</param>
        /// <returns>True if transmission was successful, false otherwise.</returns>
        protected abstract bool DoTransmit(byte[] data);

        /// <summary>
        /// Asynchronously transmits the given data on the transport channel.
        /// </summary>
        /// <param name="data">Raw data frame to transmit (including address and checksum).</param>
        /// <returns>A task representing the asynchronous operation. Contains 
        /// true if transmission was successful, false otherwise.</returns>
        protected abstract Task<bool> DoTransmitAsync(byte[] data);

        /// <summary>
        /// Transmits to and afterwards receives data from the transport channel.
        /// </summary>
        /// <param name="data">Raw data frame to transmit (including address and checksum).</param>
        /// <param name="bytesRequested">Number of raw bytes to receive (including address and checksum).</param>
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
        /// <param name="data">Raw data frame to transmit (including address and checksum).</param>
        /// <param name="bytesRequested">Number of raw bytes to receive (including address and checksum).</param>
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


        /// <summary>
        /// Total number of bytes transmitted using this transport.
        /// </summary>
        public int TransmitCount { get; protected set; } = 0;

        /// <summary>
        /// Total number of bytes received using this transport.
        /// </summary>
        public int ReceiveCount { get; protected set; } = 0;

        /// <summary>
        /// Resets the TransmitCount and ReceiveCount properties to zero.
        /// </summary>
        public void ResetCounters()
        {
            TransmitCount = 0;
            ReceiveCount = 0;
        }

        private readonly AsyncLock busLock = new AsyncLock();
    }
}
