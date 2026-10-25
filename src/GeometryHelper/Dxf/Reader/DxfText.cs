using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A text of a DXF file, a TEXT of one line or an MTEXT of paragraphs, as <see cref="DxfReader"/> reads it into
    /// <see cref="DxfShape.Shape"/>: where it stands, which way it faces and reads, how tall it is, and its words.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A text is the same in the plan and in space, so <see cref="DxfReadOptions.PlanAs2D"/> reads it as this class
    /// either way: it has no twin of the plane. A TEXT 2.5 high reading "Grid A" at (1 000, 500) on the floor is at
    /// (1 000, 500, 0), its normal (0, 0, 1) and its rotation nought.
    /// </para>
    /// <para>
    /// It cannot be changed once made, and two texts are the same only where they are one object: it does not compare
    /// its numbers.
    /// </para>
    /// </remarks>
    public sealed class DxfText
    {
        /// <summary>
        /// Initializes a text of one's own, to be written by <see cref="DxfWriter.Add(DxfText, string)"/>.
        /// </summary>
        /// <param name="position">Where it stands: the start of its baseline for a TEXT, the insertion point for an MTEXT.</param>
        /// <param name="normal">The way it faces; made unit, and kept as given where it is within 1E-12 of unit already.</param>
        /// <param name="height">How tall its letters are; above nought.</param>
        /// <param name="rotation">How far it is turned, in radians counter-clockwise about the normal from the x axis of the
        /// normal's own system, as AutoCAD works it out; for the normal (0, 0, 1), from the x axis. It is kept from nought up to
        /// a whole turn: -pi / 2 is 3 pi / 2.</param>
        /// <param name="value">Its words; a TEXT's on one line, an MTEXT's with a line feed between paragraphs.</param>
        /// <param name="isMultiline">true for an MTEXT; false for a TEXT.</param>
        /// <param name="widthFactor">How much wider than its height says its letters are drawn, above nought; 1 for an MTEXT,
        /// which has none.</param>
        /// <param name="width">How wide an MTEXT's paragraphs run before they wrap, nought for no limit; nought for a TEXT,
        /// which does not wrap.</param>
        /// <exception cref="ArgumentNullException">Thrown when the value is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the position or the normal is not finite, the normal has no length,
        /// or the value holds a carriage return, or a line feed in a TEXT.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height or width factor is not a finite number above
        /// nought, the rotation not finite, the width not a finite number of nought or more, an MTEXT given a width factor
        /// other than 1, or a TEXT a width other than nought.</exception>
        public DxfText(GeoPoint3 position, GeoVector3 normal, double height, double rotation, string value, bool isMultiline = false, double widthFactor = 1.0, double width = 0.0)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!Guard.IsFinite(position.X) || !Guard.IsFinite(position.Y) || !Guard.IsFinite(position.Z))
            {
                throw new ArgumentException("The position of a text has to be finite.", nameof(position));
            }

            double length = normal.Length;

            if (!(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException("The normal of a text has to be a finite vector of some length.", nameof(normal));
            }

            Guard.Positive(height, nameof(height), "The height of a text has to be a finite number above nought.");
            Guard.Finite(rotation, nameof(rotation), "The rotation of a text has to be a finite number.");
            Guard.Positive(widthFactor, nameof(widthFactor), "The width factor of a text has to be a finite number above nought.");
            Guard.NonNegative(width, nameof(width), "The width of a text has to be a finite number of nought or more.");

            if (isMultiline && widthFactor != 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(widthFactor), widthFactor, "An MTEXT has no width factor: it is 1.");
            }

            if (!isMultiline && width != 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), width, "A TEXT does not wrap: its width is nought.");
            }

            if (value.IndexOf('\r') >= 0 || (!isMultiline && value.IndexOf('\n') >= 0))
            {
                throw new ArgumentException(isMultiline ? "The value of an MTEXT has a line feed between paragraphs, and no carriage return." : "A TEXT is one line: its value has no line feed or carriage return.", nameof(value));
            }

            Position = position;
            Normal = Math.Abs(length - 1.0) <= 1E-12 ? normal : new GeoVector3(normal.X / length, normal.Y / length, normal.Z / length);
            Height = height;
            Rotation = Turn(rotation);
            Value = value;
            IsMultiline = isMultiline;
            WidthFactor = widthFactor;
            Width = width;
        }

        // A text as the reader read it, its numbers checked there; the value first, to tell it from the public one.
        private DxfText(string value, GeoPoint3 position, GeoVector3 normal, double height, double rotation, bool isMultiline, double widthFactor, double width)
        {
            Position = position;
            Normal = normal;
            Height = height;
            Rotation = rotation;
            Value = value;
            IsMultiline = isMultiline;
            WidthFactor = widthFactor;
            Width = width;
        }

        /// <summary>
        /// An angle in radians brought into a turn, from nought up to but not including 2 pi; one in it already is kept to the
        /// bit, and one that rounds to a whole turn is nought.
        /// </summary>
        internal static double Turn(double angle)
        {
            const double Whole = Math.PI * 2.0;

            if (angle >= 0.0 && angle < Whole)
            {
                return angle + 0.0;
            }

            double turned = angle % Whole;
            turned = turned < 0.0 ? turned + Whole : turned;
            return turned < Whole ? turned + 0.0 : 0.0;
        }

        /// <summary>A text as the reader read it, its numbers checked there.</summary>
        internal static DxfText Read(GeoPoint3 position, GeoVector3 normal, double height, double rotation, string value, bool isMultiline, double widthFactor, double width)
            => new DxfText(value, position, normal, height, rotation, isMultiline, widthFactor, width);

        /// <summary>
        /// Gets where the text stands, in world coordinates: the alignment point of a TEXT, the first (group 10) where it is
        /// aligned left on its baseline and where it is aligned or fitted between two points, which start at the first, and
        /// the second (11) where it is aligned any other way, centred, right or in the middle, as AutoCAD places it; the
        /// insertion point of an MTEXT (10), its attachment saying which corner or side of its paragraphs that is.
        /// </summary>
        public GeoPoint3 Position { get; }

        /// <summary>
        /// Gets the way the text faces, of unit length: its extrusion (group 210), (0, 0, 1) where the file gives none. A text
        /// whose normal is (0, 0, -1) reads mirrored seen from above, as AutoCAD writes the text of a mirrored block.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets how tall the letters are (group 40), above nought.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Gets how far the text is turned, in radians counter-clockwise about <see cref="Normal"/> from the x axis of the
        /// normal's own system by the arbitrary axis algorithm: a TEXT's group 50 in degrees made radians, an MTEXT's from its
        /// direction (group 11) where it has one and from its group 50 in degrees otherwise, as AutoCAD writes it (the DXF
        /// reference says radians). It is from nought up to but not including 2 pi, whatever the file gives: a TEXT turned -90
        /// degrees is turned 3 pi / 2. For the normal (0, 0, 1) it is the angle from the x axis: a text reading up the page is
        /// turned pi / 2.
        /// </summary>
        public double Rotation { get; }

        /// <summary>
        /// Gets the words: a TEXT's as written, an MTEXT's with its formatting taken out and its paragraphs (\P) a line feed
        /// apart. In both, %%c, %%d and %%p (either case) are the diameter, degree and plus-minus signs, %%% a per cent sign
        /// and %%nnn the character of the code nnn; a letter written as an escape, <c>\U+00E9</c>, is the letter, and so is
        /// the text <c>\U+0041</c> written in the words, which reads as A; a control character in AutoCAD's caret form, ^J,
        /// ^M or ^I, is the character, and <c>^ </c> a caret. A TEXT is one line, so a line feed in its words is a space; a
        /// carriage return is left out of either.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets whether the text is an MTEXT, of paragraphs; false for a TEXT, of one line.
        /// </summary>
        public bool IsMultiline { get; }

        /// <summary>
        /// Gets how much wider than its height says its letters are drawn: a TEXT's group 41, 1 where it gives none; 1 for an
        /// MTEXT, which sets it in its formatting.
        /// </summary>
        public double WidthFactor { get; }

        /// <summary>
        /// Gets how wide an MTEXT's paragraphs run before they wrap (group 41), nought where it gives none; nought for a TEXT.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Gives the text as a line for debugging: its kind, its words, where it stands and its height.
        /// </summary>
        /// <returns>The text as a line, such as <c>TEXT "Grid A" at (1000, 500, 0), 2.5 high</c>.</returns>
        public override string ToString() => $"{(IsMultiline ? "MTEXT" : "TEXT")} \"{Value}\" at {Position}, {Height:0.###} high";
    }
}
