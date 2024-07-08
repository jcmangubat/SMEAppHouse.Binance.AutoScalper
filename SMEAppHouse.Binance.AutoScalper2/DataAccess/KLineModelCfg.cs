using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SMEAppHouse.Binance.AutoScalper2.Models;
using SMEAppHouse.Core.Patterns.EF.Helpers;
using SMEAppHouse.Core.Patterns.EF.StrategyForModelCfg;

namespace SMEAppHouse.Binance.AutoScalper2.DataAccess
{
    public class KLineModelCfg : EntityConfigurationBase<KLine, int>
    {
        public KLineModelCfg(string schema = "dbo")
            : base("KLines", true, false, schema)
        {

        }

        public override void Map(EntityTypeBuilder<KLine> entityBuilder)
        {
            entityBuilder.DefineDbField(x => x.UnixClosingDateTime, true);
            entityBuilder.DefineDbField(x => x.ClosingValue, true);

            base.Map(entityBuilder);
        }
    }
}
