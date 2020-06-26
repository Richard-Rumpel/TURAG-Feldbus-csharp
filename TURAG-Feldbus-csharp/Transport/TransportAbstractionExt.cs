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


        private protected override async Task<Tuple<TransportErrorCode, byte[]>> TransceiveAsyncInternal(int address, byte[] transmitData, int requestedBytes, bool sync)
        {
            switch (Mode)
            {
                case TransmissionMode.Normal:
                    return await base.TransceiveAsyncInternal(address, transmitData, requestedBytes, sync);

                case TransmissionMode.TransmitOnly:
                    {
                        byte[] transmitBuffer = AddAddressAndChecksum(transmitData, address);
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
                            return Tuple.Create(TransportErrorCode.TransmissionError, new byte[0]);
                        }

                        TransmitCount += transmitBuffer.Length;
                        ReceiveCount += requestedBytes + 2;

                        return Tuple.Create(TransportErrorCode.Success, new byte[requestedBytes + 2]);
                    }

                case TransmissionMode.ReceiveOnly:
                    {
                        Tuple<bool, byte[]> receiveResult;

                        TransmitCount += transmitData.Length + 2;

                        if (sync)
                        {
                            receiveResult = DoReceive(requestedBytes + 2);
                        }
                        else
                        {
                            receiveResult = await DoReceiveAsync(requestedBytes + 2);
                        }
                        bool receiptionSuccessful = receiveResult.Item1;
                        byte[] receiveBuffer = receiveResult.Item2;

                        if (!receiptionSuccessful)
                        {
                            return Tuple.Create(TransportErrorCode.ReceptionError, new byte[0]);
                        }

                        ReceiveCount += receiveBuffer.Length;

                        var (crcCorrect, receivedData) = CheckCrcAndExtractData(receiveBuffer);

                        if (!crcCorrect)
                        {
                            return Tuple.Create(TransportErrorCode.ChecksumError, new byte[0]);
                        }

                        return Tuple.Create(TransportErrorCode.Success, receivedData);
                    }
            }
            return Tuple.Create(TransportErrorCode.TransmissionError, new byte[0]);
        }

        private protected override Task<TransportErrorCode> TransmitAsyncInternal(int address, byte[] transmitData, bool sync)
        {
            if (Mode == TransmissionMode.ReceiveOnly)
            {
                return Task.FromResult(TransportErrorCode.Success);
            }
            else
            {
                return base.TransmitAsyncInternal(address, transmitData, sync);
            }
        }


        /// <summary>
        /// Blockingly receive specified amount of data or
        /// all currently available data.
        /// </summary>
        /// <param name="bytesRequested"></param>
        /// <param name="data">-1 for all currently available data</param>
        /// <param name="timeoutMs">-1 for unlimited timeout</param>
        /// <returns>True on success</returns>
        protected abstract Tuple<bool, byte[]> DoReceive(int bytesRequested);
        protected abstract Task<Tuple<bool, byte[]>> DoReceiveAsync(int bytesRequested);
    }
}
