using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
            var outDuration = new StringBuilder();
            if (ts.TotalSeconds < 1)
            {
                outDuration.Append(ts.TotalMilliseconds.ToString("#ms"));
            }
            else
            {
                if ((int)ts.TotalHours > 0)
                {
                    outDuration.Append(ts.TotalHours.ToString("#h"));
                }
                if ((int)ts.TotalMinutes > 0)
                {
                    outDuration.Append(ts.Minutes.ToString("#m"));
                }
                if ((int)ts.TotalSeconds > 0)
                {
                    outDuration.Append(ts.Seconds.ToString("#"));
                }

                if (ts.Milliseconds > 0)
                {
                    outDuration.Append(".");
                    outDuration.Append(ts.Milliseconds.ToString("#"));
                }
                if (ts.Seconds > 0)
                {
                    outDuration.Append("s");
                }
            }
            return outDuration.ToString();
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
