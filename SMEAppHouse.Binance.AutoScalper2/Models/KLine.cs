using SMEAppHouse.Core.Patterns.EF.ModelComposite.VariationModels;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class KLine : IntKeyedEntity
    {
        private readonly DateTime _unixDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public long UnixClosingDateTime { get; set; }
        public double ClosingValue { get; set; }

        [NotMapped]
        public string LocalDateTime => _unixDateTime.AddMilliseconds(UnixClosingDateTime).ToLocalTime().ToString("MM/dd/YYYY HH:mm");


        #region foreign key

        public virtual TradePair TradePair { get; set; }
        public int TradePairId { get; set; }

        #endregion
    }


}
