using System;
using System.Collections.Generic;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lykke.Service.FIXQuotes.Controllers
{
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class FixQuoteController : Controller
    {
        private readonly IFixQuotesManager _fixQuotesManager;

        public FixQuoteController(IFixQuotesManager fixQuotesManager)
        {
            _fixQuotesManager = fixQuotesManager;
        }

        [HttpGet]
        public IReadOnlyCollection<FixQuoteModel> Index(int activationPeriod)
        {
            var fixingTime = DateTime.UtcNow;
            var tradeTime = fixingTime.AddHours(activationPeriod);
            return _fixQuotesManager.GetFixPrices(tradeTime, fixingTime);
        }
    }
}