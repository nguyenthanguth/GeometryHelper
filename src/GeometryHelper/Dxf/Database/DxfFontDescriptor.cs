using System;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The TrueType font of a text style, as AutoCAD's <c>FontDescriptor</c>: its typeface, bold, italic, character set and
    /// pitch and family, kept in the extended data of the application <c>ACAD</c> of the STYLE record.
    /// </summary>
    /// <remarks>
    /// The style of Arial, neither bold nor italic, is <c>new DxfFontDescriptor("Arial", false, false, 0, 34)</c>, written as
    /// AutoCAD writes it: <c>1001 ACAD, 1000 Arial, 1071 34</c>, the 1071 holding the pitch and family in its lowest byte, the
    /// character set in the next, italic as 0x1000000 and bold as 0x2000000. A style of a font of shapes (an SHX) has none:
    /// its typeface is empty.
    /// </remarks>
    public readonly struct DxfFontDescriptor : IEquatable<DxfFontDescriptor>
    {
        private readonly string _typeFace;

        /// <summary>
        /// Initializes a font, as AutoCAD's <c>FontDescriptor(typeFace, bold, italic, characters, pitchAndFamily)</c>.
        /// </summary>
        /// <param name="typeFace">The name of the typeface, such as <c>Arial</c>; empty for none.</param>
        /// <param name="bold">Whether it is bold.</param>
        /// <param name="italic">Whether it is italic.</param>
        /// <param name="characters">The character set, as Windows numbers it: 0 for ANSI, 1 the default.</param>
        /// <param name="pitchAndFamily">The pitch and family, as Windows numbers them: 34 for Arial (variable pitch, swiss).</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the character set or the pitch and family is not from 0 to 255.</exception>
        public DxfFontDescriptor(string typeFace, bool bold, bool italic, int characters, int pitchAndFamily)
        {
            if (characters < 0 || characters > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(characters), characters, "A character set is from 0 to 255.");
            }

            if (pitchAndFamily < 0 || pitchAndFamily > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(pitchAndFamily), pitchAndFamily, "A pitch and family is from 0 to 255.");
            }

            _typeFace = typeFace ?? string.Empty;
            Bold = bold;
            Italic = italic;
            CharacterSet = characters;
            PitchAndFamily = pitchAndFamily;
        }

        /// <summary>Gets the name of the typeface, as AutoCAD's <c>TypeFace</c>; empty for none.</summary>
        public string TypeFace => _typeFace ?? string.Empty;

        /// <summary>Gets whether the font is bold, as AutoCAD's <c>Bold</c>.</summary>
        public bool Bold { get; }

        /// <summary>Gets whether the font is italic, as AutoCAD's <c>Italic</c>.</summary>
        public bool Italic { get; }

        /// <summary>Gets the character set, as AutoCAD's <c>CharacterSet</c>.</summary>
        public int CharacterSet { get; }

        /// <summary>Gets the pitch and family, as AutoCAD's <c>PitchAndFamily</c>.</summary>
        public int PitchAndFamily { get; }

        /// <summary>Gets whether two fonts are equal.</summary>
        /// <param name="left">One.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether their typefaces, letter for letter, and the rest are.</returns>
        public static bool operator ==(DxfFontDescriptor left, DxfFontDescriptor right) => left.Equals(right);

        /// <summary>Gets whether two fonts differ.</summary>
        /// <param name="left">One.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether anything of them differs.</returns>
        public static bool operator !=(DxfFontDescriptor left, DxfFontDescriptor right) => !left.Equals(right);

        /// <summary>Gets whether the font equals another.</summary>
        /// <param name="other">The other.</param>
        /// <returns>Whether their typefaces, letter for letter, and the rest are equal.</returns>
        public bool Equals(DxfFontDescriptor other)
            => string.Equals(TypeFace, other.TypeFace, StringComparison.Ordinal) && Bold == other.Bold && Italic == other.Italic && CharacterSet == other.CharacterSet
                && PitchAndFamily == other.PitchAndFamily;

        /// <summary>Gets whether the font equals an object.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is a font equal to this one.</returns>
        public override bool Equals(object obj) => obj is DxfFontDescriptor other && Equals(other);

        /// <summary>Gets a hash code of the font, the same for two equal ones.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeFace) ^ Flags;

        /// <summary>Gives the font as a line: its typeface, bold and italic where it is.</summary>
        /// <returns>The line, such as <c>Arial bold</c>.</returns>
        public override string ToString() => TypeFace + (Bold ? " bold" : string.Empty) + (Italic ? " italic" : string.Empty);

        /// <summary>The 1071 AutoCAD writes for the font: pitch and family, character set shifted by 8, italic 0x1000000, bold 0x2000000.</summary>
        internal int Flags => PitchAndFamily | (CharacterSet << 8) | (Italic ? 0x1000000 : 0) | (Bold ? 0x2000000 : 0);

        /// <summary>The font of a typeface and the 1071 written with it.</summary>
        internal static DxfFontDescriptor Of(string typeFace, int flags)
            => new DxfFontDescriptor(typeFace, (flags & 0x2000000) != 0, (flags & 0x1000000) != 0, (flags >> 8) & 0xFF, flags & 0xFF);
    }
}
