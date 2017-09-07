namespace Lykke.Service.FIXQuotes.Core.Domain.Models
{
    /// <summary>
    /// An array of FixQuotes to publish for external consumers
    /// </summary>
    public sealed class FixQuotePack
    {
        public FixQuoteModel[] Quotes { get; set; }
    }
}