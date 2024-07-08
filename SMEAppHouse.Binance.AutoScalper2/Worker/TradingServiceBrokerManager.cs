using System;
using System.Collections.Generic;
using System.Linq;
using SMEAppHouse.Binance.AutoScalper2.Models;
using static SMEAppHouse.Binance.AutoScalper2.Models.Rules;

namespace SMEAppHouse.Binance.AutoScalper2.Worker
{
    public class TradingServiceBrokerManager
    {
        public List<TradingServiceBroker> TradingServiceBrokers { get; set; }

        public event TradingCheckedCallbackEventHandler OnTradingCheckedCallback;

        private string _apiKey;
        private string _apiSecret;
        private TradeBrokerConfigurations _configurations;
        private List<TradingServiceBroker> _tradingServiceBrokers;

        public TradingServiceBrokerManager(string apiKey, string apiSecret, TradeBrokerConfigurations configurations)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;
            _configurations = configurations;
            _tradingServiceBrokers = new List<TradingServiceBroker>();

            _configurations.TraderBrokerSettings.ForEach(brokerBehaviorSetting =>
            {
                var broker = new TradingServiceBroker(_apiKey, _apiSecret, brokerBehaviorSetting);
                broker.OnTradingCheckedCallback += Broker_OnTradingCheckedCallback;

                _tradingServiceBrokers.Add(broker);
            });

        }

        private void Broker_OnTradingCheckedCallback(object sender, TradingCheckedCallbackEventArgs e)
        {
            OnTradingCheckedCallback?.Invoke(this, e);
        }

        /* public void CheckTrades()
         {
             var checkQueueToken = Guid.NewGuid();
             _tradingServiceBrokers.ForEach(broker =>
             {
                 broker.CheckQueueToken = checkQueueToken;
             });

             while (_tradingServiceBrokers.Exists(p => p.CheckQueueToken == checkQueueToken && p.BrokerSettings.Active))
             {
                 var numberQueryingKLines = _tradingServiceBrokers.Count(p => p.ApiQueryingKLines);
                 if (numberQueryingKLines >= 3)
                     continue;

                 var brokers = _tradingServiceBrokers
                                 .Where(p => p.CheckQueueToken == checkQueueToken && p.BrokerSettings.Active)
                                 .Take(5)
                                 .ToList();

                 brokers.ForEach(async broker =>
                 {
                     if (broker.BrokerSettings.Active)
                         await broker.TryTradeAsync();
                 });
             }
         }*/

        public void CheckTrades()
        {
            _tradingServiceBrokers.ForEach(async broker =>
            {
                if (broker.BrokerSettings.Active)
                    await broker.TryTradeAsync();
            });
        }
    }

}
