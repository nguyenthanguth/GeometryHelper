using System;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The variables of the header AutoCAD's <c>Database</c> has as properties, each read from its variable of the HEADER
    /// section and set in it, every other byte of the header kept; where the header gives none, the value a new drawing of
    /// ezdxf 1.4.5 is given. Set to the value it has, a variable changes nothing; one the header has not is added at its end.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>Gets or sets the linetype new entities are drawn with (<c>$CELTYPE</c>), by name: <c>ByLayer</c> where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the linetype set is not in the linetype table, BYLAYER and BYBLOCK aside.</exception>
        public string Celtype
        {
            get => Name("$CELTYPE", 6, "ByLayer");

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Celtype, StringComparison.Ordinal))
                {
                    return;
                }

                if (!File.LinetypeListed(value))
                {
                    throw new ArgumentException("The linetype " + value + " is not in the file's linetype table.", nameof(value));
                }

                Set("$CELTYPE", 6, value);
            }
        }

        /// <summary>
        /// Gets or sets the colour new entities are drawn in (<c>$CECOLOR</c>), as AutoCAD's <c>Cecolor</c>: BYLAYER where the
        /// header gives none.
        /// </summary>
        /// <value>Set, BYLAYER, BYBLOCK or a colour of the index: the header holds an index alone.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour or another the header does not hold.</exception>
        public DxfColor Cecolor
        {
            get => DxfColor.OfIndex(Whole("$CECOLOR", 62, 256));

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.ColorMethod != DxfColorMethod.ByLayer && value.ColorMethod != DxfColorMethod.ByBlock && value.ColorMethod != DxfColorMethod.ByAci)
                {
                    throw new ArgumentException("The header holds the colour of new entities as an index: by layer, by block or 1 to 255.", nameof(value));
                }

                if (!value.Equals(Cecolor))
                {
                    Set("$CECOLOR", 62, value.ColorIndex.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>Gets or sets the lineweight new entities are drawn with (<c>$CELWEIGHT</c>): <see cref="DxfLineWeight.ByLayer"/> where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one DXF writes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no lineweights.</exception>
        public DxfLineWeight Celweight
        {
            get => (DxfLineWeight)Whole("$CELWEIGHT", 370, -1);

            set
            {
                if (value == Celweight)
                {
                    return;
                }

                if (!((int)value >= -3 && (int)value <= -1) && !DxfShape.IsLineweight((int)value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A lineweight is by layer, by block, the default or one of AutoCAD's.");
                }

                File.Requires(1015, "$CELWEIGHT", "R2000 (AC1015)");
                Set("$CELWEIGHT", 370, ((int)value).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Gets or sets whether lineweights are drawn on the screen (<c>$LWDISPLAY</c> of 1), as AutoCAD's
        /// <c>LineWeightDisplay</c>; false where the header gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no lineweights.</exception>
        public bool LineWeightDisplay
        {
            get => Whole("$LWDISPLAY", 290, 0) != 0;

            set
            {
                if (value != LineWeightDisplay)
                {
                    File.Requires(1015, "$LWDISPLAY", "R2000 (AC1015)");
                    Set("$LWDISPLAY", 290, value ? "1" : "0");
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the pictures of the objects of other applications are saved with them (<c>$PROXYGRAPHICS</c>),
        /// as AutoCAD's <c>Saveproxygraphics</c>: 1 they are, 0 they are not; 1 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 or 1.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file older than R2000 (AC1015), which has no such variable.</exception>
        public int Saveproxygraphics
        {
            get => Whole("$PROXYGRAPHICS", 70, 1);

            set
            {
                if (value == Saveproxygraphics)
                {
                    return;
                }

                InRange(value, 0, 1, nameof(value));
                File.Requires(1015, "$PROXYGRAPHICS", "R2000 (AC1015)");
                Set("$PROXYGRAPHICS", 70, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets or sets the scale of every linetype of the drawing (<c>$LTSCALE</c>): 1 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        public double Ltscale
        {
            get => Real("$LTSCALE", 40, 1.0);

            set
            {
                if (!Same(value, Ltscale))
                {
                    Guard.Positive(value, nameof(value), "A linetype scale has to be a finite number above nought.");
                    SetReal("$LTSCALE", 40, value, Ltscale);
                }
            }
        }

        /// <summary>Gets or sets the least corner of the drawing's limits in model space (<c>$LIMMIN</c>): (0, 0) where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint2 Limmin
        {
            get => Point2("$LIMMIN", new GeoPoint2(0.0, 0.0));
            set => SetPoint2("$LIMMIN", value, Limmin);
        }

        /// <summary>Gets or sets the greatest corner of the drawing's limits in model space (<c>$LIMMAX</c>): (420, 297) where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint2 Limmax
        {
            get => Point2("$LIMMAX", new GeoPoint2(420.0, 297.0));
            set => SetPoint2("$LIMMAX", value, Limmax);
        }

        /// <summary>Gets or sets the point the drawing is inserted at when it is inserted into another (<c>$INSBASE</c>): the origin where the header gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        public GeoPoint3 Insbase
        {
            get => Point("$INSBASE");
            set => SetPoint("$INSBASE", value);
        }

        /// <summary>Gets or sets how points are drawn (<c>$PDMODE</c>): 0, a dot, where the header gives none.</summary>
        public int Pdmode
        {
            get => Whole("$PDMODE", 70, 0);
            set => SetWhole("$PDMODE", 70, value, Pdmode);
        }

        /// <summary>
        /// Gets or sets how many segments each piece of a polyline fitted with a spline is drawn with (<c>$SPLINESEGS</c>), as
        /// AutoCAD's <c>Splinesegs</c>: 8 where the header gives none; below nought, arcs rather than lines.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is nought or outside -32768 to 32767.</exception>
        public int Splinesegs
        {
            get => Whole("$SPLINESEGS", 70, 8);

            set
            {
                // The number it has changes nothing, whatever the header holds.
                if (value != Splinesegs && (value == 0 || value < short.MinValue || value > short.MaxValue))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "SPLINESEGS is from -32768 to 32767, not nought.");
                }

                SetWhole("$SPLINESEGS", 70, value, Splinesegs);
            }
        }

        /// <summary>
        /// Gets or sets the spline a polyline is fitted with (<c>$SPLINETYPE</c>), as AutoCAD's <c>Splinetype</c>: 5 quadratic,
        /// 6 cubic; 6 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 5 or 6.</exception>
        public int Splinetype
        {
            get => Whole("$SPLINETYPE", 70, 6);
            set => SetWhole("$SPLINETYPE", 70, value == Splinetype ? value : InRange(value, 5, 6, nameof(value)), Splinetype);
        }

        /// <summary>
        /// Gets or sets the surface a polygon mesh is fitted with (<c>$SURFTYPE</c>), as AutoCAD's <c>Surftype</c>: 5 quadratic,
        /// 6 cubic, 8 Bezier; 6 where the header gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 5, 6 or 8.</exception>
        public int Surftype
        {
            get => Whole("$SURFTYPE", 70, 6);

            set
            {
                if (value != Surftype && value != 5 && value != 6 && value != 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "SURFTYPE is 5, 6 or 8.");
                }

                SetWhole("$SURFTYPE", 70, value, Surftype);
            }
        }

        /// <summary>Gets or sets how many lines a fitted polygon mesh is drawn with along M (<c>$SURFU</c>), as AutoCAD's <c>Surfu</c>: 0 to 200; 6 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 200.</exception>
        public int Surfu
        {
            get => Whole("$SURFU", 70, 6);
            set => SetWhole("$SURFU", 70, value == Surfu ? value : InRange(value, 0, 200, nameof(value)), Surfu);
        }

        /// <summary>Gets or sets how many lines a fitted polygon mesh is drawn with along N (<c>$SURFV</c>), as AutoCAD's <c>Surfv</c>: 0 to 200; 6 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 0 to 200.</exception>
        public int Surfv
        {
            get => Whole("$SURFV", 70, 6);
            set => SetWhole("$SURFV", 70, value == Surfv ? value : InRange(value, 0, 200, nameof(value)), Surfv);
        }

        /// <summary>Gets or sets how large points are drawn (<c>$PDSIZE</c>): nought where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the size set is not a finite number.</exception>
        public double Pdsize
        {
            get => Real("$PDSIZE", 40, 0.0);

            set
            {
                Guard.Finite(value, nameof(value), "A size has to be a finite number.");
                SetReal("$PDSIZE", 40, value, Pdsize);
            }
        }

        /// <summary>Gets or sets the height new texts are given (<c>$TEXTSIZE</c>): 2.5 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        public double Textsize
        {
            get => Real("$TEXTSIZE", 40, 2.5);

            set
            {
                if (!Same(value, Textsize))
                {
                    Guard.Positive(value, nameof(value), "A text height has to be a finite number above nought.");
                    SetReal("$TEXTSIZE", 40, value, Textsize);
                }
            }
        }

        /// <summary>Gets or sets the style new texts are written in (<c>$TEXTSTYLE</c>), by name: <c>Standard</c> where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is not in the text style table.</exception>
        public string Textstyle
        {
            get => Name("$TEXTSTYLE", 7, "Standard");

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Textstyle, StringComparison.Ordinal))
                {
                    return;
                }

                if (!TextStyleTable.Has(value))
                {
                    throw new ArgumentException("The text style " + value + " is not in the file's text style table.", nameof(value));
                }

                Set("$TEXTSTYLE", 7, value);
            }
        }

        /// <summary>Gets or sets the style new dimensions are drawn in (<c>$DIMSTYLE</c>), by name: <c>ISO-25</c> where the header gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set is one AutoCAD does not take.</exception>
        public string Dimstyle
        {
            get => Name("$DIMSTYLE", 2, "ISO-25");

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, Dimstyle, StringComparison.Ordinal))
                {
                    DxfSymbolTable.CheckName(value, nameof(value));
                    Set("$DIMSTYLE", 2, value);
                }
            }
        }

        /// <summary>Gets or sets how lengths are shown (<c>$LUNITS</c>): 1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional; 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 1 to 5.</exception>
        public int Lunits
        {
            get => Whole("$LUNITS", 70, 2);
            set => SetWhole("$LUNITS", 70, value == Lunits ? value : InRange(value, 1, 5, nameof(value)), Lunits);
        }

        /// <summary>Gets or sets how many places lengths are shown to (<c>$LUPREC</c>): 4 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 to 8.</exception>
        public int Luprec
        {
            get => Whole("$LUPREC", 70, 4);
            set => SetWhole("$LUPREC", 70, value == Luprec ? value : InRange(value, 0, 8, nameof(value)), Luprec);
        }

        /// <summary>Gets or sets how angles are shown (<c>$AUNITS</c>): 0 degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians, 4 surveyor's; 0 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 to 4.</exception>
        public int Aunits
        {
            get => Whole("$AUNITS", 70, 0);
            set => SetWhole("$AUNITS", 70, value == Aunits ? value : InRange(value, 0, 4, nameof(value)), Aunits);
        }

        /// <summary>Gets or sets how many places angles are shown to (<c>$AUPREC</c>): 2 where the header gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not 0 to 8.</exception>
        public int Auprec
        {
            get => Whole("$AUPREC", 70, 2);
            set => SetWhole("$AUPREC", 70, value == Auprec ? value : InRange(value, 0, 8, nameof(value)), Auprec);
        }

        /// <summary>
        /// Gets or sets the direction angles are measured from (<c>$ANGBASE</c>), in radians as AutoCAD's <c>Angbase</c>: the
        /// header writes degrees, 90 being pi / 2; nought where it gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        public double Angbase
        {
            get => Real("$ANGBASE", 50, 0.0) * Math.PI / 180.0;

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (!Same(value, Angbase))
                {
                    Set("$ANGBASE", 50, Numbers.Exact(value * 180.0 / Math.PI + 0.0));
                }
            }
        }

        /// <summary>Gets or sets whether angles grow clockwise (<c>$ANGDIR</c> of 1); counter-clockwise where the header gives none.</summary>
        public bool Angdir
        {
            get => Whole("$ANGDIR", 70, 0) == 1;
            set => SetFlag("$ANGDIR", value, Angdir);
        }

        /// <summary>Gets or sets whether drawing is held to the axes (<c>$ORTHOMODE</c> of 1); false where the header gives none.</summary>
        public bool Orthomode
        {
            get => Whole("$ORTHOMODE", 70, 0) != 0;
            set => SetFlag("$ORTHOMODE", value, Orthomode);
        }

        /// <summary>Gets or sets whether solids, traces and wide polylines are filled (<c>$FILLMODE</c> of 1); true where the header gives none.</summary>
        public bool Fillmode
        {
            get => Whole("$FILLMODE", 70, 1) != 0;
            set => SetFlag("$FILLMODE", value, Fillmode);
        }

        /// <summary>Gets or sets whether texts are mirrored with what they are in (<c>$MIRRTEXT</c> of 1); true where the header gives none.</summary>
        public bool Mirrtext
        {
            get => Whole("$MIRRTEXT", 70, 1) != 0;
            set => SetFlag("$MIRRTEXT", value, Mirrtext);
        }

        private string Name(string name, int code, string fallback)
        {
            string value = DxfReader.NameOf(First(name, code) ?? string.Empty);
            return value.Length == 0 ? fallback : value;
        }

        private double Real(string name, int code, double fallback)
            => Numbers.TryRead(First(name, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        private int Whole(string name, int code, int fallback)
            => int.TryParse(First(name, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private GeoPoint2 Point2(string name, GeoPoint2 fallback)
        {
            bool x = Numbers.TryRead(First(name, 10), out double px) && Guard.IsFinite(px);
            bool y = Numbers.TryRead(First(name, 20), out double py) && Guard.IsFinite(py);
            return x || y ? new GeoPoint2(x ? px : 0.0, y ? py : 0.0) : fallback;
        }

        private void Set(string name, int code, string value) => File.SetHeader(name, new[] { new DxfTypedValue(code, value) });

        private void SetReal(string name, int code, double value, double now)
        {
            if (!Same(value, now))
            {
                Set(name, code, Numbers.Exact(value + 0.0));
            }
        }

        private void SetWhole(string name, int code, int value, int now)
        {
            if (value != now)
            {
                Set(name, code, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void SetFlag(string name, bool value, bool now)
        {
            if (value != now)
            {
                Set(name, 70, value ? "1" : "0");
            }
        }

        private void SetPoint2(string name, GeoPoint2 value, GeoPoint2 now)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }

            if (!Same(value.X, now.X) || !Same(value.Y, now.Y))
            {
                File.SetHeader(name, new[] { new DxfTypedValue(10, Numbers.Exact(value.X + 0.0)), new DxfTypedValue(20, Numbers.Exact(value.Y + 0.0)) });
            }
        }

        private static int InRange(int value, int least, int most, string parameter)
        {
            if (value < least || value > most)
            {
                throw new ArgumentOutOfRangeException(parameter, value, "The number runs from " + least.ToString(CultureInfo.InvariantCulture) + " to " + most.ToString(CultureInfo.InvariantCulture) + ".");
            }

            return value;
        }

        private static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
