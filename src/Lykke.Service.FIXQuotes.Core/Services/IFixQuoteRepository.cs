using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.FIXQuotes.Core.Domain;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IFixQuoteRepository
    {
        Task AddOrUpdateBatchAsync(IEnumerable<FixQuote> quotes);
        Task<FixQuote> Get(DateTime date, string assetPair);

        Task<IReadOnlyCollection<FixQuote>> GetPerDate(DateTime date);
    }
}