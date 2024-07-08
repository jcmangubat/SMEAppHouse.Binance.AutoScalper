namespace SMEAppHouse.Binance.AutoScalper3.Models.Indicators
{
    public interface IIndicator
    {
        public int RequiredTreshold { get; }
    }

    public class Indicator : IIndicator
    {
        public int RequiredTreshold { get; private protected set; }
    }
}

