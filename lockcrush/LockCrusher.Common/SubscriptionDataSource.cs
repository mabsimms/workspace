using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    public class SubscriptionDataSource
    {
        public SubscriptionDataSource(int subscriptionSetSize, TimeSpan minAccess, TimeSpan jitter)
        {
            this.subscriptionSetSize = subscriptionSetSize;
            this.minAccess = minAccess;
            this.jitter = jitter;

            // Initialize the data
            ids = Enumerable.Range(0, subscriptionSetSize)
                .Select(e => Guid.NewGuid().ToString()).ToArray();

            data = ids.Select(e => new KeyValuePair<string, CachedSubscription>(
                e, new CachedSubscription() { SubscriptionId = e }))
                .ToImmutableDictionary();
        }

        
        private ImmutableDictionary<string, CachedSubscription> data;
        private string[] ids;        
        private readonly int subscriptionSetSize;
        private readonly TimeSpan minAccess;
        private readonly TimeSpan jitter;

        public string[] SubscriptionIds
        {
            get
            {
                return ids;
            }
        }

        public async Task<CachedSubscription> GetSubcriptionAsync(string subscriptionId, TimeSpan dwellTime)
        {
            await Task.Delay(dwellTime);
            return data[subscriptionId];
        }

        public async Task<CachedSubscription> GetSubcriptionAsync(string subscriptionId)
        {
            var dwellTime = minAccess + TimeSpan.FromMilliseconds(
                    RandomNumber.Next(jitter.Milliseconds));
            await Task.Delay(dwellTime);
            return data[subscriptionId];
        }

        public string GetRandomSubscriptionId()
        {
            var index = RandomNumber.Next(subscriptionSetSize);
            return ids[index];
        }
    }
}
