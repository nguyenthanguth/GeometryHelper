using System;
using System.Globalization;

namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// How a volume take-off works out the material two parts share, and how many threads it may use.
    /// </summary>
    public sealed class VolumeTakeoffOptions : IEquatable<VolumeTakeoffOptions>
    {
        /// <summary>
        /// Gets the options that work out every overlap with <see cref="SolidBooleanOptions.Default"/>, on every
        /// processor.
        /// </summary>
        public static VolumeTakeoffOptions Default { get; } = new VolumeTakeoffOptions(SolidBooleanOptions.Default);

        /// <summary>
        /// Initializes the options.
        /// </summary>
        /// <param name="boolean">
        /// How each overlap is worked out and each volume measured: the tolerance, the contact and the fallback of the
        /// booleans. A Tekla model in millimetres takes a tolerance of a thousandth with a contact and a fallback of a
        /// hundredth, as <see cref="SolidBooleanOptions"/> says.
        /// </param>
        /// <param name="maxDegreeOfParallelism">How many threads may work at once; -1 for every processor.</param>
        /// <exception cref="ArgumentNullException">Thrown when the boolean options are null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the parallelism is neither -1 nor positive.</exception>
        public VolumeTakeoffOptions(SolidBooleanOptions boolean, int maxDegreeOfParallelism = -1)
        {
            if (boolean == null)
            {
                throw new ArgumentNullException(nameof(boolean));
            }

            if (maxDegreeOfParallelism == 0 || maxDegreeOfParallelism < -1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), maxDegreeOfParallelism, "Use -1 for every processor, or a positive count.");
            }

            Boolean = boolean;
            MaxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        /// <summary>
        /// Gets how each overlap is worked out and each volume measured.
        /// </summary>
        public SolidBooleanOptions Boolean { get; }

        /// <summary>
        /// Gets how many threads may work at once; -1 for every processor. The volumes come out the same, bit for bit,
        /// whatever the number.
        /// </summary>
        public int MaxDegreeOfParallelism { get; }

        /// <inheritdoc/>
        public bool Equals(VolumeTakeoffOptions other)
        {
            return other != null
                && Boolean.Equals(other.Boolean)
                && MaxDegreeOfParallelism == other.MaxDegreeOfParallelism;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is VolumeTakeoffOptions other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Boolean.GetHashCode();
                hash = hash * 397 ^ MaxDegreeOfParallelism;
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Boolean: {0}, MaxDegreeOfParallelism: {1})",
                Boolean,
                MaxDegreeOfParallelism);
        }
    }
}
