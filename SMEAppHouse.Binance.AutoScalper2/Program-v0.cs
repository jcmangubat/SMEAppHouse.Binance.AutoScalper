using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.Enumerations;
using Microsoft.EntityFrameworkCore;
using SMEAppHouse.Binance.AutoScalper2.DataAccess;
using SMEAppHouse.Binance.AutoScalper2.Worker;
using SMEAppHouse.Core.TopshelfAdapter;
using SMEAppHouse.Core.TopshelfAdapter.Aggregation;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Topshelf;

namespace SMEAppHouse.Binance.AutoScalper2
{
    class Program
    {
        enum TradeActionsEnum
        {
            NONE,
            BUYING,
            SELLING
        }

        private const double PRICE_TRADE_LIMIT = 0.70;

        private static readonly bool[] BuyingPatternValues = new bool[] { true, true, true, false };
        private static readonly bool[] SellingPatternValues = new bool[] { true, true, true, false };

        private static TradeActionsEnum _currentTradeAction = TradeActionsEnum.NONE;
        private static double _currentTradeValue = 0.0;

        static void Main(string[] args)
        {
            InitDb();

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
                x.SetDescription("SMEAppHouse Binance Auto Trading Scalper(v1.0)");
                x.SetServiceName("SMEAppHouse.Binance.AutoScaler");

                x.StartAutomatically();
                x.RunAsLocalSystem();
                x.EnablePauseAndContinue();

                x.EnableServiceRecovery(r =>
                {
                    //should this be true for crashed or non-zero exits
                    r.OnCrashOnly();
                    r.RestartService(1); //first; restart a minute after the crash
                    r.RestartService(1); //second
                    r.RestartService(1); //subsequents
                    r.SetResetPeriod(0);
                });

            });
        }

        #region other callbacks/methods

        private static void SvcController_OnServiceWorkerInitialized(object sender, ServiceWorkerInitializedEventArgs e)
        {
            var typ = e.ServiceWorker.GetType();
            e.ServiceWorker.NLog($"Service initialized: {typ.Name} !");
        }

        private static ServiceController MakeServiceController()
        {
            var svcController = new ServiceController();
            svcController.OnServiceWorkerInitialized += SvcController_OnServiceWorkerInitialized; ;

            var unixTimerService = new UnixTimerService();
            unixTimerService.OnTickCallback += UnixTimerService_OnTickCallback;
            ITopshelfClientExt initUnixTimerService() => unixTimerService;
            svcController.ServiceWorkers.Add(initUnixTimerService());

            return svcController;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static async void UnixTimerService_OnTickCallback(object sender, UnixTimerService.TickCallbackEventArgs e)
        {
            var closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string localTime;

            if (DateTime.Now.Second == 5)
            {
                //Get KLines/Candlestick data for given Trade Pair
                var symbol = "XRPUSDT";
                var publicRestClient = new BinanceClient();
                var kLines = await publicRestClient.KLines(symbol, KlineInterval.Minute1, SellingPatternValues.Length);
                var closingPrices = kLines.OrderBy(p => p.CloseTime).Select(p => (double)p.Close).ToArray();

                EvaluateTradingAction(closingPrices, (trdAction, closingPrice) =>
                {
                    if ((trdAction == TradeActionsEnum.BUYING && _currentTradeAction != TradeActionsEnum.BUYING) ||
                        (trdAction == TradeActionsEnum.SELLING && _currentTradeAction != TradeActionsEnum.SELLING))
                    {
                        foreach (var item in kLines)
                        {
                            localTime = closingStartTime
                                            .AddMilliseconds(item.CloseTime)
                                            .ToLocalTime()
                                            .Subtract(TimeSpan.FromMinutes(1))
                                            .ToString("MM/dd/yyyy HH:mm");
                            Console.WriteLine($"Time:  {localTime} --> Close: {item.Close} ");
                        }
                        Console.WriteLine($"--> Identified trading: {trdAction} closing @ {closingPrice}\r\n{new string('-', 60)}");

                        _currentTradeAction = trdAction;
                        _currentTradeValue = closingPrice;
                    }
                });
            }

            /*localTime = DateTime.Now.ToLocalTime().ToString("MM/dd/yyyy HH:mm:ss");
            Console.WriteLine($"Time:  {localTime}");*/
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="closingPrices"></param>
        /// <param name="postCallback"></param>
        private static void EvaluateTradingAction(double[] closingPrices, Action<TradeActionsEnum, double> postCallback)
        {
            var tradingAction = IdentifyTradeAction(closingPrices);

            // to do perform validations

            if (tradingAction.tradeAction != TradeActionsEnum.NONE)
                postCallback?.Invoke(tradingAction.tradeAction, tradingAction.closingPrice);
        }

        #region trading callbacks/methods

        private static (bool inBuyPattern, double closingPrice) InBuyPattern(double[] closingPrices)
        {
            var cPrices = closingPrices.Reverse().Take(BuyingPatternValues.Length).Reverse().ToArray();

            if (cPrices.Length < BuyingPatternValues.Length)
                return (false, 0);

            var acquiredPatterns = new bool[BuyingPatternValues.Length];
            var prvPrc = -909090909.0;
            foreach ((bool v, int idx) in BuyingPatternValues.Select((value, idx) => (value, idx)))
            {
                if (prvPrc > -909090909.0)
                    acquiredPatterns[idx - 1] = prvPrc > cPrices[idx];

                prvPrc = cPrices[idx];
            }

            return (Enumerable.SequenceEqual(BuyingPatternValues, acquiredPatterns), cPrices[cPrices.Length - 1]);
        }

        private static (bool inSellPattern, double closingPrice) InSellPattern(double[] closingPrices)
        {
            var cPrices = closingPrices.Reverse().Take(SellingPatternValues.Length).Reverse().ToArray();

            if (cPrices.Length < SellingPatternValues.Length)
                return (false, 0);

            var acquiredPatterns = new bool[SellingPatternValues.Length];
            var prvPrc = -909090909.0;
            foreach ((bool v, int idx) in SellingPatternValues.Select((value, idx) => (value, idx)))
            {
                if (prvPrc > -909090909.0)
                    acquiredPatterns[idx - 1] = prvPrc < cPrices[idx];

                prvPrc = cPrices[idx];
            }

            return (Enumerable.SequenceEqual(SellingPatternValues, acquiredPatterns), cPrices[cPrices.Length - 1]);
        }

        private static (TradeActionsEnum tradeAction, double closingPrice) IdentifyTradeAction(double[] closingPrices)
        {
            var checkSelling = InSellPattern(closingPrices);
            if (checkSelling.inSellPattern)
                return (TradeActionsEnum.SELLING, checkSelling.closingPrice);

            var checkBuying = InBuyPattern(closingPrices);
            if (checkBuying.inBuyPattern)
                return (TradeActionsEnum.BUYING, checkBuying.closingPrice);

            return (TradeActionsEnum.NONE, 0);
        }

        #endregion

    }
}
