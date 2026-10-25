using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A HATCH of a DXF file, as AutoCAD's <c>Hatch</c>: an area closed by loops in the plane of its <see cref="Normal"/> at its
    /// <see cref="Elevation"/>, filled solid, with a pattern of lines or with a gradient of colours.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A square 30 by 20 with a circle of radius 5 inside it, filled with ANSI31 in the <see cref="DxfHatchStyle.Normal"/> style,
    /// has two loops and <see cref="Area"/> 600 less 78.54, 521.46; read from a file AutoCAD wrote, its pattern holds the one line
    /// of ANSI31 (<see cref="GetPatternDefinitionAt"/>), at 45 degrees and 3.175 apart, where the same made in code holds none, as
    /// the next paragraph says. The loops are given in the world as the library's shapes (<see cref="GetLoopAt"/>); the file holds them in
    /// the system of the normal, as AutoCAD's <c>GetLoopAt</c> gives them.
    /// </para>
    /// <para>
    /// Each property is read from and written to the HATCH's own groups, every group a change is not about keeping its text, and
    /// a property given the value it has changes nothing. A change to the loops of an associative HATCH (<see cref="Associative"/>)
    /// takes its association away with the handles of the objects it followed, as AutoCAD's does: its loops are its own from then
    /// on. A copy (<see cref="DxfEntity.Clone"/>) follows nothing; one copied with every object it follows by
    /// <see cref="DxfDatabase.WblockCloneObjects"/> follows their copies. The lines a pattern draws are worked out by AutoCAD from the pattern and the loops (<c>EvaluateHatch</c>), and are not
    /// in the file; a pattern of AutoCAD's own pattern file the HATCH does not hold the lines of is not known to the library:
    /// <c>new DxfHatch()</c> given ANSI31 by <see cref="SetHatchPattern"/> holds no lines of it
    /// (<see cref="NumberOfPatternDefinitions"/> 0), its name and type written for AutoCAD to take the lines from its own file.
    /// </para>
    /// <para>
    /// A file older than R2000 has no HATCH: <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> refuses one there, and a
    /// gradient needs R2004 (AC1018). A HATCH copied into a drawing of another version is written as that version writes it,
    /// as AutoCAD's SAVEAS writes it: each spline edge's count of fit points (97) as <see cref="InsertLoopAt"/> says, and its
    /// gradient taken out in a drawing before R2004, the HATCH left filled solid.
    /// </para>
    /// </remarks>
    public sealed partial class DxfHatch : DxfEntity
    {
        // The groups AutoCAD writes a HATCH's own part in, and the blocks among them: where one the record has not goes.
        private static readonly int[] Ranked = { 10, 20, 30, 210, 220, 230, 2, 70, 71, 91, 75, 76, 52, 41, 77, 78, 47, 98, 450 };

        // The codes of the boundary paths, those of the pattern's lines and those of the seed points, as ezdxf takes them.
        private static readonly HashSet<int> PathCodes = new HashSet<int> { 10, 20, 11, 21, 12, 22, 13, 23, 40, 42, 50, 51, 72, 73, 74, 92, 93, 94, 95, 96, 97, 330 };
        private static readonly HashSet<int> LineCodes = new HashSet<int> { 53, 43, 44, 45, 46, 79, 49 };
        private static readonly HashSet<int> SeedCodes = new HashSet<int> { 10, 20 };

        // The gradients AutoCAD has of its own, as ezdxf names them.
        private static readonly string[] Gradients = { "LINEAR", "CYLINDER", "INVCYLINDER", "SPHERICAL", "INVSPHERICAL", "HEMISPHERICAL", "INVHEMISPHERICAL", "CURVED", "INVCURVED" };

        private const string Background = "HATCHBACKGROUNDCOLOR";

        /// <summary>
        /// Initializes a HATCH of no loops, filled solid, on the floor, as AutoCAD's <c>new Hatch()</c>, on layer 0 in no file;
        /// give it its pattern with <see cref="SetHatchPattern"/> and its loops with <see cref="AppendLoop(DxfHatchLoop)"/>.
        /// </summary>
        public DxfHatch()
            : base("HATCH", new[]
            {
                new DxfTypedValue(100, "AcDbHatch"), new DxfTypedValue(10, "0"), new DxfTypedValue(20, "0"), new DxfTypedValue(30, "0"),
                new DxfTypedValue(210, "0"), new DxfTypedValue(220, "0"), new DxfTypedValue(230, "1"), new DxfTypedValue(2, "SOLID"),
                new DxfTypedValue(70, "1"), new DxfTypedValue(71, "0"), new DxfTypedValue(91, "0"), new DxfTypedValue(75, "0"),
                new DxfTypedValue(76, "1"), new DxfTypedValue(98, "0"),
            })
        {
        }

        private DxfHatch(DxfItem item)
            : base(item)
        {
        }

        [DxfType("HATCH")]
        internal static DxfEntity Create(DxfItem item) => new DxfHatch(item);

        #region The plane

        /// <summary>
        /// Gets the plane of the HATCH, as AutoCAD's <c>GetPlane</c>: that of its <see cref="Normal"/> at its <see cref="Elevation"/>,
        /// through the point of elevation (groups 10, 20, 30) in the world, where its loops lie.
        /// </summary>
        /// <returns>The plane.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public override GeoPlane3 GetPlane()
        {
            Snapshot snapshot = Snap();
            return new GeoPlane3(OcsToWorld(snapshot.Normal, new GeoPoint3(0.0, 0.0, snapshot.Elevation)), snapshot.Normal);
        }

        /// <summary>
        /// Gets the box the loops of the HATCH fill, as AutoCAD's <c>GeometricExtents</c>: the least and greatest x, y and z of
        /// their lines, arcs, ellipses and splines in the world, as <see cref="GetLoopAt"/> gives them; a square 30 by 20 round a
        /// circle gives the square's.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH has no loop, as AutoCAD throws for an entity with no
        /// extents, or a loop has an edge that is no shape.</exception>
        public override GeoAabb3 GeometricExtents
        {
            get
            {
                double[] box = null;

                for (int i = 0; i < NumberOfLoops; i++)
                {
                    DxfHatchLoop loop = GetLoopAt(i);
                    IEnumerable<object> shapes = loop.IsPolyline ? (IEnumerable<object>)new[] { loop.Polyline } : loop.Curves;

                    foreach (object shape in shapes)
                    {
                        DxfWriter.RecordsOf(shape, 0, 0.0, true, true, out double[] extents);

                        if (extents == null)
                        {
                            continue;
                        }

                        if (box == null)
                        {
                            box = (double[])extents.Clone();
                            continue;
                        }

                        for (int k = 0; k < 3; k++)
                        {
                            box[k] = Math.Min(box[k], extents[k]);
                            box[k + 3] = Math.Max(box[k + 3], extents[k + 3]);
                        }
                    }
                }

                if (box == null)
                {
                    throw new InvalidOperationException("The HATCH " + (Handle ?? string.Empty) + " has no loop: it has no extents.");
                }

                return new GeoAabb3(new GeoPoint3(box[0], box[1], box[2]), new GeoPoint3(box[3], box[4], box[5]));
            }
        }

        /// <summary>
        /// Gets or sets how far the plane of the HATCH stands from the origin along its normal, as AutoCAD's <c>Elevation</c>: the
        /// z of its point of elevation (group 30); nought where the record gives none.
        /// </summary>
        /// <value>Set, a finite number: the loops keep their coordinates in the plane, which moves along the normal.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public double Elevation
        {
            get => Snap().Elevation;

            set
            {
                Guard.Finite(value, nameof(value), "An elevation has to be a finite number.");
                Parts record = Record();

                if (Same(value, Num(record.Text(30), 0.0)))
                {
                    return;
                }

                foreach (int code in new[] { 10, 20 })
                {
                    if (record.Text(code) == null)
                    {
                        record.Set(code, "0");
                    }
                }

                record.Set(30, Real(value));
                Write(record);
            }
        }

        /// <summary>
        /// Gets or sets the normal of the plane of the HATCH (groups 210, 220, 230), as AutoCAD's <c>Normal</c>: of unit length,
        /// (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>
        /// Set, a direction of some length, written made of unit length. The loops keep their coordinates in the plane of the
        /// normal, as a <see cref="DxfPolyline"/>'s vertices do, so the HATCH turns with the plane.
        /// </value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();

            set
            {
                KeyValuePair<int, string>[] groups = NormalGroups(value);

                if (groups.Length == 0)
                {
                    return;
                }

                Parts record = Record();

                foreach (KeyValuePair<int, string> group in groups)
                {
                    record.Set(group.Key, group.Value);
                }

                Write(record);
            }
        }

        #endregion

        #region The pattern

        /// <summary>
        /// Gets the name of the pattern (group 2), as AutoCAD's <c>PatternName</c>: <c>SOLID</c> for a solid fill, <c>ANSI31</c>,
        /// <c>_USER</c> for one made by the user; <c>SOLID</c> where the record gives none. It is set by <see cref="SetHatchPattern"/>.
        /// </summary>
        public string PatternName => Record().Text(2)?.Trim() ?? "SOLID";

        /// <summary>
        /// Gets where the pattern comes from (group 76), as AutoCAD's <c>PatternType</c>; <see cref="DxfHatchPatternType.PreDefined"/>
        /// where the record gives none. It is set by <see cref="SetHatchPattern"/>.
        /// </summary>
        public DxfHatchPatternType PatternType => (DxfHatchPatternType)Whole(Record().Text(76), 1);

        /// <summary>Gets whether the HATCH is filled solid (group 70 of 1), as AutoCAD's <c>IsSolidFill</c>: a gradient is filled solid too.</summary>
        public bool IsSolidFill => Whole(Record().Text(70), 1) == 1;

        /// <summary>
        /// Gets or sets the scale of a pattern of a pattern file (group 41), as AutoCAD's <c>PatternScale</c>: 2 draws ANSI31's
        /// lines twice as far apart; 1 where the record gives none, and for a pattern made by the user, whose group 41 is its
        /// spacing (<see cref="PatternSpace"/>).
        /// </summary>
        /// <value>
        /// Set, a finite number above nought: written in group 41, and the lines of the pattern the HATCH holds scaled by the new
        /// scale over the old, their points about the origin of the plane, as ezdxf's <c>set_pattern_scale</c> scales them.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a scale other than the one it has is set on a solid fill or a
        /// pattern made by the user, which have none, or on a HATCH that is erased.</exception>
        public double PatternScale
        {
            get => PatternType == DxfHatchPatternType.UserDefined ? 1.0 : Num(Record().Text(41), 1.0);

            set
            {
                if (Same(value, PatternScale))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "A pattern's scale has to be a finite number above nought.");
                Parts record = Patterned("scale");

                if (PatternType == DxfHatchPatternType.UserDefined)
                {
                    throw new InvalidOperationException("A pattern made by the user has a spacing, not a scale: set its PatternSpace.");
                }

                double old = Num(record.Text(41), 1.0);
                record.Set(41, Real(value));
                ScaleLines(record, old > 0.0 ? value / old : 1.0, 0.0);
                Write(record);
            }
        }

        /// <summary>
        /// Gets or sets the angle of the pattern (group 52, in degrees), as AutoCAD's <c>PatternAngle</c>: in radians, nought where
        /// the record gives none.
        /// </summary>
        /// <value>
        /// Set, a finite number: written in group 52 in degrees, and the lines of the pattern the HATCH holds turned by the new
        /// angle less the old, about the origin of the plane, as ezdxf's <c>set_pattern_angle</c> turns them.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an angle other than the one it has is set on a solid fill, which
        /// has none, or on a HATCH that is erased.</exception>
        public double PatternAngle
        {
            get => Num(Record().Text(52), 0.0) * (Math.PI / 180.0);

            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

                if (Same(value, PatternAngle))
                {
                    return;
                }

                Parts record = Patterned("angle");
                double old = Num(record.Text(52), 0.0);
                double degrees = DxfOcsTransform.Degrees(value);
                record.Set(52, Real(degrees));
                ScaleLines(record, 1.0, degrees - old);
                Write(record);
            }
        }

        /// <summary>
        /// Gets or sets the spacing of the lines of a pattern made by the user (group 41), as AutoCAD's <c>PatternSpace</c>; 1 where
        /// the record gives none, and for a pattern of a pattern file, whose group 41 is its scale.
        /// </summary>
        /// <value>Set, a finite number above nought: written in group 41, and the lines of the pattern written anew, one family at
        /// <see cref="PatternAngle"/> that far apart and, where it is double, another square to it, as AutoCAD makes them.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the spacing set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a spacing other than the one it has is set on a solid fill or a
        /// pattern of a pattern file, or on a HATCH that is erased.</exception>
        public double PatternSpace
        {
            get => PatternType == DxfHatchPatternType.UserDefined ? Num(Record().Text(41), 1.0) : 1.0;

            set
            {
                if (Same(value, PatternSpace))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "A pattern's spacing has to be a finite number above nought.");
                Parts record = Patterned("spacing");

                if (PatternType != DxfHatchPatternType.UserDefined)
                {
                    throw new InvalidOperationException("A pattern of a pattern file has a scale, not a spacing: set its PatternScale.");
                }

                record.Set(41, Real(value));
                record.SetPart(Kind.Pattern, UserLines(Num(record.Text(52), 0.0), value, Whole(record.Text(77), 0) != 0));
                Write(record);
            }
        }

        /// <summary>
        /// Gets or sets whether the lines of a pattern made by the user are crossed by as many again square to them (group 77), as
        /// AutoCAD's <c>PatternDouble</c>; false where the record gives none.
        /// </summary>
        /// <value>Set, written in group 77; the lines of a pattern made by the user written anew to agree.</value>
        /// <exception cref="InvalidOperationException">Thrown when it is changed on a solid fill, which has no pattern, or on a
        /// HATCH that is erased.</exception>
        public bool PatternDouble
        {
            get => Whole(Record().Text(77), 0) != 0;

            set
            {
                if (value == PatternDouble)
                {
                    return;
                }

                Parts record = Patterned("double");
                record.Set(77, value ? "1" : "0");

                if (PatternType == DxfHatchPatternType.UserDefined)
                {
                    record.SetPart(Kind.Pattern, UserLines(Num(record.Text(52), 0.0), Num(record.Text(41), 1.0), value));
                }

                Write(record);
            }
        }

        /// <summary>
        /// Gives the HATCH a pattern, as AutoCAD's <c>SetHatchPattern</c>: its type (group 76) and name (2). <c>SOLID</c> fills it
        /// solid (70 of 1) and takes out the pattern's angle, scale and lines and any gradient; any other name fills it with a
        /// pattern (70 of 0) at the angle and scale it has, its gradient taken out. A pattern made by the user is given its lines,
        /// one family at <see cref="PatternAngle"/> <see cref="PatternSpace"/> apart, crossed where it is double, as AutoCAD makes
        /// them; a pattern of the name it has keeps its lines; the lines of any other pattern are in AutoCAD's pattern file, not in
        /// the drawing, and are left out, for AutoCAD to draw the pattern by its name.
        /// </summary>
        /// <param name="patternType">Where the pattern comes from.</param>
        /// <param name="patternName">The name of the pattern, as ANSI31; <c>_USER</c> for one made by the user, as AutoCAD names it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a line break in it.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is not one of <see cref="DxfHatchPatternType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void SetHatchPattern(DxfHatchPatternType patternType, string patternName) => Pattern(patternType, patternName, false);

        /// <summary>Gives the HATCH a pattern, as <see cref="SetHatchPattern"/> does; forced, a gradient of the same pattern taken out.</summary>
        private void Pattern(DxfHatchPatternType patternType, string patternName, bool force)
        {
            if (patternName == null)
            {
                throw new ArgumentNullException(nameof(patternName));
            }

            if (patternType < DxfHatchPatternType.UserDefined || patternType > DxfHatchPatternType.CustomDefined)
            {
                throw new ArgumentOutOfRangeException(nameof(patternType), patternType, "A pattern is made by the user, of AutoCAD's pattern file or of another, 0 to 2.");
            }

            string name = patternName.Trim();

            if (name.Length == 0 || name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A pattern has a name, on one line.", nameof(patternName));
            }

            Parts record = Record();
            string was = record.Text(2)?.Trim() ?? "SOLID";
            bool solid = string.Equals(name, "SOLID", StringComparison.OrdinalIgnoreCase) && patternType != DxfHatchPatternType.UserDefined;
            bool same = string.Equals(name, was, StringComparison.OrdinalIgnoreCase) && Whole(record.Text(76), 1) == (int)patternType;

            if (same && (solid ? IsSolidFill && !(force && IsGradient) : !IsSolidFill))
            {
                return;
            }

            record.Set(2, name);
            record.Set(76, ((int)patternType).ToString(CultureInfo.InvariantCulture));
            record.SetPart(Kind.Gradient, null);

            if (solid)
            {
                record.Set(70, "1");
                record.Set(52, null);
                record.Set(41, null);
                record.Set(77, null);
                record.SetPart(Kind.Pattern, null);
                Write(record);
                return;
            }

            record.Set(70, "0");
            double angle = Num(record.Text(52), 0.0);
            double scale = Num(record.Text(41), 1.0);
            bool twice = Whole(record.Text(77), 0) != 0;
            record.Set(52, record.Text(52) ?? "0");
            record.Set(41, record.Text(41) ?? "1");
            record.Set(77, record.Text(77) ?? "0");

            if (patternType == DxfHatchPatternType.UserDefined)
            {
                record.SetPart(Kind.Pattern, UserLines(angle, scale, twice));
            }
            else if (!same)
            {
                record.SetPart(Kind.Pattern, null);
            }

            Write(record);
        }

        /// <summary>Gets how many lines the pattern has (group 78), as AutoCAD's <c>NumberOfPatternDefinitions</c>: nought for a solid fill.</summary>
        public int NumberOfPatternDefinitions => Lines(Record()).Count;

        /// <summary>
        /// Gets a line of the pattern, as AutoCAD's <c>GetPatternDefinitionAt</c>: as the HATCH holds it, scaled and turned with
        /// the pattern, in the system of its normal.
        /// </summary>
        /// <param name="index">The index of the line, from nought.</param>
        /// <returns>The line.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the pattern has no line of the index.</exception>
        public DxfPatternDefinition GetPatternDefinitionAt(int index)
        {
            List<double[]> lines = Lines(Record());

            if (index < 0 || index >= lines.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The pattern has " + lines.Count.ToString(CultureInfo.InvariantCulture) + " lines, from nought.");
            }

            double[] line = lines[index];
            var dashes = new double[line.Length - 5];
            Array.Copy(line, 5, dashes, 0, dashes.Length);
            return new DxfPatternDefinition(line[0] * (Math.PI / 180.0), line[1], line[2], line[3], line[4], dashes);
        }

        /// <summary>
        /// Gets or sets which areas the loops close are filled (group 75), as AutoCAD's <c>HatchStyle</c>;
        /// <see cref="DxfHatchStyle.Normal"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the style set is not one of <see cref="DxfHatchStyle"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public DxfHatchStyle HatchStyle
        {
            get => (DxfHatchStyle)Whole(Record().Text(75), 0);

            set
            {
                if (value == HatchStyle)
                {
                    return;
                }

                if (value < DxfHatchStyle.Normal || value > DxfHatchStyle.Ignore)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A hatch style is normal, outer or ignore, 0 to 2.");
                }

                Parts record = Record();
                record.Set(75, ((int)value).ToString(CultureInfo.InvariantCulture));
                Write(record);
            }
        }

        #endregion

        #region The gradient

        /// <summary>
        /// Gets or sets whether the HATCH is filled with a pattern or a gradient (group 450), as AutoCAD's <c>HatchObjectType</c>;
        /// <see cref="DxfHatchObjectType.HatchObject"/> where the record gives none.
        /// </summary>
        /// <value>
        /// Set to a gradient, the HATCH is given one as <see cref="SetGradient"/> gives it, LINEAR where it had none; set to a
        /// pattern, its gradient is taken out and it is filled solid, as <see cref="SetHatchPattern"/> fills it with SOLID.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is not one of <see cref="DxfHatchObjectType"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased, or a gradient is set in a file older than R2004 (AC1018).</exception>
        public DxfHatchObjectType HatchObjectType
        {
            get => IsGradient ? DxfHatchObjectType.GradientObject : DxfHatchObjectType.HatchObject;

            set
            {
                if (value == HatchObjectType)
                {
                    return;
                }

                if (value == DxfHatchObjectType.GradientObject)
                {
                    string name = Gradient(Record())?.Name;
                    SetGradient(DxfGradientPatternType.PreDefinedGradient, name != null && Array.IndexOf(Gradients, name.ToUpperInvariant()) >= 0 ? name : "LINEAR");
                    return;
                }

                if (value != DxfHatchObjectType.HatchObject)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A hatch is filled with a pattern, 0, or a gradient, 1.");
                }

                Pattern(DxfHatchPatternType.PreDefined, "SOLID", true);
            }
        }

        /// <summary>Gets whether the HATCH is filled with a gradient (group 450 of 1), as AutoCAD's <c>IsGradient</c>.</summary>
        public bool IsGradient => Gradient(Record())?.Kind == 1;

        /// <summary>Gets whether the HATCH is filled with a pattern, solid fill among them, not a gradient, as AutoCAD's <c>IsHatch</c>.</summary>
        public bool IsHatch => !IsGradient;

        /// <summary>
        /// Gets where the gradient comes from, as AutoCAD's <c>GradientType</c>: one of AutoCAD's nine by its name (group 470),
        /// <see cref="DxfGradientPatternType.UserDefinedGradient"/> for any other name.
        /// </summary>
        public DxfGradientPatternType GradientType
        {
            get
            {
                string name = GradientName;
                return name.Length == 0 || Array.IndexOf(Gradients, name.ToUpperInvariant()) >= 0 ? DxfGradientPatternType.PreDefinedGradient : DxfGradientPatternType.UserDefinedGradient;
            }
        }

        /// <summary>Gets the name of the gradient (group 470), as AutoCAD's <c>GradientName</c>: LINEAR, SPHERICAL; empty where the HATCH has no gradient data.</summary>
        public string GradientName => Gradient(Record())?.Name ?? string.Empty;

        /// <summary>
        /// Gets or sets the angle of the gradient (group 460), as AutoCAD's <c>GradientAngle</c>: in radians, nought where the
        /// record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when another angle is set on a HATCH with no gradient data, or one that is erased.</exception>
        public double GradientAngle
        {
            get => Num(Gradient(Record())?.Text(460), 0.0);
            set => SetGradientNumber(460, value, GradientAngle, "angle");
        }

        /// <summary>
        /// Gets or sets how far the gradient is shifted from its middle (group 461), as AutoCAD's <c>GradientShift</c>: nought
        /// for a gradient centred, 1 for one shifted all the way; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when another value is set on a HATCH with no gradient data, or one that is erased.</exception>
        public float GradientShift
        {
            get => (float)Num(Gradient(Record())?.Text(461), 0.0);
            set => SetGradientNumber(461, value, GradientShift, "shift");
        }

        /// <summary>
        /// Gets or sets how the colour of a gradient of one colour is tinted (group 462), as AutoCAD's <c>ShadeTintValue</c>: from
        /// nought, dark, to 1, light; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when another value is set on a HATCH with no gradient data, or one that is erased.</exception>
        public float ShadeTintValue
        {
            get => (float)Num(Gradient(Record())?.Text(462), 0.0);
            set => SetGradientNumber(462, value, ShadeTintValue, "tint");
        }

        /// <summary>
        /// Gets or sets whether the gradient runs from one colour to its tint (group 452 of 1), as AutoCAD's
        /// <c>GradientOneColorMode</c>, rather than between two colours; false where the record gives none.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when it is changed on a HATCH with no gradient data, or one that is erased.</exception>
        public bool GradientOneColorMode
        {
            get => Whole(Gradient(Record())?.Text(452), 0) != 0;

            set
            {
                if (value == GradientOneColorMode)
                {
                    return;
                }

                Parts record = Record();
                GradientData gradient = Gradient(record) ?? throw NoGradient();
                gradient.Set(452, value ? "1" : "0");
                record.SetPart(Kind.Gradient, gradient.Groups);
                Write(record);
            }
        }

        /// <summary>
        /// Gets the colours of the gradient (groups 463, 63 and 421), as AutoCAD's <c>GetGradientColors</c>: each a colour of the
        /// index where the record writes one (63), else the true colour of its red, green and blue (421), with its value; none
        /// where the HATCH has no gradient data.
        /// </summary>
        /// <returns>The colours, in their order.</returns>
        public DxfGradientColor[] GetGradientColors()
        {
            GradientData gradient = Gradient(Record());
            return gradient == null ? new DxfGradientColor[0] : gradient.Colors().ToArray();
        }

        /// <summary>
        /// Gives the gradient its colours, as AutoCAD's <c>SetGradientColors</c>: their count (group 453) and, for each, its value
        /// (463), its index where it is a colour of the index (63) and its red, green and blue (421). The colours it has change
        /// nothing.
        /// </summary>
        /// <param name="value">The colours, one or two: a true colour or one of AutoCAD's index.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colours are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there are none or more than two, or a colour is BYLAYER, BYBLOCK or of no red, green and blue.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH has no gradient data, or is erased.</exception>
        public void SetGradientColors(DxfGradientColor[] value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.Length < 1 || value.Length > 2)
            {
                throw new ArgumentException("A gradient has one colour or two.", nameof(value));
            }

            Parts record = Record();
            GradientData gradient = Gradient(record) ?? throw NoGradient();
            List<KeyValuePair<int, string>> colours = GradientData.ColorGroups(value, nameof(value));

            if (gradient.SetColors(colours))
            {
                record.SetPart(Kind.Gradient, gradient.Groups);
                Write(record);
            }
        }

        /// <summary>
        /// Fills the HATCH with a gradient, as AutoCAD's <c>SetGradient</c>: filled solid (70 of 1) with the pattern SOLID, its
        /// pattern's angle, scale and lines taken out, and its gradient data (450 of 1 on) of the name given; the angle, shift,
        /// tint and colours it has kept, or, where it had no gradient data, nought and AutoCAD's colours of a new gradient, blue
        /// (0, 0, 255) to a pale yellow (255, 255, 153), as the variables GFCLR1 and GFCLR2 start.
        /// </summary>
        /// <param name="gradientType">Where the gradient comes from: one of AutoCAD's nine, or one of another name.</param>
        /// <param name="gradientName">The name: LINEAR, CYLINDER, INVCYLINDER, SPHERICAL, INVSPHERICAL, HEMISPHERICAL,
        /// INVHEMISPHERICAL, CURVED or INVCURVED, letters of either case the same, for one of AutoCAD's.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or has a line break in it, or is not one of AutoCAD's
        /// for <see cref="DxfGradientPatternType.PreDefinedGradient"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file is older than R2004 (AC1018), which has no gradients, or the HATCH is erased.</exception>
        public void SetGradient(DxfGradientPatternType gradientType, string gradientName)
        {
            if (gradientName == null)
            {
                throw new ArgumentNullException(nameof(gradientName));
            }

            string name = gradientName.Trim();

            if (name.Length == 0 || name.IndexOf('\n') >= 0 || name.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A gradient has a name, on one line.", nameof(gradientName));
            }

            if (gradientType == DxfGradientPatternType.PreDefinedGradient)
            {
                int known = Array.IndexOf(Gradients, name.ToUpperInvariant());

                if (known < 0)
                {
                    throw new ArgumentException("AutoCAD's gradients are LINEAR, CYLINDER, INVCYLINDER, SPHERICAL, INVSPHERICAL, HEMISPHERICAL, INVHEMISPHERICAL, CURVED and INVCURVED: " + name + " is none of them.", nameof(gradientName));
                }

                name = Gradients[known];
            }
            else if (gradientType != DxfGradientPatternType.UserDefinedGradient)
            {
                throw new ArgumentOutOfRangeException(nameof(gradientType), gradientType, "A gradient is one of AutoCAD's, 0, or of another name, 1.");
            }

            Parts record = Record();
            GradientData gradient = Gradient(record);

            if (gradient != null && gradient.Kind == 1 && string.Equals(gradient.Name, name, StringComparison.Ordinal) && IsSolidFill && string.Equals(PatternName, "SOLID", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Item.File.Requires(1018, "gradients", "R2004 (AC1018)");
            gradient = gradient ?? GradientData.Fresh();
            gradient.Set(450, "1");
            gradient.Set(470, name);
            record.Set(2, "SOLID");
            record.Set(70, "1");
            record.Set(76, "1");
            record.Set(52, null);
            record.Set(41, null);
            record.Set(77, null);
            record.SetPart(Kind.Pattern, null);
            record.SetPart(Kind.Gradient, gradient.Groups);
            Write(record);
        }

        /// <summary>
        /// Gets or sets the colour behind the pattern, as AutoCAD's <c>BackgroundColor</c>: from the extended data of the
        /// application HATCHBACKGROUNDCOLOR (1071), as AutoCAD and ezdxf write it; none where the HATCH has none.
        /// </summary>
        /// <value>Set, a true colour, one of the index, BYLAYER or BYBLOCK; none takes the data out. The colour it has changes nothing.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is by pen, or a layer's off or frozen.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public DxfColor BackgroundColor
        {
            get
            {
                IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(Background);

                if (data != null)
                {
                    foreach (DxfTypedValue group in data)
                    {
                        if (group.TypeCode == 1071)
                        {
                            return DxfMLeaderColors.Of(group.Value);
                        }
                    }
                }

                return DxfColor.FromColorIndex(DxfColorMethod.None, 0);
            }

            set
            {
                int raw = DxfMLeaderColors.Raw(value, nameof(value));

                if (raw == DxfMLeaderColors.Raw(BackgroundColor, nameof(value)) && (value.IsNone || GetXDataForApplication(Background) != null))
                {
                    return;
                }

                XData = value.IsNone
                    ? new[] { new DxfTypedValue(1001, Background) }
                    : new[] { new DxfTypedValue(1001, Background), new DxfTypedValue(1071, raw.ToString(CultureInfo.InvariantCulture)) };
            }
        }

        #endregion

        #region The seed points

        /// <summary>
        /// Gets how many seed points the HATCH has (group 98), as ObjectARX's <c>numSeedPoints</c>: the points inside its areas
        /// AutoCAD found the loops from.
        /// </summary>
        public int NumSeedPoints => Seeds(Record()).Count;

        /// <summary>Gets a seed point (groups 10 and 20 after the 98), in the system of the normal, as ObjectARX's <c>getSeedPointAt</c>.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <returns>The point.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no seed point of the index.</exception>
        public GeoPoint2 GetSeedPointAt(int index)
        {
            List<GeoPoint2> seeds = Seeds(Record());

            if (index < 0 || index >= seeds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The HATCH has " + seeds.Count.ToString(CultureInfo.InvariantCulture) + " seed points, from nought.");
            }

            return seeds[index];
        }

        /// <summary>Adds a seed point after the others, in the system of the normal, as ObjectARX's <c>appendSeedPoint</c>; its count written anew.</summary>
        /// <param name="point">The point.</param>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void AppendSeedPoint(GeoPoint2 point)
        {
            if (!point.IsValid)
            {
                throw new ArgumentException("A seed point has to be of finite numbers: " + point + " is not.", nameof(point));
            }

            Parts record = Record();
            List<KeyValuePair<int, string>> seeds = SeedGroups(record);
            seeds.Add(G(10, point.X));
            seeds.Add(G(20, point.Y));
            WriteSeeds(record, seeds);
        }

        /// <summary>Takes a seed point out, as ObjectARX's <c>removeSeedPointAt</c>; its count written anew.</summary>
        /// <param name="index">The index of the point, from nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the HATCH has no seed point of the index.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        public void RemoveSeedPointAt(int index)
        {
            Parts record = Record();
            List<KeyValuePair<int, string>> seeds = SeedGroups(record);
            int count = seeds.Count / 2;

            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The HATCH has " + count.ToString(CultureInfo.InvariantCulture) + " seed points, from nought.");
            }

            seeds.RemoveRange(2 * index, 2);
            WriteSeeds(record, seeds);
        }

        #endregion

        #region Moving

        /// <summary>
        /// Moves, turns, scales or mirrors the HATCH, as AutoCAD's <c>TransformBy</c>, as ezdxf carries one: each loop taken from
        /// the old plane to the world, moved, and into the plane of the new normal, its arcs and ellipses turned with it; the
        /// elevation that of the old plane's origin there; the pattern's lines scaled, turned and their base points moved, its
        /// scale and angle with them; and, past ezdxf, the seed points moved and the gradient's angle turned, as AutoCAD moves them.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <remarks>
        /// A transformation that scales the plane unlike along its axes, or shears it, makes the arcs of the loops, and the arcs of
        /// a polyline loop, ellipse edges, and each ellipse edge the ellipse it becomes, as ezdxf makes them; the pattern is scaled
        /// by the length its x axis takes, as ezdxf scales it.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when it takes the plane of the HATCH to no plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the HATCH is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            GeoVector3 normal = Normal;
            var ocs = new DxfOcsTransform(normal, transform);

            // Arcs stay arcs only where the plane is scaled alike along its axes and its axes stay square.
            GeoVector3 x = ocs.TransformDirection(new GeoVector3(1.0, 0.0, 0.0));
            GeoVector3 y = ocs.TransformDirection(new GeoVector3(0.0, 1.0, 0.0));
            bool conformal = ocs.ScaleUniform && Math.Abs(x.DotProduct(y)) <= 1E-9 * x.Length * y.Length;
            Parts record = Record();
            double elevation = Num(record.Text(30), 0.0);
            GeoPoint3 lifted = ocs.TransformVertex(new GeoPoint3(0.0, 0.0, elevation));
            var moved = new List<List<KeyValuePair<int, string>>>();

            foreach (List<KeyValuePair<int, string>> loop in Loops(record))
            {
                moved.Add(MovedLoop(conformal ? loop : ForAffine(loop), ocs, elevation, conformal));
            }

            if (record.Part(Kind.Paths) != null)
            {
                record.SetPart(Kind.Paths, JoinLoops(moved));
            }

            foreach (int code in new[] { 10, 20 })
            {
                if (record.Text(code) != null && Num(record.Text(code), 0.0) != 0.0)
                {
                    record.Set(code, "0");
                }
            }

            record.Set(30, Real(lifted.Z));

            foreach (KeyValuePair<int, string> group in NormalGroups(ocs.NewExtrusion))
            {
                record.Set(group.Key, group.Value);
            }

            double factor = ocs.TransformLength(new GeoVector3(1.0, 0.0, 0.0));
            double turn = ocs.TransformDegrees(0.0);
            List<double[]> lines = Lines(record);

            if (lines.Count > 0 || record.Part(Kind.Pattern) != null)
            {
                foreach (double[] line in lines)
                {
                    GeoPoint3 basePoint = ocs.TransformVertex(new GeoPoint3(line[1], line[2], elevation));
                    Turn(line, factor, turn);
                    line[1] = basePoint.X;
                    line[2] = basePoint.Y;
                }

                record.SetPart(Kind.Pattern, LineGroups(lines));
            }

            if (record.Text(41) != null && !IsSolidFill)
            {
                record.Set(41, Real(Num(record.Text(41), 1.0) * factor));
            }

            if (record.Text(52) != null && !IsSolidFill)
            {
                record.Set(52, Real(Num(record.Text(52), 0.0) + turn));
            }

            List<KeyValuePair<int, string>> seeds = SeedGroups(record);

            for (int i = 0; i + 1 < seeds.Count; i += 2)
            {
                GeoPoint3 seed = ocs.TransformVertex(new GeoPoint3(Num(seeds[i].Value, 0.0), Num(seeds[i + 1].Value, 0.0), elevation));
                seeds[i] = G(10, seed.X);
                seeds[i + 1] = G(20, seed.Y);
            }

            if (record.Part(Kind.Seeds) != null)
            {
                record.SetPart(Kind.Seeds, SeedPart(seeds));
            }

            GradientData gradient = Gradient(record);

            if (gradient != null && gradient.Text(460) != null && turn != 0.0)
            {
                double angle = (Num(gradient.Text(460), 0.0) + turn * (Math.PI / 180.0)) % (2.0 * Math.PI);
                gradient.Set(460, Real(angle < 0.0 ? angle + 2.0 * Math.PI : angle));
                record.SetPart(Kind.Gradient, gradient.Groups);
            }

            Write(record);
        }

        #endregion

        #region The record

        /// <summary>The kinds of the parts of the HATCH's own groups.</summary>
        private enum Kind
        {
            Group,
            Paths,
            Pattern,
            Seeds,
            Gradient,
        }

        /// <summary>A part of the own groups: one group, or a block of them (the loops, the pattern's lines, the seeds, the gradient).</summary>
        private sealed class Part
        {
            internal Part(Kind kind, List<KeyValuePair<int, string>> groups)
            {
                Kind = kind;
                Groups = groups;
            }

            internal Kind Kind { get; }

            internal List<KeyValuePair<int, string>> Groups { get; set; }

            /// <summary>Where AutoCAD writes the part among the others; -1 for a group it writes nowhere of its own.</summary>
            internal int Rank
            {
                get
                {
                    switch (Kind)
                    {
                        case Kind.Paths: return Array.IndexOf(Ranked, 91);
                        case Kind.Pattern: return Array.IndexOf(Ranked, 78);
                        case Kind.Seeds: return Array.IndexOf(Ranked, 98);
                        case Kind.Gradient: return Array.IndexOf(Ranked, 450);
                        default: return Array.IndexOf(Ranked, Groups[0].Key);
                    }
                }
            }
        }

        /// <summary>
        /// The own groups of the HATCH in parts, as ezdxf reads them: the loops from the first 91 on, as long as their codes run;
        /// after them the gradient from its 450 to the end, the pattern's lines from its 78 as long as theirs run, and the seed
        /// points from the 98; every other group a part of its own, in its place.
        /// </summary>
        private sealed class Parts
        {
            private readonly List<Part> _parts = new List<Part>();

            internal Parts(List<KeyValuePair<int, string>> run)
            {
                bool paths = false;
                bool pattern = false;
                bool seeds = false;

                for (int i = 0; i < run.Count; i++)
                {
                    int code = run[i].Key;

                    if (code == 91 && !paths)
                    {
                        paths = true;
                        i = Block(run, i, Kind.Paths, PathCodes);
                    }
                    else if (code == 450 && paths)
                    {
                        _parts.Add(new Part(Kind.Gradient, run.GetRange(i, run.Count - i)));
                        break;
                    }
                    else if (code == 78 && paths && !pattern)
                    {
                        pattern = true;
                        i = Block(run, i, Kind.Pattern, LineCodes);
                    }
                    else if (code == 98 && paths && !seeds)
                    {
                        seeds = true;
                        i = Block(run, i, Kind.Seeds, SeedCodes);
                    }
                    else
                    {
                        _parts.Add(new Part(Kind.Group, new List<KeyValuePair<int, string>> { run[i] }));
                    }
                }
            }

            /// <summary>The text of the first group of a code that is a part of its own; null where there is none.</summary>
            internal string Text(int code)
            {
                foreach (Part part in _parts)
                {
                    if (part.Kind == Kind.Group && part.Groups[0].Key == code)
                    {
                        return part.Groups[0].Value;
                    }
                }

                return null;
            }

            /// <summary>Sets the first group of a code that is a part of its own, or puts one where AutoCAD writes it; null takes every one out.</summary>
            internal void Set(int code, string value)
            {
                if (value == null)
                {
                    _parts.RemoveAll(p => p.Kind == Kind.Group && p.Groups[0].Key == code);
                    return;
                }

                foreach (Part part in _parts)
                {
                    if (part.Kind == Kind.Group && part.Groups[0].Key == code)
                    {
                        part.Groups[0] = G(code, value);
                        return;
                    }
                }

                Put(new Part(Kind.Group, new List<KeyValuePair<int, string>> { G(code, value) }));
            }

            /// <summary>The block of a kind; null where there is none.</summary>
            internal List<KeyValuePair<int, string>> Part(Kind kind) => _parts.Find(p => p.Kind == kind)?.Groups;

            /// <summary>Sets the block of a kind, or puts one where AutoCAD writes it; null takes it out.</summary>
            internal void SetPart(Kind kind, List<KeyValuePair<int, string>> groups)
            {
                int at = _parts.FindIndex(p => p.Kind == kind);

                if (groups == null)
                {
                    if (at >= 0)
                    {
                        _parts.RemoveAt(at);
                    }

                    return;
                }

                if (at >= 0)
                {
                    _parts[at].Groups = groups;
                    return;
                }

                Put(new Part(kind, groups));
            }

            /// <summary>The groups, one run.</summary>
            internal List<KeyValuePair<int, string>> Groups()
            {
                var run = new List<KeyValuePair<int, string>>();

                foreach (Part part in _parts)
                {
                    run.AddRange(part.Groups);
                }

                return run;
            }

            /// <summary>Puts a part after the last of those AutoCAD writes before it.</summary>
            private void Put(Part part)
            {
                int rank = part.Rank;
                int at = 0;

                for (int i = 0; i < _parts.Count; i++)
                {
                    int other = _parts[i].Rank;

                    if (other >= 0 && other < rank)
                    {
                        at = i + 1;
                    }
                }

                _parts.Insert(at, part);
            }

            /// <summary>A block from its head on, as long as its codes run; the index of its last group.</summary>
            private int Block(List<KeyValuePair<int, string>> run, int head, Kind kind, HashSet<int> codes)
            {
                int end = head + 1;

                while (end < run.Count && codes.Contains(run[end].Key))
                {
                    end++;
                }

                _parts.Add(new Part(kind, run.GetRange(head, end - head)));
                return end - 1;
            }
        }

        /// <summary>The gradient data of the HATCH: its groups from the 450 on.</summary>
        private sealed class GradientData
        {
            internal GradientData(List<KeyValuePair<int, string>> groups)
            {
                Groups = groups;
            }

            internal List<KeyValuePair<int, string>> Groups { get; }

            internal int Kind => Whole(Text(450), 0);

            internal string Name => Text(470)?.Trim();

            /// <summary>The data AutoCAD writes for a new gradient: LINEAR, at nought, centred, of two colours, blue to pale yellow.</summary>
            internal static GradientData Fresh() => new GradientData(new List<KeyValuePair<int, string>>
            {
                G(450, "1"), G(451, "0"), G(460, "0"), G(461, "0"), G(452, "0"), G(462, "0"), G(453, "2"),
                G(463, "0"), G(421, "255"), G(463, "1"), G(421, "16777113"), G(470, "LINEAR"),
            });

            internal string Text(int code) => DxfLeaderGroups.Text(Groups, code);

            /// <summary>Sets the first group of a code, or puts one before the count of the colours (or the name for 470).</summary>
            internal void Set(int code, string value)
            {
                int at = Groups.FindIndex(g => g.Key == code);

                if (at >= 0)
                {
                    Groups[at] = G(code, value);
                    return;
                }

                int before = code == 470 ? Groups.Count : Groups.FindIndex(g => g.Key == 453 || g.Key == 463 || g.Key == 421 || g.Key == 470);
                Groups.Insert(before < 0 ? Groups.Count : before, G(code, value));
            }

            /// <summary>The colours, each its 463, 63 and 421.</summary>
            internal List<DxfGradientColor> Colors()
            {
                var colours = new List<DxfGradientColor>();
                double value = 0.0;
                int? index = null;

                foreach (KeyValuePair<int, string> group in Groups)
                {
                    switch (group.Key)
                    {
                        case 463:
                            value = Num(group.Value, 0.0);
                            index = null;
                            break;
                        case 63:
                            index = Whole(group.Value, 0);
                            break;
                        case 421:
                            DxfColor colour = index >= 1 && index <= 255 ? DxfColor.OfIndex(index.Value) : DxfColor.OfRgb(Whole(group.Value, 0), null);
                            colours.Add(new DxfGradientColor(colour, (float)value));
                            index = null;
                            break;
                    }
                }

                return colours;
            }

            /// <summary>The groups of colours, as AutoCAD writes them: the count, then each one's value, its index where it is one of the index, and its red, green and blue.</summary>
            internal static List<KeyValuePair<int, string>> ColorGroups(DxfGradientColor[] colours, string parameter)
            {
                var groups = new List<KeyValuePair<int, string>> { G(453, colours.Length) };

                foreach (DxfGradientColor colour in colours)
                {
                    DxfColor c = colour.Color ?? throw new ArgumentException("A colour of a gradient is null.", parameter);

                    if (!c.IsByColor && !c.IsByAci)
                    {
                        throw new ArgumentException("A colour of a gradient is a true colour or one of AutoCAD's index: " + c + " is neither.", parameter);
                    }

                    groups.Add(G(463, (double)colour.Value));

                    if (c.IsByAci)
                    {
                        groups.Add(G(63, c.ColorIndex));
                    }

                    groups.Add(G(421, c.ColorValue & 0xFFFFFF));
                }

                return groups;
            }

            /// <summary>Puts the groups of the colours in place of those the data has; false where they are the same, colour for colour.</summary>
            internal bool SetColors(List<KeyValuePair<int, string>> colours)
            {
                int first = Groups.FindIndex(g => g.Key == 453);
                int last = first;

                for (int i = Math.Max(first, 0); i < Groups.Count; i++)
                {
                    if (Groups[i].Key == 453 || Groups[i].Key == 463 || Groups[i].Key == 63 || Groups[i].Key == 421)
                    {
                        first = first < 0 ? i : first;
                        last = i;
                    }
                }

                List<DxfGradientColor> now = Colors();
                List<DxfGradientColor> wanted = new GradientData(colours).Colors();
                bool same = now.Count == wanted.Count;

                for (int i = 0; same && i < now.Count; i++)
                {
                    same = SameColour(now[i], wanted[i]);
                }

                if (same)
                {
                    return false;
                }

                if (first < 0)
                {
                    int name = Groups.FindIndex(g => g.Key == 470);
                    Groups.InsertRange(name < 0 ? Groups.Count : name, colours);
                    return true;
                }

                Groups.RemoveRange(first, last - first + 1);
                Groups.InsertRange(first, colours);
                return true;
            }

            private static bool SameColour(DxfGradientColor a, DxfGradientColor b)
                => a.Value == b.Value && a.Color.ColorValue == b.Color.ColorValue && a.Color.ColorMethod == b.Color.ColorMethod && a.Color.ColorIndex == b.Color.ColorIndex;
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Array.AsReadOnly(new[] { 100 });

        /// <summary>The own groups of the HATCH in parts.</summary>
        private Parts Record() => new Parts(Run(GetOwnGroups(0)));

        /// <summary>Writes the own groups of the HATCH in one writing; nothing where they are as they were.</summary>
        private void Write(Parts record)
        {
            List<KeyValuePair<int, string>> run = record.Groups();
            ReplaceGroups(0, Codes(GetOwnGroups(0), run), run.ToArray());
        }

        /// <summary>The gradient data; null where the HATCH has none.</summary>
        private static GradientData Gradient(Parts record)
        {
            List<KeyValuePair<int, string>> groups = record.Part(Kind.Gradient);
            return groups == null ? null : new GradientData(new List<KeyValuePair<int, string>>(groups));
        }

        private void SetGradientNumber(int code, double value, double now, string what)
        {
            Guard.Finite(value, nameof(value), "The gradient's " + what + " has to be a finite number.");

            if (Same(value, now))
            {
                return;
            }

            Parts record = Record();
            GradientData gradient = Gradient(record) ?? throw NoGradient();
            gradient.Set(code, Real(value));
            record.SetPart(Kind.Gradient, gradient.Groups);
            Write(record);
        }

        private static InvalidOperationException NoGradient()
            => new InvalidOperationException("The HATCH has no gradient data: give it a gradient with SetGradient first.");

        /// <summary>The record of a HATCH filled with a pattern, to set a part of the pattern of.</summary>
        /// <exception cref="InvalidOperationException">Thrown for a solid fill, which has no pattern.</exception>
        private Parts Patterned(string what)
        {
            if (IsSolidFill)
            {
                throw new InvalidOperationException("A solid fill has no pattern to give a " + what + ": give it one with SetHatchPattern first.");
            }

            return Record();
        }

        /// <summary>The lines of the pattern, each its angle in degrees, base point, offset and dashes.</summary>
        private static List<double[]> Lines(Parts record)
        {
            var lines = new List<double[]>();
            List<KeyValuePair<int, string>> groups = record.Part(Kind.Pattern);

            if (groups == null)
            {
                return lines;
            }

            List<double> line = null;

            foreach (KeyValuePair<int, string> group in groups)
            {
                switch (group.Key)
                {
                    case 53:
                        line = new List<double> { Num(group.Value, 0.0), 0.0, 0.0, 0.0, 0.0 };
                        lines.Add(null);
                        break;
                    case 43:
                    case 44:
                    case 45:
                    case 46:
                        if (line != null)
                        {
                            line[group.Key - 42] = Num(group.Value, 0.0);
                        }

                        break;
                    case 49:
                        line?.Add(Num(group.Value, 0.0));
                        break;
                }

                if (line != null)
                {
                    lines[lines.Count - 1] = line.ToArray();
                }
            }

            return lines;
        }

        /// <summary>The groups of the pattern's lines, as AutoCAD writes them: their count, then each one's angle, base point, offset and dashes.</summary>
        private static List<KeyValuePair<int, string>> LineGroups(List<double[]> lines)
        {
            var groups = new List<KeyValuePair<int, string>> { G(78, lines.Count) };

            foreach (double[] line in lines)
            {
                groups.Add(G(53, line[0]));
                groups.Add(G(43, line[1]));
                groups.Add(G(44, line[2]));
                groups.Add(G(45, line[3]));
                groups.Add(G(46, line[4]));
                groups.Add(G(79, line.Length - 5));

                for (int i = 5; i < line.Length; i++)
                {
                    groups.Add(G(49, line[i]));
                }
            }

            return groups;
        }

        /// <summary>The lines of a pattern made by the user: one family at the angle, the spacing apart, and one square to it where it is double.</summary>
        private static List<KeyValuePair<int, string>> UserLines(double degrees, double spacing, bool twice)
        {
            var lines = new List<double[]>();

            foreach (double angle in twice ? new[] { degrees, degrees + 90.0 } : new[] { degrees })
            {
                double radians = angle * (Math.PI / 180.0);
                lines.Add(new[] { angle, 0.0, 0.0, -spacing * Math.Sin(radians), spacing * Math.Cos(radians) });
            }

            return LineGroups(lines);
        }

        /// <summary>Scales the pattern's lines by a factor and turns them by degrees, about the origin of the plane, as ezdxf's <c>scale_pattern</c>.</summary>
        private static void ScaleLines(Parts record, double factor, double degrees)
        {
            List<double[]> lines = Lines(record);

            if (lines.Count == 0)
            {
                return;
            }

            foreach (double[] line in lines)
            {
                Turn(line, factor, degrees);
                Rotate(line, 1, degrees);
                line[1] *= factor;
                line[2] *= factor;
            }

            record.SetPart(Kind.Pattern, LineGroups(lines));
        }

        /// <summary>Turns a line's angle and offset by degrees and scales its offset and dashes by a factor; its base point left for the caller.</summary>
        private static void Turn(double[] line, double factor, double degrees)
        {
            if (degrees != 0.0)
            {
                Rotate(line, 3, degrees);
                double angle = (line[0] + degrees) % 360.0;
                line[0] = angle < 0.0 ? angle + 360.0 : angle;
            }

            for (int i = 3; i < line.Length; i++)
            {
                line[i] *= factor;
            }
        }

        /// <summary>Turns the point of a line at an index (its base point at 1, its offset at 3) by degrees about the origin.</summary>
        private static void Rotate(double[] line, int at, double degrees)
        {
            if (degrees == 0.0)
            {
                return;
            }

            double radians = degrees * (Math.PI / 180.0);
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            double x = line[at];
            double y = line[at + 1];
            line[at] = x * cos - y * sin;
            line[at + 1] = x * sin + y * cos;
        }

        /// <summary>The seed points, in the system of the normal.</summary>
        private static List<GeoPoint2> Seeds(Parts record)
        {
            List<KeyValuePair<int, string>> groups = SeedGroups(record);
            var seeds = new List<GeoPoint2>(groups.Count / 2);

            for (int i = 0; i + 1 < groups.Count; i += 2)
            {
                seeds.Add(new GeoPoint2(Num(groups[i].Value, 0.0), Num(groups[i + 1].Value, 0.0)));
            }

            return seeds;
        }

        /// <summary>The groups of the seed points, a 10 and a 20 for each, as the record has them: a copy to change.</summary>
        private static List<KeyValuePair<int, string>> SeedGroups(Parts record)
        {
            List<KeyValuePair<int, string>> part = record.Part(Kind.Seeds);
            var seeds = new List<KeyValuePair<int, string>>();
            KeyValuePair<int, string>? x = null;

            foreach (KeyValuePair<int, string> group in part ?? new List<KeyValuePair<int, string>>())
            {
                if (group.Key == 10)
                {
                    x = group;
                }
                else if (group.Key == 20 && x.HasValue)
                {
                    seeds.Add(x.Value);
                    seeds.Add(group);
                    x = null;
                }
            }

            return seeds;
        }

        private static List<KeyValuePair<int, string>> SeedPart(List<KeyValuePair<int, string>> seeds)
        {
            var part = new List<KeyValuePair<int, string>> { G(98, seeds.Count / 2) };
            part.AddRange(seeds);
            return part;
        }

        private void WriteSeeds(Parts record, List<KeyValuePair<int, string>> seeds)
        {
            record.SetPart(Kind.Seeds, SeedPart(seeds));
            Write(record);
        }

        #endregion
    }
}
