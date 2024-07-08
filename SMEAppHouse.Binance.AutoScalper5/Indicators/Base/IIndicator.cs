using SMEAppHouse.Binance.AutoScalper5.Models;
using System.Collections.Generic;
using static SMEAppHouse.Binance.AutoScalper5.Indicators.Base.IndicatorEventsHandler;

namespace SMEAppHouse.Binance.AutoScalper5.Indicators.Base
{
    public interface IIndicator
    {
        bool GoodToBuy { get; }
        bool GoodToSell { get; }
        void Run(IEnumerable<KLine> tradeKLines);

        TradePairSetting TradePairSetting { get; }

        event MarketEvaluatedEventHandler OnMarketEvaluated;
    }
}