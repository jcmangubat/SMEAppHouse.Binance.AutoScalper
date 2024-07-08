using Newtonsoft.Json;
using System.Collections.Generic;

namespace SMEAppHouse.Binance.AutoScalper3.Models
{
    public class TradeBrokerConfigurations
    {
        [JsonProperty("trader-broker-config")]
        public List<TradeBrokerSettings> TraderBrokerSettings { get; set; }
    }

    public class TradeBrokerSettings
    {
        public string Symbol { get; set; }
        public string Pair { get; set; }
        public bool Active { get; set; }
        public decimal TradeScalpTolerance { get; set; }
        public decimal TradingFund { get; set; }
        public int TradeTimeFrameInSeconds { get; set; }

        [JsonIgnore]
        public string TradePairString => $"{Symbol.Trim().ToUpper()}{Pair.Trim().ToUpper()}";

        [JsonIgnore]
        public TradePair TradePair => new TradePair(Symbol, Pair);
    }


}
