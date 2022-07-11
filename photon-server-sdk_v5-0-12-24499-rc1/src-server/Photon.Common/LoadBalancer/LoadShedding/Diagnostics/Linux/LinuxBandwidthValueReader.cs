using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExitGames.Logging;

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics.Linux
{
    class LinuxBandwidthValuesReader
    {
        #region .flds
        private static readonly ILogger log = LogManager.GetCurrentClassLogger();

        private FileStream file = new FileStream("/proc/net/dev", FileMode.Open, FileAccess.Read);
        private const int BuffLen = 512;

        private readonly byte[] buffer = new byte[BuffLen];

        private ulong total;
        private readonly List<ulong> valuesList = new List<ulong>(2);

        private readonly byte counterType = 0;
        #endregion

        enum ReaderType
        {
            BytesInReader = 0,
            BytesOutReader = 8,
        }

        #region .ctr
        private LinuxBandwidthValuesReader(ReaderType type)
        {
            this.counterType = (byte)type;

            if (!ReadValues(file, buffer, valuesList, this.counterType))
            {
                return;
            }

            this.UpdateTotalReceivedAndTransmited();
        }

        #endregion

        #region Publics

        public static LinuxBandwidthValuesReader CreateBytesInReader()
        { 
            return new LinuxBandwidthValuesReader(ReaderType.BytesInReader);
        }

        public static LinuxBandwidthValuesReader CreateBytesOutReader()
        {
            return new LinuxBandwidthValuesReader(ReaderType.BytesOutReader);
        }

        public float ReadValue()
        {
            var old_total = this.total;

            if (!ReadValues(this.file, this.buffer, this.valuesList, this.counterType))
            {
                return 0.0f;
            }

            this.UpdateTotalReceivedAndTransmited();

            //log.Warn($"Current bandwidth from {(ReaderType)this.counterType} is {(this.total - old_total)/1024.0} kB");

            return this.total - old_total;
        }
        #endregion       

        #region Methods
        private static bool ReadValues(Stream file, byte[] buffer, List<ulong> values, byte counterType)
        {
            values.Clear();
            int buffLen = buffer.Length;
            file.Seek(0, SeekOrigin.Begin);
            if (file.Read(buffer, 0, buffLen) <= 0)
            {
                log.Warn("Failed to get data from system file");
                return false;
            }
            var str = Encoding.ASCII.GetString(buffer);
            // we skip first two lines
            var index = str.IndexOf('\n');
            if (index == -1)
            {
                return false;
            }

            index = str.IndexOf('\n', index + 1);
            if (index == -1)
            {
                return false;
            }

            return GetValuesFromString(str, index, values, counterType);
        }

        private static bool GetValuesFromString(string str, int index, List<ulong> values, byte counterType)
        {
            // take names of interfaces and stats for them
            var colonIndex = str.IndexOf(':', index + 1);
            int endOfLine = index;
            while (colonIndex != -1)
            {
                var interfaceName = str.Substring(endOfLine + 1, colonIndex - endOfLine - 1).Trim();
                if (interfaceName == "lo")
                {
                    colonIndex = str.IndexOf(':', colonIndex + 1);
                    continue;
                }

                endOfLine = str.IndexOf('\n', colonIndex + 1);
                if (endOfLine == -1)
                {
                    return false;
                }
                string line = str.Substring(colonIndex + 1, endOfLine - colonIndex - 1);

                string[] numbers = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                values.Add(ulong.Parse(numbers[counterType]));

                colonIndex = str.IndexOf(':', colonIndex + 1);
            }
            return true;
        }

        private void UpdateTotalReceivedAndTransmited()
        {
            this.total = 0;
            for (int i = 0; i < this.valuesList.Count; ++i)
            {
                this.total += this.valuesList[i];
            }
        }

        #endregion    
    }

}
