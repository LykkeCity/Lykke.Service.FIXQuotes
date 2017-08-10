using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using AzureStorage;
using AzureStorage.Tables;
using AzureStorage.Tables.Decorators;
using Common.Log;
using Lykke.Service.FIXQuotes.AzureRepositories;
using Lykke.Service.FIXQuotes.Core;
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

            var storage = new AzureTableStorage<FixQuoteDto>(_settings.Db.FixQuotesBackupConnString, "fix_quotes_backup", _log);
            var wrapper = new RetryOnFailureAzureTableStorageDecorator<FixQuoteDto>(storage, 5, 5, TimeSpan.FromSeconds(10));

            builder.RegisterInstance(wrapper)
                .As<INoSQLTableStorage<FixQuoteDto>>();

            builder.RegisterType<FixQuoteRepository>()
                .As<IFixQuoteRepository>();

            builder.RegisterType<QuoteBroker>()
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
