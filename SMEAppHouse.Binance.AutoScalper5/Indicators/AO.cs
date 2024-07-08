namespace SMEAppHouse.Binance.AutoScalper3.Models.Indicators
{
    public class AO : Indicator
    {
        public decimal Midpoint { get; set; }
        public decimal SMA5 { get; set; }
        public decimal SMA34 { get; set; }
        public decimal AOValue { get; set; }

        public AO()
        {
            RequiredTreshold = 34;
        }
    }
}

