using System;

namespace Lykke.Service.FIXQuotes.Core.Domain.Models
{
    /// <summary>
    /// A quote model for external consumers
    /// </summary>
    public sealed class FixQuoteModel
    {
        /// <summary>
        /// An unique asset ID
        /// </summary>
        public string AssetPair { get; set; }

        /// <summary>
        /// The price calculation time
        /// </summary>
        public DateTime FixingTime { get; set; }


        /// <summary>
        /// The time when the trade can be done
        /// </summary>
        public DateTime TradeTime { get; set; }

        /// <summary>
        /// The ask price
        /// </summary>
        public double Ask { get; set; }

        /// <summary>
        /// The bid price
        /// </summary>
        public double Bid { get; set; }
    }
}
