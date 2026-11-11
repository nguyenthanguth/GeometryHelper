using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A TOLERANCE of a DXF file, as AutoCAD's <c>FeatureControlFrame</c>: a feature control frame of geometric dimensioning
    /// and tolerancing, its words in group 1 as AutoCAD's TOLERANCE command writes them, put at its insertion point along its
    /// direction in the plane of its normal and drawn by its dimension style.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The words (<see cref="Text"/>) hold one row of the frame to a line, a line feed between rows (<c>^J</c> in the file),
    /// and its cells in the row with <c>%%v</c> between them: the symbol, the two tolerances, the three datums. A symbol is a
    /// letter of AutoCAD's gdt font, <c>{\Fgdt;j}</c> the position, <c>{\Fgdt;n}</c> the diameter sign and <c>{\Fgdt;m}</c>
    /// the maximum material condition, so that the position of a diameter 0.1 at the maximum material condition to the datums
    /// A, B and C is <c>{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA%%vB%%vC</c>. The height of a projected zone and the datum
    /// identifier are rows of their own: <c>5{\Fgdt;p}</c> and <c>D</c>.
    /// </para>
    /// <para>
    /// The record holds the dimension style (group 3), the insertion point (10, 20, 30), the words (1), the normal (210, 220,
    /// 230) and the direction (11, 21, 31), the last two left out where they are the world's z and x, as AutoCAD 2021 writes
    /// them. Overrides of the dimension style are the list DSTYLE of its extended data of ACAD, as a dimension's are:
    /// <see cref="GetDimstyleData"/> reads them. A file of R12 has no TOLERANCE.
    /// </para>
    /// <para>
    /// <see cref="DxfEntity.TransformBy"/> moves it as AutoCAD 2021 moves one, and so do MOVE, COPY, ROTATE, SCALE and MIRROR:
    /// its point moved, its direction turned, its normal the one its direction and the frame's up make after the move, so that
    /// a mirror turns it over (normal (0, 0, -1)); a move that scales it writes its text height and gap scaled as overrides
    /// (DIMTXT, then DIMGAP), as AutoCAD 2021 writes them: a frame of the style's 0.18 and 0.09 scaled by 3 holds
    /// <c>1070 140, 1040 0.54, 1070 147, 1040 0.27</c>. It is not exploded: AutoCAD 2021's <c>Explode</c> refuses one too.
    /// </para>
    /// <para>
    /// <see cref="GeometricExtents"/> is the box of its rows as AutoCAD 2021 lays them out, where the drawing's
    /// <see cref="DxfDatabase.FontSearchPath"/> measures its words.
    /// </para>
    /// </remarks>
    public sealed class DxfTolerance : DxfEntity
    {
        // The groups of AcDbFcf in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[] { 100, 3, 10, 20, 30, 1, 210, 220, 230, 11, 21, 31 }
        );

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private static readonly GeoPoint3 XAxis = new GeoPoint3(1.0, 0.0, 0.0);

        /// <summary>
        /// Initializes a TOLERANCE of no words at the origin, along the world's x in the plane of its z, in the dimension style
        /// Standard, as AutoCAD's <c>new FeatureControlFrame()</c>, on layer 0 in no file; give it its words, point and
        /// direction before it is drawn.
        /// </summary>
        public DxfTolerance()
            : base(
                "TOLERANCE",
                new[]
                {
                    new DxfTypedValue(100, "AcDbFcf"),
                    new DxfTypedValue(3, "Standard"),
                    new DxfTypedValue(10, "0"),
                    new DxfTypedValue(20, "0"),
                    new DxfTypedValue(30, "0"),
                    new DxfTypedValue(1, string.Empty),
                }
            ) { }

        private DxfTolerance(DxfItem item)
            : base(item) { }

        [DxfType("TOLERANCE")]
        internal static DxfEntity Create(DxfItem item) => new DxfTolerance(item);

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        /// <summary>
        /// Gets or sets the words of the frame (group 1), as AutoCAD's <c>Text</c>: a line feed between rows and <c>%%v</c>
        /// between cells, the symbols in the gdt font, as the class's remarks set them out; empty where the record gives none.
        /// </summary>
        /// <value>Set, the words; a line feed is written <c>^J</c>, as AutoCAD writes it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the words set are null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public string Text
        {
            get => DxfReader.Decode(DxfReader.Carets(GetString(1) ?? string.Empty));
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, Text, StringComparison.Ordinal))
                {
                    SetString(1, DxfReader.InCaretForm(value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the insertion point (groups 10, 20, 30), as AutoCAD's <c>Location</c>: in the world, the middle of the
        /// left side of the first row, where a leader meets the frame; the origin where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public GeoPoint3 Location
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets the direction the rows run in (groups 11, 21, 31), as AutoCAD's <c>Direction</c>: of unit length, the
        /// world's x where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public GeoVector3 Direction
        {
            get
            {
                GeoPoint3 raw = GetPoint(11, XAxis);
                double length = Math.Sqrt(raw.X * raw.X + raw.Y * raw.Y + raw.Z * raw.Z);
                return !(length > 0.0) || double.IsInfinity(length) ? new GeoVector3(1.0, 0.0, 0.0)
                    : Math.Abs(length - 1.0) <= 1E-12 ? new GeoVector3(raw.X, raw.Y, raw.Z)
                    : new GeoVector3(raw.X / length, raw.Y / length, raw.Z / length);
            }
            set
            {
                GeoVector3 unit = DxfCircle.Unit(value, nameof(value));
                GeoVector3 now = Direction;

                if (!(Same(unit.X, now.X) && Same(unit.Y, now.Y) && Same(unit.Z, now.Z)))
                {
                    SetPoint(11, new GeoPoint3(unit.X, unit.Y, unit.Z), XAxis);
                }
            }
        }

        /// <summary>
        /// Gets or sets the normal of the plane the frame lies in (groups 210, 220, 230), as AutoCAD's <c>Normal</c>: of unit
        /// length, the world's z where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the normal set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>
        /// Gets or sets the name of the dimension style the frame is drawn by (group 3), as AutoCAD's
        /// <c>DimensionStyleName</c>; <c>Standard</c> where the record gives none.
        /// </summary>
        /// <value>Set, the name of a style of the drawing's DIMSTYLE table, letters of either case the same, as the table names it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is in a drawing that has no dimension style of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        public string DimensionStyleName
        {
            get
            {
                string name = DxfReader.NameOf(GetString(3) ?? string.Empty);
                return name.Length == 0 ? "Standard" : name;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, DimensionStyleName, StringComparison.Ordinal))
                {
                    return;
                }

                string name = value.Trim();
                DxfItem style = Style(name);

                if (style == null && IsResident)
                {
                    throw new ArgumentException(
                        "The drawing has no dimension style named " + name + ".",
                        nameof(value)
                    );
                }

                SetString(3, style == null ? name : DxfReader.NameOf(style.File.ValueOf(style, 2) ?? name));
            }
        }

        /// <summary>
        /// Gets the variables the frame is drawn with, as AutoCAD's <c>GetDimstyleData</c>: a new
        /// <see cref="DxfDimStyleTableRecord"/> in no table, named as its <see cref="DimensionStyleName"/>, each variable as the
        /// frame reads it, its override (the list DSTYLE of its extended data of ACAD) where it has one, else its style's, else
        /// AutoCAD's own. A frame of the style ISO-25 given a text height of 5 gives <c>Dimtxt</c> 5 and <c>Dimgap</c> 0.625,
        /// the style's. The frame's rows are twice <c>Dimtxt</c> times <c>Dimscale</c> high, in <c>Dimtxsty</c>, its lines in
        /// <c>Dimclrd</c> and its words in <c>Dimclrt</c>.
        /// </summary>
        /// <returns>The record, to be read.</returns>
        public DxfDimStyleTableRecord GetDimstyleData()
        {
            var data = new DxfDimStyleTableRecord { Name = DimensionStyleName };
            List<DxfTypedValue> acad = Acad(out int open, out int close);
            DxfItem item = Style(DimensionStyleName);
            DxfDimStyleTableRecord style = item == null ? null : DxfDimStyleTableRecord.Of(item);
            DxfFile naming = NamingDrawing();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                int at = OverrideAt(acad, open, close, variable.Code);
                object value;

                if (at >= 0)
                {
                    value =
                        !variable.IsReference ? variable.Read(acad[at + 1].Value)
                        : naming == null ? null
                        : DxfDimStyleTableRecord.Resolve(naming, variable, acad[at + 1].Value);
                }
                else
                {
                    value =
                        style != null ? style.Get(variable)
                        : variable.IsReference ? null
                        : variable.Read(null);
                }

                data.Keep(variable, value);
            }

            return data;
        }

        /// <summary>
        /// Gets the box the frame fills, as AutoCAD's <c>GeometricExtents</c>: that of its rows as AutoCAD 2021 lays them out
        /// (probes GHTQ1 and GHTQ2), each twice the text height high (<c>Dimtxt</c> times <c>Dimscale</c> of
        /// <see cref="GetDimstyleData"/>), the first about its <see cref="Location"/>, each next under the one before on the same
        /// left side, each as wide as its cells, a cell its ink and the text height, along its <see cref="Direction"/> in the
        /// plane of its <see cref="Normal"/>. <c>{\Fgdt;j}%%v0.1</c> at (0, 0, 0) in a style of Arial 0.18 high is from
        /// (0, -0.18, 0) to (0.92296, 0.18, 0), and a frame of no words from (0, -0.18, 0) to (0, 0.18, 0). The words are
        /// measured in the drawing's fonts (<see cref="DxfDatabase.FontSearchPath"/>), in the text style of <c>Dimtxsty</c>, the
        /// symbols of the gdt font by the widths AutoCAD gives them. Elsewhere, where a cell holds words the fonts do not
        /// measure, as <see cref="DxfEntity.GeometricExtents"/> gives it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown, with words not measured, as <see cref="DxfEntity.GeometricExtents"/> throws.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                DxfDimStyleTableRecord data = GetDimstyleData();
                double h = data.Dimtxt * (data.Dimscale > 0.0 ? data.Dimscale : 1.0);
                List<double> widths = h > 0.0 && !double.IsInfinity(h) ? RowWidths(data, h) : null;

                if (widths == null)
                {
                    return base.GeometricExtents;
                }

                GeoPoint3 at = Location;
                GeoVector3 x = Direction;
                GeoVector3 normal = Normal;
                GeoVector3 y = normal.CrossProduct(x);
                double length = y.Length;
                y = length > 0.0 ? y.Divide(length) : new GeoVector3(0.0, 1.0, 0.0);
                double[] box = null;

                for (int r = 0; r < widths.Count; r++)
                {
                    foreach (double u in new[] { 0.0, widths[r] })
                    {
                        foreach (double v in new[] { h - 2.0 * h * r, -h - 2.0 * h * r })
                        {
                            GeoPoint3 p = at.Add(x.Multiply(u)).Add(y.Multiply(v));
                            box = box ?? new[] { p.X, p.Y, p.Z, p.X, p.Y, p.Z };
                            box[0] = Math.Min(box[0], p.X);
                            box[1] = Math.Min(box[1], p.Y);
                            box[2] = Math.Min(box[2], p.Z);
                            box[3] = Math.Max(box[3], p.X);
                            box[4] = Math.Max(box[4], p.Y);
                            box[5] = Math.Max(box[5], p.Z);
                        }
                    }
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        /// <summary>
        /// The width of each row of the frame at a text height, its words measured in the drawing's fonts; null where a cell
        /// holds words they do not measure, or the frame is in no drawing with fonts.
        /// </summary>
        private List<double> RowWidths(DxfDimStyleTableRecord data, double h)
        {
            DxfFonts fonts = IsResident ? Item.File.Database?.Fonts : null;
            bool asked = false;
            bool failed = false;
            DxfFont font = null;
            DxfFonts.Big big = null;

            double[] Measure(string words)
            {
                if (!asked)
                {
                    asked = true;
                    DxfTextStyleTableRecord style = data.Dimtxsty
                        ?? (
                            Item.File.Database != null && Item.File.Database.TextStyleTable.Has("Standard")
                                ? Item.File.Database.TextStyleTable["Standard"]
                                : null
                        );
                    font =
                        fonts == null || style == null || !Same(style.XScale, 1.0) || style.ObliquingAngle != 0.0
                            ? null
                            : fonts.FontOf(style, out big);
                }

                DxfMTextLayout layout = font == null ? null : DxfMTextLayout.Of(font, big, words, h, 0.0, 1.0, false);

                if (layout == null || layout.Lines.Count == 0)
                {
                    failed = true;
                    return null;
                }

                // The ink as an MTEXT of the words measures it: of a TrueType font none left of the pen's start.
                DxfTextLine line = layout.Lines[0].Measure;
                bool inked = !line.AllSpaces && line.ActualWidth > 0.0;
                return new[]
                {
                    inked ? line.WordsStart : double.NaN,
                    inked ? line.WordsStart + line.ActualWidth : double.NaN,
                    line.Advance,
                    0.0,
                };
            }

            var widths = new List<double>();
            var placed = new List<KeyValuePair<double, object>>();

            foreach (string row in DxfFeatureFrameLayout.Rows(Text))
            {
                double left = 0.0;

                foreach (string cell in DxfFeatureFrameLayout.Cells(row))
                {
                    double width = DxfFeatureFrameLayout.Cell(cell, h, Measure, placed, out _);
                    left += width > 0.0 ? width : 0.0;
                }

                widths.Add(left);
            }

            return failed ? null : widths;
        }

        /// <summary>
        /// Gives the frame the variables of a record that differ from what it reads, each as an override (the list DSTYLE of
        /// its extended data of ACAD), as AutoCAD writes one: an override it has already changed where it stands, a new one put
        /// last; a block, linetype or text style by its handle. A variable the file's version has not is passed over where the
        /// record holds AutoCAD's own value of it.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the record is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value is one AutoCAD does not take.</exception>
        /// <exception cref="ArgumentException">Thrown when a record of the record is of another drawing, or the frame is in none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased, or its file is too old for a variable given.</exception>
        internal void SetDimstyleData(DxfDimStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            DxfDimStyleTableRecord now = GetDimstyleData();
            DxfFile file = Item.File;
            var changes = new List<KeyValuePair<int, DxfTypedValue>>();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                object value = style.Get(variable);

                if (
                    (value == null && variable.Kind == DxfDimKind.TextStyle)
                    || variable.SameValue(value, now.Get(variable))
                    || (
                        !file.Supports(variable.Since)
                        && (variable.IsReference ? value == null : variable.SameValue(value, variable.Read(null)))
                    )
                )
                {
                    continue;
                }

                variable.Check(value, nameof(style));
                DxfDatabase.CheckDimRange(variable, value, nameof(style));

                if (IsErased)
                {
                    throw new InvalidOperationException(
                        "The TOLERANCE " + (Handle ?? string.Empty) + " is erased, and is not changed."
                    );
                }

                file.Requires(variable.Since, variable.Name.ToUpperInvariant(), variable.SinceSaid);
                string text;

                if (!variable.IsReference)
                {
                    text = variable.Write(value);
                }
                else if (value == null)
                {
                    text = "0";
                }
                else if (!IsResident)
                {
                    throw new ArgumentException(
                        "The "
                            + variable.Name
                            + " of a frame is a record of its own drawing, and the frame is in none.",
                        nameof(style)
                    );
                }
                else
                {
                    text = DxfDimStyleTableRecord.Reference(file, variable, (DxfDBObject)value, false, nameof(style));
                }

                changes.Add(
                    new KeyValuePair<int, DxfTypedValue>(
                        variable.Code,
                        new DxfTypedValue(variable.DataCode(false), text)
                    )
                );
            }

            if (changes.Count == 0)
            {
                return;
            }

            List<DxfTypedValue> data = Acad(out int open, out int close);

            if (open < 0)
            {
                data.Add(new DxfTypedValue(1000, "DSTYLE"));
                data.Add(new DxfTypedValue(1002, "{"));
                data.Add(new DxfTypedValue(1002, "}"));
                open = data.Count - 2;
                close = data.Count - 1;
            }

            foreach (KeyValuePair<int, DxfTypedValue> change in changes)
            {
                int at = OverrideAt(data, open, close, change.Key);

                if (at >= 0)
                {
                    data[at + 1] = change.Value;
                    continue;
                }

                data.Insert(close, change.Value);
                data.Insert(close, new DxfTypedValue(1070, change.Key.ToString(CultureInfo.InvariantCulture)));
                close += 2;
            }

            var groups = new List<DxfTypedValue> { new DxfTypedValue(1001, "ACAD") };
            groups.AddRange(data);
            XData = groups;
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the frame, as AutoCAD 2021's <c>TransformBy</c> does: its point moved, its direction
        /// the frame's x moved and made of unit length, its normal the cross of the frame's x and up moved, so that a mirror
        /// turns it over; where the move scales the frame's x, its text height and gap are written as overrides that much
        /// larger, DIMTXT then DIMGAP, as AutoCAD 2021 writes them. The record is written once.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the frame's x or up to no length, or one
        /// along the other.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the frame is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            GeoVector3 x = Direction;
            GeoVector3 normal = Normal;
            GeoVector3 up = normal.CrossProduct(x);
            GeoPoint3 at = transform.Transform(Location);
            GeoVector3 movedX = transform.Transform(x);
            GeoVector3 movedUp = transform.Transform(up);
            double scale = movedX.Length;
            GeoVector3 movedNormal = movedX.CrossProduct(movedUp);
            double length = movedNormal.Length;

            if (!(scale > 0.0) || double.IsInfinity(scale) || !(length > 0.0) || double.IsInfinity(length))
            {
                throw new ArgumentException(
                    "The transformation takes the frame's direction or its up to no length, or one along the other.",
                    nameof(transform)
                );
            }

            GeoVector3 direction = movedX.Divide(scale);
            SetGroups(
                Join(
                    PointGroups(10, at, Origin),
                    NormalGroups(movedNormal.Divide(length)),
                    PointGroups(11, new GeoPoint3(direction.X, direction.Y, direction.Z), XAxis)
                )
            );

            if (!Same(scale, 1.0))
            {
                DxfDimStyleTableRecord data = GetDimstyleData();
                WriteScaled(data.Dimtxt * scale, data.Dimgap * scale);
            }
        }

        /// <summary>
        /// Writes the text height and the gap of a frame scaled as AutoCAD 2021 writes them: each override of the two taken out,
        /// then DIMTXT and DIMGAP put last of the list DSTYLE, made where the frame has none, the rest of its extended data kept.
        /// </summary>
        private void WriteScaled(double height, double gap)
        {
            List<DxfTypedValue> data = Acad(out int open, out int close);

            if (open < 0)
            {
                data.Add(new DxfTypedValue(1000, "DSTYLE"));
                data.Add(new DxfTypedValue(1002, "{"));
                data.Add(new DxfTypedValue(1002, "}"));
                open = data.Count - 2;
                close = data.Count - 1;
            }

            foreach (int code in new[] { 140, 147 })
            {
                int at = OverrideAt(data, open, close, code);

                if (at >= 0)
                {
                    data.RemoveRange(at, 2);
                    close -= 2;
                }
            }

            data.InsertRange(
                close,
                new[]
                {
                    new DxfTypedValue(1070, "140"),
                    new DxfTypedValue(1040, Numbers.Exact(height + 0.0)),
                    new DxfTypedValue(1070, "147"),
                    new DxfTypedValue(1040, Numbers.Exact(gap + 0.0)),
                }
            );
            var groups = new List<DxfTypedValue> { new DxfTypedValue(1001, "ACAD") };
            groups.AddRange(data);
            XData = groups;
        }

        /// <summary>
        /// The drawing the frame's dimension style is read in: its own; for a copy in no file, the drawing it was copied from;
        /// null for one erased, or made in no drawing.
        /// </summary>
        private DxfFile NamingDrawing() => IsResident || Item.File.IsDetached ? Item.File.NamingDrawing : null;

        /// <summary>The DIMSTYLE of a name, letters of either case the same, of the drawing the frame reads it in; null where there is none.</summary>
        private DxfItem Style(string name)
        {
            DxfFile file = NamingDrawing();

            if (file == null)
            {
                return null;
            }

            foreach (DxfItem item in file.TableItems("DIMSTYLE"))
            {
                if (
                    !item.IsRemoved
                    && string.Equals(
                        DxfReader.NameOf(item.File.ValueOf(item, 2) ?? string.Empty),
                        name,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>The extended data of ACAD after its 1001, and where the braces of DSTYLE stand in it; -1 for each where there are none.</summary>
        private List<DxfTypedValue> Acad(out int open, out int close)
        {
            IReadOnlyList<DxfTypedValue> acad = GetXDataForApplication("ACAD");
            var data = new List<DxfTypedValue>();

            for (int i = 1; acad != null && i < acad.Count; i++)
            {
                data.Add(acad[i]);
            }

            open = -1;
            close = -1;

            for (int i = 0; i + 1 < data.Count; i++)
            {
                if (
                    data[i].TypeCode == 1000
                    && string.Equals(data[i].Value.Trim(), "DSTYLE", StringComparison.OrdinalIgnoreCase)
                    && data[i + 1].TypeCode == 1002
                    && data[i + 1].Value.Trim() == "{"
                )
                {
                    open = i + 1;
                    close = data.FindIndex(open + 1, g => g.TypeCode == 1002 && g.Value.Trim() == "}");
                    close = close < 0 ? data.Count : close;
                    break;
                }
            }

            return data;
        }

        /// <summary>Where the 1070 of a variable's override stands in the list DSTYLE read; -1 where the frame has none.</summary>
        private static int OverrideAt(List<DxfTypedValue> data, int open, int close, int code)
        {
            for (int i = open + 1; open >= 0 && i + 1 < close; i += 2)
            {
                if (
                    data[i].TypeCode == 1070
                    && int.TryParse(
                        data[i].Value.Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int read
                    )
                    && read == code
                )
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
