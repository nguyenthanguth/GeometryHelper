using System;
using System.Globalization;

namespace GeometryHelper.Clash
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
        /// Initializes the options with no minimum depth or volume: every overlap is a <see cref="ClashKind.Hard"/> clash.
        /// </summary>
        /// <param name="clearance">
        /// How far apart two parts have to stay; two nearer than this are reported as
        /// <see cref="ClashKind.Clearance"/>. Nought asks only for parts running into or touching each other.
        /// </param>
        /// <param name="includeTouching">Whether parts that touch without sharing volume are reported.</param>
        /// <param name="maxDegreeOfParallelism">How many threads may check pairs at once; -1 for every processor.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the clearance is negative, NaN or infinite, or the parallelism is neither -1 nor positive.
        /// </exception>
        /// <remarks>
        /// The constructor as it was before the minimums, kept so that code built against it runs as it did.
        /// </remarks>
        public ClashOptions(double clearance, bool includeTouching, int maxDegreeOfParallelism)
            : this(clearance, includeTouching, maxDegreeOfParallelism, 0.0, 0.0)
        {
        }

        /// <summary>
        /// Initializes the options.
        /// </summary>
        /// <param name="clearance">
        /// How far apart two parts have to stay; two nearer than this are reported as
        /// <see cref="ClashKind.Clearance"/>. Nought asks only for parts running into or touching each other.
        /// </param>
        /// <param name="includeTouching">Whether parts that touch without sharing volume are reported.</param>
        /// <param name="maxDegreeOfParallelism">How many threads may check pairs at once; -1 for every processor.</param>
        /// <param name="minimumDepth">
        /// How deep two parts have to run into each other for a <see cref="ClashKind.Hard"/> clash; see
        /// <see cref="MinimumDepth"/>. Nought reports every overlap.
        /// </param>
        /// <param name="minimumVolume">
        /// How much volume two bodies have to share for a <see cref="ClashKind.Hard"/> clash; see
        /// <see cref="MinimumVolume"/>. Nought reports every overlap.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the clearance, the minimum depth or the minimum volume is negative, NaN or infinite, or the
        /// parallelism is neither -1 nor positive.
        /// </exception>
        public ClashOptions(double clearance = 0.0, bool includeTouching = true, int maxDegreeOfParallelism = -1, double minimumDepth = 0.0, double minimumVolume = 0.0)
        {
            Guard.NonNegative(clearance, nameof(clearance), "A clearance has to be a number, and cannot be negative.");
            Guard.NonNegative(minimumDepth, nameof(minimumDepth), "A minimum depth has to be a number, and cannot be negative.");
            Guard.NonNegative(minimumVolume, nameof(minimumVolume), "A minimum volume has to be a number, and cannot be negative.");

            if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Use -1 for every processor, or a positive count.");
            }

            Clearance = clearance;
            IncludeTouching = includeTouching;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
            MinimumDepth = minimumDepth;
            MinimumVolume = minimumVolume;
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

        /// <summary>
        /// Gets how deep two parts have to run into each other for a <see cref="ClashKind.Hard"/> clash; nought
        /// reports every overlap.
        /// </summary>
        /// <remarks>
        /// A pair that shares volume but less deeply than this is taken as touching: reported as
        /// <see cref="ClashKind.Touch"/> when <see cref="IncludeTouching"/> is set, keeping the
        /// <see cref="ClashResult.Depth"/>, <see cref="ClashResult.Volume"/> and <see cref="ClashResult.Overlaps"/> it
        /// was found with, and not at all otherwise. The depth is the one <see cref="ClashResult.Depth"/> reports:
        /// a bar grazing a flange by half a millimetre is half a millimetre deep, however long the graze.
        /// </remarks>
        public double MinimumDepth { get; }

        /// <summary>
        /// Gets how much volume two bodies have to share for a <see cref="ClashKind.Hard"/> clash; nought reports
        /// every overlap.
        /// </summary>
        /// <remarks>
        /// A pair sharing less is taken as touching, as for <see cref="MinimumDepth"/>. A bar checked by its centre line
        /// (<see cref="ClashBar"/>) has no volume measured, and only the depth applies to it.
        /// </remarks>
        public double MinimumVolume { get; }

        /// <inheritdoc/>
        public bool Equals(ClashOptions other)
        {
            return other != null
                && Clearance.Equals(other.Clearance)
                && IncludeTouching == other.IncludeTouching
                && MaxDegreeOfParallelism == other.MaxDegreeOfParallelism
                && MinimumDepth.Equals(other.MinimumDepth)
                && MinimumVolume.Equals(other.MinimumVolume);
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
                hash = hash * 397 ^ MinimumDepth.GetHashCode();
                hash = hash * 397 ^ MinimumVolume.GetHashCode();
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Clearance: {0}, IncludeTouching: {1}, MaxDegreeOfParallelism: {2}, MinimumDepth: {3}, MinimumVolume: {4})",
                Clearance,
                IncludeTouching,
                MaxDegreeOfParallelism,
                MinimumDepth,
                MinimumVolume);
        }
    }
}
