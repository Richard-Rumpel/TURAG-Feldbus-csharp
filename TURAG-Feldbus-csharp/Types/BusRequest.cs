using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
