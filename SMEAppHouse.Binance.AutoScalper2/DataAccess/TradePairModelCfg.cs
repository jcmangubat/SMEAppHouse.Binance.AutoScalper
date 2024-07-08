using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEAppHouse.Binance.AutoScalper2.Models;
using SMEAppHouse.Core.Patterns.EF.Helpers;
using SMEAppHouse.Core.Patterns.EF.StrategyForModelCfg;

namespace SMEAppHouse.Binance.AutoScalper2.DataAccess
{
    public class TradePairModelCfg : EntityConfigurationBase<TradePair, int>
    {
        public TradePairModelCfg(string schema = "dbo")
            : base("TradePairs", true, false, schema)
        {

        }

        public override void Map(EntityTypeBuilder<TradePair> entityBuilder)
        {
            entityBuilder.DefineDbField(x => x.Name, true);
            
            entityBuilder.HasMany(p => p.KLines)
                .WithOne(p => p.TradePair)
                .HasForeignKey(b => b.TradePairId)
                .IsRequired(true);

            base.Map(entityBuilder);
        }
    }
}
