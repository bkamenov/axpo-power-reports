using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PowerReports.Services;

namespace PowerReport
{
    class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var powerReportService = host.Services.GetRequiredService<PowerReportService>();
            Task.Run(() => powerReportService.RunAsync()).GetAwaiter().GetResult();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
           Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                      .AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<PowerReportService>();
                services.AddLogging(config =>
                {
                    config.AddConsole();
                    config.AddDebug();
                });
            });
    }
}
