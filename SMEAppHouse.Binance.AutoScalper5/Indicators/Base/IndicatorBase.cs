using SMEAppHouse.Binance.AutoScalper5.Models;
using System;
using System.Collections.Generic;
using static SMEAppHouse.Binance.AutoScalper5.Indicators.Base.IndicatorEventsHandler;

namespace SMEAppHouse.Binance.AutoScalper5.Indicators.Base
{
    public abstract class IndicatorBase : IIndicator
    {
        private protected bool _goodToBuy;
        private protected bool _goodToSell;
        private protected TradePairSetting _tradePairSetting;

        public abstract bool GoodToBuy { get; }

        public abstract bool GoodToSell { get; }

        public TradePairSetting TradePairSetting { get => _tradePairSetting; }

        public abstract event MarketEvaluatedEventHandler OnMarketEvaluated;

        protected IndicatorBase(TradePairSetting tradePairSetting)
        {
            _tradePairSetting = tradePairSetting;
        }

        public abstract void Run(IEnumerable<KLine> tradeKLines);
    }
}

