using App.Metrics;
using App.Metrics.Gauge;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    public class CacheHammer
    {
        private readonly IMetrics metrics;
        private readonly FrontdoorLimitedMemoryCache<CachedSubscription> cache;
        private readonly SubscriptionDataSource dataSource;
        private readonly TimeSpan minDwellTime;
        private readonly TimeSpan jitterDwellTime;
        private long concurrentWorkers = 0;
        
        private readonly GaugeOptions concurrentWorkersGauge = new GaugeOptions
        {
            Name = "Concurrent Workers",
            MeasurementUnit = Unit.Threads
        };

       

        public CacheHammer(IMetricsRoot metrics, 
            FrontdoorLimitedMemoryCache<CachedSubscription> cache, 
            SubscriptionDataSource dataSource, 
            TimeSpan minDwellTime,
            TimeSpan jitterDwellTime)
        {
            this.metrics = metrics;
            this.cache = cache;
            this.dataSource = dataSource;
            this.minDwellTime = minDwellTime;
            this.jitterDwellTime = jitterDwellTime;

            metrics.Measure.Gauge.SetValue(concurrentWorkersGauge, 0);
        }

        public Task CreateWorker(CancellationToken token)
        {            
           var workerTask = Task.Run(async () =>
           {
               while (!token.IsCancellationRequested)
               {
                   var subId = dataSource.GetRandomSubscriptionId();

                   await cache.AddOrGetExisting(subId, 
                       () => dataSource.GetSubcriptionAsync(subId), 
                       (c) => true
                   );
                   
                   var dwellTime = minDwellTime + TimeSpan.FromMilliseconds(
                       RandomNumber.Next(jitterDwellTime.Milliseconds));
                   await Task.Delay(dwellTime);
               }
            }).ContinueWith( (t) =>
            {
                Interlocked.Decrement(ref concurrentWorkers);
                metrics.Measure.Gauge.SetValue(concurrentWorkersGauge,
                    Interlocked.Read(ref concurrentWorkers));

                if (t.IsFaulted)
                {
                    Console.WriteLine("Error: " + t.Exception.ToString());
                }
            });

            Interlocked.Increment(ref concurrentWorkers);
            metrics.Measure.Gauge.SetValue(concurrentWorkersGauge, 
                Interlocked.Read(ref concurrentWorkers));

            return workerTask;

        }
    }
}
