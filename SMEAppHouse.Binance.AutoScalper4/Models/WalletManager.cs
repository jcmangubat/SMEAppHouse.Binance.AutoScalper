using M3C.Finance.BinanceSdk;
using M3C.Finance.BinanceSdk.ResponseObjects;
using Newtonsoft.Json;
using RestSharp;
using SMEAppHouse.Core.CodeKits.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SMEAppHouse.Binance.AutoScalper4.Models.Rules;

namespace SMEAppHouse.Binance.AutoScalper4.Models
{
    public class WalletManager
    {
        private static readonly Lazy<WalletManager> Lazy = new(() => new WalletManager());

        private bool _initialized = false;

        private BinanceApiKeys _apiKeys;

        public static WalletManager Instance => Lazy.Value;

        public Dictionary<string, CryptoAsset> CryptoAssets { get; private set; }

        public BinanceExchangeInfo ExchangeInfo { get; set; }

        public event CryptoAssetActiveEventHandler OnCryptoAssetActive = delegate { };
        public event CryptoAssetUpdatedEventHandler OnCryptoAssetUpdated = delegate { };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="apiSecret"></param>
        /// <param name="cryptoSymbols"></param>
        /// <returns></returns>
        public WalletManager Init(BinanceApiKeys apiKeys, string[] cryptoSymbols)
        {
            _apiKeys = apiKeys;

            CryptoAssets = new Dictionary<string, CryptoAsset>();
            foreach (var symbol in cryptoSymbols)
            {
                CryptoAssets.Add(symbol, new CryptoAsset(symbol));
                OnCryptoAssetActive?.Invoke(this, new CryptoAssetActiveEventArgs(symbol));
            }

            ExchangeInfo = GetBinanceExchangeInfo();
            var cryptExcInf = WalletManager.Instance.ExchangeInfo.Symbols.FirstOrDefault(p => p.BaseAsset.Equals("ETH") && p.QuoteAsset.Equals("USDT"));

            _initialized = true;

            return Lazy.Value;
        }

        private static BinanceExchangeInfo GetBinanceExchangeInfo()
        {
            var client = new RestClient("https://www.binance.com/api/v1/exchangeInfo")
            {
                Timeout = -1
            };
            var request = new RestRequest(Method.GET);
            request.AddHeader("Content-Type", "application/json");
            IRestResponse response = client.Execute(request);

            return JsonConvert.DeserializeObject<BinanceExchangeInfo>(response.Content);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="forEachAction"></param>
        public void ForEachInAsset(Action<CryptoAsset> forEachAction)
        {
            if (!_initialized)
                throw new Exception($"{nameof(WalletManager)}: Needs initialized.");

            foreach (var crypto in CryptoAssets)
            {
                forEachAction?.Invoke(crypto.Value);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> RefreshAsync(bool throwException=true)
        {
            if (!_initialized)
                throw new Exception($"{nameof(WalletManager)}: Needs initialized.");

            var restClient = new BinanceClient(_apiKeys.ApiKey, _apiKeys.ApiSecret);

            //Get Account Info and Current Balances
            var retryInterval = new TimeSpan(0, 0, 0, 30);
            AccountResponse accountInfo = await RetryCodeKit.Do(async () => await restClient.GetAccountInfo(true), retryInterval);

            if (accountInfo == null)
            {
                if (throwException)
                    throw new Exception("Failed to fetch Binance account info");
                else return false;
            }

            foreach (var balanceItem in accountInfo.Balances)
            {
                if (!CryptoAssets.ContainsKey(balanceItem.Asset))
                    continue;

                var crypto = CryptoAssets[balanceItem.Asset];
                if (crypto == null)
                    continue;

                crypto.Balance = balanceItem.Free;
                crypto.Locked = balanceItem.Locked;

                OnCryptoAssetUpdated?.Invoke(this, new CryptoAssetUpdatedEventArgs(balanceItem.Asset, (double)balanceItem.Free, (double)balanceItem.Locked));
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        public class CryptoAsset
        {
            public string Symbol { get; private set; }
            public decimal Balance { get; set; }
            public decimal Locked { get; set; }
            public decimal LastTradePrice { get; set; }
            public TradeActionsEnum LastAction { get; set; }
            public DateTime LastActionDateTime { get; set; }

            public CryptoAsset(string symbol)
            {
                Symbol = symbol;
                LastAction = TradeActionsEnum.NONE;
            }
        }

        #region event handlers

        public class CryptoAssetActiveEventArgs : EventArgs
        {
            public string Symbol { get; private set; }

            public CryptoAssetActiveEventArgs(string symbol)
            {
                Symbol = symbol;
            }
        }

        public class CryptoAssetUpdatedEventArgs : EventArgs
        {
            public string Symbol { get; private set; }
            public double Balance { get; private set; }
            public double Locked { get; private set; }

            public CryptoAssetUpdatedEventArgs(string symbol, double balance, double locked)
            {
                Symbol = symbol;
                Balance = balance;
                Locked = locked;
            }
        }

        public delegate void CryptoAssetActiveEventHandler(object sender, CryptoAssetActiveEventArgs e);
        public delegate void CryptoAssetUpdatedEventHandler(object sender, CryptoAssetUpdatedEventArgs e);

        #endregion
    }


}
