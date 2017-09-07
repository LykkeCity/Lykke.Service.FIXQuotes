using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.MarketProfile.Client.Models;

namespace Lykke.Service.FIXQuotes.Core.Services
{
    public interface IMarketProfileService : IDisposable
    {
        Task<IList<AssetPairModel>> GetAllPairsAsync();
    }
}