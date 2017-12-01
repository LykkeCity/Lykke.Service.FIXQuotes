using System;
using System.Collections.Generic;
using Lykke.Service.FIXQuotes.Core.Domain.Models;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IFixQuotesManager
    {
        IReadOnlyCollection<FixQuoteModel> GetFixPrices(DateTime tradeTime, DateTime fixingTime);
    }
}