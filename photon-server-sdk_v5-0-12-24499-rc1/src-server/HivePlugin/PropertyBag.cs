// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PropertyBag.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Photon.Hive.Plugin
{
    /// <summary>
    /// The property bag.
    /// </summary>
    /// <typeparam name="TKey">
    /// The property key type
    /// </typeparam>
    [Serializable]
    public class PropertyBag<TKey>
    {
        #region Constants and Fields

        /// <summary>
        /// The dictionary.
        /// </summary>
        private readonly Dictionary<TKey, Property<TKey>> dictionary;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBag{TKey}"/> class.
        /// </summary>
        public PropertyBag()
        {
            this.dictionary = new Dictionary<TKey, Property<TKey>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBag{TKey}"/> class.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        public PropertyBag(IEnumerable<KeyValuePair<TKey, object>> values)
            : this()
        {
            foreach (KeyValuePair<TKey, object> item in values)
            {
                this.Set(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyBag{TKey}"/> class.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        public PropertyBag(IDictionary values)
            : this()
        {
            foreach (TKey key in values.Keys)
            {
                this.Set(key, values[key]);
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// The property changed event.
        /// </summary>
        public event EventHandler<PropertyChangedEventArgs<TKey>> PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the number of properties in this instance.
        /// </summary>
        public int Count => this.dictionary.Count;

        public bool DeleteNullProps { get; set; }
        public int TotalSize { get; private set; }
        #endregion

        #region Public Methods

        public IDictionary<TKey, Property<TKey>> AsDictionary()
        {
            return this.dictionary;
        }

        /// <summary>
        /// The get all.
        /// </summary>
        /// <returns>
        /// A list of all properties
        /// </returns>
        public IList<Property<TKey>> GetAll()
        {
            var properties = new Property<TKey>[this.dictionary.Count];
            this.dictionary.Values.CopyTo(properties, 0);
            return properties;
        }

        /// <summary>
        /// Get all properties.
        /// </summary>
        /// <returns>
        /// A copy of all properties with keys
        /// </returns>
        public Hashtable GetProperties()
        {
            var result = new Hashtable(this.dictionary.Count);
            this.CopyPropertiesToHashtable(result);
            return result;
        }

        /// <summary>
        /// The get properties.
        /// </summary>
        /// <param name="propertyKeys">
        /// The property keys.
        /// </param>
        /// <returns>
        /// The values for the given <paramref name="propertyKeys"/>
        /// </returns>
        public Hashtable GetProperties(IList<TKey> propertyKeys)
        {
            if (propertyKeys == null)
            {
                return this.GetProperties();
            }

            var result = new Hashtable(propertyKeys.Count);
            this.CopyPropertiesToHashtable(result, propertyKeys);
            return result;
        }

        /// <summary>
        /// The get properties.
        /// </summary>
        /// <param name="propertyKeys">
        /// The property keys.
        /// </param>
        /// <returns>
        /// The values for the given <paramref name="propertyKeys"/>
        /// </returns>
        public Hashtable GetProperties(IEnumerable<TKey> propertyKeys)
        {
            if (propertyKeys == null)
            {
                return this.GetProperties();
            }

            var result = new Hashtable();
            this.CopyPropertiesToHashtable(result, propertyKeys);
            return result;
        }

        /// <summary>
        /// The get properties.
        /// </summary>
        /// <param name="propertyKeys">
        /// The property keys.
        /// </param>
        /// <returns>
        /// The values for the given <paramref name="propertyKeys"/>
        /// </returns>
        public Hashtable GetProperties(IEnumerable propertyKeys)
        {
            if (propertyKeys == null)
            {
                return this.GetProperties();
            }

            var result = new Hashtable();
            this.CopyPropertiesToHashtable(result, propertyKeys);
            return result;
        }

        /// <summary>
        /// The get property.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <returns>
        /// The value for the <paramref name="key"/>.
        /// </returns>
        public Property<TKey> GetProperty(TKey key)
        {
            this.dictionary.TryGetValue(key, out var value);
            return value;
        }

        public bool SetProperty(TKey key, object value)
        {
            return this.SetProperty(key, value, null);
        }

        /// <summary>
        /// tries to set property and returns whether it was changed
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="value">
        /// The value.
        /// </param>
        /// <param name="keyValueSizes"></param>
        public bool SetProperty(TKey key, object value, KeyValuePair<int, int>? keyValueSizes)
        {
            if (this.DeleteNullProps && value == null)
            {
                if (this.RemoveProperty(key, out var p))
                { 
                    this.TotalSize -= p.TotalSize;
                    return true;
                }
                return false;
            }

            if (this.dictionary.TryGetValue(key, out var property))
            {
                if (!PropertyValueComparer.Compare(property.Value, value))
                {
                    property.Value = value;
                    this.TotalSize -= property.ValueSize;
                    this.TotalSize += keyValueSizes?.Value ?? 0;
                    property.ValueSize = keyValueSizes?.Value ?? 0;
                    return true;
                }
                return false;
            }

            int keySize = 0;
            int valueSize = 0;
            if (keyValueSizes.HasValue)
            {
                keySize = keyValueSizes.Value.Key;
                valueSize = keyValueSizes.Value.Value;
            }

            property = new Property<TKey>(key, value, keySize, valueSize);

            this.TotalSize += property.TotalSize;

            property.PropertyChanged += this.OnPropertyPropertyChanged;
            this.dictionary.Add(key, property);
            this.RaisePropertyChanged(key, value);

            return true;
        }

        public void Set(TKey key, object value)
        {
            this.Set(key, value, null);
        }

        /// <summary>
        /// tries to set property
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <param name="keyValueSizes"></param>
        public void Set(TKey key, object value, KeyValuePair<int, int>? keyValueSizes)
        {
            if (this.DeleteNullProps && value == null)
            {
                if (this.RemoveProperty(key, out var p))
                {
                    this.TotalSize -= p.TotalSize;
                }
                return;
            }

            if (this.dictionary.TryGetValue(key, out var property))
            {
                property.Value = value;
                this.TotalSize -= property.ValueSize;
                this.TotalSize += keyValueSizes?.Value ?? 0;
                property.ValueSize = keyValueSizes?.Value ?? 0;
                return;
            }

            int keySize = 0;
            int valueSize = 0;
            if (keyValueSizes.HasValue)
            {
                keySize = keyValueSizes.Value.Key;
                valueSize = keyValueSizes.Value.Value;
            }

            property = new Property<TKey>(key, value, keySize, valueSize);

            property.PropertyChanged += this.OnPropertyPropertyChanged;
            this.dictionary.Add(key, property);
            this.RaisePropertyChanged(key, value);
        }


        /// <summary>
        /// The set properties.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        public void SetProperties(IDictionary values)
        {
            this.SetProperties(values, out _);
        }

        public void SetProperties(IDictionary values, out bool changed)
        {
            this.SetProperties(values, out changed, null);
        }

        public void SetProperties(IDictionary values, out bool changed, Dictionary<object, KeyValuePair<int, int>> metaData)
        {
            changed = false;
            foreach (TKey key in values.Keys)
            {
                var meta = new KeyValuePair<int, int>(0, 0);
                metaData?.TryGetValue(key, out meta);
                changed |= this.SetProperty(key, values[key], meta);
            }
        }

        /// <summary>
        /// The set properties.
        /// </summary>
        /// <param name="values">
        ///     The values.
        /// </param>
        /// <param name="expectedValues">
        ///     The expected values for properties in order to apply CAS.
        /// </param>
        /// <param name="debugMessage"></param>
        public bool SetPropertiesCAS(IDictionary values, IDictionary expectedValues, out string debugMessage)
        {
            debugMessage = string.Empty;
            if (expectedValues == null || expectedValues.Count == 0)
            {
                this.SetProperties(values, out _);
                return true;
            }

            if (!this.CompareProperties(expectedValues, out debugMessage))
            {
                return false;
            }

            this.SetProperties(values, out _);
            return true;
        }

        public bool CompareProperties(IDictionary expectedValues, out string debugMessage)
        {
            if (expectedValues == null || expectedValues.Count == 0)
            {
                debugMessage = string.Empty;
                return true;
            }

            foreach (DictionaryEntry expectedValue in expectedValues)
            {
                var property = this.GetProperty((TKey)expectedValue.Key);
                if (property == null && 
                    expectedValue.Value != null || 
                    property != null && 
                    !PropertyValueComparer.Compare(property.Value, expectedValue.Value))
                {
                    MakeCASDebugMessage(out debugMessage, property, new KeyValuePair<TKey, object>((TKey)expectedValue.Key, expectedValue.Value));
                    return false;
                }
            }


            debugMessage = string.Empty;
            return true;
        }

        public bool SetPropertiesCAS(IDictionary values, IDictionary expectedValues, ref bool valuesChanged, out string debugMessage)
        {
            return this.SetPropertiesCAS(values, expectedValues, ref valuesChanged, out debugMessage, null);
        }

        /// <summary>
        /// The set properties.
        /// </summary>
        /// <param name="values">
        ///     The values.
        /// </param>
        /// <param name="expectedValues">
        ///     The expected values for properties in order to apply CAS.
        /// </param>
        /// <param name="valuesChanged">informs whether values were really changed</param>
        /// <param name="debugMessage"></param>
        /// <param name="metaData"></param>
        public bool SetPropertiesCAS(IDictionary values, IDictionary expectedValues, ref bool valuesChanged, out string debugMessage, Dictionary<object, KeyValuePair<int, int>> metaData)
        {
            debugMessage = string.Empty;
            bool changed;
            if (expectedValues == null || expectedValues.Count == 0)
            {
                this.SetProperties(values, out changed, metaData);
                valuesChanged |= changed;
                return true;
            }

            if (!this.CompareProperties(expectedValues, out debugMessage))
            {
                return false;
            }

            this.SetProperties(values, out changed, metaData);
            valuesChanged |= changed;
            return true;
        }

        /// <summary>
        /// The set properties.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        public void SetProperties(IDictionary<TKey, object> values)
        {
            this.SetProperties(values, out _);
        }

        public void SetProperties(IDictionary<TKey, object> values, out bool changed)
        {
            changed = false;
            foreach (var keyValue in values)
            {
                changed |= this.SetProperty(keyValue.Key, keyValue.Value);
            }
        }

        /// <summary>
        /// The set properties.
        /// </summary>
        /// <param name="values">
        /// The values.
        /// </param>
        /// <param name="expectedValues">
        /// expected values for properties, which we are going to change
        /// </param>
        /// <param name="debugMessage"></param>
        public bool SetPropertiesCAS(IDictionary<TKey, object> values, IDictionary<TKey, object> expectedValues, out string debugMessage)
        {
            debugMessage = string.Empty;
            bool changed;
            if (expectedValues == null || expectedValues.Count == 0)
            {
                this.SetProperties(values, out changed);
                return changed;
            }

            foreach (var expectedValue in expectedValues)
            {
                var property = this.GetProperty(expectedValue.Key);
                if (property == null || !PropertyValueComparer.Compare(property.Value, expectedValue.Value))
                {
                    MakeCASDebugMessage(out debugMessage, property, expectedValue);
                    return false;
                }
            }

            this.SetProperties(values, out changed);
            return changed;
        }

        private static void MakeCASDebugMessage(out string debugMessage, Property<TKey> property, KeyValuePair<TKey, object> expectedValue)
        {
            debugMessage = "CAS update failed on server";
        }

        public bool TryGetValue(TKey key, out object value)
        {
            if (this.dictionary.TryGetValue(key, out var property))
            {
                value = property.Value;
                return true;
            }

            value = null;
            return false;
        }

        public void Clear()
        {
            this.dictionary.Clear();
            this.TotalSize = 0;
        }

#endregion

#region Methods

        /// <summary>
        /// The copy properties to hashtable.
        /// </summary>
        /// <param name="hashtable">
        /// The hashtable.
        /// </param>
        private void CopyPropertiesToHashtable(IDictionary hashtable)
        {
            foreach (KeyValuePair<TKey, Property<TKey>> keyValue in this.dictionary)
            {
                hashtable.Add(keyValue.Key, keyValue.Value.Value);
            }
        }

        /// <summary>
        /// The copy properties to hashtable.
        /// </summary>
        /// <param name="hashtable">
        /// The hashtable.
        /// </param>
        /// <param name="propertyKeys">
        /// The property keys.
        /// </param>
        private void CopyPropertiesToHashtable(IDictionary hashtable, IEnumerable<TKey> propertyKeys)
        {
            foreach (TKey key in propertyKeys)
            {
                if (!hashtable.Contains(key))
                {
                    if (this.dictionary.TryGetValue(key, out Property<TKey> property))
                    {
                        hashtable.Add(key, property.Value);
                    }
                }
            }
        }

        /// <summary>
        /// The copy properties to hashtable.
        /// </summary>
        /// <param name="hashtable">
        /// The hashtable.
        /// </param>
        /// <param name="propertyKeys">
        /// The property keys.
        /// </param>
        private void CopyPropertiesToHashtable(IDictionary hashtable, IEnumerable propertyKeys)
        {
            foreach (TKey key in propertyKeys)
            {
                if (!hashtable.Contains(key))
                {
                    if (this.dictionary.TryGetValue(key, out Property<TKey> property))
                    {
                        hashtable.Add(key, property.Value);
                    }
                }
            }
        }

        /// <summary>
        /// The on property property changed.
        /// </summary>
        /// <param name="sender">
        /// The sender.
        /// </param>
        /// <param name="e">
        /// The e.
        /// </param>
        private void OnPropertyPropertyChanged(object sender, EventArgs e)
        {
            var property = (Property<TKey>)sender;
            this.RaisePropertyChanged(property.Key, property.Value);
        }

        /// <summary>
        /// The raise property changed.
        /// </summary>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="value">
        /// The value.
        /// </param>
        private void RaisePropertyChanged(TKey key, object value)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs<TKey>(key, value));
            }
        }

        private bool RemoveProperty(TKey key, out Property<TKey> p)
        {
#if NETFRAMEWORK
            if (this.dictionary.TryGetValue(key, out p))
            {
                return this.dictionary.Remove(key);
            }
            return false;
#else
            return this.dictionary.Remove(key, out p);
#endif
        }

        #endregion
    }
}