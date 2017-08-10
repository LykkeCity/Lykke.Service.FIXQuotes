using System.Threading.Tasks;
using Autofac;
using Lykke.Domain.Prices.Contracts;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IFixQuotesManager : IStartable
    {
        Task ProcessQuoteAsync(IQuote quote);
    }
}