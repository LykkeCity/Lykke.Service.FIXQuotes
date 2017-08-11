using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain;
using Lykke.Service.FIXQuotes.Core.Services;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class FixQuotesManager : IFixQuotesManager, IDisposable
    {
        private readonly ILog _log;
        private readonly IFixQuoteRepository _quoteRepository;
        private readonly AppSettings.FIXQuotesSettings _settings;
        private readonly IFixQuotePublisher _fixQuotePublisher;
        private ConcurrentDictionary<string, FixQuote> _cache;
        private DateTime _startTime;
        private DateTime _publishTime;
        private bool _backupIsActive;
        private readonly Timer _backupTimer;
        private readonly Timer _publishTimer;
        private readonly TimeSpan _accumulationPeriod;
        private readonly TimeSpan _backupPeriod = TimeSpan.FromSeconds(1);

        public FixQuotesManager(ILog log, IFixQuoteRepository quoteRepository, AppSettings.FIXQuotesSettings settings, IFixQuotePublisher fixQuotePublisher)
        {
            _log = log;
            _quoteRepository = quoteRepository;
            _settings = settings;
            _fixQuotePublisher = fixQuotePublisher;
            SetStartTime();
            SetPublishTime();
            _backupTimer = new Timer(OnBackup, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _publishTimer = new Timer(OnPublish, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _accumulationPeriod = TimeSpan.FromHours(_settings.AccumulationPeriodHours);
            SchedulePublishing();
        }

        private void SchedulePublishing()
        {
            var nextPublishTime = _publishTime > DateTime.UtcNow ? _publishTime : _publishTime.AddDays(1);
            var durationToNextPublishing = nextPublishTime - DateTime.UtcNow;
            _publishTimer.Change(durationToNextPublishing, TimeSpan.FromDays(1));
        }

        private void SetPublishTime()
        {
            _publishTime = DateTime.UtcNow.Date.AddHours(_settings.PublishTime);
        }

        private void SetStartTime()
        {
            _startTime = DateTime.UtcNow.Date.AddHours(_settings.PublishTime - _settings.AccumulationPeriodHours);
        }

        private async void OnPublish(object state)
        {
            await _fixQuotePublisher.Publish(new ReadOnlyCollection<FixQuote>(_cache.Values.ToArray()));
        }

        private async void OnBackup(object state)
        {
            try
            {
                await _quoteRepository.AddOrUpdateBatchAsync(_cache.Values);
            }
            catch (Exception e)
            {
                await _log.WriteErrorAsync(nameof(FixQuotesManager), nameof(OnBackup), "Saving fix prices to DB", e);
            }
            finally
            {
                if (_backupIsActive)
                {
                    _backupTimer.Change(_backupPeriod, Timeout.InfiniteTimeSpan);
                }
            }
        }


        public void Start()
        {
            CacheQuotesAsync().Wait();
        }

        private async Task CacheQuotesAsync()
        {
            var fromDb = await _quoteRepository.GetPerDate(DateTime.UtcNow);
            _cache = new ConcurrentDictionary<string, FixQuote>(fromDb.ToDictionary(kv => kv.AssetPair));
            await _log.WriteInfoAsync(nameof(FixQuotesManager), nameof(CacheQuotesAsync), null, "All quotes are cached");

        }

        public async Task ProcessQuoteAsync(IQuote quote)
        {
            var ask = quote.IsBuy;
            var price = (decimal)quote.Price;
            ShiftDates();
            await ScheduleBackup();
            if (TakeQuoteIntoAccount(quote))
            {
                FixQuote newFixQuote;
                if (_cache.TryGetValue(quote.AssetPair, out var fixQuote))
                {

                    newFixQuote = ask ? new FixQuote(quote.Timestamp, quote.AssetPair, price + fixQuote.AskSum, fixQuote.AskNum + 1, fixQuote.BidSum, fixQuote.BidNum)
                        : new FixQuote(quote.Timestamp, quote.AssetPair, fixQuote.AskSum, fixQuote.AskNum, fixQuote.BidSum + price, fixQuote.BidNum + 1);

                }
                else
                {
                    newFixQuote = ask ? new FixQuote(quote.Timestamp, quote.AssetPair, price, 1, 0, 0) : new FixQuote(quote.Timestamp, quote.AssetPair, 0, 0, price, 1);
                }
                _cache[quote.AssetPair] = newFixQuote;
            }
        }

        private void ShiftDates()
        {
            if (_startTime.Date != DateTime.UtcNow.Date)
            {
                SetStartTime();
            }
            if (_publishTime != DateTime.UtcNow.Date)
            {
                SetPublishTime();
            }
        }

        private async Task ScheduleBackup()
        {
            var now = DateTime.UtcNow;
            if (_backupIsActive)
            {
                if (now > _publishTime)
                {
                    _backupIsActive = false;
                    _cache.Clear();
                    _backupTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                    await _log.WriteInfoAsync(nameof(FixQuotesManager), nameof(OnBackup), "Saving fix prices to DB", "Backup stopped");

                }
            }
            else
            {
                if (_publishTime > now && _publishTime - now <= _accumulationPeriod)
                {
                    _backupIsActive = true;
                    _backupTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
                    await _log.WriteInfoAsync(nameof(FixQuotesManager), nameof(OnBackup), "Saving fix prices to DB", "Backup started");

                }
            }
        }

        private bool TakeQuoteIntoAccount(IQuote quote)
        {
            return _publishTime > quote.Timestamp && _publishTime - quote.Timestamp <= _accumulationPeriod;
        }

        public void Dispose()
        {
            _backupTimer?.Dispose();
        }
    }
}