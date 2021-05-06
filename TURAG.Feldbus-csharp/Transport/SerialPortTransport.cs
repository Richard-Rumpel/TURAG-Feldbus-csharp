using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// Serial port transport implementation.
    /// </summary>
    public class SerialPortTransport : TransportAbstraction
    {
        public SerialPortTransport(string portName, int baudRate, int timeoutMs = 50) 
        {
            TimeoutMs = timeoutMs;

            serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudRate,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,

                // Set the read/write timeouts, only applicable for sync case.
                ReadTimeout = TimeoutMs,
                WriteTimeout = TimeoutMs
            };

            serialPort.Open();
        }

        public int TimeoutMs { get; set; }


        protected override bool DoClearBuffer()
        {
            serialPort.DiscardInBuffer();
            return true;
        }

        protected override Task<bool> DoClearBufferAsync()
        {
            serialPort.DiscardInBuffer();
            return Task.FromResult(true);
        }

#if __DOXYGEN__
        protected override Tuple<bool, byte[]> DoTransceive(byte[] data, int bytesRequested)
#else
        protected override (bool, byte[]) DoTransceive(byte[] data, int bytesRequested)
#endif
        {
            try
            {
                serialPort.BaseStream.Write(data, 0, data.Length);
            }
            catch (Exception)
            {
                return (false, new byte[0]);
            }

            byte[] received = new byte[bytesRequested];
            int bytesReadTotal = 0;
            try
            {
                while (bytesRequested > bytesReadTotal)
                {
                    int bytesRead = serialPort.BaseStream.Read(received, bytesReadTotal, bytesRequested - bytesReadTotal);
                    bytesReadTotal += bytesRead;
                }
            }
            catch (TimeoutException)
            {
            }

            return (bytesReadTotal == bytesRequested, received);
        }

#if __DOXYGEN__
        protected override async Task<Tuple<bool, byte[]>> DoTransceiveAsync(byte[] data, int bytesRequested)
#else
        protected override async Task<(bool, byte[])> DoTransceiveAsync(byte[] data, int bytesRequested)
#endif
        {
            try
            {
                await serialPort.BaseStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception)
            {
                return (false, new byte[0]);
            }

            byte[] received = new byte[bytesRequested];
            int bytesReadTotal = 0;
            while (bytesRequested > bytesReadTotal)
            {
                Task<int> readTask = serialPort.BaseStream.ReadAsync(received, bytesReadTotal, bytesRequested - bytesReadTotal);
                Task delayTask = Task.Delay(TimeoutMs);
                Task finishedTask = await Task.WhenAny(readTask, delayTask);
                if (finishedTask == readTask)
                {
                    bytesReadTotal += await readTask;
                }
                else
                {
                    break;
                }
            }

            return (bytesReadTotal == bytesRequested, received);
        }

        protected override bool DoTransmit(byte[] data)
        {
            try
            {
                serialPort.BaseStream.Write(data, 0, data.Length);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        protected async override Task<bool> DoTransmitAsync(byte[] data)
        {
            try
            {
                await serialPort.BaseStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private readonly SerialPort serialPort;
    }
}
