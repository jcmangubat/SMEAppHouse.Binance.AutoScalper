using M3C.Finance.BinanceSdk;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SMEAppHouse.Binance.AutoScalper2.DataAccess;
using SMEAppHouse.Binance.AutoScalper2.Models;
using SMEAppHouse.Binance.AutoScalper2.Worker;
using SMEAppHouse.Core.TopshelfAdapter;
using SMEAppHouse.Core.TopshelfAdapter.Aggregation;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using Topshelf;

namespace SMEAppHouse.Binance.AutoScalper2
{
    class Program
    {
        private static TradingServiceBrokerManager _tradingServiceBrokerManager;
        private static UnixTimerService _unixTimerService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            InitDb();
            InitTopshelfWrapper();
        }

        #region private methods

        /// <summary>
        /// 
        /// </summary>
        private static void InitTopshelfWrapper()
        {
            HostFactory.Run(x =>
            {
                x.Service<ServiceController>(cfg =>
                {
                    cfg.ConstructUsing(hostSettings => MakeServiceController());

                    cfg.WhenStarted(svcCtrlr =>
                    {
                        bool StartSrvc()
                        {
                            var myThread = new Thread(svcCtrlr.ResumeAll)
                            {
                                IsBackground = true
                            };
                            myThread.Start();
                            return true;
                        }
                        StartSrvc();
                    });

                    cfg.WhenStopped(svcCtrlr =>
                    {
                        bool HaltSrvc()
                        {
                            var myThread = new Thread(svcCtrlr.HaltAll)
                            {
                                IsBackground = true
                            };
                            myThread.Start();
                            return true;
                        }
                        HaltSrvc();
                    });

                    cfg.WhenShutdown(svcCtrlr =>
                    {
                        bool StopSrvc()
                        {
                            var myThread = new Thread(svcCtrlr.ShutdownAll)
                            {
                                IsBackground = true
                            };
                            myThread.Start();
                            return true;
                        }
                        StopSrvc();
                    });

                });

                x.SetDisplayName("SMEAppHouse Binance Auto Trading Scalper (v1.0)");
                x.SetDescription("SMEAppHouse Binance Auto Trading Scalper (v1.0)");
                x.SetServiceName("SMEAppHouse.Binance.AutoScalper");

                x.StartAutomatically();
                x.RunAsLocalSystem();
                x.EnablePauseAndContinue();

                x.EnableServiceRecovery(r =>
                {
                    //This should be true for crashed or non-zero exits
                    r.OnCrashOnly();
                    r.RestartService(1); //first; restart a minute after the crash
                    r.RestartService(1); //second
                    r.RestartService(1); //subsequents
                    r.SetResetPeriod(0);
                });
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="secondsTrigger"></param>
        /// <returns></returns>
        private static ServiceController MakeServiceController(int secondsTrigger = 0)
        {
            var svcController = new ServiceController();
            svcController.OnServiceWorkerInitialized += SvcController_OnServiceWorkerInitialized; ;

            _unixTimerService = new UnixTimerService();
            _unixTimerService.OnTickCallback += UnixTimerService_OnTickCallback;

            svcController.ServiceWorkers.Add(_unixTimerService);

            InitTraderBroker();

            return svcController;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static void InitTraderBroker()
        {
            TradeBrokerConfigurations _tradeBrokerConfigurations = null;
            var apiKey = ConfigurationManager.AppSettings["BinanceApiKey"];
            var apiSecret = ConfigurationManager.AppSettings["BinanceApiSecret"];

            using (StreamReader file = File.OpenText(@"trader-broker.json"))
            {
                var serializer = new JsonSerializer();
                _tradeBrokerConfigurations = (TradeBrokerConfigurations)serializer.Deserialize(file, typeof(TradeBrokerConfigurations));
            }

            var symbols = _tradeBrokerConfigurations
                                .TraderBrokerSettings
                                .Where(p => p.Active)
                                .Select(p => p.Symbol)
                                .ToArray();

            var pairs = _tradeBrokerConfigurations
                                .TraderBrokerSettings
                                .Where(p => p.Active)
                                .Select(p => p.Pair)
                                .Distinct()
                                .ToArray();

            symbols = symbols.Concat(pairs).ToArray();

            AssetsLedger.Instance.OnCryptoAssetActive += Instance_OnCryptoAssetActive;
            AssetsLedger.Instance.Init(apiKey, apiSecret, symbols);

            // refresh the balances direct from Binance;
            AssetsLedger.Instance.RefreshAsync().Wait();

            // for now: mock the purchase currency values to 1000 for testing purpose only.
            foreach (var pair in pairs)
            {
                AssetsLedger.Instance.CryptoAssets[pair].Balance = 1000;
            }

            _tradingServiceBrokerManager = new TradingServiceBrokerManager(apiKey, apiSecret, _tradeBrokerConfigurations);
            _tradingServiceBrokerManager.OnTradingCheckedCallback += _tradingServiceBrokerManager_OnTradingCheckedCallback;
        }

        private static void InitDb()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

            const string company = "SMEAppHouse";
            const string clientOrProduct = "BinanceScalper";
            const string sqliteDb = "SMEAppHouse.BinanceScalper.sqlite";

            var dbPath = Path.Combine(appDataPath, $"{company}\\{clientOrProduct}");

            if (!Directory.Exists(dbPath))
                Directory.CreateDirectory(dbPath);

            if (!File.Exists(Path.Combine(dbPath, sqliteDb)))
                File.Copy(sqliteDb, Path.Combine(dbPath, sqliteDb));

            using var dbContext = new AppDbContext();
            dbContext.Database.Migrate();
            dbContext.Database.EnsureCreated();
        }


        #endregion

        #region event callbacks

        private static void Instance_OnCryptoAssetActive(object sender, CryptoAssetActiveEventArgs e)
        {
            Console.WriteLine($" Trading active currency: {e.Symbol}");
        }

        private static void _tradingServiceBrokerManager_OnTradingCheckedCallback(object sender, TradingCheckedCallbackEventArgs e)
        {
            if (e.TradeAction != Rules.TradeActionsEnum.NONE)
            {
                var asset = AssetsLedger.Instance.CryptoAssets[e.TradePair.symbol];
                if (asset != null)
                {
                    var details = $"@ {asset.LastHoldPrice}; New balance: ${asset.Balance} Pattern: {string.Join('-', e.Pattern)}";
                    Console.WriteLine($" Trading checked, {e.TradePair}:, {e.TradeAction} {details}");
                }
            }
            else if (!string.IsNullOrEmpty(e.NotTradingReason))
            {
                Console.WriteLine($" \r\nTrading faulted, {e.TradePair}: {e.NotTradingReason}");
            }

            if (e.TradeAction == Rules.TradeActionsEnum.SELLING || e.TradeAction == Rules.TradeActionsEnum.SELLBACK)
            {
                SoundPlayer player = new SoundPlayer(@"Media\MONEYWIN.wav");
                player.Play();
            }
            else if (e.TradeAction == Rules.TradeActionsEnum.BUYING || e.TradeAction == Rules.TradeActionsEnum.BUYBACK)
            {
                SoundPlayer player = new SoundPlayer(@"Media\UIAlert_Idea 2 (ID 1399)_BSB.wav");
                player.Play();
            }

        }


        private static void SvcController_OnServiceWorkerInitialized(object sender, ServiceWorkerInitializedEventArgs e)
        {
            var typ = e.ServiceWorker.GetType();
            e.ServiceWorker.NLog($"Service initialized: {typ.Name} !");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void UnixTimerService_OnTickCallback(object sender, UnixTimerService.TickCallbackEventArgs e)
        {
            Console.Write($" ({60 - e.DateTime.Second}) Time: {e.DateTime}");
            _tradingServiceBrokerManager.CheckTrades();
        }

        #endregion
    }
}
