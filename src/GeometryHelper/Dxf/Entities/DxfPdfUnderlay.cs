using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A PDFUNDERLAY of a DXF file, as AutoCAD's <c>PdfReference</c>: one page of a PDF file placed in the drawing, its file and
    /// page named by its PDFDEFINITION (<see cref="Definition"/>), shown at a place, a scale and a turn, faded, in grey or
    /// clipped as its record says. The library reads no PDF: it places, scales and clips the underlay as its record says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record holds the insertion point (groups 10, 20, 30) where the lower left corner of the page stands, the scale along
    /// the page's two sides and its depth (41, 42, 43), the turn (50, in degrees as the file writes it, in radians here) about
    /// the insertion point in the plane of the extrusion (210, 220, 230), the display flags (280), the contrast (281, 0 to
    /// 100, 100 as it is) and the fade (282, 0 to 100, 0 as it is), and the clip boundary (the run of 11 with its 21).
    /// </para>
    /// <para>
    /// The page is measured in inches, as AutoCAD 2021 measures it: a page of 200 by 100 points, 1/72 of an inch each, is 2.7778
    /// by 1.3889 at scale 1, and 200 by 100 points at scale 72 is the size it is printed on a sheet in inches. The clip boundary is
    /// in the same inches from the page's lower left corner, before the scale: the corners (1, 1) and (2, 2) of a page placed
    /// at (10, 20) with the scale 2 show the part from (12, 22) to (14, 24). Two corners are a rectangle, which AutoCAD stores as
    /// its upper left and lower right (0, 20) and (30, 0); three or more are a polygon, open as AutoCAD keeps it.
    /// </para>
    /// <para>
    /// A file older than R2010 has no PDF underlays: one is not added to it. Every group the class does not set is kept as it was.
    /// </para>
    /// </remarks>
    public sealed class DxfPdfUnderlay : DxfEntity
    {
        /// <summary>The flag of the display options that switches the clip boundary on.</summary>
        private const int ClipFlag = 1;

        /// <summary>The flag that shows the underlay.</summary>
        private const int OnFlag = 2;

        /// <summary>The flag that shows the underlay in monochrome.</summary>
        private const int MonochromeFlag = 4;

        /// <summary>The flag that adjusts the colours for the background.</summary>
        private const int AdjustFlag = 8;

        /// <summary>The flag that shows what lies outside the boundary rather than inside it (clip inverted).</summary>
        private const int InvertFlag = 16;

        /// <summary>The flags a new underlay has, and one whose record has none: clipped (on, as AutoCAD keeps a new one), shown, colours adjusted.</summary>
        private const int DefaultFlags = ClipFlag | OnFlag | AdjustFlag;

        // The groups of AcDbUnderlayReference in the order AutoCAD 2021 writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(
            new[] { 100, 340, 10, 20, 30, 41, 42, 43, 50, 210, 220, 230, 280, 281, 282, 11, 21 }
        );

        // The groups of the run of the clip boundary, rewritten together.
        private static readonly IReadOnlyCollection<int> BoundaryCodes = Array.AsReadOnly(new[] { 11, 21 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        // The definition an underlay made in code shows, until it is appended to the definition's drawing.
        private DxfPdfDefinition _definition;

        /// <summary>
        /// Initializes an underlay made in code of a definition of a drawing, as AutoCAD's <c>new PdfReference()</c> given its
        /// <c>DefinitionId</c>: its corner at the origin, scale 1 along every side, not turned, shown, its colours adjusted, its
        /// contrast 100 and its fade 0, with no clip boundary, as AutoCAD 2021 writes a new one. Appended to a block of the
        /// definition's drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>, it is counted by
        /// <see cref="DxfPdfDefinition.GetEntityCount"/>.
        /// </summary>
        /// <param name="definition">The definition, of a drawing's dictionary of PDF underlays.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is in no drawing, or erased.</exception>
        public DxfPdfUnderlay(DxfPdfDefinition definition)
            : base("PDFUNDERLAY", NewGroups(definition))
        {
            _definition = definition;
        }

        private DxfPdfUnderlay(DxfItem item)
            : base(item) { }

        #region Where it is

        /// <summary>
        /// Gets or sets the corner of the page, as AutoCAD's <c>Position</c>: where the lower left corner of the page stands, in world
        /// coordinates (groups 10, 20, 30).
        /// </summary>
        /// <value>Set, the underlay moves with its turn and scale kept.</value>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public GeoPoint3 Position
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>
        /// Gets or sets the scale along the page's width, its height and its depth, as AutoCAD's <c>ScaleFactors</c> (groups 41, 42,
        /// 43): 1 for every side as it is made. The page 2.7778 by 1.3889 inches given the scale (25.4, 25.4, 25.4) is 70.56 by 35.28.
        /// </summary>
        /// <value>Set, each of the three above nought.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public GeoVector3 ScaleFactors
        {
            get { return new GeoVector3(GetDouble(41, 1.0), GetDouble(42, 1.0), GetDouble(43, 1.0)); }
            set
            {
                Guard.Positive(value.X, nameof(value), "A scale along the width is above nought.");
                Guard.Positive(value.Y, nameof(value), "A scale along the height is above nought.");
                Guard.Positive(value.Z, nameof(value), "A scale along the depth is above nought.");
                SetScale(value.X, value.Y, value.Z);
            }
        }

        /// <summary>
        /// Gets or sets the turn of the underlay about its corner, as AutoCAD's <c>Rotation</c>, in radians in the plane of the
        /// <see cref="Normal"/>; the file writes degrees (group 50): 0.5 radian is 28.6479.
        /// </summary>
        /// <value>Set, a finite number.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public double Rotation
        {
            get => GetDouble(50, 0.0) * Math.PI / 180.0;
            set
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");
                double degrees = value * 180.0 / Math.PI;

                if (!Same(degrees, GetDouble(50, 0.0)))
                {
                    SetString(50, Numbers.Exact(degrees + 0.0));
                }
            }
        }

        /// <summary>
        /// Gets or sets the normal of the plane the underlay lies in, as AutoCAD's <c>Normal</c> (groups 210, 220, 230): (0, 0, 1)
        /// for one laid in the plane xy, as it is made.
        /// </summary>
        /// <value>Set, a direction of finite numbers and some length, made of unit length.</value>
        /// <exception cref="ArgumentException">Thrown when the direction has no length or is not finite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public GeoVector3 Normal
        {
            get => GetNormal();
            set => SetNormal(value);
        }

        /// <summary>
        /// Gets the transformation from the page's inches to the world, as AutoCAD's <c>Transform</c>: the page's (x, y), from its
        /// lower left corner, scaled, turned in the plane of the normal and moved to the corner. The page 2.7778 inches wide at
        /// the scale 2 at (10, 20) takes (1, 0) to (12, 20, 0); turned a quarter turn, to (10, 22, 0).
        /// </summary>
        public GeoTransform3 Transform
        {
            get
            {
                GeoVector3 n = Normal;
                GeoVector3 ax;

                // The arbitrary axis algorithm: the x of the plane of a normal, as AutoCAD's OCS has it.
                if (Math.Abs(n.X) < 1.0 / 64.0 && Math.Abs(n.Y) < 1.0 / 64.0)
                {
                    ax = new GeoVector3(0.0, 1.0, 0.0).CrossProduct(n);
                }
                else
                {
                    ax = new GeoVector3(0.0, 0.0, 1.0).CrossProduct(n);
                }

                ax = ax.Divide(ax.Length);
                GeoVector3 ay = n.CrossProduct(ax);
                double angle = Rotation;
                double cos = Math.Cos(angle);
                double sin = Math.Sin(angle);
                GeoVector3 scale = ScaleFactors;
                GeoVector3 u = ax.Multiply(cos * scale.X).Add(ay.Multiply(sin * scale.X));
                GeoVector3 v = ax.Multiply(-sin * scale.Y).Add(ay.Multiply(cos * scale.Y));
                GeoVector3 w = n.Multiply(scale.Z);
                GeoPoint3 at = Position;

                return new GeoTransform3(
                    new[,]
                    {
                        { u.X, v.X, w.X, at.X },
                        { u.Y, v.Y, w.Y, at.Y },
                        { u.Z, v.Z, w.Z, at.Z },
                        { 0.0, 0.0, 0.0, 1.0 },
                    }
                );
            }
        }

        /// <summary>Sets the three scales in one writing; nothing where they are as read.</summary>
        private void SetScale(double x, double y, double z) => SetGroups(ScaleGroups(x, y, z));

        /// <summary>The groups that set the scale (41, 42, 43), each where it differs from the one read, to the bit.</summary>
        private KeyValuePair<int, string>[] ScaleGroups(double x, double y, double z)
        {
            var groups = new List<KeyValuePair<int, string>>();
            double[] values = { x, y, z };

            for (int i = 0; i < 3; i++)
            {
                if (!Same(values[i], GetDouble(41 + i, 1.0)))
                {
                    groups.Add(new KeyValuePair<int, string>(41 + i, Numbers.Exact(values[i] + 0.0)));
                }
            }

            return groups.ToArray();
        }

        #endregion

        #region Its page

        /// <summary>
        /// Gets the definition the underlay shows (group 340), as AutoCAD's <c>DefinitionId</c> names it: of its drawing, of the
        /// drawing it was copied from for a copy in no file; null where the drawing has no PDFDEFINITION of the handle.
        /// </summary>
        public DxfPdfDefinition Definition
        {
            get
            {
                if (!IsResident && _definition != null)
                {
                    return _definition.IsErased ? null : _definition;
                }

                string handle = DefinitionHandle;
                DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;

                if (
                    string.IsNullOrEmpty(handle)
                    || handle == "0"
                    || database == null
                    || !database.TryGetObject(handle, out DxfDBObject found)
                )
                {
                    return null;
                }

                return found as DxfPdfDefinition;
            }
        }

        /// <summary>The handle of the PDFDEFINITION the record names (340), as written; null where it names none.</summary>
        internal string DefinitionHandle => GetString(340)?.Trim();

        /// <summary>
        /// Gets the name of the underlay, as AutoCAD's <c>Name</c>: the name its PDFDEFINITION has in the dictionary of PDF
        /// underlays (ACAD_PDFUNDERLAYS), such as <c>sample - Page_1</c>; null where it shows no definition of the dictionary.
        /// </summary>
        public string Name
        {
            get
            {
                DxfPdfDefinition definition = Definition;
                DxfDatabase database = definition?.Item?.File.Database;
                DxfDBDictionary underlays = database == null ? null : DxfPdfDefinition.GetPdfDictionary(database);
                return underlays?.NameOf(definition);
            }
        }

        /// <summary>Gets the path of the PDF file, as AutoCAD's <c>Path</c>: the path its definition names, as the file writes it; empty where it shows none.</summary>
        public string Path => Definition?.SourceFileName ?? string.Empty;

        /// <summary>Gets the name of the page, as AutoCAD's <c>NameOfSheet</c>: the number of the page as text, <c>1</c> for the first; empty where it shows none.</summary>
        public string NameOfSheet => Definition?.ItemName ?? string.Empty;

        /// <summary>
        /// Makes the underlay show another definition of its drawing, as AutoCAD's <c>DefinitionId</c> set: its 340 the definition's
        /// handle, the definition's reactors given the underlay.
        /// </summary>
        /// <param name="definition">The definition, of the underlay's drawing; of any drawing for an underlay made in code in none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is erased, in no drawing, or of another drawing than the underlay's.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public void SetDefinition(DxfPdfDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Guard.Argument(
                definition.IsResident && !definition.IsErased,
                nameof(definition),
                "The PDFDEFINITION is in no drawing, or erased: add it to a dictionary of PDF underlays first."
            );
            Guard.Argument(
                !IsResident || definition.Item.File == Item.File,
                nameof(definition),
                "The PDFDEFINITION is of another drawing than the underlay."
            );
            DxfPdfDefinition before = Definition;
            SetString(340, definition.Handle);

            if (IsResident && before != null && before.IsResident && !before.IsErased)
            {
                Item.File.SetReactor(before.Item, Item.Handle, false);
            }

            if (IsResident)
            {
                Item.File.SetReactor(definition.Item, Item.Handle, true);
            }
        }

        #endregion

        #region How it looks

        /// <summary>The display flags (group 280); 11 where the record gives none, as AutoCAD writes none for the flags it makes a new underlay.</summary>
        private int Flags => GetInt(280, DefaultFlags);

        /// <summary>Sets a display flag on or off.</summary>
        private void SetFlag(int flag, bool on)
        {
            int now = Flags;
            int then = on ? now | flag : now & ~flag;

            if (then != now)
            {
                SetString(280, then.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets or sets whether the underlay is shown, as AutoCAD's <c>IsOn</c> (flag 2 of group 280); true as it is made.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public bool IsOn
        {
            get => (Flags & OnFlag) != 0;
            set => SetFlag(OnFlag, value);
        }

        /// <summary>
        /// Gets or sets whether the underlay is clipped, as AutoCAD's <c>IsClipped</c> (flag 1): true as it is made, which clips
        /// nothing while the underlay has no boundary (<see cref="HasClipBoundary"/>), as AutoCAD 2021 keeps a new one.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public bool IsClipped
        {
            get => (Flags & ClipFlag) != 0;
            set => SetFlag(ClipFlag, value);
        }

        /// <summary>
        /// Gets or sets whether the underlay shows what lies outside its boundary and hides what lies inside, as AutoCAD's
        /// <c>IsClipInverted</c> (flag 16); false as it is made.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public bool IsClipInverted
        {
            get => (Flags & InvertFlag) != 0;
            set => SetFlag(InvertFlag, value);
        }

        /// <summary>Gets or sets whether the underlay is shown in monochrome, as AutoCAD's <c>Monochrome</c> (flag 4); false as it is made.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public bool Monochrome
        {
            get => (Flags & MonochromeFlag) != 0;
            set => SetFlag(MonochromeFlag, value);
        }

        /// <summary>Gets or sets whether the colours are adjusted for the background, as AutoCAD's <c>AdjustColorForBackground</c> (flag 8); true as it is made.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public bool AdjustColorForBackground
        {
            get => (Flags & AdjustFlag) != 0;
            set => SetFlag(AdjustFlag, value);
        }

        /// <summary>Gets or sets the contrast (group 281), as AutoCAD's <c>Contrast</c>: 0 to 100, 100 as it is made, where the colours are as the PDF has them.</summary>
        /// <value>Set, from 0 to 100.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the contrast set is outside 0 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public int Contrast
        {
            get => Level(281, 100);
            set => SetLevel(281, value, 100, "contrast");
        }

        /// <summary>Gets or sets the fade (group 282), as AutoCAD's <c>Fade</c>: 0 to 100, 0 as it is made, where the colours are as the PDF has them; 100 fades them into the background.</summary>
        /// <value>Set, from 0 to 100.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the fade set is outside 0 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public int Fade
        {
            get => Level(282, 0);
            set => SetLevel(282, value, 0, "fade");
        }

        private int Level(int code, int fallback) => Math.Max(0, Math.Min(100, GetInt(code, fallback)));

        private void SetLevel(int code, int value, int fallback, string what)
        {
            if (value < 0 || value > 100)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The " + what + " of a PDF underlay is from 0 to 100."
                );
            }

            if (value != Level(code, fallback))
            {
                SetString(code, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        #endregion

        #region Its clip boundary

        /// <summary>Gets whether the underlay has a clip boundary, two corners or a polygon (the run of 11 with its 21), whether or not it is on (<see cref="IsClipped"/>).</summary>
        public bool HasClipBoundary => Boundary().Count >= 2;

        /// <summary>
        /// Gets the clip boundary, as AutoCAD's <c>GetClipBoundary</c>: in the page's inches from its lower left corner, before the
        /// scale. Two corners for a rectangle, as AutoCAD stores it, the upper left and the lower right; the corners of a polygon
        /// otherwise, not repeating the first at the end; none for an underlay with no boundary.
        /// </summary>
        /// <returns>The corners, a new list on each call.</returns>
        public IReadOnlyList<GeoPoint2> GetClipBoundary() => Boundary().AsReadOnly();

        /// <summary>
        /// Sets the clip boundary, as AutoCAD's <c>SetClipBoundary</c>, and switches the clip on: two corners are a rectangle,
        /// written as AutoCAD 2021 writes it, the upper left and the lower right, so that (1, 1) and (2, 2) are (1, 2) and (2, 1);
        /// three or more are a polygon, written as given. The page's inches from its lower left corner, before the scale.
        /// </summary>
        /// <param name="corners">The corners, two or at least three of finite numbers.</param>
        /// <exception cref="ArgumentNullException">Thrown when the corners are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is one corner, or a corner is not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public void SetClipBoundary(IReadOnlyList<GeoPoint2> corners)
        {
            if (corners == null)
            {
                throw new ArgumentNullException(nameof(corners));
            }

            Guard.Argument(
                corners.Count == 2 || corners.Count >= 3,
                nameof(corners),
                "A clip boundary is a rectangle of two corners or a polygon of three or more."
            );

            foreach (GeoPoint2 corner in corners)
            {
                Guard.Argument(
                    Guard.IsFinite(corner.X) && Guard.IsFinite(corner.Y),
                    nameof(corners),
                    "A corner of a boundary has to be of finite numbers: " + corner + " is not."
                );
            }

            var kept = new List<GeoPoint2>(corners);

            if (kept.Count == 2)
            {
                double x0 = Math.Min(kept[0].X, kept[1].X);
                double x1 = Math.Max(kept[0].X, kept[1].X);
                double y0 = Math.Min(kept[0].Y, kept[1].Y);
                double y1 = Math.Max(kept[0].Y, kept[1].Y);
                kept = new List<GeoPoint2> { new GeoPoint2(x0, y1), new GeoPoint2(x1, y0) };
            }

            WriteBoundary(kept);
            SetFlag(ClipFlag, true);
        }

        /// <summary>Takes the clip boundary away, as AutoCAD's PDFCLIP Delete: the whole page is shown, the clip on as a new underlay has it.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the underlay is erased.</exception>
        public void ClearClipBoundary()
        {
            WriteBoundary(new List<GeoPoint2>());
            SetFlag(ClipFlag, true);
            SetFlag(InvertFlag, false);
        }

        private void WriteBoundary(List<GeoPoint2> corners)
        {
            var groups = new List<KeyValuePair<int, string>>();

            foreach (GeoPoint2 corner in corners)
            {
                groups.Add(new KeyValuePair<int, string>(11, Numbers.Exact(corner.X + 0.0)));
                groups.Add(new KeyValuePair<int, string>(21, Numbers.Exact(corner.Y + 0.0)));
            }

            ReplaceGroups(0, BoundaryCodes, groups.ToArray());
        }

        /// <summary>The corners of the boundary the record holds (the run of 11 with its 21), in its order.</summary>
        private List<GeoPoint2> Boundary()
        {
            IReadOnlyList<DxfTypedValue> groups = GetOwnGroups(0);
            var corners = new List<GeoPoint2>();

            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].TypeCode != 11)
                {
                    continue;
                }

                double x = DxfLeaderGroups.Num(groups[i].Value, 0.0);
                double y =
                    i + 1 < groups.Count && groups[i + 1].TypeCode == 21
                        ? DxfLeaderGroups.Num(groups[++i].Value, 0.0)
                        : 0.0;
                corners.Add(new GeoPoint2(x, y));
            }

            return corners;
        }

        /// <summary>
        /// Gets the clip boundary as a closed polygon in world coordinates, as AutoCAD's frame draws it: the rectangle's four
        /// corners or the polygon's, laid through <see cref="Transform"/>; none for an underlay with no boundary.
        /// </summary>
        /// <returns>The corners, the first not repeated at the end; empty where there is no boundary.</returns>
        public IReadOnlyList<GeoPoint3> GetClipFrame()
        {
            List<GeoPoint2> held = Boundary();
            var frame = new List<GeoPoint3>();

            if (held.Count < 2)
            {
                return frame.AsReadOnly();
            }

            if (held.Count == 2)
            {
                double x0 = Math.Min(held[0].X, held[1].X);
                double x1 = Math.Max(held[0].X, held[1].X);
                double y0 = Math.Min(held[0].Y, held[1].Y);
                double y1 = Math.Max(held[0].Y, held[1].Y);
                held = new List<GeoPoint2>
                {
                    new GeoPoint2(x0, y0),
                    new GeoPoint2(x1, y0),
                    new GeoPoint2(x1, y1),
                    new GeoPoint2(x0, y1),
                };
            }

            GeoTransform3 to = Transform;

            foreach (GeoPoint2 corner in held)
            {
                frame.Add(to.Transform(new GeoPoint3(corner.X, corner.Y, 0.0)));
            }

            return frame.AsReadOnly();
        }

        #endregion

        #region As an entity

        /// <summary>
        /// Gives the clip boundary of the underlay in world coordinates as one closed <see cref="GeoPolyline3"/>, which is all the
        /// library knows of its extent: the library reads no page, so an underlay with no boundary gives no shape and no extents.
        /// </summary>
        private protected override IReadOnlyList<object> OwnGeometry()
        {
            IReadOnlyList<GeoPoint3> frame = GetClipFrame();
            return frame.Count < 3
                ? Array.AsReadOnly(new object[0])
                : Array.AsReadOnly(new object[] { new GeoPolyline3(new List<GeoPoint3>(frame).ToArray(), true) });
        }

        /// <summary>
        /// Moves, turns and scales the underlay, as AutoCAD 2021's <c>TransformBy</c> does: the corner moved, the sides of the
        /// page turned and scaled, its turn and scale rewritten. A mirror or a shear, which AutoCAD does not keep in a
        /// PDFUNDERLAY, is refused.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the transformation mirrors or shears the page, or takes a side of it to no length.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            GeoTransform3 now = Transform;
            GeoPoint3 at = transform.Transform(Position);
            GeoVector3 u = transform.Transform(now.Transform(new GeoVector3(1.0, 0.0, 0.0)));
            GeoVector3 v = transform.Transform(now.Transform(new GeoVector3(0.0, 1.0, 0.0)));
            GeoVector3 w = transform.Transform(now.Transform(new GeoVector3(0.0, 0.0, 1.0)));
            double a = u.Length;
            double b = v.Length;
            double c = w.Length;

            if (!(a > 0.0) || !(b > 0.0) || !(c > 0.0) || double.IsInfinity(a + b + c))
            {
                throw new ArgumentException(
                    "The transformation takes a side of the underlay to no length.",
                    nameof(transform)
                );
            }

            GeoVector3 n = u.CrossProduct(v);
            double tolerance = Tolerance.Default.EqualVector;

            if (
                Math.Abs(u.DotProduct(v)) > tolerance * a * b
                || n.DotProduct(w) <= 0.0
                || Math.Abs(n.Length - a * b) > tolerance * a * b
            )
            {
                throw new ArgumentException(
                    "AutoCAD keeps no PDF underlay mirrored or sheared: the transformation does either.",
                    nameof(transform)
                );
            }

            GeoVector3 unit = w.Divide(c);
            GeoVector3 ax;

            if (Math.Abs(unit.X) < 1.0 / 64.0 && Math.Abs(unit.Y) < 1.0 / 64.0)
            {
                ax = new GeoVector3(0.0, 1.0, 0.0).CrossProduct(unit);
            }
            else
            {
                ax = new GeoVector3(0.0, 0.0, 1.0).CrossProduct(unit);
            }

            ax = ax.Divide(ax.Length);
            GeoVector3 ay = unit.CrossProduct(ax);
            double angle = Math.Atan2(u.DotProduct(ay), u.DotProduct(ax));
            SetGroups(Join(PointGroups(10, at, Origin), ScaleGroups(a, b, c), NormalGroups(unit)));
            Rotation = angle;
        }

        /// <inheritdoc/>
        private protected override IReadOnlyList<int> GroupOrder => Order;

        [DxfType("PDFUNDERLAY")]
        internal static DxfEntity Create(DxfItem item) => new DxfPdfUnderlay(item);

        /// <summary>Called once the underlay is appended: its definition's reactor, its classes listed and counted, as AutoCAD 2021 lists them.</summary>
        internal override void Appended(bool madeInCode)
        {
            DxfPdfDefinition definition = _definition;
            _definition = null;

            if (definition != null && definition.Item != null && definition.Item.File == Item.File)
            {
                Item.File.SetReactor(definition.Item, Item.Handle, true);
            }

            Item.File.AddClass("PDFUNDERLAY", "AcDbPdfReference", 4095, "ObjectDBX Classes", 1, true);
            Item.File.CountClass("PDFUNDERLAY");
        }

        /// <summary>Refuses an underlay made in code for a block of another drawing than its definition's, where it would point at nothing.</summary>
        /// <exception cref="ArgumentException">Thrown when the definition is of another drawing, or erased.</exception>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (_definition != null && !IsResident)
            {
                Guard.Argument(
                    !_definition.IsErased && owner.File != null && _definition.Item.File == owner.File,
                    "entity",
                    "The underlay shows a PDFDEFINITION of another drawing, or one erased: make it of a definition of this drawing."
                );
                owner.File.Requires(1024, "PDF underlays", "R2010 (AC1024)");
            }
        }

        /// <summary>
        /// Gives a copy of an underlay in no file the definition it was made of, to be shown in the drawing it goes into as the
        /// underlay's is.
        /// </summary>
        private protected override void Copied(DxfEntity original)
        {
            if (original is DxfPdfUnderlay underlay && underlay._definition != null && !underlay.IsResident)
            {
                _definition = underlay._definition;
            }
        }

        #endregion

        /// <summary>The groups of a new underlay of a definition, as AutoCAD 2021 writes one given its <c>DefinitionId</c>.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the definition is in no drawing, or erased.</exception>
        private static IEnumerable<DxfTypedValue> NewGroups(DxfPdfDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (!definition.IsResident || definition.IsErased)
            {
                throw new ArgumentException(
                    "The PDFDEFINITION is in no drawing, or erased: add it to a dictionary of PDF underlays first, by DxfDBDictionary.SetAt.",
                    nameof(definition)
                );
            }

            return new[]
            {
                new DxfTypedValue(100, "AcDbUnderlayReference"),
                new DxfTypedValue(340, definition.Handle),
                new DxfTypedValue(10, "0"),
                new DxfTypedValue(20, "0"),
                new DxfTypedValue(30, "0"),
                new DxfTypedValue(41, "1"),
                new DxfTypedValue(42, "1"),
                new DxfTypedValue(43, "1"),
                new DxfTypedValue(281, "100"),
                new DxfTypedValue(282, "0"),
            };
        }
    }
}
