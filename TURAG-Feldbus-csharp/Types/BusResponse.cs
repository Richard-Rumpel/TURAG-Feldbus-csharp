using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Types
{
    public class BusResponse : BinaryReader
    {
        public BusResponse(int size = 0) : base(new MemoryStream(size))
        {

        }

        public long Capacity { get => ((MemoryStream)BaseStream).Capacity; }

        public void Fill(byte[] data)
        {
            ((MemoryStream)BaseStream).Write(data, 0, data.Length);
            ((MemoryStream)BaseStream).Seek(0, SeekOrigin.Begin);
        }
    }
}
