using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.Enumerations;
using M3C.Finance.BinanceSdk.ResponseObjects;
using SMEAppHouse.Binance.AutoScalper3.Helpers;
using SMEAppHouse.Binance.AutoScalper3.Models;
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
            var kLines = GetTradingData();
            if (kLines == null)
                return;

            // produce the MACD result from the trading data
            var macdData = kLines.ProduceMACDData();

            // analyze the MACD result
            var patternData = macdData.GenerateMACDSignalPattern();
            OnMACDPatternIdentified?.Invoke(this, new MACDPatternGeneratedEventArgs(_traderBrokerSettings.TradePair, patternData));

            (TradeActionsEnum tradeAction, bool?[] pattern) tradeSuggestion = AnalyzeMACDSignalPattern(macdData, patternData);
            var macdAnalysisEA = new MACDPatternAnalyzedEventArgs(_traderBrokerSettings.TradePair, tradeSuggestion.tradeAction, tradeSuggestion.pattern);
            OnMACDPatternAnalyzed?.Invoke(this, macdAnalysisEA);

            //Not proceed to order action if there's no trade action anyway. 
            if (tradeSuggestion.tradeAction == TradeActionsEnum.NONE)
                return;

            // perform order action
            var orderResponse = MakeTrade(tradeSuggestion, macdData);
            if (orderResponse != null)
                OnNewOrderPushed?.Invoke(this, new NewOrderPushedEventArgs(_traderBrokerSettings.TradePair, tradeSuggestion.tradeAction, tradeSuggestion.pattern, orderResponse));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="macdData"></param>
        /// <param name="patternData"></param>
        /// <returns></returns>
        private (TradeActionsEnum tradeAction, bool?[] pattern) AnalyzeMACDSignalPattern(IEnumerable<MACDData> macdData, bool?[] patternData)
        {
            Func<TradeActionsEnum, List<bool?[]>, IEnumerable<MACDData>, (TradeActionsEnum, bool?[])> identTradePattern = (tradeAction, srcPatterns, macdData) =>
            {
                foreach (var keyFlags in srcPatterns)
                {
                    var detected = true;
                    var reviewFlags = patternData.Reverse().Take(keyFlags.Length).Reverse().ToArray();

                    for (var ctr = 0; ctr < reviewFlags.Count(); ctr++)
                    {
                        if (reviewFlags[ctr] != keyFlags[ctr])
                        {
                            detected = false;
                            break;
                        }
                    }
                    if (detected)
                        return (tradeAction, reviewFlags);
                }
                return (TradeActionsEnum.NONE, null);
            };

            Func<bool> inBuyRegion = () =>
            {
                var macds = macdData.ToArray();
                var inRegion = macds[macds.Length - 1].Signal < 0 &&
                    macds[macds.Length - 2].Signal < 0 &&
                    macds[macds.Length - 3].Signal < 0;
                return inRegion;
            };

            Func<bool> inSellRegion = () =>
            {
                var macds = macdData.ToArray();
                var inRegion = macds[macds.Length - 1].Signal > 0 &&
                    macds[macds.Length - 2].Signal > 0 &&
                    macds[macds.Length - 3].Signal > 0;
                return inRegion;
            };

            Func<bool> canSell = () =>
            {
                var macds = macdData.ToArray();
                var canTrade = macds[macds.Length - 1].Signal <= macds[macds.Length - 2].Signal;
                return canTrade;
            };

            Func<bool> canBuy = () =>
            {
                var macds = macdData.ToArray();
                var canTrade = macds[macds.Length - 1].Signal >= macds[macds.Length - 2].Signal;
                return canTrade;
            };

            (TradeActionsEnum tradeAction, bool?[] pattern) finalResult = (TradeActionsEnum.NONE, null);
            (TradeActionsEnum tradeAction, bool?[] pattern) buyResult = identTradePattern(TradeActionsEnum.BUY, _traderBrokerSettings.BuyDipFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) sellBackResult = identTradePattern(TradeActionsEnum.SELLBACK, _traderBrokerSettings.SellBackFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) sellResult = identTradePattern(TradeActionsEnum.SELL, _traderBrokerSettings.SellPeekFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) buyBackResult = identTradePattern(TradeActionsEnum.BUYBACK, _traderBrokerSettings.BuyBackFlags, macdData);

            if (buyResult.tradeAction != TradeActionsEnum.NONE)
                finalResult = buyResult;
            if (sellBackResult.tradeAction != TradeActionsEnum.NONE)
                finalResult = sellBackResult;
            if (sellResult.tradeAction != TradeActionsEnum.NONE)
                finalResult = sellResult;
            if (buyBackResult.tradeAction != TradeActionsEnum.NONE)
                finalResult = buyBackResult;

            if (((finalResult.tradeAction == TradeActionsEnum.BUY || finalResult.tradeAction == TradeActionsEnum.BUYBACK) && !(inBuyRegion() && canBuy())) ||
                (finalResult.tradeAction == TradeActionsEnum.BUY || finalResult.tradeAction == TradeActionsEnum.BUYBACK) && !(inSellRegion() && canSell()))
                finalResult = (TradeActionsEnum.NONE, null);

            return finalResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private NewOrderResponse MakeTrade((TradeActionsEnum tradeAction, bool?[] pattern) tradeSuggestion, IEnumerable<MACDData> macdData)
        {
            var cryptExcInf = WalletManager.Instance.ExchangeInfo.Symbols.FirstOrDefault(p => p.BaseAsset.Equals(_traderBrokerSettings.Symbol) && p.QuoteAsset.Equals(_traderBrokerSettings.Pair));
            var lotSzFilter = cryptExcInf.Filters.FirstOrDefault(p => p.FilterType.Equals("LOT_SIZE"));
            var precision = (int)(Math.Round(-Math.Log(double.Parse(lotSzFilter.StepSize), 10), 0));

            var toBuy = tradeSuggestion.tradeAction == TradeActionsEnum.BUY || tradeSuggestion.tradeAction == TradeActionsEnum.BUYBACK;
            var toSell = tradeSuggestion.tradeAction == TradeActionsEnum.SELL || tradeSuggestion.tradeAction == TradeActionsEnum.SELLBACK;
            var cyptoAssetSymbol = WalletManager.Instance.CryptoAssets[_traderBrokerSettings.Symbol];
            var cyptoAssetPair = WalletManager.Instance.CryptoAssets[_traderBrokerSettings.Pair];
            var exchangeRate = macdData.ToArray()[macdData.Count() - 1].ClosingPrice; // get the latest closing price
            var cryptoAssetValue = Math.Round(cyptoAssetSymbol.Balance * exchangeRate, precision); // compute how much in USDT the held cyto asset is

            if (// if buying and not much amount to buy, exit
                (toBuy && cyptoAssetPair.Balance < _traderBrokerSettings.TradePlayAmount) ||
                // if selling and currency equivalent amount not exceeding the playamount, exit
                (toSell && cryptoAssetValue < _traderBrokerSettings.TradePlayAmount))
                return null;

            var publicRestClient = new BinanceClient(_apiKeys.ApiKey, _apiKeys.ApiSecret);
            var clientOrderId = $"{_traderBrokerSettings.TradePair}-{(toBuy ? "B" : "S") }-{DateTime.Now:MMddyyHHmm}";
            var orderSide = tradeSuggestion.tradeAction == TradeActionsEnum.BUY ||
                            tradeSuggestion.tradeAction == TradeActionsEnum.BUYBACK ?
                                OrderSide.Buy : OrderSide.Sell;

            var qtyAmt = 0M;
            if (toBuy)
                // buy eth based on the play amount value                
                qtyAmt = Math.Round(_traderBrokerSettings.TradePlayAmount / exchangeRate, precision);
            else
                qtyAmt = cyptoAssetSymbol.Balance;

            var orderResult = publicRestClient.NewOrderAsync(
                                    symbol: _traderBrokerSettings.TradePair.ToString(),
                                    side: orderSide,
                                    orderType: OrderType.Market,
                                    timeInForce: null,
                                    quantity: qtyAmt,
                                    price: null,
                                    isTestOrder: false,
                                    newClientOrderId: clientOrderId).Result;

            WalletManager.Instance.RefreshAsync().Wait();

            return orderResult;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        private (TradeActionsEnum, bool?[]) AnalyzeMACDSignalPattern_v1(bool?[] data, IEnumerable<MACDData> macdData)
        {
            Func<TradeActionsEnum, IEnumerable<MACDData>, bool> checkIfInTradeBoundary = (tradeAction, macdData) =>
            {
                var macdDataArray = macdData.ToArray();
                if (tradeAction == TradeActionsEnum.BUY || tradeAction == TradeActionsEnum.BUYBACK)
                {
                    return macdDataArray[macdDataArray.Length - 1].Histogram < 0 &&
                            macdDataArray[macdDataArray.Length - 2].Histogram < 0 &&
                            macdDataArray[macdDataArray.Length - 3].Histogram < 0;
                }
                else if (tradeAction == TradeActionsEnum.SELL || tradeAction == TradeActionsEnum.SELLBACK)
                {
                    return macdDataArray[macdDataArray.Length - 1].Histogram > 0 &&
                            macdDataArray[macdDataArray.Length - 2].Histogram > 0 &&
                            macdDataArray[macdDataArray.Length - 3].Histogram > 0;
                }
                else return false;
            };

            Func<TradeActionsEnum, List<bool?[]>, IEnumerable<MACDData>, (TradeActionsEnum, bool?[])> identTradeAction = (tradeAction, srcPatterns, macdData) =>
            {
                foreach (var keyFlags in srcPatterns)
                {
                    var detected = true;
                    var reviewFlags = data.Reverse().Take(keyFlags.Length).Reverse().ToArray();

                    for (var ctr = 0; ctr < reviewFlags.Count(); ctr++)
                    {
                        if (reviewFlags[ctr] != keyFlags[ctr])
                        {
                            detected = false;
                            break;
                        }
                    }
                    if (detected && checkIfInTradeBoundary(tradeAction, macdData))
                        return (tradeAction, reviewFlags);
                }
                return (TradeActionsEnum.NONE, null);
            };

            (TradeActionsEnum tradeAction, bool?[] pattern) buyResult = identTradeAction(TradeActionsEnum.BUY, _traderBrokerSettings.BuyDipFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) sellBackResult = identTradeAction(TradeActionsEnum.SELLBACK, _traderBrokerSettings.SellBackFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) sellResult = identTradeAction(TradeActionsEnum.SELL, _traderBrokerSettings.SellPeekFlags, macdData);
            (TradeActionsEnum tradeAction, bool?[] pattern) buyBackResult = identTradeAction(TradeActionsEnum.BUYBACK, _traderBrokerSettings.BuyBackFlags, macdData);

            if (buyResult.tradeAction != TradeActionsEnum.NONE)
                return buyResult;

            if (sellBackResult.tradeAction != TradeActionsEnum.NONE)
                return sellBackResult;

            if (sellResult.tradeAction != TradeActionsEnum.NONE)
                return sellResult;

            if (buyBackResult.tradeAction != TradeActionsEnum.NONE)
                return buyBackResult;

            return (TradeActionsEnum.NONE, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private IEnumerable<KLine> GetTradingData(bool omitCurrentTime = false)
        {
            try
            {
                var publicRestClient = new BinanceClient();
                var bKLines = publicRestClient.KLines(_traderBrokerSettings.TradePairString, KlineInterval.Minute1, 15 + 33 + 3).Result;

                var kLinesCleared = bKLines.ToList();
                if (omitCurrentTime)
                {
                    var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    var lastMinuteKline = kLinesCleared
                                                .FirstOrDefault(p => closingStartTime.AddMilliseconds(p.CloseTime).ToLocalTime().Minute == DateTime.Now.Minute);
                    if (lastMinuteKline != null)
                        kLinesCleared.Remove(lastMinuteKline);
                }

                kLinesCleared.Reverse();
                kLinesCleared = kLinesCleared.Take(15 + 33).ToList();
                kLinesCleared.Reverse();

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
            public TradeActionsEnum TradeAction { get; private set; }
            public bool?[] MACDPattern { get; private set; }

            public MACDPatternAnalyzedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, bool?[] macdPattern)
            {
                MACDPattern = macdPattern;
                TradeAction = tradeAction;
                TradePair = tradePair;
            }
        }

        public delegate void MACDPatternAnalyzedEventHandler(object sender, MACDPatternAnalyzedEventArgs e);

        public class NewOrderPushedEventArgs : EventArgs
        {
            public TradePair TradePair { get; private set; }
            public TradeActionsEnum TradeAction { get; set; }
            public bool?[] Pattern { get; set; }
            public NewOrderResponse OrderResponse { get; private set; }

            public NewOrderPushedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, bool?[] pattern, NewOrderResponse orderResponse)
            {
                TradePair = tradePair;
                TradeAction = tradeAction;
                Pattern = pattern;
                OrderResponse = orderResponse;
            }
        }

        public delegate void NewOrderPushedEventHandler(object sender, NewOrderPushedEventArgs e);

        #endregion
    }


}
