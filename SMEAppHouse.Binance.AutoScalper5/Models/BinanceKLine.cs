using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SMEAppHouse.Binance.AutoScalper5.Models
{
    public class KLine
    {
        private readonly DateTime _unixDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public long UnixClosingDateTime { get; set; }
        public decimal Open { get; internal set; }
        public decimal Close { get; set; }
        public decimal Low { get; internal set; }
        public decimal High { get; internal set; }
        public decimal Volume { get; set; }

        [NotMapped]
        public DateTime LocalDateTime => _unixDateTime.AddMilliseconds(UnixClosingDateTime).ToLocalTime();

        [NotMapped]
        public string LocalDateTimeString => LocalDateTime.ToString("MM/dd/YYYY HH:mm");

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public TradeData ToTradingData() => new()
        {
            ClosingDateTime = LocalDateTime,
            Open = Open,
            Close = Close,
            Low = Low,
            High = High,
            Volume = Volume
        };

    }
}
