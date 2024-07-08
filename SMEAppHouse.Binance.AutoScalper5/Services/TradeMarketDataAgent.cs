using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.Enumerations;
using SMEAppHouse.Binance.AutoScalper5.Helpers;
using SMEAppHouse.Binance.AutoScalper5.Models;
using SMEAppHouse.Core.CodeKits.Tools;
using SMEAppHouse.Core.TopshelfAdapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace SMEAppHouse.Binance.AutoScalper5.Services
{
    public class TradeMarketDataAgent : TopshelfSocketBase<TradeMarketDataAgent>
    {
        private readonly TradePairSetting _tradePairSetting;

        public TradeMarketDataAgent(TradePairSetting tradePairSetting)
            : base(TimeSpan.FromSeconds(1), null, true, true)
        {
            _tradePairSetting = tradePairSetting;
        }

        protected override void ServiceActionCallback()
        {
            if (DateTime.Now.Second == _tradePairSetting.TradeTimeFrameInSeconds)
            {
                var tradeBrokerThread = new Thread(new ThreadStart(FetchTradingData));
                tradeBrokerThread.Start();
            }

            //OnTickCallback?.Invoke(this, new TickCallbackEventArgs(_traderBrokerSettings.TradePair, DateTime.Now));
        }

        protected override void ServiceInitializeCallback()
        {
            //throw new NotImplementedException();
        }

        protected override void ServiceTerminateCallback()
        {
            //throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        private void FetchTradingData() {
            // get data from Binance
            var retryTimeOut = new TimeSpan(0, 0, 0, 1);
            var kLines = RetryCodeKit.Do(() => GetTradingData(), retryTimeOut, 3, true);
            if (kLines == null)
                return;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="omitCurrentTime"></param>
        /// <param name="samplesCount"></param>
        /// <returns></returns>
        private IEnumerable<KLine> GetTradingData(bool omitCurrentTime = false)
        {
            try
            {
                var publicRestClient = new BinanceClient();

                // check kline interval if integer?
                var test = KlineInterval.Day1;

                var bKLines = publicRestClient
                                    .KLines(_tradePairSetting.TradePairString, 
                                            _tradePairSetting.KLineInterval, 
                                            _tradePairSetting.TradeSamplesMinimum + _tradePairSetting.TradeSamplesSize)
                                    .Result;

                var kLinesCleared = bKLines.ToList();
                if (omitCurrentTime)
                {
                    var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var lastMinuteKline = kLinesCleared
                                                .FirstOrDefault(p => closingStartTime.AddMilliseconds(p.CloseTime).ToLocalTime().Minute == DateTime.Now.Minute);
                    if (lastMinuteKline != null)
                        kLinesCleared.Remove(lastMinuteKline);
                }

                return kLinesCleared.ToKLines();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

    }

}
