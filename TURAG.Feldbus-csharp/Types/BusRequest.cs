using System;
using System.IO;

namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Models the data part of a packet sent from host to device, excluding
    /// address and checksum, which are added automatically.
    /// Data to be sent can be added using the various Write- functions of the 
    /// BinaryWriter base class.
    /// </summary>
    public class BusRequest : BinaryWriter
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public BusRequest() : base(new MemoryStream())
        {
        }

        internal byte[] GetByteArray()
        {
            MemoryStream stream = (MemoryStream)BaseStream;
            byte[] data = new byte[stream.Length];
            Array.Copy(stream.GetBuffer(), data, stream.Length);
            return data;
        }
    }
}
