using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PushoverQ
{
    /// <summary>
    /// A subscription description, containing both the topic and subscription name.
    /// </summary>
    public struct SubscriptionDescription : IEquatable<SubscriptionDescription>
    {
        /// <summary>
        /// Gets the topic.
        /// </summary>
        public string Topic { get; private set; }

        /// <summary>
        /// Gets the subscription.
        /// </summary>
        public string Subscription { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubscriptionDescription"/> struct.
        /// </summary>
        /// <param name="topic"> The topic. </param>
        /// <param name="subscription"> The subscription. </param>
        public SubscriptionDescription(string topic, string subscription)
            : this()
        {
            if (topic == null) throw new ArgumentNullException("topic");
            if (subscription == null) throw new ArgumentNullException("subscription");

            Topic = topic;
            Subscription = subscription;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Topic.GetHashCode() * 397) ^ Subscription.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public static bool operator ==(SubscriptionDescription left, SubscriptionDescription right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc/>
        public static bool operator !=(SubscriptionDescription left, SubscriptionDescription right)
        {
            return !left.Equals(right);
        }

        /// <inheritdoc/>
        public bool Equals(SubscriptionDescription other)
        {
            return string.Equals(Topic, other.Topic) && string.Equals(Subscription, other.Subscription);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SubscriptionDescription && Equals((SubscriptionDescription)obj);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Topic + "/" + Subscription;
        }
    }
}
