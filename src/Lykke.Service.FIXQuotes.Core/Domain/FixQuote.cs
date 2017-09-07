using System;

namespace Lykke.Service.FIXQuotes.Core.Domain
{
    public sealed class FixQuote
    {
        public DateTime Date { get; }
        public decimal Ask { get; }
        public decimal Bid { get; }
        public string AssetPair { get; }

        public FixQuote(DateTime date, string assetPair, decimal ask, decimal bid)
        {
            Date = date;
            AssetPair = assetPair;
            Ask = ask;
            Bid = bid;
        }

        public FixQuote(DateTime date, string assetPair, double ask, double bid) : this(date, assetPair, (decimal)ask, (decimal)bid)
        {

        }
    }
}