using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TURAG.Feldbus.Types;

namespace TURAG.Feldbus.Devices
{
    /// <summary>
    /// Helper class implementing UUID based binary address search.
    /// </summary>
    public class BinaryAddressSearcher
    {
        const int MaxLevel = 32;

        class SearchAddress
        {
            static public SearchAddress GetStartSearchAddress()
            {
                return new SearchAddress(0, 0);
            }

            private SearchAddress(uint address, int level)
            {
                Address = address;
                Level = level;
            }

            public uint Address { get; }
            public int Level { get; }

            public bool Found => Level > MaxLevel;

            public (SearchAddress detectedDevice, SearchAddress nextLevelAddresses, SearchAddress sameLevelAddress) GetNextAddresses(bool foundThisAddress)
            {
                SearchAddress nextLevelAddresses = null;
                SearchAddress sameLevelAddress = null;
                SearchAddress detectedDevice = null;

                if (Level == 0)
                {
                    if (foundThisAddress)
                    {
                        nextLevelAddresses = new SearchAddress(Address, Level + 1);
                    }
                }
                else
                {
                    bool leftBranch = (Address & (uint)(1 << (Level - 1))) == 0;

                    if (foundThisAddress)
                    {
                        var oneLevelDeeper = new SearchAddress(Address, Level + 1);
                        if (oneLevelDeeper.Found)
                        {
                            detectedDevice = oneLevelDeeper;
                        }
                        else
                        {
                            nextLevelAddresses = oneLevelDeeper;
                        }

                        if (leftBranch)
                        {
                            sameLevelAddress = new SearchAddress(Address | (uint)(1 << (Level - 1)), Level); // sibling on same level
                        }
                    } 
                    else if (leftBranch)
                    {
                        var oneLevelDeeper = new SearchAddress(Address | (uint)(1 << (Level - 1)), Level + 1); // sibling one level deeper
                        if (oneLevelDeeper.Found)
                        {
                            detectedDevice = oneLevelDeeper;
                        }
                        else
                        {
                            nextLevelAddresses = oneLevelDeeper;
                        }
                    }
                }

                return (detectedDevice, nextLevelAddresses, sameLevelAddress);
            }
        }

        readonly IList<SearchAddress> addressesToSearch;
        readonly DeviceLocator deviceLocator;
        readonly Stopwatch timer = new Stopwatch();
        long lastTransmissionTime = 0;

        /// <summary>
        /// Creates a new instance.
        /// </summary>
        /// <param name="locator">The DeviceLocator class to use for bus access.</param>
        /// <param name="delayTime">Minimum time to wait before requesting the next bus assertion. 
        /// <param name="onlyDevicesWithoutAddress">If true, then the
        ///  RequestBusAssertion​IfNoAddress broadcast is used to address only devices which do not
        ///  have a valid bus address.</param>
        /// Choose this delay according to the processing time of the bus devices.</param>
        public BinaryAddressSearcher(DeviceLocator locator, double delayTime = 5e-3, bool onlyDevicesWithoutAddress = false)
        {
            DelayTime = delayTime;
            OnlyDevicesWithoutAddress = onlyDevicesWithoutAddress;

            addressesToSearch = new List<SearchAddress> {
                (SearchAddress.GetStartSearchAddress())
            };
            Devices = new List<uint>();
            deviceLocator = locator;

            timer.Start();
        }

        /// <summary>
        /// Minimum time to wait before requesting the next bus assertion. 
        /// Choose this delay according to the processing time of the bus devices.
        /// </summary>
        public double DelayTime { get; }

        /// <summary>
        /// If true, then the
        ///  RequestBusAssertion​IfNoAddress broadcast is used to address only devices which do not
        ///  have a valid bus address.
        /// </summary>
        public bool OnlyDevicesWithoutAddress { get; }

        /// <summary>
        /// Detected devices.
        /// </summary>
        public IList<uint> Devices { get; }

        /// <summary>
        /// Attempts to find the next device on the bus.
        /// </summary>
        /// <returns>A tuple containing an error code, describing the result of the call, 
        /// and a flag indicating whether a device was found or not.</returns>
#if __DOXYGEN__
        public ValueTuple<ErrorCode error, bool foundDevice> FindNextDevice()
#else
        public (ErrorCode error, bool foundDevice) FindNextDevice()
#endif
        {
            return FindNextDeviceAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to find the next device on the bus.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.
        /// Contains a tuple containing an error code, describing the result of the call, 
        /// and a flag indicating whether a device was found or not.</returns>
        public Task<(ErrorCode error, bool foundDevice)> FindNextDeviceAsync()
        {
            return FindNextDeviceAsyncInternal(sync: false);
        }

        private async Task<(ErrorCode error, bool foundDevice)> FindNextDeviceAsyncInternal(bool sync)
        {
            while (addressesToSearch.Count > 0)
            {
                var searchAddress = addressesToSearch[0];
                addressesToSearch.RemoveAt(0);

                long timeDiff = timer.ElapsedTicks - lastTransmissionTime;
                double timeDiffSec = (double)timeDiff / Stopwatch.Frequency;

                // Console.WriteLine("timediffSec: " + timeDiffSec);

                if (timeDiffSec < DelayTime)
                {
                    int delayMs = (int)((DelayTime - timeDiffSec) * 1000.0) + 1;

                    if (sync)
                    {
                        Thread.Sleep(delayMs);
                    }
                    else
                    {
                        await Task.Delay(delayMs);
                    }
                }

                lastTransmissionTime = timer.ElapsedTicks;

                var error = sync ?
                    deviceLocator.RequestBusAssertion(searchAddress.Level, searchAddress.Address, OnlyDevicesWithoutAddress) :
                    await deviceLocator.RequestBusAssertionAsync(searchAddress.Level, searchAddress.Address, OnlyDevicesWithoutAddress);


                if (error != ErrorCode.Success && error != ErrorCode.NoAssertionDetected)
                {
                    return (error, false);
                }
                else
                {
                    (var detectedDevice, var nextLevelAddresses, var sameLevelAddress) = searchAddress.GetNextAddresses(error == ErrorCode.Success);

                    if (nextLevelAddresses != null)
                    {
                        addressesToSearch.Insert(0, nextLevelAddresses);
                    }
                    if (sameLevelAddress != null)
                    {
                        addressesToSearch.Add(sameLevelAddress);
                    }

                    if (detectedDevice != null)
                    {
                        Devices.Add(detectedDevice.Address);

                        // Console.WriteLine("found " + BaseDevice.FormatUuid(detectedDevice.Address));

                        return (ErrorCode.Success, true);
                    }
                }
            }

            return (ErrorCode.Success, false);
        }

        /// <summary>
        /// Attempts to find all devices available on the bus. The detected devices are placed in the Devices property.
        /// This function returns ErrorCode.Success once all devices are found.
        /// </summary>
        /// <returns>An error code describing the result of the call.</returns>
        public ErrorCode FindAllDevices()
        {
            return FindAllDevicesAsyncInternal(sync: true).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to find all devices available on the bus. The detected devices are placed in the Devices property.
        /// This function returns ErrorCode.Success once all devices are found.
        /// </summary>
        /// <returns>>A task representing the asynchronous operation.
        /// Contains an error code describing the result of the call. </returns>
        public Task<ErrorCode> FindAllDevicesAsync()
        {
            return FindAllDevicesAsyncInternal(sync: false);
        }

        private async Task<ErrorCode> FindAllDevicesAsyncInternal(bool sync)
        {
            while (addressesToSearch.Count > 0)
            {
                (var error, _) = sync ?
                    FindNextDeviceAsyncInternal(sync: true).GetAwaiter().GetResult() :
                    await FindNextDeviceAsyncInternal(sync: false);

                if (error != ErrorCode.Success)
                {
                    return error;
                }
            }

            return ErrorCode.Success;
        }
    }
}
