using System;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Transport
{
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


        public TransportAbstractionExt(TransmissionMode mode = TransmissionMode.Normal)
        {
            this.Mode = mode;
        }

        public TransmissionMode Mode { get; set; }


#if ! __DOXYGEN__
        private protected override async Task<(ErrorCode, byte[])> TransceiveAsyncInternal(int address, byte[] transmitData, int requestedBytes, bool sync)
        {
            switch (Mode)
            {
                case TransmissionMode.Normal:
                    return await base.TransceiveAsyncInternal(address, transmitData, requestedBytes, sync);

                case TransmissionMode.TransmitOnly:
                    {
                        byte[] transmitBuffer = AddAddressAndChecksum(transmitData, address);

                        bool success = sync ?
                            DoTransmit(transmitBuffer) :
                            await DoTransmitAsync(transmitBuffer);

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
                        TransmitCount += transmitData.Length + 2;

                        (bool receiptionSuccessful, byte[] receiveBuffer) = sync ?
                            DoReceive(requestedBytes + 2) :
                            await DoReceiveAsync(requestedBytes + 2);

                        if (!receiptionSuccessful)
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

        /// <summary>
        /// Blockingly receive specified amount of data or
        /// all currently available data.
        /// </summary>
        /// <param name="bytesRequested">Number of bytes to receive.</param>
        /// <returns>True on success, false otherwise.</returns>
#if __DOXYGEN__
        protected abstract Tuple<bool, byte[]> DoReceive(int bytesRequested);
#else
        protected abstract (bool, byte[]) DoReceive(int bytesRequested);
#endif

        /// <summary>
        /// Receive specified amount of data or
        /// all currently available data.
        /// </summary>
        /// <param name="bytesRequested">Number of bytes to receive.</param>
        /// <returns>A task representing the asynchronous operation. Contains 
        /// true on usccess, false otherwise.</returns>
#if __DOXYGEN__
        protected abstract Task<Tuple<bool, byte[]>> DoReceiveAsync(int bytesRequested);
#else
        protected abstract Task<(bool, byte[])> DoReceiveAsync(int bytesRequested);
#endif
    }
}
