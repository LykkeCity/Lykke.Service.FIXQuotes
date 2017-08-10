using System;

namespace Lykke.Service.FIXQuotes.Core.Domain
{
   public sealed class FixQuote
    {
        public DateTime Date { get; }
        public decimal AskSum { get; }
        public decimal AskNum { get; }
        public string AssetPair { get; }
        public decimal BidSum { get; }
        public decimal BidNum { get; }
        public decimal Mid { get; }

        public FixQuote(DateTime date, string assetPair, decimal askSum, decimal askNum, decimal bidSum, decimal bidNum)
        {
            Date = date;
            AskSum = askSum;
            AskNum = askNum;
            AssetPair = assetPair;
            BidSum = bidSum;
            BidNum = bidNum;
            Mid = (askSum + bidSum) / (askNum + bidNum) / 2;
        }
    }
}