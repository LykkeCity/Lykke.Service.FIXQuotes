using Lykke.Domain.Prices.Contracts;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IFixQuotesManager
    {
        void ProcessQuote(IQuote quote);
    }
}