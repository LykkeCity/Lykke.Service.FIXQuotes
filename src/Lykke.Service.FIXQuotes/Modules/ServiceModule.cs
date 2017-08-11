using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage;
using AzureStorage.Tables;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.FIXQuotes.AzureRepositories;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain;
using Lykke.Service.FIXQuotes.Core.Services;
using Lykke.Service.FIXQuotes.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lykke.Service.FIXQuotes.Modules
{
    public class ServiceModule : Module
    {
        private readonly AppSettings.FIXQuotesSettings _settings;
        private readonly ILog _log;
        // NOTE: you can remove it if you don't need to use IServiceCollection extensions to register service specific dependencies
        private readonly IServiceCollection _services;

        private const string BackupTableName = "fixquotesbackup";

        public ServiceModule(AppSettings.FIXQuotesSettings settings, ILog log)
        {
            _settings = settings;
            _log = log;

            _services = new ServiceCollection();
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings)
                .SingleInstance();

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();

            var storage = new AzureTableStorage<FixQuoteDto>(_settings.Db.FixQuotesBackupConnString, BackupTableName, _log);
            //   var wrapper = new RetryOnFailureAzureTableStorageDecorator<FixQuoteDto>(storage, 5, 5, TimeSpan.FromSeconds(10));

            builder.RegisterInstance(storage)
                .As<INoSQLTableStorage<FixQuoteDto>>();

            builder.RegisterType<FixQuoteRepository>()
                .As<IFixQuoteRepository>();

            var reciverRabbitMqSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _settings.QuoteFeedRabbit.ConnectionString,
                QueueName = _settings.QuoteFeedRabbit.QueueName,
                ExchangeName = _settings.QuoteFeedRabbit.ExchangeName,
                IsDurable = true
            };

            var subscriber = new RabbitMqSubscriber<IQuote>(reciverRabbitMqSettings,
                    new ResilientErrorHandlingStrategy(_log, reciverRabbitMqSettings, TimeSpan.FromSeconds(10)))
                .SetMessageDeserializer(new JsonMessageDeserializer<Quote>())
                .SetMessageReadStrategy(new MessageReadQueueStrategy())
                .SetLogger(_log);

            builder.RegisterInstance(subscriber);

            var publisherRabbitMqSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _settings.FixQuoteFeedRabbit.ConnectionString,
                QueueName = _settings.FixQuoteFeedRabbit.QueueName,
                ExchangeName = _settings.FixQuoteFeedRabbit.ExchangeName,
                IsDurable = true
            };

            var rabbitMqPublisher = new RabbitMqPublisher<AggregatedQuote>(publisherRabbitMqSettings)
                .SetSerializer(new JsonMessageSerializer<AggregatedQuote>())
                .SetPublishStrategy(new DefaultFanoutPublishStrategy(publisherRabbitMqSettings));

            builder.RegisterInstance(rabbitMqPublisher);

            builder.RegisterType<FixQuotePublisher>()
                .As<IFixQuotePublisher>();

            builder.RegisterType<QuoteReceiver>()
                .As<IStartable>()
                .SingleInstance()
                .AutoActivate();

            builder.RegisterType<FixQuotesManager>()
                .As<IFixQuotesManager>()
                .As<IStartable>()
                .SingleInstance();

            builder.Populate(_services);
        }
    }
}
