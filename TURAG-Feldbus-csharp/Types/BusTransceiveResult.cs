namespace TURAG.Feldbus.Types
{
    public class BusTransceiveResult
    {
        public BusTransceiveResult(ErrorCode transportError, BusResponse response)
        {
            this.TransportError = transportError;
            this.Response = response;
        }

        public ErrorCode TransportError { get; }

        public bool Success
        {
            get
            {
                return TransportError == ErrorCode.Success;
            }
        }

        public BusResponse Response { get; }
    }
}
