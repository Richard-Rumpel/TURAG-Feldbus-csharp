using System;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Devices
{
    public class Aseb : Device
    {
        public Aseb(int address, TransportAbstraction busAbstraction) : base(address, busAbstraction)
        {
        }

        public override bool Initialize()
        {
            return InitializeAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public override Task<bool> InitializeAsync()
        {
            return InitializeAsyncInternal(sync: false);
        }

        private async Task<bool> InitializeAsyncInternal(bool sync)
        {
            if ((sync ? base.Initialize() : await base.InitializeAsync()) == false)
            {
                return false;
            }

            if (!initialized)
            {
                syncSize = sync ? ReceiveSyncSize() : await ReceiveSyncSizeAsync();
                numberOfDigitalInputs = sync ? ReceiveDigitalInputSize() : await ReceiveDigitalInputSizeAsync();
                numberOfDigitalOutputs = sync ? ReceiveDigitalOutputSize() : await ReceiveDigitalOutputSizeAsync();
                numberOfAnalogInputs = sync ? ReceiveAnalogInputSize() : await ReceiveAnalogInputSizeAsync();

                if (syncSize == -1 || numberOfDigitalInputs == -1 || numberOfDigitalOutputs == -1 || numberOfAnalogInputs == -1)
                {
                    return false;
                }

                if ((sync ? InitCommandNames() : await InitCommandNamesAsync()) == false)
                {
                    return false;
                }

                if ((sync ? InitAnalogInputs() : await InitAnalogInputsAsync()) == false)
                {
                    return false;
                }

                if ((sync ? InitDigitalOutputBuffer() : await InitDigitalOutputBufferAsync()) == false)
                {
                    return false;
                }

                initialized = true;
            }
            return true;
        }

        /// <summary>
        /// Synchronizes the inputs of the device.
        /// </summary>
        /// <returns>True on success, false otherwise.</returns>
        public bool Sync()
        {
            return SyncAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public Task<bool> SyncAsync()
        {
            return SyncAsyncInternal(sync: false);
        }

        private async Task<bool> SyncAsyncInternal(bool sync)
        {
            if (!initialized)
            {
                return false;
            }

            if (syncSize > 2)
            {
                BusRequest request = new BusRequest();
                request.Write((byte)0xFF);

                BusTransceiveResult result = sync ? Transceive(request, syncSize - 2) : await TransceiveAsync(request, syncSize - 2);

                if (!result.Success)
                {
                    return false;
                }
                else
                {
                    if (numberOfDigitalInputs > 0)
                    {
                        digitalInputValue = result.Response.ReadUInt16();
                    }

                    for (int i = 0; i < numberOfAnalogInputs; ++i)
                    {
                        analogInputs[i] = result.Response.ReadUInt16();
                    }
                }
            }
            return true;
        }

        public bool GetDigitalInput(uint key)
        {
            return (digitalInputValue & (1 << (int)key)) != 0;
        }

        public float GetAnalogInput(uint key)
        {
            if (!initialized)
            {
                return float.NaN;
            }
            else
            {
                return analogInputs[key];
            }
        }

        public bool GetDigitalOutput(uint key)
        {
            return (digitalOutputValue & (1 << (int)key)) != 0;
        }

        /// <summary>
        /// Sets the specified digital output to the given state.
        /// </summary>
        /// <param name="key">Index of the output.</param>
        /// <param name="value">value to set.</param>
        /// <returns>True on success, false otherwise.</returns>
        public bool SetDigitalOutput(uint key, bool value)
        {
            return SetDigitalOutputAsyncInternal(key, value, sync: true).GetAwaiter().GetResult();
        }

        public Task<bool> SetDigitalOutputAsync(uint key, bool value)
        {
            return SetDigitalOutputAsyncInternal(key, value, sync: false);
        }

        private async Task<bool> SetDigitalOutputAsyncInternal(uint key, bool value, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write((byte)(key + 33));
            request.Write((byte)(value ? 1 : 0));

            BusTransceiveResult result = sync ? Transceive(request, 0) : await TransceiveAsync(request, 0);

            if (!result.Success)
            {
                return false;
            }
            else
            {
                if (value)
                {
                    digitalOutputValue |= (UInt16)(1 << (int)key);
                }
                else
                {
                    digitalOutputValue &= (UInt16)(~(1 << (int)key));
                }

                return true;
            }
        }

        public string GetDigitalInputName(uint key)
        {
            return digitalInputNames[key];
        }

        public string GetDigitalOutputName(uint key)
        {
            return digitalOutputNames[key];
        }

        public string GetAnalogInputName(uint key)
        {
            return analogInputNames[key];
        }

        public int NumberOfDigitalInputs
        {
            get => numberOfDigitalInputs;
        }

        public int NumberOfDigitalOutputs
        {
            get => numberOfDigitalOutputs;
        }

        public int NumberOfAnalogInputs
        {
            get => numberOfAnalogInputs;
        }

        public bool Initialized
        {
            get => initialized;
        }

        private int ReceiveSyncSize()
        {
            return ReceiveByte((byte)0xF4);
        }
        private Task<int> ReceiveSyncSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF4);
        }

        private int ReceiveDigitalInputSize()
        {
            return ReceiveByte((byte)0xF7);
        }
        private Task<int> ReceiveDigitalInputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF7);
        }

        private int ReceiveDigitalOutputSize()
        {
            return ReceiveByte((byte)0xF8);
        }
        private Task<int> ReceiveDigitalOutputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF8);
        }

        private int ReceiveAnalogInputSize()
        {
            return ReceiveByte((byte)0xF9);
        }
        private Task<int> ReceiveAnalogInputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF9);
        }

        private int ReceiveByte(byte command, byte? secondByte = null)
        {
            return ReceiveByteAsyncInternal(command, secondByte, sync: true).GetAwaiter().GetResult();
        }

        private Task<int> ReceiveByteAsync(byte command, byte? secondByte = null)
        {
            return ReceiveByteAsyncInternal(command, secondByte, sync: false);
        }

        private async Task<int> ReceiveByteAsyncInternal(byte command, byte? secondByte, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write(command);
            if (secondByte != null)
            {
                request.Write((byte)secondByte);
            }

            BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

            if (!result.Success)
            {
                return -1;
            }
            else
            {
                return result.Response.ReadByte();
            }
        }

        private float ReceiveFloat(byte command, byte? secondByte = null)
        {
            return ReceiveFloatAsyncInternal(command, secondByte, sync: true).GetAwaiter().GetResult();
        }

        private Task<float> ReceiveFloatAsync(byte command, byte? secondByte = null)
        {
            return ReceiveFloatAsyncInternal(command, secondByte, sync: false);
        }

        private async Task<float> ReceiveFloatAsyncInternal(byte command, byte? secondByte, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write(command);
            if (secondByte != null)
            {
                request.Write((byte)secondByte);
            }

            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

            if (!result.Success)
            {
                return float.NaN;
            }
            else
            {
                return result.Response.ReadSingle();
            }
        }

        private string ReceiveCommandName(uint raw_key)
        {
            return ReceiveCommandNameAsyncInternal(raw_key, sync: true).GetAwaiter().GetResult();
        }

        private Task<string> ReceiveCommandNameAsync(uint raw_key)
        {
            return ReceiveCommandNameAsyncInternal(raw_key, sync: false);
        }

        private async Task<string> ReceiveCommandNameAsyncInternal(uint raw_key, bool sync)
        {
            int commandNameLength = sync ? ReceiveByte((byte)0xF6, (byte)raw_key) : await ReceiveByteAsync((byte)0xF6, (byte)raw_key);
            if (commandNameLength == -1)
            {
                return null;
            }

            BusRequest request = new BusRequest();
            request.Write((byte)0xF5);
            request.Write((byte)raw_key);

            BusTransceiveResult result = sync ? Transceive(request, commandNameLength) : await TransceiveAsync(request, commandNameLength);

            if (!result.Success)
            {
                return null;
            }
            else
            {
                byte[] commandNameBytes = result.Response.ReadBytes(commandNameLength);
                System.Text.UTF8Encoding enc = new System.Text.UTF8Encoding();
                string commandName = enc.GetString(commandNameBytes);

                return commandName;
            }
        }

        private bool InitDigitalOutputBuffer()
        {
            return InitDigitalOutputBufferAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        private Task<bool> InitDigitalOutputBufferAsync()
        {
            return InitDigitalOutputBufferAsyncInternal(sync: false);
        }

        private async Task<bool> InitDigitalOutputBufferAsyncInternal(bool sync)
        {
            if (numberOfDigitalOutputs == -1)
            {
                return false;
            }

            digitalOutputValue = 0;

            for (int i = 0; i < numberOfDigitalOutputs; ++i)
            {
                int outState = sync ? ReceiveByte((byte)(i + 33)) : await ReceiveByteAsync((byte)(i + 33));
                if (outState == -1)
                {
                    return false;
                }

                if (outState != 0)
                {
                    digitalOutputValue |= (UInt16)(1 << i);
                }
            }

            return true;
        }

        private bool InitAnalogInputs()
        {
            return InitAnalogInputsAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        private Task<bool> InitAnalogInputsAsync()
        {
            return InitAnalogInputsAsyncInternal(sync: false);
        }

        private async Task<bool> InitAnalogInputsAsyncInternal(bool sync)
        {
            if (numberOfAnalogInputs == -1)
            {
                return false;
            }

            analogInputs = new UInt16[numberOfAnalogInputs];
            analogInputFactors = new float[numberOfAnalogInputs];

            for (int i = 0; i < numberOfAnalogInputs; ++i)
            {
                float factor = sync ? ReceiveFloat((byte)0xFB, (byte)(i + 17)) : await ReceiveFloatAsync((byte)0xFB, (byte)(i + 17));
                if (factor == float.NaN)
                {
                    return false;
                }
                else
                {
                    analogInputFactors[i] = factor;
                }
            }
            return true;
        }

        private bool InitCommandNames()
        {
            return InitCommandNamesAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        private Task<bool> InitCommandNamesAsync()
        {
            return InitCommandNamesAsyncInternal(sync: false);
        }

        private async Task<bool> InitCommandNamesAsyncInternal(bool sync)
        {
            if (numberOfDigitalInputs == -1 || numberOfDigitalOutputs == -1 || numberOfAnalogInputs == -1)
            {
                return false;
            }

            digitalInputNames = new string[numberOfDigitalInputs];
            for (int i = 0; i < numberOfDigitalInputs; ++i)
            {
                string commandName = sync ? ReceiveCommandName((uint)(i + 1)) : await ReceiveCommandNameAsync((uint)(i + 1));
                if (commandName == null)
                {
                    return false;
                }
                else
                {
                    digitalInputNames[i] = commandName;
                }
            }

            digitalOutputNames = new string[numberOfDigitalOutputs];
            for (int i = 0; i < numberOfDigitalOutputs; ++i)
            {
                string commandName = sync ? ReceiveCommandName((uint)(i + 33)) : await ReceiveCommandNameAsync((uint)(i + 33));
                if (commandName == null)
                {
                    return false;
                }
                else
                {
                    digitalOutputNames[i] = commandName;
                }
            }

            analogInputNames = new string[numberOfAnalogInputs];
            for (int i = 0; i < numberOfAnalogInputs; ++i)
            {
                string commandName = sync ? ReceiveCommandName((uint)(i + 17)) : await ReceiveCommandNameAsync((uint)(i + 17));
                if (commandName == null)
                {
                    return false;
                }
                else
                {
                    analogInputNames[i] = commandName;
                }
            }

            return true;
        }


        private int numberOfDigitalInputs = -1;
        private int numberOfDigitalOutputs = -1;
        private int numberOfAnalogInputs = -1;
        private int syncSize = -1;

        private UInt16 digitalInputValue = 0;
        private string[] digitalInputNames = null;

        private UInt16 digitalOutputValue = 0;
        private string[] digitalOutputNames = null;

        private UInt16[] analogInputs = null;
        private float[] analogInputFactors = null;
        private string[] analogInputNames = null;

        private bool initialized = false;
    }
}
