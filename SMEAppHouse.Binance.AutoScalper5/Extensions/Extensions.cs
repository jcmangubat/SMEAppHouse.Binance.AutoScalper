using M3C.Finance.BinanceSdk.ResponseObjects;
using SMEAppHouse.Binance.AutoScalper5.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMEAppHouse.Binance.AutoScalper5.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="binanceKLines"></param>
        /// <returns></returns>
        public static IEnumerable<KLine> ToKLines(this IEnumerable<KLinesResponseItem> binanceKLines)
        {
            return binanceKLines.Select(kl => new KLine
            {
                UnixClosingDateTime = kl.CloseTime,
                Open = kl.Open,
                Close = kl.Close,
                Low = kl.Low,
                High = kl.High,
                Volume = kl.Volume
            }); ;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bKLine"></param>
        /// <returns></returns>
        public static DateTime ClosingDateTime(this KLinesResponseItem bKLine)
        {
            var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return closingStartTime.AddMilliseconds(bKLine.CloseTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="unixTime"></param>
        /// <returns></returns>
        public static DateTime AsDateTime(long unixTime)
        {
            var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return closingStartTime.AddMilliseconds(unixTime);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="flags"></param>
        /// <param name="charIndicators"></param>
        /// <returns></returns>
        public static string ToPatternString(this bool?[] pattern)
        {
            var indicators = new[] { '-', '/', '\\' }; // up, down, equals
            var patternStr = new StringBuilder();
            for (var ctr = 0; ctr < pattern.Length; ctr++)
            {
                var idx = !pattern[ctr].HasValue ? 0 :
                            pattern[ctr].Value ? 1 : 2;

                patternStr.Append(indicators[idx]);
            }
            return patternStr.ToString();
        }
    }
}
