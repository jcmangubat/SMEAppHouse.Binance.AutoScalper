using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.Enumerations;
using M3C.Finance.BinanceSdk.ResponseObjects;
using SMEAppHouse.Binance.AutoScalper2.Models;
using static SMEAppHouse.Binance.AutoScalper2.Models.Rules;

namespace SMEAppHouse.Binance.AutoScalper2.Worker
{
    public class TradingServiceBroker
    {
        private KLineItem.KLineItems _currentKLines = new KLineItem.KLineItems();

        private string _apiKey;
        private string _apiSecret;
        private AssetsLedger.CryptoAsset _pairAsset = null;

        public TraderBrokerSettings BrokerSettings { get; set; }
        public bool ApiQueryingKLines { get; set; }
        public Guid CheckQueueToken { get; set; } = Guid.Empty;

        public event TradingCheckedCallbackEventHandler OnTradingCheckedCallback;

        public TradingServiceBroker(string apiKey, string apiSecret, TraderBrokerSettings behaviorSettings)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;

            BrokerSettings = behaviorSettings;

            if (AssetsLedger.Instance.CryptoAssets.ContainsKey(BrokerSettings.Pair))
                _pairAsset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task TryTradeAsync()
        {
            if (BrokerSettings.TradeAtSecondsTrigger == 0 ||
                !(DateTime.Now.Second == BrokerSettings.TradeAtSecondsTrigger))
                return;

            List<KLinesResponseItem> kLines;
            try
            {
                ApiQueryingKLines = true;
                var publicRestClient = new BinanceClient();
                var _kLines = publicRestClient.KLines(BrokerSettings.TradePair, KlineInterval.Minute1, 15).Result;
                kLines = new List<KLinesResponseItem>(_kLines);

                CheckQueueToken = Guid.Empty;
                ApiQueryingKLines = false;
            }
            catch (Exception ex)
            {
                return;
            }

            var result = AnalyzeKLines(kLines);
            if (result.canTrade)
            {
                var success = ActOnTheTrade(result.tradeAction, result.closingPrice);

                if (success)
                {
                    var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
                    var pair = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];

                    asset.LastAction = result.tradeAction;
                    asset.LastActionDateTime = DateTime.Now;

                    var percent = ((result.closingPrice.Value - asset.LastHoldPrice) / asset.LastHoldPrice) * 100;
                    var value = asset.LastHoldPrice * (percent / 100);
                    asset.LastHoldPrice += value;

                    await AssetsLedger.Instance.RefreshAsync();
                }
            }

            var trdingChckdCallbackEA = new TradingCheckedCallbackEventArgs(result.tradeAction, (BrokerSettings.Symbol, BrokerSettings.Pair));

            if (result.tradeAction != TradeActionsEnum.NONE)
            {
                trdingChckdCallbackEA.ClosingPrice = result.closingPrice;
                trdingChckdCallbackEA.Pattern = result.pattern;
            }
            else if (!string.IsNullOrEmpty(result.notTradingReason))
                trdingChckdCallbackEA.NotTradingReason = result.notTradingReason;

            OnTradingCheckedCallback?.Invoke(this, trdingChckdCallbackEA);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tradeAction"></param>
        /// <param name="closingPrice"></param>
        /// <returns></returns>
        private bool ActOnTheTrade(TradeActionsEnum tradeAction, double? closingPrice)
        {
            var publicRestClient = new BinanceClient(_apiKey, _apiSecret);
            var success = true;// false;

            switch (tradeAction)
            {
                case TradeActionsEnum.BUYING:
                    break;
                case TradeActionsEnum.SELLBACK:
                    break;
                case TradeActionsEnum.SELLING:
                    break;
                case TradeActionsEnum.BUYBACK:
                    break;
            }

            return success;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="kLines"></param>
        /// <returns></returns>
        private (bool canTrade, TradeActionsEnum tradeAction, double? closingPrice, int[] pattern, string notTradingReason) AnalyzeKLines(IEnumerable<KLinesResponseItem> kLines)
        {
            var canTrade = false;
            var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var KLineCleared = kLines.ToList();
            var lastMinuteKline = KLineCleared.FirstOrDefault(p => closingStartTime.AddMilliseconds(p.CloseTime).ToLocalTime().Minute == DateTime.Now.Minute);

            if (lastMinuteKline != null)
                KLineCleared.Remove(lastMinuteKline);


            _currentKLines.Refresh(KLineCleared);
            var closings = _currentKLines.Select(p => p.ClosingPrice).ToArray();
            //closings = KLineItem.KLineItems.Smoothen(closings, 3);

            var tradeEval = IdentifyTradeAction(closings);
            var notTradingReason = string.Empty;

            switch (tradeEval.tradeAction)
            {
                case TradeActionsEnum.BUYING:
                    canTrade = CanBuy(tradeEval, out notTradingReason);
                    break;
                case TradeActionsEnum.SELLBACK:
                    canTrade = CanSellBack(tradeEval, out notTradingReason);
                    break;
                case TradeActionsEnum.SELLING:
                    canTrade = CanSell(tradeEval, out notTradingReason);
                    break;
                case TradeActionsEnum.BUYBACK:
                    canTrade = CanBuyBack(tradeEval, out notTradingReason);
                    break;
                default:
                    break;
            }

            if (canTrade)
                return (canTrade, tradeEval.tradeAction, tradeEval.closingPrice.Value, tradeEval.pattern, notTradingReason);

            return (false, TradeActionsEnum.NONE, null, null, notTradingReason);
        }

        private bool CanBuy((TradeActionsEnum tradeAction, double? closingPrice, int[] pattern) tradeEval, out string notTradingReason)
        {
            notTradingReason = string.Empty;
            var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
            var pairAsset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];

            // - Cancel buy when Pair (USDT) is less than this assetLedger's playAmount (we cannot afford)
            if (BrokerSettings.TradePlayAmount > 0 && pairAsset.Balance < BrokerSettings.TradePlayAmount)
            {
                notTradingReason = $"paired asset to pay for sell is less than this asset's playAmount";
                return false;
            }


            var percent = ((tradeEval.closingPrice.Value - asset.LastHoldPrice) / asset.LastHoldPrice) * 100;
            if (percent < BrokerSettings.TradeScalpTolerance)
            {
                notTradingReason = $"scalp price tolerance is too low or less than the set percentage.";
                return false;
            }

            /*// - Cancel buy when computed price is less than this asset's trade price limit (we're not satified)
            if (asset.LastHoldPrice > 0
                && (asset.LastHoldPrice - tradeEval.closingPrice.Value) < BrokerSettings.TradeScalpTolerance
                && asset.LastAction != TradeActionsEnum.SELLBACK)
                notTradingReason = $"price difference is less than this asset's trade price limit";*/

            return true;
        }

        private bool CanSellBack((TradeActionsEnum tradeAction, double? closingPrice, int[] pattern) tradeEval, out string notTradingReason)
        {
            notTradingReason = string.Empty;
            var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
            var pairAsset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];

            // Cancel sellback when closing price is higher than last hold price
            if (tradeEval.closingPrice.Value >= asset.LastHoldPrice)
                notTradingReason = $"closing price is higher than last hold price";

            if (!string.IsNullOrEmpty(notTradingReason))
                return false;

            return true;
        }

        private bool CanSell((TradeActionsEnum tradeAction, double? closingPrice, int[] pattern) tradeEval, out string notTradingReason)
        {
            notTradingReason = string.Empty;
            var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
            var pairAsset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];

            // - Cancel sell when there is no amount in the asset to sell
            if (asset.Balance <= 0)
                notTradingReason = $"there is no amount in the asset to sell";

            var percent = ((tradeEval.closingPrice.Value - asset.LastHoldPrice) / asset.LastHoldPrice) * 100;
            if (percent < BrokerSettings.TradeScalpTolerance)
            {
                notTradingReason = $"scalp price tolerance is too low or less than the set percentage.";
                return false;
            }

            /*// - Cancel sell when price is less than this asset's trade price limit
            else if ((tradeEval.closingPrice.Value - asset.LastHoldPrice) < BrokerSettings.TradeScalpTolerance)
                notTradingReason = $"price is less than this asset's trade price limit";*/

            return true;
        }

        private bool CanBuyBack((TradeActionsEnum tradeAction, double? closingPrice, int[] pattern) tradeEval, out string notTradingReason)
        {
            notTradingReason = string.Empty;
            var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
            var pairAsset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Pair];

            // Cancel buyback when closing price is lesser than last hold price
            if (tradeEval.closingPrice.Value <= asset.LastHoldPrice)
                notTradingReason = $"closing price is lesser than last hold price";

            // - Cancel buy when Pair (USDT) is less than this assetLedger's playAmount
            else if (BrokerSettings.TradePlayAmount > 0 && pairAsset.Balance < BrokerSettings.TradePlayAmount)
                notTradingReason = $"Pair (USDT) is less than this assetLedger's playAmount";

            if (!string.IsNullOrEmpty(notTradingReason))
                return false;

            return true;
        }

        #region private methods

        /// <summary>
        /// 
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (bool inPattern, double? closingPrice) CheckPricePattern(int[] pattern, double[] closingPrices)
        {
            var cPrices = closingPrices.Reverse().Take(pattern.Length + 1).Reverse().ToArray();
            if (cPrices.Length < pattern.Length)
                return (false, null);

            var acquiredPatterns = new List<int>();
            var prvPrc = -909090909.0;

            for (var ctr = 0; ctr < cPrices.Length; ctr++)
            {
                if (prvPrc > -909090909.0)
                    acquiredPatterns.Add(cPrices[ctr] >= prvPrc ? 1 : 0);
                prvPrc = cPrices[ctr];
            }
            return (Enumerable.SequenceEqual(pattern, acquiredPatterns), closingPrices[^1]);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (bool inBuyPattern, double? closingPrice, int[] pattern) InBuyPattern(double[] closingPrices)
        {
            foreach (var pattern in BrokerSettings.BuyDipFlags)
            {
                var (patternFollows, closingPrice) = CheckPricePattern(pattern, closingPrices);
                if (patternFollows)
                    return (true, closingPrice.Value, pattern);
            }
            return (false, null, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (bool inSellBackPattern, double? closingPrice, int[] pattern) InSellBackPattern(double[] closingPrices)
        {
            foreach (var pattern in BrokerSettings.SellBackFlags)
            {
                var (patternFollows, closingPrice) = CheckPricePattern(pattern, closingPrices);
                if (patternFollows)
                    return (true, closingPrice.Value, pattern);
            }
            return (false, null, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (bool inSellPattern, double? closingPrice, int[] pattern) InSellPattern(double[] closingPrices)
        {
            var notTradingReason = string.Empty;
            foreach (var pattern in BrokerSettings.SellPeekFlags)
            {
                var (patternFollows, closingPrice) = CheckPricePattern(pattern, closingPrices);
                if (patternFollows)
                {


                    return (true, closingPrice.Value, pattern);
                }
            }
            return (false, null, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (bool inBuyBackPattern, double? closingPrice, int[] pattern) InBuyBackPattern(double[] closingPrices)
        {
            foreach (var pattern in BrokerSettings.BuyBackFlags)
            {
                var (patternFollows, closingPrice) = CheckPricePattern(pattern, closingPrices);
                if (patternFollows)
                    return (true, closingPrice.Value, pattern);
            }
            return (false, null, null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <returns></returns>
        private (TradeActionsEnum tradeAction, double? closingPrice, int[] pattern) IdentifyTradeAction(double[] closingPrices)
        {
            try
            {
                var asset = AssetsLedger.Instance.CryptoAssets[BrokerSettings.Symbol];
                var task1 = Task.Run(() => InBuyPattern(closingPrices));
                var task2 = Task.Run(() => InSellBackPattern(closingPrices));
                var task3 = Task.Run(() => InSellPattern(closingPrices));
                var task4 = Task.Run(() => InBuyBackPattern(closingPrices));
                Task<(bool inPattern, double? closingPrice, int[] pattern)[]> tasks = Task.WhenAll(task1, task2, task3, task4);

                if (tasks.Result[0].inPattern)
                {
                    var tskTup = tasks.Result[0];
                    var tradeAction = TradeActionsEnum.BUYING;

                    // Cancel buy when last action is not selling or sellback or default
                    if (!new[] { TradeActionsEnum.SELLING, TradeActionsEnum.SELLBACK, TradeActionsEnum.NONE }.Any(p => p == asset.LastAction))
                        return (TradeActionsEnum.NONE, null, null);

                    return (tradeAction, tskTup.closingPrice.Value, tskTup.pattern);
                }

                if (tasks.Result[1].inPattern)
                {
                    var tskTup = tasks.Result[1];
                    var tradeAction = TradeActionsEnum.SELLBACK;

                    // Cancel sellback when last action is not buying
                    if (asset.LastAction != TradeActionsEnum.BUYING)
                        return (TradeActionsEnum.NONE, null, null);

                    return (tradeAction, tskTup.closingPrice.Value, tskTup.pattern);
                }

                if (tasks.Result[2].inPattern)
                {
                    var tskTup = tasks.Result[2];
                    var tradeAction = TradeActionsEnum.SELLING;

                    // Cancel sell when last action is not buying or buyback
                    if (!new[] { TradeActionsEnum.BUYING, TradeActionsEnum.BUYBACK }.Any(p => p == asset.LastAction) && asset.LastAction != TradeActionsEnum.BUYBACK)
                        return (TradeActionsEnum.NONE, null, null);

                    return (tradeAction, tskTup.closingPrice.Value, tskTup.pattern);
                }

                if (tasks.Result[3].inPattern)
                {
                    var tskTup = tasks.Result[3];
                    var tradeAction = TradeActionsEnum.BUYBACK;

                    // Cancel buyback when last action is not selling
                    if (asset.LastAction != TradeActionsEnum.SELLING)
                        return (TradeActionsEnum.NONE, null, null);

                    return (tradeAction, tskTup.closingPrice.Value, tskTup.pattern);
                }

            }
            catch (Exception ex)
            {
                throw;
            }
            return (TradeActionsEnum.NONE, null, null);
        }

        #endregion
    }

    #region event handlers

    public class TradingCheckedCallbackEventArgs : EventArgs
    {
        public TradeActionsEnum TradeAction { get; private set; }
        public double? ClosingPrice { get; set; }
        public (string symbol, string pair) TradePair { get; private set; }
        public int[] Pattern { get; set; }
        public string NotTradingReason { get; set; }

        public TradingCheckedCallbackEventArgs(TradeActionsEnum tradeAction, (string symbol, string pair) tradePair)
            : this(tradeAction, tradePair, null, null, string.Empty)
        {
        }
        public TradingCheckedCallbackEventArgs(TradeActionsEnum tradeAction, (string symbol, string pair) tradePair, double? closingPrice, int[] pattern, string notTradingReason)
        {
            TradeAction = tradeAction;
            ClosingPrice = closingPrice;
            TradePair = tradePair;
            Pattern = pattern;
            NotTradingReason = notTradingReason;
        }
    }

    public delegate void TradingCheckedCallbackEventHandler(object sender, TradingCheckedCallbackEventArgs e);

    #endregion

}
