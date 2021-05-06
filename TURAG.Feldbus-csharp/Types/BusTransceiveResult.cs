namespace TURAG.Feldbus.Types
{
    public class BusTransceiveResult
    {
        public BusTransceiveResult(ErrorCode transportError, BusResponse response)
        {
            this.TransportError = transportError;
            this.Response = response;
        }

        public bool Success
        {
            get
            {
                return TransportError == ErrorCode.Success;
            }
        }

        public ErrorCode TransportError { get; }

        public BusResponse Response { get; }
    }
}
