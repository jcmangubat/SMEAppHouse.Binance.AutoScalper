using Newtonsoft.Json;
using SMEAppHouse.Binance.AutoScalper4.Models;
using SMEAppHouse.Core.TopshelfAdapter.Aggregation;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using Topshelf;

namespace SMEAppHouse.Binance.AutoScalper4
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.Unicode;
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
            TradeBrokerConfigurations tradeBrokerConfigurations = null;
            using (var file = File.OpenText(@"trade-pair-settings.json"))
            {
                var serializer = new JsonSerializer();
                tradeBrokerConfigurations = (TradeBrokerConfigurations)serializer.Deserialize(file, typeof(TradeBrokerConfigurations));
            }

            var apiKeys = LoadApiKeys();
            var symbols = tradeBrokerConfigurations
                                .TraderBrokerSettings
                                .Where(p => p.Active)
                                .Select(p => p.Symbol)
                                .ToArray();

            var pairs = tradeBrokerConfigurations
                                .TraderBrokerSettings
                                .Where(p => p.Active)
                                .Select(p => p.Pair)
                                .Distinct()
                                .ToArray();

            symbols = symbols.Concat(pairs).ToArray();

            WalletManager.Instance.OnCryptoAssetActive += Instance_OnCryptoAssetActive; ;
            WalletManager.Instance.Init(apiKeys, symbols);
            WalletManager.Instance.RefreshAsync().Wait();

            var ethTraderBrokerSettings = tradeBrokerConfigurations.TraderBrokerSettings.Find(p => p.TradePair.ToString().Equals("ETHUSDT"));
            
            //_traderBrokerService = new TradeBrokerService(apiKeys, ethTraderBrokerSettings);
            //_traderBrokerService.OnTickCallback += _traderService_OnTickCallback; ;
            //_traderBrokerService.OnMACDPatternIdentified += _traderService_OnMACDPatternIdentified;
            //_traderBrokerService.OnMACDPatternAnalyzed += _traderBrokerService_OnMACDPatternAnalyzed;
            //_traderBrokerService.OnNewOrderPushed += _traderBrokerService_OnNewOrderPushed;
            //_traderBrokerService.OnNewOrderDenied += _traderBrokerService_OnNewOrderDenied;

            var svcController = new ServiceController();
            //svcController.OnServiceWorkerInitialized += SvcController_OnServiceWorkerInitialized; ;
            //svcController.ServiceWorkers.Add(_traderBrokerService);
            return svcController;
        }

        private static void Instance_OnCryptoAssetActive(object sender, WalletManager.CryptoAssetActiveEventArgs e)
        {
            Console.WriteLine($" Trading active currency: {e.Symbol}");
        }

        /*private static void _traderBrokerService_OnNewOrderDenied(object sender, TradeBrokerService.NewOrderDeniedEventArgs e)
        {
            var statMsg = $"\rOrder denied. Details: {e.TradeAction} {e.TradePair.Symbol} - {e.ErrorMessage}";
            Console.WriteLine(statMsg);
        }*/

        /*private static void _traderBrokerService_OnNewOrderPushed(object sender, TradeBrokerService.NewOrderPushedEventArgs e)
        {
            if (e.TradeAction == Rules.TradeActionsEnum.SELL)
            {
                var player = new SoundPlayer(@"Media\MONEYWIN.wav");
                player.Play();
            }
            else if (e.TradeAction == Rules.TradeActionsEnum.BUY)
            {
                var player = new SoundPlayer(@"Media\UIAlert_Idea 2 (ID 1399)_BSB.wav");
                player.Play();
            }

            var statMsg = $"\rNew order pushed to {e.TradeAction} {e.TradePair.Symbol}! Accepted with Id: {e.OrderResponse.OrderId} @ Time: {Extensions.AsDateTime(e.OrderResponse.TransactionTime).ToLocalTime():g}";
            if (e.TradeAction == Rules.TradeActionsEnum.SELL)
            {
                var nwAssetVal = WalletManager.Instance.CryptoAssets[e.TradePair.Pair].Balance;
                statMsg += $"\r\nNew asset value: {nwAssetVal}";
            }
            Console.WriteLine(statMsg);
        }*/
    }
}
