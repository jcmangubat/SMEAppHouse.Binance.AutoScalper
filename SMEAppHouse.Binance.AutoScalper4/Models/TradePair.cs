using System.Text.Json.Serialization;

namespace SMEAppHouse.Binance.AutoScalper4.Models
{
    public class TradePair
    {
        public string Symbol { get; set; }
        public string Pair { get; set; }

        public TradePair(string symbol, string pair)
        {
            Symbol = symbol;
            Pair = pair;
        }

        public override string ToString()
        {
            return $"{Symbol.Trim().ToUpper()}{Pair.Trim().ToUpper()}";
        }
    }

}
