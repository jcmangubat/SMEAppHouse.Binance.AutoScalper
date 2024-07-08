using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.Enumerations;
using M3C.Finance.BinanceSdk.ResponseObjects;
using SMEAppHouse.Binance.AutoScalper3.Helpers;
using SMEAppHouse.Binance.AutoScalper3.Models;
using SMEAppHouse.Core.CodeKits;
using SMEAppHouse.Core.CodeKits.Tools;
using SMEAppHouse.Core.TopshelfAdapter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static SMEAppHouse.Binance.AutoScalper3.Models.Rules;

namespace SMEAppHouse.Binance.AutoScalper3.Worker
{
    public class TradeBrokerService : TopshelfSocketBase<TradeBrokerService>
    {
        private readonly BinanceApiKeys _apiKeys;
        private readonly TradeBrokerSettings _traderBrokerSettings;

        public event TickCallbackEventHandler OnTickCallback;
        public event MACDPatternGeneratedEventHandler OnMACDPatternIdentified;
        public event MACDPatternAnalyzedEventHandler OnMACDPatternAnalyzed;
        public event NewOrderPushedEventHandler OnNewOrderPushed;
        public event NewOrderDeniedEventHandler OnNewOrderDenied;

        #region constructors

        public TradeBrokerService(BinanceApiKeys apiKeys, TradeBrokerSettings traderBrokerSettings)
            : base(TimeSpan.FromSeconds(1), null, true, true)
        {
            _apiKeys = apiKeys;
            _traderBrokerSettings = traderBrokerSettings;
        }

        #endregion

        #region TopshelfSocketBase members

        protected override void ServiceInitializeCallback()
        {
            //throw new NotImplementedException();
        }

        protected override void ServiceTerminateCallback()
        {
            //throw new NotImplementedException();
        }

        protected override void ServiceActionCallback()
        {
            if (DateTime.Now.Second == _traderBrokerSettings.TradeTimeFrameInSeconds)
            {
                var tradeBrokerThread = new Thread(new ThreadStart(RunTrade));
                tradeBrokerThread.Start();
            }

            OnTickCallback?.Invoke(this, new TickCallbackEventArgs(_traderBrokerSettings.TradePair, DateTime.Now));
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public void RunTrade()
        {
            // get data from Binance
            var retryTimeOut = new TimeSpan(0, 0, 0, 1);
            var kLines = RetryCodeKit.Do(() => GetTradingData(samplesCount: 60), retryTimeOut, 3, true);
            if (kLines == null)
                return;

            // produce the MACD result from the trading data
            var tradeData = kLines.ProduceDataSeries(true);
            var (tradeAction, tradeQty, tradePrice) = AnalyzeTradingData(tradeData);

            var macdAnalysisEA = new MACDPatternAnalyzedEventArgs(_traderBrokerSettings.TradePair, tradeAction, tradeQty, tradePrice);
            OnMACDPatternAnalyzed?.Invoke(this, macdAnalysisEA);

            //Not proceed to order action if there's no trade action anyway. 
            if (tradeAction == TradeActionsEnum.NONE)
                return;

            var traderThread = new Thread(new ThreadStart(() =>
            {
                try
                {
                    // perform order action
                    var orderResponse = MakeTrade(tradeAction, tradeQty, tradePrice);
                    if (orderResponse != null)
                        OnNewOrderPushed?.Invoke(this, new NewOrderPushedEventArgs(_traderBrokerSettings.TradePair, tradeAction, orderResponse));
                }
                catch (Exception ex)
                {
                    OnNewOrderDenied?.Invoke(this, new NewOrderDeniedEventArgs(_traderBrokerSettings.TradePair, tradeAction, ex.Message));
                }
            }));
            traderThread.Start();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeData"></param>
        /// <param name="patternData"></param>
        /// <returns></returns>
        private (TradeActionsEnum, decimal, decimal) AnalyzeTradingData(IEnumerable<TradeData> tradeData)
        {
            var samples = tradeData.Select(p => p).ToArray();

            Func<TradeActionsEnum> identPattern = () =>
            {
                /*var toBuy = samples[^1].AO > samples[^2].AO &&
                            samples[^2].AO < samples[^3].AO &&
                            samples[^1].AO < 0 &&
                            samples[^2].AO < 0 &&
                            samples[^3].AO < 0;

                toBuy = toBuy && samples[^1].MACD >= samples[^2].MACD;

                var toSell = samples[^1].AO < samples[^2].AO &&
                            samples[^2].AO > samples[^3].AO &&
                            samples[^1].AO > 0 &&
                            samples[^2].AO > 0 &&
                            samples[^3].AO > 0;

                toSell = toSell && samples[^1].MACD <= samples[^2].MACD;*/

                /* var toBuy =  &&
                             samples[^1].AO >= samples[^2].AO;
                             //samples[^1].MACD >= samples[^2].Signal;

                 var  &&
                             samples[^1].AO <= samples[^2].AO;
                             //samples[^1].MACD <= samples[^2].Signal;*/

                var toBuy = samples[^1].MACD.SignalOptimized > samples[^2].MACD.SignalOptimized &&
                            samples[^2].MACD.SignalOptimized <= samples[^3].MACD.SignalOptimized &&
                            samples[^3].MACD.SignalOptimized <= samples[^4].MACD.SignalOptimized;

                toBuy = toBuy && samples[^1].AO.AOValue < 0 &&
                                 samples[^2].AO.AOValue < 0 &&
                                 samples[^3].AO.AOValue < 0;

                var toSell = samples[^1].MACD.SignalOptimized < samples[^2].MACD.SignalOptimized &&
                            samples[^2].MACD.SignalOptimized >= samples[^3].MACD.SignalOptimized &&
                            samples[^3].MACD.SignalOptimized >= samples[^4].MACD.SignalOptimized;

                toSell = toSell &&
                            samples[^1].AO.AOValue > 0 &&
                            samples[^2].AO.AOValue > 0 &&
                            samples[^3].AO.AOValue > 0;

                if (toBuy)
                    return TradeActionsEnum.BUY;
                else if (toSell)
                    return TradeActionsEnum.SELL;
                else
                    return TradeActionsEnum.NONE;
            };

            var cryptExcInf = WalletManager.Instance.ExchangeInfo.Symbols.FirstOrDefault(p => p.BaseAsset.Equals(_traderBrokerSettings.Symbol) && p.QuoteAsset.Equals(_traderBrokerSettings.Pair));
            var lotSzFilter = cryptExcInf.Filters.FirstOrDefault(p => p.FilterType.Equals("LOT_SIZE"));
            var precision = (int)Math.Round(-Math.Log(double.Parse(lotSzFilter.StepSize), 10), 0);

            var retryTimeOut = new TimeSpan(0, 0, 0, 1);
            var result = RetryCodeKit.Do(() => WalletManager.Instance.RefreshAsync().Result, retryTimeOut, 3, true);
            if (!result)
                return (TradeActionsEnum.NONE, 0m, 0m);

            var cyptoAssetSymbol = WalletManager.Instance.CryptoAssets[_traderBrokerSettings.Symbol];
            var cyptoAssetPair = WalletManager.Instance.CryptoAssets[_traderBrokerSettings.Pair];
            var currentClosePrice = samples[^1].Close; // get the latest closing price

            var cryptoAssetValue = Math.Round(cyptoAssetSymbol.Balance * currentClosePrice, precision);
            var canSell = cryptoAssetValue > cyptoAssetSymbol.LastTradePrice && cryptoAssetValue > (_traderBrokerSettings.TradingFund * 0.25m);
            var canBuy = cyptoAssetPair.Balance >= _traderBrokerSettings.TradingFund;
            var tradePattern = identPattern();

            if (tradePattern == TradeActionsEnum.BUY && canBuy)
            {
                var qtyAmt = Math.Round(_traderBrokerSettings.TradingFund / currentClosePrice, precision);
                qtyAmt -= qtyAmt * 0.03m;
                return (TradeActionsEnum.BUY, Math.Round(qtyAmt, precision), cryptoAssetValue);
            }

            if (tradePattern == TradeActionsEnum.SELL && canSell)
            {
                var qtyAmt = cyptoAssetSymbol.Balance;
                qtyAmt -= qtyAmt * 0.01m;
                return (TradeActionsEnum.SELL, Math.Round(qtyAmt, precision), 0m);
            }

            return (TradeActionsEnum.NONE, 0m, 0m);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeAction"></param>
        /// <param name="tradeQtyAmount"></param>
        /// <param name="tradePrice"></param>
        /// <returns></returns>
        private NewOrderResponse MakeTrade(TradeActionsEnum tradeAction, decimal tradeQtyAmount, decimal tradePrice)
        {
            if (tradeAction == TradeActionsEnum.NONE)
                return null;

            NewOrderResponse orderResult = null;

            var cyptoAssetSymbol = WalletManager.Instance.CryptoAssets[_traderBrokerSettings.Symbol];
            var publicRestClient = new BinanceClient(_apiKeys.ApiKey, _apiKeys.ApiSecret);
            var clientOrderId = $"{_traderBrokerSettings.TradePair}-{(tradeAction == TradeActionsEnum.BUY ? "B" : "S") }-{DateTime.Now:MMddyyHHmm}";
            var orderSide = tradeAction == TradeActionsEnum.BUY ? OrderSide.Buy : OrderSide.Sell;

            if (tradeQtyAmount > 0)
            {
                try
                {
                    orderResult = publicRestClient.NewOrderAsync(
                                           symbol: _traderBrokerSettings.TradePair.ToString(),
                                           side: orderSide,
                                           orderType: OrderType.Market,
                                           timeInForce: null,
                                           quantity: tradeQtyAmount,
                                           price: null,
                                           isTestOrder: false,
                                           newClientOrderId: clientOrderId).Result;

                    cyptoAssetSymbol.LastTradePrice = tradeAction == TradeActionsEnum.BUY ? tradePrice : 0m;
                    cyptoAssetSymbol.LastAction = tradeAction;
                    cyptoAssetSymbol.LastActionDateTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            WalletManager.Instance.RefreshAsync().Wait();
            return orderResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="omitCurrentTime"></param>
        /// <param name="samplesCount">number of minutes of samples</param>
        /// <returns></returns>
        private IEnumerable<KLine> GetTradingData(bool omitCurrentTime = false, int samplesCount = 15)
        {
            try
            {
                var publicRestClient = new BinanceClient();
                var bKLines = publicRestClient.KLines(_traderBrokerSettings.TradePairString, KlineInterval.Minute1, samplesCount + 60).Result;

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

        #region event handlers

        public class TickCallbackEventArgs : EventArgs
        {
            public DateTime DateTime { get; private set; }
            public TradePair TradePair { get; private set; }

            public TickCallbackEventArgs(TradePair tradePair)
                : this(tradePair, DateTime.Now)
            {
            }

            public TickCallbackEventArgs(TradePair tradePair, DateTime dateTime)
            {
                DateTime = dateTime;
                TradePair = tradePair;
            }
        }

        public delegate void TickCallbackEventHandler(object sender, TickCallbackEventArgs e);

        public class MACDPatternGeneratedEventArgs : EventArgs
        {
            public TradePair TradePair { get; private set; }
            public bool?[] MACDPattern { get; private set; }

            public MACDPatternGeneratedEventArgs(TradePair tradePair, bool?[] macdPattern)
            {
                MACDPattern = macdPattern;
                TradePair = tradePair;
            }
        }

        public delegate void MACDPatternGeneratedEventHandler(object sender, MACDPatternGeneratedEventArgs e);

        public class MACDPatternAnalyzedEventArgs : EventArgs
        {
            public TradePair TradePair { get; private set; }
            public decimal TradeQty { get; set; }
            public decimal TradePrice { get; set; }
            public TradeActionsEnum TradeAction { get; private set; }

            public MACDPatternAnalyzedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, decimal tradeQty, decimal tradePrice)
            {
                TradeAction = tradeAction;
                TradePair = tradePair;
                TradeQty = tradeQty;
                TradePrice = tradePrice;
            }
        }

        public delegate void MACDPatternAnalyzedEventHandler(object sender, MACDPatternAnalyzedEventArgs e);

        public class NewOrderPushedEventArgs : EventArgs
        {
            public TradePair TradePair { get; private set; }
            public TradeActionsEnum TradeAction { get; set; }
            public NewOrderResponse OrderResponse { get; private set; }

            public NewOrderPushedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, NewOrderResponse orderResponse)
            {
                TradePair = tradePair;
                TradeAction = tradeAction;
                OrderResponse = orderResponse;
            }
        }

        public delegate void NewOrderPushedEventHandler(object sender, NewOrderPushedEventArgs e);

        public class NewOrderDeniedEventArgs : EventArgs
        {
            public string ErrorMessage { get; private set; }
            public TradePair TradePair { get; private set; }
            public TradeActionsEnum TradeAction { get; private set; }

            public NewOrderDeniedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, string errorMessage)
            {
                ErrorMessage = errorMessage;
                TradePair = tradePair;
                TradeAction = tradeAction;
            }
        }

        public delegate void NewOrderDeniedEventHandler(object sender, NewOrderDeniedEventArgs e);

        #endregion
    }


}
