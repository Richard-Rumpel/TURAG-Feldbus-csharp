using System;
using System.IO.Ports;
using System.Threading.Tasks;

namespace TURAG.Feldbus.Transport
{
    /// <summary>
    /// Serial port transport implementation.
    /// </summary>
    public class SerialPortTransport : PhysicalUartTransport
    {
        readonly SerialPort serialPort;
        int baudrate;

        public SerialPortTransport(string portName, int baudrate_, int timeoutMs = 50, double deviceProcessingTime = 1e-3) :
            base(baudrate_, deviceProcessingTime)
        {
            TimeoutMs = timeoutMs;
            baudrate = baudrate_;

            serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudrate,
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

        public override int Baudrate
        {
            get => baudrate;
            set
            {
                if (Baudrate != 0 && value != baudrate)
                {
                    throw new NotImplementedException();
                }
            }
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
        protected override ValueTuple<bool success, byte[] receivedData> DoTransceive(byte[] data, int bytesRequested)
#else
        protected override (bool success, byte[] receivedData) DoTransceive(byte[] data, int bytesRequested)
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

            byte[] receiveResult = new byte[bytesReadTotal];
            Array.Copy(received, receiveResult, bytesReadTotal);
            return (bytesReadTotal == bytesRequested, receiveResult);
        }

        protected override async Task<(bool success, byte[] receivedData)> DoTransceiveAsync(byte[] data, int bytesRequested)
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

            byte[] receiveResult = new byte[bytesReadTotal];
            Array.Copy(received, receiveResult, bytesReadTotal);
            return (bytesReadTotal == bytesRequested, receiveResult);
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

    }
}
