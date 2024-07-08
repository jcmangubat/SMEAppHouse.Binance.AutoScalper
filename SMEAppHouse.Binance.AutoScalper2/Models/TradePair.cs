using SMEAppHouse.Core.Patterns.EF.ModelComposite.VariationModels;
using System.Collections.Generic;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class TradePair : IntKeyedEntity
    {
        public string Name { get; set; }
        public virtual IList<KLine> KLines { get; set; }
    }
}
