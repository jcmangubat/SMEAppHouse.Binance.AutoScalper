using Microsoft.EntityFrameworkCore;
using SMEAppHouse.Binance.AutoScalper2.Models;
using SMEAppHouse.Core.Patterns.EF.StrategyForDBCtxt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SMEAppHouse.Binance.AutoScalper2.DataAccess
{
    public class AppDbContext : AppDbContextExtended<AppDbContext>
    {
        #region constructor

        public AppDbContext()
            : this(new DbContextOptions<AppDbContext>())
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {

        }

        #endregion

        public virtual DbSet<TradePair> TradePairs { get; set; }
        public virtual DbSet<KLine> KLines { get; set; }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (modelBuilder == null)
                throw new ArgumentNullException(nameof(modelBuilder));

            var defDbSchema = "dbo";
            //if (AppConfig?.AppEFBehaviorAttributes != null
            //    && !string.IsNullOrEmpty(AppConfig?.AppEFBehaviorAttributes.DbSchema))
            //{
            //    defDbSchema = AppConfig.AppEFBehaviorAttributes.DbSchema;
            //    modelBuilder.HasDefaultSchema(AppConfig.AppEFBehaviorAttributes.DbSchema);
            //}

            modelBuilder.ApplyConfiguration(new TradePairModelCfg(defDbSchema));
            modelBuilder.ApplyConfiguration(new KLineModelCfg(defDbSchema));

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            const string company = "SMEAppHouse";
            const string clientOrProduct = "BinanceScalper";
            const string sqliteDb = "SMEAppHouse.BinanceScalper.sqlite";

            var dbPath = Path.Combine(appDataPath, $"{company}\\{clientOrProduct}");

            optionsBuilder.UseSqlite($"Data Source={Path.Combine(dbPath, sqliteDb)}");

            base.OnConfiguring(optionsBuilder);
        }
    }
}
