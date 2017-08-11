using System;


namespace Lykke.Service.FIXQuotes.Core.Domain
{
    /// <summary>
    /// A quote aggregated withing a time period 
    /// </summary>
    public sealed class AggregatedQuote
    {
        /// <summary>
        /// An unique asset ID
        /// </summary>
        public string AssetPair { get; set; }

        /// <summary>
        /// The price calculation time
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// A price of the quote
        /// </summary>
        public double Price { get; set; }
    }
}
