using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;

namespace TURAG.Feldbus
{
    public class Aseb : Device
    {
        public Aseb(int address, TransportAbstraction busAbstraction) : base(address, busAbstraction)
        {
        }

        public override async Task<bool> InitializeAsync()
        {
            await base.InitializeAsync();

            if (!initialized)
            {
                syncSize = await ReceiveSyncSizeAsync();
                numberOfDigitalInputs = await ReceiveDigitalInputSizeAsync();
                numberOfDigitalOutputs = await ReceiveDigitalOutputSizeAsync();
                numberOfAnalogInputs = await ReceiveAnalogInputSizeAsync();

                if (syncSize == -1 || numberOfDigitalInputs == -1 || numberOfDigitalOutputs == -1 || numberOfAnalogInputs == -1)
                {
                    return false;
                }

                if (!await InitCommandNamesAsync())
                {
                    return false;
                }

                if (!await InitAnalogInputsAsync())
                {
                    return false;
                }

                if (!await InitDigitalOutputBufferAsync())
                {
                    return false;
                }

                initialized = true;
            }
            return true;
        }

        public async Task<bool> Sync()
        {
            if (!initialized)
            {
                return false;
            }

            if (syncSize > 2)
            {
                Request request = new Request();
                request.Write((byte)0xFF);

                TransceiveResult result = await TransceiveAsync(request, syncSize - 2);

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

        public async Task<bool> SetDigitalOutputAsync(uint key, bool value)
        {
            Request request = new Request();
            request.Write((byte)(key + 33));
            request.Write((byte)(value ? 1 : 0));

            TransceiveResult result = await TransceiveAsync(request, 0);

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

        private Task<int> ReceiveSyncSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF4);
        }

        private Task<int> ReceiveDigitalInputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF7);
        }

        private Task<int> ReceiveDigitalOutputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF8);
        }

        private Task<int> ReceiveAnalogInputSizeAsync()
        {
            return ReceiveByteAsync((byte)0xF9);
        }

        private async Task<int> ReceiveByteAsync(byte command, byte? secondByte = null)
        {
            Request request = new Request();
            request.Write(command);
            if (secondByte != null)
            {
                request.Write((byte)secondByte);
            }

            TransceiveResult result = await TransceiveAsync(request, 1);

            if (!result.Success)
            {
                return -1;
            }
            else
            {
                return result.Response.ReadByte();
            }
        }

        private async Task<float> ReceiveFloatAsync(byte command, byte? secondByte = null)
        {
            Request request = new Request();
            request.Write(command);
            if (secondByte != null)
            {
                request.Write((byte)secondByte);
            }

            TransceiveResult result = await TransceiveAsync(request, 4);

            if (!result.Success)
            {
                return float.NaN;
            }
            else
            {
                return result.Response.ReadSingle();
            }
        }

        private async Task<string> ReceiveCommandNameAsync(uint raw_key)
        {
            int commandNameLength = await ReceiveByteAsync((byte)0xF6, (byte)raw_key);
            if (commandNameLength == -1)
            {
                return null;
            }

            Request request = new Request();
            request.Write((byte)0xF5);
            request.Write((byte)raw_key);

            TransceiveResult result = await TransceiveAsync(request, commandNameLength);

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

        private async Task<bool> InitDigitalOutputBufferAsync()
        {
            if (numberOfDigitalOutputs == -1)
            {
                return false;
            }

            digitalOutputValue = 0;

            for (int i = 0; i < numberOfDigitalOutputs; ++i)
            {
                int outState = await ReceiveByteAsync((byte)(i + 33));
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

        private async Task<bool> InitAnalogInputsAsync()
        {
            if (numberOfAnalogInputs == -1)
            {
                return false;
            }

            analogInputs = new UInt16[numberOfAnalogInputs];
            analogInputFactors = new float[numberOfAnalogInputs];

            for (int i = 0; i < numberOfAnalogInputs; ++i)
            {
                float factor = await ReceiveFloatAsync((byte)0xFB, (byte)(i + 17));
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

        private async Task<bool> InitCommandNamesAsync()
        {
            if (numberOfDigitalInputs == -1 || numberOfDigitalOutputs == -1 || numberOfAnalogInputs == -1)
            {
                return false;
            }

            digitalInputNames = new string[numberOfDigitalInputs];
            for (int i = 0; i < numberOfDigitalInputs; ++i)
            {
                string commandName = await ReceiveCommandNameAsync((uint)(i + 1));
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
                string commandName = await ReceiveCommandNameAsync((uint)(i + 33));
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
                string commandName = await ReceiveCommandNameAsync((uint)(i + 17));
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
