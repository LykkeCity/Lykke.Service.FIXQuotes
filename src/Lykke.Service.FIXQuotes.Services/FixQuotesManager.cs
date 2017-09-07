using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;
using MoreLinq;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class FixQuotesManager : IFixQuotesManager
    {
        private readonly ILog _log;
        private readonly IFixQuotePublisher _quotePublisher;
        private readonly IMarketProfileService _marketProfileService;
        private readonly AppSettings.FixQuotesSettings _settings;
        private DateTime _cutoffTime;
        private readonly ConcurrentDictionary<string, IQuote> _lastReceivedAsks = new ConcurrentDictionary<string, IQuote>();
        private readonly ConcurrentDictionary<string, IQuote> _lastReceivedBids = new ConcurrentDictionary<string, IQuote>();
        private readonly ConcurrentDictionary<string, FixQuote> _lastReceived = new ConcurrentDictionary<string, FixQuote>();
        private readonly Timer _publishTimer;
        private readonly TimeSpan _publishPeriod = TimeSpan.FromHours(24);

        public FixQuotesManager(ILog log, IFixQuotePublisher quotePublisher, IMarketProfileService marketProfileService, AppSettings.FixQuotesSettings settings)
        {
            _log = log;
            _quotePublisher = quotePublisher;
            _marketProfileService = marketProfileService;
            _settings = settings;
            _publishTimer = new Timer(OnPublish);
            SetPublishTime();
            Start();
        }

        private void SetPublishTime()
        {
            _cutoffTime = DateTime.UtcNow.Date.AddHours(_settings.FixingHour);
            if (_cutoffTime <= DateTime.UtcNow)
            {
                _cutoffTime = _cutoffTime.Add(_publishPeriod);
            }
            var delayToNextPublish = _cutoffTime - DateTime.UtcNow;
            _publishTimer.Change(delayToNextPublish, Timeout.InfiniteTimeSpan);
        }

        private async void OnPublish(object state)
        {
            try
            {
                await MergeQuotes(true);
                var toPublish = (from fixQuote in _lastReceived.Values.ToArray()
                    let ask = ShiftPrice(fixQuote.Ask, _settings.SpreadPercent)
                    let bid = ShiftPrice(fixQuote.Bid, -_settings.SpreadPercent)
                    select new FixQuoteModel
                    {
                        AssetPair = fixQuote.AssetPair,
                        Ask = ask,
                        Bid = bid,
                        FixingTime = _cutoffTime,
                        TradeTime = DateTime.UtcNow.Date.AddHours(_settings.TradeHour)
                    }).ToList();

                await _quotePublisher.Publish(toPublish);
            }
            finally
            {
                SetPublishTime();
            }
        }


        private async Task MergeQuotes(bool logUnpaired = false)
        {
            var fullJoin = _lastReceivedAsks.FullGroupJoin(_lastReceivedBids, kv => kv.Key, kv => kv.Key, (key, kv1, kv2) => new { AssetPair = key, ask = kv1.FirstOrDefault().Value, bid = kv2.FirstOrDefault().Value });

            foreach (var tuple in fullJoin)
            {
                if (logUnpaired)
                {
                    if (tuple.ask == null)
                    {
                        await _log.WriteWarningAsync(nameof(FixQuotesManager), nameof(MergeQuotes), "Publish quotes", $"No pair ask quote for {tuple.AssetPair}");
                        continue;
                    }
                    if (tuple.bid == null)
                    {
                        await _log.WriteWarningAsync(nameof(FixQuotesManager), nameof(MergeQuotes), "Publish quotes", $"No pair bid quote for {tuple.AssetPair}");
                        continue;
                    }
                }

                _lastReceived[tuple.AssetPair] = new FixQuote(DateTime.UtcNow, tuple.AssetPair, tuple.ask.Price, tuple.bid.Price);
            }
        }

        private static double ShiftPrice(decimal price, decimal shiftPercent)
        {
            return (double)(price + price / 100 * shiftPercent);
        }

        public void ProcessQuote(IQuote quote)
        {

            if (ShouldProcess(quote))
            {
                var ask = quote.IsBuy;
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
            }
        }



        private bool ShouldProcess(IQuote quote)
        {
            return _cutoffTime > quote.Timestamp;
        }


        private async void Start()
        {
            try
            {
                var allPairs = await _marketProfileService.GetAllPairsAsync();
                foreach (var asset in allPairs)
                {
                    _lastReceivedAsks[asset.AssetPair] = new Quote
                    {
                        AssetPair = asset.AssetPair,
                        Price = asset.AskPrice,
                        Timestamp = asset.AskPriceTimestamp
                    };

                    _lastReceivedBids[asset.AssetPair] = new Quote
                    {
                        AssetPair = asset.AssetPair,
                        Price = asset.BidPrice,
                        Timestamp = asset.BidPriceTimestamp
                    };
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