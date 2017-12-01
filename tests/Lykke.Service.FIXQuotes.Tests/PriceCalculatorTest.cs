using System;
using System.IO;
using NUnit.Framework;
using Lykke.Service.FIXQuotes.PriceCalculator;

namespace Lykke.Service.FIXQuotes.Tests
{
    [TestFixture]
    public class PriceCalculatorTest
    {
        [Test]
        public void SuccessfulPath()
        {
            var threshold = 0.001; // threshold for the Intrinsic Time, 0.01 is equal to 1%

            var priceDiscovery = new PriceDiscovery(threshold);


            var filePath = @".\TestData";
            var fileName = "EURCHF_UTC_Ticks_Bid_2017.05.21_2017.06.14.csv";
            var dateFormat = "yyyy.MM.dd HH:mm:ss.SSS";
            var sr = File.ReadLines(Path.Combine(filePath, fileName));
            long i = 0;
            bool skipHeader = true;
            foreach (var priceLine in sr)
            {
                if (skipHeader)
                {
                    skipHeader = false;
                    continue;
                }
                var price = Tools.PriceLineToPrice(priceLine, ',', 5, dateFormat, 1, 2, 0);
                priceDiscovery.Run(price); // should run this method for all historical ticks.
                i++;
            }

            var yearsToMaturity = 1 / TimeSpan.FromDays(365).TotalHours; // (1.0 / 365.0 / 24.0) is equal to one hour. It is also the period of the activation time.
            const double dividend = 0.0001; // dividends from the original paper.
            priceDiscovery.Finish(dividend, yearsToMaturity);
            var computedVolatility = priceDiscovery.LatestVolatility;
            var putPrice = priceDiscovery.LatestPutStrike;
            var callPrice = priceDiscovery.LatestCallStrike;
            Console.WriteLine("Computed volatility (in Intrinsic Time): " + computedVolatility);
            Console.WriteLine($"Ask {priceDiscovery.LatestPrice.FloatAsk} Bid {priceDiscovery.LatestPrice.FloatBid}");

            Console.WriteLine("Fixed Ask (call): " + callPrice);
            Console.WriteLine("Fixed Bid (put): " + putPrice);

            Assert.That(computedVolatility, Is.EqualTo(0.0292970455147442).Within(0.00001));
            Assert.That(priceDiscovery.LatestPrice.FloatAsk, Is.EqualTo(1.08612).Within(0.00001));
            Assert.That(priceDiscovery.LatestPrice.FloatBid, Is.EqualTo(1.08602).Within(0.00001));
            Assert.That(callPrice, Is.EqualTo(1.08801276244632).Within(0.00001));
            Assert.That(putPrice, Is.EqualTo(1.08412712059506).Within(0.00001));

        }
    }

}
