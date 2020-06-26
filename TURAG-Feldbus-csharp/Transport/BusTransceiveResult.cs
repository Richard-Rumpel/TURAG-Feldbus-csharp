namespace TURAG.Feldbus.Transport
{
    internal enum BusTransceiveResult
    {
        Success,
        ChecksumError,
        ReceptionError,
        TransmissionError
    }
}
