using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// One view of a drawing (L2): a window into the model, as a Tekla view or an AutoCAD MVIEW is, with its direction and
    /// depth (<see cref="Frame"/>), its crop, its scale, the lines L1 made for it, and, once the drawing is made, its
    /// dimensions, its marks, its symbols and where it stands on the sheet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lines are in view millimetres at 1:1 (<see cref="Lines"/>); the scale maps them to the sheet. The front view of a
    /// beam 3 000 long at 1:20 has its lines from 0 to 3 000 across, and stands 150 wide on the sheet. Its crop is by default
    /// the box of its lines, a margin about it; a view's lines are made again by <see cref="ViewBuilder"/> when its crop,
    /// direction or depth changes, and not when its scale does.
    /// </para>
    /// <para>
    /// A view does not change once its drawing is made; <see cref="ViewBuilder"/> gives a changed copy. Its id is made from
    /// its kind and the name of its section or detail, V-FRONT, V-SECTION-A, V-DETAIL-B, so that the next update of the
    /// drawing recognises it.
    /// </para>
    /// </remarks>
    public sealed class DrawingView
    {
        private IReadOnlyList<DrawingDimension> _dimensions = new DrawingDimension[0];
        private IReadOnlyList<DrawingMark> _marks = new DrawingMark[0];

        internal DrawingView(
            string id,
            ViewKind kind,
            string name,
            ViewFrame3 frame,
            GeoPlane3? sectionPlane,
            string parentViewId,
            GeoRectangle2 crop,
            double scale,
            bool hiddenLines,
            ProjectedView2 lines,
            string[] objectIds
        )
        {
            Id = id;
            Kind = kind;
            Name = name;
            Frame = frame;
            SectionPlane = sectionPlane;
            ParentViewId = parentViewId;
            Crop = crop;
            Scale = scale;
            HiddenLines = hiddenLines;
            Lines = lines;
            ObjectIds = Array.AsReadOnly(objectIds);
        }

        /// <summary>Gets the id of the view, unique in its drawing: V-FRONT, V-TOP, V-SECTION-A, V-DETAIL-B, V-ISOMETRIC.</summary>
        public string Id { get; }

        /// <summary>Gets what the view is.</summary>
        public ViewKind Kind { get; }

        /// <summary>Gets the name of the view, as its title begins: FRONT, SECTION A-A, DETAIL B.</summary>
        public string Name { get; }

        /// <summary>Gets the title of the view: its name and its scale, as "SECTION A-A 1:10".</summary>
        public string Title => Name + " 1:" + Scale.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>Gets where the view looks from and how deep it sees.</summary>
        public ViewFrame3 Frame { get; }

        /// <summary>Gets the plane a section is cut by, its normal towards the viewer; null for a view that cuts nothing.</summary>
        public GeoPlane3? SectionPlane { get; }

        /// <summary>Gets the id of the view a detail is taken from; null for a view that is not a detail.</summary>
        public string ParentViewId { get; }

        /// <summary>Gets the part of the view drawn, in view millimetres at 1:1.</summary>
        public GeoRectangle2 Crop { get; }

        /// <summary>Gets the scale the view is drawn at: 20 for 1:20.</summary>
        public double Scale { get; }

        /// <summary>Gets whether the view draws its hidden lines.</summary>
        public bool HiddenLines { get; }

        /// <summary>Gets the lines of the view, in view millimetres at 1:1, as L1 made them.</summary>
        public ProjectedView2 Lines { get; }

        /// <summary>Gets the ids of the objects the view was projected from, in the order of their ids.</summary>
        public IReadOnlyList<string> ObjectIds { get; }

        /// <summary>Gets the dimensions of the view, as the drawing made them; empty for a view not dimensioned.</summary>
        public IReadOnlyList<DrawingDimension> Dimensions
        {
            get => _dimensions;
            internal set => _dimensions = value;
        }

        /// <summary>
        /// Gets the ids the drawing knows the dimensions by, in the order of <see cref="Dimensions"/>: unique in the drawing,
        /// the view's id, /D-, the kind, the axis and the number among those alike, as V-FRONT/D-Overall-X-1.
        /// </summary>
        public IReadOnlyList<string> DimensionIds { get; internal set; } = new string[0];

        /// <summary>
        /// Gets the marks of the view, as <see cref="Marker"/> placed them; empty for a view not marked. The drawing knows each
        /// by the view's id, / and the mark's id: V-FRONT/M-P-1.
        /// </summary>
        public IReadOnlyList<DrawingMark> Marks
        {
            get => _marks;
            internal set => _marks = value;
        }

        /// <summary>Gets the centre lines of the holes, bolts and tubes the view sees as circles; empty until the drawing is made.</summary>
        public IReadOnlyList<DrawingCentreLine> CentreLines { get; internal set; } = new DrawingCentreLine[0];

        /// <summary>Gets the symbols of the welds the view sees; empty until the drawing is made.</summary>
        public IReadOnlyList<WeldSymbol> WeldSymbols { get; internal set; } = new WeldSymbol[0];

        /// <summary>
        /// Gets the marks of the sections cut across what the view sees, each where the section's plane meets the view, its
        /// letter the section's name: SECTION A-A of an end view has its mark A in the front view. Empty until the drawing is
        /// made.
        /// </summary>
        public IReadOnlyList<SectionMark> SectionMarks { get; internal set; } = new SectionMark[0];

        /// <summary>Gets the marks of the details taken from the view, each round its detail's crop; empty until the drawing is made.</summary>
        public IReadOnlyList<DetailMark> DetailMarks { get; internal set; } = new DetailMark[0];

        /// <summary>
        /// Gets the level marks of the view, one for each object that gives its elevation, where the settings ask for them
        /// (<see cref="SymbolSettings.LevelMarks"/>); empty until the drawing is made.
        /// </summary>
        public IReadOnlyList<LevelMark> LevelMarks { get; internal set; } = new LevelMark[0];

        /// <summary>
        /// Gets the box of everything the view draws, its crop, its dimensions, its marks and its symbols, in view
        /// millimetres at 1:1; its crop until the drawing is made.
        /// </summary>
        public GeoRectangle2 Box { get; internal set; }

        /// <summary>Gets where the view's origin stands on the sheet, in paper millimetres; the origin until the drawing is made.</summary>
        public GeoPoint2 SheetOrigin { get; internal set; }

        /// <summary>
        /// Gets where the view's origin stands in model space when the drawing is written as a layout and viewports: each
        /// view in a place of its own, at 1:1.
        /// </summary>
        public GeoPoint2 ModelOrigin { get; internal set; }

        /// <summary>Gets the box of the view on the sheet, in paper millimetres: <see cref="Box"/> divided by the scale, at <see cref="SheetOrigin"/>.</summary>
        public GeoRectangle2 SheetBox =>
            new GeoRectangle2(
                new GeoPoint2(SheetOrigin.X + Box.Center.X / Scale, SheetOrigin.Y + Box.Center.Y / Scale),
                Box.Width / Scale,
                Box.Height / Scale
            );

        /// <inheritdoc/>
        public override string ToString() => $"View {Id} {Title}";

        /// <summary>A copy of the view, its lines and kind kept, the rest as given.</summary>
        internal DrawingView With(
            ViewKind kind,
            ViewFrame3 frame,
            GeoRectangle2 crop,
            double scale,
            ProjectedView2 lines,
            string[] objectIds
        ) =>
            new DrawingView(
                Id,
                kind,
                Name,
                frame,
                SectionPlane,
                ParentViewId,
                crop,
                scale,
                HiddenLines,
                lines,
                objectIds
            )
            {
                Box = crop,
            };

        /// <summary>The view as values of the drawing document, its links the objects in it.</summary>
        internal DrawingElement ToElement()
        {
            GeoCoordinateSystem3 cs = Frame.CoordinateSystem;
            var values = new List<KeyValuePair<string, string>>
            {
                Value("Kind", Kind.ToString()),
                Value("Name", Name),
                Value("Scale", DrawingSettings.Write(Scale)),
                Value("Origin", Point(cs.Origin.X, cs.Origin.Y, cs.Origin.Z)),
                Value("XAxis", Point(cs.XAxis.X, cs.XAxis.Y, cs.XAxis.Z)),
                Value("YAxis", Point(cs.YAxis.X, cs.YAxis.Y, cs.YAxis.Z)),
                Value("DepthInFront", DrawingSettings.Write(Frame.DepthInFront)),
                Value("DepthBehind", DrawingSettings.Write(Frame.DepthBehind)),
                Value("Crop", Rectangle(Crop)),
                Value("Box", Rectangle(Box)),
                Value("SheetOrigin", Point(SheetOrigin.X, SheetOrigin.Y)),
                Value("ModelOrigin", Point(ModelOrigin.X, ModelOrigin.Y)),
                Value("Parent", ParentViewId),
                Value("HiddenLines", HiddenLines ? "1" : "0"),
            };

            return new DrawingElement(Id, "View", null, new DrawingLinks(ObjectIds), values);
        }

        private static KeyValuePair<string, string> Value(string name, string value) =>
            new KeyValuePair<string, string>(name, value);

        private static string Point(params double[] coordinates) =>
            string.Join(",", Array.ConvertAll(coordinates, DrawingSettings.Write));

        private static string Rectangle(GeoRectangle2 box) => Point(box.Center.X, box.Center.Y, box.Width, box.Height);
    }
}
