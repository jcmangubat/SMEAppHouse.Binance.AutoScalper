using SMEAppHouse.Binance.AutoScalper5.Indicators.Base;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SMEAppHouse.Binance.AutoScalper5.Indicators.Base.IndicatorEventsHandler;

namespace SMEAppHouse.Binance.AutoScalper5.Models.Indicators
{
    public class STCIndicator : IndicatorBase
    {
        public STCIndicator(TradePairSetting tradePairSetting) :
            base(tradePairSetting)
        {

        }

        public override bool GoodToBuy { get => _goodToBuy; }
        public override bool GoodToSell { get => _goodToSell; }

        public override event MarketEvaluatedEventHandler OnMarketEvaluated;

        public override void Run(IEnumerable<KLine> tradeKLines)
        {
            Task.Factory.StartNew(() =>
            {
                var marketEvaluatedEventArgs = new MarketEvaluatedEventArgs(base.TradePairSetting.TradePair, TradeActionsEnum.NONE);
                OnMarketEvaluated?.Invoke(this, marketEvaluatedEventArgs);
            });
        }
    }
}