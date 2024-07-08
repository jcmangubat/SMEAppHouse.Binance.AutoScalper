using System.Collections.Generic;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class TradingPatterns
    {
        public string TradePair { get; set; }
        public List<bool[]> BuyDipFlags { get; set; }
        public List<bool[]> SellPeekFlags { get; set; }
        public List<bool[]> BuyAgainFlags { get; set; }
        public List<bool[]> SellAgainFlags { get; set; }
    }
}
