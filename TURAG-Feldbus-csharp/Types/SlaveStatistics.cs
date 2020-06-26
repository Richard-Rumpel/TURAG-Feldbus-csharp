using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Types
{
    public class SlaveStatistics
    {
        public SlaveStatistics(uint correct, uint overflow, uint lost, uint chksum_error)
        {
            NoError = correct;
            BufferOverflow = overflow;
            LostPackets = lost;
            ChecksumError = chksum_error;
        }

        public uint NoError { get; }
        public uint BufferOverflow { get; }
        public uint LostPackets { get; }
        public uint ChecksumError { get; }
    }
}
