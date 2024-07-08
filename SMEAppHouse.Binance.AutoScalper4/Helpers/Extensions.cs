using M3C.Finance.BinanceSdk.ResponseObjects;
using SMEAppHouse.Binance.AutoScalper4.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SMEAppHouse.Binance.AutoScalper4.Helpers
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
        /// <param name="kLines"></param>
        /// <returns></returns>
        public static IEnumerable<TradeData> ProduceDataSeries(this IEnumerable<KLine> kLines, bool copyToClipboard = false)
        {
            if (kLines.Count() <= 60)
                return null;

            var tradingDataEnvelopes = kLines.Select(kl => kl.ToTradingData()).ToList();

            tradingDataEnvelopes = tradingDataEnvelopes.ComputeMACDs().ToList();
            tradingDataEnvelopes = tradingDataEnvelopes.ComputeVWAPs().ToList();
            tradingDataEnvelopes = tradingDataEnvelopes.ComputeAwesomeOscillator().ToList();

            if (copyToClipboard)
            {
                /*var samplesData = string.Join("\r", tradingDataEnvelopes.Select(p => $"{p.ClosingDateTime}\t{p.High}\t{p.Low}\t{p.Open}\t{p.Close}\t{p.Volume}").ToArray());*/
                var samplesData = string.Join("\r", tradingDataEnvelopes.Select(p => $"{p.ClosingDateTime}\t{p.Close}").ToArray());
                TextCopy.ClipboardService.SetText(samplesData);
            }

            tradingDataEnvelopes = tradingDataEnvelopes.Skip(60).ToList();

            return tradingDataEnvelopes;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeData"></param>
        /// <returns></returns>
        public static IEnumerable<TradeData> ComputeMACDs(this IEnumerable<TradeData> tradeData)
        {
            var data = tradeData.ToList();
            var ema12Mult = decimal.Divide(2, 12 + 1);
            var ema26Mult = decimal.Divide(2, 26 + 1);
            for (var idx = 0; idx < data.Count; idx++)
            {
                if (idx == 11)
                {
                    data[idx].MACD = new MACD();
                    data[idx].MACD.EMA12 = data.Take(12).Average(p => p.Close);
                }

                if (idx > 11)
                    data[idx].MACD.EMA12 = ((data[idx].Close - data[idx - 1].MACD.EMA12) * ema12Mult) + data[idx - 1].MACD.EMA12;

                if (idx == 25)
                    data[idx].MACD.EMA26 = data.Take(26).Average(p => p.Close);

                if (idx > 25)
                {
                    data[idx].MACD.EMA26 = ((data[idx].Close - data[idx - 1].MACD.EMA26) * ema26Mult) + data[idx - 1].MACD.EMA26;
                    data[idx].MACD.MACDValue = data[idx].MACD.EMA12 - data[idx].MACD.EMA26;
                }

                if (idx >= 25 + 8)
                {
                    data[idx].MACD.Signal = data.Skip(idx - 8).Take(9).Average(c => c.MACD.MACDValue);
                    data[idx].MACD.SignalOptimized = data.Skip(idx - 2).Take(6).Average(c => c.MACD.MACDValue);
                    data[idx].MACD.Histogram = data[idx].MACD.MACDValue - data[idx].MACD.Signal;
                }
            }
            return data;
        }

        /// <summary>
        /// https://www.youtube.com/watch?v=ixEDqZaCAdY
        /// </summary>
        /// <param name="tradeData"></param>
        /// <returns></returns>
        public static IEnumerable<TradeData> ComputeVWAPs(this IEnumerable<TradeData> tradeData)
        {
            var data = tradeData.ToList();
            for (var ctr = 0; ctr < data.Count; ctr++)
            {
                if (ctr >= 12)
                {
                    var samples = data.Take(ctr + 1).Reverse().Take(13);
                    data[ctr].VWAP = new VWAP();
                    data[ctr].VWAP.VWAP13 = samples.Sum(p => p.Volume * p.Close) / samples.Sum(p => p.Volume);
                }

                if (ctr >= 19)
                {
                    var samples = data.Take(ctr + 1).Reverse().Take(20);
                    data[ctr].VWAP.VWAP20 = samples.Sum(p => p.Volume * p.Close) / samples.Sum(p => p.Volume);
                }
            }
            return data;
        }

        /// <summary>
        /// https://www.youtube.com/watch?v=aA8KisioyUI
        /// </summary>
        /// <param name="tradeData"></param>
        /// <returns></returns>
        public static IEnumerable<TradeData> ComputeAwesomeOscillator(this IEnumerable<TradeData> tradeData)
        {
            var data = tradeData.ToList();
            for (var ctr = 0; ctr < data.Count; ctr++)
            {
                if (ctr >= 4)
                {
                    var samples = data.Take(ctr + 1).Reverse().Take(5);
                    data[ctr].AO = new AO();
                    data[ctr].AO.SMA5 = samples.Average(p => (p.Low + p.High) / 2);
                }

                if (ctr >= 33)
                {
                    var samples = data.Take(ctr + 1).Reverse().Take(34);
                    data[ctr].AO.SMA34 = samples.Average(p => (p.Low + p.High) / 2);
                    data[ctr].AO.AOValue = data[ctr].AO.SMA5 - data[ctr].AO.SMA34;
                }
            }
            return data;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="macdData"></param>
        public static bool?[] GenerateMACDSignalPattern(this IEnumerable<TradeData> macdData)
        {
            var macds = macdData.ToList();
            var signals = new List<bool?>();
            for (var ctr = 0; ctr < macds.Count; ctr++)
            {
                if (ctr == 0) continue;
                if (macds[ctr].MACD.Signal == macds[ctr - 1].MACD.Signal)
                    signals.Add(null);
                else if (macds[ctr].MACD.Signal > macds[ctr - 1].MACD.Signal)
                    signals.Add(true);
                else if (macds[ctr].MACD.Signal < macds[ctr - 1].MACD.Signal)
                    signals.Add(false);
            }
            return signals.ToArray();
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
