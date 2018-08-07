//-----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------

namespace Microsoft.WindowsAzure.ResourceStack.Common.Algorithms
{
    using System;
    using System.Collections.Specialized;
    using System.Runtime.Caching;

    /// <summary>
    /// The limited memory cache.
    /// </summary>
    /// <typeparam name="T">Type being cached.</typeparam>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Instances of this type are meant to be singletons.")]
    public class LimitedMemoryCache<T>
    {
        /// <summary>
        /// The memory cache
        /// </summary>
        private readonly MemoryCache memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="LimitedMemoryCache&lt;T&gt;" /> class.
        /// </summary>
        /// <param name="cacheName">The cache name.</param>
        /// <param name="cacheMemoryLimitMegabytes">The cache memory limit in mega bytes.</param>
        /// <param name="pollingInterval">The polling interval.</param>
        public LimitedMemoryCache(string cacheName, int cacheMemoryLimitMegabytes, TimeSpan pollingInterval)
        {
            var memoryCacheConfig = new NameValueCollection(2);
            memoryCacheConfig.Add("CacheMemoryLimitMegabytes", cacheMemoryLimitMegabytes.ToString());
            memoryCacheConfig.Add("PollingInterval", pollingInterval.ToString());

            this.memoryCache = new MemoryCache(name: cacheName, config: memoryCacheConfig);            
        }

        /// <summary>
        /// Gets the cache entry.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public T GetCacheItem(string cacheKey)
        {
            // Seriously, this is a really fancy two-layer wrapper around a (T)
            //return this.memoryCache[cacheKey].Cast<T>();
            return (T)this.memoryCache[cacheKey];
        }

        /// <summary>
        /// Adds the cache entry.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="data">The data to add.</param>
        /// <param name="absoluteExpirationTime">The absolute expiration time.</param>
        public void SetCacheItem(string cacheKey, T data, DateTimeOffset absoluteExpirationTime)
        {
            this.memoryCache.Set(key: cacheKey, value: data, absoluteExpiration: absoluteExpirationTime);
        }

        /// <summary>
        /// Removes the cache entry.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        public void RemoveCacheItem(string cacheKey)
        {
            this.memoryCache.Remove(key: cacheKey);
        }
    }
}
