using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>How the end of a plotted line is shaped, as the Line end style list of the Plot Style Table Editor; the values are the ones a table file keeps.</summary>
    public enum CadPlotLineEnd
    {
        /// <summary>Cut square across the line.</summary>
        Butt = 0,

        /// <summary>Cut square, half a line width beyond the end.</summary>
        Square = 1,

        /// <summary>Rounded.</summary>
        Round = 2,

        /// <summary>Pointed as a diamond; the presenter draws it round.</summary>
        Diamond = 3,

        /// <summary>The object's own, which the presenter draws butt.</summary>
        Object = 4,
    }

    /// <summary>How two plotted segments are joined, as the Line join style list of the Plot Style Table Editor; the values are the ones a table file keeps.</summary>
    public enum CadPlotLineJoin
    {
        /// <summary>Mitred: the outer edges run on to a point.</summary>
        Miter = 0,

        /// <summary>Bevelled: the point cut off flat.</summary>
        Bevel = 1,

        /// <summary>Rounded.</summary>
        Round = 2,

        /// <summary>Pointed as a diamond; the presenter draws it mitred.</summary>
        Diamond = 3,

        /// <summary>The object's own, which the presenter draws mitred.</summary>
        Object = 5,
    }

    /// <summary>How a filled area is plotted, as the Fill style list of the Plot Style Table Editor; the values are the ones a table file keeps.</summary>
    public enum CadPlotFill
    {
        /// <summary>Solid.</summary>
        Solid = 64,

        /// <summary>A checkerboard.</summary>
        Checkerboard = 65,

        /// <summary>A crosshatch.</summary>
        Crosshatch = 66,

        /// <summary>Diamonds.</summary>
        Diamonds = 67,

        /// <summary>Horizontal bars.</summary>
        HorizontalBars = 68,

        /// <summary>Slanting to the left.</summary>
        SlantLeft = 69,

        /// <summary>Slanting to the right.</summary>
        SlantRight = 70,

        /// <summary>Square dots.</summary>
        SquareDots = 71,

        /// <summary>Vertical bars.</summary>
        VerticalBars = 72,

        /// <summary>The object's own.</summary>
        Object = 73,
    }

    /// <summary>
    /// One plot style of a plot style table: what an object of one colour (a colour-dependent .ctb) or of one name (a named
    /// .stb) is plotted as in place of its own colour, lineweight, linetype and the shape of its line ends and corners, as
    /// AutoCAD 2021's Plot Style Table Editor lists its properties. A style that changes nothing is "Use object ...": its
    /// colour is the object's, its lineweight 0, its linetype <see cref="ObjectLinetype"/>, its screening 100.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A style a plot reads is applied to colour, grayscale, screening, lineweight, linetype and the line end and join
    /// (<see cref="CadPlotStyleResolver"/>). Dithering, the pen numbers, adaptive adjustment and the fill style are kept in the
    /// file and shown in the editor; a PDF or an image has no pens to dither or number, and a filled area is plotted solid.
    /// </para>
    /// <para>
    /// The colour is kept as the file keeps it, two whole numbers (<c>color</c> and <c>mode_color</c>) whose top byte says
    /// what kind of colour it is, so that a table read and written again is the same text: <see cref="Rgb"/> and
    /// <see cref="UsesObjectColor"/> are the way in.
    /// </para>
    /// </remarks>
    public sealed class CadPlotStyle
    {
        /// <summary>The linetype value that means the object's own (the editor's "Use object linetype"): 31.</summary>
        public const int ObjectLinetype = 31;

        /// <summary>
        /// The names of the 32 linetypes a style can plot in, by the value the file keeps: 0 is Solid, 1 to 30 the dashed and
        /// dotted ones the Linetype list shows, 31 "Use object linetype".
        /// </summary>
        public static IReadOnlyList<string> LinetypeNames { get; } =
            new[]
            {
                "Solid",
                "Dashed",
                "Dotted",
                "Dash Dot",
                "Short Dash",
                "Medium Dash",
                "Long Dash",
                "Short Dash X2",
                "Medium Dash X2",
                "Long Dash X2",
                "Medium Long Dash Short Dash",
                "Medium Dash Short Dash Short Dash",
                "Long Dash Short Dash",
                "Long Dash Dot Dot",
                "Long Dash Dot",
                "Medium Dash Dot Short Dash Dot",
                "Sparse Dot",
                "ISO Dash",
                "ISO Dash Space",
                "ISO Long Dash Dot",
                "ISO Long Dash Double Dot",
                "ISO Long Dash Triple Dot",
                "ISO Dot",
                "ISO Long Dash Short Dash",
                "ISO Long Dash Double Short Dash",
                "ISO Dash Dot",
                "ISO Double Dash Dot",
                "ISO Double Dash Double Dot",
                "ISO Dash Double Dot",
                "ISO Double Dash Triple Dot",
                "ISO Dash Triple Dot",
                "Use object linetype",
            };

        /// <summary>The colour value the file keeps for "Use object color": -1.</summary>
        private const int ObjectColorValue = -1;

        /// <summary>The other value that means the object's colour, as AutoCAD 2021 writes it in some tables: 0xC3FFFFFF.</summary>
        private const int ObjectColorValue2 = unchecked((int)0xC3FFFFFF);

        private string _name;
        private int _color = ObjectColorValue;
        private int _modeColor = ObjectColorValue;
        private int _screening = 100;
        private int _physicalPen;
        private int _virtualPen;
        private int _linetype = ObjectLinetype;
        private int _lineweightIndex;

        /// <summary>Makes a style that changes nothing: every property is the object's own.</summary>
        /// <param name="name">The name: Color_1 to Color_255 in a colour-dependent table, any other in a named one.</param>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a line break in it.</exception>
        public CadPlotStyle(string name)
        {
            _name = CheckName(name);
            LocalizedName = _name;
        }

        /// <summary>Gets or sets the name: Color_1 for colour 1 of a colour-dependent table, the name a drawing gives its objects in a named one.</summary>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a line break in it.</exception>
        public string Name
        {
            get => _name;
            set => _name = CheckName(value);
        }

        /// <summary>Gets or sets the name the editor shows: the same as <see cref="Name"/> unless a file says another.</summary>
        public string LocalizedName { get; set; }

        /// <summary>Gets or sets the description, one line; empty for none.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets whether the style plots in the object's own colour (the editor's "Use object color").</summary>
        public bool UsesObjectColor => _color == ObjectColorValue || _color == ObjectColorValue2;

        /// <summary>
        /// Gets or sets the colour the style plots in, 0xRRGGBB, where <see cref="UsesObjectColor"/> is false; read as black
        /// for a style of the object's colour. Setting it chooses a colour of its own, of the kind the style had.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0 to 0xFFFFFF.</exception>
        public int Rgb
        {
            get => UsesObjectColor ? 0 : _modeColor & 0xFFFFFF;
            set
            {
                if (value < 0 || value > 0xFFFFFF)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A colour is 0 to 0xFFFFFF.");
                }

                _color = unchecked((int)(((uint)ColorKind << 24) | (uint)value));
                _modeColor = _color;
            }
        }

        /// <summary>
        /// Gets or sets the kind byte of a colour of its own, as the file keeps it in the top byte of <c>mode_color</c>: 0xC2
        /// (an RGB colour, a colour-dependent table's), 0xC3 (a colour of AutoCAD's index, a named table's). A style made in
        /// the editor of a table takes the kind of the table.
        /// </summary>
        public int ColorKind { get; set; } = 0xC2;

        /// <summary>Sets the style to plot in the object's own colour.</summary>
        public void UseObjectColor()
        {
            _color = ObjectColorValue;
            _modeColor = ObjectColorValue;
        }

        /// <summary>Gets or sets the colour value of the file, <c>color</c>: -1 for the object's colour.</summary>
        internal int RawColor
        {
            get => _color;
            set => _color = value;
        }

        /// <summary>Gets or sets the colour value of the file, <c>mode_color</c>; kept as read, and not written where the colour is the object's.</summary>
        internal int RawModeColor
        {
            get => _modeColor;
            set => _modeColor = value;
        }

        /// <summary>Gets or sets the colour policy of the file, <c>color_policy</c>: bit 1 dither, bit 2 grayscale, bit 4 a named style's colour; 1 for a style made new.</summary>
        internal int Policy { get; set; } = 1;

        /// <summary>Gets or sets whether the colours are dithered on a plotter that can (the editor's Enable dithering); on by default. A PDF or an image has no dots to dither with, so a plot is the same either way.</summary>
        public bool Dither
        {
            get => (Policy & 1) != 0;
            set => Policy = value ? Policy | 1 : Policy & ~1;
        }

        /// <summary>Gets or sets whether the colour is plotted as the grey of its brightness (the editor's Convert to grayscale); off by default.</summary>
        public bool Grayscale
        {
            get => (Policy & 2) != 0;
            set => Policy = value ? Policy | 2 : Policy & ~2;
        }

        /// <summary>Gets or sets the number of the physical pen, 1 to 32; 0 for automatic. A PDF or an image has no pens, so the number is kept and not used.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0 to 32.</exception>
        public int PhysicalPen
        {
            get => _physicalPen;
            set => _physicalPen = Whole(value, 0, 32);
        }

        /// <summary>Gets or sets the number of the virtual pen, 1 to 255; 0 for automatic. A PDF or an image has no pens, so the number is kept and not used.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0 to 255.</exception>
        public int VirtualPen
        {
            get => _virtualPen;
            set => _virtualPen = Whole(value, 0, 255);
        }

        /// <summary>
        /// Gets or sets the screening, how much of the colour is plotted, 0 to 100 per cent; 100 by default. A colour at 50 is
        /// plotted half way to white on the white sheet: pure red #FF0000 at 50 is #FF8080. A table of AutoCAD's may keep 255,
        /// its "no screening", which is read as 100 where the plot is made (<see cref="EffectiveScreening"/>).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0 to 255.</exception>
        public int Screening
        {
            get => _screening;
            set => _screening = Whole(value, 0, 255);
        }

        /// <summary>Gets the screening a plot applies: <see cref="Screening"/>, 100 where it is above 100.</summary>
        public int EffectiveScreening => Math.Min(_screening, 100);

        /// <summary>
        /// Gets or sets the length the pattern of <see cref="Linetype"/> is built from, in inches as the file keeps it; 0.5 by
        /// default. One unit of the presenter's patterns is a quarter of it, 3.175 mm at the default.
        /// </summary>
        public double LinePatternSize { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets the linetype the style plots in, 0 to 30 as <see cref="LinetypeNames"/> lists them, or
        /// <see cref="ObjectLinetype"/> (31) for the object's own; 31 by default.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not 0 to 31.</exception>
        public int Linetype
        {
            get => _linetype;
            set => _linetype = Whole(value, 0, ObjectLinetype);
        }

        /// <summary>Gets or sets whether the pattern of the linetype is adjusted to finish whole at the end of a line (Adaptive adjustment); on by default. Kept and not used by the presenter, which plots a pattern as it is.</summary>
        public bool Adaptive { get; set; } = true;

        /// <summary>
        /// Gets or sets which entry of the table's lineweight list the style plots at, as the file keeps it: 0 for "Use object
        /// lineweight" (the table's entry 0, which is 0). <see cref="CadPlotStyleSheet.LineWeightOf"/> gives its millimetres.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is below nought.</exception>
        public int LineWeightIndex
        {
            get => _lineweightIndex;
            set => _lineweightIndex = Whole(value, 0, int.MaxValue);
        }

        /// <summary>Gets or sets the shape of a line's end; the object's own by default.</summary>
        public CadPlotLineEnd LineEnd { get; set; } = CadPlotLineEnd.Object;

        /// <summary>Gets or sets the shape of a corner of a line; the object's own by default.</summary>
        public CadPlotLineJoin LineJoin { get; set; } = CadPlotLineJoin.Object;

        /// <summary>Gets or sets how a filled area is plotted; the object's own by default. Kept and shown, a filled area is plotted solid.</summary>
        public CadPlotFill Fill { get; set; } = CadPlotFill.Object;

        /// <summary>Makes a copy that changes apart from this one.</summary>
        /// <returns>The copy.</returns>
        public CadPlotStyle Clone() => (CadPlotStyle)MemberwiseClone();

        /// <inheritdoc/>
        public override string ToString() => _name;

        private static string CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A plot style needs a name.", nameof(name));
            }

            string trimmed = name.Trim();

            if (trimmed.IndexOfAny(new[] { '\r', '\n', '\0' }) >= 0)
            {
                throw new ArgumentException("A plot style's name is one line.", nameof(name));
            }

            return trimmed;
        }

        private static int Whole(int value, int min, int max)
        {
            if (value < min || value > max)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The value has to be from " + min + " to " + max + "."
                );
            }

            return value;
        }
    }
}
