using System;
using System.Collections.Generic;
using System.Linq;
using M3C.Finance.BinanceSdk.ResponseObjects;
using MathNet.Numerics.Statistics;
using SMEAppHouse.Core.CodeKits.Data;

namespace SMEAppHouse.Binance.AutoScalper2.Models
{
    public class KLineItem
    {
        private readonly DateTime _closingStartTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public long ClosingDateUnix { get; set; }
        public double ClosingPrice { get; set; }

        public DateTime ClosingDateLocal => _closingStartTime
                                        .AddMilliseconds(ClosingDateUnix)
                                        .ToLocalTime()
                                        .Subtract(TimeSpan.FromMinutes(1));

        public class KLineItems : List<KLineItem>
        {
            public void Refresh(IEnumerable<KLinesResponseItem> klineItems)
            {
                this.Clear();
                klineItems.ForEach(kl =>
                {
                    this.Add(new KLineItem()
                    {
                        ClosingDateUnix = kl.CloseTime,
                        ClosingPrice = (double)kl.Close
                    });
                });
            }

            public double[] SmoothenClosingPrices(int rangeFrq = 5)
            {
                var rawPrices = this.Select(p => p.ClosingPrice).ToArray();
                return KLineItems.Smoothen(rawPrices);
            }

            public static double[] Smoothen(double[] data, int rangeFrq = 5)
            {
                var unnoisedPrices = new double[data.Length];
                for (var ctr = 0; ctr < data.Length; ctr++)
                {
                    var samples = data.Skip(ctr).Take(rangeFrq).ToArray();
                    var median = samples.Median();
                    unnoisedPrices[ctr] = median;
                }
                return unnoisedPrices;
            }
        }
    }
}
