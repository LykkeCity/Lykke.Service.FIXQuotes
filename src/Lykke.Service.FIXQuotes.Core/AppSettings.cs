namespace Lykke.Service.FIXQuotes.Core
{
    public class AppSettings
    {
        public FIXQuotesSettings FIXQuotesService { get; set; }
        public SlackNotificationsSettings SlackNotifications { get; set; }

        public class FIXQuotesSettings
        {
            public DbSettings Db { get; set; }
            public RabbitSettings QuoteFeedRabbit { get; set; }
            public RabbitSettings FixQuoteFeedRabbit { get; set; }
            public int PublishTime { get; set; }
            public int AccumulationPeriodHours { get; set; }
        }
    }

    public class DbSettings
    {
        public string LogsConnString { get; set; }
        public string FixQuotesBackupConnString { get; set; }
    }

    public class SlackNotificationsSettings
    {
        public AzureQueueSettings AzureQueue { get; set; }

        public int ThrottlingLimitSeconds { get; set; }
    }

    public class AzureQueueSettings
    {
        public string ConnectionString { get; set; }

        public string QueueName { get; set; }
    }


    public class RabbitSettings
    {
        public string ConnectionString { get; set; }
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
    }
}
