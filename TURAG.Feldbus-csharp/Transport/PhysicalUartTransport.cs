using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// Abstract base class which adds features for physical UART-based transport mechanisms.
    /// By supplying the physical baudrate and optionally the packet processing time for the
    /// bus devices in the constructor, this transport is able to make sure that necessary
    /// delays between packets are properly kept.
    /// </summary>
    public abstract class PhysicalUartTransport : TransportAbstraction
    {
        /// <summary>
        /// Calculates the time required to transport data on the bus in one direction. 
        /// </summary>
        /// <param name="byteCount">Number of bytes to transmit.</param>
        /// <param name="baudrate">Baudrate used on the bus.</param>
        /// <returns>Required time to transmit the data in seconds.</returns>
        public static double DataDuration(int byteCount, int baudrate)
        {
            // 10 symbols per byte
            return (double)byteCount * 10 / baudrate;
        }

        /// <summary>
        /// Calculates the time required to transport data on the bus in one direction. 
        /// </summary>
        /// <param name="byteCount">Number of bytes to transmit.</param>
        /// <returns>Required time to transmit the data in seconds.</returns>
        public double DataDuration(int byteCount)
        {
            // 10 symbols per byte
            return (double)byteCount * 10 / Baudrate;
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
        /// Caculates the time needed for a device to detect the end of a packet.
        /// </summary>
        /// <returns>Required time to detect the end of a packet in seconds.</returns>
        public double PacketDetectionTimeout()
        {
            return 15.0 / Baudrate;
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
        /// Calculates the time required until a device received and processed a broadcast.
        /// </summary>
        /// <param name="dataSize">Number of bytes to transmit.</param>
        /// <returns>Sum of time required to send the data and process the broadcast in the device.</returns>
        public double BroadcastDuration(int dataSize)
        {
            // data + packet delimiter + processing time
            return DataDuration(dataSize) + PacketDetectionTimeout() + DeviceProcessingTime;
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

        /// <summary>
        /// Calculates the time required until a device received, processed and answered a packet.
        /// </summary>
        /// <param name="requestSize">Number of bytes to transmit.</param>
        /// <param name="responseSize">Number of bytes to receive.</param>
        /// <returns>Sum of time required to send the data, process it in the device and return the response.</returns>
        public double PacketDuration(int requestSize, int responseSize)
        {
            // request + packet delimiter + processing time + response
            return DataDuration(requestSize) + PacketDetectionTimeout() + DeviceProcessingTime + DataDuration(responseSize);
        }

        readonly Stopwatch timer = new Stopwatch();
        int lastTargetAddress = 0;
        long lastTransmissionTime = 0;
        double requiredDelayOfLastTransmission = 0.0;


        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="baudrate">Physical baudrate of the transport.</param>
        /// <param name="deviceProcessingTime">Packet processing time of the slowest device on the bus.</param>
        public PhysicalUartTransport(int baudrate, double deviceProcessingTime = 1e-3)
        {
            Baudrate = baudrate;
            DeviceProcessingTime = deviceProcessingTime;

            timer.Start();
        }

        /// <summary>
        /// Represents the physical baudrate of the bus and is used to calculate 
        /// and keep necessary inter-packet delays. Must be > 0.
        /// </summary>
        public virtual int Baudrate { get; set; }

        /// <summary>
        /// Represents the time bus devices require to process received packets and is used 
        /// to calculate and keep necessary inter-packet delays.
        /// </summary>
        public double DeviceProcessingTime { get; set; }


        private async Task DelayTransmissionIfNecessaryAsyncInternal(bool sync)
        {
            long timeDiff = timer.ElapsedTicks - lastTransmissionTime;
            double timeDiffSec = (double)timeDiff / Stopwatch.Frequency;

            if (timeDiffSec < requiredDelayOfLastTransmission)
            {
                int delayMs = (int)((requiredDelayOfLastTransmission - timeDiffSec) * 1000.0) + 1;

                if (sync)
                {
                    Thread.Sleep(delayMs);
                }
                else
                {
                    await Task.Delay(delayMs);
                }
            }
        }

#if !__DOXYGEN__

        private protected override async Task<(ErrorCode, byte[])> TransceiveAsyncInternal(int address, byte[] transmitData, int requestedBytes, bool sync)
        {
            // make sure we do not send multiple packets too fast in a row.
            if (lastTargetAddress != address)
            {
                if (sync)
                {
                    DelayTransmissionIfNecessaryAsyncInternal(sync: true).GetAwaiter().GetResult();
                }
                else
                {
                    await DelayTransmissionIfNecessaryAsyncInternal(sync: false);
                }
            }

            lastTargetAddress = address;
            lastTransmissionTime = timer.ElapsedTicks;
            requiredDelayOfLastTransmission = DataDuration(transmitData.Length) + PacketDetectionTimeout();

            var result = sync ?
                base.TransceiveAsyncInternal(address, transmitData, requestedBytes, sync: true).GetAwaiter().GetResult() :
                await base.TransceiveAsyncInternal(address, transmitData, requestedBytes, sync: false);


            return result;
        }


        private protected override async Task<ErrorCode> TransmitAsyncInternal(int address, byte[] transmitData, bool sync)
        {
            // make sure we do not send multiple packets too fast in a row.
            if (sync)
            {
                DelayTransmissionIfNecessaryAsyncInternal(sync: true).GetAwaiter().GetResult();
            }
            else
            {
                await DelayTransmissionIfNecessaryAsyncInternal(sync: false);
            }


            lastTargetAddress = address;
            lastTransmissionTime = timer.ElapsedTicks;
            requiredDelayOfLastTransmission = BroadcastDuration(transmitData.Length);

            var result = sync ?
                base.TransmitAsyncInternal(address, transmitData, sync: true).GetAwaiter().GetResult() :
                await base.TransmitAsyncInternal(address, transmitData, sync: true);


            return result;
        }

#endif
    }
}
