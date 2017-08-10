using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Domain.Prices;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Services;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class QuoteBroker : IStartable, IDisposable
    {
        private readonly ILog _log;
        private readonly IFixQuotesManager _quotesManager;
        private readonly AppSettings.FIXQuotesSettings _settings;
        private RabbitMqSubscriber<IQuote> _subscriber;


        public QuoteBroker(ILog log, IFixQuotesManager quotesManager, AppSettings.FIXQuotesSettings settings)
        {
            _log = log;
            _quotesManager = quotesManager;
            _settings = settings;
        }

        public void Start()
        {
            var settings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _settings.QuoteFeedRabbit.ConnectionString,
                QueueName = $"{_settings.QuoteFeedRabbit.ExchangeName}.fixquotes",
                ExchangeName = _settings.QuoteFeedRabbit.ExchangeName,
                IsDurable = true
            };

            try
            {
                _subscriber = new RabbitMqSubscriber<IQuote>(settings,
                        new ResilientErrorHandlingStrategy(_log, settings,
                            retryTimeout: TimeSpan.FromSeconds(10),
                            next: new DeadQueueErrorHandlingStrategy(_log, settings)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<Quote>())
                    .SetMessageReadStrategy(new MessageReadQueueStrategy())
                    .Subscribe(ProcessQuoteAsync)
                    .CreateDefaultBinding()
                    .SetLogger(_log)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(QuoteBroker), nameof(Start), null, ex).Wait();
                throw;
            }
        }

        private void Stop()
        {
            _subscriber.Stop();
        }

        private async Task ProcessQuoteAsync(IQuote quote)
        {
            try
            {
                var validationErrors = ValidateQuote(quote);
                if (validationErrors.Any())
                {
                    var message = string.Join("\r\n", validationErrors);
                    await _log.WriteWarningAsync(nameof(QuoteBroker), nameof(ProcessQuoteAsync), quote.ToJson(), message);

                    return;
                }

                await _quotesManager.ProcessQuoteAsync(quote);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(QuoteBroker), nameof(ProcessQuoteAsync), null, ex);
            }
        }

        private static IReadOnlyCollection<string> ValidateQuote(IQuote quote)
        {
            var errors = new List<string>();

            if (quote == null)
            {
                errors.Add("Argument 'Order' is null.");
            }
            if (quote != null && string.IsNullOrEmpty(quote.AssetPair))
            {
                errors.Add(string.Format("Invalid 'AssetPair': '{0}'", quote.AssetPair ?? ""));
            }
            if (quote != null && (quote.Timestamp == DateTime.MinValue || quote.Timestamp == DateTime.MaxValue))
            {
                errors.Add(string.Format("Invalid 'Timestamp' range: '{0}'", quote.Timestamp));
            }
            if (quote != null && quote.Timestamp.Kind != DateTimeKind.Utc)
            {
                errors.Add(string.Format("Invalid 'Timestamp' Kind (UTC is required): '{0}'", quote.Timestamp));
            }

            return errors;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}