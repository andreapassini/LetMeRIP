using ExitGames.Diagnostics.Counter;
using System.IO;
using System.Text;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics.Linux
{
    class LinuxCPUReader : ICounter
    {
        private readonly ValuesReader valueReader = new ValuesReader();

        public CounterType CounterType => CounterType.Numeric;

        public string Name => "CpuPhoton";

        public bool IsValid => true;

        public long Decrement()
        {
            throw new System.NotImplementedException();
        }

        public float GetNextValue()
        {
            return valueReader.ReadValue();
        }

        public long Increment()
        {
            throw new System.NotImplementedException();
        }

        public long IncrementBy(long value)
        {
            throw new System.NotImplementedException();
        }

        class ValuesReader
        {
            private FileStream file = new FileStream("/proc/stat", FileMode.Open, FileAccess.Read);
            private const int BuffLen = 512;

            private byte[] buffer = new byte[BuffLen];
            private ulong[] values = new ulong[10];

            private ulong total_tick = 0;
            ulong idle = 0; 


            public ValuesReader()
            {
                if (!ReadValues(file, buffer, values))
                {
                    return;
                }

                for (var i = 0; i < 10; i++)

                {
                    total_tick += values[i];
                }

                idle = values[3];


            }

            public float ReadValue()
            {
                var old_idle = idle;
                var total_tick_old = total_tick;

                if (!ReadValues(file, buffer, values))
                {
                    return 0.0f;
                }

                total_tick = 0;
                for (var i = 0; i < 10; i++)
                {
                    total_tick += values[i];
                }

                idle = values[3];

                ulong del_total_tick = total_tick - total_tick_old;
                ulong del_idle = idle - old_idle;

                return ((del_total_tick - del_idle) / (float)del_total_tick) * 100;
            }

            private static bool ReadValues(Stream file, byte[] buffer, ulong[] values)
            {
                int buffLen = buffer.Length;
                file.Seek(5, SeekOrigin.Begin);

                if (file.Read(buffer, 0, buffLen) < buffLen)
                {
                    return false;
                }

                var str = Encoding.ASCII.GetString(buffer);
                string[] numbers = str.Split(' ', '\n');

                for (int i = 0; i < 10; ++i)
                {
                    var v = numbers[i];
                    values[i] = ulong.Parse(v);
                }
                return true;
            }
        }
    }
}
