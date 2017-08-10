using System;
using Common.Log;

namespace Lykke.Service.FIXQuotes.Client
{
    public class FIXQuotesClient : IFIXQuotesClient, IDisposable
    {
        private readonly ILog _log;

        public FIXQuotesClient(string serviceUrl, ILog log)
        {
            _log = log;
        }

        public void Dispose()
        {
            //if (_service == null)
            //    return;
            //_service.Dispose();
            //_service = null;
        }
    }
}
