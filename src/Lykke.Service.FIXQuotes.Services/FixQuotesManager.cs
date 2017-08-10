using System;
using System.Collections.Concurrent;
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
    public sealed class FixQuotesManager : IFixQuotesManager
    {
        private readonly ILog _log;
        private readonly IFixQuoteRepository _quoteRepository;
        private readonly AppSettings.FIXQuotesSettings _settings;
        private ConcurrentDictionary<string, FixQuote> _cache;
        private DateTime _startTime;
        private DateTime _publishTime;
        private bool _backupIsActive;
        private readonly Timer _backupTimer;

        public FixQuotesManager(ILog log, IFixQuoteRepository quoteRepository, AppSettings.FIXQuotesSettings settings)
        {
            _log = log;
            _quoteRepository = quoteRepository;
            _settings = settings;
            SetStartTime();
            SetPublishTime();
            _backupTimer = new Timer(OnBackup, null, TimeSpan.MaxValue, Timeout.InfiniteTimeSpan);
        }

        private void SetPublishTime()
        {
            _startTime = DateTime.UtcNow.Date.AddHours(_settings.PublishTime);
        }

        private void SetStartTime()
        {
            _publishTime = DateTime.Now.Date.AddHours(_settings.PublishTime - _settings.AccumulationPeriodHours);
        }

        private void OnBackup(object state)
        {
            _quoteRepository.AddOrUpdateBatchAsync(_cache.Values);
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

        public Task ProcessQuoteAsync(IQuote quote)
        {
            var ask = quote.IsBuy;
            var price = (decimal)quote.Price;
            ShiftDates();
            ScheduleBackup();
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
            return Task.CompletedTask;
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

        private void ScheduleBackup()
        {
            var now = DateTime.UtcNow;
            if (_backupIsActive)
            {
                if (now < _startTime || now > _publishTime)
                {
                    _backupIsActive = false;
                    _cache.Clear();
                    _backupTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
                }
            }
            else
            {
                if (now >= _startTime && now < _publishTime)
                {
                    _backupIsActive = true;
                    _backupTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
                }
            }
        }

        private bool TakeQuoteIntoAccount(IQuote quote)
        {
            return (quote.Timestamp - _startTime).Hours <= _settings.AccumulationPeriodHours;
        }
    }
}