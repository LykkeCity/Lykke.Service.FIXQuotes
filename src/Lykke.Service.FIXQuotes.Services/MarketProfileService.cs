using System.Collections.Generic;
using System.Threading.Tasks;
using Lykke.Service.FIXQuotes.Core.Services;
using Lykke.Service.MarketProfile.Client;
using Lykke.Service.MarketProfile.Client.Models;

namespace Lykke.Service.FIXQuotes.Services
{
    public sealed class MarketProfileService : IMarketProfileService
    {
        private readonly ILykkeMarketProfile _api;

        public MarketProfileService(ILykkeMarketProfile api)
        {
            _api = api;
        }

        public async Task<IList<AssetPairModel>> GetAllPairsAsync()
        {
            return await _api.ApiMarketProfileGetAsync();
        }

        public void Dispose()
        {
            _api.Dispose();
        }
    }
}