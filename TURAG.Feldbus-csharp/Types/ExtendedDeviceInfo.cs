using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Types
{
    /// <summary>
    /// Class containing extended information about the device.
    /// </summary>
    public class ExtendedDeviceInfo
    {
        internal ExtendedDeviceInfo(string deviceName, string versionInfo, int bufferSize)
        {
            DeviceName = deviceName;
            VersionInfo = versionInfo;
            BufferSize = bufferSize;
        }

        /// <summary>
        /// Name of the device.
        /// </summary>
        public string DeviceName { get; }

        /// <summary>
        /// Version information of the device firmware.
        /// </summary>
        public string VersionInfo { get; }

        /// <summary>
        /// Buffer size of the device, defining boundaries on the maximum packet size it can
        /// process.
        /// </summary>
        public int BufferSize { get; }
    }
}
