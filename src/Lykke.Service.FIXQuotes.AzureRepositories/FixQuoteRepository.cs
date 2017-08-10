using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using AzureStorage;
using Common;
using Lykke.Service.FIXQuotes.Core.Domain;
using Lykke.Service.FIXQuotes.Core.Services;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.FIXQuotes.AzureRepositories
{
    public sealed class FixQuoteRepository : IFixQuoteRepository
    {
        private readonly INoSQLTableStorage<FixQuoteDto> _table;

        public FixQuoteRepository(INoSQLTableStorage<FixQuoteDto> table)
        {
            _table = table;
        }

        public async Task AddOrUpdateBatchAsync(IEnumerable<FixQuote> quotes)
        {
            foreach (var batch in quotes.Batch(100))
            {
                var newBatch = new TableBatchOperation();

                foreach (var quote in batch)
                {
                    var dto = new FixQuoteDto(quote.Date, quote.AssetPair)
                    {
                        AskNum = (double)quote.AskNum,
                        AskSum = (double)quote.AskSum,
                        BidNum = (double)quote.BidNum,
                        BidSum = (double)quote.BidSum
                    };
                    newBatch.InsertOrReplace(dto);
                }
                await _table.DoBatchAsync(newBatch);
            }
        }

        public async Task<FixQuote> Get(DateTime date, string assetPair)
        {
            var quote = await _table.GetDataAsync(date.Date.ToIsoDate(), assetPair) ?? new FixQuoteDto(date.Date, assetPair);
            var result = new FixQuote(date, quote.AssetPair, (decimal)quote.AskSum, (decimal)quote.AskNum, (decimal)quote.BidSum, (decimal)quote.BidNum);
            return result;
        }

        public async Task<IReadOnlyCollection<FixQuote>> GetPerDate(DateTime date)
        {
            var query = new TableQuery<FixQuoteDto>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, date.ToIsoDate()));
            var quotes = await _table.WhereAsync(query);
            var fixQuotes = quotes.Select(q => new FixQuote(q.Date, q.AssetPair, (decimal) q.AskSum, (decimal) q.AskNum, (decimal) q.BidSum, (decimal) q.BidNum));
            return new ReadOnlyCollection<FixQuote>(fixQuotes.ToList());

        }
    }
}