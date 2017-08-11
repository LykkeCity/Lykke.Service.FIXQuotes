using System;
using Common;
using Microsoft.WindowsAzure.Storage.Table;

namespace Lykke.Service.FIXQuotes.AzureRepositories
{
    public sealed class FixQuoteDto : TableEntity
    {
        public FixQuoteDto()
        {

        }

        public FixQuoteDto(DateTime quoteDate, string assetPair)
        {
            Date = quoteDate.Date;
            AssetPair = assetPair;

            PartitionKey = Date.ToIsoDate();
            RowKey = assetPair;

        }

        public DateTime Date { get; set; }
        public string AssetPair { get; set; }
        public double AskSum { get; set; }
        public double AskNum { get; set; }
        public double BidSum { get; set; }
        public double BidNum { get; set; }
    }
}