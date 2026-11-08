using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Writes a drawing into a DXF (L5, 15-output-dxf.md): the lines, dimensions and marks of its views, its sheet, and the
    /// drawing document, every entity tagged with its owner and the id of what it draws (<see cref="DrawingEntityTags"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default (<see cref="DrawingExportMode.LayoutAndViewports"/>) the lines of each view are in model space at 1:1, each
    /// view in a place of its own, and the sheet is the layout <see cref="LayoutName"/>: its frame, title block and part list,
    /// and one viewport per view at its scale looking at its lines. A beam 3 000 long in a front view at 1:20 is 3 000 long in
    /// model space, and its viewport 150 wide on the sheet. Dimensions and marks of a view are in model space with its
    /// lines, their text as high as the settings ask on paper times the scale: 2.5 on paper is 50 at 1:20. A dimension there
    /// is drawn in the style GH_MODEL_ and the scale, GH_MODEL_20, whose DIMLFAC is 1, so that it reads the length it
    /// spans. With <see cref="DrawingExportMode.SheetInModelSpace"/> the whole sheet is drawn at 1:1 in model space, each
    /// view at its scale, and its dimensions in the style GH_ and the scale, whose DIMLFAC is the scale.
    /// </para>
    /// <para>
    /// The layers are Visible, Hidden (linetype HIDDEN), Centre (CENTER), Hatch, Dimension, Mark, Sheet and Viewport, the
    /// last not plotted. Lines beyond a view's crop are cut at it, so that model space holds what the viewports show. The
    /// title block is an INSERT of the block GH_TITLE with its texts as attributes; the part list is drawn in lines and
    /// texts, since the library makes no TABLE of its own.
    /// </para>
    /// <para>
    /// The same drawing gives the same DXF to the byte: everything is written in the order of the views, the objects and
    /// the ids, and nothing reads the clock.
    /// </para>
    /// </remarks>
    public static class DrawingDxfWriter
    {
        /// <summary>The name of the layout the sheet is drawn on.</summary>
        public const string LayoutName = "Sheet";

        /// <summary>The layer of the frame, the title block, the part list and the view titles.</summary>
        public const string SheetLayer = "Sheet";

        /// <summary>The layer of the viewports.</summary>
        public const string ViewportLayer = "Viewport";

        /// <summary>The name of the block of the title block.</summary>
        public const string TitleBlockName = "GH_TITLE";

        internal const double TitleHeight = 3.5;
        private const double TitleGap = 2.0;

        private static readonly (string Head, double Width)[] PartColumns =
        {
            ("MARK", 20), ("QTY", 12), ("PROFILE", 40), ("MATERIAL", 25), ("LENGTH", 23), ("NAME", 40),
        };

        /// <summary>The cells of the title block: the attribute's tag, its label, and its cell from the block's lower left corner.</summary>
        private static readonly (string Tag, string Label, double X, double Y, double Width)[] TitleCells =
        {
            ("PROJECT", "PROJECT", 0, 30, 180),
            ("TITLE", "TITLE", 0, 20, 180),
            ("DRAWING_NO", "DRAWING NO.", 0, 10, 90),
            ("REVISION", "REV.", 90, 10, 30),
            ("SCALE", "SCALE", 120, 10, 30),
            ("SHEET", "SHEET", 150, 10, 30),
            ("COMPANY", "COMPANY", 0, 0, 90),
            ("DRAWN_BY", "DRAWN", 90, 0, 45),
            ("DATE", "DATE", 135, 0, 45),
        };

        /// <summary>
        /// Writes a drawing into a new DXF of AutoCAD 2013 (AC1027): its sheet as its settings lay it out, and its document.
        /// </summary>
        /// <param name="drawing">The drawing; one made or updated, so that it has its views.</param>
        /// <returns>The DXF, the same as <see cref="Drawing.Database"/> holds but for what was drawn there by hand.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened from a file and not updated since,
        /// so that it has no views to write.</exception>
        public static DxfDatabase Write(Drawing drawing)
        {
            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            if (drawing.Content == null)
            {
                throw new InvalidOperationException("A drawing opened from a file has no views to write until it is updated from its source.");
            }

            var database = new DxfDatabase("AC1027");
            WriteContent(database, drawing.Content, drawing.SettingsInUse, drawing.Edits, drawing.Tolerance);
            DrawingDocumentStore.Write(database, drawing.Records());
            return database;
        }

        /// <summary>
        /// Writes the entities of a drawing into a DXF, the hand edits applied: the views, their annotation and the sheet.
        /// </summary>
        internal static void WriteContent(DxfDatabase database, DrawingContent content, DrawingSettings settings, IReadOnlyList<DrawingEdit> edits, Tolerance tolerance)
        {
            bool layout = settings.ExportMode == DrawingExportMode.LayoutAndViewports;
            Prepare(database);
            SortedDictionary<string, List<DrawingEdit>> byId = ById(edits);
            DxfBlockTableRecord sheetSpace = layout ? SheetLayout(database).BlockTableRecord : database.ModelSpace;
            var dimensionIds = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var styles = new SortedDictionary<string, double>(StringComparer.Ordinal);
            var texts = new SortedDictionary<string, string>(StringComparer.Ordinal);

            foreach (DrawingView view in content.Views)
            {
                GeoPoint2 sheetOrigin = Moved(view.SheetOrigin, byId, view.Id);
                Placement place = layout ? new Placement(view.ModelOrigin, 1.0) : new Placement(sheetOrigin, view.Scale);
                WriteLines(database.ModelSpace, view, place, layout ? view.Scale : 1.0, tolerance.EqualPoint);

                for (int i = 0; i < view.Dimensions.Count; i++)
                {
                    DrawingDimension dimension = view.Dimensions[i];
                    dimension.WriteTo(database, new ViewPlacement2(view.Id, place.Origin, place.Scale));
                    dimensionIds[view.Id + "\u0001" + dimension.Id] = view.DimensionIds[i];
                    styles[view.DimensionIds[i]] = view.Scale;
                    texts[view.DimensionIds[i]] = dimension.Text;
                }

                foreach (DrawingMark mark in view.Marks)
                {
                    GeoVector2 offset = Offset(byId, mark.Id);
                    WriteMark(database.ModelSpace, mark, place, layout ? offset : new GeoVector2(offset.X * view.Scale, offset.Y * view.Scale));
                }

                GeoRectangle2 box = SheetBox(view, sheetOrigin);

                if (layout)
                {
                    WriteViewport(sheetSpace, view, box);
                }

                WriteTitle(sheetSpace, view, box);
            }

            FinishDimensions(database, dimensionIds, styles, texts, layout, settings, byId);
            WriteSheet(database, sheetSpace, content.Sheet, content.Scale);

            if (layout)
            {
                database.TileMode = false;
            }
        }

        /// <summary>
        /// Writes one mark: its leader from its point on the part to the lower left corner of its text, and its text, both
        /// tagged with the mark's id and its view's.
        /// </summary>
        internal static void WriteMark(DxfBlockTableRecord space, DrawingMark mark, Placement place, GeoVector2 offset)
        {
            GeoPoint2 at = new GeoPoint2(mark.TextPosition.X + offset.X, mark.TextPosition.Y + offset.Y);
            var leader = new DxfLine(place.ToSpace(mark.Anchor), place.ToSpace(at)) { Layer = DrawingMark.Layer };
            space.AppendEntity(leader);
            DrawingEntityTags.Tag(leader, mark.Id, mark.ViewId);
            var text = new DxfMText
            {
                Location = place.ToSpace(at),
                TextHeight = mark.TextHeight / place.Scale,
                Attachment = DxfAttachmentPoint.BottomLeft,
                Contents = mark.Text,
                Layer = DrawingMark.Layer,
            };

            space.AppendEntity(text);
            DrawingEntityTags.Tag(text, mark.Id, mark.ViewId);
        }

        /// <summary>
        /// Applies a hand edit of a dimension to the DIMENSION that draws it: its text overridden, moved as a whole, or erased.
        /// The text is overridden in the block that draws it too, so that it shows at once: the measured text, as the drawing
        /// wrote it, stands for &lt;&gt; in the override, and comes back where the override is empty.
        /// </summary>
        internal static void ApplyDimensionEdit(DxfDimension dimension, DrawingEdit edit, string measured)
        {
            switch (edit.Kind)
            {
                case Dxf.Viewer.CadEditKind.Erase:
                    if (!dimension.IsErased)
                    {
                        dimension.Erase();
                    }

                    return;

                case Dxf.Viewer.CadEditKind.Move:
                    if (TryReadMove(edit.NewValue, out GeoVector2 move))
                    {
                        dimension.TransformBy(GeoTransform3.Translation(new GeoVector3(move.X, move.Y, 0.0)));
                    }

                    return;

                case Dxf.Viewer.CadEditKind.Property:
                    string text = edit.NewValue ?? string.Empty;
                    dimension.DimensionText = text;
                    DxfBlockTableRecord block = dimension.DimBlock;

                    if (block != null)
                    {
                        foreach (DxfEntity entity in block)
                        {
                            if (entity is DxfMText shown && !shown.IsErased)
                            {
                                shown.Contents = text.Length == 0 ? measured : text.Replace("<>", measured);
                            }
                        }
                    }

                    return;
            }
        }

        /// <summary>Reads a displacement "dx,dy" as an edit's value gives it.</summary>
        internal static bool TryReadMove(string value, out GeoVector2 move)
        {
            move = new GeoVector2(0.0, 0.0);

            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(',');

            if (parts.Length != 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double dx)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double dy)
                || double.IsNaN(dx) || double.IsInfinity(dx) || double.IsNaN(dy) || double.IsInfinity(dy))
            {
                return false;
            }

            move = new GeoVector2(dx, dy);
            return true;
        }

        /// <summary>The sum of the moves recorded for an element, in the order they were made.</summary>
        internal static GeoVector2 Offset(SortedDictionary<string, List<DrawingEdit>> byId, string id)
        {
            double x = 0.0, y = 0.0;

            if (byId.TryGetValue(id, out List<DrawingEdit> edits))
            {
                foreach (DrawingEdit edit in edits)
                {
                    if (edit.Kind == Dxf.Viewer.CadEditKind.Move && TryReadMove(edit.NewValue, out GeoVector2 move))
                    {
                        x += move.X;
                        y += move.Y;
                    }
                }
            }

            return new GeoVector2(x, y);
        }

        /// <summary>The edits grouped by the id of the element they edit, each group in the order made.</summary>
        internal static SortedDictionary<string, List<DrawingEdit>> ById(IReadOnlyList<DrawingEdit> edits)
        {
            var byId = new SortedDictionary<string, List<DrawingEdit>>(StringComparer.Ordinal);

            foreach (DrawingEdit edit in edits)
            {
                if (!byId.TryGetValue(edit.ObjectId, out List<DrawingEdit> list))
                {
                    list = new List<DrawingEdit>();
                    byId.Add(edit.ObjectId, list);
                }

                list.Add(edit);
            }

            return byId;
        }

        /// <summary>The layout of the sheet: the one of its name, else an empty Layout1 renamed, else a new one.</summary>
        internal static DxfLayout SheetLayout(DxfDatabase database)
        {
            DxfLayout empty = null;

            foreach (DxfLayout layout in database.Layouts)
            {
                if (string.Equals(layout.LayoutName, "Model", StringComparison.OrdinalIgnoreCase) || layout.BlockTableRecord == null)
                {
                    continue;
                }

                if (string.Equals(layout.LayoutName, LayoutName, StringComparison.OrdinalIgnoreCase))
                {
                    return layout;
                }

                if (empty == null && string.Equals(layout.LayoutName, "Layout1", StringComparison.OrdinalIgnoreCase) && Count(layout.BlockTableRecord) == 0)
                {
                    empty = layout;
                }
            }

            if (empty != null)
            {
                empty.LayoutName = LayoutName;
                empty.Initialize();
                return empty;
            }

            var made = new DxfLayout { LayoutName = LayoutName };
            made.AddToLayoutDictionary(database, null);
            made.Initialize();
            return made;
        }

        /// <summary>A view's place on the sheet, in paper millimetres, at a sheet origin.</summary>
        internal static GeoRectangle2 SheetBox(DrawingView view, GeoPoint2 sheetOrigin)
            => new GeoRectangle2(new GeoPoint2(sheetOrigin.X + view.Box.Center.X / view.Scale, sheetOrigin.Y + view.Box.Center.Y / view.Scale), view.Box.Width / view.Scale, view.Box.Height / view.Scale);

        /// <summary>A point moved by the sum of the moves recorded for an element.</summary>
        internal static GeoPoint2 Moved(GeoPoint2 point, SortedDictionary<string, List<DrawingEdit>> byId, string id)
        {
            GeoVector2 move = Offset(byId, id);
            return new GeoPoint2(point.X + move.X, point.Y + move.Y);
        }

        /// <summary>The height of a view's title and its gap, on paper, which the box of each view keeps below what it draws.</summary>
        internal static double TitleBand => TitleHeight + 2.0 * TitleGap;

        private static int Count(DxfBlockTableRecord block)
        {
            int count = 0;

            foreach (DxfEntity entity in block)
            {
                if (!entity.IsErased)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Prepare(DxfDatabase database)
        {
            database.Insunits = DxfUnits.Millimeters;
            database.Measurement = DxfMeasurementValue.Metric;
            database.Psltscale = true;
            AddLinetype(database, "HIDDEN", "__ __ __ __ __ __ __ __", 6.35, -3.175);
            AddLinetype(database, "CENTER", "____ _ ____ _ ____ _ ____", 31.75, -6.35, 6.35, -6.35);
            AddLayer(database, ProjectedView2.VisibleLayer, 7, null);
            AddLayer(database, ProjectedView2.HiddenLayer, 8, "HIDDEN");
            AddLayer(database, ProjectedView2.CentreLayer, 1, "CENTER");
            AddLayer(database, ProjectedView2.HatchLayer, 8, null);
            AddLayer(database, DrawingDimension.Layer, 3, null);
            AddLayer(database, DrawingMark.Layer, 2, null);
            AddLayer(database, SheetLayer, 7, null);
            AddLayer(database, ViewportLayer, 8, null);
            database.LayerTable[ViewportLayer].IsPlottable = false;
        }

        private static void WriteLines(DxfBlockTableRecord space, DrawingView view, Placement place, double hatchScale, double tol)
        {
            // The crop taken in by the point tolerance, so that a line on its edge is kept whole.
            var crop = new Extent(ViewBuilder.Grow(view.Crop, tol));

            foreach (ProjectedFace2 section in view.Lines.SectionFaces)
            {
                var faceBox = new Extent(double.PositiveInfinity, double.PositiveInfinity, double.NegativeInfinity, double.NegativeInfinity);

                foreach (GeoPoint2 p in section.Face.Boundary.Vertices)
                {
                    faceBox.Add(p);
                }

                if (faceBox.X0 > crop.X1 || faceBox.X1 < crop.X0 || faceBox.Y0 > crop.Y1 || faceBox.Y1 < crop.Y0)
                {
                    continue;
                }

                var hatch = new DxfHatch { Layer = ProjectedView2.HatchLayer };
                hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
                hatch.PatternScale = hatchScale;
                hatch.AppendLoop(DxfHatchLoopTypes.External, place.ToSpace2(section.Face.Boundary.Vertices), null);

                foreach (GeoPolygon2 hole in section.Face.Holes)
                {
                    hatch.AppendLoop(DxfHatchLoopTypes.Default, place.ToSpace2(hole.Vertices), null);
                }

                space.AppendEntity(hatch);
                DrawingEntityTags.Tag(hatch, section.ObjectId, view.Id);
            }

            WriteEdges(space, view.Lines.VisibleEdges, ProjectedView2.VisibleLayer, view.Id, crop, place);

            if (view.HiddenLines)
            {
                WriteEdges(space, view.Lines.HiddenEdges, ProjectedView2.HiddenLayer, view.Id, crop, place);
            }
        }

        private static void WriteEdges(DxfBlockTableRecord space, IReadOnlyList<ProjectedEdge2> edges, string layer, string viewId, Extent crop, Placement place)
        {
            foreach (ProjectedEdge2 edge in edges)
            {
                if (!edge.IsArc)
                {
                    if (Clip.Line(edge.Line.StartPoint, edge.Line.EndPoint, crop, out GeoPoint2 a, out GeoPoint2 b))
                    {
                        Append(space, new DxfLine(place.ToSpace(a), place.ToSpace(b)), layer, edge.ObjectId, viewId);
                    }

                    continue;
                }

                GeoArc2 arc = edge.Arc;
                double start = arc.SweptAngle >= 0.0 ? arc.StartAngle : arc.StartAngle + arc.SweptAngle;
                double sweep = Math.Abs(arc.SweptAngle);
                GeoPoint3 centre = place.ToSpace(arc.Center);
                double radius = arc.Radius / place.Scale;

                foreach (double[] piece in Clip.Arc(arc.Center, arc.Radius, start, sweep, edge.IsCircle, crop))
                {
                    DxfEntity entity = piece == null
                        ? (DxfEntity)new DxfCircle(centre, GeoVector3.ZAxis, radius)
                        : new DxfArc(centre, radius, Turn(piece[0]), Turn(piece[0] + piece[1]));
                    Append(space, entity, layer, edge.ObjectId, viewId);
                }
            }
        }

        private static void Append(DxfBlockTableRecord space, DxfEntity entity, string layer, string objectId, string viewId)
        {
            entity.Layer = layer;
            space.AppendEntity(entity);
            DrawingEntityTags.Tag(entity, objectId, viewId);
        }

        /// <summary>An angle taken into [0, 2π).</summary>
        private static double Turn(double angle)
        {
            double turn = 2.0 * Math.PI;
            double wrapped = angle - turn * Math.Floor(angle / turn);
            return wrapped >= turn ? 0.0 : wrapped;
        }

        private static void WriteViewport(DxfBlockTableRecord sheetSpace, DrawingView view, GeoRectangle2 box)
        {
            var viewport = new DxfViewport { Layer = ViewportLayer };
            sheetSpace.AppendEntity(viewport);
            viewport.CenterPoint = new GeoPoint3(box.Center.X, box.Center.Y, 0.0);
            viewport.Width = box.Width;
            viewport.Height = box.Height;
            viewport.ViewCenter = new GeoPoint2(view.ModelOrigin.X + view.Box.Center.X, view.ModelOrigin.Y + view.Box.Center.Y);
            viewport.ViewHeight = view.Box.Height;
            viewport.On = true;
            DrawingEntityTags.Tag(viewport, view.Id, view.Id);
        }

        private static void WriteTitle(DxfBlockTableRecord sheetSpace, DrawingView view, GeoRectangle2 box)
        {
            var title = new DxfDBText
            {
                Position = new GeoPoint3(box.Center.X - box.Width / 2.0, box.Center.Y - box.Height / 2.0 + TitleGap, 0.0),
                Height = TitleHeight,
                TextString = view.Title,
                Layer = SheetLayer,
            };

            sheetSpace.AppendEntity(title);
            DrawingEntityTags.Tag(title, view.Id, view.Id);
        }

        /// <summary>
        /// Gives each dimension written the drawing's id for it in its tag, its style in model space, and its hand edits;
        /// and takes out the blocks of dimensions no longer drawn.
        /// </summary>
        private static void FinishDimensions(DxfDatabase database, SortedDictionary<string, string> ids, SortedDictionary<string, double> scales, SortedDictionary<string, string> texts, bool layout, DrawingSettings settings, SortedDictionary<string, List<DrawingEdit>> byId)
        {
            var used = new SortedSet<string>(StringComparer.Ordinal);
            var written = new List<DxfDimension>();

            foreach (DxfEntity entity in database.ModelSpace)
            {
                if (entity is DxfDimension found && !found.IsErased)
                {
                    written.Add(found);
                }
            }

            foreach (DxfDimension dimension in written)
            {
                if (!DrawingEntityTags.TryRead(dimension, out string local, out string viewId))
                {
                    continue;
                }

                if (viewId == null || !ids.TryGetValue(viewId + "\u0001" + local, out string id))
                {
                    // A dimension tagged with the drawing's id already, written before.
                    continue;
                }

                DrawingEntityTags.Tag(dimension, id, viewId);

                if (layout)
                {
                    dimension.DimensionStyle = ModelStyle(database, scales[id], settings.Dimensions);
                }

                if (byId.TryGetValue(id, out List<DrawingEdit> edits))
                {
                    foreach (DrawingEdit edit in edits)
                    {
                        ApplyDimensionEdit(dimension, edit, texts[id]);
                    }
                }
            }

            foreach (DxfDimension dimension in written)
            {
                if (!dimension.IsErased && dimension.DimBlock != null)
                {
                    used.Add(dimension.DimBlock.Name);
                }
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (!block.IsErased && block.Name.StartsWith("GH_D_", StringComparison.Ordinal) && !used.Contains(block.Name))
                {
                    block.Erase();
                }
            }
        }

        /// <summary>The dimension style of a view in model space: GH_MODEL_ and the scale, DIMLFAC 1, sizes on paper times the scale.</summary>
        private static DxfDimStyleTableRecord ModelStyle(DxfDatabase database, double scale, DimensionSettings settings)
        {
            string name = "GH_MODEL_" + scale.ToString("0.######", CultureInfo.InvariantCulture);

            if (database.DimStyleTable.Has(name))
            {
                return (DxfDimStyleTableRecord)database.DimStyleTable[name];
            }

            double height = settings.TextHeight * scale;
            var style = new DxfDimStyleTableRecord
            {
                Name = name,
                Dimlfac = 1.0,
                Dimtxt = height,
                Dimasz = height,
                Dimexo = height / 4.0,
                Dimexe = height / 2.0,
                Dimgap = height / 4.0,
                Dimlunit = 2,
                Dimdec = DrawingDimension.Decimals(settings.Rounding),
                Dimrnd = settings.Rounding,
                Dimzin = 8,
                Dimdsep = '.',
                Dimtad = 1,
                Dimtih = false,
                Dimtoh = false,
                Dimtofl = true,
            };

            database.DimStyleTable.Add(style);
            return style;
        }

        private static void WriteSheet(DxfDatabase database, DxfBlockTableRecord space, DrawingSheet sheet, double scale)
        {
            Rectangle(space, new Extent(0.0, 0.0, sheet.Width, sheet.Height), DrawingBuilder.SheetId);
            Rectangle(space, new Extent(sheet.Frame), DrawingBuilder.SheetId);
            WriteTitleBlock(database, space, sheet, scale);

            if (sheet.PartListBox.HasValue)
            {
                WritePartList(space, sheet.PartList, new Extent(sheet.PartListBox.Value));
            }
        }

        private static void Rectangle(DxfBlockTableRecord space, Extent box, string id)
        {
            var polyline = new DxfPolyline { Layer = SheetLayer };
            polyline.AddVertexAt(0, new GeoPoint2(box.X0, box.Y0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(box.X1, box.Y0), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(box.X1, box.Y1), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(3, new GeoPoint2(box.X0, box.Y1), 0.0, 0.0, 0.0);
            polyline.Closed = true;
            space.AppendEntity(polyline);
            DrawingEntityTags.Tag(polyline, id, null);
        }

        private static void WriteTitleBlock(DxfDatabase database, DxfBlockTableRecord space, DrawingSheet sheet, double scale)
        {
            DxfBlockTableRecord block = TitleBlockDefinition(database);
            var corner = new Extent(sheet.TitleBlockBox);
            var insert = new DxfBlockReference(new GeoPoint3(corner.X0, corner.Y0, 0.0), block) { Layer = SheetLayer };
            space.AppendEntity(insert);
            TitleBlock title = sheet.TitleBlock;
            string[] values =
            {
                title.Project, title.Title, title.DrawingNumber, title.Revision, "1:" + scale.ToString("0.######", CultureInfo.InvariantCulture), sheet.SizeName,
                title.Company, title.DrawnBy, title.Date,
            };

            for (int i = 0; i < TitleCells.Length; i++)
            {
                var at = new GeoPoint3(corner.X0 + TitleCells[i].X + 2.0, corner.Y0 + TitleCells[i].Y + 1.5, 0.0);
                var attribute = new DxfAttributeReference(at, values[i], TitleCells[i].Tag, null) { Height = 3.0, Layer = SheetLayer };
                insert.AttributeCollection.AppendAttribute(attribute);
            }

            DrawingEntityTags.Tag(insert, DrawingBuilder.TitleBlockId, null);
        }

        private static DxfBlockTableRecord TitleBlockDefinition(DxfDatabase database)
        {
            if (database.BlockTable.Has(TitleBlockName))
            {
                return database.BlockTable[TitleBlockName];
            }

            var block = new DxfBlockTableRecord { Name = TitleBlockName, Origin = new GeoPoint3(0.0, 0.0, 0.0) };
            database.BlockTable.Add(block);
            double w = TitleBlock.Width, h = TitleBlock.Height;
            BlockLine(block, 0, 0, w, 0);
            BlockLine(block, w, 0, w, h);
            BlockLine(block, w, h, 0, h);
            BlockLine(block, 0, h, 0, 0);
            BlockLine(block, 0, 10, w, 10);
            BlockLine(block, 0, 20, w, 20);
            BlockLine(block, 0, 30, w, 30);

            foreach ((string tag, string label, double x, double y, double width) in TitleCells)
            {
                if (x > 0.0)
                {
                    BlockLine(block, x, y, x, y + 10);
                }

                var text = new DxfDBText { Position = new GeoPoint3(x + 1.0, y + 7.0, 0.0), Height = 1.8, TextString = label, Layer = "0" };
                block.AppendEntity(text);
                var definition = new DxfAttributeDefinition(new GeoPoint3(x + 2.0, y + 1.5, 0.0), string.Empty, tag, label, null) { Height = 3.0, Layer = "0" };
                block.AppendEntity(definition);
            }

            return block;
        }

        private static void BlockLine(DxfBlockTableRecord block, double x0, double y0, double x1, double y1)
            => block.AppendEntity(new DxfLine(new GeoPoint3(x0, y0, 0.0), new GeoPoint3(x1, y1, 0.0)) { Layer = "0" });

        private static void WritePartList(DxfBlockTableRecord space, IReadOnlyList<PartListRow> rows, Extent box)
        {
            double row = DrawingSheet.PartListRowHeight;

            for (int i = 0; i <= rows.Count + 1; i++)
            {
                double y = box.Y1 - i * row;
                SheetLine(space, box.X0, y, box.X1, y);
            }

            double x = box.X0;

            foreach ((string _, double width) in PartColumns)
            {
                SheetLine(space, x, box.Y0, x, box.Y1);
                x += width;
            }

            SheetLine(space, box.X1, box.Y0, box.X1, box.Y1);

            for (int i = 0; i <= rows.Count; i++)
            {
                string[] cells = i == 0
                    ? Array.ConvertAll(PartColumns, c => c.Head)
                    : new[]
                    {
                        rows[i - 1].Mark,
                        rows[i - 1].Quantity.ToString(CultureInfo.InvariantCulture),
                        rows[i - 1].Profile,
                        rows[i - 1].Material,
                        rows[i - 1].Length.ToString("0", CultureInfo.InvariantCulture),
                        rows[i - 1].Name,
                    };
                double y = box.Y1 - (i + 1) * row + 1.75;
                x = box.X0;

                for (int c = 0; c < cells.Length; c++)
                {
                    if (cells[c].Length > 0)
                    {
                        var text = new DxfDBText { Position = new GeoPoint3(x + 1.0, y, 0.0), Height = 2.5, TextString = cells[c], Layer = SheetLayer };
                        space.AppendEntity(text);
                        DrawingEntityTags.Tag(text, DrawingBuilder.PartListId, null);
                    }

                    x += PartColumns[c].Width;
                }
            }
        }

        private static void SheetLine(DxfBlockTableRecord space, double x0, double y0, double x1, double y1)
        {
            var line = new DxfLine(new GeoPoint3(x0, y0, 0.0), new GeoPoint3(x1, y1, 0.0)) { Layer = SheetLayer };
            space.AppendEntity(line);
            DrawingEntityTags.Tag(line, DrawingBuilder.PartListId, null);
        }

        private static void AddLinetype(DxfDatabase database, string name, string description, params double[] dashes)
        {
            if (database.LinetypeTable.Has(name))
            {
                return;
            }

            var linetype = new DxfLinetypeTableRecord { Name = name, AsciiDescription = description, NumDashes = dashes.Length };

            for (int i = 0; i < dashes.Length; i++)
            {
                linetype.SetDashLengthAt(i, dashes[i]);
            }

            database.LinetypeTable.Add(linetype);
        }

        private static void AddLayer(DxfDatabase database, string name, int color, string linetype)
        {
            if (database.LayerTable.Has(name))
            {
                return;
            }

            var layer = new DxfLayerTableRecord { Name = name, ColorIndex = color };

            if (linetype != null)
            {
                layer.LinetypeName = linetype;
            }

            database.LayerTable.Add(layer);
        }

        /// <summary>Where a view is written: a point of the view at (x, y) goes to the origin plus (x, y) over the scale.</summary>
        internal readonly struct Placement
        {
            public Placement(GeoPoint2 origin, double scale)
            {
                Origin = origin;
                Scale = scale;
            }

            public GeoPoint2 Origin { get; }

            public double Scale { get; }

            public GeoPoint3 ToSpace(GeoPoint2 p) => new GeoPoint3(Origin.X + p.X / Scale, Origin.Y + p.Y / Scale, 0.0);

            public GeoPoint2[] ToSpace2(IReadOnlyList<GeoPoint2> points)
            {
                var moved = new GeoPoint2[points.Count];

                for (int i = 0; i < moved.Length; i++)
                {
                    moved[i] = new GeoPoint2(Origin.X + points[i].X / Scale, Origin.Y + points[i].Y / Scale);
                }

                return moved;
            }
        }
    }
}
