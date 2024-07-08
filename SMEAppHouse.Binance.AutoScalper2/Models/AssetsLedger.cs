using M3C.Finance.BinanceSdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SMEAppHouse.Binance.AutoScalper2.Models.Rules;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class AssetsLedger
    {
        private static readonly Lazy<AssetsLedger> Lazy = new(() => new AssetsLedger());

        private bool _initialized = false;

        private string _apiKey;
        private string _apiSecret;

        public static AssetsLedger Instance => Lazy.Value;

        public Dictionary<string, CryptoAsset> CryptoAssets { get; private set; }

        public event CryptoAssetActiveEventHandler OnCryptoAssetActive = delegate { };
        public event CryptoAssetUpdatedEventHandler OnCryptoAssetUpdated = delegate { };

        public void Init(string apiKey, string apiSecret, string[] cryptoSymbols)
        {
            _apiKey = apiKey;
            _apiSecret = apiSecret;

            CryptoAssets = new Dictionary<string, CryptoAsset>();
            foreach (var symbol in cryptoSymbols)
            {
                CryptoAssets.Add(symbol, new CryptoAsset(symbol));
                OnCryptoAssetActive?.Invoke(this, new CryptoAssetActiveEventArgs(symbol));
            }

            _initialized = true;
        }

        public void ForEachInAsset(Action<CryptoAsset> forEachAction)
        {
            foreach (var crypto in CryptoAssets)
            {
                forEachAction?.Invoke(crypto.Value);
            }
        }

        public async Task RefreshAsync()
        {
            if (!_initialized)
                throw new Exception($"{nameof(AssetsLedger)}: Needs initialized.");

            var restClient = new BinanceClient(_apiKey, _apiSecret);

            //Get Account Info and Current Balances
            var accountInfo = await restClient.GetAccountInfo(true);

            foreach (var balanceItem in accountInfo.Balances)
            {
                if (!CryptoAssets.ContainsKey(balanceItem.Asset))
                    continue;

                var crypto = CryptoAssets[balanceItem.Asset];
                if (crypto == null)
                    continue;

                crypto.Balance = (double)balanceItem.Free;
                crypto.Locked = (double)balanceItem.Locked;

                OnCryptoAssetUpdated?.Invoke(this, new CryptoAssetUpdatedEventArgs(balanceItem.Asset, (double)balanceItem.Free, (double)balanceItem.Locked));
            }
        }

        public class CryptoAsset
        {
            public string Symbol { get; private set; }
            public double Balance { get; set; }
            public double Locked { get; set; }
            public double LastHoldPrice { get; set; }        
            public TradeActionsEnum LastAction { get; set; }
            public DateTime LastActionDateTime { get; set; }
            public CryptoAsset(string symbol)
            {
                Symbol = symbol;
                LastAction = TradeActionsEnum.NONE;
            }
        }
    }

    #region event handlers

    public class CryptoAssetActiveEventArgs : EventArgs
    {
        public string Symbol{ get; private set; }
        
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
