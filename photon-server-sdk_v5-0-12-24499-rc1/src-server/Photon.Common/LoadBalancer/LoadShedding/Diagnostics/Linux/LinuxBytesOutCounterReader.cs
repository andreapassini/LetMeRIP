using ExitGames.Diagnostics.Counter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics.Linux
{
    class LinuxBytesOutCounterReader : ICounter
    {
        #region .flds
        private readonly LinuxBandwidthValuesReader reader = LinuxBandwidthValuesReader.CreateBytesOutReader();
        #endregion

        #region .ctr
        public LinuxBytesOutCounterReader()
        {

        }
        #endregion

        #region ICounter 
        public CounterType CounterType => CounterType.Numeric;

        public string Name => "BytesOut";

        public bool IsValid => true;

        public long Decrement()
        {
            throw new System.NotImplementedException();
        }

        public float GetNextValue()
        {
            return this.reader.ReadValue();
        }

        public long Increment()
        {
            throw new System.NotImplementedException();
        }

        public long IncrementBy(long value)
        {
            throw new System.NotImplementedException();
        }

        #endregion
    }
}
