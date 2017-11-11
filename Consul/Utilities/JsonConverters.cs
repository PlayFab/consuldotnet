// -----------------------------------------------------------------------
//  <copyright file="JsonConverters.cs" company="PlayFab Inc">
//    Copyright 2015 PlayFab Inc.
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using System.Globalization;

namespace Consul
{
    public class Rfc3339DateTimeConverter : JsonConverter
    {
        private const string Rfc3339DateTimePattern1 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffffK";
        private const string Rfc3339DateTimePattern2 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffffK";
        private const string Rfc3339DateTimePattern3 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffffK";
        private const string Rfc3339DateTimePattern4 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffffK";
        private const string Rfc3339DateTimePattern5 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK";
        private const string Rfc3339DateTimePattern6 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'ffK";
        private const string Rfc3339DateTimePattern7 = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fK";

        private static readonly string[] Formats =
        {
            Rfc3339DateTimePattern1, Rfc3339DateTimePattern2, Rfc3339DateTimePattern3, Rfc3339DateTimePattern4,
            Rfc3339DateTimePattern5, Rfc3339DateTimePattern6, Rfc3339DateTimePattern7
        };
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer,
                ((DateTime) value).ToString(Rfc3339DateTimePattern1, DateTimeFormatInfo.InvariantInfo), typeof(string));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var value = (string) serializer.Deserialize(reader, typeof(string));
            
            foreach (var format in Formats)
            {
                if (DateTime.TryParseExact(value, format, DateTimeFormatInfo.InvariantInfo, DateTimeStyles.AssumeLocal,
                    out var result))
                {
                    return result;
                }
            }

            throw new FormatException(string.Format(CultureInfo.InvariantCulture,
                "{0} is not a valid RFC 3339 string representation of a date and time.", value));
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(DateTime))
            {
                return true;
            }
            return false;
        }
    }
    
    public class NanoSecTimespanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, (long)((TimeSpan)value).TotalMilliseconds * 1000000, typeof(long));
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            return Extensions.FromGoDuration((string)serializer.Deserialize(reader, typeof(string)));
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(TimeSpan))
            {
                return true;
            }
            return false;
        }
    }

    public class DurationTimespanConverter : JsonConverter
    {
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, ((TimeSpan)value).ToGoDuration());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            return Extensions.FromGoDuration((string)serializer.Deserialize(reader, typeof(string)));
        }

        public override bool CanConvert(Type objectType)
        {
            if (objectType == typeof(TimeSpan))
            {
                return true;
            }
            return false;
        }
    }
}