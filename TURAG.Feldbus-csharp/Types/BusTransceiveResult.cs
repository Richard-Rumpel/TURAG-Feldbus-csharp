namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Return type fro tranceive operations, containing an error code
    /// and the BusResponse object.
    /// </summary>
    public class BusTransceiveResult
    {
        /// <summary>
        /// Creates a new instance, initializing the fields.
        /// </summary>
        /// <param name="transportError">The error code associated with the transceive operation.</param>
        /// <param name="response">The response of the associated transceive operation.</param>
        public BusTransceiveResult(ErrorCode transportError, BusResponse response)
        {
            this.TransportError = transportError;
            this.Response = response;
        }

        /// <summary>
        /// Returns true if TransportError equals ErrorCode.Success,
        /// false otherwise.
        /// </summary>
        public bool Success
        {
            get
            {
                return TransportError == ErrorCode.Success;
            }
        }

        /// <summary>
        /// The error code associated with the transceive operation.
        /// </summary>
        public ErrorCode TransportError { get; }

        /// <summary>
        /// The response of the associated transceive operation.
        /// </summary>
        public BusResponse Response { get; }
    }
}
