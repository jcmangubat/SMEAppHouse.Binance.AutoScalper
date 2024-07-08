namespace SMEAppHouse.Binance.AutoScalper3.Models.Indicators
{
    public class MACD : Indicator
    {
        public decimal EMA12 { get; set; }
        public decimal EMA26 { get; set; }
        public decimal MACDValue { get; set; }
        public decimal Signal { get; set; }
        public decimal SignalOptimized { get; set; }
        public decimal Histogram { get; set; }

        public MACD()
        {
            RequiredTreshold = 34;
        }
    }
}

