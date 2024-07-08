using Newtonsoft.Json;
using System.Collections.Generic;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class TradeBrokerConfigurations
    {
        [JsonProperty("trader-broker-config")]
        public List<TraderBrokerSettings> TraderBrokerSettings { get; set; }
    }

    public class TraderBrokerSettings
    {
        public string Symbol { get; set; }
        public string Pair { get; set; }
        public bool Active { get; set; }
        public double TradeScalpTolerance { get; set; }
        public double TradePlayAmount { get; set; }
        public int TradeAtSecondsTrigger { get; set; }
        public List<int[]> BuyDipFlags { get; set; }
        public List<int[]> SellBackFlags { get; set; }
        public List<int[]> SellPeekFlags { get; set; }
        public List<int[]> BuyBackFlags { get; set; }

        [JsonIgnore]
        public string TradePair => $"{Symbol.Trim().ToUpper()}{Pair.Trim().ToUpper()}";
    }

}
