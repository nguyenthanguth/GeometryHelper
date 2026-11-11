using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Makes a drawing from a source and its settings: the views (L2), their dimensions, marks and symbols (L3), the sheet
    /// with the views placed on it (L4), the DXF (L5) and the document that keeps it all (L7).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The views are made once, at 1:1. Then the scales allowed are tried from the largest drawing down, the smallest
    /// denominator first: at each, the views are dimensioned and marked and packed onto the sheet by
    /// <see cref="SheetPacker"/>, the views along the axes kept round the front view as the projection method puts them
    /// (D9), sections, details, the isometric view and the part list each a box of its own; the first scale at which
    /// every box finds a place is taken. The strip of the title block along the bottom of the frame is kept free, since the
    /// packer has no blocked areas. Where nothing fits, a larger sheet is tried if the settings allow it, and otherwise the
    /// smallest scale is taken and the drawing says so in its warnings.
    /// </para>
    /// <para>
    /// An assembly of a beam 3 000 long and its end plate, with the assembly preset on an A2, comes out at 1:10: front, top
    /// and right views, the front and top dimensioned (the overall 3000 among them), the end plate marked P1, and a part
    /// list of two rows.
    /// </para>
    /// <para>
    /// The ids are made from the source and the kinds of view, so that an update recognises everything again: the drawing
    /// is the main object's id, a view V-FRONT or V-SECTION-A, a dimension its view's id, /D-, its kind, its axis and its
    /// number among those of its kind and axis (V-FRONT/D-Overall-X-1), a mark its view's id, /M- and the part's id.
    /// </para>
    /// <para>
    /// Every view gets the centre lines of its holes, bolts and tubes (<see cref="DrawingCentreLine"/>) and the symbols of
    /// its welds (<see cref="WeldSymbol"/>); the first view along the axes that sees a section's plane edge on gets its
    /// <see cref="SectionMark"/>, and the view a detail is taken from its <see cref="DetailMark"/>, each with the letter of
    /// its name; with <see cref="SymbolSettings.LevelMarks"/> on, as in the general arrangement preset, each elevation
    /// marks the level of every object that gives its attribute Elevation (<see cref="LevelMark"/>). The weld symbols are
    /// placed once the other symbols are known, clear of the view's dimensions, centre lines and symbols, each arrow still
    /// on its weld. The marks are placed last, by <see cref="Marker"/>, in the first view along the axes and those <see cref="ViewSettings.MarkedViews"/> names,
    /// clear of the view's dimensions and symbols: the mark P1 of the plate of TC1 stands beside the cut of its section A-A,
    /// not over it.
    /// </para>
    /// </remarks>
    public static class DrawingBuilder
    {
        /// <summary>The id of the element that keeps the settings in the drawing document.</summary>
        internal const string SettingsId = "GH-SETTINGS";

        /// <summary>The id of the frame and the edge of the sheet.</summary>
        internal const string SheetId = "GH-SHEET";

        /// <summary>The id of the title block.</summary>
        internal const string TitleBlockId = "GH-TITLE";

        /// <summary>The id of the part list.</summary>
        internal const string PartListId = "GH-PARTLIST";

        /// <summary>What the id of a level mark starts with, before the id of its object.</summary>
        private const string LevelPrefix = "L-";

        private static readonly ViewKind[] SlotKinds =
        {
            ViewKind.Front,
            ViewKind.Top,
            ViewKind.Bottom,
            ViewKind.Left,
            ViewKind.Right,
            ViewKind.Back,
        };

        /// <summary>
        /// Makes a drawing of a source with settings: its views, dimensions, marks and sheet, written into a DXF that holds
        /// the document too.
        /// </summary>
        /// <param name="source">The objects drawn.</param>
        /// <param name="settings">The settings, a preset or one's own; copied into the drawing, so that changing them
        /// afterwards changes nothing made.</param>
        /// <param name="tolerance">The tolerance everything is made within.</param>
        /// <returns>The drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the source or the settings are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the settings allow no scale, or one that is not a finite number
        /// above nought, or ask for a sheet of its own size with no width or height.</exception>
        /// <exception cref="NotSupportedException">Thrown when the settings ask for weld symbols to
        /// <see cref="WeldStandard.AwsA24"/>, which are not drawn yet (<see cref="WeldSymbol.Find(ProjectedView2, IEnumerable{DrawingObject}, GeoCoordinateSystem3, SymbolSettings, double)"/>).</exception>
        public static Drawing Build(DrawingSource source, DrawingSettings settings, Tolerance tolerance)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DrawingSettings copy = settings.Clone();
            DrawingContent content = Make(source, copy, tolerance);
            var drawing = new Drawing(content.Id, source, copy, tolerance, content);
            GeometryHelperLog.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "DrawingBuilder: {0} objects gave the drawing {1} of {2} views at 1:{3} on {4}.",
                    source.Objects.Count,
                    content.Id,
                    content.Views.Count,
                    Write(content.Scale),
                    content.Sheet.SizeName
                )
            );
            return drawing;
        }

        /// <summary>Makes everything of a drawing but its DXF: views, annotation, sheet and the elements of the document.</summary>
        internal static DrawingContent Make(DrawingSource source, DrawingSettings settings, Tolerance tolerance)
        {
            double[] scales = settings.SortedScales();

            if (settings.Sheet.Size == PaperSize.Custom && !settings.Sheet.IsCustom)
            {
                throw new ArgumentException("A sheet of its own size needs a width and a height.", nameof(settings));
            }

            string id = DrawingId(source);
            var objects = new Dictionary<string, DrawingObject>(StringComparer.Ordinal);

            foreach (DrawingObject item in source.Objects)
            {
                objects.Add(item.Id, item);
            }

            IReadOnlyList<DrawingView> made = new ViewBuilder(source, settings, tolerance).Build();
            var warnings = new List<string>();
            List<PaperSize> sizes = Sizes(settings.Sheet);
            List<DrawingView> placed = null;
            List<PartListRow> rows = PartRows(source, settings);
            Layout layout = null;
            double chosen = scales[scales.Length - 1];
            PaperSize size = sizes[0];

            foreach (PaperSize candidate in sizes)
            {
                Sheet sheet = NewSheet(settings.Sheet, candidate);

                foreach (double scale in scales)
                {
                    if (!MayFit(made, scale, settings, sheet, tolerance.EqualPoint))
                    {
                        continue;
                    }

                    // Marks and symbols only make a view's box larger, so a scale at which the views and their dimensions alone
                    // find no place is passed over before the marks are placed, which takes the longest.
                    if (
                        !Place(
                            Annotate(made, scale, settings, objects, tolerance, false),
                            settings,
                            sheet,
                            rows,
                            tolerance
                        ).AllPlaced
                    )
                    {
                        continue;
                    }

                    List<DrawingView> views = Annotate(made, scale, settings, objects, tolerance, true);
                    Layout tried = Place(views, settings, sheet, rows, tolerance);

                    if (tried.AllPlaced)
                    {
                        placed = views;
                        layout = tried;
                        chosen = scale;
                        size = candidate;
                        break;
                    }
                }

                if (placed != null)
                {
                    break;
                }
            }

            if (placed == null)
            {
                size = sizes[sizes.Count - 1];
                placed = Annotate(made, chosen, settings, objects, tolerance, true);
                layout = Place(placed, settings, NewSheet(settings.Sheet, size), rows, tolerance);
                warnings.Add(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "The views do not fit the sheet at any scale allowed; they are drawn at 1:{0} and {1} of them stand off the sheet.",
                        Write(chosen),
                        layout.Unplaced
                    )
                );
                GeometryHelperLog.Warn("DrawingBuilder: " + warnings[warnings.Count - 1]);
            }

            PlaceInModelSpace(placed);
            DrawingSheet drawingSheet = Sheet(source, settings, size, id, layout);
            return new DrawingContent(id, placed, drawingSheet, chosen, warnings);
        }

        /// <summary>The elements of the drawing document: the views, then the settings, the sheet and the annotation.</summary>
        internal static void Elements(
            DrawingContent content,
            DrawingSettings settings,
            out List<DrawingElement> views,
            out List<DrawingElement> annotations
        )
        {
            views = new List<DrawingElement>();
            annotations = new List<DrawingElement>
            {
                new DrawingElement(SettingsId, "Settings", null, null, settings.ToValues()),
                new DrawingElement(
                    SheetId,
                    "Sheet",
                    null,
                    null,
                    new[]
                    {
                        new KeyValuePair<string, string>("Size", content.Sheet.SizeName),
                        new KeyValuePair<string, string>("Width", Write(content.Sheet.Width)),
                        new KeyValuePair<string, string>("Height", Write(content.Sheet.Height)),
                    }
                ),
                new DrawingElement(TitleBlockId, "TitleBlock"),
            };

            if (content.Sheet.PartListBox.HasValue)
            {
                annotations.Add(
                    new DrawingElement(
                        PartListId,
                        "PartList",
                        null,
                        null,
                        new[]
                        {
                            new KeyValuePair<string, string>(
                                "Rows",
                                content.Sheet.PartList.Count.ToString(CultureInfo.InvariantCulture)
                            ),
                        }
                    )
                );
            }

            foreach (DrawingView view in content.Views)
            {
                views.Add(view.ToElement());

                for (int i = 0; i < view.Dimensions.Count; i++)
                {
                    DrawingDimension d = view.Dimensions[i];
                    annotations.Add(
                        new DrawingElement(
                            view.DimensionIds[i],
                            "Dimension",
                            view.Id,
                            d.Links,
                            new[]
                            {
                                new KeyValuePair<string, string>("Kind", d.Kind.ToString()),
                                new KeyValuePair<string, string>("Axis", d.Direction.X > 0.5 ? "X" : "Y"),
                                new KeyValuePair<string, string>("Text", d.Text),
                                new KeyValuePair<string, string>("Value", Write(d.Value)),
                            }
                        )
                    );
                }

                foreach (DrawingMark mark in view.Marks)
                {
                    annotations.Add(
                        new DrawingElement(MarkId(view, mark), "Mark", view.Id, mark.Links, mark.ToValues())
                    );
                }

                foreach (DrawingCentreLine line in view.CentreLines)
                {
                    annotations.Add(Symbol(view, line.Id, "CentreLine", line.Links, null));
                }

                foreach (WeldSymbol weld in view.WeldSymbols)
                {
                    annotations.Add(Symbol(view, weld.Id, "Weld", weld.Links, null));
                }

                foreach (SectionMark section in view.SectionMarks)
                {
                    annotations.Add(Symbol(view, section.Id, "SectionMark", null, section.Letter));
                }

                foreach (DetailMark detail in view.DetailMarks)
                {
                    annotations.Add(Symbol(view, detail.Id, "DetailMark", null, detail.Letter));
                }

                foreach (LevelMark level in view.LevelMarks)
                {
                    annotations.Add(
                        Symbol(
                            view,
                            level.Id,
                            "LevelMark",
                            new DrawingLinks(new[] { level.Id.Substring(LevelPrefix.Length) }),
                            level.Text
                        )
                    );
                }
            }
        }

        /// <summary>The id the drawing knows a mark by: its view's id, / and its own, V-FRONT/M-P-1.</summary>
        internal static string MarkId(DrawingView view, DrawingMark mark) => view.Id + "/" + mark.Id;

        /// <summary>The id the drawing knows a symbol by: its view's id, / and its own, V-FRONT/CL-1 or V-FRONT/SM-A.</summary>
        internal static string SymbolId(DrawingView view, string id) => view.Id + "/" + id;

        /// <summary>
        /// The element of a symbol: kind Symbol, which the presenter leaves as it is, with what symbol it is and its text.
        /// </summary>
        private static DrawingElement Symbol(DrawingView view, string id, string kind, DrawingLinks links, string text)
        {
            var values = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Kind", kind) };

            if (text != null)
            {
                values.Add(new KeyValuePair<string, string>("Text", text));
            }

            return new DrawingElement(SymbolId(view, id), "Symbol", view.Id, links, values);
        }

        /// <summary>The drawing's id: the main object's, the one marked main with the lowest id, else the lowest id.</summary>
        private static string DrawingId(DrawingSource source)
        {
            foreach (DrawingObject item in source.Objects)
            {
                if (item.IsMain)
                {
                    return item.Id;
                }
            }

            return source.Objects.Count > 0 ? source.Objects[0].Id : "DRAWING";
        }

        /// <summary>The sheets tried: the one asked for, then the larger A sheets where the settings allow them.</summary>
        private static List<PaperSize> Sizes(SheetSettings sheet)
        {
            var sizes = new List<PaperSize> { sheet.IsCustom ? PaperSize.Custom : sheet.Size };

            if (!sheet.IsCustom && sheet.AllowLargerSheet)
            {
                for (int i = (int)sheet.Size - 1; i >= (int)PaperSize.A0; i--)
                {
                    sizes.Add((PaperSize)i);
                }
            }

            return sizes;
        }

        private static Sheet NewSheet(SheetSettings settings, PaperSize size)
        {
            Sheet sheet =
                size == PaperSize.Custom
                    ? Packing.Sheet.Custom(settings.CustomWidth, settings.CustomHeight)
                    : new Sheet(size, settings.Orientation);
            double edge = settings.Margin + settings.ViewSpacing;
            sheet.OffsetLeft = edge;
            sheet.OffsetRight = edge;
            sheet.OffsetTop = edge;
            sheet.OffsetBottom = edge + TitleBlock.Height;
            return sheet;
        }

        /// <summary>Whether every view's crop, at a scale, is no larger than the room the sheet leaves for views.</summary>
        private static bool MayFit(
            IReadOnlyList<DrawingView> views,
            double scale,
            DrawingSettings settings,
            Sheet sheet,
            double tol
        )
        {
            double width = sheet.PaperWidth - sheet.OffsetLeft - sheet.OffsetRight;
            double height = sheet.PaperHeight - sheet.OffsetTop - sheet.OffsetBottom;
            double area = 0.0;

            foreach (DrawingView view in views)
            {
                double s = ScaleOf(view, scale, settings);
                double w = view.Crop.Width / s;
                double h = view.Crop.Height / s;

                if (w > width + tol || h > height + tol)
                {
                    return false;
                }

                area += w * h;
            }

            return area <= width * height;
        }

        /// <summary>The scale of a view when the drawing is at a scale: a detail's the nearest allowed no smaller than its enlargement asks.</summary>
        private static double ScaleOf(DrawingView view, double scale, DrawingSettings settings)
        {
            if (view.Kind != ViewKind.Detail)
            {
                return scale;
            }

            double enlargement = 1.0;

            foreach (DetailViewRequest request in settings.Views.Details)
            {
                if (string.Equals("V-DETAIL-" + ViewBuilder.Clean(request.Name), view.Id, StringComparison.Ordinal))
                {
                    enlargement = request.Enlargement;
                    break;
                }
            }

            double wanted = scale / enlargement;
            double[] scales = settings.SortedScales();
            double best = scales[0];

            foreach (double s in scales)
            {
                if (s <= wanted * (1.0 + 1E-9))
                {
                    best = s;
                }
            }

            return best;
        }

        /// <summary>
        /// The views at a scale, each dimensioned as the settings say, with its marks and symbols where asked, and the box of
        /// what each draws.
        /// </summary>
        private static List<DrawingView> Annotate(
            IReadOnlyList<DrawingView> made,
            double scale,
            DrawingSettings settings,
            Dictionary<string, DrawingObject> objects,
            Tolerance tolerance,
            bool annotated
        )
        {
            var views = new List<DrawingView>(made.Count);
            DrawingView marked = null;
            var toMark = new List<(DrawingView View, List<DrawingObject> Objects)>();
            var toWeld = new List<(DrawingView View, List<DrawingObject> Objects)>();

            foreach (DrawingView view in made)
            {
                if (Array.IndexOf(SlotKinds, view.Kind) >= 0 || view.Kind == ViewKind.Isometric)
                {
                    marked = view;
                    break;
                }
            }

            foreach (DrawingView source in made)
            {
                double s = ScaleOf(source, scale, settings);
                DrawingView view = source.With(
                    source.Kind,
                    source.Frame,
                    source.Crop,
                    s,
                    source.Lines,
                    Ids(source.ObjectIds)
                );
                var inView = new List<DrawingObject>();

                foreach (string objectId in view.ObjectIds)
                {
                    inView.Add(objects[objectId]);
                }

                if (
                    view.Kind != ViewKind.Isometric
                    && (
                        (view.Kind != ViewKind.Detail && settings.Views.Dimensioned.Contains(view.Kind))
                        || settings.Views.DimensionedViews.Contains(view.Id)
                    )
                )
                {
                    IReadOnlyList<DrawingDimension> dimensions = Annotator.Dimension(
                        view.Lines,
                        inView,
                        view.Frame.CoordinateSystem,
                        DrawingSettings.CloneDimensions(settings.Dimensions, s),
                        tolerance
                    );
                    view.Dimensions = dimensions;
                    view.DimensionIds = DimensionIds(view.Id, dimensions);
                }

                views.Add(view);

                if (!annotated)
                {
                    continue;
                }

                if (ReferenceEquals(source, marked) || settings.Views.MarkedViews.Contains(view.Id))
                {
                    toMark.Add((view, inView));
                }

                view.CentreLines = DrawingCentreLine.Find(view.Lines, inView, settings.Symbols, s, tolerance);
                toWeld.Add((view, inView));

                if (settings.Symbols.LevelMarks && Array.IndexOf(SlotKinds, view.Kind) >= 0)
                {
                    view.LevelMarks = Levels(view, inView, settings.Symbols, s, tolerance);
                }
            }

            if (annotated)
            {
                SectionAndDetailMarks(views, settings, tolerance);

                // The weld symbols once the other symbols are known, clear of them and of the dimensions.
                foreach ((DrawingView view, List<DrawingObject> inView) in toWeld)
                {
                    SymbolObstacles(view, out List<GeoRectangle2> boxes, out List<GeoLine2> lines);
                    view.WeldSymbols = WeldSymbol.Find(
                        view.Lines,
                        inView,
                        view.Frame.CoordinateSystem,
                        view.Dimensions,
                        boxes,
                        lines,
                        settings.Symbols,
                        view.Scale,
                        tolerance
                    );
                }

                // The marks last, clear of every symbol of their view as of its dimensions.
                foreach ((DrawingView view, List<DrawingObject> inView) in toMark)
                {
                    SymbolObstacles(view, out List<GeoRectangle2> boxes, out List<GeoLine2> lines);
                    view.Marks = Marker.Mark(
                        view.Lines,
                        inView,
                        view.Frame.CoordinateSystem,
                        view.Dimensions,
                        boxes,
                        lines,
                        settings.Marks,
                        settings.Marks.Content,
                        view.Scale,
                        tolerance
                    );
                }
            }

            foreach (DrawingView view in views)
            {
                view.Box = BoxOf(view);
            }

            return views;
        }

        /// <summary>
        /// What the marks of a view keep clear of besides its dimensions: the boxes of the texts of its symbols and of its weld
        /// symbols, and the lines of its symbols, centre lines, cut lines, arrows and outlines.
        /// </summary>
        internal static void SymbolObstacles(DrawingView view, out List<GeoRectangle2> boxes, out List<GeoLine2> lines)
        {
            boxes = new List<GeoRectangle2>();
            lines = new List<GeoLine2>();

            foreach (DrawingCentreLine line in view.CentreLines)
            {
                lines.Add(line.Line);
            }

            foreach (WeldSymbol weld in view.WeldSymbols)
            {
                boxes.Add(weld.Bounds);
                lines.Add(new GeoLine2(weld.Knee, weld.ArrowTip));
            }

            foreach (SectionMark section in view.SectionMarks)
            {
                lines.Add(section.CutLine);

                foreach (GeoPoint2 end in new[] { section.CutLine.StartPoint, section.CutLine.EndPoint })
                {
                    lines.Add(
                        new GeoLine2(
                            end,
                            new GeoPoint2(
                                end.X + section.Direction.X * 2.0 * section.TextHeight,
                                end.Y + section.Direction.Y * 2.0 * section.TextHeight
                            )
                        )
                    );
                }

                boxes.AddRange(section.LetterBoxes);
            }

            foreach (DetailMark detail in view.DetailMarks)
            {
                GeoPoint2[] corners = detail.Bounds.GetVertices();

                for (int i = 0; i < corners.Length; i++)
                {
                    lines.Add(new GeoLine2(corners[i], corners[(i + 1) % corners.Length]));
                }

                boxes.Add(detail.LetterBox);
            }

            foreach (LevelMark level in view.LevelMarks)
            {
                double half = 0.5 * level.TextHeight;
                GeoRectangle2 text = level.TextBox;
                boxes.Add(
                    Box(
                        level.Point.X - half,
                        level.Point.Y,
                        text.Center.X + 0.5 * text.Width + 0.2 * level.TextHeight,
                        text.Center.Y + 0.5 * text.Height
                    )
                );
            }
        }

        private static GeoRectangle2 Box(double x0, double y0, double x1, double y1) =>
            new GeoRectangle2(new GeoPoint2((x0 + x1) / 2.0, (y0 + y1) / 2.0), x1 - x0, y1 - y0);

        /// <summary>
        /// Gives each section its mark in the first view along the axes that sees its plane edge on, and each detail its mark
        /// in the view it is taken from, round its crop; each letter the section's or detail's name.
        /// </summary>
        private static void SectionAndDetailMarks(
            List<DrawingView> views,
            DrawingSettings settings,
            Tolerance tolerance
        )
        {
            var sections = new Dictionary<string, List<SectionMark>>(StringComparer.Ordinal);
            var details = new Dictionary<string, List<DetailMark>>(StringComparer.Ordinal);

            foreach (SectionViewRequest request in settings.Views.Sections)
            {
                DrawingView section = views.Find(v =>
                    string.Equals(v.Id, "V-SECTION-" + ViewBuilder.Clean(request.Name), StringComparison.Ordinal)
                );

                if (section == null || !section.SectionPlane.HasValue)
                {
                    continue;
                }

                if (request.CutLine.HasValue)
                {
                    // Cut along a line drawn in a view: the mark is that line, in that view.
                    DrawingView drawnIn = views.Find(v => v.Kind == request.LookingAs);

                    if (drawnIn != null)
                    {
                        Add(
                            sections,
                            drawnIn.Id,
                            new SectionMark(
                                "SM-" + ViewBuilder.Clean(request.Name),
                                request.Name,
                                request.CutLine.Value,
                                request.LookInView(),
                                settings.Symbols.TextHeight * drawnIn.Scale
                            )
                        );
                    }

                    continue;
                }

                foreach (DrawingView parent in views)
                {
                    if (
                        Array.IndexOf(SlotKinds, parent.Kind) >= 0
                        && TryCutLine(
                            parent,
                            section.SectionPlane.Value,
                            tolerance,
                            out GeoLine2 cut,
                            out GeoVector2 look
                        )
                    )
                    {
                        double height = settings.Symbols.TextHeight * parent.Scale;
                        Add(
                            sections,
                            parent.Id,
                            new SectionMark("SM-" + ViewBuilder.Clean(request.Name), request.Name, cut, look, height)
                        );
                        break;
                    }
                }
            }

            foreach (DetailViewRequest request in settings.Views.Details)
            {
                DrawingView detail = views.Find(v =>
                    string.Equals(v.Id, "V-DETAIL-" + ViewBuilder.Clean(request.Name), StringComparison.Ordinal)
                );
                DrawingView parent =
                    detail == null
                        ? null
                        : views.Find(v => string.Equals(v.Id, detail.ParentViewId, StringComparison.Ordinal));

                if (parent != null)
                {
                    double height = settings.Symbols.TextHeight * parent.Scale;
                    Add(
                        details,
                        parent.Id,
                        DetailMark.Rectangle("DM-" + ViewBuilder.Clean(request.Name), request.Name, detail.Crop, height)
                    );
                }
            }

            foreach (DrawingView view in views)
            {
                if (sections.TryGetValue(view.Id, out List<SectionMark> s))
                {
                    view.SectionMarks = s.ToArray();
                }

                if (details.TryGetValue(view.Id, out List<DetailMark> d))
                {
                    view.DetailMarks = d.ToArray();
                }
            }
        }

        private static void Add<T>(Dictionary<string, List<T>> lists, string key, T item)
        {
            if (!lists.TryGetValue(key, out List<T> list))
            {
                lists.Add(key, list = new List<T>());
            }

            list.Add(item);
        }

        /// <summary>
        /// Where a section's plane meets a view that sees it edge on, across the view's crop, and the way the section looks
        /// in the view; false where the view does not see the plane edge on.
        /// </summary>
        private static bool TryCutLine(
            DrawingView view,
            GeoPlane3 plane,
            Tolerance tolerance,
            out GeoLine2 cut,
            out GeoVector2 look
        )
        {
            cut = default;
            look = default;
            GeoCoordinateSystem3 cs = view.Frame.CoordinateSystem;
            double a = plane.Normal.DotProduct(cs.XAxis),
                b = plane.Normal.DotProduct(cs.YAxis),
                length = Math.Sqrt(a * a + b * b);

            if (!(length > 0.0) || Math.Abs(plane.Normal.DotProduct(cs.ZAxis)) > tolerance.EqualVector * length)
            {
                return false;
            }

            a /= length;
            b /= length;
            double c = plane.Normal.DotProduct(cs.Origin.GetVectorTo(plane.Origin)) / length;
            var through = new GeoPoint2(a * c, b * c);
            var along = new GeoVector2(-b, a);
            var crop = new Extent(view.Crop);
            double low = double.PositiveInfinity,
                high = double.NegativeInfinity;

            foreach (
                GeoPoint2 corner in new[]
                {
                    new GeoPoint2(crop.X0, crop.Y0),
                    new GeoPoint2(crop.X1, crop.Y0),
                    new GeoPoint2(crop.X1, crop.Y1),
                    new GeoPoint2(crop.X0, crop.Y1),
                }
            )
            {
                double t = (corner.X - through.X) * along.X + (corner.Y - through.Y) * along.Y;
                low = Math.Min(low, t);
                high = Math.Max(high, t);
            }

            cut = new GeoLine2(
                new GeoPoint2(through.X + along.X * low, through.Y + along.Y * low),
                new GeoPoint2(through.X + along.X * high, through.Y + along.Y * high)
            );

            // The section looks away from its viewer, against the plane's normal.
            look = new GeoVector2(-a, -b);
            return high - low > tolerance.EqualPoint;
        }

        /// <summary>
        /// The level marks of a view along the axes whose up is the model's Z: one for each object of it whose attribute
        /// Elevation reads as a number, its point just right of the upper right corner of the object's box, in the order of
        /// the objects' ids.
        /// </summary>
        private static LevelMark[] Levels(
            DrawingView view,
            List<DrawingObject> inView,
            SymbolSettings settings,
            double scale,
            Tolerance tolerance
        )
        {
            GeoCoordinateSystem3 cs = view.Frame.CoordinateSystem;

            if (!cs.YAxis.IsEqualTo(GeoVector3.ZAxis, tolerance))
            {
                return new LevelMark[0];
            }

            double height = settings.TextHeight * scale;
            var marks = new List<LevelMark>();

            foreach (DrawingObject item in inView)
            {
                if (
                    !item.Attributes.TryGetValue(LevelMark.ElevationAttribute, out string written)
                    || !double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double elevation)
                    || double.IsNaN(elevation)
                    || double.IsInfinity(elevation)
                )
                {
                    continue;
                }

                double right = double.NegativeInfinity,
                    top = double.NegativeInfinity;

                foreach (GeoFace3 face in item.Solid.Faces)
                {
                    foreach (GeoPoint3 p in face.Boundary.Vertices)
                    {
                        GeoPoint3 local = cs.ToLocal(p);
                        right = Math.Max(right, local.X);
                        top = Math.Max(top, local.Y);
                    }
                }

                if (!double.IsInfinity(right))
                {
                    marks.Add(
                        new LevelMark(LevelPrefix + item.Id, new GeoPoint2(right + height, top), elevation, height)
                    );
                }
            }

            return marks.ToArray();
        }

        /// <summary>The ids of a view's dimensions: the view's, /D-, the kind, the axis and the number among those alike.</summary>
        private static string[] DimensionIds(string viewId, IReadOnlyList<DrawingDimension> dimensions)
        {
            var ids = new string[dimensions.Count];
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < dimensions.Count; i++)
            {
                string key = dimensions[i].Kind + "-" + (dimensions[i].Direction.X > 0.5 ? "X" : "Y");
                counts.TryGetValue(key, out int count);
                counts[key] = ++count;
                ids[i] = viewId + "/D-" + key + "-" + count.ToString(CultureInfo.InvariantCulture);
            }

            return ids;
        }

        /// <summary>
        /// The box of what a view draws: its crop, its dimensions, its marks with their leaders, and its symbols, in view
        /// millimetres.
        /// </summary>
        internal static GeoRectangle2 BoxOf(DrawingView view)
        {
            var box = new Extent(view.Crop);

            foreach (DrawingDimension d in view.Dimensions)
            {
                if (d.DimensionLine.HasValue)
                {
                    box.Add(d.DimensionLine.Value.StartPoint);
                    box.Add(d.DimensionLine.Value.EndPoint);
                }

                foreach (GeoLine2 line in d.ExtensionLines)
                {
                    box.Add(line.StartPoint);
                    box.Add(line.EndPoint);
                }

                box.Add(d.TextBox);
            }

            foreach (DrawingMark mark in view.Marks)
            {
                foreach (GeoPoint2 p in mark.Leader)
                {
                    box.Add(p);
                }

                box.Add(mark.TextBox);
            }

            foreach (DrawingCentreLine line in view.CentreLines)
            {
                box.Add(line.Line.StartPoint);
                box.Add(line.Line.EndPoint);
            }

            foreach (WeldSymbol weld in view.WeldSymbols)
            {
                box.Add(weld.ArrowTip);
                box.Add(weld.Bounds);
            }

            foreach (SectionMark section in view.SectionMarks)
            {
                foreach (GeoPoint2 end in new[] { section.CutLine.StartPoint, section.CutLine.EndPoint })
                {
                    box.Add(end);
                    box.Add(
                        new GeoPoint2(
                            end.X + section.Direction.X * 2.0 * section.TextHeight,
                            end.Y + section.Direction.Y * 2.0 * section.TextHeight
                        )
                    );
                }

                foreach (GeoRectangle2 letter in section.LetterBoxes)
                {
                    box.Add(letter);
                }
            }

            foreach (DetailMark detail in view.DetailMarks)
            {
                box.Add(detail.Bounds);
                box.Add(detail.LetterBox);
            }

            foreach (LevelMark level in view.LevelMarks)
            {
                double half = 0.5 * level.TextHeight;
                box.Add(new GeoPoint2(level.Point.X - half, level.Point.Y));
                box.Add(new GeoPoint2(level.Point.X + half, level.Point.Y + 0.8 * level.TextHeight));
                box.Add(level.TextBox);
                GeoRectangle2 text = level.TextBox;
                box.Add(new GeoPoint2(text.Center.X + 0.5 * text.Width + 0.2 * level.TextHeight, text.Center.Y));
            }

            // The title of the view stands below what it draws, on paper, each character taken as 0.9 of its height wide.
            double titleWidth = view.Title.Length * 0.9 * DrawingDxfWriter.TitleHeight * view.Scale;
            box.Add(new GeoPoint2(box.X0 + titleWidth, box.Y0 - DrawingDxfWriter.TitleBand * view.Scale));
            return box.ToRectangle();
        }

        /// <summary>
        /// Packs the views and the part list onto a sheet: the views along the axes as one block round the front view, the
        /// others and the part list a box each.
        /// </summary>
        private static Layout Place(
            List<DrawingView> views,
            DrawingSettings settings,
            Sheet sheet,
            List<PartListRow> rows,
            Tolerance tolerance
        )
        {
            double spacing = settings.Sheet.ViewSpacing;
            var groups = new List<GeoRectangle2[]>();
            var owners = new List<int[]>();
            var origins = new List<GeoPoint2[]>();
            var slots = new List<int>();
            var columns = new SortedDictionary<int, double[]>();
            var rowsOf = new SortedDictionary<int, double[]>();
            var slot = new int[views.Count, 2];

            for (int i = 0; i < views.Count; i++)
            {
                if (Array.IndexOf(SlotKinds, views[i].Kind) < 0)
                {
                    continue;
                }

                StandardViews.GetSlot(views[i].Kind, settings.ProjectionMethod, out int column, out int row);
                slot[i, 0] = column;
                slot[i, 1] = row;
                slots.Add(i);
                Extent paper = PaperBox(views[i]);
                Widen(columns, column, -paper.X0, paper.X1);
                Widen(rowsOf, row, -paper.Y0, paper.Y1);
            }

            if (slots.Count > 0)
            {
                Dictionary<int, double> x = Positions(columns, spacing);
                Dictionary<int, double> y = Positions(rowsOf, spacing);
                var boxes = new GeoRectangle2[slots.Count];
                var at = new GeoPoint2[slots.Count];

                for (int k = 0; k < slots.Count; k++)
                {
                    int i = slots[k];
                    at[k] = new GeoPoint2(x[slot[i, 0]], y[slot[i, 1]]);
                    Extent paper = PaperBox(views[i]);
                    boxes[k] = new GeoRectangle2(
                        at[k].X + paper.X0,
                        at[k].Y + paper.Y0,
                        paper.X1 - paper.X0,
                        paper.Y1 - paper.Y0
                    );
                }

                groups.Add(boxes);
                owners.Add(slots.ToArray());
                origins.Add(at);
            }

            for (int i = 0; i < views.Count; i++)
            {
                if (Array.IndexOf(SlotKinds, views[i].Kind) >= 0)
                {
                    continue;
                }

                Extent paper = PaperBox(views[i]);
                groups.Add(new[] { new GeoRectangle2(paper.X0, paper.Y0, paper.X1 - paper.X0, paper.Y1 - paper.Y0) });
                owners.Add(new[] { i });
                origins.Add(new[] { new GeoPoint2(0.0, 0.0) });
            }

            int partList = -1;

            if (settings.Sheet.PartList && rows.Count > 0)
            {
                partList = groups.Count;
                groups.Add(
                    new[]
                    {
                        new GeoRectangle2(
                            0.0,
                            0.0,
                            DrawingSheet.PartListWidth,
                            DrawingSheet.PartListHeight(rows.Count)
                        ),
                    }
                );
                owners.Add(new[] { -1 });
                origins.Add(new[] { new GeoPoint2(0.0, 0.0) });
            }

            var options = new PackOptions
            {
                Spacing = spacing,
                GroupSpacing = spacing,
                GroupLayout = GroupLayout.Keep,
                Tolerance = tolerance,
            };
            PackResult result = SheetPacker.Pack(groups, sheet, options);
            var layout = new Layout(rows);
            double offSheet = sheet.PaperWidth + spacing;

            for (int g = 0; g < groups.Count; g++)
            {
                // A block too large for the sheet is split by the packer and laid out afresh; the views along the axes would no
                // longer stand round the front view, so a block that did not move as one counts as not placed.
                bool whole = true;

                for (int k = 1; k < groups[g].Length; k++)
                {
                    GeoVector2 first = result.Placements[g][0].Translation,
                        other = result.Placements[g][k].Translation;
                    whole &=
                        Math.Abs(first.X - other.X) <= tolerance.EqualPoint
                        && Math.Abs(first.Y - other.Y) <= tolerance.EqualPoint;
                }

                bool onSheet = whole;

                for (int k = 0; k < groups[g].Length; k++)
                {
                    onSheet &= result.Placements[g][k].Placed && result.Placements[g][k].SheetIndex == 0;
                }

                GeoVector2 off = new GeoVector2(0.0, 0.0);

                if (!onSheet)
                {
                    // Off the sheet to its right, the block whole, one after another, where it can be seen and moved by hand.
                    var block = new Extent(groups[g][0]);

                    foreach (GeoRectangle2 box in groups[g])
                    {
                        block.Add(box);
                    }

                    layout.Unplaced += groups[g].Length;
                    off = new GeoVector2(offSheet - block.X0, spacing - block.Y0);
                    offSheet += block.X1 - block.X0 + spacing;
                }

                for (int k = 0; k < groups[g].Length; k++)
                {
                    GeoVector2 move = onSheet ? result.Placements[g][k].Translation : off;
                    var origin = new GeoPoint2(origins[g][k].X + move.X, origins[g][k].Y + move.Y);

                    if (g == partList)
                    {
                        layout.PartListBox = new GeoRectangle2(
                            origin.X,
                            origin.Y,
                            DrawingSheet.PartListWidth,
                            DrawingSheet.PartListHeight(rows.Count)
                        );
                    }
                    else
                    {
                        views[owners[g][k]].SheetOrigin = origin;
                    }
                }
            }

            return layout;
        }

        /// <summary>The box of what a view draws, on paper, about its origin.</summary>
        private static Extent PaperBox(DrawingView view)
        {
            GeoRectangle2 box = view.Box;
            double s = view.Scale;
            return new Extent(
                (box.Center.X - box.Width / 2.0) / s,
                (box.Center.Y - box.Height / 2.0) / s,
                (box.Center.X + box.Width / 2.0) / s,
                (box.Center.Y + box.Height / 2.0) / s
            );
        }

        private static void Widen(SortedDictionary<int, double[]> reach, int key, double before, double after)
        {
            if (!reach.TryGetValue(key, out double[] r))
            {
                reach.Add(key, new[] { before, after });
                return;
            }

            r[0] = Math.Max(r[0], before);
            r[1] = Math.Max(r[1], after);
        }

        /// <summary>The positions of the columns, or rows, in order: each its reach before it past the one before and the spacing.</summary>
        private static Dictionary<int, double> Positions(SortedDictionary<int, double[]> reach, double spacing)
        {
            var positions = new Dictionary<int, double>();
            double at = 0.0;
            double[] before = null;

            foreach (KeyValuePair<int, double[]> entry in reach)
            {
                at = before == null ? entry.Value[0] : at + before[1] + spacing + entry.Value[0];
                positions.Add(entry.Key, at);
                before = entry.Value;
            }

            return positions;
        }

        /// <summary>
        /// Gives each view a place of its own in model space, for a layout of viewports: in a row along X, a gap between them,
        /// each from its lines and everything it draws.
        /// </summary>
        private static void PlaceInModelSpace(List<DrawingView> views)
        {
            double at = 0.0;

            foreach (DrawingView view in views)
            {
                var all = new Extent(view.Box);
                all.Add(view.Lines.Bounds);
                double gap = Math.Max(1000.0, 0.2 * (all.X1 - all.X0));
                view.ModelOrigin = new GeoPoint2(at - all.X0, -all.Y0);
                at += all.X1 - all.X0 + gap;
            }
        }

        /// <summary>The rows of the part list: the parts of one mark, profile, material, length and name, in that order.</summary>
        private static List<PartListRow> PartRows(DrawingSource source, DrawingSettings settings)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var rows = new SortedDictionary<string, PartListRow>(StringComparer.Ordinal);

            if (!settings.Sheet.PartList)
            {
                return new List<PartListRow>();
            }

            foreach (DrawingObject item in source.Objects)
            {
                if (item.Kind != DrawingObjectKind.Part)
                {
                    continue;
                }

                double length = Math.Round(Reach(item), MidpointRounding.AwayFromZero);
                string key =
                    item.Mark
                    + "\u0001"
                    + item.Profile
                    + "\u0001"
                    + item.Material
                    + "\u0001"
                    + length.ToString("000000000000.###", CultureInfo.InvariantCulture)
                    + "\u0001"
                    + item.Name;
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
                rows[key] = new PartListRow(item.Mark, count + 1, item.Profile, item.Material, length, item.Name);
            }

            return new List<PartListRow>(rows.Values);
        }

        private static double Reach(DrawingObject item)
        {
            double low = double.PositiveInfinity,
                high = double.NegativeInfinity;

            foreach (GeoFace3 face in item.Solid.Faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    double x = item.Frame.ToLocal(p).X;
                    low = Math.Min(low, x);
                    high = Math.Max(high, x);
                }
            }

            return low > high ? 0.0 : high - low;
        }

        private static DrawingSheet Sheet(
            DrawingSource source,
            DrawingSettings settings,
            PaperSize size,
            string id,
            Layout layout
        )
        {
            Sheet sheet = NewSheet(settings.Sheet, size);
            double margin = settings.Sheet.Margin;
            var frame = new GeoRectangle2(
                margin,
                margin,
                sheet.PaperWidth - 2.0 * margin,
                sheet.PaperHeight - 2.0 * margin
            );
            var titleBox = new GeoRectangle2(
                sheet.PaperWidth - margin - TitleBlock.Width,
                margin,
                TitleBlock.Width,
                TitleBlock.Height
            );
            TitleBlock title = settings.Sheet.TitleBlock.Clone();

            if (title.DrawingNumber.Length == 0)
            {
                title.DrawingNumber = id;
            }

            if (title.Title.Length == 0)
            {
                DrawingObject main = null;

                foreach (DrawingObject item in source.Objects)
                {
                    if (string.Equals(item.Id, id, StringComparison.Ordinal))
                    {
                        main = item;
                        break;
                    }
                }

                title.Title = main == null ? string.Empty : (main.Name + " " + main.Mark).Trim();
            }

            string name =
                size == PaperSize.Custom ? Write(sheet.PaperWidth) + "x" + Write(sheet.PaperHeight) : size.ToString();
            return new DrawingSheet(
                name,
                sheet.PaperWidth,
                sheet.PaperHeight,
                frame,
                title,
                titleBox,
                layout.Rows,
                layout.PartListBox
            );
        }

        private static string[] Ids(IReadOnlyList<string> ids)
        {
            var copy = new string[ids.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = ids[i];
            }

            return copy;
        }

        private static string Write(double value) => DrawingSettings.Write(value);

        /// <summary>Where the boxes of one try went: the part list's box, and how many found no place on the sheet.</summary>
        private sealed class Layout
        {
            public Layout(List<PartListRow> rows)
            {
                Rows = rows;
            }

            public List<PartListRow> Rows { get; }

            public GeoRectangle2? PartListBox { get; set; }

            public int Unplaced { get; set; }

            public bool AllPlaced => Unplaced == 0;
        }
    }

    /// <summary>What a drawing is made of but its DXF: its id, views, sheet, scale and warnings.</summary>
    internal sealed class DrawingContent
    {
        public DrawingContent(
            string id,
            List<DrawingView> views,
            DrawingSheet sheet,
            double scale,
            List<string> warnings
        )
        {
            Id = id;
            Views = views;
            Sheet = sheet;
            Scale = scale;
            Warnings = warnings;
        }

        public string Id { get; }

        public List<DrawingView> Views { get; }

        public DrawingSheet Sheet { get; }

        public double Scale { get; }

        public List<string> Warnings { get; }
    }

    /// <summary>A box square to the axes, grown point by point.</summary>
    internal struct Extent
    {
        public double X0;
        public double Y0;
        public double X1;
        public double Y1;

        public Extent(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public Extent(GeoRectangle2 box)
            : this(
                box.Center.X - box.Width / 2.0,
                box.Center.Y - box.Height / 2.0,
                box.Center.X + box.Width / 2.0,
                box.Center.Y + box.Height / 2.0
            ) { }

        public void Add(GeoPoint2 p)
        {
            X0 = Math.Min(X0, p.X);
            Y0 = Math.Min(Y0, p.Y);
            X1 = Math.Max(X1, p.X);
            Y1 = Math.Max(Y1, p.Y);
        }

        public void Add(GeoRectangle2 box)
        {
            var other = new Extent(box);
            Add(new GeoPoint2(other.X0, other.Y0));
            Add(new GeoPoint2(other.X1, other.Y1));
        }

        public GeoRectangle2 ToRectangle() =>
            new GeoRectangle2(new GeoPoint2((X0 + X1) / 2.0, (Y0 + Y1) / 2.0), X1 - X0, Y1 - Y0);
    }
}
