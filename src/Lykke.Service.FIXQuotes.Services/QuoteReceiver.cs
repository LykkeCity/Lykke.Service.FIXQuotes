using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.Domain.Prices;
using Lykke.Domain.Prices.Contracts;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.FIXQuotes.Core.Services;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class QuoteReceiver : IStartable, IDisposable
    {
        private readonly ILog _log;
        private readonly IFixQuotesManager _quotesManager;
        private readonly RabbitMqSubscriber<IQuote> _subscriber;


        public QuoteReceiver(ILog log, IFixQuotesManager quotesManager, RabbitMqSubscriber<IQuote> subscriber)
        {
            _log = log;
            _quotesManager = quotesManager;
            _subscriber = subscriber;
        }

        public void Start()
        {
            try
            {
                _subscriber
                    .Subscribe(ProcessQuoteAsync)
                    .Start();
            }
            catch (Exception ex)
            {
                _log.WriteErrorAsync(nameof(QuoteReceiver), nameof(Start), null, ex).Wait();
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
                    await _log.WriteWarningAsync(nameof(QuoteReceiver), nameof(ProcessQuoteAsync), quote.ToJson(), message);

                    return;
                }

                await _quotesManager.ProcessQuoteAsync(quote);
            }
            catch (Exception ex)
            {
                await _log.WriteErrorAsync(nameof(QuoteReceiver), nameof(ProcessQuoteAsync), null, ex);
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