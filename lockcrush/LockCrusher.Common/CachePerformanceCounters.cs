using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Timer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    public class CachePerformanceCounters
    {
        #region "Singleton"
        /// <summary>
        /// Performance counters category instance singleton value.
        /// </summary>
        private static CachePerformanceCounters InstanceValue;

        /// <summary>
        /// Performance counters category instance singleton lock.
        /// </summary>
        private static object InstanceLock = new object();

        public static void SetMetrics(IMetrics metrics)
        {
            CachePerformanceCounters.metrics = metrics;
        }

        private static IMetrics metrics;

        /// <summary>
        /// Gets the performance counters category instance singleton value.
        /// </summary>
        public static CachePerformanceCounters Instance
        {
            get
            {
                if (CachePerformanceCounters.InstanceValue == null)
                {
                    lock (CachePerformanceCounters.InstanceLock)
                    {
                        if (CachePerformanceCounters.InstanceValue == null)
                        {
                            try
                            {
                                CachePerformanceCounters.InstanceValue = new CachePerformanceCounters();
                            }
                            catch (Exception)
                            {
                                CachePerformanceCounters.InstanceValue = null;
                                throw;
                            }
                        }
                    }
                }

                return CachePerformanceCounters.InstanceValue;
            }
        }
        #endregion

        private readonly ConcurrentDictionary<string, CachePerformanceCountersInstance> counters;

        public CachePerformanceCounters()
        {
            this.counters = new ConcurrentDictionary<string, CachePerformanceCountersInstance>();            
        }

        public static CachePerformanceCountersInstance GetInstance(string instanceName)
        {
            CachePerformanceCounters category = CachePerformanceCounters.Instance;

            return category.counters.GetOrAdd(instanceName,
                new CachePerformanceCountersInstance(metrics, instanceName));         
        }
    }

    public class CachePerformanceCountersInstance
    {
        private readonly IMetrics metrics;
        private readonly string instanceName;
        private readonly TimerOptions requestTimer;

        private readonly CounterOptions hitCounter;
        private readonly CounterOptions missCounter;
        private readonly CounterOptions totalCounter;

        public CachePerformanceCountersInstance(IMetrics metrics, string instanceName)
        {
            this.metrics = metrics;
            this.instanceName = instanceName;

            requestTimer = new TimerOptions
            {
                Name = "CacheTiming_" + instanceName,
                MeasurementUnit = Unit.Requests,
                DurationUnit = TimeUnit.Nanoseconds,
                RateUnit = TimeUnit.Nanoseconds,
                Context = "Cache"
            };

            hitCounter = new CounterOptions
            {
                Name = "CacheHit_" + instanceName,
                MeasurementUnit = Unit.Requests,
                ResetOnReporting = true,
                Context = "Cache"
            };

            missCounter = new CounterOptions
            {
                Name = "CacheMiss_" + instanceName,
                MeasurementUnit = Unit.Requests,
                ResetOnReporting = true,
                Context = "Cache"
            };

            totalCounter = new CounterOptions
            {
                Name = "CacheTotal" + instanceName,
                MeasurementUnit = Unit.Requests,
                ResetOnReporting = true,
                Context = "Cache"
            };

        }

        public void CacheMiss()
        {
            metrics.Measure.Counter.Increment(missCounter);
            metrics.Measure.Counter.Increment(totalCounter);
        }

        public void CacheHit()
        {
            metrics.Measure.Counter.Increment(hitCounter);
            metrics.Measure.Counter.Increment(totalCounter);
        }

        public IDisposable MeasureOperation(string operationName = null)
        {
            //return Disposable.Empty();

            return (operationName != null)
                ? metrics.Measure.Timer.Time(requestTimer, operationName)
                : metrics.Measure.Timer.Time(requestTimer);
        }
    }

    public class Disposable
    {
        public static IDisposable Empty()
        {
            return new EmptyDisposable();
        }

        public class EmptyDisposable : IDisposable
        {
            public void Dispose()
            {
                
            }
        }
    }

   
    
}
