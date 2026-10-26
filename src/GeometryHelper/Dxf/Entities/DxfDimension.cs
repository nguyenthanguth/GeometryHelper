using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A dimension of a DXF file, as AutoCAD's <c>Dimension</c>: the points it measures between, the text it writes, the style
    /// it is drawn in and the variables of that style it overrides; each kind a class of its own, a
    /// <see cref="DxfRotatedDimension"/> for a linear one, a <see cref="DxfRadialDimension"/> for a radius.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The points are in world coordinates, as AutoCAD gives them, though the file writes some in the system of the dimension's
    /// <see cref="Normal"/> (the text's place, group 11, and the place of its block, 12). The rotated dimension of 100 from
    /// (0, 0, 0) to (100, 0, 0), its dimension line 20 above, has <see cref="DxfRotatedDimension.XLine1Point"/> (0, 0, 0),
    /// <see cref="DxfRotatedDimension.XLine2Point"/> (100, 0, 0), <see cref="DxfRotatedDimension.DimLinePoint"/> on the line
    /// 20 above and a <see cref="Measurement"/> of 100, worked out from the points as AutoCAD works it out.
    /// </para>
    /// <para>
    /// What a dimension is drawn with, its lines, arrowheads and text, is the anonymous block <see cref="DimBlock"/>
    /// (<c>*D7</c>) that AutoCAD drew for it, placed at <see cref="DimBlockPosition"/>. The library does not draw a dimension:
    /// a point, a text or a variable set is written into the record, and the block is left as it was drawn, to be drawn anew by
    /// AutoCAD when it next draws the dimension, as its <c>RecomputeDimensionBlock</c> would; until then AutoCAD shows the old
    /// picture. <see cref="DxfEntity.TransformBy(GeoTransform3)"/> moves the points as ezdxf's <c>transform()</c> moves them, and
    /// the picture with them where AutoCAD gives the block to this dimension alone (flag 32 of group 70) and places it at no
    /// point of its own (no group 12): the block's entities moved by the same transformation, so that the dimension is drawn
    /// where it now stands.
    /// </para>
    /// <para>
    /// A variable of the style, as <see cref="Dimasz"/>, reads as the dimension draws with it: its own where it overrides the
    /// style (its extended data of ACAD, the list DSTYLE of a 1070 naming the variable's group and its value), else its
    /// style's, else AutoCAD's own. Set, it is written as an override, as AutoCAD's DIMOVERRIDE writes one; the style is left
    /// as it is. A block, linetype or text style set on a dimension of a drawing is one of that drawing; one set on a dimension
    /// in no drawing yet is held as the record it is, and taken to the record of its name in the drawing it is added to, as
    /// AutoCAD's deep clone maps one, the dimension refused where that drawing has none of that name. A copy added to another
    /// drawing keeps its overrides that point at a record, those it read and those set on it, each taken to the record of the
    /// same name there, which the drawing is given where it has none, as AutoCAD's deep clone between drawings does. An override
    /// of a variable a drawing of an older version has not is left out when the dimension is added to it, as AutoCAD's SAVEAS to
    /// that version leaves it out: DIMLTYPE and DIMFXLEN, which R2007 brought, in a drawing of R2000.
    /// </para>
    /// <para>
    /// A dimension made in code, <c>new DxfRotatedDimension(0, p1, p2, onLine, "", null)</c>, is drawn by no block until it is
    /// given one: <see cref="DimBlock"/>, a block of the drawing it is added to, is needed before
    /// <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> takes it. A copy added to another drawing brings a copy of its
    /// block where AutoCAD names the block itself (<c>*D1</c>), and needs one of the same name there where it does not. A copy
    /// added to the drawing it came from shares its original's block, and loses flag 32, so that moving the one leaves the
    /// other's picture where it is; one made in code is written without it.
    /// </para>
    /// </remarks>
    public abstract class DxfDimension : DxfEntity
    {
        // The groups of AcDbDimension in the order ezdxf and AutoCAD write them.
        private static readonly IReadOnlyList<int> BaseOrder = Array.AsReadOnly(new[] { 280, 2, 3, 10, 20, 30, 11, 21, 31, 12, 22, 32, 70, 71, 72, 41, 42, 73, 74, 75, 1, 52, 53, 51, 210, 220, 230 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private const string Application = "ACAD";

        private const string Overrides = "DSTYLE";

        // The blocks, linetypes and text styles set while the dimension is in no drawing, taken to their namesakes on appending.
        private Dictionary<DxfDimVariable, DxfDBObject> _held;

        /// <summary>The object of a record of a file.</summary>
        private protected DxfDimension(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// A dimension made in code: its type, the marker AcDbDimension with its style's name, definition point, kind (without
        /// 32: the block it is given may be another's) and text, then the groups of its kind, which begin with its markers.
        /// </summary>
        private protected DxfDimension(string dxfName, int kind, GeoPoint3 definition, string dimensionText, DxfDimStyleTableRecord dimensionStyle, IEnumerable<DxfTypedValue> own)
            : base(dxfName, Groups(kind, definition, dimensionText, dimensionStyle, own))
        {
        }

        #region What the subclass gives

        /// <summary>The codes of the groups of the kind's own subclasses, in the order AutoCAD writes them.</summary>
        private protected abstract IReadOnlyList<int> SubOrder { get; }

        /// <summary>
        /// The codes of the kind's own points (after its subclass marker) that the file writes in world coordinates, moved where
        /// the record has them, as ezdxf moves them: 13, 14 and 15 of a DIMENSION, which a file of R12 writes for every kind.
        /// </summary>
        private protected virtual IReadOnlyList<int> WorldPoints => DimensionWorldPoints;

        /// <summary>The codes of the kind's own points that the file writes in the system of the normal: the arc point (16) of a DIMENSION.</summary>
        private protected virtual IReadOnlyList<int> PlanePoints => DimensionPlanePoints;

        private static readonly IReadOnlyList<int> DimensionWorldPoints = Array.AsReadOnly(new[] { 13, 14, 15 });

        private static readonly IReadOnlyList<int> DimensionPlanePoints = Array.AsReadOnly(new[] { 16 });

        #endregion

        #region The points and the text

        /// <summary>
        /// Gets the measurement of the dimension, as AutoCAD's <c>Measurement</c>: worked out from its points, as AutoCAD works
        /// it out, a length or, for an angular dimension, an angle in radians; the 100 between (0, 0, 0) and (100, 0, 0) of a
        /// rotated dimension along x. The measurement AutoCAD wrote with the block it drew (group 42) agrees with it until a point
        /// is moved.
        /// </summary>
        public abstract double Measurement { get; }

        /// <summary>
        /// Gets or sets the text the dimension writes (group 1), as AutoCAD's <c>DimensionText</c>: empty or <c>&lt;&gt;</c> for the
        /// measurement, <c>&lt;&gt; TYP</c> for the measurement and TYP after it, a single space for none; empty where the record
        /// gives none.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string DimensionText
        {
            get => Own().Text(1, Own().Base) ?? string.Empty;

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (!string.Equals(value, DimensionText, StringComparison.Ordinal))
                {
                    Write(g => g.Set(1, value, g.Base, BaseOrder));
                }
            }
        }

        /// <summary>
        /// Gets or sets the middle of the text (group 11, written in the system of the normal), as AutoCAD's <c>TextPosition</c>:
        /// in world coordinates; the origin where the record gives none.
        /// </summary>
        /// <value>Set, the text moved there, the dimension then no longer at its default place (flag 128 of group 70 set), as AutoCAD
        /// sets it; the place the text has changes nothing.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoPoint3 TextPosition
        {
            get => OcsToWorld(Normal, Own().Point(11, Own().Base, Origin));

            set
            {
                CheckPoint(value);

                if (Same(value, TextPosition))
                {
                    return;
                }

                GeoPoint3 ocs = WorldToOcs(Normal, value);
                Write(g =>
                {
                    g.SetPoint(11, ocs, g.Base, BaseOrder, Origin);
                    SetFlag(g, 128, true);
                });
            }
        }

        /// <summary>
        /// Gets or sets whether the text stands where the style puts it (flag 128 of group 70 not set), as AutoCAD's
        /// <c>UsingDefaultTextPosition</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool UsingDefaultTextPosition
        {
            get => (Kind & 128) == 0;

            set
            {
                if (value != UsingDefaultTextPosition)
                {
                    Write(g => SetFlag(g, 128, !value));
                }
            }
        }

        /// <summary>
        /// Gets or sets the turn of the text from the direction of the dimension line (group 53, in degrees), as AutoCAD's
        /// <c>TextRotation</c>: in radians; nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double TextRotation
        {
            get => Own().Real(53, Own().Base, 0.0) * (Math.PI / 180.0);
            set => SetAngle(53, value, TextRotation, false);
        }

        /// <summary>
        /// Gets or sets the direction AutoCAD takes as level for the dimension (group 51, in degrees), as AutoCAD's
        /// <c>HorizontalRotation</c>: in radians, the negative of the angle from the x of the normal's system to that of the user's;
        /// nought where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double HorizontalRotation
        {
            get => Own().Real(51, Own().Base, 0.0) * (Math.PI / 180.0);
            set => SetAngle(51, value, HorizontalRotation, false);
        }

        /// <summary>
        /// Gets or sets the normal of the plane the dimension lies in (groups 210, 220, 230), as AutoCAD's <c>Normal</c>; of unit
        /// length, (0, 0, 1) where the record gives none.
        /// </summary>
        /// <value>Set, a direction of some length, written made of unit length; the points the file writes in the system of the
        /// normal (the text's, the block's, an arc point) stay where they are in the world, written anew in the new system, and so
        /// do the directions its angles give (50, 51 and 53), each as the angle of the same direction in the new system.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();

            set
            {
                KeyValuePair<int, string>[] normal = NormalGroups(value);

                if (normal.Length == 0)
                {
                    return;
                }

                GeoVector3 now = Normal;
                GeoVector3 unit = DxfOcsTransform.Normalized(value);
                var turn = new DxfOcsTransform(now, unit, GeoTransform3.Identity);
                Write(g =>
                {
                    foreach (KeyValuePair<int, bool> point in OcsPoints(g))
                    {
                        DxfDimGroups.Range part = point.Value ? g.Sub : g.Base;
                        g.SetPoint(point.Key, WorldToOcs(unit, OcsToWorld(now, g.Point(point.Key, part, Origin))), part, point.Value ? SubOrder : BaseOrder, Origin);
                    }

                    TurnAngles(g, turn, false);

                    foreach (KeyValuePair<int, string> group in normal)
                    {
                        g.Set(group.Key, group.Value, g.Base, BaseOrder);
                    }
                });
            }
        }

        /// <summary>
        /// Gets or sets how far the dimension stands along its normal, as AutoCAD's <c>Elevation</c>: the third coordinate of its
        /// text's place (group 31) in the system of the normal, where ezdxf takes it from too.
        /// </summary>
        /// <value>Set, the points the file writes in the system of the normal (the text's, the block's, an arc point) given that
        /// third coordinate.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Elevation
        {
            get => Own().Point(11, Own().Base, Origin).Z;

            set
            {
                if (Same(value, Elevation))
                {
                    return;
                }

                Guard.Finite(value, nameof(value), "An elevation has to be a finite number.");
                Write(g =>
                {
                    foreach (KeyValuePair<int, bool> point in OcsPoints(g))
                    {
                        DxfDimGroups.Range part = point.Value ? g.Sub : g.Base;
                        GeoPoint3 now = g.Point(point.Key, part, Origin);

                        if (g.Find(point.Key, part) >= 0 || point.Key == 11)
                        {
                            g.SetPoint(point.Key, new GeoPoint3(now.X, now.Y, value), part, point.Value ? SubOrder : BaseOrder, Origin);
                        }
                    }
                });
            }
        }

        /// <summary>
        /// Gets or sets where the text is held by its point, as AutoCAD's <c>TextAttachment</c> (group 71, from R2000 on);
        /// <see cref="DxfAttachmentPoint.MiddleCenter"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the attachment set is not one of 1 to 9.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfAttachmentPoint TextAttachment
        {
            get => (DxfAttachmentPoint)Own().Whole(71, Own().Base, 5);

            set
            {
                // The attachment read changes nothing, whatever the file holds, as a 71 of 0 or 10.
                if (value == TextAttachment)
                {
                    return;
                }

                if (value < DxfAttachmentPoint.TopLeft || value > DxfAttachmentPoint.BottomRight)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A dimension's text is held by one of its nine points, 1 to 9.");
                }

                SetWhole(71, (int)value, (int)TextAttachment);
            }
        }

        /// <summary>
        /// Gets or sets how the spacing of the text's lines is taken, as AutoCAD's <c>TextLineSpacingStyle</c> (group 72, from
        /// R2000 on); <see cref="DxfLineSpacingStyle.AtLeast"/> where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the style set is not one of <see cref="DxfLineSpacingStyle"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfLineSpacingStyle TextLineSpacingStyle
        {
            get => Own().Whole(72, Own().Base, 1) == 2 ? DxfLineSpacingStyle.Exactly : DxfLineSpacingStyle.AtLeast;

            set
            {
                if (value != DxfLineSpacingStyle.AtLeast && value != DxfLineSpacingStyle.Exactly)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The spacing is at least or exactly.");
                }

                SetWhole(72, (int)value, (int)TextLineSpacingStyle);
            }
        }

        /// <summary>
        /// Gets or sets the spacing of the text's lines for AutoCAD's own, as AutoCAD's <c>TextLineSpacingFactor</c> (group 41 of
        /// AcDbDimension, from R2000 on): 1.5 spaces them half again as far; 1 where the record gives none.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the factor set is not from 0.25 to 4, as AutoCAD takes it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public double TextLineSpacingFactor
        {
            get => Own().Real(41, Own().Base, 1.0);

            set
            {
                if (Same(value, TextLineSpacingFactor))
                {
                    return;
                }

                Guard.Finite(value, nameof(value), "A spacing has to be a finite number.");

                if (value < 0.25 || value > 4.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A dimension's lines are spaced from 0.25 to 4 times AutoCAD's own spacing.");
                }

                Item.File.Requires(1015, "line spacing of a dimension's text", "R2000 (AC1015)");
                Write(g => g.Set(41, Numbers.Exact(value + 0.0), g.Base, BaseOrder));
            }
        }

        /// <summary>
        /// Gets or sets the anonymous block that draws the dimension (group 2), as AutoCAD's <c>DimBlockId</c> gives its id:
        /// <c>*D7</c>, drawn by AutoCAD; null where the record names none, as a dimensional constraint does, or the drawing has no
        /// block of its name. A dimension in no drawing yet, copied from one, gives the block of that drawing.
        /// </summary>
        /// <value>Set, a block of the drawing (of any drawing for a dimension made in code, whose drawing's block of the same name
        /// it then names), written as its name; the block is not drawn anew.</value>
        /// <exception cref="ArgumentNullException">Thrown when the block set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, in no drawing, or of another drawing than the
        /// dimension's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfBlockTableRecord DimBlock
        {
            get
            {
                string name = DxfReader.NameOf(Own().Text(2, Own().Base) ?? string.Empty);
                DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;
                return name.Length == 0 || database == null || !database.BlockTable.Has(name) ? null : database.BlockTable[name];
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.File == null || value.IsLayout || (IsResident && value.File != Item.File))
                {
                    throw new ArgumentException("A dimension is drawn by a block of its own drawing, not a layout: " + value.Name + " is not one.", nameof(value));
                }

                if (!string.Equals(value.Name, Own().Text(2, Own().Base), StringComparison.Ordinal))
                {
                    Write(g => g.Set(2, value.Name, g.Base, BaseOrder));
                }
            }
        }

        /// <summary>
        /// Gets the point the block that draws the dimension is placed at (group 12, written in the system of the normal), as
        /// AutoCAD's <c>DimBlockPosition</c>: in world coordinates; the origin where the record gives none, the block then drawn
        /// where it stands.
        /// </summary>
        public GeoPoint3 DimBlockPosition
        {
            get
            {
                DxfDimGroups g = Own();
                return OcsToWorld(Normal, g.Find(12, g.Base) >= 0 ? g.Point(12, g.Base, Origin) : g.Point(12, g.Sub, Origin));
            }
        }

        /// <summary>
        /// Gets or sets the name of the style the dimension is drawn in (group 3), as AutoCAD's <c>DimensionStyleName</c>;
        /// <c>Standard</c> where the record gives none.
        /// </summary>
        /// <value>Set, the name of a style of the drawing's <see cref="DxfDatabase.DimStyleTable"/>, as the table spells it.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no style of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string DimensionStyleName
        {
            get
            {
                string name = DxfReader.NameOf(Own().Text(3, Own().Base) ?? string.Empty);
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

                DxfDatabase database = IsResident ? Item.File.Database : null;

                if (database != null)
                {
                    if (!database.DimStyleTable.Has(value))
                    {
                        throw new ArgumentException("The drawing has no dimension style named " + value + ".", nameof(value));
                    }

                    value = database.DimStyleTable[value].Name;
                }

                Write(g => g.Set(3, value, g.Base, BaseOrder));
            }
        }

        /// <summary>
        /// Gets or sets the style the dimension is drawn in, as AutoCAD's <c>DimensionStyle</c> gives its id: the record of the
        /// drawing's <see cref="DxfDatabase.DimStyleTable"/> named by <see cref="DimensionStyleName"/>; null where the drawing has
        /// none of that name, or the dimension is in no drawing.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the style set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the style set is of another drawing than the dimension's, or in none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfDimStyleTableRecord DimensionStyle
        {
            get
            {
                DxfDatabase database = IsResident ? Item.File.Database : null;
                return database != null && database.DimStyleTable.Has(DimensionStyleName) ? database.DimStyleTable[DimensionStyleName] : null;
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Item == null || (IsResident && value.Item.File != Item.File))
                {
                    throw new ArgumentException("A dimension is drawn in a style of its own drawing.", nameof(value));
                }

                DimensionStyleName = value.Name;
            }
        }

        #endregion

        #region The text's parts and zeros

        /// <summary>
        /// Gets or sets what is written before the measurement, as AutoCAD's <c>Prefix</c>: of <see cref="Dimpost"/>, what comes
        /// before its <c>&lt;&gt;</c>, Ø of <c>Ø&lt;&gt; mm</c>; empty where it has none.
        /// </summary>
        /// <value>Set, written into the override of <see cref="Dimpost"/>, its suffix kept.</value>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string Prefix
        {
            get => Before(Dimpost, "<>");

            set
            {
                if (!string.Equals(value, Prefix, StringComparison.Ordinal))
                {
                    Dimpost = Joined(value, "<>", Suffix);
                }
            }
        }

        /// <summary>
        /// Gets or sets what is written after the measurement, as AutoCAD's <c>Suffix</c>: of <see cref="Dimpost"/>, what comes
        /// after its <c>&lt;&gt;</c>, " mm" of <c>Ø&lt;&gt; mm</c>, or the whole of one that has none.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string Suffix
        {
            get => After(Dimpost, "<>");

            set
            {
                if (!string.Equals(value, Suffix, StringComparison.Ordinal))
                {
                    Dimpost = Joined(Prefix, "<>", value);
                }
            }
        }

        /// <summary>Gets or sets what is written before the measurement in alternate units, as AutoCAD's <c>AlternatePrefix</c>: of <see cref="Dimapost"/>, what comes before its <c>[]</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string AlternatePrefix
        {
            get => Before(Dimapost, "[]");

            set
            {
                if (!string.Equals(value, AlternatePrefix, StringComparison.Ordinal))
                {
                    Dimapost = Joined(value, "[]", AlternateSuffix);
                }
            }
        }

        /// <summary>Gets or sets what is written after the measurement in alternate units, as AutoCAD's <c>AlternateSuffix</c>: of <see cref="Dimapost"/>, what comes after its <c>[]</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string AlternateSuffix
        {
            get => After(Dimapost, "[]");

            set
            {
                if (!string.Equals(value, AlternateSuffix, StringComparison.Ordinal))
                {
                    Dimapost = Joined(AlternatePrefix, "[]", value);
                }
            }
        }

        /// <summary>Gets or sets whether leading zeros of decimal lengths are left out (flag 4 of <see cref="Dimzin"/>), as AutoCAD's <c>SuppressLeadingZeros</c>: .5 for 0.5.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool SuppressLeadingZeros { get => (Dimzin & 4) != 0; set => Dimzin = Bit(Dimzin, 4, value); }

        /// <summary>Gets or sets whether trailing zeros of decimal lengths are left out (flag 8 of <see cref="Dimzin"/>), as AutoCAD's <c>SuppressTrailingZeros</c>: 12.5 for 12.50.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool SuppressTrailingZeros { get => (Dimzin & 8) != 0; set => Dimzin = Bit(Dimzin, 8, value); }

        /// <summary>Gets or sets whether nought feet are left out (<see cref="Dimzin"/> of 0 or 3), as AutoCAD's <c>SuppressZeroFeet</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool SuppressZeroFeet { get => Feet(Dimzin); set => Dimzin = FeetAndInches(Dimzin, value, Inches(Dimzin)); }

        /// <summary>Gets or sets whether nought inches are left out (<see cref="Dimzin"/> of 0 or 2), as AutoCAD's <c>SuppressZeroInches</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool SuppressZeroInches { get => Inches(Dimzin); set => Dimzin = FeetAndInches(Dimzin, Feet(Dimzin), value); }

        /// <summary>Gets or sets whether leading zeros of alternate units are left out (flag 4 of <see cref="Dimaltz"/>), as AutoCAD's <c>AltSuppressLeadingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltSuppressLeadingZeros { get => (Dimaltz & 4) != 0; set => Dimaltz = Bit(Dimaltz, 4, value); }

        /// <summary>Gets or sets whether trailing zeros of alternate units are left out (flag 8 of <see cref="Dimaltz"/>), as AutoCAD's <c>AltSuppressTrailingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltSuppressTrailingZeros { get => (Dimaltz & 8) != 0; set => Dimaltz = Bit(Dimaltz, 8, value); }

        /// <summary>Gets or sets whether nought feet of alternate units are left out, as AutoCAD's <c>AltSuppressZeroFeet</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltSuppressZeroFeet { get => Feet(Dimaltz); set => Dimaltz = FeetAndInches(Dimaltz, value, Inches(Dimaltz)); }

        /// <summary>Gets or sets whether nought inches of alternate units are left out, as AutoCAD's <c>AltSuppressZeroInches</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltSuppressZeroInches { get => Inches(Dimaltz); set => Dimaltz = FeetAndInches(Dimaltz, Feet(Dimaltz), value); }

        /// <summary>Gets or sets whether leading zeros of tolerances are left out (flag 4 of <see cref="Dimtzin"/>), as AutoCAD's <c>ToleranceSuppressLeadingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool ToleranceSuppressLeadingZeros { get => (Dimtzin & 4) != 0; set => Dimtzin = Bit(Dimtzin, 4, value); }

        /// <summary>Gets or sets whether trailing zeros of tolerances are left out (flag 8 of <see cref="Dimtzin"/>), as AutoCAD's <c>ToleranceSuppressTrailingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool ToleranceSuppressTrailingZeros { get => (Dimtzin & 8) != 0; set => Dimtzin = Bit(Dimtzin, 8, value); }

        /// <summary>Gets or sets whether nought feet of tolerances are left out, as AutoCAD's <c>ToleranceSuppressZeroFeet</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool ToleranceSuppressZeroFeet { get => Feet(Dimtzin); set => Dimtzin = FeetAndInches(Dimtzin, value, Inches(Dimtzin)); }

        /// <summary>Gets or sets whether nought inches of tolerances are left out, as AutoCAD's <c>ToleranceSuppressZeroInches</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool ToleranceSuppressZeroInches { get => Inches(Dimtzin); set => Dimtzin = FeetAndInches(Dimtzin, Feet(Dimtzin), value); }

        /// <summary>Gets or sets whether leading zeros of tolerances in alternate units are left out (flag 4 of <see cref="Dimalttz"/>), as AutoCAD's <c>AltToleranceSuppressLeadingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltToleranceSuppressLeadingZeros { get => (Dimalttz & 4) != 0; set => Dimalttz = Bit(Dimalttz, 4, value); }

        /// <summary>Gets or sets whether trailing zeros of tolerances in alternate units are left out (flag 8 of <see cref="Dimalttz"/>), as AutoCAD's <c>AltToleranceSuppressTrailingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltToleranceSuppressTrailingZeros { get => (Dimalttz & 8) != 0; set => Dimalttz = Bit(Dimalttz, 8, value); }

        /// <summary>Gets or sets whether nought feet of tolerances in alternate units are left out, as AutoCAD's <c>AltToleranceSuppressZeroFeet</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltToleranceSuppressZeroFeet { get => Feet(Dimalttz); set => Dimalttz = FeetAndInches(Dimalttz, value, Inches(Dimalttz)); }

        /// <summary>Gets or sets whether nought inches of tolerances in alternate units are left out, as AutoCAD's <c>AltToleranceSuppressZeroInches</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool AltToleranceSuppressZeroInches { get => Inches(Dimalttz); set => Dimalttz = FeetAndInches(Dimalttz, Feet(Dimalttz), value); }

        /// <summary>Gets or sets whether leading zeros of angles are left out (flag 1 of <see cref="Dimazin"/>), as AutoCAD's <c>SuppressAngularLeadingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool SuppressAngularLeadingZeros { get => (Dimazin & 1) != 0; set => Dimazin = Bit(Dimazin, 1, value); }

        /// <summary>Gets or sets whether trailing zeros of angles are left out (flag 2 of <see cref="Dimazin"/>), as AutoCAD's <c>SuppressAngularTrailingZeros</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool SuppressAngularTrailingZeros { get => (Dimazin & 2) != 0; set => Dimazin = Bit(Dimazin, 2, value); }

        /// <summary>Gets the size of the centre mark, as AutoCAD's <c>CenterMarkSize</c>: the size of <see cref="Dimcen"/>, 2.5 for -2.5.</summary>
        public double CenterMarkSize => Math.Abs(Dimcen);

        /// <summary>Gets what the centre is marked with, as AutoCAD's <c>CenterMarkType</c>: by the sign of <see cref="Dimcen"/>, a mark above nought, a line below, none at nought.</summary>
        public DxfDimensionCenterMarkType CenterMarkType
        {
            get
            {
                double size = Dimcen;
                return size > 0.0 ? DxfDimensionCenterMarkType.Mark : size < 0.0 ? DxfDimensionCenterMarkType.Line : DxfDimensionCenterMarkType.None;
            }
        }

        #endregion

        #region The variables, overridden or the style's

        /// <summary>Gets or sets the text style of the text, as AutoCAD's <c>TextStyleId</c> gives its id: the style's <see cref="DxfDimStyleTableRecord.Dimtxsty"/> or the dimension's override of it (340); null where neither names one.</summary>
        /// <exception cref="ArgumentException">Thrown when the style set is of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfTextStyleTableRecord TextStyle { get => (DxfTextStyleTableRecord)Get(V("Dimtxsty")); set => Set(V("Dimtxsty"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimadec"/>, as AutoCAD's <c>Dimadec</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimadec { get => (int)Get(V("Dimadec")); set => Set(V("Dimadec"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimalt"/>, as AutoCAD's <c>Dimalt</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimalt { get => (bool)Get(V("Dimalt")); set => Set(V("Dimalt"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaltd"/>, as AutoCAD's <c>Dimaltd</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public int Dimaltd { get => (int)Get(V("Dimaltd")); set => Set(V("Dimaltd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaltf"/>, as AutoCAD's <c>Dimaltf</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimaltf { get => (double)Get(V("Dimaltf")); set => Set(V("Dimaltf"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaltrnd"/>, as AutoCAD's <c>Dimaltrnd</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public double Dimaltrnd { get => (double)Get(V("Dimaltrnd")); set => Set(V("Dimaltrnd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimalttd"/>, as AutoCAD's <c>Dimalttd</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimalttd { get => (int)Get(V("Dimalttd")); set => Set(V("Dimalttd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimalttz"/>, as AutoCAD's <c>Dimalttz</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimalttz { get => (int)Get(V("Dimalttz")); set => Set(V("Dimalttz"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaltu"/>, as AutoCAD's <c>Dimaltu</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimaltu { get => (int)Get(V("Dimaltu")); set => Set(V("Dimaltu"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaltz"/>, as AutoCAD's <c>Dimaltz</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimaltz { get => (int)Get(V("Dimaltz")); set => Set(V("Dimaltz"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimapost"/>, as AutoCAD's <c>Dimapost</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string Dimapost { get => (string)Get(V("Dimapost")); set => Set(V("Dimapost"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimarcsym"/>, as AutoCAD's <c>Dimarcsym</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimarcsym { get => (int)Get(V("Dimarcsym")); set => Set(V("Dimarcsym"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimasz"/>, as AutoCAD's <c>Dimasz</c>: its override, else its style's; 4.5 where it overrides the style's 2.5 with 4.5 (1070 41, 1040 4.5).</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimasz { get => (double)Get(V("Dimasz")); set => Set(V("Dimasz"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimatfit"/>, as AutoCAD's <c>Dimatfit</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimatfit { get => (int)Get(V("Dimatfit")); set => Set(V("Dimatfit"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimaunit"/>, as AutoCAD's <c>Dimaunit</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimaunit { get => (int)Get(V("Dimaunit")); set => Set(V("Dimaunit"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimazin"/>, as AutoCAD's <c>Dimazin</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimazin { get => (int)Get(V("Dimazin")); set => Set(V("Dimazin"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimblk"/>, as AutoCAD's <c>Dimblk</c>: its override (1005 the block's handle, or 1000 its name in a file of R12), else its style's; null for the closed filled arrowhead.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfBlockTableRecord Dimblk { get => (DxfBlockTableRecord)Get(V("Dimblk")); set => Set(V("Dimblk"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimblk1"/>, as AutoCAD's <c>Dimblk1</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfBlockTableRecord Dimblk1 { get => (DxfBlockTableRecord)Get(V("Dimblk1")); set => Set(V("Dimblk1"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimblk2"/>, as AutoCAD's <c>Dimblk2</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfBlockTableRecord Dimblk2 { get => (DxfBlockTableRecord)Get(V("Dimblk2")); set => Set(V("Dimblk2"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimcen"/>, as AutoCAD's <c>Dimcen</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimcen { get => (double)Get(V("Dimcen")); set => Set(V("Dimcen"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimclrd"/>, as AutoCAD's <c>Dimclrd</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfColor Dimclrd { get => (DxfColor)Get(V("Dimclrd")); set => Set(V("Dimclrd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimclre"/>, as AutoCAD's <c>Dimclre</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfColor Dimclre { get => (DxfColor)Get(V("Dimclre")); set => Set(V("Dimclre"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimclrt"/>, as AutoCAD's <c>Dimclrt</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public DxfColor Dimclrt { get => (DxfColor)Get(V("Dimclrt")); set => Set(V("Dimclrt"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimdec"/>, as AutoCAD's <c>Dimdec</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimdec { get => (int)Get(V("Dimdec")); set => Set(V("Dimdec"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimdle"/>, as AutoCAD's <c>Dimdle</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimdle { get => (double)Get(V("Dimdle")); set => Set(V("Dimdle"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimdli"/>, as AutoCAD's <c>Dimdli</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimdli { get => (double)Get(V("Dimdli")); set => Set(V("Dimdli"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimdsep"/>, as AutoCAD's <c>Dimdsep</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public char Dimdsep { get => (char)Get(V("Dimdsep")); set => Set(V("Dimdsep"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimexe"/>, as AutoCAD's <c>Dimexe</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimexe { get => (double)Get(V("Dimexe")); set => Set(V("Dimexe"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimexo"/>, as AutoCAD's <c>Dimexo</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimexo { get => (double)Get(V("Dimexo")); set => Set(V("Dimexo"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimfrac"/>, as AutoCAD's <c>Dimfrac</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimfrac { get => (int)Get(V("Dimfrac")); set => Set(V("Dimfrac"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimfxlen"/>, as AutoCAD's <c>Dimfxlen</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public double Dimfxlen { get => (double)Get(V("Dimfxlen")); set => Set(V("Dimfxlen"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.DimfxlenOn"/>, as AutoCAD's <c>DimfxlenOn</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public bool DimfxlenOn { get => (bool)Get(V("DimfxlenOn")); set => Set(V("DimfxlenOn"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimgap"/>, as AutoCAD's <c>Dimgap</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimgap { get => (double)Get(V("Dimgap")); set => Set(V("Dimgap"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimjogang"/> in radians, as AutoCAD's <c>Dimjogang</c>: its override (in degrees), else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public double Dimjogang { get => (double)Get(V("Dimjogang")); set => Set(V("Dimjogang"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimjust"/>, as AutoCAD's <c>Dimjust</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimjust { get => (int)Get(V("Dimjust")); set => Set(V("Dimjust"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimldrblk"/>, as AutoCAD's <c>Dimldrblk</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the block set is a layout, or of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfBlockTableRecord Dimldrblk { get => (DxfBlockTableRecord)Get(V("Dimldrblk")); set => Set(V("Dimldrblk"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimlfac"/>, as AutoCAD's <c>Dimlfac</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimlfac { get => (double)Get(V("Dimlfac")); set => Set(V("Dimlfac"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimlim"/>, as AutoCAD's <c>Dimlim</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimlim { get => (bool)Get(V("Dimlim")); set => Set(V("Dimlim"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimltex1"/>, as AutoCAD's <c>Dimltex1</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltex1 { get => (DxfLinetypeTableRecord)Get(V("Dimltex1")); set => Set(V("Dimltex1"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimltex2"/>, as AutoCAD's <c>Dimltex2</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltex2 { get => (DxfLinetypeTableRecord)Get(V("Dimltex2")); set => Set(V("Dimltex2"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimltype"/>, as AutoCAD's <c>Dimltype</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentException">Thrown when the linetype set is of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public DxfLinetypeTableRecord Dimltype { get => (DxfLinetypeTableRecord)Get(V("Dimltype")); set => Set(V("Dimltype"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimlunit"/>, as AutoCAD's <c>Dimlunit</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimlunit { get => (int)Get(V("Dimlunit")); set => Set(V("Dimlunit"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimlwd"/>, as AutoCAD's <c>Dimlwd</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfLineWeight Dimlwd { get => (DxfLineWeight)Get(V("Dimlwd")); set => Set(V("Dimlwd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimlwe"/>, as AutoCAD's <c>Dimlwe</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the weight set is not one of AutoCAD's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public DxfLineWeight Dimlwe { get => (DxfLineWeight)Get(V("Dimlwe")); set => Set(V("Dimlwe"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimpost"/>, as AutoCAD's <c>Dimpost</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the text set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the text set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public string Dimpost { get => (string)Get(V("Dimpost")); set => Set(V("Dimpost"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimrnd"/>, as AutoCAD's <c>Dimrnd</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimrnd { get => (double)Get(V("Dimrnd")); set => Set(V("Dimrnd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimsah"/>, as AutoCAD's <c>Dimsah</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimsah { get => (bool)Get(V("Dimsah")); set => Set(V("Dimsah"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimscale"/>, as AutoCAD's <c>Dimscale</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimscale { get => (double)Get(V("Dimscale")); set => Set(V("Dimscale"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimsd1"/>, as AutoCAD's <c>Dimsd1</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool Dimsd1 { get => (bool)Get(V("Dimsd1")); set => Set(V("Dimsd1"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimsd2"/>, as AutoCAD's <c>Dimsd2</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool Dimsd2 { get => (bool)Get(V("Dimsd2")); set => Set(V("Dimsd2"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimse1"/>, as AutoCAD's <c>Dimse1</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimse1 { get => (bool)Get(V("Dimse1")); set => Set(V("Dimse1"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimse2"/>, as AutoCAD's <c>Dimse2</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimse2 { get => (bool)Get(V("Dimse2")); set => Set(V("Dimse2"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimsoxd"/>, as AutoCAD's <c>Dimsoxd</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimsoxd { get => (bool)Get(V("Dimsoxd")); set => Set(V("Dimsoxd"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtad"/>, as AutoCAD's <c>Dimtad</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public int Dimtad { get => (int)Get(V("Dimtad")); set => Set(V("Dimtad"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtdec"/>, as AutoCAD's <c>Dimtdec</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimtdec { get => (int)Get(V("Dimtdec")); set => Set(V("Dimtdec"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtfac"/>, as AutoCAD's <c>Dimtfac</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtfac { get => (double)Get(V("Dimtfac")); set => Set(V("Dimtfac"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtfill"/>, as AutoCAD's <c>Dimtfill</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public int Dimtfill { get => (int)Get(V("Dimtfill")); set => Set(V("Dimtfill"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtfillclr"/>, as AutoCAD's <c>Dimtfillclr</c>: its override (1070 70), else its style's.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2007.</exception>
        public DxfColor Dimtfillclr { get => (DxfColor)Get(V("Dimtfillclr")); set => Set(V("Dimtfillclr"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtih"/>, as AutoCAD's <c>Dimtih</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimtih { get => (bool)Get(V("Dimtih")); set => Set(V("Dimtih"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtix"/>, as AutoCAD's <c>Dimtix</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimtix { get => (bool)Get(V("Dimtix")); set => Set(V("Dimtix"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtm"/>, as AutoCAD's <c>Dimtm</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtm { get => (double)Get(V("Dimtm")); set => Set(V("Dimtm"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtmove"/>, as AutoCAD's <c>Dimtmove</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimtmove { get => (int)Get(V("Dimtmove")); set => Set(V("Dimtmove"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtofl"/>, as AutoCAD's <c>Dimtofl</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimtofl { get => (bool)Get(V("Dimtofl")); set => Set(V("Dimtofl"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtoh"/>, as AutoCAD's <c>Dimtoh</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimtoh { get => (bool)Get(V("Dimtoh")); set => Set(V("Dimtoh"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtol"/>, as AutoCAD's <c>Dimtol</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public bool Dimtol { get => (bool)Get(V("Dimtol")); set => Set(V("Dimtol"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtolj"/>, as AutoCAD's <c>Dimtolj</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimtolj { get => (int)Get(V("Dimtolj")); set => Set(V("Dimtolj"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtp"/>, as AutoCAD's <c>Dimtp</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtp { get => (double)Get(V("Dimtp")); set => Set(V("Dimtp"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtsz"/>, as AutoCAD's <c>Dimtsz</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtsz { get => (double)Get(V("Dimtsz")); set => Set(V("Dimtsz"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtvp"/>, as AutoCAD's <c>Dimtvp</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtvp { get => (double)Get(V("Dimtvp")); set => Set(V("Dimtvp"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtxt"/>, as AutoCAD's <c>Dimtxt</c>: its override, else its style's.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public double Dimtxt { get => (double)Get(V("Dimtxt")); set => Set(V("Dimtxt"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtxtdirection"/>, as AutoCAD's <c>Dimtxtdirection</c>: its override (1070 294), else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2010.</exception>
        public bool Dimtxtdirection { get => (bool)Get(V("Dimtxtdirection")); set => Set(V("Dimtxtdirection"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimtzin"/>, as AutoCAD's <c>Dimtzin</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public int Dimtzin { get => (int)Get(V("Dimtzin")); set => Set(V("Dimtzin"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimupt"/>, as AutoCAD's <c>Dimupt</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or its file is older than R2000.</exception>
        public bool Dimupt { get => (bool)Get(V("Dimupt")); set => Set(V("Dimupt"), value); }

        /// <summary>Gets or sets the dimension's <see cref="DxfDimStyleTableRecord.Dimzin"/>, as AutoCAD's <c>Dimzin</c>: its override, else its style's.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        public int Dimzin { get => (int)Get(V("Dimzin")); set => Set(V("Dimzin"), value); }

        /// <summary>
        /// Gets the variables the dimension is drawn with, as AutoCAD's <c>GetDimstyleData</c>: a new
        /// <see cref="DxfDimStyleTableRecord"/> in no table, named as the dimension's style, each variable as the dimension reads
        /// it, its override or its style's.
        /// </summary>
        /// <returns>The record, to be read, or given to another dimension by <see cref="SetDimstyleData"/>.</returns>
        public DxfDimStyleTableRecord GetDimstyleData()
        {
            var data = new DxfDimStyleTableRecord { Name = DimensionStyleName };

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                data.Keep(variable, Get(variable));
            }

            return data;
        }

        /// <summary>
        /// Gives the dimension the variables of a record, as AutoCAD's <c>SetDimstyleData</c>: each that differs from what the
        /// dimension reads written as an override, as its setter writes one; those the dimension reads already change nothing.
        /// A variable its file's version has not is passed over where the record holds AutoCAD's own value of it, and refused
        /// where it holds another; a record that names no text style leaves the dimension's as it is. A whole number is checked
        /// against AutoCAD's range as <see cref="DxfDatabase.SetDimstyleData"/> checks it, a <c>Dimdec</c> of 9 refused, and a
        /// size of the arrows, the text or the scale below nought. Every value is checked before any is written.
        /// </summary>
        /// <param name="style">The record: of a table, or made in code, as <see cref="GetDimstyleData"/> gives one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the record is null, or holds a null text or colour.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a value of the record is one AutoCAD does not take.</exception>
        /// <exception cref="ArgumentException">Thrown when a block, linetype or text style of the record is of another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased, or the record holds a value other than
        /// AutoCAD's own of a variable the dimension's file is too old to hold.</exception>
        public void SetDimstyleData(DxfDimStyleTableRecord style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            var changes = new List<KeyValuePair<DxfDimVariable, object>>();
            DxfFile file = Item.File;

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                object value = style.Get(variable);

                if ((value == null && variable.Kind == DxfDimKind.TextStyle)
                    || (IsResident && !file.Supports(variable.Since) && (variable.IsReference ? value == null : variable.SameValue(value, variable.Read(null))))
                    || variable.SameValue(value, Get(variable)))
                {
                    continue;
                }

                variable.Check(value, nameof(style));
                DxfDatabase.CheckDimRange(variable, value, nameof(style));
                file.Requires(variable.Since, variable.Name.ToUpperInvariant(), variable.SinceSaid);

                if (IsResident && variable.IsReference && value != null)
                {
                    DxfDimStyleTableRecord.Reference(file, variable, (DxfDBObject)value, !file.HasMarkers, nameof(style));
                }

                changes.Add(new KeyValuePair<DxfDimVariable, object>(variable, value));
            }

            foreach (KeyValuePair<DxfDimVariable, object> change in changes)
            {
                Set(change.Key, change.Value);
            }
        }

        /// <summary>
        /// Gets the text the dimension writes for a measurement, as AutoCAD's <c>FormatMeasurement</c>: the measurement in the
        /// units and to the places of the variables it reads, put into a text as its own text is. A length is multiplied by
        /// <see cref="Dimlfac"/> (a negative one taken as its size in paper space and as 1 elsewhere, as AutoCAD takes it),
        /// rounded by <see cref="Dimrnd"/> and written in <see cref="Dimlunit"/>: 1 scientific, 2 decimal, 3 engineering,
        /// 4 architectural, 5 fractional, 6 as the Windows desktop writes it under the culture of the thread; to
        /// <see cref="Dimdec"/> places, or in architectural and fractional units to a fraction of 2 to the power of them, stacked
        /// as <see cref="Dimfrac"/> says in AutoCAD's codes of an MTEXT; its zeros left out as <see cref="Dimzin"/> says,
        /// <see cref="Dimdsep"/> its decimal separator, <see cref="Dimpost"/> about it; and where <see cref="Dimalt"/> is on,
        /// its alternate units after it in brackets, or in the place of a <c>[]</c> of the text. An angle, of a <see cref="DxfLineAngularDimension2"/> or a
        /// <see cref="DxfPoint3AngularDimension"/>, is given in radians and written in <see cref="Dimaunit"/> (0 decimal
        /// degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians) to <see cref="Dimadec"/> places (those of
        /// <see cref="Dimdec"/> at -1), its zeros left out as <see cref="Dimazin"/> says. In the style ISO-25, two places, a
        /// comma and trailing zeros left out, 1234.5 is <c>1234,5</c> and 60 is <c>60</c>; in architectural units to a sixteenth,
        /// not stacked, 15.5 is <c>1'-3 1/2"</c>; to two decimal places 2.675 is <c>2.68</c>, a number being rounded as its 15
        /// significant figures (a .NET decimal), half away from nought; pi / 4 of an angular dimension is <c>45°</c>. Tolerances, limits,
        /// the arc symbol and the R or diameter sign before a radius are not part of it.
        /// </summary>
        /// <param name="measurement">The measurement: a length in drawing units, or an angle in radians; <see cref="Measurement"/> for the one the dimension shows.</param>
        /// <param name="dimensionText">The text to put it in, as <see cref="DimensionText"/>: empty or null for the measurement alone, <c>&lt;&gt; TYP</c> for it and TYP after it, <c>&lt;&gt; (=[])</c> with the alternate measurement in the brackets' place, a single space for no text.</param>
        /// <returns>The text: <c>60</c>, or <c>60 TYP</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the measurement is not a finite number.</exception>
        public string FormatMeasurement(double measurement, string dimensionText)
        {
            Guard.Finite(measurement, nameof(measurement), "The measurement has to be a finite number.");
            bool angular = this is DxfLineAngularDimension2 || this is DxfPoint3AngularDimension;
            bool paperSpace = Owner is DxfBlockTableRecord owner && owner.IsLayout && !string.Equals(owner.Name, "*Model_Space", StringComparison.OrdinalIgnoreCase);
            return DxfDimFormat.Text(dimensionText, measurement, angular, paperSpace, name => Get(V(name)));
        }

        #endregion

        #region Moving

        /// <summary>
        /// Moves, turns, scales or mirrors the dimension, as AutoCAD's <c>TransformBy</c> moves its points and as ezdxf's
        /// <c>transform()</c> carries them: the points the file writes in world coordinates (10, 13, 14, 15 and those of the
        /// kind) moved by the transformation, those it writes in the system of the normal (11, 12, the arc point 16) carried into
        /// the system of the new normal, the angles 50, 51 and 53 turned with it, and the normal turned; the record written once.
        /// The block that draws the dimension is moved with it where it is the dimension's alone (flag 32 of group 70) and the
        /// record places it at no point of its own (12): each of its entities moved by the transformation, seen from the system
        /// of the normal, as <see cref="DxfEntity.TransformBy(GeoTransform3)"/> moves one; an entity of a type the library keeps
        /// as it is, or one that cannot hold the move, is left, as ezdxf leaves it.
        /// </summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation takes the plane of the dimension to no plane.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            GeoVector3 normal = Normal;
            var ocs = new DxfOcsTransform(normal, transform);
            DxfDimGroups g = Own();
            bool placed = g.Find(12, g.All) >= 0;
            bool own = (Kind & 32) != 0;
            DxfBlockTableRecord block = IsResident && own && !placed ? DimBlock : null;

            foreach (KeyValuePair<int, bool> point in OcsPoints(g))
            {
                DxfDimGroups.Range part = point.Value ? g.Sub : g.Base;

                if (g.Find(point.Key, part) >= 0)
                {
                    g.SetPoint(point.Key, ocs.TransformVertex(g.Point(point.Key, part, Origin)), part, point.Value ? SubOrder : BaseOrder, Origin);
                }
            }

            foreach (KeyValuePair<int, bool> point in WcsPoints(g))
            {
                DxfDimGroups.Range part = point.Value ? g.Sub : g.Base;

                if (g.Find(point.Key, part) >= 0)
                {
                    g.SetPoint(point.Key, transform.Transform(g.Point(point.Key, part, Origin)), part, point.Value ? SubOrder : BaseOrder, Origin);
                }
            }

            TurnAngles(g, ocs, true);
            GeoVector3 turned = ocs.NewExtrusion;
            KeyValuePair<int, string>[] normalGroups = NormalGroups(turned);

            foreach (KeyValuePair<int, string> group in normalGroups)
            {
                g.Set(group.Key, group.Value, g.Base, BaseOrder);
            }

            g.Write(BaseOrder);

            if (block != null)
            {
                MovePicture(block, normal, turned, transform);
            }
        }

        /// <summary>
        /// Turns the angles of the dimension where the record has them, as a change of its system turns them: the text's (53)
        /// and the direction measured along (50) as the angle of the same direction in the new system, as ezdxf turns them, and
        /// the level direction (51) so too; an ordinate's 51, the negative of the angle from the system's x to the user's, as the
        /// negative of the new angle of that x, and written where it was nought too when the transformation is the entity's own
        /// move, so that the ordinate measures along the same axes after a move or a turn.
        /// </summary>
        private void TurnAngles(DxfDimGroups g, DxfOcsTransform ocs, bool moving)
        {
            bool ordinate = this is DxfOrdinateDimension;

            foreach (KeyValuePair<int, bool> angle in new[] { new KeyValuePair<int, bool>(53, false), new KeyValuePair<int, bool>(51, false), new KeyValuePair<int, bool>(50, true) })
            {
                DxfDimGroups.Range part = angle.Value ? g.Sub : g.Base;
                int at = g.Find(angle.Key, part);
                double degrees = g.Real(angle.Key, part, 0.0);

                if (ordinate && angle.Key == 51)
                {
                    double level = -ocs.TransformDegrees(-degrees) + 0.0;

                    if (at >= 0)
                    {
                        g.List[at] = new KeyValuePair<int, string>(51, Numbers.Exact(level));
                    }
                    else if (moving || !Same(level, 0.0))
                    {
                        g.Set(51, Numbers.Exact(level), g.Base, BaseOrder);
                    }

                    continue;
                }

                if (at >= 0)
                {
                    g.List[at] = new KeyValuePair<int, string>(angle.Key, Numbers.Exact(ocs.TransformDegrees(degrees) + 0.0));
                }
            }
        }

        /// <summary>
        /// Moves the entities of the block that draws the dimension by a transformation seen from the dimension's system: taken
        /// from the system of the old normal into the world, moved, and taken into the system of the new, as the block's entities
        /// stand in the system of the dimension's normal.
        /// </summary>
        private static void MovePicture(DxfBlockTableRecord block, GeoVector3 before, GeoVector3 after, GeoTransform3 transform)
        {
            GeoTransform3 into = Frame(before, false);
            GeoTransform3 outOf = Frame(after, true);
            GeoTransform3 move = outOf * transform * into;
            int left = 0;

            foreach (DxfEntity entity in block)
            {
                try
                {
                    entity.TransformBy(move);
                }
                catch (Exception e) when (e is NotSupportedException || e is ArgumentException)
                {
                    left++;
                }
            }

            if (left > 0)
            {
                GeometryHelperLog.Debug("DxfDimension: " + left.ToString(CultureInfo.InvariantCulture) + " entities of the block " + block.Name + " could not be moved with the dimension, and were left where they stand.");
            }
        }

        /// <summary>The transformation from the system of a normal to the world, or from the world to it.</summary>
        internal static GeoTransform3 Frame(GeoVector3 normal, bool fromWorld)
        {
            OcsAxes(normal, out GeoVector3 u, out GeoVector3 v);
            GeoVector3 n = normal;

            return fromWorld
                ? new GeoTransform3(new double[,] { { u.X, u.Y, u.Z, 0.0 }, { v.X, v.Y, v.Z, 0.0 }, { n.X, n.Y, n.Z, 0.0 }, { 0.0, 0.0, 0.0, 1.0 } })
                : new GeoTransform3(new double[,] { { u.X, v.X, n.X, 0.0 }, { u.Y, v.Y, n.Y, 0.0 }, { u.Z, v.Z, n.Z, 0.0 }, { 0.0, 0.0, 0.0, 1.0 } });
        }

        /// <summary>
        /// Refuses a dimension whose drawing has no block of the name of the one that draws it, as one made in code and given
        /// none: AutoCAD has nothing to draw it with until it draws it anew, and ezdxf's audit takes such a dimension out. A copy
        /// of another drawing whose block is one AutoCAD names itself (<c>*D7</c>) brings a copy of it, as the file's adopting
        /// makes one, and needs none here. The blocks, linetypes and text styles set as overrides while the
        /// dimension was in no drawing are written as the records of their names: in this drawing for one made in code or copied
        /// in it, refused where it has none of a name; in the drawing a copy comes from for a copy of another, whose overrides the
        /// copying takes to the records of their names here, as AutoCAD's deep clone maps them, refused where that drawing has none.
        /// </summary>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            string name = DxfReader.NameOf(Own().Text(2, Own().Base) ?? string.Empty);
            DxfDatabase database = owner.File?.Database;
            DxfFile origin = Item.File.IsDetached ? Item.File.Origin : null;
            bool otherDrawing = origin != null && origin != owner.File && origin.Database != null;
            bool brought = otherDrawing && name.StartsWith("*", StringComparison.Ordinal) && origin.Database.BlockTable.Has(name);

            if (name.Length == 0 || database == null || (!brought && !database.BlockTable.Has(name)))
            {
                throw new ArgumentException("The drawing has no block " + (name.Length == 0 ? "named by the dimension" : name) + " to draw the dimension with: give it its DimBlock, a block of the drawing.", "entity");
            }

            bool r12 = !owner.File.HasMarkers;
            DxfDatabase names = otherDrawing ? origin.Database : database;

            // The overrides of a copy of another drawing are written in the forms of the record it was, which the copying takes
            // to this drawing; the others in this drawing's.
            bool form = otherDrawing ? !Item.File.HasMarkers : r12;
            var held = new List<KeyValuePair<DxfDimVariable, string>>();

            // Every held record taken to its namesake before anything is written, in the order of the variables.
            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                if (_held == null || !_held.TryGetValue(variable, out DxfDBObject value))
                {
                    continue;
                }

                if (!owner.File.Supports(variable.Since) || (r12 && variable.R12Code == 0))
                {
                    throw new ArgumentException("A DXF of version " + (owner.File.Version ?? "R12") + " has no " + variable.Name.ToUpperInvariant() + ", which came with " + variable.SinceSaid + ".", "entity");
                }

                string text = value == null ? (form ? string.Empty : "0") : Namesake(names, variable, (DxfSymbolTableRecord)value, form);
                held.Add(new KeyValuePair<DxfDimVariable, string>(variable, text));
            }

            // A copy added to the drawing it came from shares its original's block: the block is no longer the copy's alone
            // (flag 32), so that moving the copy leaves the original's picture where it is.
            if (Item.File.IsDetached && origin == owner.File && (Kind & 32) != 0)
            {
                Write(g => SetFlag(g, 32, false));
            }

            foreach (KeyValuePair<DxfDimVariable, string> pair in held)
            {
                WriteOverride(pair.Key, pair.Value, form);
            }

            _held = null;
        }

        /// <summary>Gives a copy the records set on this dimension while it is in no drawing, to be taken to their namesakes when the copy is appended.</summary>
        private protected override void Copied(DxfEntity original)
        {
            if (original is DxfDimension dimension && dimension._held != null)
            {
                _held = new Dictionary<DxfDimVariable, DxfDBObject>(dimension._held);
            }
        }

        /// <summary>
        /// The handle (the name in a drawing of R12) of the record of a drawing of the same name as one held: a block, a linetype
        /// or a text style.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the drawing has no record of that name.</exception>
        private static string Namesake(DxfDatabase database, DxfDimVariable variable, DxfSymbolTableRecord record, bool r12)
        {
            string name = record.Name;
            DxfSymbolTable table = variable.Kind == DxfDimKind.Block ? database.BlockTable : variable.Kind == DxfDimKind.Linetype ? (DxfSymbolTable)database.LinetypeTable : database.TextStyleTable;

            if (!table.Has(name))
            {
                throw new ArgumentException("The drawing has no " + (variable.Kind == DxfDimKind.Block ? "block" : variable.Kind == DxfDimKind.Linetype ? "linetype" : "text style") + " named " + name + " for the dimension's " + variable.Name + ".", "entity");
            }

            DxfSymbolTableRecord found = table[name];
            return r12 ? found.Name : found.Handle;
        }

        #endregion

        #region Reading and writing

        /// <summary>
        /// Gets the entities the dimension is drawn with, as AutoCAD's <c>Explode</c>: those of its block
        /// (<see cref="DimBlock"/>, <c>*D7</c>) placed where the dimension draws them, as ezdxf's <c>virtual_entities()</c> places
        /// them, each a copy of its own look; moved by <see cref="DimBlockPosition"/>, and carried from the system of the
        /// dimension's <see cref="Normal"/> to the world at the height of its text where that is not the world's. The block is
        /// the picture AutoCAD drew last: a point set on the dimension since is not in it.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no block of the name the dimension is drawn by.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            DxfBlockTableRecord block = DimBlock ?? throw new InvalidOperationException("The drawing has no block " + PictureName + " the dimension is drawn with, to explode it into.");
            GeoVector3 normal = Normal;
            double slack = Tolerance.Default.EqualVector;
            GeoTransform3 placing = GeoTransform3.Translation(DimBlockPosition.ToVector());

            if (Math.Abs(normal.X) > slack || Math.Abs(normal.Y) > slack || Math.Abs(normal.Z - 1.0) > slack)
            {
                double elevation = Own().Point(11, Own().Base, Origin).Z;
                placing = placing * Frame(normal, false) * GeoTransform3.Translation(new GeoVector3(0.0, 0.0, elevation));
            }

            return PlacedCopies(block, placing * GeoTransform3.Translation(block.Origin.ToVector().Negate())).AsReadOnly();
        }

        [DxfType("DIMENSION")]
        internal static DxfEntity Create(DxfItem item)
        {
            if (!int.TryParse(item.File.ValueOf(item, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind))
            {
                return null;
            }

            switch (kind & 15)
            {
                case 0: return new DxfRotatedDimension(item);
                case 1: return new DxfAlignedDimension(item);
                case 2: return new DxfLineAngularDimension2(item);
                case 3: return new DxfDiametricDimension(item);
                case 4: return new DxfRadialDimension(item);
                case 5: return new DxfPoint3AngularDimension(item);
                case 6: return new DxfOrdinateDimension(item);
                default: return null;
            }
        }

        /// <summary>The name of the block that draws the dimension (its 2); empty where it names none.</summary>
        internal string PictureName => DxfReader.NameOf(Own().Text(2, Own().Base) ?? string.Empty);

        /// <summary>The flags of group 70 of AcDbDimension: the kind in its four low bits, 32, 64 and 128.</summary>
        private protected int Kind => Own().Whole(70, Own().Base, 0);

        /// <summary>The own groups of the record, read anew.</summary>
        private protected DxfDimGroups Own() => new DxfDimGroups(Item);

        /// <summary>Changes the own groups and writes them in one writing, where they differ.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the dimension is erased.</exception>
        private protected void Write(Action<DxfDimGroups> change)
        {
            if (IsErased)
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " is erased, and is not changed.");
            }

            DxfDimGroups g = Own();
            change(g);
            g.Write(BaseOrder);
        }

        /// <summary>A point of the kind's own part in world coordinates, as the file writes it there; the origin where it gives none.</summary>
        private protected GeoPoint3 SubPoint(int code) => Own().Point(code, Own().Sub, Origin);

        /// <summary>The definition point (10), in world coordinates; the origin where the record gives none.</summary>
        private protected GeoPoint3 DefinitionPoint => Own().Point(10, Own().Base, Origin);

        /// <summary>A point of the kind's own part written in the system of the normal, in world coordinates.</summary>
        private protected GeoPoint3 SubPlanePoint(int code) => OcsToWorld(Normal, Own().Point(code, Own().Sub, Origin));

        /// <summary>Sets a point of the kind's own part, in world coordinates; nothing where it is as it was.</summary>
        private protected void SetSubPoint(int code, GeoPoint3 value)
        {
            CheckPoint(value);
            Write(g => g.SetPoint(code, value, g.Sub, SubOrder, Origin));
        }

        /// <summary>Sets a point of the kind's own part written in the system of the normal, given in world coordinates.</summary>
        private protected void SetSubPlanePoint(int code, GeoPoint3 value)
        {
            CheckPoint(value);

            if (!Same(value, SubPlanePoint(code)))
            {
                GeoPoint3 ocs = WorldToOcs(Normal, value);
                Write(g => g.SetPoint(code, ocs, g.Sub, SubOrder, Origin));
            }
        }

        /// <summary>Sets the definition point (10), in world coordinates.</summary>
        private protected void SetDefinitionPoint(GeoPoint3 value)
        {
            CheckPoint(value);
            Write(g => g.SetPoint(10, value, g.Base, BaseOrder, Origin));
        }

        /// <summary>A number of the kind's own part; the fallback where it gives none.</summary>
        private protected double SubReal(int code, double fallback) => Own().Real(code, Own().Sub, fallback);

        /// <summary>Sets a number of the kind's own part; nothing where it is the one read, to the bit.</summary>
        private protected void SetSubReal(int code, double value, double now)
        {
            Guard.Finite(value, nameof(value), "The number has to be a finite number.");

            if (!Same(value, now))
            {
                Write(g => g.Set(code, Numbers.Exact(value + 0.0), g.Sub, SubOrder));
            }
        }

        /// <summary>A whole number of the kind's own part; the fallback where it gives none.</summary>
        private protected int SubWhole(int code, int fallback) => Own().Whole(code, Own().Sub, fallback);

        /// <summary>Sets a whole number of the kind's own part; nothing where it is the one read.</summary>
        private protected void SetSubWhole(int code, int value, int now)
        {
            if (value != now)
            {
                Write(g => g.Set(code, value.ToString(CultureInfo.InvariantCulture), g.Sub, SubOrder));
            }
        }

        /// <summary>Sets an angle in radians, written in degrees in the base part or the kind's own; nothing where it reads back as the angle it is.</summary>
        private protected void SetAngle(int code, double value, double now, bool sub)
        {
            Guard.Finite(value, nameof(value), "An angle has to be a finite number.");

            if (!Same(value, now))
            {
                Write(g => g.Set(code, Numbers.Exact(DxfOcsTransform.Degrees(value)), sub ? g.Sub : g.Base, sub ? SubOrder : BaseOrder));
            }
        }

        /// <summary>
        /// The angle of the extension lines from the square of the dimension line (52, in degrees), in radians: of AcDbDimension,
        /// where ezdxf writes it, else of the kind's part, where the DXF reference puts it; nought where the record gives none.
        /// </summary>
        private protected double ObliqueAngle()
        {
            DxfDimGroups g = Own();
            return g.Real(52, g.Find(52, g.Base) >= 0 ? g.Base : g.Sub, 0.0) * (Math.PI / 180.0);
        }

        /// <summary>Sets the oblique angle in radians, where the record has its 52, else in AcDbDimension.</summary>
        private protected void SetOblique(double value)
        {
            DxfDimGroups g = Own();
            bool sub = g.Marked && g.Find(52, g.Base) < 0 && g.Find(52, g.Sub) >= 0;
            SetAngle(52, value, ObliqueAngle(), sub);
        }

        /// <summary>Sets or clears a flag of group 70 of AcDbDimension.</summary>
        private protected void SetFlag(DxfDimGroups g, int flag, bool on)
        {
            int kind = g.Whole(70, g.Base, 0);
            int now = on ? kind | flag : kind & ~flag;

            if (now != kind)
            {
                g.Set(70, now.ToString(CultureInfo.InvariantCulture), g.Base, BaseOrder);
            }
        }

        /// <summary>The angle of a direction about the normal, seen in the system of the normal, in radians from 0 to two pi.</summary>
        private protected double AngleOf(GeoVector3 direction)
        {
            OcsAxes(Normal, out GeoVector3 u, out GeoVector3 v);
            double angle = Math.Atan2(direction.DotProduct(v), direction.DotProduct(u));
            return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
        }

        /// <summary>
        /// The angle swept counter-clockwise about the normal from one direction to another, from 0 to two pi; that from the
        /// other to the one where a third direction, that of the arc point, does not lie within it.
        /// </summary>
        private protected double Sweep(GeoVector3 from, GeoVector3 to, GeoVector3 through)
        {
            double start = AngleOf(from);
            double sweep = AngleOf(to) - start;
            sweep = sweep < 0.0 ? sweep + 2.0 * Math.PI : sweep;
            double at = AngleOf(through) - start;
            at = at < 0.0 ? at + 2.0 * Math.PI : at;
            return at <= sweep ? sweep : 2.0 * Math.PI - sweep;
        }

        /// <summary>The kind's own points written in the system of the normal, each with whether it is of the kind's part: the text's (11), the block's (12) where it stands, and the kind's own.</summary>
        private IEnumerable<KeyValuePair<int, bool>> OcsPoints(DxfDimGroups g)
        {
            yield return new KeyValuePair<int, bool>(11, false);
            yield return new KeyValuePair<int, bool>(12, g.Find(12, g.Base) < 0 && g.Marked && g.Find(12, g.Sub) >= 0);

            foreach (int code in PlanePoints)
            {
                yield return new KeyValuePair<int, bool>(code, true);
            }
        }

        /// <summary>The points written in world coordinates: the definition point (10) and the kind's own.</summary>
        private IEnumerable<KeyValuePair<int, bool>> WcsPoints(DxfDimGroups g)
        {
            yield return new KeyValuePair<int, bool>(10, false);

            foreach (int code in WorldPoints)
            {
                yield return new KeyValuePair<int, bool>(code, true);
            }
        }

        private void SetWhole(int code, int value, int now)
        {
            if (value == now)
            {
                return;
            }

            Item.File.Requires(1015, "group " + code.ToString(CultureInfo.InvariantCulture) + " of a dimension", "R2000 (AC1015)");
            Write(g => g.Set(code, value.ToString(CultureInfo.InvariantCulture), g.Base, BaseOrder));
        }

        /// <summary>The groups of a dimension made in code.</summary>
        private static IEnumerable<DxfTypedValue> Groups(int kind, GeoPoint3 definition, string dimensionText, DxfDimStyleTableRecord dimensionStyle, IEnumerable<DxfTypedValue> own)
        {
            CheckPoint(definition);

            var groups = new List<DxfTypedValue>
            {
                new DxfTypedValue(100, "AcDbDimension"),
                new DxfTypedValue(3, dimensionStyle?.Name ?? "Standard"),
                new DxfTypedValue(10, Numbers.Exact(definition.X + 0.0)),
                new DxfTypedValue(20, Numbers.Exact(definition.Y + 0.0)),
                new DxfTypedValue(30, Numbers.Exact(definition.Z + 0.0)),
                new DxfTypedValue(70, kind.ToString(CultureInfo.InvariantCulture)),
            };

            if (!string.IsNullOrEmpty(dimensionText))
            {
                groups.Add(new DxfTypedValue(1, dimensionText));
            }

            groups.AddRange(own);
            return groups;
        }

        /// <summary>The groups of a point of a dimension made in code.</summary>
        private protected static IEnumerable<DxfTypedValue> PointGroups(int code, GeoPoint3 point)
        {
            CheckPoint(point);
            return new[]
            {
                new DxfTypedValue(code, Numbers.Exact(point.X + 0.0)),
                new DxfTypedValue(code + 10, Numbers.Exact(point.Y + 0.0)),
                new DxfTypedValue(code + 20, Numbers.Exact(point.Z + 0.0)),
            };
        }

        private protected static void CheckPoint(GeoPoint3 value)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }
        }

        private protected static bool Same(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        #endregion

        #region Overrides

        /// <summary>The value of a variable as the dimension draws with it: its override, else its style's, else AutoCAD's own.</summary>
        internal object Get(DxfDimVariable variable)
        {
            if (_held != null && _held.TryGetValue(variable, out DxfDBObject held))
            {
                return held;
            }

            if (Override(variable, out string text))
            {
                return variable.IsReference ? DxfDimStyleTableRecord.Resolve(Item.File, variable, text) : variable.Read(text);
            }

            DxfDimStyleTableRecord style = DimensionStyle;

            if (style != null)
            {
                return style.Get(variable);
            }

            return variable.IsReference ? null : variable.Read(null);
        }

        /// <summary>
        /// Sets a variable as an override, where it differs from what the dimension reads: compared first, then checked, so that
        /// the value read changes nothing whatever the file holds. A block, linetype or text style set while the dimension is
        /// in no drawing is held as the record it is, and taken to the record of its name in the drawing it is added to.
        /// </summary>
        private void Set(DxfDimVariable variable, object value)
        {
            if (variable.SameValue(value, Get(variable)))
            {
                return;
            }

            variable.Check(value, nameof(value));

            if (IsErased)
            {
                throw new InvalidOperationException("The " + Item.Type + " " + (Handle ?? string.Empty) + " is erased, and is not changed.");
            }

            DxfFile file = Item.File;
            bool r12 = !file.HasMarkers;
            file.Requires(variable.Since, variable.Name.ToUpperInvariant(), variable.SinceSaid);
            string text;

            if (variable.IsReference)
            {
                if (r12 && variable.R12Code == 0)
                {
                    throw new InvalidOperationException("A DXF of R12 names no " + variable.Name.ToUpperInvariant() + " of a dimension.");
                }

                if (!IsResident)
                {
                    if (value != null && (((DxfDBObject)value).IsErased || !(value is DxfSymbolTableRecord record) || record.Item == null))
                    {
                        throw new ArgumentException("The " + variable.Name + " is a record of a drawing, not erased: it is taken to the record of its name in the drawing the dimension is added to.", nameof(value));
                    }

                    _held = _held ?? new Dictionary<DxfDimVariable, DxfDBObject>();
                    _held[variable] = (DxfDBObject)value;
                    return;
                }

                text = value == null ? (r12 ? string.Empty : "0") : DxfDimStyleTableRecord.Reference(file, variable, (DxfDBObject)value, r12, nameof(value));
            }
            else
            {
                text = variable.Write(value);
            }

            WriteOverride(variable, text, r12);
        }

        /// <summary>The text of the dimension's override of a variable (in the list DSTYLE of its data of ACAD); false where it overrides none.</summary>
        private bool Override(DxfDimVariable variable, out string text)
        {
            text = null;
            List<DxfTypedValue> data = AcadData();
            bool r12 = !Item.File.HasMarkers;

            if (data == null || !Bounds(data, out int open, out int close))
            {
                return false;
            }

            int code = variable.CodeIn(r12);

            for (int i = open + 1; i + 1 < close; i += 2)
            {
                if (data[i].TypeCode == 1070 && int.TryParse(data[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int named) && named == code)
                {
                    text = data[i + 1].Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Writes an override into the list DSTYLE of the extended data of ACAD, in place where the dimension has one of the
        /// variable, else last in the list; the list and the data of ACAD made where there are none, the rest of ACAD's data
        /// kept.
        /// </summary>
        private void WriteOverride(DxfDimVariable variable, string text, bool r12)
        {
            List<DxfTypedValue> data = AcadData() ?? new List<DxfTypedValue> { new DxfTypedValue(1001, Application) };
            int code = variable.CodeIn(r12);
            var pair = new[] { new DxfTypedValue(1070, code.ToString(CultureInfo.InvariantCulture)), new DxfTypedValue(variable.DataCode(r12), text) };

            if (!Bounds(data, out int open, out int close))
            {
                data.Add(new DxfTypedValue(1000, Overrides));
                data.Add(new DxfTypedValue(1002, "{"));
                data.AddRange(pair);
                data.Add(new DxfTypedValue(1002, "}"));
            }
            else
            {
                int at = -1;

                for (int i = open + 1; i + 1 < close && at < 0; i += 2)
                {
                    if (data[i].TypeCode == 1070 && int.TryParse(data[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int named) && named == code)
                    {
                        at = i;
                    }
                }

                if (at >= 0)
                {
                    data[at + 1] = pair[1];
                }
                else
                {
                    data.InsertRange(close, pair);
                }
            }

            XData = data;
        }

        /// <summary>The extended data of ACAD, its 1001 first, as a list to be changed; null where the dimension has none.</summary>
        private List<DxfTypedValue> AcadData()
        {
            IReadOnlyList<DxfTypedValue> data = GetXDataForApplication(Application);
            return data == null ? null : new List<DxfTypedValue>(data);
        }

        /// <summary>Where the list DSTYLE opens (its 1002 {) and closes (its 1002 }) in the data of ACAD; false where it has none.</summary>
        private static bool Bounds(List<DxfTypedValue> data, out int open, out int close)
        {
            open = -1;
            close = -1;

            for (int i = 1; i + 1 < data.Count; i++)
            {
                if (data[i].TypeCode == 1000 && string.Equals(data[i].Value?.Trim(), Overrides, StringComparison.OrdinalIgnoreCase) && data[i + 1].TypeCode == 1002 && data[i + 1].Value?.Trim() == "{")
                {
                    open = i + 1;
                    break;
                }
            }

            for (int i = open + 1; open >= 0 && i < data.Count; i++)
            {
                if (data[i].TypeCode == 1002 && data[i].Value?.Trim() == "}")
                {
                    close = i;
                    return true;
                }
            }

            return false;
        }

        private static DxfDimVariable V(string name) => DxfDimVariable.Named(name);

        private static string Before(string text, string mark)
        {
            int at = text.IndexOf(mark, StringComparison.Ordinal);
            return at < 0 ? string.Empty : text.Substring(0, at);
        }

        private static string After(string text, string mark)
        {
            int at = text.IndexOf(mark, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(at + mark.Length);
        }

        private static string Joined(string prefix, string mark, string suffix)
        {
            if (prefix == null || suffix == null)
            {
                throw new ArgumentNullException(prefix == null ? "prefix" : "suffix");
            }

            return prefix.Length == 0 && suffix.Length == 0 ? string.Empty : prefix + mark + suffix;
        }

        private static int Bit(int value, int bit, bool on) => on ? value | bit : value & ~bit;

        // The low two bits of a DIMZIN: 0 leaves out nought feet and nought inches, 1 shows both, 2 shows feet and leaves out
        // inches, 3 shows inches and leaves out feet.
        private static bool Feet(int zin) => (zin & 3) == 0 || (zin & 3) == 3;

        private static bool Inches(int zin) => (zin & 3) == 0 || (zin & 3) == 2;

        private static int FeetAndInches(int zin, bool feet, bool inches) => (zin & ~3) | (feet ? (inches ? 0 : 3) : (inches ? 2 : 1));

        #endregion
    }
}
