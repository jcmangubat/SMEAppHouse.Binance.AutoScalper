using Newtonsoft.Json;
using System.Collections.Generic;

namespace SMEAppHouse.Binance.AutoScalper3.Models
{
    public class RateLimit
    {
        [JsonProperty("rateLimitType")]
        public string RateLimitType { get; set; }

        [JsonProperty("interval")]
        public string Interval { get; set; }

        [JsonProperty("intervalNum")]
        public int IntervalNum { get; set; }

        [JsonProperty("limit")]
        public int Limit { get; set; }
    }

    public class Filter
    {
        [JsonProperty("filterType")]
        public string FilterType { get; set; }

        [JsonProperty("minPrice")]
        public string MinPrice { get; set; }

        [JsonProperty("maxPrice")]
        public string MaxPrice { get; set; }

        [JsonProperty("tickSize")]
        public string TickSize { get; set; }

        [JsonProperty("multiplierUp")]
        public string MultiplierUp { get; set; }

        [JsonProperty("multiplierDown")]
        public string MultiplierDown { get; set; }

        [JsonProperty("avgPriceMins")]
        public int? AvgPriceMins { get; set; }

        [JsonProperty("minQty")]
        public string MinQty { get; set; }

        [JsonProperty("maxQty")]
        public string MaxQty { get; set; }

        [JsonProperty("stepSize")]
        public string StepSize { get; set; }

        [JsonProperty("minNotional")]
        public string MinNotional { get; set; }

        [JsonProperty("applyToMarket")]
        public bool? ApplyToMarket { get; set; }

        [JsonProperty("limit")]
        public int? Limit { get; set; }

        [JsonProperty("maxNumOrders")]
        public int? MaxNumOrders { get; set; }

        [JsonProperty("maxNumAlgoOrders")]
        public int? MaxNumAlgoOrders { get; set; }
    }

    public class CryptoAsset
    {
        [JsonProperty("symbol")]
        public string Symbol { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("baseAsset")]
        public string BaseAsset { get; set; }

        [JsonProperty("baseAssetPrecision")]
        public int BaseAssetPrecision { get; set; }

        [JsonProperty("quoteAsset")]
        public string QuoteAsset { get; set; }

        [JsonProperty("quotePrecision")]
        public int QuotePrecision { get; set; }

        [JsonProperty("quoteAssetPrecision")]
        public int QuoteAssetPrecision { get; set; }

        [JsonProperty("baseCommissionPrecision")]
        public int BaseCommissionPrecision { get; set; }

        [JsonProperty("quoteCommissionPrecision")]
        public int QuoteCommissionPrecision { get; set; }

        [JsonProperty("orderTypes")]
        public List<string> OrderTypes { get; set; }

        [JsonProperty("icebergAllowed")]
        public bool IcebergAllowed { get; set; }

        [JsonProperty("ocoAllowed")]
        public bool OCOAllowed { get; set; }

        [JsonProperty("quoteOrderQtyMarketAllowed")]
        public bool QuoteOrderQtyMarketAllowed { get; set; }

        [JsonProperty("isSpotTradingAllowed")]
        public bool IsSpotTradingAllowed { get; set; }

        [JsonProperty("isMarginTradingAllowed")]
        public bool IsMarginTradingAllowed { get; set; }

        [JsonProperty("filters")]
        public List<Filter> Filters { get; set; }

        [JsonProperty("permissions")]
        public List<string> Permissions { get; set; }
    }

    public class BinanceExchangeInfo
    {
        [JsonProperty("timezone")]
        public string TimeZone { get; set; }

        [JsonProperty("serverTime")]
        public long ServerTime { get; set; }

        [JsonProperty("rateLimits")]
        public List<RateLimit> RateLimits { get; set; }

        [JsonProperty("ExchangeFilters")]
        public List<object> ExchangeFilters { get; set; }

        [JsonProperty("symbols")]
        public List<CryptoAsset> Symbols { get; set; }
    }
}