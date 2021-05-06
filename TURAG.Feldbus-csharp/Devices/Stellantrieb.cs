using System;
using System.Text;
using System.Threading.Tasks;
using TURAG.Feldbus.Transport;
using TURAG.Feldbus.Types;
using TURAG.Feldbus.Util;

namespace TURAG.Feldbus.Devices
{
    public class Stellantrieb : Device
    {
        private enum CommandKey : byte
        {
            CommandInfoGet = 0x00,
            CommandInfoGetCommandSetSize = 0x01,
            CommandInfoGetNameLength = 0x02,
            CommandInfoGetName = 0x03,

            StructuredOutputGet = 0xFF,
            StructuredOutputControl = 0xFF,
            StructuredOutputSetStructure = 0x00,
            StructuredOutputGetBufferSize = 0x01,
            StructuredOutputTableOk = 0x01,
            StructuredOutputTableRejected = 0x00,
        };


        public class Command
        {
            private static readonly float CommandFactorControlValue = 0.0f;

            public enum Access : byte
            {
                ReadOnly = 0x00,
                WriteAccess = 0x01
            };

            public enum Length : byte
            {
                None = 0x00,
                Byte = 0x01,
                Short = 0x02,
                Long = 0x04,
                NoneAsText = 0x05,
                Float = 0x06
            };

            internal Command(Access accessMode, Length length, float factor)
            {
                AccessMode = accessMode;
                CommandLength = length;
                Factor = factor;
                Name = "uninitialized";
            }

            internal Command(string name, Access accessMode, Length length, float factor)
            {
                AccessMode = accessMode;
                CommandLength = length;
                Factor = factor;
                Name = name;
            }

            public Access AccessMode { get; }
            public Length CommandLength { get; }
            public float Factor { get; }
            public string Name { get; }

            public bool IsControlValue
            {
                get
                {
                    return Factor == CommandFactorControlValue;
                }
            }

            public virtual bool BufferValid
            {
                get
                {
                    return false;
                }
            }
        }

        private class CommandInternal : Command
        {
            internal CommandInternal(Access accessMode, Length length, float factor) : base(accessMode, length, factor)
            {
                bufferValid = false;
            }

            internal CommandInternal(string name, Access accessMode, Length length, float factor) : base(name, accessMode, length, factor)
            {
                bufferValid = false;
            }

            public float floatBuffer;
            public Int32 intBuffer;
            public bool bufferValid;
        }



        public Stellantrieb(int address, TransportAbstraction busAbstraction) : base(address, busAbstraction)
        {
            commandSet = null;
        }

        public override ErrorCode Initialize()
        {
            return InitializeAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public override Task<ErrorCode> InitializeAsync()
        {
            return InitializeAsyncInternal(sync: false);
        }

        private async Task<ErrorCode> InitializeAsyncInternal(bool sync)
        {
            ErrorCode baseInitError = sync ? base.Initialize() : await base.InitializeAsync();
            if (baseInitError != ErrorCode.Success)
            {
                return baseInitError;
            }

            return sync ? PopulateCommandSet() : await PopulateCommandSetAsync();
        }

        private (ErrorCode, uint) GetCommandsetLength()
        {
            return GetCommandsetLengthAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        private Task<(ErrorCode, uint)> GetCommandsetLengthAsync()
        {
            return GetCommandsetLengthAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode, uint)> GetCommandsetLengthAsyncInternal(bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write((byte)CommandKey.CommandInfoGetCommandSetSize);
            request.Write((byte)CommandKey.CommandInfoGetCommandSetSize);
            request.Write((byte)CommandKey.CommandInfoGetCommandSetSize);
            request.Write((byte)CommandKey.CommandInfoGetCommandSetSize);

            BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

            if (result.Success)
            {
                return (ErrorCode.Success, result.Response.ReadByte());
            }
            else
            {
                return (result.TransportError, 0);
            }
        }

        private ErrorCode PopulateCommandSet()
        {
            return PopulateCommandSetAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        private Task<ErrorCode> PopulateCommandSetAsync()
        {
            return PopulateCommandSetAsyncInternal(sync: false);
        }

        private async Task<ErrorCode> PopulateCommandSetAsyncInternal(bool sync)
        {
            (ErrorCode commandSetLengthError, uint commandSetLength) = sync ?
                GetCommandsetLength() :
                await GetCommandsetLengthAsync();

            if (commandSetLengthError != ErrorCode.Success)
            {
                Log.Logger?.Error("unable to retrieve command set length", this);
                return commandSetLengthError;
            }

            if (commandSetLength == 0)
            {
                Log.Logger?.Error("command set length of 0 is unsupported", this);
                return ErrorCode.StellantriebCommandLengthZero;
            }

            CommandInternal[] tempCommandSet = new CommandInternal[commandSetLength];


            for (int i = 0; i < commandSetLength; ++i)
            {
                BusRequest request = new BusRequest();
                request.Write((byte)(i + 1));
                request.Write((byte)CommandKey.CommandInfoGet);
                request.Write((byte)CommandKey.CommandInfoGet);
                request.Write((byte)CommandKey.CommandInfoGet);

                BusTransceiveResult result = sync ? Transceive(request, 6) : await TransceiveAsync(request, 6);

                if (!result.Success)
                {
                    return result.TransportError;
                }

                tempCommandSet[i] = new CommandInternal(
                    (Command.Access)result.Response.ReadByte(),
                    (Command.Length)result.Response.ReadByte(),
                    result.Response.ReadSingle());
            }

            commandSet = tempCommandSet;

            return ErrorCode.Success;
        }

        public ErrorCode RetrieveCommandNames()
        {
            return RetrieveCommandNamesAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        public Task<ErrorCode> RetrieveCommandNamesAsync()
        {
            return RetrieveCommandNamesAsyncInternal(sync: false);
        }

        private async Task<ErrorCode> RetrieveCommandNamesAsyncInternal(bool sync)
        {
            if (commandSet == null)
            {
                ErrorCode populateCommandSetError = sync ?
                    PopulateCommandSet() :
                    await PopulateCommandSetAsync();

                if (populateCommandSetError != ErrorCode.Success)
                {
                    Log.Logger?.Error("unable to retrieve command set", this);
                    return populateCommandSetError;
                }
            }

            for (int i = 0; i < commandSet.Length; ++i)
            {
                (ErrorCode commandNameError, string commandName) = sync ?
                    GetCommandName((byte)(i + 1)) :
                    await GetCommandNameAsync((byte)(i + 1));

                if (commandNameError != ErrorCode.Success)
                {
                    Log.Logger?.Error("unable to retrieve command name", this);
                    return commandNameError;
                }

                Command oldCommand = commandSet[i];

                commandSet[i] = new CommandInternal(
                    commandName,
                    oldCommand.AccessMode,
                    oldCommand.CommandLength,
                    oldCommand.Factor);
            }

            return ErrorCode.Success;
        }

        private (ErrorCode, byte) GetCommandNameLength(byte key)
        {
            return GetCommandNameLengthAsyncInternal(key, sync: true).GetAwaiter().GetResult();
        }

        private Task<(ErrorCode, byte)> GetCommandNameLengthAsync(byte key)
        {
            return GetCommandNameLengthAsyncInternal(key, sync: false);
        }

        private async Task<(ErrorCode, byte)> GetCommandNameLengthAsyncInternal(byte key, bool sync)
        {
            BusRequest request = new BusRequest();
            request.Write(key);
            request.Write((byte)CommandKey.CommandInfoGetNameLength);
            request.Write((byte)CommandKey.CommandInfoGetNameLength);
            request.Write((byte)CommandKey.CommandInfoGetNameLength);

            BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

            if (result.Success)
            {
                return (ErrorCode.Success, result.Response.ReadByte());
            }
            else
            {
                return (result.TransportError, 0);
            }
        }

        private (ErrorCode, string) GetCommandName(byte key)
        {
            return GetCommandNameAsyncInternal(key, sync: true).GetAwaiter().GetResult();
        }

        private Task<(ErrorCode, string)> GetCommandNameAsync(byte key)
        {
            return GetCommandNameAsyncInternal(key, sync: false);
        }

        private async Task<(ErrorCode, string)> GetCommandNameAsyncInternal(byte key, bool sync)
        {
            (ErrorCode commandNameLengthError, byte length) = sync ?
                GetCommandNameLength(key) :
                await GetCommandNameLengthAsync(key);

            if (commandNameLengthError != ErrorCode.Success)
            {
                Log.Logger?.Error("unable to retrieve command name length", this);
                return (commandNameLengthError, null);
            }

            BusRequest request = new BusRequest();
            request.Write(key);
            request.Write((byte)CommandKey.CommandInfoGetName);
            request.Write((byte)CommandKey.CommandInfoGetName);
            request.Write((byte)CommandKey.CommandInfoGetName);

            BusTransceiveResult result = sync ? Transceive(request, length) : await TransceiveAsync(request, length);

            if (result.Success)
            {
                return (ErrorCode.Success, Encoding.UTF8.GetString(result.Response.ReadBytes(length)));
            }
            else
            {
                return (result.TransportError, null);
            }
        }


        public ErrorCode GetFloatValue(byte key, out float value)
        {
            ErrorCode error;
            (error, value) = GetFloatValueAsyncInternal(key, sync: true).GetAwaiter().GetResult();
            return error;
        }

#if __DOXYGEN__
        public Task<Tuple<ErrorCode, float>> GetFloatValueAsync(byte key)
#else
        public Task<(ErrorCode, float)> GetFloatValueAsync(byte key)
#endif
        {
            return GetFloatValueAsyncInternal(key, sync: false);
        }

        private async Task<(ErrorCode, float)> GetFloatValueAsyncInternal(byte key, bool sync)
        {
            if (CommandSet == null)
            {
                Log.Logger?.Error("commandSet not populated", this);
                return (ErrorCode.DeviceNotInitialized, float.NaN);
            }
            if (key > CommandSet.Length || key == 0)
            {
                Log.Logger?.Error("key not within commandSetLength", this);
                return (ErrorCode.StellantriebInvalidKey, float.NaN);
            }

            CommandInternal command = commandSet[key - 1];

            if (command.CommandLength == Command.Length.None || command.CommandLength == Command.Length.NoneAsText)
            {
                Log.Logger?.Error("%s: key not supported", this);
                return (ErrorCode.StellantriebInvalidKey, float.NaN);
            }
            if (command.IsControlValue)
            {
                Log.Logger?.Error("value with key %u is not floating point, which was requested", this);
                return (ErrorCode.StellantriebInvalidKey, float.NaN);
            }

            if (!command.bufferValid)
            {
                BusRequest request = new BusRequest();
                request.Write(key);

                switch (command.CommandLength)
                {
                    case Command.Length.Byte:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

                            if (!result.Success)
                            {
                                return (result.TransportError, float.NaN);
                            }
                            else
                            {
                                command.floatBuffer = (float)result.Response.ReadByte() * command.Factor;
                            }
                            break;
                        }
                    case Command.Length.Short:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 2) : await TransceiveAsync(request, 2);

                            if (!result.Success)
                            {
                                return (result.TransportError, float.NaN);
                            }
                            else
                            {
                                command.floatBuffer = (float)result.Response.ReadInt16() * command.Factor;
                            }
                            break;
                        }
                    case Command.Length.Long:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

                            if (!result.Success)
                            {
                                return (result.TransportError, float.NaN);
                            }
                            else
                            {
                                command.floatBuffer = (float)result.Response.ReadInt32() * command.Factor;
                            }
                            break;
                        }
                    case Command.Length.Float:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

                            if (!result.Success)
                            {
                                return (result.TransportError, float.NaN);
                            }
                            else
                            {
                                command.floatBuffer = result.Response.ReadSingle() * command.Factor;
                            }
                            break;
                        }
                    default:
                        return (ErrorCode.Unspecified, float.NaN);
                }

                // protocol definition: writable values are bufferable
                if (command.AccessMode == Command.Access.WriteAccess)
                {
                    command.bufferValid = true;
                }

            }
            return (ErrorCode.Success, command.floatBuffer);
        }

        public ErrorCode GetIntValue(byte key, out int value)
        {
            ErrorCode error;
            (error, value) = GetIntValueAsyncInternal(key, sync: true).GetAwaiter().GetResult();
            return error;
        }

#if __DOXYGEN__
        public Task<Tuple<ErrorCode, int>> GetIntValueAsync(byte key)
#else
        public Task<(ErrorCode, int)> GetIntValueAsync(byte key)
#endif
        {
            return GetIntValueAsyncInternal(key, sync: false);
        }

        private async Task<(ErrorCode, int)> GetIntValueAsyncInternal(byte key, bool sync)
        {
            if (CommandSet == null)
            {
                Log.Logger?.Error("commandSet not populated", this);
                return (ErrorCode.DeviceNotInitialized, 0);
            }
            if (key > CommandSet.Length || key == 0)
            {
                Log.Logger?.Error("key not within commandSetLength", this);
                return (ErrorCode.StellantriebInvalidKey, 0);
            }

            CommandInternal command = commandSet[key - 1];

            if (command.CommandLength == Command.Length.None || command.CommandLength == Command.Length.NoneAsText)
            {
                Log.Logger?.Error("%s: key not supported", this);
                return (ErrorCode.StellantriebInvalidKey, 0);
            }
            if (!command.IsControlValue)
            {
                Log.Logger?.Error("value with key %u is not a control value, which was requested", this);
                return (ErrorCode.StellantriebInvalidKey, 0);
            }

            if (!command.bufferValid)
            {
                BusRequest request = new BusRequest();
                request.Write(key);

                switch (command.CommandLength)
                {
                    case Command.Length.Byte:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 1) : await TransceiveAsync(request, 1);

                            if (!result.Success)
                            {
                                return (result.TransportError, 0);
                            }
                            else
                            {
                                command.intBuffer = (int)result.Response.ReadByte();
                            }
                            break;
                        }
                    case Command.Length.Short:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 2) : await TransceiveAsync(request, 2);

                            if (!result.Success)
                            {
                                return (result.TransportError, 0);
                            }
                            else
                            {
                                command.intBuffer = (int)result.Response.ReadInt16();
                            }
                            break;
                        }
                    case Command.Length.Long:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

                            if (!result.Success)
                            {
                                return (result.TransportError, 0);
                            }
                            else
                            {
                                command.intBuffer = (int)result.Response.ReadInt32();
                            }
                            break;
                        }
                    case Command.Length.Float:
                        {
                            BusTransceiveResult result = sync ? Transceive(request, 4) : await TransceiveAsync(request, 4);

                            if (!result.Success)
                            {
                                return (result.TransportError, 0);
                            }
                            else
                            {
                                // this actually does not make sense. But the problem ist that the configuration is
                                // invalid in the first place.
                                command.intBuffer = (int)result.Response.ReadSingle();
                            }
                            break;
                        }
                    default:
                        return (ErrorCode.Unspecified, 0);
                }

                // protocol definition: writable values are bufferable
                if (command.AccessMode == Command.Access.WriteAccess)
                {
                    command.bufferValid = true;
                }

            }
            return (ErrorCode.Success, command.intBuffer);
        }


        public ErrorCode SetFloatValue(byte key, float value)
        {
            return SetFloatValueAsyncInternal(key, value, sync: true).GetAwaiter().GetResult();
        }

        public Task<ErrorCode> SetFloatValueAsync(byte key, float value)
        {
            return SetFloatValueAsyncInternal(key, value, sync: false);
        }

        private async Task<ErrorCode> SetFloatValueAsyncInternal(byte key, float value, bool sync)
        {
            if (CommandSet == null)
            {
                Log.Logger?.Error("commandSet not populated", this);
                return ErrorCode.DeviceNotInitialized;
            }
            if (key > CommandSet.Length || key == 0)
            {
                Log.Logger?.Error("key not within commandSetLength", this);
                return ErrorCode.StellantriebInvalidKey;
            }

            CommandInternal command = commandSet[key - 1];

            if (command.CommandLength == Command.Length.None || command.CommandLength == Command.Length.NoneAsText)
            {
                Log.Logger?.Error("%s: key not supported", this);
                return ErrorCode.StellantriebInvalidKey;
            }
            if (command.AccessMode != Command.Access.WriteAccess)
            {
                Log.Logger?.Error("key not writable", this);
                return ErrorCode.StellantriebValueReadOnly;
            }
            if (command.IsControlValue)
            {
                Log.Logger?.Error("value with key %u is not floating point, which was requested", this);
                return ErrorCode.StellantriebInvalidKey;
            }

            command.bufferValid = false;

            BusRequest request = new BusRequest();
            request.Write(key);

            switch (command.CommandLength)
            {
                case Command.Length.Byte:
                    request.Write((byte)(value / command.Factor));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Short:
                    request.Write((Int16)(value / command.Factor));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Long:
                    request.Write((Int32)(value / command.Factor));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Float:
                    request.Write(value / command.Factor);
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                default:
                    return ErrorCode.Unspecified;
            }
        }

        public ErrorCode SetIntValue(byte key, int value)
        {
            return SetIntValueAsyncInternal(key, value, sync: true).GetAwaiter().GetResult();
        }

        public Task<ErrorCode> SetIntValueAsync(byte key, int value)
        {
            return SetIntValueAsyncInternal(key, value, sync: false);
        }

        private async Task<ErrorCode> SetIntValueAsyncInternal(byte key, int value, bool sync)
        {
            if (CommandSet == null)
            {
                Log.Logger?.Error("commandSet not populated", this);
                return ErrorCode.DeviceNotInitialized;
            }
            if (key > CommandSet.Length || key == 0)
            {
                Log.Logger?.Error("key not within commandSetLength", this);
                return ErrorCode.StellantriebInvalidKey;
            }

            CommandInternal command = commandSet[key - 1];

            if (command.CommandLength == Command.Length.None || command.CommandLength == Command.Length.NoneAsText)
            {
                Log.Logger?.Error("%s: key not supported", this);
                return ErrorCode.StellantriebInvalidKey;
            }
            if (command.AccessMode != Command.Access.WriteAccess)
            {
                Log.Logger?.Error("key not writable", this);
                return ErrorCode.StellantriebValueReadOnly;
            }
            if (!command.IsControlValue)
            {
                Log.Logger?.Error("value with key %u is floating point, which was not requested", this);
                return ErrorCode.StellantriebInvalidKey;
            }

            command.bufferValid = false;

            BusRequest request = new BusRequest();
            request.Write(key);

            switch (command.CommandLength)
            {
                case Command.Length.Byte:
                    request.Write((byte)(value));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Short:
                    request.Write((short)(value));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Long:
                    request.Write((Int32)(value));
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                case Command.Length.Float:
                    // this actually does not make sense. But the problem ist that the configuration is
                    // invalid in the first place.
                    request.Write((float)value);
                    return sync ? Transceive(request, 0).TransportError : (await TransceiveAsync(request, 0)).TransportError;

                default:
                    return ErrorCode.Unspecified;
            }
        }


        public Command[] CommandSet
        {
            get => commandSet;
        }

        private CommandInternal[] commandSet;
    }
}
