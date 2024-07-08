using M3C.Finance.BinanceSdk.Enumerations;
using SMEAppHouse.Binance.AutoScalper5.Models;
using SMEAppHouse.Binance.AutoScalper5.Services;
using SMEAppHouse.Core.CodeKits.Helpers;
using SMEAppHouse.Core.TopshelfAdapter.Aggregation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;
using Topshelf;

namespace SMEAppHouse.Binance.AutoScalper5
{
    class Program
    {
        static List<TradePairSetting> _tradePairSettings = null;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
            _tradePairSettings = JsonHelper.ReadJson<List<TradePairSetting>>("trade-pair-settings.json");

            InitTopshelfWrapper();
        }

        private static BinanceApiKeys LoadApiKeys()
        {
            return new BinanceApiKeys
            {
                ApiKey = ConfigurationManager.AppSettings["BinanceApiKey"],
                ApiSecret = ConfigurationManager.AppSettings["BinanceApiSecret"]
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private static void InitTopshelfWrapper()
        {
            HostFactory.Run(x =>
            {
                x.Service<ServiceController>(cfg =>
                {
                    cfg.ConstructUsing(hostSettings => InitServiceController());

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

        private static ServiceController InitServiceController()
        {
            var svcController = new ServiceController();
            svcController.OnServiceWorkerInitialized += SvcController_OnServiceWorkerInitialized;

            foreach (var tradePairSetting in _tradePairSettings)
            {
                var tradeMrktDataAgent = new TradeMarketDataAgent(tradePairSetting);
                svcController.ServiceWorkers.Add(tradeMrktDataAgent);
            }

            return svcController;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void SvcController_OnServiceWorkerInitialized(object sender, ServiceWorkerInitializedEventArgs e)
        {
            var typ = e.ServiceWorker.GetType();
            e.ServiceWorker.NLog($"Service initialized: {typ.Name} !");

            var apiKeys = LoadApiKeys();
            var symbols = _tradePairSettings
                                .Where(p => p.Active)
                                .Select(p => p.Symbol)
                                .ToArray();

            var pairs = _tradePairSettings
                                .Where(p => p.Active)
                                .Select(p => p.Pair)
                                .Distinct()
                                .ToArray();

            symbols = symbols.Concat(pairs).ToArray();

            WalletManager.Instance.OnCryptoAssetActive += Instance_OnCryptoAssetActive; ;
            WalletManager.Instance.Initialize(apiKeys, symbols);
            WalletManager.Instance.RefreshAsync().Wait();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Instance_OnCryptoAssetActive(object sender, WalletManager.CryptoAssetActiveEventArgs e)
        {
            Console.WriteLine($" Trading active currency: {e.Symbol}");
        }
    }
}
