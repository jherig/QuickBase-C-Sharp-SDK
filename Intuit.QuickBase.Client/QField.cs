﻿/*
 * Copyright © 2010 Intuit Inc. All rights reserved.
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.opensource.org/licenses/eclipse-1.0.php
 */
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using Intuit.QuickBase.Core;

namespace Intuit.QuickBase.Client
{
    internal class QField
    {
        private static readonly DateTime qbOffset = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        // Instance fields
        private object _value;

        internal QField(int fieldId)
            : this(fieldId, null, FieldType.empty, null, null)
        {
        }

        // Constructors
        internal QField(int fieldId, object value, FieldType type, IQRecord record, IQColumn column) : this(fieldId, value, type, record, column, false)
        {
        }

        internal QField(int fieldId, object value, FieldType type, IQRecord record, IQColumn column,  bool QBinternal)
        {
            FieldId = fieldId;
            Type = type;
            Record = record; // needs to be before Value.
            Column = column;
            if (QBinternal)
            {
                QBValue = (string)value;
            }
            else
            {
                Value = value;
            }
        }

        // Properties
        internal int FieldId { get; private set; }

        internal string QBValue
        {
            get
            {
                if (_value == null) return String.Empty;
                switch (Type)
                {
                    case FieldType.address:
                        return string.Empty;
                    case FieldType.timestamp:
                    case FieldType.date:
                        return ConvertDateTimeToQBMilliseconds((DateTime)_value);
                    case FieldType.timeofday:
                        return ConvertTimeSpanToQBMilliseconds((TimeSpan)_value);
                    case FieldType.duration:
                        return ((TimeSpan)_value).Milliseconds.ToString();
                    case FieldType.checkbox:
                        return (bool)_value == true ? "1" : "0";
                    case FieldType.percent:
                        return Math.Round((float) _value * 100.0, 6).ToString(); //Get around roundtrip bug in Quickbase
                    default:
                        return _value.ToString();
                }
            }
            set
            {
                switch (Type)
                {
                    case FieldType.address:
                        // do nothing: child columns will fill this out
                        break;
                    case FieldType.date:
                        _value = String.IsNullOrEmpty(value) ? new DateTime?() : ConvertQBMillisecondsToDateTime(value).Date;
                        break;
                    case FieldType.timeofday:
                        _value = String.IsNullOrEmpty(value) ? new TimeSpan?() : ConvertQBMillisecondsToDateTime(value).TimeOfDay;
                        break;
                    case FieldType.duration:
                        _value = String.IsNullOrEmpty(value) ? new TimeSpan?() : TimeSpan.FromMilliseconds(Int64.Parse(value));
                        break;
                    case FieldType.timestamp:
                        _value = String.IsNullOrEmpty(value) ? new DateTime?() : ConvertQBMillisecondsToDateTime(value);
                        break;
                    case FieldType.checkbox:
                        _value = String.IsNullOrEmpty(value) ? new bool?() : (value == "1" || value == "true");
                        break;
                    case FieldType.percent:
                    case FieldType.rating:
                        _value = String.IsNullOrEmpty(value) ? new float?() : float.Parse(value);
                        break;
                    case FieldType.@float:
                    case FieldType.currency:
                        _value = String.IsNullOrEmpty(value) ? new decimal?() : decimal.Parse(value);
                        break;
                    default:
                        _value = value;
                        break;
                }
            }
        }

        internal object Value
        {
            get
            {
                switch (Type)
                {
                    case FieldType.address:
                        Dictionary<string, int> colDict = ((IQColumn_int) Column).GetComposites();
                        return new QAddress(
                            (string)Record[colDict["street"]],
                            (string)Record[colDict["street2"]],
                            (string)Record[colDict["city"]],
                            (string)Record[colDict["region"]],
                            (string)Record[colDict["postal"]],
                            (string)Record[colDict["country"]]
                            );
                    default:
                        return _value;
                }
            }
            set
            {
                if (value == null)
                {
                    if (_value != null)
                    {
                        _value = null;
                        Update = true;
                    }
                }
                else
                {
                    if (_value == null || !_value.Equals(value))
                    {
                        Update = true;
                        switch (Type)
                        {
                            case FieldType.address:
                                if (value.GetType() != typeof (QAddress))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                Dictionary<string, int> colDict = ((IQColumn_int) Column).GetComposites();
                                Record[colDict["street"]] = ((QAddress) value).Line1;
                                Record[colDict["street2"]] = ((QAddress) value).Line2;
                                Record[colDict["city"]] = ((QAddress) value).City;
                                Record[colDict["region"]] = ((QAddress) value).Province;
                                Record[colDict["postal"]] = ((QAddress) value).PostalCode;
                                Record[colDict["country"]] = ((QAddress) value).Country;
                                break;
                            case FieldType.rating:
                                if (value.GetType() != typeof(float) || (float) value < 0 || (float) value > 5)
                                    throw new ArgumentException("Invalid value for 'rating' fieldtype");
                                _value = value;
                                break;
                            case FieldType.date:
                                if (value.GetType() != typeof(DateTime))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                _value = ((DateTime) value).Date;
                                break;
                            case FieldType.timestamp:
                                if (value.GetType() != typeof(DateTime))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                _value = value;
                                break;
                            case FieldType.duration:
                            case FieldType.timeofday:
                                if (value.GetType() != typeof(TimeSpan))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                _value = value;
                                break;
                            case FieldType.@float:
                            case FieldType.currency:
                                decimal? val = value as decimal?;
                                Int32? val2 = value as Int32?;
                                if (val == null && val2 == null)
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                if (val.HasValue)
                                    _value = val.Value;
                                else
                                    _value = val2.Value;
                                break;
                            case FieldType.checkbox:
                                if (value.GetType() != typeof(bool))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                _value = value;
                                break;
                            default:
                                if (value.GetType() != typeof(string))
                                    throw new ArgumentException("Can't supply type of " + value.GetType() + " to a " +
                                                                this.Type.ToString() + " field.");
                                _value = value;
                                break;
                        }
                    }
                }
            }
        }
        internal FieldType Type { get; set; }
        internal IQRecord Record { get; set; }
        internal IQColumn Column { get; set; }
        internal string FullName { get; set; }
        internal bool Update { get; set; }

        public bool Equals(QField other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.FieldId == FieldId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (QField)) return false;
            return Equals((QField) obj);
        }

        public override int GetHashCode()
        {
            return FieldId;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        private static DateTime ConvertQBMillisecondsToDateTime(string milliseconds)
        {
            return new DateTime(qbOffset.Ticks + (Int64.Parse(milliseconds) * TimeSpan.TicksPerMillisecond));
        }

        private static string ConvertDateTimeToQBMilliseconds(DateTime inDate)
        {
            return ((inDate.Ticks - qbOffset.Ticks)/TimeSpan.TicksPerMillisecond).ToString();
        }

        private static string ConvertTimeSpanToQBMilliseconds(TimeSpan inTime)
        {
            return (inTime.Ticks / TimeSpan.TicksPerMillisecond).ToString();
        }
    }
}
