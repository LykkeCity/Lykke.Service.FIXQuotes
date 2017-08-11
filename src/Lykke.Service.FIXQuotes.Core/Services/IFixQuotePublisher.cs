using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.FIXQuotes.Core.Domain;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IFixQuotePublisher
    {
        Task Publish(IReadOnlyCollection<FixQuote> quotes);
    }
}