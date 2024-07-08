using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEAppHouse.Binance.AutoScalper.DataAccess
{
    /// <inheritdoc />
    /// <summary>
    /// 
    /// Reference: https://docs.microsoft.com/en-us/ef/core/miscellaneous/cli/powershell
    /// 
    ///     1. Add-Migration -Name InitialMigration -OutputDir Migrations -Context AppDbContext -Project BNS.MSDesigner.DA -StartupProject BNS.MSDesigner.Web.UI160119
    ///     2. Update-Database -Context AppDbContext -Project BNS.MSDesigner.DA -StartupProject BNS.MSDesigner.Web.UI160119
    /// 
    /// Optionals:
    /// 
    ///     3. Remove-Migration -Force -Context ClientDbContext -Project GPS.PFAS.ClientDataAccess -StartupProject GPS.PFAS.Web.API
    ///     4. Script-Migration -Context ClientDbContext -Project GPS.PFAS.ClientDataAccess -StartupProject BNS.TDSvc.Web
    ///     5. Drop-Database -Context ClientDbContext -Project GPS.PFAS.ClientDataAccess -StartupProject BNS.TDSvc.Web -Confirm A
    /// 
    /// To Rollback one migration backward:
    /// 
    ///     6. Remove-Migration -Force -Context ClientDbContext -Project GPS.PFAS.ClientDataAccess -StartupProject BNS.TDSvc.Web
    ///     7. Update-Database -Context ClientDbContext -Project GPS.PFAS.ClientDataAccess -StartupProject BNS.TDSvc.Web
    /// 
    /// </summary>
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

        public virtual DbSet<Product> Products { get; set; }


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

            modelBuilder.ApplyConfiguration(new ProductModelCfg(defDbSchema));

            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            const string company = "SMEAppHouse";
            const string client = "SEnterpriSYS.ListingsCrawler2";
            const string sqliteDb = "SEnterpriSYS.ListingsCrawler2.sqlite";

            var dbPath = Path.Combine(appDataPath, $"{company}\\{client}");

            optionsBuilder.UseSqlite($"Data Source={Path.Combine(dbPath, sqliteDb)}");

            base.OnConfiguring(optionsBuilder);
        }
    }
}
