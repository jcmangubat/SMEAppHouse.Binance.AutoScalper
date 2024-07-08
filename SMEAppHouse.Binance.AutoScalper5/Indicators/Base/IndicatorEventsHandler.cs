using SMEAppHouse.Binance.AutoScalper5.Models;
using System;

namespace SMEAppHouse.Binance.AutoScalper5.Indicators.Base
{
    public static class IndicatorEventsHandler
    {
        public class MarketEvaluatedEventArgs : EventArgs
        {
            public string ErrorMessage { get; private set; }
            public TradePair TradePair { get; private set; }
            public TradeActionsEnum TradeAction { get; private set; }

            #region constructors

            public MarketEvaluatedEventArgs()
            {
            }

            public MarketEvaluatedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction)
                : this(tradePair, tradeAction, string.Empty)
            {
            }

            public MarketEvaluatedEventArgs(TradePair tradePair, TradeActionsEnum tradeAction, string errorMessage)
            {
                ErrorMessage = errorMessage;
                TradePair = tradePair;
                TradeAction = tradeAction;
            }

            #endregion

        }

        public delegate void MarketEvaluatedEventHandler(object sender, MarketEvaluatedEventArgs e);
    }
}