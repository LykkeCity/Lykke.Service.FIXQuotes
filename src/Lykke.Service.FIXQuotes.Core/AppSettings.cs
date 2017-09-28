namespace Lykke.Service.FIXQuotes.Core
{
    public sealed class AppSettings
    {
        public FixQuotesSettings FixQuotesService { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }

        public sealed class FixQuotesSettings
        {
            public DbSettings Db { get; set; }
            public RabbitSettings QuoteFeedRabbit { get; set; }
            public RabbitSettings FixQuoteFeedRabbit { get; set; }
            public double FixingHour { get; set; }
            public double TradeHour { get; set; }
            public double Premium { get; set; }
            public MarketProfileServiceClient MarketProfileServiceClient { get; set; }
        }
    }

    public class DbSettings
    {
        public string LogsConnString { get; set; }
    }

    public class SlackNotificationsSettings
    {
        public AzureQueueSettings AzureQueue { get; set; }
    }

    public class AzureQueueSettings
    {
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }
    }

    public class MarketProfileServiceClient
    {
        public string ServiceUrl { get; set; }
    }


    public class RabbitSettings
    {
        public string ConnectionString { get; set; }
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
    }
}
