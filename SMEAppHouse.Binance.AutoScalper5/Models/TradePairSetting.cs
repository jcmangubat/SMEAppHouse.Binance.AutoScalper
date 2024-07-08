using M3C.Finance.BinanceSdk.Enumerations;
using Newtonsoft.Json;
using System.Text;

namespace SMEAppHouse.Binance.AutoScalper5.Models
{
    public class TradePairSetting
    {
        [JsonProperty("Symbol")]
        public string Symbol { get; set; }

        [JsonProperty("Pair")]
        public string Pair { get; set; }

        [JsonProperty("TradeTimeFrameInSecs")]
        public int TradeTimeFrameInSeconds { get; set; }

        [JsonProperty("TradeFundBehavior")]
        public TradeFundBehaviorsEnum TradeFundBehavior { get; set; }

        [JsonProperty("TradeFundLimit")]
        public int TradeFundLimit { get; set; }

        [JsonProperty("Active")]
        public bool Active { get; set; }

        [JsonProperty("KLineInterval")]
        public KlineInterval KLineInterval { get; set; }

        [JsonProperty("TradeSamplesMinimum")]
        public int TradeSamplesMinimum { get; set; }

        [JsonProperty("TradeSamplesSize")]
        public int TradeSamplesSize { get; set; }

        [JsonIgnore]
        public string TradePairString => $"{Symbol.Trim().ToUpper()}{Pair.Trim().ToUpper()}";

        [JsonIgnore]
        public TradePair TradePair => new TradePair(Symbol, Pair);
    }
}
