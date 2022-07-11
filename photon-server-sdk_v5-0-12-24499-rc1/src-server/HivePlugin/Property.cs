// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Property.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.Hive.Plugin
{
    using System;

    /// <summary>
    /// The property.
    /// </summary>
    /// <typeparam name="TKey">
    /// The property key type.
    /// </typeparam>
    [Serializable]
    public class Property<TKey>
    {
        #region Constants and Fields

        /// <summary>
        /// The value.
        /// </summary>
        private object value;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Property{TKey}"/> class.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="value">
        /// The value.
        /// </param>
        public Property(TKey key, object value)
        :this(key, value, 0, 0)
        {
        }

        public Property(TKey key, object value, int keySize, int valueSize)
        {
            this.Key = key;
            this.Value = value;
            this.KeySize = keySize;
            this.ValueSize = valueSize;
        }

        #endregion

        #region Events

        /// <summary>
        /// The property changed.
        /// </summary>
        public event EventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets Key.
        /// </summary>
        public TKey Key { get; private set; }

        /// <summary>
        /// Gets or sets Value.
        /// </summary>
        public object Value
        {
            get
            {
                return this.value;
            }

            set
            {
                if (this.value != value)
                {
                    this.value = value;
                    this.RaisePropertyChanged();
                }
            }
        }

        public int KeySize { get; private set; }

        public int ValueSize { get; set; }

        public int TotalSize => this.KeySize + this.ValueSize;
        #endregion

        #region Methods

        /// <summary>
        /// Invokes the <see cref="PropertyChanged"/> event. 
        /// </summary>
        private void RaisePropertyChanged()
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        #endregion
    }
}