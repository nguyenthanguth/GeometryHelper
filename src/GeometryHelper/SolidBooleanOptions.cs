using System;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper
{
    /// <summary>
    /// How a boolean of two solids is worked out: within which tolerance, how far apart two faces of the two bodies may lie
    /// and still be taken as touching, and within which tolerance it is worked out again where the result is not valid.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tolerance says how near two corners have to be to be one, and a boolean is as exact as it. How near two faces
    /// of two parts have to be to touch is another question, one of the model: parts drawn against each other in Tekla
    /// Structures come a few thousandths of a millimetre into or off each other. Cut within a thousandth, a part met by
    /// a cutter whose face stood 0.0049 inside its own kept a skin of itself 0.0049 thick, 19.6 and 41.7 metres long:
    /// 110 square metres of surface on a part of 119, and 0.27 litres of volume. With <see cref="Contact"/> such faces
    /// are taken as touching, and the skin is not left.
    /// </para>
    /// <para>
    /// Of the 79 864 parts of a Tekla model, each cut one after another by the parts it meets within a thousandth,
    /// 3 344 came out with more than 1 % more surface than within a hundredth, 5 466 square metres in all; with a
    /// contact of a hundredth, 203 and 220 square metres, the volume of them all within 0.05 cubic metres of 202 236.
    /// A contact of two hundredths took away what is there as well.
    /// </para>
    /// </remarks>
    public sealed class SolidBooleanOptions : IEquatable<SolidBooleanOptions>
    {
        /// <summary>
        /// Gets the options of a boolean within the default tolerance, with no contact and nothing worked out again: the
        /// boolean as the overloads taking a <see cref="Tolerance"/> work it out.
        /// </summary>
        public static SolidBooleanOptions Default { get; } = new SolidBooleanOptions(Tolerance.Default);

        /// <summary>
        /// Initializes the options.
        /// </summary>
        /// <param name="tolerance">The tolerance the boolean is worked out within.</param>
        /// <param name="contact">
        /// How far apart a face of the second body may lie from a face of the first, parallel to it, and still be taken as
        /// lying against it; see <see cref="Contact"/>. Nought takes the bodies as they come.
        /// </param>
        /// <param name="fallback">
        /// The tolerance a boolean whose result is not valid is worked out again within; see <see cref="Fallback"/>. Null
        /// keeps the result as it comes.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the contact is negative, NaN or infinite.</exception>
        public SolidBooleanOptions(Tolerance tolerance, double contact = 0.0, Tolerance? fallback = null)
        {
            Guard.NonNegative(contact, nameof(contact), "A contact has to be a number, and cannot be negative.");
            Tolerance = tolerance;
            Contact = contact;
            Fallback = fallback;
        }

        /// <summary>
        /// Gets the tolerance the boolean is worked out within.
        /// </summary>
        public Tolerance Tolerance { get; }

        /// <summary>
        /// Gets how far apart a face of the second body may lie from a face of the first, parallel to it, and still be
        /// taken as lying against it; nought takes the bodies as they come.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Each face of the second body that lies parallel to a face of the first, either way round, within the angle of
        /// the tolerance, with every corner of it within this distance of that face's plane and the boxes of the two
        /// meeting, is put onto that plane before the boolean: its corners are moved onto the plane, the corners the
        /// second body's other faces share with it moved with them, and those faces built again from their corners (see
        /// <see cref="GeoFace3.FromLoops(System.Collections.Generic.IEnumerable{GeoPoint3}, System.Collections.Generic.IEnumerable{System.Collections.Generic.IEnumerable{GeoPoint3}}, Tolerance)"/>).
        /// The first body is not moved, and keeps the exactness of the tolerance; the second moves by no more than this
        /// distance, and only where it touches the first. Where moving it would leave it open, it is taken as it came. A
        /// face lying on such a plane within the tolerance already is not moved: the boolean takes it as lying there.
        /// </para>
        /// <para>
        /// A face of the second body standing off the first at a slant, nearer than this at one end and further at the
        /// other, is not moved, and what lies between the two is cut as it is. A hundredth suits a Tekla model in
        /// millimetres: its parts come up to a few thousandths of a millimetre into or off each other.
        /// </para>
        /// </remarks>
        public double Contact { get; }

        /// <summary>
        /// Gets the tolerance a boolean whose result is not valid is worked out again within; null keeps the result as it
        /// comes.
        /// </summary>
        /// <remarks>
        /// With a fallback, the result is checked by <see cref="GeoSolid3.Validate(Tolerance)"/> within <see cref="Tolerance"/>:
        /// a result that is open, or closed with faces wound the same way along an edge, or one that could not be worked
        /// out, is worked out again within the fallback, and that result taken where it is valid. A result closed with two
        /// faces lying one on the other the same way round holds the wrong volume: a girder of a Tekla model cut within a
        /// hundredth came out of one cut so, 3.4 litres short. Of the 79 864 parts of that model cut one after another
        /// within a thousandth, 180 met a cut whose result was not valid or could not be worked out, with a contact of a
        /// hundredth 109, and with a fallback of a hundredth as well 19, 136 cuts being taken within it.
        /// </remarks>
        public Tolerance? Fallback { get; }

        /// <inheritdoc/>
        public bool Equals(SolidBooleanOptions other)
        {
            return other != null
                && Tolerance.Equals(other.Tolerance)
                && Contact.Equals(other.Contact)
                && Fallback.Equals(other.Fallback);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is SolidBooleanOptions other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Tolerance.GetHashCode();
                hash = hash * 397 ^ Contact.GetHashCode();
                hash = hash * 397 ^ Fallback.GetHashCode();
                return hash;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "(Tolerance: {0}, Contact: {1}, Fallback: {2})",
                Tolerance,
                Contact,
                Fallback.HasValue ? Fallback.Value.ToString() : "none");
        }
    }
}
