using System;
using System.Globalization;

namespace GeometryHelper
{
    /// <summary>
    /// What a clash check between many parts looks for, and how many threads it may use.
    /// </summary>
    public sealed class ClashOptions : IEquatable<ClashOptions>
    {
        /// <summary>
        /// Gets the options that report parts running into each other and parts touching, with no clearance,
        /// on every processor.
        /// </summary>
        public static ClashOptions Default { get; } = new ClashOptions();

        /// <summary>
        /// Initializes the options.
        /// </summary>
        /// <param name="clearance">
        /// How far apart two parts have to stay; two nearer than this are reported as
        /// <see cref="Enums.ClashKind.Clearance"/>. Nought asks only for parts running into or touching each other.
        /// </param>
        /// <param name="includeTouching">Whether parts that touch without sharing volume are reported.</param>
        /// <param name="maxDegreeOfParallelism">How many threads may check pairs at once; -1 for every processor.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the clearance is negative, NaN or infinite, or the parallelism is neither -1 nor positive.
        /// </exception>
        public ClashOptions(double clearance = 0.0, bool includeTouching = true, int maxDegreeOfParallelism = -1)
        {
            Guard.NonNegative(clearance, nameof(clearance), "A clearance has to be a number, and cannot be negative.");

            if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Use -1 for every processor, or a positive count.");
            }

            Clearance = clearance;
            IncludeTouching = includeTouching;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>
        /// Gets how far apart two parts have to stay; nought when no clearance is checked.
        /// </summary>
        public double Clearance { get; }

        /// <summary>
        /// Gets whether parts that touch without sharing volume are reported.
        /// </summary>
        public bool IncludeTouching { get; }

        /// <summary>
        /// Gets how many threads may check pairs at once; -1 for every processor.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        /// <inheritdoc/>
        public bool Equals(ClashOptions other)
        {
            return other != null
                && Clearance.Equals(other.Clearance)
                && IncludeTouching == other.IncludeTouching
                && MaxDegreeOfParallelism == other.MaxDegreeOfParallelism;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ClashOptions other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Clearance.GetHashCode();
                hash = hash * 397 ^ IncludeTouching.GetHashCode();
                hash = hash * 397 ^ MaxDegreeOfParallelism;
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Clearance: {0}, IncludeTouching: {1}, MaxDegreeOfParallelism: {2})",
                Clearance,
                IncludeTouching,
                MaxDegreeOfParallelism);
        }
    }
}
