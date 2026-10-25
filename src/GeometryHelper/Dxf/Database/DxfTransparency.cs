using System;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// How much an entity or a layer lets show through it, as AutoCAD's <c>Autodesk.AutoCAD.Colors.Transparency</c>: by layer, by
    /// block, or an alpha from 0, clear, to 255, solid. An entity writes it as its group 440, a layer in its extended data of
    /// the application <c>AcCmTransparency</c>, both as AutoCAD writes them, from R2004 (AC1018) on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD's 50 per cent is the alpha 127, <c>new DxfTransparency(127)</c>, written as 33 554 559 (0x0200007F); BYBLOCK
    /// is written as 16 777 216 (0x01000000), and BYLAYER as no group at all. A transparency of nought is the default value of
    /// the structure, BYLAYER.
    /// </para>
    /// <para>
    /// Two transparencies are equal where their methods are and, by alpha, their alphas are.
    /// </para>
    /// </remarks>
    public readonly struct DxfTransparency : IEquatable<DxfTransparency>
    {
        private readonly byte _alpha;
        private readonly DxfTransparencyMethod _method;

        /// <summary>
        /// Initializes a transparency of its own, by alpha, as AutoCAD's <c>Transparency(byte)</c>: 0 clear, 255 solid, 127 the 50
        /// per cent AutoCAD's dialog shows.
        /// </summary>
        /// <param name="alpha">The alpha, 0 to 255.</param>
        public DxfTransparency(byte alpha)
        {
            _method = DxfTransparencyMethod.ByAlpha;
            _alpha = alpha;
        }

        /// <summary>
        /// Initializes a transparency by its method, as AutoCAD's <c>Transparency(TransparencyMethod)</c>: BYLAYER or BYBLOCK,
        /// whose alpha is nought; by alpha, solid, 255.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the method is not one of <see cref="DxfTransparencyMethod"/>.</exception>
        public DxfTransparency(DxfTransparencyMethod method)
        {
            if (method < DxfTransparencyMethod.ByLayer || method > DxfTransparencyMethod.ErrorValue)
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, "A method of transparency is one of DxfTransparencyMethod.");
            }

            _method = method;
            _alpha = method == DxfTransparencyMethod.ByAlpha ? (byte)255 : (byte)0;
        }

        /// <summary>Gets the transparency BYLAYER.</summary>
        public static DxfTransparency ByLayer => new DxfTransparency(DxfTransparencyMethod.ByLayer);

        /// <summary>Gets the transparency BYBLOCK.</summary>
        public static DxfTransparency ByBlock => new DxfTransparency(DxfTransparencyMethod.ByBlock);

        /// <summary>Gets the alpha, as AutoCAD's <c>Alpha</c>: 0 clear to 255 solid by alpha; nought by layer or by block.</summary>
        public byte Alpha => _alpha;

        /// <summary>Gets how the transparency is given, as AutoCAD's <c>Method</c> does.</summary>
        public DxfTransparencyMethod Method => _method;

        /// <summary>Gets whether the transparency is by alpha, as AutoCAD's <c>IsByAlpha</c>.</summary>
        public bool IsByAlpha => _method == DxfTransparencyMethod.ByAlpha;

        /// <summary>Gets whether the transparency is BYBLOCK, as AutoCAD's <c>IsByBlock</c>.</summary>
        public bool IsByBlock => _method == DxfTransparencyMethod.ByBlock;

        /// <summary>Gets whether the transparency is BYLAYER, as AutoCAD's <c>IsByLayer</c>.</summary>
        public bool IsByLayer => _method == DxfTransparencyMethod.ByLayer;

        /// <summary>Gets whether the transparency is by alpha and wholly clear, the alpha nought, as AutoCAD's <c>IsClear</c>.</summary>
        public bool IsClear => IsByAlpha && _alpha == 0;

        /// <summary>Gets whether the transparency is by alpha and solid, the alpha 255, as AutoCAD's <c>IsSolid</c>.</summary>
        public bool IsSolid => IsByAlpha && _alpha == 255;

        /// <summary>Gets whether the transparency is none AutoCAD writes, as AutoCAD's <c>IsInvalid</c>: one read of a value that is no method.</summary>
        public bool IsInvalid => _method == DxfTransparencyMethod.ErrorValue;

        /// <summary>Gets whether two transparencies are equal.</summary>
        /// <param name="left">One.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether their methods and, by alpha, their alphas are.</returns>
        public static bool operator ==(DxfTransparency left, DxfTransparency right) => left.Equals(right);

        /// <summary>Gets whether two transparencies differ.</summary>
        /// <param name="left">One.</param>
        /// <param name="right">The other.</param>
        /// <returns>Whether their methods or, by alpha, their alphas differ.</returns>
        public static bool operator !=(DxfTransparency left, DxfTransparency right) => !left.Equals(right);

        /// <summary>Gets whether the transparency equals another.</summary>
        /// <param name="other">The other.</param>
        /// <returns>Whether their methods and, by alpha, their alphas are equal.</returns>
        public bool Equals(DxfTransparency other) => _method == other._method && (_method != DxfTransparencyMethod.ByAlpha || _alpha == other._alpha);

        /// <summary>Gets whether the transparency equals an object.</summary>
        /// <param name="obj">The object.</param>
        /// <returns>Whether it is a transparency equal to this one.</returns>
        public override bool Equals(object obj) => obj is DxfTransparency other && Equals(other);

        /// <summary>Gets a hash code of the transparency, the same for two equal ones.</summary>
        /// <returns>The hash code.</returns>
        public override int GetHashCode() => ((int)_method << 8) | (IsByAlpha ? _alpha : 0);

        /// <summary>
        /// Gives the transparency as a line: <c>ByLayer</c>, <c>ByBlock</c>, <c>ByAlpha 127</c> or <c>ErrorValue</c>.
        /// </summary>
        /// <returns>The line.</returns>
        public override string ToString() => IsByAlpha ? "ByAlpha " + _alpha.ToString(CultureInfo.InvariantCulture) : _method.ToString();

        /// <summary>
        /// The transparency of the value a file writes (an entity's 440, a layer's 1071 of <c>AcCmTransparency</c>): its top byte
        /// the method, 2 by alpha, 1 BYBLOCK, 0 BYLAYER, and its low byte the alpha; any other method an error value.
        /// </summary>
        internal static DxfTransparency OfValue(int value)
        {
            switch ((value >> 24) & 0xFF)
            {
                case 0: return ByLayer;
                case 1: return ByBlock;
                case 2: return new DxfTransparency((byte)(value & 0xFF));
                default: return new DxfTransparency(DxfTransparencyMethod.ErrorValue);
            }
        }

        /// <summary>The value a file writes for the transparency: 0x020000TT by alpha, 0x01000000 BYBLOCK, nought BYLAYER.</summary>
        internal int Value => IsByAlpha ? 0x02000000 | _alpha : IsByBlock ? 0x01000000 : 0;
    }
}
