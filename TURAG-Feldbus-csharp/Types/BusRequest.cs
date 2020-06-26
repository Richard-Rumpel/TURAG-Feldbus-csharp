using System;
using System.IO;

namespace TURAG.Feldbus.Types
{
    public class BusRequest : BinaryWriter
    {
        public BusRequest() : base(new MemoryStream())
        {
        }

        public byte[] GetByteArray()
        {
            MemoryStream stream = (MemoryStream)BaseStream;
            byte[] data = new byte[stream.Length];
            Array.Copy(stream.GetBuffer(), data, stream.Length);
            return data;
        }
    }
}
