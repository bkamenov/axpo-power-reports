using Axpo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PowerReports.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerReports.Services
{
    class PowerReportService
    {
        private readonly ILogger<PowerService> _logger;
        private readonly IConfiguration _configuration;
        private readonly PowerService _powerService;
        private readonly string _folderPath;
        private readonly int _intervalMinutes;

        public PowerReportService(ILogger<PowerService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _powerService = new PowerService();
            _folderPath = _configuration.GetValue<string>("FolderPath", ".\\"); //Default to CWD
            _intervalMinutes = _configuration.GetValue<int>("IntervalMinutes", 15); // Default to 15 if not provided
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("Power Reporting Service started.");

            //Generate the first report immediately
            await AggregatedTradeReportAsync();

            //Start the timer after the first interval elapses
            var timer = new System.Threading.Timer(async _ =>
            {
                await AggregatedTradeReportAsync();
            }, null, TimeSpan.FromMinutes(_intervalMinutes), TimeSpan.FromMinutes(_intervalMinutes));

            //Keep the app running
            await Task.Delay(-1);
        }

        private async Task AggregatedTradeReportAsync()
        {
            //Retry interval on failure
            const int delayMilliseconds = 2000;

            _logger.LogInformation("Starting aggregated trade report extraction...");

            while(true) //Depending on the needs we could have defined a maxRetries limit.
            {
                var nowDateTimeUtc = DateTime.UtcNow;
                var dayAheadDate = nowDateTimeUtc.Date.AddDays(1);

                IEnumerable<PowerTrade> trades;
                try
                {
                    trades = await _powerService.GetTradesAsync(dayAheadDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed trade extraction. Retrying...");
                    await Task.Delay(delayMilliseconds);
                    continue;
                }

                try
                {
                    _logger.LogInformation("Generating aggregated trade report...");

                    List<AggregatedPosition> aggregatedTrade =
                    trades.SelectMany(t => t.Periods)
                    .GroupBy(p => p.Period)
                    .Select(g => new AggregatedPosition
                    {
                        //The -2 guarantees us that the first position is at 23:00 the same day
                        DateTime = dayAheadDate.AddHours(g.Key - 2).ToString("yyyy-MM-ddTHH:00:00Z", CultureInfo.InvariantCulture),
                        Volume = g.Sum(p => p.Volume)
                    })
                    .OrderBy(x => x.DateTime)
                    .ToList();

                    var fileName = $"PowerPosition_{dayAheadDate:yyyyMMdd}_{nowDateTimeUtc:yyyyMMddHHmm}.csv";
                    var filePath = Path.Combine(_folderPath, fileName);

                    using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                    {
                        writer.WriteLine("Datetime;Volume");
                        foreach (var record in aggregatedTrade)
                        {
                            writer.WriteLine($"{record.DateTime};{record.Volume:F}");
                        }
                    }

                    _logger.LogInformation($"Aggregated trade report completed. File saved to {filePath}");

                    //Task completed
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while generating the aggregated trade report report. Retrying...");
                    await Task.Delay(delayMilliseconds);
                    continue;
                }
            }
        }
    }
}
