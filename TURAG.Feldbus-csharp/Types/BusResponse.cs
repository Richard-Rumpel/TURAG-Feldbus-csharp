using System.IO;

namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Models the data part of a packet sent from device to host, excluding
    /// address and checksum, which are removed from the raw data beforehand.
    /// Received data can be accessed using the various Read- functions of the 
    /// BinaryReader base class.
    /// </summary>
    public class BusResponse : BinaryReader
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="size">Initial capacitance for the underlying MemoryStream object.</param>
        public BusResponse(int size = 0) : base(new MemoryStream(size))
        {

        }

        internal long Capacity { get => ((MemoryStream)BaseStream).Capacity; }

        internal void Fill(byte[] data)
        {
            ((MemoryStream)BaseStream).Write(data, 0, data.Length);
            ((MemoryStream)BaseStream).Seek(0, SeekOrigin.Begin);
        }
    }
}
