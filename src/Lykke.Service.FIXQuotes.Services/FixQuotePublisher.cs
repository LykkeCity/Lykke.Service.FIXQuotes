using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Common.Log;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class FixQuotePublisher : IFixQuotePublisher
    {
        private readonly ILog _log;
        private readonly RabbitMqPublisher<IEnumerable<FixQuoteModel>> _publisher;
        public FixQuotePublisher(ILog log, RabbitMqPublisher<IEnumerable<FixQuoteModel>> publisher)
        {
            _log = log;
            _publisher = publisher;
        }

        public async Task Publish(IReadOnlyCollection<FixQuoteModel> quotes)
        {
            try
            {
                await _publisher.ProduceAsync(quotes.ToArray());
            }
            catch (System.Exception exception)
            {
                await _log.WriteErrorAsync(nameof(FixQuotePublisher), nameof(Publish), "Publishing fix quotes", exception);
            }
            await _log.WriteInfoAsync(nameof(FixQuotePublisher), nameof(Publish), "Publishing fix quotes", $"{quotes.Count} fix quotes has been successfully published");
        }
    }
}