using Microsoft.WindowsAzure.ResourceStack.Common.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    /// <summary>
    /// Limited memory cache based on MemoryCache.
    /// </summary>
    /// <typeparam name="T">The type of cache data.</typeparam>
    public class FrontdoorLimitedMemoryCache<T>
    {
        /// <summary>
        /// Gets or sets the cache performance counters.
        /// </summary>
        private CachePerformanceCountersInstance FrontdoorMemoryCachePerformanceCounters { get; set; }

        /// <summary>
        /// Gets or sets the cache name.
        /// </summary>
        private string CacheName { get; set; }

        /// <summary>
        /// Gets or sets the cache staleness threshold.
        /// </summary>
        private TimeSpan CacheStalenessThreshold { get; set; }

        /// <summary>
        /// Gets or sets the cache staleness jitter. 
        /// </summary>
        private TimeSpan CacheStalenessJitter { get; set; }

        /// <summary>
        /// Gets or sets the limited memory cache.
        /// </summary>
        private LimitedMemoryCache<MemoryCacheItem<T>> MemoryCache { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FrontdoorLimitedMemoryCache{T}" /> class.
        /// </summary>
        /// <param name="cacheName">The cache name.</param>
        /// <param name="memoryLimitMegabytes">The cache memory limit in mega bytes.</param>
        /// <param name="pollingInterval">The cache polling interval.</param>
        /// <param name="stalenessThreshold">The cache data staleness threshold.</param>
        /// <param name="stalenessJitter">The cache data staleness jitter.</param>
        public FrontdoorLimitedMemoryCache(string cacheName, int memoryLimitMegabytes, TimeSpan pollingInterval, TimeSpan stalenessThreshold, TimeSpan stalenessJitter)
        {
            this.CacheName = cacheName;
            this.CacheStalenessThreshold = stalenessThreshold;
            this.CacheStalenessJitter = stalenessJitter;
            this.FrontdoorMemoryCachePerformanceCounters = CachePerformanceCounters.GetInstance(cacheName);

            this.MemoryCache = new LimitedMemoryCache<MemoryCacheItem<T>>(
                cacheName: cacheName,
                cacheMemoryLimitMegabytes: memoryLimitMegabytes,
                pollingInterval: pollingInterval);
        }

        /// <summary>
        /// Adds or gets existing data from cache.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="getFreshData">The delegate used to get data if cache is empty or stale.</param>
        /// <param name="allowCaching">The delegate used to determine if caching the data is allowed.</param>
        public async Task<T> AddOrGetExisting(string cacheKey, Func<Task<T>> getFreshData, Func<T, bool> allowCaching = null)
        {
            MemoryCacheItem<T> cacheItem = null;

            using (FrontdoorMemoryCachePerformanceCounters.MeasureOperation())
            {
                cacheItem = this.MemoryCache.GetCacheItem(cacheKey: cacheKey);
            }
            
            if (cacheItem == null || cacheItem.ExpirationTime < DateTime.UtcNow)
            {
                FrontdoorMemoryCachePerformanceCounters.CacheMiss();

                cacheItem = new MemoryCacheItem<T>
                {
                    ExpirationTime = DateTime.UtcNow
                        .Add(this.CacheStalenessThreshold)
                        .Add(RandomNumber.Next(this.CacheStalenessJitter)),

                    CacheData = await getFreshData().ConfigureAwait(continueOnCapturedContext: false)
                };

                if (cacheItem.CacheData != null && (allowCaching == null || allowCaching(cacheItem.CacheData)))
                {
                    this.MemoryCache.SetCacheItem(
                        cacheKey: cacheKey,
                        data: cacheItem,
                        absoluteExpirationTime: cacheItem.ExpirationTime);
                }
            }
            else
            {
                FrontdoorMemoryCachePerformanceCounters.CacheHit();
            }

            return cacheItem.CacheData;            
        }

        /// <summary>
        /// Removes the cache item with the given key. 
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void Remove(string cacheKey)
        {
            this.MemoryCache.RemoveCacheItem(cacheKey: cacheKey);
        }

        #region cache data item

        /// <summary>
        /// Cache item used with expiration item cache.
        /// </summary>
        /// <typeparam name="TCacheData">The type of cache data.</typeparam>
        private class MemoryCacheItem<TCacheData>
        {
            /// <summary>
            /// Gets or sets the expiration time of the cached item.
            /// </summary>
            public DateTime ExpirationTime
            {
                get;
                set;
            }

            /// <summary>
            /// Gets or sets the items.
            /// </summary>
            public TCacheData CacheData
            {
                get;
                set;
            }
        }

        #endregion
    }
}
