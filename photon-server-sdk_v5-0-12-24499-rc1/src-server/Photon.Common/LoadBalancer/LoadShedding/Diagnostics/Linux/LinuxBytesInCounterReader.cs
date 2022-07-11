using ExitGames.Diagnostics.Counter;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics.Linux
{
    class LinuxBytesInCounterReader : ICounter
    {
        #region .flds
        private readonly LinuxBandwidthValuesReader reader = LinuxBandwidthValuesReader.CreateBytesInReader();
        #endregion

        #region .ctr
        public LinuxBytesInCounterReader()
        {

        }
        #endregion

        #region ICounter 
        public CounterType CounterType => CounterType.Numeric;

        public string Name => "BytesIn";

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
