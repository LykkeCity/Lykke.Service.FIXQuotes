using System.Collections.Generic;
using System.Threading.Tasks;
using Autofac;
using Common.Log;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.Service.FIXQuotes.Core.Domain;
using Lykke.Service.FIXQuotes.Core.Services;

namespace Lykke.Service.FIXQuotes.Services
{
    public class FixQuotePublisher : IStartable, IFixQuotePublisher
    {
        private readonly ILog _log;
        private readonly RabbitMqPublisher<AggregatedQuote> _publisher;
        public FixQuotePublisher(ILog log, RabbitMqPublisher<AggregatedQuote> publisher)
        {
            _log = log;
            _publisher = publisher;
        }

        public async Task Publish(IReadOnlyCollection<FixQuote> quotes)
        {
            try
            {
                foreach (var quote in quotes)
                {
                    var ac = new AggregatedQuote
                    {
                        AssetPair = quote.AssetPair,
                        Date = quote.Date,
                        Price = (double)quote.Mid
                    };
                    await _publisher.ProduceAsync(ac);
                }
            }
            catch (System.Exception exception)
            {
                await _log.WriteErrorAsync(nameof(FixQuotePublisher), nameof(Publish), "Publishing fix quotes", exception);
            }
            await _log.WriteInfoAsync(nameof(FixQuotePublisher), nameof(Publish), "Publishing fix quotes", $"{quotes.Count} fix quotes has been successfully published");
        }

        public void Start()
        {
            _publisher.Start();
        }
    }
}