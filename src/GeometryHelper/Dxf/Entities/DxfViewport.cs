using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A VIEWPORT of a DXF file, as AutoCAD's <c>Viewport</c>: a window of a layout's sheet onto model space, its place and size
    /// on the sheet, the view it shows, its snap and grid, its user coordinate system and the layers frozen in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A viewport 200 wide and 150 high about (150, 100) on the sheet that shows model space about (5 000, 3 000) at 1:50 has
    /// <see cref="CenterPoint"/> (150, 100, 0), <see cref="Width"/> 200, <see cref="Height"/> 150, <see cref="ViewCenter"/>
    /// (5 000, 3 000) and <see cref="ViewHeight"/> 7 500: <see cref="CustomScale"/> is 150 / 7 500, 0.02, and
    /// <see cref="StandardScale"/> <see cref="DxfStandardScaleType.Scale1To50"/>. Its <see cref="Number"/> is the id the file
    /// gives it (group 69) while it is on, 1 for the viewport that shows the sheet itself, and -1 turned off.
    /// </para>
    /// <para>
    /// A file of R2000 or later holds the view in the viewport's own groups (12 to 51, 90 for its flags, 331 for its frozen
    /// layers); one of R12, R13 or R14 in its extended data of ACAD, after <c>MVIEW</c>, each value in its place, read and
    /// written there. What those have not, as the user coordinate system or the plot style, reads as AutoCAD's default and is
    /// not set there.
    /// </para>
    /// <para>
    /// The view is not drawn anew: a change to it is written, and AutoCAD shows it when the drawing is next opened.
    /// <see cref="DxfEntity.TransformBy"/> moves and scales the viewport on its sheet, as AutoCAD's MOVE and SCALE do, the view in
    /// it kept: a viewport scaled by 2 shows the same part of model space twice as large.
    /// </para>
    /// </remarks>
    public sealed class DxfViewport : DxfEntity
    {
        // The groups of AcDbViewport in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[]
        {
            100, 10, 20, 30, 40, 41, 68, 69, 12, 22, 13, 23, 14, 24, 15, 25, 16, 26, 36, 17, 27, 37, 42, 43, 44, 45, 50, 51, 72, 331, 90,
            340, 1, 281, 71, 74, 110, 120, 130, 111, 121, 131, 112, 122, 132, 345, 346, 79, 146, 170, 61, 332, 333, 348, 292, 282, 141, 142,
            63, 421, 431, 361, 335, 343, 344, 91,
        });

        private static readonly int[] FrozenCodes = { 331 };

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        // The places of the values of a viewport of R12 in its extended data of ACAD, a point one place, MVIEW the first.
        private const int R12Target = 3;
        private const int R12Direction = 4;
        private const int R12Twist = 5;
        private const int R12Height = 6;
        private const int R12CenterX = 7;
        private const int R12CenterY = 8;
        private const int R12Lens = 9;
        private const int R12Front = 10;
        private const int R12Back = 11;
        private const int R12ViewMode = 12;
        private const int R12Circle = 13;
        private const int R12FastZoom = 14;
        private const int R12UcsIcon = 15;
        private const int R12Snap = 16;
        private const int R12Grid = 17;
        private const int R12SnapStyle = 18;
        private const int R12IsoPair = 19;
        private const int R12SnapAngle = 20;
        private const int R12SnapBaseX = 21;
        private const int R12SnapBaseY = 22;
        private const int R12SnapX = 23;
        private const int R12SnapY = 24;
        private const int R12GridX = 25;
        private const int R12GridY = 26;
        private const int R12HidePlot = 27;

        /// <summary>
        /// Initializes a viewport made in code, as AutoCAD's <c>new Viewport()</c>: about the origin, 1 by 1, off, showing the
        /// top view of model space about its origin; give it its place, size and view, append it to a layout of paper space and
        /// turn it <see cref="On"/>.
        /// </summary>
        public DxfViewport()
            : base("VIEWPORT", new[]
            {
                new DxfTypedValue(100, "AcDbViewport"),
                new DxfTypedValue(10, "0.0"), new DxfTypedValue(20, "0.0"), new DxfTypedValue(30, "0.0"),
                new DxfTypedValue(40, "1.0"), new DxfTypedValue(41, "1.0"), new DxfTypedValue(68, "0"), new DxfTypedValue(69, "2"),
                new DxfTypedValue(12, "0.0"), new DxfTypedValue(22, "0.0"), new DxfTypedValue(13, "0.0"), new DxfTypedValue(23, "0.0"),
                new DxfTypedValue(14, "10.0"), new DxfTypedValue(24, "10.0"), new DxfTypedValue(15, "10.0"), new DxfTypedValue(25, "10.0"),
                new DxfTypedValue(16, "0.0"), new DxfTypedValue(26, "0.0"), new DxfTypedValue(36, "1.0"),
                new DxfTypedValue(17, "0.0"), new DxfTypedValue(27, "0.0"), new DxfTypedValue(37, "0.0"),
                new DxfTypedValue(42, "50.0"), new DxfTypedValue(43, "0.0"), new DxfTypedValue(44, "0.0"), new DxfTypedValue(45, "1.0"),
                new DxfTypedValue(50, "0.0"), new DxfTypedValue(51, "0.0"), new DxfTypedValue(72, "100"), new DxfTypedValue(90, "131072"),
                new DxfTypedValue(1, string.Empty), new DxfTypedValue(281, "0"), new DxfTypedValue(71, "1"), new DxfTypedValue(74, "0"),
                new DxfTypedValue(110, "0.0"), new DxfTypedValue(120, "0.0"), new DxfTypedValue(130, "0.0"),
                new DxfTypedValue(111, "1.0"), new DxfTypedValue(121, "0.0"), new DxfTypedValue(131, "0.0"),
                new DxfTypedValue(112, "0.0"), new DxfTypedValue(122, "1.0"), new DxfTypedValue(132, "0.0"),
                new DxfTypedValue(79, "0"), new DxfTypedValue(146, "0.0"),
            })
        {
        }

        private DxfViewport(DxfItem item)
            : base(item)
        {
        }

        [DxfType("VIEWPORT")]
        internal static DxfEntity Create(DxfItem item) => new DxfViewport(item);

        private protected override IReadOnlyList<int> GroupOrder => Order;

        #region On the sheet

        /// <summary>Gets or sets the centre of the viewport on its sheet (groups 10, 20, 30), as AutoCAD's <c>CenterPoint</c>, in the units of paper space.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoPoint3 CenterPoint
        {
            get => GetPoint(10, Origin);
            set => SetPoint(10, value, Origin);
        }

        /// <summary>Gets or sets the width of the viewport on its sheet (group 40), as AutoCAD's <c>Width</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double Width
        {
            get => GetDouble(40, 0.0);
            set => SetSize(40, value, Width);
        }

        /// <summary>Gets or sets the height of the viewport on its sheet (group 41), as AutoCAD's <c>Height</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double Height
        {
            get => GetDouble(41, 0.0);
            set => SetSize(41, value, Height);
        }

        /// <summary>
        /// Gets the number of the viewport in its layout, as AutoCAD's <c>Number</c> gives it once the layout is shown: the id
        /// of group 69 for a viewport that is <see cref="On"/>, 1 for the viewport that shows the sheet itself and 2 and on for
        /// those on it; -1 for a viewport turned off, as AutoCAD 2021 gave its viewport 94 of id 3 turned off, which a file
        /// still writes the id of. A viewport made in code is given the next free id of its layout when appended. AutoCAD gives
        /// -1 for every viewport of a layout not yet shown as well, which a file does not say: the library reads the layout as
        /// shown.
        /// </summary>
        public int Number => On ? Id : -1;

        /// <summary>
        /// Gets or sets whether the viewport is on, as AutoCAD's <c>On</c>: its status (group 68) not nought and, from R2000 on, its
        /// flag 0x20000 of group 90 clear.
        /// </summary>
        /// <value>Set off, the status 0 and the flag set; set on, the flag cleared and the status the next above those of the other viewports of its layout, as AutoCAD stacks them.</value>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool On
        {
            get => GetInt(68, 1) != 0 && (InMview || (Flags() & 0x20000) == 0);

            set
            {
                if (value == On)
                {
                    return;
                }

                string status = value ? NextStatus().ToString(CultureInfo.InvariantCulture) : "0";

                if (InMview)
                {
                    SetString(68, status);
                    return;
                }

                int flags = value ? Flags() & ~0x20000 : Flags() | 0x20000;
                SetGroups(Join(new[] { new KeyValuePair<int, string>(68, status) }, FlagGroups(flags)));
            }
        }

        /// <summary>Gets or sets whether the view of the viewport is locked against zooming and panning (flag 0x4000 of group 90), as AutoCAD's <c>Locked</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000, which has no such flag.</exception>
        public bool Locked
        {
            get => Bit(0x4000);
            set => SetBit(0x4000, value, -1, 0);
        }

        /// <summary>Gets or sets whether the viewport is clipped by another entity (flag 0x10000 of group 90, the entity group 340), as AutoCAD's <c>NonRectClipOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool NonRectClipOn
        {
            get => Bit(0x10000);
            set => SetBit(0x10000, value, -1, 0);
        }

        #endregion

        #region The view

        /// <summary>Gets or sets the centre of the view (groups 12, 22), as AutoCAD's <c>ViewCenter</c>, in the coordinates of the view about its target.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoPoint2 ViewCenter
        {
            get => !InMview ? new GeoPoint2(GetDouble(12, 0.0), GetDouble(22, 0.0)) : new GeoPoint2(R12Real(R12CenterX, 0.0), R12Real(R12CenterY, 0.0));

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                GeoPoint2 now = ViewCenter;

                if (Same(value.X, now.X) && Same(value.Y, now.Y))
                {
                    return;
                }

                if (InMview)
                {
                    SetR12(new[] { R12CenterX, R12CenterY }, new[] { Real(value.X), Real(value.Y) });
                    return;
                }

                SetGroups(new KeyValuePair<int, string>(12, Real(value.X)), new KeyValuePair<int, string>(22, Real(value.Y)));
            }
        }

        /// <summary>Gets or sets the height of model space the viewport shows (group 45), as AutoCAD's <c>ViewHeight</c>; 1 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double ViewHeight
        {
            get => Value(45, R12Height, 1.0);

            set
            {
                if (Same(value, ViewHeight))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "The height of a view has to be a finite number above nought.");
                SetValue(45, R12Height, value);
            }
        }

        /// <summary>Gets or sets the point the view looks at (groups 17, 27, 37), as AutoCAD's <c>ViewTarget</c>, in world coordinates; the origin where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoPoint3 ViewTarget
        {
            get
            {
                if (!InMview)
                {
                    return GetPoint(17, Origin);
                }

                double[] p = R12Point(R12Target);
                return p == null ? Origin : new GeoPoint3(p[0], p[1], p[2]);
            }

            set
            {
                if (InMview)
                {
                    SetR12Point(R12Target, value.X, value.Y, value.Z, ViewTarget.X, ViewTarget.Y, ViewTarget.Z, value.IsValid);
                    return;
                }

                SetPoint(17, value, Origin);
            }
        }

        /// <summary>
        /// Gets or sets the direction from the target to the eye (groups 16, 26, 36), as AutoCAD's <c>ViewDirection</c>, in world
        /// coordinates: (0, 0, 1) for the view from above, which a record that gives none has.
        /// </summary>
        /// <value>Set, a direction of some length, written as it is given.</value>
        /// <exception cref="ArgumentException">Thrown when the direction set has no length or is not of finite numbers.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoVector3 ViewDirection
        {
            get
            {
                if (!InMview)
                {
                    GeoPoint3 p = GetPoint(16, new GeoPoint3(0.0, 0.0, 1.0));
                    return new GeoVector3(p.X, p.Y, p.Z);
                }

                double[] d = R12Point(R12Direction);
                return d == null ? new GeoVector3(0.0, 0.0, 1.0) : new GeoVector3(d[0], d[1], d[2]);
            }

            set
            {
                GeoVector3 now = ViewDirection;

                if (Same(value.X, now.X) && Same(value.Y, now.Y) && Same(value.Z, now.Z))
                {
                    return;
                }

                if (!value.IsValid || !(value.Length > 0.0))
                {
                    throw new ArgumentException("A direction has to be of finite numbers and some length: " + value + " is not.", nameof(value));
                }

                if (InMview)
                {
                    SetR12Point(R12Direction, value.X, value.Y, value.Z, now.X, now.Y, now.Z, true);
                    return;
                }

                SetPoint(16, new GeoPoint3(value.X, value.Y, value.Z), new GeoPoint3(0.0, 0.0, 1.0));
            }
        }

        /// <summary>
        /// Gets or sets how far the view is turned about its direction, as AutoCAD's <c>TwistAngle</c>, in radians: group 51, in
        /// degrees, from R2000; in radians in the extended data MVIEW of a file of R12 to R14, as AutoCAD 2021 reads it there (a
        /// 30 written there turns the view by 30 radians, 1 718.87 degrees).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double TwistAngle
        {
            get => InMview ? R12Real(R12Twist, 0.0) : GetDouble(51, 0.0) * Math.PI / 180.0;

            set
            {
                if (!InMview)
                {
                    SetAngle(51, R12Twist, value, TwistAngle);
                }
                else if (!Same(value, TwistAngle))
                {
                    Guard.Finite(value, nameof(value), "An angle has to be a finite number.");
                    SetValue(51, R12Twist, value);
                }
            }
        }

        /// <summary>Gets or sets the length of the lens of a view in perspective (group 42), as AutoCAD's <c>LensLength</c>, in millimetres; 50 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the length set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double LensLength
        {
            get => Value(42, R12Lens, 50.0);

            set
            {
                if (Same(value, LensLength))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "The length of a lens has to be a finite number above nought.");
                SetValue(42, R12Lens, value);
            }
        }

        /// <summary>Gets or sets how far the front clipping plane is from the target (group 43), as AutoCAD's <c>FrontClipDistance</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double FrontClipDistance
        {
            get => Value(43, R12Front, 0.0);
            set => SetFinite(43, R12Front, value, FrontClipDistance);
        }

        /// <summary>Gets or sets how far the back clipping plane is from the target (group 44), as AutoCAD's <c>BackClipDistance</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the distance set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double BackClipDistance
        {
            get => Value(44, R12Back, 0.0);
            set => SetFinite(44, R12Back, value, BackClipDistance);
        }

        /// <summary>Gets or sets whether the view is in perspective (flag 1 of group 90; of the view mode of R12), as AutoCAD's <c>PerspectiveOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool PerspectiveOn
        {
            get => Bit(0x1);
            set => SetBit(0x1, value, R12ViewMode, 1);
        }

        /// <summary>Gets or sets whether the view is clipped in front (flag 2), as AutoCAD's <c>FrontClipOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool FrontClipOn
        {
            get => Bit(0x2);
            set => SetBit(0x2, value, R12ViewMode, 2);
        }

        /// <summary>Gets or sets whether the view is clipped behind (flag 4), as AutoCAD's <c>BackClipOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool BackClipOn
        {
            get => Bit(0x4);
            set => SetBit(0x4, value, R12ViewMode, 4);
        }

        /// <summary>Gets or sets whether the front clipping plane is at the eye: flag 0x10 (front clip not at eye) clear, as AutoCAD's <c>FrontClipAtEyeOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool FrontClipAtEyeOn
        {
            get => !Bit(0x10);
            set => SetBit(0x10, !value, R12ViewMode, 16);
        }

        /// <summary>Gets or sets whether the view follows the user coordinate system (flag 8), as AutoCAD's <c>UcsFollowModeOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool UcsFollowModeOn
        {
            get => Bit(0x8);
            set => SetBit(0x8, value, R12ViewMode, 8);
        }

        /// <summary>Gets or sets whether lines behind faces are hidden when the viewport is plotted (flag 0x800), as AutoCAD's <c>HiddenLinesRemoved</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool HiddenLinesRemoved
        {
            get => Bit(0x800);
            set => SetBit(0x800, value, R12HidePlot, 1);
        }

        /// <summary>Gets or sets whether zooming is fast (flag 0x80), as AutoCAD's <c>FastZoomOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool FastZoomOn
        {
            get => Bit(0x80);
            set => SetBit(0x80, value, R12FastZoom, 1);
        }

        /// <summary>Gets or sets how many sides a circle is drawn with when zoomed (group 72), as AutoCAD's <c>CircleSides</c>: 1 to 20 000; 100 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 1 to 20 000.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public int CircleSides
        {
            get => !InMview ? GetInt(72, 100) : (int)R12Real(R12Circle, 100.0);

            set
            {
                if (value == CircleSides)
                {
                    return;
                }

                if (value < 1 || value > 20000)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A circle is drawn with 1 to 20 000 sides.");
                }

                if (InMview)
                {
                    SetR12(new[] { R12Circle }, new[] { value.ToString(CultureInfo.InvariantCulture) });
                    return;
                }

                SetString(72, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Gets the view along an axis the viewport shows, as AutoCAD's <c>ViewOrthographic</c>: <see cref="DxfOrthographicView.TopView"/>
        /// where <see cref="ViewDirection"/> is (0, 0, 1) to <see cref="Tolerance.Default"/>, and the five others likewise;
        /// <see cref="DxfOrthographicView.NonOrthoView"/> for any other direction.
        /// </summary>
        public DxfOrthographicView ViewOrthographic
        {
            get
            {
                GeoVector3 direction = ViewDirection;
                double length = direction.Length;

                if (!(length > 0.0))
                {
                    return DxfOrthographicView.NonOrthoView;
                }

                GeoVector3 unit = direction.Divide(length);

                for (int view = 1; view <= 6; view++)
                {
                    GeoVector3 axis = ViewOf((DxfOrthographicView)view);

                    if (unit.Subtract(axis).Length <= Tolerance.Default.EqualVector)
                    {
                        return (DxfOrthographicView)view;
                    }
                }

                return DxfOrthographicView.NonOrthoView;
            }
        }

        /// <summary>
        /// Points the view along an axis, as AutoCAD's <c>SetViewDirection</c>: from above (0, 0, 1), below (0, 0, -1), the front
        /// (0, -1, 0), the back (0, 1, 0), the left (-1, 0, 0) or the right (1, 0, 0).
        /// </summary>
        /// <param name="view">The view.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the view is not one of the six along an axis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public void SetViewDirection(DxfOrthographicView view)
        {
            if (view < DxfOrthographicView.TopView || view > DxfOrthographicView.RightView)
            {
                throw new ArgumentOutOfRangeException(nameof(view), view, "A view along an axis is one of the six from TopView to RightView.");
            }

            ViewDirection = ViewOf(view);
        }

        /// <summary>
        /// Gets or sets the scale of the view, as AutoCAD's <c>CustomScale</c>: the units of the sheet to the units of model space,
        /// <see cref="Height"/> over <see cref="ViewHeight"/>; 0.02 for a viewport 150 high showing 7 500 of model space.
        /// </summary>
        /// <value>Set, the view's height made the viewport's height over it, the centre of the view kept.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of no height, which has no scale.</exception>
        public double CustomScale
        {
            get
            {
                double view = ViewHeight;
                return view > 0.0 ? Height / view : 0.0;
            }

            set
            {
                if (Same(value, CustomScale))
                {
                    return;
                }

                Guard.Positive(value, nameof(value), "A scale has to be a finite number above nought.");

                if (!(Height > 0.0))
                {
                    throw new InvalidOperationException("A viewport of no height has no scale to set.");
                }

                ViewHeight = Height / value;
            }
        }

        /// <summary>
        /// Gets or sets the standard scale of the view, as AutoCAD's <c>StandardScale</c>: the one whose ratio is
        /// <see cref="CustomScale"/> to within a part in a thousand million, the first in the order of the type where two have it;
        /// <see cref="DxfStandardScaleType.CustomScale"/> where none has. A file keeps no standard scale of a viewport, so one
        /// scaled to fit reads as the scale it was fitted to.
        /// </summary>
        /// <value>Set, the view's height made that of the scale, as <see cref="CustomScale"/> sets it.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is <see cref="DxfStandardScaleType.ScaleToFit"/>,
        /// which needs AutoCAD's extents, <see cref="DxfStandardScaleType.CustomScale"/>, or no scale of the type.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of no height.</exception>
        public DxfStandardScaleType StandardScale
        {
            get
            {
                double scale = CustomScale;

                foreach (DxfStandardScaleType type in StandardTypes)
                {
                    double ratio = Ratio(type);

                    if (ratio > 0.0 && Math.Abs(scale - ratio) <= ratio * 1E-9)
                    {
                        return type;
                    }
                }

                return DxfStandardScaleType.CustomScale;
            }

            set
            {
                if (value == StandardScale)
                {
                    return;
                }

                double ratio = Ratio(value);

                if (!(ratio > 0.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A standard scale of a ratio is set; ScaleToFit and CustomScale are not one.");
                }

                CustomScale = ratio;
            }
        }

        #endregion

        #region Snap and grid

        /// <summary>Gets or sets whether the snap is on (flag 0x100), as AutoCAD's <c>SnapOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool SnapOn
        {
            get => Bit(0x100);
            set => SetBit(0x100, value, R12Snap, 1);
        }

        /// <summary>Gets or sets whether the snap is isometric (flag 0x400), as AutoCAD's <c>SnapIsometric</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool SnapIsometric
        {
            get => Bit(0x400);
            set => SetBit(0x400, value, R12SnapStyle, 1);
        }

        /// <summary>
        /// Gets or sets the plane of an isometric snap, as AutoCAD's <c>SnapIsoPair</c>: 0 left, 1 top (flag 0x1000 alone), 2 right
        /// (flag 0x2000 alone); both flags read as left.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public int SnapIsoPair
        {
            get
            {
                if (InMview)
                {
                    return (int)R12Real(R12IsoPair, 0.0);
                }

                int flags = Flags() & 0x3000;
                return flags == 0x1000 ? 1 : flags == 0x2000 ? 2 : 0;
            }

            set
            {
                if (value == SnapIsoPair)
                {
                    return;
                }

                if (value < 0 || value > 2)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The plane of an isometric snap is 0 left, 1 top or 2 right.");
                }

                if (InMview)
                {
                    SetR12(new[] { R12IsoPair }, new[] { value.ToString(CultureInfo.InvariantCulture) });
                    return;
                }

                SetGroups(FlagGroups((Flags() & ~0x3000) | (value == 1 ? 0x1000 : value == 2 ? 0x2000 : 0)));
            }
        }

        /// <summary>
        /// Gets or sets the angle of the snap, as AutoCAD's <c>SnapAngle</c>, in radians: group 50, in degrees, from R2000; in
        /// radians in the extended data MVIEW of a file of R12 to R14, as the twist is, AutoCAD 2021 writing a snap of 30 degrees
        /// there as 0.5235987755982988 (a 30 written there turns the snap by 30 radians).
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the angle set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public double SnapAngle
        {
            get => InMview ? R12Real(R12SnapAngle, 0.0) : GetDouble(50, 0.0) * Math.PI / 180.0;

            set
            {
                if (!InMview)
                {
                    SetAngle(50, R12SnapAngle, value, SnapAngle);
                }
                else if (!Same(value, SnapAngle))
                {
                    Guard.Finite(value, nameof(value), "An angle has to be a finite number.");
                    SetValue(50, R12SnapAngle, value);
                }
            }
        }

        /// <summary>Gets or sets the point the snap is laid from (groups 13, 23), as AutoCAD's <c>SnapBasePoint</c>.</summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoPoint2 SnapBasePoint
        {
            get
            {
                double[] p = Pair(13, R12SnapBaseX, 0.0);
                return new GeoPoint2(p[0], p[1]);
            }

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                SetPair(13, R12SnapBaseX, value.X, value.Y, 0.0);
            }
        }

        /// <summary>Gets or sets the spacing of the snap (groups 14, 24), as AutoCAD's <c>SnapIncrement</c>; 10 by 10 where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a part set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoVector2 SnapIncrement
        {
            get
            {
                double[] p = Pair(14, R12SnapX, 10.0);
                return new GeoVector2(p[0], p[1]);
            }

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("A spacing has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                SetPair(14, R12SnapX, value.X, value.Y, 10.0);
            }
        }

        /// <summary>Gets or sets whether the grid is shown (flag 0x200), as AutoCAD's <c>GridOn</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool GridOn
        {
            get => Bit(0x200);
            set => SetBit(0x200, value, R12Grid, 1);
        }

        /// <summary>Gets or sets the spacing of the grid (groups 15, 25), as AutoCAD's <c>GridIncrement</c>; 10 by 10 where the record gives none.</summary>
        /// <exception cref="ArgumentException">Thrown when a part set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public GeoVector2 GridIncrement
        {
            get
            {
                double[] p = Pair(15, R12GridX, 10.0);
                return new GeoVector2(p[0], p[1]);
            }

            set
            {
                if (!value.IsValid)
                {
                    throw new ArgumentException("A spacing has to be of finite numbers: " + value + " is not.", nameof(value));
                }

                SetPair(15, R12GridX, value.X, value.Y, 10.0);
            }
        }

        /// <summary>Gets or sets how many lines of the grid one major line is drawn for (group 61, from R2007), as AutoCAD's <c>GridMajor</c>; 5 where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number set is not from 1 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public short GridMajor
        {
            get => (short)GetInt(61, 5);

            set
            {
                if (value == GridMajor)
                {
                    return;
                }

                if (value < 1 || value > 100)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A major line of the grid is drawn every 1 to 100 lines.");
                }

                Item.File.Requires(1021, "the major lines of a viewport's grid", "R2007 (AC1021)");
                SetString(61, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets or sets whether the grid is kept to the limits: flag 0x40000 (grid beyond the limits) clear, as AutoCAD's <c>GridBoundToLimits</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool GridBoundToLimits
        {
            get => !Bit(0x40000);
            set => SetBit(0x40000, !value, -1, 0);
        }

        /// <summary>Gets or sets whether the grid adapts to the zoom (flag 0x80000), as AutoCAD's <c>GridAdaptive</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool GridAdaptive
        {
            get => Bit(0x80000);
            set => SetBit(0x80000, value, -1, 0);
        }

        /// <summary>Gets or sets whether an adaptive grid is kept from dividing below its spacing: flag 0x100000 (divide below the spacing) clear, as AutoCAD's <c>GridSubdivisionRestricted</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool GridSubdivisionRestricted
        {
            get => !Bit(0x100000);
            set => SetBit(0x100000, !value, -1, 0);
        }

        /// <summary>Gets or sets whether the grid follows the plane of the user coordinate system (flag 0x200000), as AutoCAD's <c>GridFollow</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool GridFollow
        {
            get => Bit(0x200000);
            set => SetBit(0x200000, value, -1, 0);
        }

        #endregion

        #region The user coordinate system

        /// <summary>Gets or sets whether the icon of the user coordinate system is shown (flag 0x20; bit 1 of the R12 icon setting), as AutoCAD's <c>UcsIconVisible</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool UcsIconVisible
        {
            get => Bit(0x20);
            set => SetBit(0x20, value, R12UcsIcon, 1);
        }

        /// <summary>Gets or sets whether the icon of the user coordinate system stands at its origin (flag 0x40; bit 2 of the R12 icon setting), as AutoCAD's <c>UcsIconAtOrigin</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public bool UcsIconAtOrigin
        {
            get => Bit(0x40);
            set => SetBit(0x40, value, R12UcsIcon, 2);
        }

        /// <summary>Gets or sets whether the viewport keeps a user coordinate system of its own (group 71 of 1), as AutoCAD's <c>UcsPerViewport</c>; false in a file older than R2000.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public bool UcsPerViewport
        {
            get => GetInt(71, 0) != 0;

            set
            {
                if (value != UcsPerViewport)
                {
                    Item.File.Requires(1015, "a viewport's own user coordinate system", "R2000 (AC1015)");
                    SetString(71, value ? "1" : "0");
                }
            }
        }

        /// <summary>
        /// Gets or sets the user coordinate system of the viewport, as AutoCAD's <c>Ucs</c>: its origin (groups 110, 120, 130)
        /// and its axes x (111, 121, 131) and y (112, 122, 132), made of unit length; the world's where the record gives none.
        /// </summary>
        /// <value>Set, as <see cref="SetUcs(GeoPoint3, GeoVector3, GeoVector3)"/> sets it of the system's origin and axes.</value>
        /// <exception cref="ArgumentNullException">Thrown when the system set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public GeoCoordinateSystem3 Ucs
        {
            get
            {
                GetUcs(out GeoPoint3 origin, out GeoVector3 x, out GeoVector3 y);
                return new GeoCoordinateSystem3(origin, x, y);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                SetUcs(value.Origin, value.XAxis, value.YAxis);
            }
        }

        /// <summary>
        /// Gives the user coordinate system of the viewport as the record holds it, as AutoCAD's <c>GetUcs</c>: its origin and its
        /// axes x and y, not made of unit length; the world's where the record gives none, or its axes are of no length or along
        /// one line.
        /// </summary>
        /// <param name="origin">The origin (groups 110, 120, 130).</param>
        /// <param name="x">The axis x (groups 111, 121, 131).</param>
        /// <param name="y">The axis y (groups 112, 122, 132).</param>
        public void GetUcs(out GeoPoint3 origin, out GeoVector3 x, out GeoVector3 y)
        {
            origin = GetPoint(110, Origin);
            GeoPoint3 a = GetPoint(111, new GeoPoint3(1.0, 0.0, 0.0));
            GeoPoint3 b = GetPoint(112, new GeoPoint3(0.0, 1.0, 0.0));
            x = new GeoVector3(a.X, a.Y, a.Z);
            y = new GeoVector3(b.X, b.Y, b.Z);

            if (!(x.CrossProduct(y).Length > 0.0))
            {
                x = new GeoVector3(1.0, 0.0, 0.0);
                y = new GeoVector3(0.0, 1.0, 0.0);
            }
        }

        /// <summary>
        /// Gives the viewport a user coordinate system of its own, as AutoCAD's <c>SetUcs</c> of an origin and two axes: they
        /// are written as given, the system named by none (345 and 346 taken out) and along no axis (79 of 0).
        /// </summary>
        /// <param name="origin">The origin.</param>
        /// <param name="x">The axis x.</param>
        /// <param name="y">The axis y, not along the axis x.</param>
        /// <exception cref="ArgumentException">Thrown when a point or axis is not of finite numbers, or the axes are of no length or along one line.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of a file older than R2000.</exception>
        public void SetUcs(GeoPoint3 origin, GeoVector3 x, GeoVector3 y) => WriteUcs(origin, x, y, DxfOrthographicView.NonOrthoView);

        /// <summary>
        /// Gives the viewport the user coordinate system of a view along an axis, as AutoCAD's <c>SetUcs</c> of an
        /// <c>OrthographicView</c>: about the world's origin, its plane that of the view, top (x along x, y along y), bottom (x,
        /// -y), front (x, z), back (-x, z), left (-y, z) or right (y, z); its view written (79), its base the world (346 taken out).
        /// </summary>
        /// <param name="view">The view.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the view is not one of the six along an axis.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of a file older than R2000.</exception>
        public void SetUcs(DxfOrthographicView view)
        {
            if (view < DxfOrthographicView.TopView || view > DxfOrthographicView.RightView)
            {
                throw new ArgumentOutOfRangeException(nameof(view), view, "A view along an axis is one of the six from TopView to RightView.");
            }

            GeoVector3 x;
            GeoVector3 y;

            switch (view)
            {
                case DxfOrthographicView.TopView: x = new GeoVector3(1.0, 0.0, 0.0); y = new GeoVector3(0.0, 1.0, 0.0); break;
                case DxfOrthographicView.BottomView: x = new GeoVector3(1.0, 0.0, 0.0); y = new GeoVector3(0.0, -1.0, 0.0); break;
                case DxfOrthographicView.FrontView: x = new GeoVector3(1.0, 0.0, 0.0); y = new GeoVector3(0.0, 0.0, 1.0); break;
                case DxfOrthographicView.BackView: x = new GeoVector3(-1.0, 0.0, 0.0); y = new GeoVector3(0.0, 0.0, 1.0); break;
                case DxfOrthographicView.LeftView: x = new GeoVector3(0.0, -1.0, 0.0); y = new GeoVector3(0.0, 0.0, 1.0); break;
                default: x = new GeoVector3(0.0, 1.0, 0.0); y = new GeoVector3(0.0, 0.0, 1.0); break;
            }

            WriteUcs(Origin, x, y, view);
        }

        /// <summary>Gives the viewport the world's coordinate system, as AutoCAD's <c>SetUcsToWorld</c>: as <see cref="SetUcs(GeoPoint3, GeoVector3, GeoVector3)"/> of the origin and the axes x and y.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of a file older than R2000.</exception>
        public void SetUcsToWorld() => WriteUcs(Origin, new GeoVector3(1.0, 0.0, 0.0), new GeoVector3(0.0, 1.0, 0.0), DxfOrthographicView.NonOrthoView);

        /// <summary>Gets the view along an axis the user coordinate system is of (group 79), as AutoCAD's <c>UcsOrthographic</c>.</summary>
        public DxfOrthographicView UcsOrthographic
        {
            get
            {
                int view = GetInt(79, 0);
                return view >= 0 && view <= 6 ? (DxfOrthographicView)view : DxfOrthographicView.NonOrthoView;
            }
        }

        /// <summary>Gets the name of the named user coordinate system of the viewport (its UCS record, group 345), as AutoCAD's <c>UcsName</c> gives its id; null where it is named by none.</summary>
        public string UcsName
        {
            get
            {
                string handle = GetString(345)?.Trim();

                if (string.IsNullOrEmpty(handle) || handle == "0")
                {
                    return null;
                }

                foreach (DxfItem record in Item.File.TableItems("UCS"))
                {
                    if (string.Equals(record.Handle, handle, StringComparison.OrdinalIgnoreCase))
                    {
                        return DxfReader.NameOf(Item.File.ValueOf(record, 2) ?? string.Empty);
                    }
                }

                return null;
            }
        }

        /// <summary>Gets or sets the elevation of the user coordinate system (group 146), as AutoCAD's <c>Elevation</c>; nought in a file older than R2000.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the elevation set is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public double Elevation
        {
            get => GetDouble(146, 0.0);

            set
            {
                if (!Same(value, Elevation))
                {
                    Guard.Finite(value, nameof(value), "An elevation has to be a finite number.");
                    Item.File.Requires(1015, "a viewport's elevation", "R2000 (AC1015)");
                    SetDouble(146, value, 0.0);
                }
            }
        }

        #endregion

        #region Layers frozen in the viewport

        /// <summary>
        /// Gets the layers frozen in the viewport, as AutoCAD's <c>GetFrozenLayers</c> gives their ids: those its groups 331 name
        /// by handle (the names of its extended data in a file older than R2000) that the layer table has, in their order.
        /// </summary>
        /// <returns>The layers, a new list.</returns>
        public IReadOnlyList<DxfLayerTableRecord> GetFrozenLayers()
        {
            var layers = new List<DxfLayerTableRecord>();
            DxfLayerTable table = Item.File.Database?.LayerTable;

            if (table == null)
            {
                return layers.AsReadOnly();
            }

            // The layers by their keys, found in one walk of the table: a dictionary to find them, the keys giving the order.
            var byKey = new Dictionary<string, DxfLayerTableRecord>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfLayerTableRecord layer in table)
            {
                string own = InMview ? layer.Name : layer.Handle;

                if (own != null && !byKey.ContainsKey(own))
                {
                    byKey.Add(own, layer);
                }
            }

            var seen = new HashSet<DxfLayerTableRecord>();

            foreach (string key in FrozenKeys())
            {
                if (byKey.TryGetValue(key, out DxfLayerTableRecord layer) && seen.Add(layer))
                {
                    layers.Add(layer);
                }
            }

            return layers.AsReadOnly();
        }

        /// <summary>Gets whether a layer is frozen in the viewport, as AutoCAD's <c>IsLayerFrozenInViewport</c>.</summary>
        /// <param name="layer">The layer.</param>
        /// <returns>Whether it is.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the layer is null.</exception>
        public bool IsLayerFrozenInViewport(DxfLayerTableRecord layer)
        {
            if (layer == null)
            {
                throw new ArgumentNullException(nameof(layer));
            }

            string key = InMview ? layer.Name : layer.Handle;
            return key != null && layer.IsResident && layer.Item.File == Item.File && FrozenKeys().Exists(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Freezes layers in the viewport, as AutoCAD's <c>FreezeLayersInViewport</c>: each not frozen in it yet named after those
        /// that are, by its handle (331), or by its name in the extended data of a file older than R2000; nothing where all are.
        /// </summary>
        /// <param name="layers">The layers, of the viewport's drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the layers or one of them are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a layer is not of the viewport's drawing, or is erased.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is in no drawing.</exception>
        public void FreezeLayersInViewport(IEnumerable<DxfLayerTableRecord> layers)
        {
            List<DxfLayerTableRecord> given = Layers(layers, nameof(layers));
            var frozen = new List<DxfLayerTableRecord>(GetFrozenLayers());
            var already = new HashSet<DxfLayerTableRecord>(frozen);
            bool changed = false;

            foreach (DxfLayerTableRecord layer in given)
            {
                if (already.Add(layer))
                {
                    frozen.Add(layer);
                    changed = true;
                }
            }

            if (changed)
            {
                WriteFrozen(frozen);
            }
        }

        /// <summary>Thaws layers in the viewport, as AutoCAD's <c>ThawLayersInViewport</c>; nothing for one not frozen in it.</summary>
        /// <param name="layers">The layers, of the viewport's drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the layers or one of them are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a layer is not of the viewport's drawing, or is erased.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is in no drawing.</exception>
        public void ThawLayersInViewport(IEnumerable<DxfLayerTableRecord> layers)
        {
            List<DxfLayerTableRecord> given = Layers(layers, nameof(layers));
            var frozen = new List<DxfLayerTableRecord>(GetFrozenLayers());

            var thawed = new HashSet<DxfLayerTableRecord>(given);

            if (frozen.RemoveAll(thawed.Contains) > 0)
            {
                WriteFrozen(frozen);
            }
        }

        /// <summary>Thaws every layer in the viewport, as AutoCAD's <c>ThawAllLayersInViewport</c>; nothing where none is frozen in it.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased.</exception>
        public void ThawAllLayersInViewport()
        {
            if (FrozenKeys().Count > 0)
            {
                WriteFrozen(new List<DxfLayerTableRecord>());
            }
        }

        #endregion

        #region Plotting and shading

        /// <summary>Gets or sets the plot style table of the viewport (group 1), as AutoCAD's <c>PlotStyleSheet</c>; empty where it has none of its own.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name set has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2000.</exception>
        public string PlotStyleSheet
        {
            get => DxfReader.Decode(GetString(1) ?? string.Empty);

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, PlotStyleSheet, StringComparison.Ordinal))
                {
                    return;
                }

                if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
                {
                    throw new ArgumentException("The name of a plot style table is one line.", nameof(value));
                }

                Item.File.Requires(1015, "a viewport's plot style table", "R2000 (AC1015)");
                SetString(1, value);
            }
        }

        /// <summary>
        /// Gets the plot style table the viewport is plotted with, as AutoCAD's <c>EffectivePlotStyleSheet</c>: its own
        /// <see cref="PlotStyleSheet"/>, else that of the layout whose block holds it.
        /// </summary>
        public string EffectivePlotStyleSheet
        {
            get
            {
                string own = PlotStyleSheet;

                if (own.Length > 0 || Item.File.Database == null || !(Owner is DxfBlockTableRecord block))
                {
                    return own;
                }

                foreach (DxfLayout layout in Item.File.Database.Layouts)
                {
                    if (ReferenceEquals(layout.BlockTableRecord, block))
                    {
                        return layout.CurrentStyleSheet;
                    }
                }

                return own;
            }
        }

        /// <summary>Gets or sets how the viewport is shaded when plotted (group 170, from R2004), as AutoCAD's <c>ShadePlot</c>.</summary>
        /// <value>Set, one that needs no object: as displayed, wireframe, hidden or rendered; the others by <see cref="SetShadePlot"/>.</value>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is a visual style or a preset of rendering, which name an object, or is none of the type.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2004.</exception>
        public DxfShadePlotType ShadePlot
        {
            get => (DxfShadePlotType)GetInt(170, 0);

            set
            {
                if (value == ShadePlot)
                {
                    return;
                }

                if (value < DxfShadePlotType.AsDisplayed || value > DxfShadePlotType.Rendered)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "A visual style or a preset of rendering is set by SetShadePlot with its object.");
                }

                SetShadePlot(value, null);
            }
        }

        /// <summary>
        /// Sets how the viewport is shaded when plotted, as AutoCAD's <c>SetShadePlot</c>: the type (group 170) and the visual style
        /// or preset of rendering it names (333), taken out for a type that names none.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="shadePlot">The visual style or preset of rendering of the drawing, for those two types; null for the others.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type is none of the type.</exception>
        /// <exception cref="ArgumentException">Thrown when a visual style or preset is given no object of the drawing, or another type is given one.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or is of a file older than R2004.</exception>
        public void SetShadePlot(DxfShadePlotType type, DxfDBObject shadePlot)
        {
            if (type < DxfShadePlotType.AsDisplayed || type > DxfShadePlotType.RenderPreset)
            {
                throw new ArgumentOutOfRangeException(nameof(type), type, "The type is one of DxfShadePlotType.");
            }

            bool named = type == DxfShadePlotType.VisualStyle || type == DxfShadePlotType.RenderPreset;

            if (named != (shadePlot != null) || (shadePlot != null && (!shadePlot.IsResident || shadePlot.Item.File != Item.File || shadePlot.Handle == null)))
            {
                throw new ArgumentException("A visual style or a preset of rendering names an object of the viewport's drawing, and the other types none.", nameof(shadePlot));
            }

            string handle = shadePlot?.Handle;

            if (type == ShadePlot && string.Equals(handle, GetString(333)?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Item.File.Requires(1018, "the shading of a viewport's plot", "R2004 (AC1018)");
            SetGroups(new KeyValuePair<int, string>(170, ((int)type).ToString(CultureInfo.InvariantCulture)), new KeyValuePair<int, string>(333, handle));
        }

        /// <summary>
        /// Gets whether the viewport is plotted as wires, as AutoCAD's <c>PlotWireframe</c>: its <see cref="ShadePlot"/>
        /// wireframe, or a visual style of the drawing it names (group 333) of 2D or 3D wireframe; or, plotted as displayed, so
        /// shown: by the visual style it is shown in (348), else by its render mode (281) of 2D optimized or wireframe. A viewport
        /// of ezdxf's, plotted as displayed and shown in 2D, is plotted as wires.
        /// </summary>
        /// <remarks>
        /// A viewport plotted with the lines behind hidden, by the shading hidden, a hidden visual style, a render mode hidden or,
        /// where it gives no group 170, as a viewport of R2000 gives none, by its flag 0x800 (<see cref="HiddenLinesRemoved"/>),
        /// is plotted neither as wires nor as a raster image, as AutoCAD plots it in lines; every other shading, a rendered one
        /// among them, as a raster image.
        /// </remarks>
        public bool PlotWireframe => PlotOutput() == PlottedAsWires;

        /// <summary>
        /// Gets whether the viewport is plotted as a raster image, as AutoCAD's <c>PlotAsRaster</c>: its <see cref="ShadePlot"/>
        /// rendered or a preset of rendering, or a visual style of any type but 2D wireframe, wireframe and hidden (4, 5 and 6),
        /// as Realistic or Conceptual; or, plotted as displayed, so shown, by the visual style it is shown in (348), else by its
        /// render mode (281) of flat or Gouraud shading. Neither this nor <see cref="PlotWireframe"/> holds for a viewport
        /// plotted with the lines behind hidden.
        /// </summary>
        public bool PlotAsRaster => PlotOutput() == PlottedAsRaster;

        /// <summary>Gets or sets whether the viewport is lit by the default light (group 292, from R2007), as AutoCAD's <c>DefaultLightingOn</c>; true where the record gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public bool DefaultLightingOn
        {
            get => GetInt(292, 1) != 0;

            set
            {
                if (value != DefaultLightingOn)
                {
                    Item.File.Requires(1021, "a viewport's lighting", "R2007 (AC1021)");
                    SetString(292, value ? "1" : "0");
                }
            }
        }

        /// <summary>Gets or sets the default light of the viewport (group 282, from R2007), as AutoCAD's <c>DefaultLightingType</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the type set is none of the type.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public DxfDefaultLightingType DefaultLightingType
        {
            get => (DxfDefaultLightingType)GetInt(282, 0);

            set
            {
                if (value == DefaultLightingType)
                {
                    return;
                }

                if (value != DxfDefaultLightingType.OneDistantLight && value != DxfDefaultLightingType.TwoDistantLights)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The default light is one distant light or two.");
                }

                Item.File.Requires(1021, "a viewport's lighting", "R2007 (AC1021)");
                SetString(282, ((int)value).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets or sets the brightness of the view (group 141, from R2007), as AutoCAD's <c>Brightness</c>: -100 to 100; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from -100 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public double Brightness
        {
            get => GetDouble(141, 0.0);
            set => SetLight(141, value, Brightness);
        }

        /// <summary>Gets or sets the contrast of the view (group 142, from R2007), as AutoCAD's <c>Contrast</c>: -100 to 100; nought where the record gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from -100 to 100.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public double Contrast
        {
            get => GetDouble(142, 0.0);
            set => SetLight(142, value, Contrast);
        }

        /// <summary>
        /// Gets or sets the colour of the ambient light (groups 63, 421 and 431, from R2007), as AutoCAD's <c>AmbientLightColor</c>:
        /// its true colour where it has one, else its colour of the index; the index 250, a dark grey, where the record gives none.
        /// </summary>
        /// <value>Set, a colour of the index or a true colour, written as AutoCAD writes an entity's.</value>
        /// <exception cref="ArgumentNullException">Thrown when the colour set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour set is not of the index or a true colour.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is erased, or set in a file older than R2007.</exception>
        public DxfColor AmbientLightColor
        {
            get
            {
                if (int.TryParse(GetString(421)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rgb))
                {
                    return DxfColor.OfRgb(rgb & 0xFFFFFF, GetString(431));
                }

                return DxfColor.OfIndex(GetInt(63, 250));
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Equals(AmbientLightColor))
                {
                    return;
                }

                if (value.ColorMethod != DxfColorMethod.ByAci && value.ColorMethod != DxfColorMethod.ByColor)
                {
                    throw new ArgumentException("An ambient light is of a colour of the index or a true colour.", nameof(value));
                }

                Item.File.Requires(1021, "a viewport's lighting", "R2007 (AC1021)");
                bool rgb = value.ColorMethod == DxfColorMethod.ByColor;
                SetGroups(
                    new KeyValuePair<int, string>(63, value.ColorIndex.ToString(CultureInfo.InvariantCulture)),
                    new KeyValuePair<int, string>(421, rgb ? value.ColorValue.ToString(CultureInfo.InvariantCulture) : null),
                    new KeyValuePair<int, string>(431, null));
            }
        }

        /// <summary>Gets the background of the view (group 332), as AutoCAD's <c>Background</c> gives its id: the object of the OBJECTS section it names; null where it names none.</summary>
        public DxfDBObject Background
        {
            get
            {
                DxfItem background = Item.File.ObjectItem(GetString(332));
                return background == null ? null : OfObject(background);
            }
        }

        /// <summary>
        /// Gets the sun the viewport is lit by (group 361, from R2007), as AutoCAD's <c>SunId</c> gives its id: the SUN it names;
        /// null where it names none, or one the file has not.
        /// </summary>
        public DxfSun Sun
        {
            get
            {
                DxfItem sun = Item.File.ObjectItem(GetString(361));
                return sun == null ? null : OfObject(sun) as DxfSun;
            }
        }

        /// <summary>
        /// Lights the viewport by a sun, as AutoCAD's <c>SetSun</c>: the sun made in code written at the end of the OBJECTS
        /// section, owned by the viewport, which names it (group 361), and the class SUN listed in the CLASSES section where it
        /// is not; the sun it had erased where the viewport owned it, as AutoCAD erases the old sun, and kept where another owns
        /// it. <c>viewport.SetSun(new DxfSun { Intensity = 1.5 })</c> gives a viewport AutoCAD's sun half as
        /// bright again.
        /// </summary>
        /// <param name="sun">The sun, made in code and in no file yet.</param>
        /// <returns>The sun, in the viewport's drawing from then on, as AutoCAD gives its id.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the sun is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the sun is in a file already.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is in no file, is erased, or is of a file older
        /// than R2007, which has no SUN.</exception>
        public DxfSun SetSun(DxfSun sun)
        {
            if (sun == null)
            {
                throw new ArgumentNullException(nameof(sun));
            }

            if (sun.Item != null)
            {
                throw new ArgumentException("A viewport is given a sun made in code and in no file yet: the " + sun + " is in one.", nameof(sun));
            }

            DxfFile file = InFile("be given a sun");
            file.Requires(1021, "SUN", "R2007 (AC1021)");
            DxfItem old = file.ObjectItem(GetString(361));
            DxfItem made = file.AddObject("SUN", Handle, b =>
            {
                b.Group(100, "AcDbSun");

                foreach (KeyValuePair<int, string> group in sun.MadeGroups)
                {
                    b.Group(group.Key, group.Value);
                }
            });

            // A drawing that lists no class SUN is given it, as AutoCAD lists it: its application SCENEOE, its flags 1153.
            file.AddClass("SUN", "AcDbSun", 1153, "SCENEOE");
            sun.Bind(made);
            SetString(361, made.Handle);

            if (old != null && old.Type == "SUN" && string.Equals(file.OwnerOf(old), Handle, StringComparison.OrdinalIgnoreCase))
            {
                file.RemoveRecord(old);
            }

            return sun;
        }

        #endregion

        /// <summary>
        /// Erases the viewport, as <see cref="DxfEntity.Erase"/> erases an entity, and takes the overrides the layers keep for it
        /// out of their extension dictionaries, as AutoCAD does: an XRECORD of overrides left with none goes with its entry. A
        /// layout whose last active viewport it was (group 331 of its LAYOUT) names its viewport 1 instead, as AutoCAD 2021 does,
        /// or none where it has no other. The viewport of
        /// id 1 (group 69) of a layout of paper space, which shows the sheet itself, is refused, as AutoCAD keeps it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport is in no file, or is erased, or is the viewport 1
        /// of a layout of paper space.</exception>
        public override void Erase()
        {
            string handle = Handle;
            bool resident = IsResident && !IsErased;

            if (resident && Id == 1 && Owner is DxfBlockTableRecord block && block.IsLayout && !DxfBlockTableRecord.IsModelName(block.Name ?? string.Empty))
            {
                throw new InvalidOperationException("The viewport " + (handle ?? string.Empty) + " is the viewport 1 of the layout " + block.Name + ", which shows the sheet itself, and AutoCAD does not erase it.");
            }

            DxfDatabase database = resident && handle != null ? Item.File.Database : null;
            base.Erase();

            if (database == null)
            {
                return;
            }

            foreach (DxfLayerTableRecord layer in database.LayerTable)
            {
                DxfLayerViewportProperties.Forget(layer, handle);
            }

            foreach (DxfLayout layout in database.Layouts)
            {
                layout.ForgetViewport(handle);
            }
        }

        /// <summary>Gives the viewport as a line for debugging: its type, handle, id and size.</summary>
        /// <returns>The line, such as <c>VIEWPORT 9D 3, 200 by 150</c>.</returns>
        public override string ToString()
            => "VIEWPORT " + (Handle ?? string.Empty) + " " + Id.ToString(CultureInfo.InvariantCulture) + ", " + Width.ToString("R", CultureInfo.InvariantCulture) + " by " + Height.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The id of the viewport in its layout (group 69), on or off: 1 for the viewport that shows the sheet itself.</summary>
        internal int Id => GetInt(69, 0);

        /// <summary>
        /// Moves and scales the viewport on its sheet, as AutoCAD's MOVE and SCALE: its centre moved, its width and height scaled,
        /// the view in it kept. A transformation that turns, mirrors or stretches unevenly is refused, as AutoCAD refuses it for a
        /// viewport: turn the view by <see cref="TwistAngle"/>.
        /// </summary>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            double scale = Math.Sqrt(transform[0, 0] * transform[0, 0] + transform[1, 0] * transform[1, 0] + transform[2, 0] * transform[2, 0]);
            double slack = Tolerance.Default.EqualVector * Math.Max(1.0, scale);

            for (int c = 0; c < 3; c++)
            {
                for (int r = 0; r < 3; r++)
                {
                    if (!(Math.Abs(transform[r, c] - (r == c ? scale : 0.0)) <= slack))
                    {
                        throw new ArgumentException("A viewport is moved and scaled alike in every direction, as AutoCAD moves and scales one: the transformation turns, mirrors or stretches it.", nameof(transform));
                    }
                }
            }

            GeoPoint3 center = transform.Transform(CenterPoint);
            SetGroups(Join(
                PointGroups(10, center, Origin),
                Same(Width * scale, Width) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(40, Real(Width * scale)) },
                Same(Height * scale, Height) ? new KeyValuePair<int, string>[0] : new[] { new KeyValuePair<int, string>(41, Real(Height * scale)) }));
        }

        /// <summary>
        /// Checks that a viewport made in code goes to a layout of paper space of a file of R2000 or later, as AutoCAD keeps them
        /// there, and gives it the first id from 2 up its layout has not (group 69), as AutoCAD numbers one; one turned on before
        /// it is appended stacked above the viewports of the layout (group 68).
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the block is not a layout of paper space, or its file is older than R2000, whose viewports keep their view in extended data the library does not make.</exception>
        internal override void Appending(DxfBlockTableRecord owner)
        {
            if (!owner.IsLayout || DxfBlockTableRecord.IsModelName(owner.Name ?? string.Empty) || owner.File == null || !owner.File.Supports(1015))
            {
                throw new ArgumentException("A viewport is appended to a layout of paper space of a file of R2000 or later: " + owner.Name + " is not one.", nameof(owner));
            }

            var numbers = new HashSet<int>();

            foreach (DxfEntity entity in owner)
            {
                if (entity is DxfViewport viewport)
                {
                    numbers.Add(viewport.Id);
                }
            }

            // The first id from 2 up the layout has not, as AutoCAD numbers a new viewport; a viewport turned on before it was
            // appended stacked above those of the layout.
            int next = 2;

            while (numbers.Contains(next))
            {
                next++;
            }

            SetString(69, next.ToString(CultureInfo.InvariantCulture));

            if (GetInt(68, 1) != 0)
            {
                SetString(68, NextStatus(owner).ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// Makes a viewport just appended to a layout the one that shows the sheet itself, as AutoCAD's <c>Initialize</c> of a
        /// layout makes it: its id 1, its status 1 and AutoCAD's flags of it, 557 088 (0x80000 adaptive grid, 0x8000 always
        /// enabled, 0x20 the icon of the user coordinate system shown), as ezdxf writes them.
        /// </summary>
        internal void MakeSheetViewport()
            => SetGroups(new KeyValuePair<int, string>(68, "1"), new KeyValuePair<int, string>(69, "1"), new KeyValuePair<int, string>(90, "557088"));

        /// <summary>Moves a viewport stacked on (status 1 or more, group 68) up one, for a viewport 1 put first; one off or off screen stays.</summary>
        internal void MoveUpInStack()
        {
            int status = GetInt(68, 0);

            if (status >= 1)
            {
                SetString(68, (status + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        // How a viewport is plotted, as PlotOutput says it.
        private const int PlottedAsWires = 0;
        private const int PlottedHidden = 1;
        private const int PlottedAsRaster = 2;

        /// <summary>
        /// How the viewport is plotted: as wires, with the lines behind hidden, or as a raster image; by its shading of the plot
        /// (170), and where that is as displayed, or a visual style the drawing has not, by how it is shown.
        /// </summary>
        private int PlotOutput()
        {
            switch (ShadePlot)
            {
                case DxfShadePlotType.Wireframe:
                    return PlottedAsWires;
                case DxfShadePlotType.Hidden:
                    return PlottedHidden;
                case DxfShadePlotType.Rendered:
                case DxfShadePlotType.RenderPreset:
                    return PlottedAsRaster;
                case DxfShadePlotType.VisualStyle:
                    int named = StyleOutput(GetString(333));

                    if (named >= 0)
                    {
                        return named;
                    }

                    break;
            }

            // A viewport of R2000 says the lines behind are hidden in the plot by the flag 0x800 alone: it has no 170.
            if (GetString(170) == null && Bit(0x800))
            {
                return PlottedHidden;
            }

            int shown = StyleOutput(GetString(348));

            if (shown >= 0)
            {
                return shown;
            }

            // The render mode: 0 2D optimized and 1 wireframe, 2 hidden, 3 to 6 flat or Gouraud shaded, with wires or not.
            int mode = GetInt(281, 0);
            return mode <= 1 ? PlottedAsWires : mode == 2 ? PlottedHidden : PlottedAsRaster;
        }

        /// <summary>
        /// How a viewport shown or plotted in a visual style is plotted, by the style's type (group 70 of its VISUALSTYLE): 4 2D
        /// wireframe and 5 wireframe as wires, 6 hidden with the lines behind hidden, every other type as a raster image; -1 where
        /// the drawing has no visual style of the handle, or it gives no type.
        /// </summary>
        private int StyleOutput(string handle)
        {
            DxfItem style = Item.File.ObjectItem(handle);

            if (style == null || style.Type != "VISUALSTYLE" || !int.TryParse(Item.File.ValueOf(style, 70)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
            {
                return -1;
            }

            return type == 4 || type == 5 ? PlottedAsWires : type == 6 ? PlottedHidden : PlottedAsRaster;
        }

        /// <summary>The standard scales of a ratio, in the order of the type.</summary>
        private static readonly DxfStandardScaleType[] StandardTypes = (DxfStandardScaleType[])Enum.GetValues(typeof(DxfStandardScaleType));

        /// <summary>The ratio of a standard scale, the sheet's units to the drawing's; nought for one scaled to fit or of a scale of its own.</summary>
        private static double Ratio(DxfStandardScaleType type)
        {
            switch (type)
            {
                case DxfStandardScaleType.Scale1To1: return 1.0;
                case DxfStandardScaleType.Scale1To2: return 1.0 / 2.0;
                case DxfStandardScaleType.Scale1To4: return 1.0 / 4.0;
                case DxfStandardScaleType.Scale1To5: return 1.0 / 5.0;
                case DxfStandardScaleType.Scale1To8: return 1.0 / 8.0;
                case DxfStandardScaleType.Scale1To10: return 1.0 / 10.0;
                case DxfStandardScaleType.Scale1To16: return 1.0 / 16.0;
                case DxfStandardScaleType.Scale1To20: return 1.0 / 20.0;
                case DxfStandardScaleType.Scale1To30: return 1.0 / 30.0;
                case DxfStandardScaleType.Scale1To40: return 1.0 / 40.0;
                case DxfStandardScaleType.Scale1To50: return 1.0 / 50.0;
                case DxfStandardScaleType.Scale1To100: return 1.0 / 100.0;
                case DxfStandardScaleType.Scale2To1: return 2.0;
                case DxfStandardScaleType.Scale4To1: return 4.0;
                case DxfStandardScaleType.Scale8To1: return 8.0;
                case DxfStandardScaleType.Scale10To1: return 10.0;
                case DxfStandardScaleType.Scale100To1: return 100.0;
                case DxfStandardScaleType.Scale1To128inAnd1ft: return 1.0 / 1536.0;
                case DxfStandardScaleType.Scale1To64inchAnd1ft: return 1.0 / 768.0;
                case DxfStandardScaleType.Scale1To32inchAnd1ft: return 1.0 / 384.0;
                case DxfStandardScaleType.Scale1To16inchAnd1ft: return 1.0 / 192.0;
                case DxfStandardScaleType.Scale3To32inchAnd1ft: return 1.0 / 128.0;
                case DxfStandardScaleType.Scale1To8inchAnd1ft: return 1.0 / 96.0;
                case DxfStandardScaleType.Scale3To16inchAnd1ft: return 1.0 / 64.0;
                case DxfStandardScaleType.Scale1To4inchAnd1ft: return 1.0 / 48.0;
                case DxfStandardScaleType.Scale3To8inchAnd1ft: return 1.0 / 32.0;
                case DxfStandardScaleType.Scale1To2inchAnd1ft: return 1.0 / 24.0;
                case DxfStandardScaleType.Scale3To4inchAnd1ft: return 1.0 / 16.0;
                case DxfStandardScaleType.Scale1inAnd1ft: return 1.0 / 12.0;
                case DxfStandardScaleType.Scale3inAnd1ft: return 1.0 / 4.0;
                case DxfStandardScaleType.Scale6inAnd1ft: return 1.0 / 2.0;
                case DxfStandardScaleType.Scale1ftAnd1ft: return 1.0;
                default: return 0.0;
            }
        }

        /// <summary>The direction from the target to the eye of a view along an axis.</summary>
        private static GeoVector3 ViewOf(DxfOrthographicView view)
        {
            switch (view)
            {
                case DxfOrthographicView.TopView: return new GeoVector3(0.0, 0.0, 1.0);
                case DxfOrthographicView.BottomView: return new GeoVector3(0.0, 0.0, -1.0);
                case DxfOrthographicView.FrontView: return new GeoVector3(0.0, -1.0, 0.0);
                case DxfOrthographicView.BackView: return new GeoVector3(0.0, 1.0, 0.0);
                case DxfOrthographicView.LeftView: return new GeoVector3(-1.0, 0.0, 0.0);
                default: return new GeoVector3(1.0, 0.0, 0.0);
            }
        }

        private static string Real(double value) => Numbers.Exact(value + 0.0);

        /// <summary>Whether the view is kept in the extended data MVIEW, as a file before R2000 keeps it.</summary>
        private bool InMview => !Item.File.Supports(1015);

        /// <summary>The flags of group 90; before R2000, those its extended data keeps, gathered at the places of group 90's.</summary>
        private int Flags()
        {
            if (!InMview)
            {
                return GetInt(90, 0);
            }

            int mode = (int)R12Real(R12ViewMode, 0.0);
            int icon = (int)R12Real(R12UcsIcon, 0.0);
            int flags = mode & 0x1F;
            flags |= (icon & 1) != 0 ? 0x20 : 0;
            flags |= (icon & 2) != 0 ? 0x40 : 0;
            flags |= R12Real(R12FastZoom, 0.0) != 0.0 ? 0x80 : 0;
            flags |= R12Real(R12Snap, 0.0) != 0.0 ? 0x100 : 0;
            flags |= R12Real(R12Grid, 0.0) != 0.0 ? 0x200 : 0;
            flags |= R12Real(R12SnapStyle, 0.0) != 0.0 ? 0x400 : 0;
            flags |= R12Real(R12HidePlot, 0.0) != 0.0 ? 0x800 : 0;
            return flags;
        }

        private bool Bit(int bit) => (Flags() & bit) != 0;

        /// <summary>The group 90 of flags, its padding kept as the file pads it.</summary>
        private static KeyValuePair<int, string>[] FlagGroups(int flags) => new[] { new KeyValuePair<int, string>(90, flags.ToString(CultureInfo.InvariantCulture)) };

        /// <summary>
        /// Sets a flag of group 90; before R2000, the bit of the value at a place of its extended data (none where the place is -1,
        /// which those have not). Nothing where the flag is as it was.
        /// </summary>
        private void SetBit(int bit, bool value, int r12Place, int r12Bit)
        {
            if (value == Bit(bit))
            {
                return;
            }

            if (!InMview)
            {
                SetGroups(FlagGroups(value ? Flags() | bit : Flags() & ~bit));
                return;
            }

            if (r12Place < 0)
            {
                throw new InvalidOperationException("A viewport of a file before R2000 has no such flag: R2000 (AC1015) brought it.");
            }

            int now = (int)R12Real(r12Place, 0.0);
            int next = value ? now | r12Bit : now & ~r12Bit;
            SetR12(new[] { r12Place }, new[] { next.ToString(CultureInfo.InvariantCulture) });
        }

        /// <summary>The status a viewport turned on is given: the next above those of the other viewports of its block, 1 where it has none.</summary>
        private int NextStatus() => NextStatus(Owner as DxfBlockTableRecord);

        /// <summary>The status a viewport turned on in a block is given: the next above those of the other viewports of the block, 1 where it has none.</summary>
        private int NextStatus(DxfBlockTableRecord block)
        {
            int most = 0;

            if (block != null)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfViewport viewport && !ReferenceEquals(viewport, this))
                    {
                        most = Math.Max(most, viewport.GetInt(68, 0));
                    }
                }
            }

            return most + 1;
        }

        /// <summary>A real of the view: its group from R2000 on, the value at its place of the extended data before R2000.</summary>
        private double Value(int code, int r12Place, double fallback) => !InMview ? GetDouble(code, fallback) : R12Real(r12Place, fallback);

        /// <summary>Sets a real of the view where it is kept, as <see cref="Value"/> reads it.</summary>
        private void SetValue(int code, int r12Place, double value)
        {
            if (!InMview)
            {
                SetString(code, Real(value));
                return;
            }

            SetR12(new[] { r12Place }, new[] { Real(value) });
        }

        private void SetFinite(int code, int r12Place, double value, double now)
        {
            if (!Same(value, now))
            {
                Guard.Finite(value, nameof(value), "The value has to be a finite number.");
                SetValue(code, r12Place, value);
            }
        }

        private void SetAngle(int code, int r12Place, double value, double now)
        {
            if (!Same(value, now))
            {
                Guard.Finite(value, nameof(value), "An angle has to be a finite number.");
                SetValue(code, r12Place, value * 180.0 / Math.PI);
            }
        }

        private void SetSize(int code, double value, double now)
        {
            if (!Same(value, now))
            {
                Guard.NonNegative(value, nameof(value), "A size has to be a finite number of nought or more.");
                SetString(code, Real(value));
            }
        }

        private void SetLight(int code, double value, double now)
        {
            if (Same(value, now))
            {
                return;
            }

            if (!(value >= -100.0 && value <= 100.0))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The value is from -100 to 100.");
            }

            Item.File.Requires(1021, "a viewport's lighting", "R2007 (AC1021)");
            SetString(code, Real(value));
        }

        /// <summary>Two reals of a pair of groups (code and code + 10), before R2000 the values at two places; the fallback for each the record gives not.</summary>
        private double[] Pair(int code, int r12Place, double fallback)
            => !InMview
                ? new[] { GetDouble(code, fallback), GetDouble(code + 10, fallback) }
                : new[] { R12Real(r12Place, fallback), R12Real(r12Place + 1, fallback) };

        /// <summary>Sets two reals of a pair of groups, as <see cref="Pair"/> reads them; nothing where both are as they were.</summary>
        private void SetPair(int code, int r12Place, double x, double y, double fallback)
        {
            double[] now = Pair(code, r12Place, fallback);

            if (Same(x, now[0]) && Same(y, now[1]))
            {
                return;
            }

            if (InMview)
            {
                SetR12(new[] { r12Place, r12Place + 1 }, new[] { Real(x), Real(y) });
                return;
            }

            SetGroups(new KeyValuePair<int, string>(code, Real(x)), new KeyValuePair<int, string>(code + 10, Real(y)));
        }

        private void WriteUcs(GeoPoint3 origin, GeoVector3 x, GeoVector3 y, DxfOrthographicView view)
        {
            if (!origin.IsValid || !x.IsValid || !y.IsValid || !(x.CrossProduct(y).Length > 0.0))
            {
                throw new ArgumentException("A coordinate system has an origin and two axes of finite numbers, of some length and not along one line.", nameof(x));
            }

            var groups = new List<KeyValuePair<int, string>>(Join(
                PointGroups(110, origin, Origin),
                PointGroups(111, new GeoPoint3(x.X, x.Y, x.Z), new GeoPoint3(1.0, 0.0, 0.0)),
                PointGroups(112, new GeoPoint3(y.X, y.Y, y.Z), new GeoPoint3(0.0, 1.0, 0.0))));

            foreach (int code in new[] { 345, 346 })
            {
                if (GetString(code) != null)
                {
                    groups.Add(new KeyValuePair<int, string>(code, null));
                }
            }

            if (UcsOrthographic != view)
            {
                groups.Add(new KeyValuePair<int, string>(79, ((int)view).ToString(CultureInfo.InvariantCulture)));
            }

            // The system the viewport has changes nothing, whatever the file's version.
            if (groups.Count == 0)
            {
                return;
            }

            Item.File.Requires(1015, "a viewport's user coordinate system", "R2000 (AC1015)");
            SetGroups(groups.ToArray());
        }

        /// <summary>The keys of the layers frozen in the viewport: their handles (331) from R2000 on, their names (1003 of the extended data) before R2000.</summary>
        private List<string> FrozenKeys()
        {
            var keys = new List<string>();

            if (!InMview)
            {
                foreach (DxfTypedValue group in GetOwnGroups(0))
                {
                    if (group.TypeCode == 331)
                    {
                        keys.Add(group.Value.Trim());
                    }
                }

                return keys;
            }

            List<DxfTypedValue> data = Mview(out int start, out int end);

            for (int i = start; i >= 0 && i < end; i++)
            {
                if (data[i].TypeCode == 1003)
                {
                    keys.Add(data[i].Value);
                }
            }

            return keys;
        }

        /// <summary>The layers given, checked to be of the viewport's drawing.</summary>
        private List<DxfLayerTableRecord> Layers(IEnumerable<DxfLayerTableRecord> layers, string parameter)
        {
            if (layers == null)
            {
                throw new ArgumentNullException(parameter);
            }

            if (!IsResident)
            {
                throw new InvalidOperationException("The viewport is in no drawing whose layers it could freeze: append it to a layout first.");
            }

            var given = new List<DxfLayerTableRecord>();

            foreach (DxfLayerTableRecord layer in layers)
            {
                if (layer == null)
                {
                    throw new ArgumentNullException(parameter);
                }

                if (!layer.IsResident || layer.IsErased || layer.Item.File != Item.File || (!InMview && layer.Handle == null))
                {
                    throw new ArgumentException("The layer " + layer.Name + " is not one of the viewport's drawing.", parameter);
                }

                given.Add(layer);
            }

            return given;
        }

        /// <summary>Writes the layers frozen in the viewport: their handles as groups 331 from R2000 on, their names in the extended data before R2000.</summary>
        private void WriteFrozen(List<DxfLayerTableRecord> layers)
        {
            if (!InMview)
            {
                var groups = new KeyValuePair<int, string>[layers.Count];

                for (int i = 0; i < layers.Count; i++)
                {
                    groups[i] = new KeyValuePair<int, string>(331, layers[i].Handle);
                }

                ReplaceGroups(0, FrozenCodes, groups);
                return;
            }

            List<DxfTypedValue> data = Mview(out int start, out int end);

            if (start < 0)
            {
                throw new InvalidOperationException("The viewport of a file before R2000 has no list of frozen layers in its extended data to write them in.");
            }

            data.RemoveRange(start, end - start);
            var names = new List<DxfTypedValue>();
            layers.ForEach(l => names.Add(new DxfTypedValue(1003, l.Name)));
            data.InsertRange(start, names);
            XData = data;
        }

        /// <summary>
        /// The extended data of the viewport, and where the names of its frozen layers stand in it: after the second group 1002
        /// <c>{</c> of the data of ACAD that begins with MVIEW, up to its <c>}</c>; -1 for both where it has no such list.
        /// </summary>
        private List<DxfTypedValue> Mview(out int start, out int end)
        {
            var data = new List<DxfTypedValue>(XData);
            start = -1;
            end = -1;
            int first = MviewStart(data);

            if (first < 0)
            {
                return data;
            }

            int opened = 0;

            for (int i = first; i < data.Count && data[i].TypeCode != 1001; i++)
            {
                if (data[i].TypeCode == 1002 && data[i].Value.Trim() == "{" && ++opened == 2)
                {
                    start = i + 1;
                }
                else if (start >= 0 && data[i].TypeCode == 1002)
                {
                    end = i;
                    return data;
                }
            }

            start = -1;
            return data;
        }

        /// <summary>Where the data of ACAD that begins with MVIEW begins in extended data: the index of its 1000 MVIEW; -1 where there is none.</summary>
        private static int MviewStart(List<DxfTypedValue> data)
        {
            for (int i = 0; i + 1 < data.Count; i++)
            {
                if (data[i].TypeCode == 1001 && string.Equals(data[i].Value.Trim(), "ACAD", StringComparison.OrdinalIgnoreCase)
                    && data[i + 1].TypeCode == 1000 && data[i + 1].Value.Trim() == "MVIEW")
                {
                    return i + 1;
                }
            }

            return -1;
        }

        /// <summary>The index in the extended data of the first group of a place of MVIEW's values (MVIEW the place 0, a point one place); -1 where there is none.</summary>
        private static int R12Index(List<DxfTypedValue> data, int place)
        {
            int at = MviewStart(data);

            if (at < 0)
            {
                return -1;
            }

            for (int i = at, count = 0; i < data.Count && data[i].TypeCode != 1001; i++)
            {
                int code = data[i].TypeCode;

                // The y and z of a point go with its x.
                if ((code >= 1020 && code <= 1039) || code == 1003)
                {
                    continue;
                }

                if (count++ == place)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The number at a place of MVIEW's values; the fallback where there is none.</summary>
        private double R12Real(int place, double fallback)
        {
            List<DxfTypedValue> data = new List<DxfTypedValue>(XData);
            int at = R12Index(data, place);
            return at >= 0 && Numbers.TryRead(data[at].Value, out double value) && Guard.IsFinite(value) ? value : fallback;
        }

        /// <summary>The point at a place of MVIEW's values (1010, 1020, 1030); null where there is none.</summary>
        private double[] R12Point(int place)
        {
            List<DxfTypedValue> data = new List<DxfTypedValue>(XData);
            int at = R12Index(data, place);

            if (at < 0)
            {
                return null;
            }

            var point = new double[3];

            for (int k = 0; k < 3 && at + k < data.Count; k++)
            {
                if (k > 0 && data[at + k].TypeCode != data[at].TypeCode + 10 * k)
                {
                    break;
                }

                point[k] = Numbers.TryRead(data[at + k].Value, out double value) && Guard.IsFinite(value) ? value : 0.0;
            }

            return point;
        }

        /// <summary>Writes texts at places of MVIEW's values, the extended data written once.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the viewport has no such place in its extended data.</exception>
        private void SetR12(int[] places, string[] values)
        {
            var data = new List<DxfTypedValue>(XData);

            for (int k = 0; k < places.Length; k++)
            {
                int at = R12Index(data, places[k]);

                if (at < 0)
                {
                    throw new InvalidOperationException("The viewport of a file before R2000 has no extended data MVIEW holding its view to write it in.");
                }

                data[at] = new DxfTypedValue(data[at].TypeCode, values[k]);
            }

            XData = data;
        }

        /// <summary>Writes a point at a place of MVIEW's values; nothing where it is as it was.</summary>
        private void SetR12Point(int place, double x, double y, double z, double nowX, double nowY, double nowZ, bool valid)
        {
            if (Same(x, nowX) && Same(y, nowY) && Same(z, nowZ))
            {
                return;
            }

            if (!valid)
            {
                throw new ArgumentException("A point has to be of finite numbers.", "value");
            }

            var data = new List<DxfTypedValue>(XData);
            int at = R12Index(data, place);

            if (at < 0 || at + 2 >= data.Count || data[at + 1].TypeCode != data[at].TypeCode + 10 || data[at + 2].TypeCode != data[at].TypeCode + 20)
            {
                throw new InvalidOperationException("The viewport of a file before R2000 has no extended data MVIEW holding its view to write it in.");
            }

            data[at] = new DxfTypedValue(data[at].TypeCode, Real(x));
            data[at + 1] = new DxfTypedValue(data[at + 1].TypeCode, Real(y));
            data[at + 2] = new DxfTypedValue(data[at + 2].TypeCode, Real(z));
            XData = data;
        }
    }
}
