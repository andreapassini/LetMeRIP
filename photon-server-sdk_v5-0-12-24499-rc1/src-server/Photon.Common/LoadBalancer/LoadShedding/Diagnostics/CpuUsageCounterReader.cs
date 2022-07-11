// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CpuUsageCounterReader.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   The process cpu usage counter.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Common.LoadBalancer.LoadShedding.Diagnostics
{
    using System;
    using System.Linq;
    using ExitGames.Diagnostics.Counter;

    /// <summary>
    ///   The process cpu usage counter.
    /// </summary>
    public sealed class CpuUsageCounterReader : ICounter
    {
#region Constants and Fields

        /// <summary>
        ///   The windows performance counter field.
        /// </summary>
        private readonly ICounter counterImpl;

        private readonly ValueHistory values;

        #endregion

        #region Constructors and Destructors
        public CpuUsageCounterReader(int averageCapacity)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                {
                    this.counterImpl = new PerformanceCounterReader("Processor Information", "% Processor Time", "_Total"); 
                    break;
                }
                case PlatformID.Unix:
                {
                    this.counterImpl = new Linux.LinuxCPUReader();
                    break;
                }
                default:
                {
                    throw new PlatformNotSupportedException($"There is not counters to read CPU on {Environment.OSVersion.Platform}");
                }
            }
            this.values = new ValueHistory(averageCapacity);
        }
        #endregion

        #region Properties

        /// <summary>
        ///   Gets CounterType.
        /// </summary>
        public CounterType CounterType => this.counterImpl.CounterType;

        /// <summary>
        ///   Gets Name.
        /// </summary>
        public string Name => this.counterImpl.Name;

        #endregion

        #region Implemented Interfaces

#region ICounter
        public bool IsValid => this.counterImpl.IsValid;
        /// <summary>
        ///   This method is not supported.
        /// </summary>
        /// <returns>
        ///   Nothing. Throws a <see cref = "NotSupportedException" />.
        /// </returns>
        /// <exception cref = "NotSupportedException">
        ///   This is a read only counter.
        /// </exception>
        public long Decrement()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   Gets the next value.
        /// </summary>
        /// <returns>
        ///   The next value.
        /// </returns>
        public float GetNextValue()
        {
            return this.counterImpl.GetNextValue();
        }

        /// <summary>
        ///   This method is not supported.
        /// </summary>
        /// <returns>
        ///   Nothing. Throws a <see cref = "NotSupportedException" />.
        /// </returns>
        /// <exception cref = "NotSupportedException">
        ///   This is a read only counter.
        /// </exception>
        public long Increment()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        ///   This method is not supported.
        /// </summary>
        /// <param name = "value">
        ///   The value to increment by.
        /// </param>
        /// <returns>
        ///   Nothing. Throws a <see cref = "NotSupportedException" />.
        /// </returns>
        /// <exception cref = "NotSupportedException">
        ///   This is a read only counter.
        /// </exception>
        public long IncrementBy(long value)
        {
            throw new NotSupportedException();
        }

        #endregion

        #endregion

        #region Methods
        public double GetNextAverage()
        {
            float value = this.GetNextValue();
            this.values.Add((int)value);
            return this.values.Average();
        }
        #endregion
    }
}
