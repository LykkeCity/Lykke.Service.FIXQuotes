using System;
using System.Collections.Generic;
using Autofac;
using Common;
using Common.Log;
using Lykke.Domain.Prices.Contracts;
using Lykke.Domain.Prices.Model;
using Lykke.RabbitMqBroker;
using Lykke.RabbitMqBroker.Publisher;
using Lykke.RabbitMqBroker.Subscriber;
using Lykke.Service.FIXQuotes.Core;
using Lykke.Service.FIXQuotes.Core.Domain.Models;
using Lykke.Service.FIXQuotes.Core.Services;
using Lykke.Service.FIXQuotes.Services;
using Lykke.Service.QuotesHistory.Client.AutorestClient;
using Lykke.SettingsReader;
using Microsoft.Rest.TransientFaultHandling;

namespace Lykke.Service.FIXQuotes.Modules
{
    public class ServiceModule : Module
    {
        private readonly AppSettings.FixQuotesSettings _settings;
        private readonly ILog _log;


        public ServiceModule(IReloadingManager<AppSettings> settings, ILog log)
        {
            _settings = settings.CurrentValue.FixQuotesService;
            _log = log;
        }

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_settings)
                .SingleInstance();

            builder.RegisterInstance(_log)
                .As<ILog>()
                .SingleInstance();


            builder.RegisterType<QuotesHistoryService>()
                .WithProperty(nameof(QuotesHistoryService.BaseUri), new Uri(_settings.Services.QuotesHistoryServiceUrl))
                .As<IQuotesHistoryService>();

            RegisterRabbit(builder);

            builder.RegisterType<FixQuotesManager>()
                .As<IFixQuotesManager>()
                .SingleInstance();

        }

        private void RegisterRabbit(ContainerBuilder builder)
        {
            var reciverRabbitMqSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _settings.QuoteFeedRabbit.ConnectionString,
                QueueName = _settings.QuoteFeedRabbit.QueueName,
                ExchangeName = _settings.QuoteFeedRabbit.ExchangeName,
                IsDurable = false
            };


            builder.Register(c => new RabbitMqSubscriber<IQuote>(reciverRabbitMqSettings,
                        new ResilientErrorHandlingStrategy(_log, reciverRabbitMqSettings, TimeSpan.FromSeconds(10)))
                    .SetMessageDeserializer(new JsonMessageDeserializer<Quote>())
                    .SetMessageReadStrategy(new MessageReadWithTemporaryQueueStrategy())
                    .SetLogger(c.Resolve<ILog>()))
                .As<IMessageConsumer<IQuote>>()
                .As<IStopable>()
                .AsSelf()
                .SingleInstance();

            var publisherRabbitMqSettings = new RabbitMqSubscriptionSettings
            {
                ConnectionString = _settings.FixQuoteFeedRabbit.ConnectionString,
                QueueName = _settings.FixQuoteFeedRabbit.QueueName,
                ExchangeName = _settings.FixQuoteFeedRabbit.ExchangeName,
                IsDurable = true
            };

            builder.Register(c => new RabbitMqPublisher<IEnumerable<FixQuoteModel>>(publisherRabbitMqSettings)
                    .SetSerializer(new JsonMessageSerializer<IEnumerable<FixQuoteModel>>())
                    .SetPublishStrategy(new DefaultFanoutPublishStrategy(publisherRabbitMqSettings))
                    .DisableInMemoryQueuePersistence()
                    .PublishSynchronously()
                    .SetLogger(c.Resolve<ILog>()))
                .As<IMessageProducer<IEnumerable<FixQuoteModel>>>()
                .As<IStopable>()
                .AsSelf()
                .SingleInstance();
        }
    }
}
