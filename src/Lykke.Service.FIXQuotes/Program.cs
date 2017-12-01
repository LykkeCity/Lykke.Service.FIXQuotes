using System;
using System.IO;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;

namespace Lykke.Service.FIXQuotes
{
    internal sealed class Program
    {
        public static string EnvInfo => Environment.GetEnvironmentVariable("ENV_INFO");

        public static async Task Main(string[] args)
        {
            var webHostCancellationTokenSource = new CancellationTokenSource();
            var end = new ManualResetEvent(false);

            AssemblyLoadContext.Default.Unloading += ctx =>
            {
                Console.WriteLine("SIGTERM received");

                webHostCancellationTokenSource.Cancel();

                end.WaitOne();
            };

            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://*:5000")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .Build();

            await host.RunAsync(webHostCancellationTokenSource.Token);

            end.Set();

            Console.WriteLine("Terminated");
        }
    }
}
