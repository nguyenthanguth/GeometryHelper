using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// What the writers of marks and symbols share: their layers and linetypes made where a drawing has none of their
    /// names, and the lines, arrows and texts they write into model space, each tagged with its element and view.
    /// </summary>
    internal static class SymbolDxf
    {
        /// <summary>The layer of marks, in colour 2, as the drawing's writer makes it.</summary>
        internal static void MarkLayer(DxfDatabase database) => AddLayer(database, DrawingMark.Layer, 2, null);

        /// <summary>The layer of symbols, in colour 6.</summary>
        internal static void SymbolLayer(DxfDatabase database) => AddLayer(database, SymbolSettings.Layer, 6, null);

        /// <summary>The layer of centre lines with the linetype CENTER, both as a view writes them.</summary>
        internal static void CentreLayer(DxfDatabase database)
        {
            AddLinetype(database, "CENTER", "____ _ ____ _ ____ _ ____", 31.75, -6.35, 6.35, -6.35);
            AddLayer(database, Projection.ProjectedView2.CentreLayer, 1, "CENTER");
        }

        /// <summary>The linetype HIDDEN, as a view writes it, for the dashed line of a weld symbol.</summary>
        internal static void HiddenLinetype(DxfDatabase database) => AddLinetype(database, "HIDDEN", "__ __ __ __ __ __ __ __", 6.35, -3.175);

        /// <summary>A line of the view, taken to the sheet, on a layer, tagged.</summary>
        internal static DxfLine Line(DxfDatabase database, ViewPlacement2 placement, GeoPoint2 a, GeoPoint2 b, string layer, string id)
        {
            var line = new DxfLine(placement.ToSheet(a), placement.ToSheet(b)) { Layer = layer };
            return Append(database, line, placement, id);
        }

        /// <summary>A closed outline of the view, taken to the sheet, on a layer, tagged.</summary>
        internal static DxfPolyline Outline(DxfDatabase database, ViewPlacement2 placement, GeoPoint2[] corners, string layer, string id)
        {
            var outline = new DxfPolyline { Layer = layer };

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint3 p = placement.ToSheet(corners[i]);
                outline.AddVertexAt(i, new GeoPoint2(p.X, p.Y), 0.0, 0.0, 0.0);
            }

            outline.Closed = true;
            return Append(database, outline, placement, id);
        }

        /// <summary>
        /// A closed filled arrow, its tip at a point of the view and pointing along a unit direction, as long as given in
        /// view millimetres and a third of that wide, as a dimension's arrows are drawn.
        /// </summary>
        internal static void Arrow(DxfDatabase database, ViewPlacement2 placement, GeoPoint2 tip, GeoVector2 pointing, double length, string layer, string id)
        {
            var back = new GeoPoint2(tip.X - pointing.X * length, tip.Y - pointing.Y * length);
            var across = new GeoVector2(-pointing.Y * length / 6.0, pointing.X * length / 6.0);
            var arrow = new DxfSolid(
                placement.ToSheet(tip),
                placement.ToSheet(new GeoPoint2(back.X + across.X, back.Y + across.Y)),
                placement.ToSheet(new GeoPoint2(back.X - across.X, back.Y - across.Y)))
            {
                Layer = layer,
            };

            Append(database, arrow, placement, id);
        }

        /// <summary>A text, its middle at a point of the view, its height in view millimetres, on a layer, tagged.</summary>
        internal static void Text(DxfDatabase database, ViewPlacement2 placement, GeoPoint2 middle, double height, string contents, string layer, string id)
        {
            var text = new DxfMText
            {
                Location = placement.ToSheet(middle),
                TextHeight = height / placement.Scale,
                Attachment = DxfAttachmentPoint.MiddleCenter,
                Contents = contents,
                Layer = layer,
            };

            Append(database, text, placement, id);
        }

        /// <summary>
        /// The width a text is taken to fill: each character as wide as the text is high, as AutoCAD's txt font and the
        /// presenter draw it: "4 x M20 x 80" 1 high is inked 11.4 wide there, and taken as 12.
        /// </summary>
        internal static double TextWidth(string text, double height) => System.Math.Max(1, text.Length) * height;

        private static T Append<T>(DxfDatabase database, T entity, ViewPlacement2 placement, string id)
            where T : DxfEntity
        {
            database.ModelSpace.AppendEntity(entity);
            DrawingEntityTags.Tag(entity, id, placement.ViewId);
            return entity;
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
    }
}
