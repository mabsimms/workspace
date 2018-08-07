using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LockCrusher.Common
{
    /// <summary>
    /// Random number generator
    /// </summary>
    public static class RandomNumber
    {
        /// <summary>
        /// The thread local instance of random number generator
        /// </summary>
        private static readonly ThreadLocal<Random> ThreadLocal = new ThreadLocal<Random>(
            () => new Random(Interlocked.Increment(ref seed)));

        /// <summary>
        /// Random number generator seed value
        /// </summary>
        private static int seed = Environment.TickCount;

        /// <summary>
        /// Returns a random number between 0.0 and 1.0.
        /// </summary>
        public static double NextDouble()
        {
            return RandomNumber.ThreadLocal.Value.NextDouble();
        }

        /// <summary>
        /// Produces 32-bit nonnegative random number
        /// </summary>
        public static int Next()
        {
            return RandomNumber.ThreadLocal.Value.Next();
        }

        /// <summary>
        /// Produces 32-bit nonnegative random number less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        public static int Next(int maxValue)
        {
            return RandomNumber.ThreadLocal.Value.Next(maxValue);
        }

        /// <summary>
        /// Produces 32-bit nonnegative random number within a specified range.
        /// </summary>
        /// <param name="minValue">The min value.</param>
        /// <param name="maxValue">The max value.</param>
        public static int Next(int minValue, int maxValue)
        {
            return RandomNumber.ThreadLocal.Value.Next(minValue, maxValue);
        }

        /// <summary>
        /// Produces the random interval less than the specified maximum.
        /// </summary>
        /// <param name="maximum">The maximum value.</param>
        public static TimeSpan Next(TimeSpan maximum)
        {
            return RandomNumber.Next(TimeSpan.Zero, maximum);
        }

        /// <summary>
        /// Produces the random interval within a specified range.
        /// </summary>
        /// <param name="minimum">The minimum value.</param>
        /// <param name="maximum">The maximum value.</param>
        public static TimeSpan Next(TimeSpan minimum, TimeSpan maximum)
        {
            return TimeSpan.FromTicks(minimum.Ticks + (long)(RandomNumber.NextDouble() * (maximum.Ticks - minimum.Ticks)));
        }

        /// <summary>
        /// Produces the random interval given an interval and jitter.
        /// </summary>
        /// <param name="interval">The interval.</param>
        /// <param name="jitter">The jitter.</param>
        public static TimeSpan Next(TimeSpan interval, double jitter)
        {
            return RandomNumber.Next(interval.ScaleBy(1.0 - jitter), interval);
        }

        /// <summary>
        /// Selects the random value with specified probability distribution.
        /// </summary>
        /// <param name="probabilityDistribution">The probability distribution.</param>
        public static string Select(Dictionary<string, int> probabilityDistribution)
        {
            var randomValue = RandomNumber.Next(probabilityDistribution.Sum(distributionKvp => distributionKvp.Value));
            foreach (var distributionKvp in probabilityDistribution)
            {
                if (randomValue < distributionKvp.Value)
                {
                    return distributionKvp.Key;
                }

                // NOTE(ilygre): instead of calculating cumulative sum we simple subtract current value and move forward.
                randomValue -= distributionKvp.Value;
            }

            throw new InvalidOperationException("Code defect: unable to select randomized value.");
        }
    }

    public static class DateTimeExtensions
    {
        /// <summary>
        /// Scales interval by specified value.
        /// </summary>
        /// <param name="interval">The interval to scale.</param>
        /// <param name="value">The value to scale by.</param>
        public static TimeSpan ScaleBy(this TimeSpan interval, double value)
        {
            return TimeSpan.FromTicks((long)(interval.Ticks * value));
        }
    }
}
