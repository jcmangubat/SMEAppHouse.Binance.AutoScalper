namespace SMEAppHouse.Binance.AutoScalper3.Models.Indicators
{
    public class VWAP : Indicator
    {
        public decimal VolumeXClose { get; set; }
        public decimal VWAP13 { get; set; }
        public decimal VWAP20 { get; set; }

        public VWAP()
        {
            RequiredTreshold = 20;
        }
    }
}

