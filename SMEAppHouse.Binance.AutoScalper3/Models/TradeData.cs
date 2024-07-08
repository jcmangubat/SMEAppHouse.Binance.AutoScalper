using SMEAppHouse.Binance.AutoScalper3.Models.Indicators;
using System;
using System.Collections.Generic;
using System.Text;

namespace SMEAppHouse.Binance.AutoScalper3.Models
{
    public class TradeData
    {
        public DateTime ClosingDateTime { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal Low { get; set; }
        public decimal High { get; set; }
        public decimal Volume { get; set; }
        public IEnumerable<IIndicator> Indicators { get; set; }

        public override string ToString()
        {
            var txt = new StringBuilder();
            txt.Append($"o:{ClosingDateTime} ");
            txt.Append($"h:{ Math.Round(High, 3)} ");
            txt.Append($"l:{ Math.Round(Low, 3)} ");
            txt.Append($"o:{Math.Round(Open, 3)} ");
            txt.Append($"c:{ Math.Round(Close, 3)} ");
            txt.Append($"v:{ Math.Round(Volume, 3)} | ");

            return txt.ToString();
        }
    }




}

