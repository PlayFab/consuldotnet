// -----------------------------------------------------------------------
//  <copyright file="Health.cs" company="PlayFab Inc">
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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Consul
{
    internal static class Extensions
    {
        internal const double Nanosecond = Microsecond / 1000;
        internal const double Microsecond = Millisecond / 1000;
        internal const double Millisecond = 1;
        internal const double Second = 1000 * Millisecond;
        internal const double Minute = 60 * Second;
        internal const double Hour = 60 * Minute;

        internal static readonly Dictionary<string, double> UnitMap = new Dictionary<string, double>()
        {
            {"ns", Nanosecond},
            {"us", Microsecond},
            {"µs", Microsecond}, // U+00B5 , micro symbol
            {"μs", Microsecond}, // U+03BC , Greek letter mu
            {"ms", Millisecond},
            {"s", Second},
            {"m", Minute},
            {"h", Hour}
        };
        internal static string ToGoDuration(this TimeSpan ts)
        {
            if (ts == TimeSpan.Zero)
            {
                return "0";
            }

            if (ts.TotalSeconds < 1)
            {
                return ts.TotalMilliseconds.ToString("#ms");
            }
            else
            {
                var outDuration = new StringBuilder();
                if ((int)ts.TotalHours > 0)
                {
                    outDuration.Append(ts.TotalHours.ToString("#h"));
                }
                if (ts.Minutes > 0)
                {
                    outDuration.Append(ts.ToString("%m'm'"));
                }
                if (ts.Seconds > 0)
                {
                    outDuration.Append(ts.ToString("%s"));
                }
                if (ts.Milliseconds > 0)
                {
                    outDuration.Append(".");
                    outDuration.Append(ts.ToString("fff"));
                }
                if (ts.Seconds > 0)
                {
                    outDuration.Append("s");
                }
                return outDuration.ToString();
            }
        }
        internal static TimeSpan FromGoDuration(string value)
        {
            const string pattern = @"([0-9]*(?:\.[0-9]*)?)([a-z]+)";

            if (string.IsNullOrEmpty(value) || value == "0")
            {
                return TimeSpan.Zero;
            }

            ulong result;
            if (ulong.TryParse(value, out result))
            {
                return TimeSpan.FromTicks((long)(result / 100));
            }

            var matches = Regex.Matches(value, pattern);
            if (matches.Count == 0)
            {
                throw new ArgumentException("Invalid duration", value);
            }
            double time = 0;

            foreach (Match match in matches)
            {
                double res;
                if (double.TryParse(match.Groups[1].Value, out res))
                {
                    if (UnitMap.ContainsKey(match.Groups[2].Value))
                    {
                        time += res * UnitMap[match.Groups[2].Value];
                    }
                    else
                    {
                        throw new ArgumentException("Invalid duration unit", value);
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid duration number", value);
                }
            }

            var span = TimeSpan.FromMilliseconds(time);

            return value[0] == '-' ? span.Negate() : span;
        }
    }
}
