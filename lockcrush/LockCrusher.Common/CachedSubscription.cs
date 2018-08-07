using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    /// <summary>
    /// The cached subscription.
    /// </summary>
    public class CachedSubscription
    {
        /// <summary>
        /// Gets or sets the subscription tenant identifier.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the subscription identifier.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string SubscriptionId { get; set; }

        /// <summary>
        /// Gets or sets the subscription display name.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the state of the subscription.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string State { get; set; }

        /// <summary>
        /// Gets or sets the offer category.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string OfferCategory { get; set; }

        /// <summary>
        /// Gets or sets the subscription policies.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string SubscriptionPolicies { get; set; }

        /// <summary>
        /// Gets or sets the internal subscription policies.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string InternalSubscriptionPolicies { get; set; }

        /// <summary>
        /// Gets or sets the subscription feature registrations.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string[] FeatureRegistrations { get; set; }

        /// <summary>
        /// Gets or sets the subscription registrations.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string[] SubscriptionRegistrations { get; set; }

        /// <summary>
        /// Gets or sets the subscription entitlement start date.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public DateTime? EntitlementStartDate { get; set; }

        /// <summary>
        /// Gets or sets the subscription managed by tenant identifiers.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string[] ManagedByTenantIds { get; set; }

        /// <summary>
        /// Gets or sets the availability zones.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string[] AvailabilityZones { get; set; }

        /// <summary>
        /// Gets or sets the subscription account owner.
        /// </summary>
        [JsonProperty(Required = Required.Default)]
        public string AccountOwner { get; set; }

      
    }
}
