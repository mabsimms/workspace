using App.Metrics;
using LockCrusher.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace lockcrush
{
    class Program
    {
        static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {
            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appconfig.json", optional: false)
                    .Build();

                IMetricsBuilder metricsBuilder = new MetricsBuilder();
                if (config.GetValue<bool>("metrics:influxdb:enabled", false))
                {
                    metricsBuilder = metricsBuilder
                        .Report.ToInfluxDb(
                            url: config.GetValue<string>("metrics:influxdb:url"),
                            database: config.GetValue<string>("metrics:influxdb:database"),
                            flushInterval: config.GetValue<TimeSpan>("metrics:influxdb:flushinterval", TimeSpan.FromSeconds(5))
                    );
                }
                var metrics = metricsBuilder.Build();
                CachePerformanceCounters.SetMetrics(metrics);

                var cancellationTokenSource = new CancellationTokenSource();

                // Set up a metrics reporting thread
                var reportTimer = new System.Timers.Timer(1000);
                reportTimer.Elapsed += async (sender, e) =>
                {
                    await Task.WhenAll(metrics.ReportRunner.RunAllAsync());
                    // TODO - reset counters here
                };
                reportTimer.Start();

                var dataSource = new SubscriptionDataSource(
                    subscriptionSetSize: config.GetValue<int>("dataSource:simulated:subscriptionCount", 100000),
                    minAccess: config.GetValue<TimeSpan>("dataSource:simulated:minDwellTime", TimeSpan.FromMilliseconds(250)),
                    jitter: config.GetValue<TimeSpan>("dataSource:simulated:jitterDwellTime", TimeSpan.FromMilliseconds(500))
                );

                var cache = new FrontdoorLimitedMemoryCache<CachedSubscription>(
                    cacheName: "subscriptions",
                    memoryLimitMegabytes: config.GetValue<int>("cache:subscription:maximumMemory", 2000),
                    pollingInterval: config.GetValue<TimeSpan>("cache:subscription:pollingInterval", TimeSpan.FromSeconds(30)),
                    stalenessThreshold: config.GetValue<TimeSpan>("cache:subscription:staleThreshold", TimeSpan.FromSeconds(30)),
                    stalenessJitter: config.GetValue<TimeSpan>("cache:subscription:staleJitter", TimeSpan.FromSeconds(1))
                );

                var cacheHammer = new CacheHammer(metrics, cache, dataSource,
                    minDwellTime: config.GetValue<TimeSpan>("cacheHammer:minDwellTime", TimeSpan.Zero),
                    jitterDwellTime: config.GetValue<TimeSpan>("cacheHammer:minDwellTime", TimeSpan.FromMilliseconds(5))
                );

                // Check for prefill cache
                if (config.GetValue<bool>("prefillCache", false))
                {
                    Console.WriteLine("Prefilling cache..");
                    var percentage = config.GetValue<int>("prefillPercentage", 90) / 100.0;
                    var lastIndex = (int)(dataSource.SubscriptionIds.Length * percentage);
                    var ids = dataSource.SubscriptionIds.Take(lastIndex);

                    foreach (var id in ids)
                    {                                                
                        await cache.AddOrGetExisting(id, 
                            async () => await dataSource.GetSubcriptionAsync(id, TimeSpan.Zero), 
                            (e) => true);
                    }

                    Console.WriteLine("Cache prefilled to {0} percent", percentage * 100.0);
                }

                // Ramp the work runners 
                var maxTasks = config.GetValue<int>("maxWorkers", 1024);
                var dwellTime = config.GetValue<TimeSpan>("workerDwellTime", TimeSpan.FromSeconds(15));

                var currentTasks = 0;                
                var workerTasks = new List<Task>();

                while (!cancellationTokenSource.IsCancellationRequested)
                {
                    if (currentTasks < maxTasks)
                    {                        
                        var task = cacheHammer.CreateWorker(cancellationTokenSource.Token);
                        workerTasks.Add(task);
                        currentTasks++;

                        Console.WriteLine("Added new worker task; current count = {0}", currentTasks);
                    }
                    await Task.Delay(dwellTime);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Console.ReadLine();
            }
        }
    }
}
