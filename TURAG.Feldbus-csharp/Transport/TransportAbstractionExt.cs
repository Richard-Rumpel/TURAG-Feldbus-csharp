using System;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// Extended transport base class which gives more control over the transmission and reception process.
    /// Sub classes are reqired to behave according to the currently set Mode when overriding DoTransmit() and 
    /// DoTransceive().
    /// </summary>
    public abstract class TransportAbstractionExt : TransportAbstraction
    {
        /// <summary>
        /// Defines how transmissions are handled.
        /// </summary>
        public enum TransmissionMode
        {
            /// <summary>
            /// Data is transmitted and expected to be received.
            /// </summary>
            Normal,

            /// <summary>
            /// Data is transmitted, reception is ignored but assumed successful.
            /// ReceiveCount is increased as if all requested data was received.
            /// </summary>
            TransmitOnly,

            /// <summary>
            /// No data is transmitted, but data is expected to be received
            /// nonetheless. TransmitCount is increased as if the data was actually sent.
            /// </summary>
            ReceiveOnly
        };


        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="mode">Transmission mode to use.</param>
        public TransportAbstractionExt(TransmissionMode mode = TransmissionMode.Normal)
        {
            this.Mode = mode;
        }

        /// <summary>
        /// Defines whether this transport only sends or receives data or behaves normally, meaning it does both.
        /// </summary>
        public TransmissionMode Mode { get; set; }


#if !__DOXYGEN__
        /// <summary>
        /// Transceives data.
        /// </summary>
        /// <param name="address">Target address.</param>
        /// <param name="transmitData">Transmit data exckuding address and achecksum.</param>
        /// <param name="requestedBytes">Expected return size, excluding checksum and address.</param>
        /// <param name="sync"></param>
        /// <returns>Received data, excluding address and checksum.</returns>
        private protected override async Task<(ErrorCode, byte[])> TransceiveAsyncInternal(int address, byte[] transmitData, int requestedBytes, bool sync)
        {
            switch (Mode)
            {
                case TransmissionMode.Normal:
                    return await base.TransceiveAsyncInternal(address, transmitData, requestedBytes, sync);

                case TransmissionMode.TransmitOnly:
                    {
                        byte[] transmitBuffer = AddAddressAndChecksum(transmitData, address);

                        (bool success, var _) = sync ?
                            DoTransceive(transmitBuffer, requestedBytes + 2) :
                            await DoTransceiveAsync(transmitBuffer, requestedBytes + 2);

                        if (!success)
                        {
                            return (ErrorCode.TransportTransmissionError, new byte[0]);
                        }

                        TransmitCount += transmitBuffer.Length;
                        ReceiveCount += requestedBytes + 2;

                        return (ErrorCode.Success, new byte[requestedBytes + 2]);
                    }

                case TransmissionMode.ReceiveOnly:
                    {
                        (bool transceiveSuccess, byte[] receiveBuffer) = sync ?
                            DoTransceive(null, requestedBytes + 2) :
                            await DoTransceiveAsync(null, requestedBytes + 2);

                        // assume transmission to be successful
                        TransmitCount += transmitData.Length + 2;
                        ReceiveCount += receiveBuffer.Length;

                        if (!transceiveSuccess)
                        {
                            if (receiveBuffer.Length == 0)
                            {
                                return (ErrorCode.TransportReceptionNoAnswerError, receiveBuffer);
                            }
                            else
                            {
                                return (ErrorCode.TransportReceptionMissingDataError, receiveBuffer);
                            }
                        }

                        var (crcCorrect, receivedData) = CheckCrcAndExtractData(receiveBuffer);

                        if (!crcCorrect)
                        {
                            return (ErrorCode.TransportChecksumError, receiveBuffer);
                        }

                        return (ErrorCode.Success, receivedData);
                    }
            }
            return (ErrorCode.TransportTransmissionError, new byte[0]);
        }

        private protected override Task<ErrorCode> TransmitAsyncInternal(int address, byte[] transmitData, bool sync)
        {
            if (Mode == TransmissionMode.ReceiveOnly)
            {
                return Task.FromResult(ErrorCode.Success);
            }
            else
            {
                return base.TransmitAsyncInternal(address, transmitData, sync);
            }
        }
#endif
    }
}
