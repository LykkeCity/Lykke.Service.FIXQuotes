using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;
using Lykke.Service.FIXQuotes.PriceCalculator;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class FixQuotesManager : IFixQuotesManager
    {
        private readonly ILog _log;
        private readonly IFixQuotePublisher _quotePublisher;
        private readonly IMarketProfileService _marketProfileService;
        private readonly AppSettings.FixQuotesSettings _settings;
        private DateTime _fixingTime;
        private readonly Dictionary<string, IQuote> _lastReceivedAsks = new Dictionary<string, IQuote>();
        private readonly Dictionary<string, IQuote> _lastReceivedBids = new Dictionary<string, IQuote>();
        private readonly Timer _publishTimer;
        private readonly TimeSpan _publishPeriod = TimeSpan.FromHours(24);
        private readonly Dictionary<string, PriceDiscovery> _priceDiscoveries;
        private const double Threshold = 0.001; // threshold for the Intrinsic Time, 0.01 is equal to 1%
        private readonly object _publishLock = new object();

        public FixQuotesManager(ILog log, IFixQuotePublisher quotePublisher, IMarketProfileService marketProfileService, AppSettings.FixQuotesSettings settings)
        {
            _log = log;
            _quotePublisher = quotePublisher;
            _marketProfileService = marketProfileService;
            _settings = settings;
            _publishTimer = new Timer(OnPublish);
            _priceDiscoveries = new Dictionary<string, PriceDiscovery>();
            SetNextPublishTime();
            Start();
        }

        private void SetNextPublishTime()
        {
            _fixingTime = DateTime.UtcNow.Date.AddHours(_settings.FixingHour);
            if (_fixingTime <= DateTime.UtcNow)
            {
                _fixingTime = _fixingTime.Add(_publishPeriod);
            }
            var delayToNextPublish = _fixingTime - DateTime.UtcNow;
            _publishTimer.Change(delayToNextPublish, Timeout.InfiniteTimeSpan);
        }

        private async void OnPublish(object state)
        {
            const double dividend = 0.0001;
            var tradeTime = DateTime.UtcNow.Date.AddHours(_settings.TradeHour);

            var yearsToMaturity = (tradeTime - _fixingTime).Hours / 365.0 / 24.0;

            try
            {
                Monitor.Enter(_publishLock);

                foreach (var pd in _priceDiscoveries.Values)
                {
                    pd.Finish(dividend, yearsToMaturity);
                }
                var toPublish = (from pd in _priceDiscoveries
                                 let ask = AddPremium(pd.Value.LatestCallStrike, _settings.Premium)
                                 let bid = AddPremium(pd.Value.LatestPutStrike, -_settings.Premium)
                                 select new FixQuoteModel
                                 {
                                     AssetPair = pd.Key,
                                     Ask = ask,
                                     Bid = bid,
                                     FixingTime = _fixingTime,
                                     TradeTime = tradeTime
                                 }).ToList();

                await _quotePublisher.Publish(toPublish);
            }
            finally
            {
                SetNextPublishTime();
                foreach (var pd in _priceDiscoveries.Values)
                {
                    pd.Reset();
                }
                Monitor.Exit(_publishLock);
            }
        }


        private static double AddPremium(double price, double shiftPercent)
        {
            return price + price / 100d * shiftPercent;
        }

        public void ProcessQuote(IQuote quote)
        {
            lock (_publishLock)
            {
                var ask = !quote.IsBuy;
                var timestamp = quote.Timestamp;
                var key = quote.AssetPair;
                if (ask)
                {
                    if (_lastReceivedAsks.TryGetValue(key, out var saved))
                    {
                        if (saved.Timestamp < timestamp)
                        {
                            _lastReceivedAsks[key] = quote;
                        }
                    }
                    else
                    {
                        _lastReceivedAsks[key] = quote;
                    }
                }
                else
                {
                    if (_lastReceivedBids.TryGetValue(key, out var saved))
                    {
                        if (saved.Timestamp < timestamp)
                        {
                            _lastReceivedBids[key] = quote;
                        }
                    }
                    else
                    {
                        _lastReceivedBids[key] = quote;
                    }
                }

                if (_lastReceivedAsks.TryGetValue(key, out var askPrice) && _lastReceivedAsks.TryGetValue(key, out var bidPrice))
                {
                    var quoteTime = askPrice.Timestamp > bidPrice.Timestamp ? askPrice.Timestamp : bidPrice.Timestamp;
                    if (!_priceDiscoveries.TryGetValue(key, out var prd))
                    {
                        prd = new PriceDiscovery(Threshold);
                        _priceDiscoveries[key] = prd;
                    }
                    var price = new Price(askPrice.Price, bidPrice.Price, quoteTime);
                    prd.Run(price);
                }
            }
        }





        private async void Start()
        {
            try
            {
                var allPairs = await _marketProfileService.GetAllPairsAsync();
                foreach (var asset in allPairs)
                {
                    var key = asset.AssetPair;
                    _lastReceivedAsks[key] = new Quote
                    {
                        AssetPair = key,
                        Price = asset.AskPrice,
                        Timestamp = asset.AskPriceTimestamp
                    };

                    _lastReceivedBids[key] = new Quote
                    {
                        AssetPair = key,
                        Price = asset.BidPrice,
                        Timestamp = asset.BidPriceTimestamp
                    };
                    if (!_priceDiscoveries.TryGetValue(key, out var prd))
                    {
                        prd = new PriceDiscovery(Threshold);
                        _priceDiscoveries[key] = prd;
                        var timestamp = asset.AskPriceTimestamp > asset.BidPriceTimestamp
                            ? asset.AskPriceTimestamp
                            : asset.BidPriceTimestamp;
                        var price = new Price(asset.AskPrice, asset.BidPrice, timestamp);
                        prd.Run(price);
                    }
                }
            }
            catch (Exception ex)
            {
                await _log.WriteFatalErrorAsync(nameof(FixQuotesManager), nameof(Start), "Loading market profile", ex);
                throw;
            }
        }
    }
}