using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;
using Lykke.Service.FIXQuotes.PriceCalculator;
using Lykke.Service.QuotesHistory.Client.AutorestClient;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class FixQuotesManager : IDisposable, IFixQuotesManager
    {
        private readonly ILog _log;
        private readonly IQuotesHistoryService _quotesHistoryService;
        private readonly IMessageProducer<IEnumerable<FixQuoteModel>> _publisher;
        private readonly AppSettings.FixQuotesSettings _settings;
        private DateTime _fixingTime;
        private readonly Dictionary<string, IQuote> _lastReceivedAsks = new Dictionary<string, IQuote>();
        private readonly Dictionary<string, IQuote> _lastReceivedBids = new Dictionary<string, IQuote>();
        private readonly Timer _publishTimer;
        private readonly TimeSpan _publishPeriod = TimeSpan.FromHours(24);
        private readonly Dictionary<string, PriceDiscovery> _priceDiscoveries;
        private const double Threshold = 0.001; // threshold for the Intrinsic Time, 0.01 is equal to 1%
        private readonly object _priceDiscoveryLock = new object();
        private readonly ManualResetEventSlim _quotesUpdateLock = new ManualResetEventSlim(false);

        public FixQuotesManager(ILog log,
            IQuotesHistoryService quotesHistoryService,
            IMessageConsumer<IQuote> subscriber,
            IMessageProducer<IEnumerable<FixQuoteModel>> publisher,
            AppSettings.FixQuotesSettings settings)
        {
            _log = log;
            _quotesHistoryService = quotesHistoryService;
            _publisher = publisher;
            _settings = settings;
            _publishTimer = new Timer(OnPublish);
            _priceDiscoveries = new Dictionary<string, PriceDiscovery>();
            subscriber.Subscribe(ProcessQuoteUpdate);
            SetNextPublishTime();
            Start().GetAwaiter().GetResult();
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
            try
            {
                var tradeTime = _fixingTime.Date.AddHours(_settings.TradeHour);
                IReadOnlyCollection<FixQuoteModel> toPublish;
                lock (_priceDiscoveryLock)
                {
                    try
                    {
                        toPublish = GetFixPrices(tradeTime, _fixingTime);
                    }
                    finally
                    {
                        foreach (var pd in _priceDiscoveries.Values)
                        {
                            pd.Reset();
                        }
                    }
                }

                await _publisher.ProduceAsync(toPublish);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(FixQuotesManager), nameof(OnPublish), "Publishing fix quotes", ex);
            }
            finally
            {
                SetNextPublishTime();
            }
        }

        public IReadOnlyCollection<FixQuoteModel> GetFixPrices(DateTime tradeTime, DateTime fixingTime)
        {
            const double dividend = 0.0001;

            var yearsToMaturity = (tradeTime - fixingTime).Hours / 365.0 / 24.0;
            lock (_priceDiscoveryLock)
            {
                foreach (var pd in _priceDiscoveries.Values)
                {
                    try
                    {
                        pd.Finish(dividend, yearsToMaturity);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _log.WriteWarningAsync(nameof(FixQuotesManager), nameof(OnPublish), "Fix price calculation", ex.Message);
                    }
                }
                var toPublish = (from pd in _priceDiscoveries
                                 let ask = AddPremium(pd.Value.LatestCallStrike, _settings.Premium)
                                 let bid = AddPremium(pd.Value.LatestPutStrike, -_settings.Premium)
                                 where !double.IsNaN(pd.Value.LatestCallStrike) && !double.IsNaN(pd.Value.LatestPutStrike)
                                 select new FixQuoteModel
                                 {
                                     AssetPair = pd.Key,
                                     Ask = ask,
                                     Bid = bid,
                                     FixingTime = fixingTime,
                                     TradeTime = tradeTime
                                 }).ToArray();
                return toPublish;
            }
        }


        private static double AddPremium(double price, double shiftPercent)
        {
            return price + price / 100d * shiftPercent;
        }

        private Task ProcessQuoteUpdate(IQuote quote)
        {
            _quotesUpdateLock.Wait();
            ProcessQuote(quote);
            return Task.CompletedTask;
        }

        private void ProcessQuote(IQuote quote)
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

            lock (_priceDiscoveryLock)
            {
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

        private async Task Start()
        {
            try
            {
                await LoadQuotesHistoryAsync();
            }
            catch (Exception ex)
            {
                await _log.WriteFatalErrorAsync(nameof(FixQuotesManager), nameof(Start), "Loading market profile", ex);
                throw;
            }
        }

        private async Task LoadQuotesHistoryAsync()
        {


            await _log.WriteInfoAsync(nameof(LoadQuotesHistoryAsync), "Loading quotes from Quotes History Service", "Requesting quotes history");

            using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15))) // Can't wait too long, the quote tick queue will overflow
            {
                var to = DateTime.UtcNow;
                var from = to - _publishPeriod;
                var quotes = await _quotesHistoryService.QuotesHistoryAsync(to.AddMinutes(-15), to, null, cts.Token);
                var savedLag = to.AddMinutes(1) - quotes.Max(q => q.Timestamp);

                await Task.Delay(savedLag, cts.Token); // To ensure quotes history service flushed its buffer
                await _log.WriteInfoAsync(nameof(LoadQuotesHistoryAsync), "Loading quotes from Quotes History Service", $"Waiting {savedLag.TotalMinutes} minutes before load history");


                quotes = await _quotesHistoryService.QuotesHistoryAsync(from, to, null, cts.Token);
                foreach (var quote in quotes.OrderBy(q => q.Timestamp))
                {
                    ProcessQuote(quote);
                }
                await _log.WriteInfoAsync(nameof(LoadQuotesHistoryAsync), "Loading quotes from Quotes History Service", $"Loaded {quotes.Count} quotes");
            }
            _quotesUpdateLock.Set();
        }

        public void Dispose()
        {
            _publishTimer?.Dispose();
            _quotesUpdateLock.Dispose();
        }
    }
}