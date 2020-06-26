namespace TURAG.Feldbus.Types
{
    public class MasterStatistics
    {
        public MasterStatistics(uint checksumErrors, uint noAnswer, uint missingData, uint transmitErrors, uint noErrors)
        {
            ChecksumErrors = checksumErrors;
            NoAnswer = noAnswer;
            MissingData = missingData;
            TransmitErrors = transmitErrors;
            NoErrors = noErrors;
        }

        public uint ChecksumErrors { get; }
        public uint NoAnswer { get; }
        public uint MissingData { get; }
        public uint TransmitErrors { get; }
        public uint NoErrors { get; }
    }
}
