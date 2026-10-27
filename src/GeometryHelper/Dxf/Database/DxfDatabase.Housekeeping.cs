using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Keeping the drawing tidy, as AutoCAD's <c>Database</c> does: its empty objects counted and erased, the references to its
    /// objects counted, what nothing uses found for purging, and its extents worked out anew.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Counts the empty objects of the drawing, as AutoCAD's <c>CountEmptyObjects</c>: those of the kinds the flags of
        /// <see cref="DxfEraseFlags"/> name, in model space, paper space and every block, as
        /// <see cref="EraseEmptyObjects"/> would erase them.
        /// </summary>
        /// <param name="flags">The kinds, flags of <see cref="DxfEraseFlags"/>: <c>(int)DxfEraseFlags.ZeroLengthCurve</c> for
        /// the curves of no length, -1 for every kind.</param>
        /// <returns>How many there are.</returns>
        public int CountEmptyObjects(int flags) => EmptyObjects(flags).Count;

        /// <summary>
        /// Erases the empty objects of the drawing, as AutoCAD's <c>EraseEmptyObjects</c>: those of the kinds the flags of
        /// <see cref="DxfEraseFlags"/> name, in model space, paper space and every block, each erased as its own
        /// <see cref="DxfEntity.Erase"/> erases it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A curve of no length (<see cref="DxfEraseFlags.ZeroLengthCurve"/>) is a LINE whose ends are one point, a CIRCLE or an
        /// ARC of no radius, an ELLIPSE whose major axis has no length, a LWPOLYLINE or a POLYLINE all of whose vertices are one
        /// point, and a SPLINE all of whose control and fit points are. Points are one where no coordinate is apart by more than
        /// 4 eps times the larger magnitude of their coordinates (eps the machine epsilon, 2.2E-16, so 4 to 8 units of the last
        /// place), not to a tolerance of the drawing: a block may be drawn at any scale, and a line 0.0008 long in a block an
        /// INSERT draws at a scale of 10 000 is 8 long there. So a line from (1000, 0, 0) to (1000.0008, 0, 0) is kept, and one
        /// whose ends are 1.1E-13 apart at x 1000, one unit of the last place, is empty; a radius is nought to the same bound of
        /// its centre. A LINE, a CIRCLE, an ARC, a LWPOLYLINE or a 2D
        /// POLYLINE with a thickness is never empty, as it draws a segment of that height: a LINE from (5, 5, 0) to (5, 5, 0) of
        /// thickness 100 is kept. An empty text (<see cref="DxfEraseFlags.EmptyText"/>) is a TEXT whose words are empty or
        /// spaces, and an MTEXT whose words without their formatting are; an attribute and its definition are not texts here.
        /// </para>
        /// <para>
        /// The objects are found before any is erased, so that <see cref="CountEmptyObjects"/> gives the number this erases.
        /// </para>
        /// </remarks>
        /// <param name="flags">The kinds, flags of <see cref="DxfEraseFlags"/>.</param>
        /// <returns>How many were erased.</returns>
        public int EraseEmptyObjects(int flags)
        {
            List<DxfEntity> empty = EmptyObjects(flags);

            foreach (DxfEntity entity in empty)
            {
                entity.Erase();
            }

            if (empty.Count > 0)
            {
                GeometryHelperLog.Info("DxfDatabase: " + empty.Count.ToString(CultureInfo.InvariantCulture) + " empty objects were erased.");
            }

            return empty.Count;
        }

        /// <summary>
        /// Counts the hard references to objects of the drawing, as AutoCAD's <c>CountHardReferences</c>, each added to the
        /// count of the same place: every hard pointer and hard owner of the drawing that names the object's handle (340 to 349,
        /// 360 to 369, 390 to 399, 480 and 481 of every record, an extension dictionary's 360 among them), each handle of
        /// extended data of any application (1005, as the overrides of a dimension name its arrow blocks and its text style), the
        /// arrow an override of a dimension of R12 names by name (1070 5, 6 or 7, then 1000 the block's name), and each layer a
        /// viewport freezes (331); and for a record of a table each group where the DXF writes by name what AutoCAD's database
        /// holds as a hard pointer to it: the layer (8) and linetype (6) of every entity, a vertex and an attribute among them,
        /// the linetype of a layer and of a multiline style, the text style of a text, of a cell of a table and of a table
        /// style (7), the dimension style of a dimension, a leader or a tolerance (3), the block of an INSERT, a dimension or a
        /// table (2), a layer a viewport of a drawing before R2000 freezes by name (1003), and the variables of the header that
        /// name one (<c>$CLAYER</c>, <c>$CELTYPE</c>, <c>$TEXTSTYLE</c>, <c>$DIMSTYLE</c>, <c>$DIMBLK</c>, <c>$CMLSTYLE</c>
        /// and their kin). An entity is owned hard by its block, or by the entity it follows, and counts that one besides.
        /// </summary>
        /// <remarks>
        /// The drawing is walked once for all the objects. A layer steel two lines are on and a viewport freezes counts 3; a
        /// group left out, the record leaving its layer or style to the default (a TEXT with no 7 is in Standard), is not
        /// counted.
        /// </remarks>
        /// <param name="ids">The objects, of this drawing and not erased.</param>
        /// <param name="count">The counts, one for each object at its place, each added to.</param>
        /// <exception cref="ArgumentNullException">Thrown when the objects or the counts are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an object is null, erased or not of this drawing, or the counts are
        /// fewer than the objects.</exception>
        public void CountHardReferences(IReadOnlyList<DxfDBObject> ids, int[] count)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            if (count == null)
            {
                throw new ArgumentNullException(nameof(count));
            }

            if (count.Length < ids.Count)
            {
                throw new ArgumentException("The counts are " + count.Length.ToString(CultureInfo.InvariantCulture) + " for " + ids.Count.ToString(CultureInfo.InvariantCulture) + " objects: one is given for each.", nameof(count));
            }

            CheckOwn(ids, nameof(ids));
            DxfFile.Uses uses = File.CountUses();

            for (int i = 0; i < ids.Count; i++)
            {
                count[i] += References(ids[i], uses);
            }
        }

        /// <summary>
        /// Keeps of objects those nothing uses, as AutoCAD's <c>Purge</c> leaves in its collection the ids that can be erased:
        /// each object no hard reference names, as <see cref="CountHardReferences"/> counts them, and that AutoCAD does not keep
        /// itself. AutoCAD keeps layer 0 and Defpoints, the linetypes ByBlock, ByLayer and Continuous, the text style Standard
        /// and every style of a file of shapes (70 of 1), the dimension style, multiline style, multileader style and table
        /// style Standard, the materials Global, ByLayer and ByBlock, the plot style Normal, every visual style, the variables of
        /// a dictionary of variables (DICTIONARYVAR) and those of images and of wipeouts, the blocks of the layouts, the external
        /// references and every record one brought (a name with a | in it), the tables, the named object dictionary and the
        /// dictionaries it names; and an entity, owned by its block, is never one to purge. A layer, a linetype or a block that
        /// any pointer of the drawing names, a soft one too, as a 330 of an XRECORD's data, is kept, as its own <c>Erase</c>
        /// refuses it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The objects are not erased. A layer, a linetype, a dimension style, a block, an XRECORD or a GROUP given back is
        /// erased by its own <c>Erase</c>, which refuses none of them: a handle of the extended data of any application counts
        /// as a hard reference, and a record a soft pointer names is kept, as <c>Erase</c> refuses both. A text style, an
        /// MLINESTYLE, a MATERIAL, a DICTIONARYVAR and the other objects of the OBJECTS section the library does not erase yet: their <c>Erase</c> throws
        /// <see cref="NotSupportedException"/>. A reference from an object given counts as any other, as AutoCAD counts it, so
        /// that a linetype only a layer given draws with is kept until that layer is erased and the purge run again.
        /// </para>
        /// <para>
        /// <c>db.Purge(db.LayerTable.Cast&lt;DxfDBObject&gt;().ToList())</c> of a drawing whose layers are 0, steel with a line on
        /// it and an empty one gives the empty one.
        /// </para>
        /// </remarks>
        /// <param name="ids">The objects, of this drawing and not erased.</param>
        /// <returns>Those of them nothing uses, in their order.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the objects are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an object is null, erased or not of this drawing.</exception>
        public IReadOnlyList<DxfDBObject> Purge(IReadOnlyList<DxfDBObject> ids)
        {
            if (ids == null)
            {
                throw new ArgumentNullException(nameof(ids));
            }

            CheckOwn(ids, nameof(ids));
            DxfFile.Uses uses = File.CountUses();
            var free = new List<DxfDBObject>();
            string root = NamedObjectsDictionary?.Handle;

            foreach (DxfDBObject value in ids)
            {
                // A layer, a linetype or a block any pointer names, a soft one too, is one its Erase refuses.
                bool pointed = (value is DxfLayerTableRecord || value is DxfLinetypeTableRecord || value is DxfBlockTableRecord) && uses.IsPointedAt(value.Handle);

                if (!pointed && !KeptByAutoCad(value, root) && References(value, uses) == 0)
                {
                    free.Add(value);
                }
            }

            return free.AsReadOnly();
        }

        /// <summary>
        /// Works the extents of model space and paper space out anew, as AutoCAD's <c>UpdateExt</c>: <c>$EXTMIN</c> and
        /// <c>$EXTMAX</c> the box of the <see cref="DxfEntity.GeometricExtents"/> of every entity of model space, <c>$PEXTMIN</c>
        /// and <c>$PEXTMAX</c> that of paper space, so that they shrink as well as grow; an entity with no extents, an IMAGE or
        /// a LINE of no length, passed over, and a text counted as the point it stands at, there being no fonts here. A space
        /// with nothing of extents in it is given AutoCAD's empty extents, 1E+20 for the least corner and -1E+20 for the most.
        /// A value the header has already, to the bit, is not written.
        /// </summary>
        /// <remarks>
        /// A drawing whose header says (0, 0, 0) to (5110, 300, 0), from a line drawn and erased since, with a circle of radius 5
        /// about (100, 50) left in model space, is given (95, 45, 0) to (105, 55, 0).
        /// </remarks>
        /// <param name="doBestFit">Whether an INSERT counts by the box of the shapes it places rather than the box of its block
        /// placed, as AutoCAD's best fit: the library always takes the shapes placed, so either gives the same.</param>
        /// <exception cref="InvalidOperationException">Thrown when the file has no HEADER section to write the extents in.</exception>
        public void UpdateExt(bool doBestFit)
        {
            double[] model = BoxOf(ModelSpace, out int skippedModel);
            double[] paper = BoxOf(PaperSpace, out int skippedPaper);
            SetBox("$EXTMIN", "$EXTMAX", model);
            SetBox("$PEXTMIN", "$PEXTMAX", paper);

            if (skippedModel + skippedPaper > 0)
            {
                GeometryHelperLog.Debug("DxfDatabase: the extents were worked out anew, " + (skippedModel + skippedPaper).ToString(CultureInfo.InvariantCulture) + " entities having no extents to count.");
            }
        }

        /// <summary>The entities of the drawing empty in the kinds the flags name, as <see cref="EraseEmptyObjects"/> says, found in one walk of every block.</summary>
        private List<DxfEntity> EmptyObjects(int flags)
        {
            bool curves = (flags & (int)DxfEraseFlags.ZeroLengthCurve) != 0;
            bool texts = (flags & (int)DxfEraseFlags.EmptyText) != 0;
            var empty = new List<DxfEntity>();

            foreach (DxfBlockTableRecord block in BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if ((curves && IsZeroLength(entity)) || (texts && IsEmptyText(entity)))
                    {
                        empty.Add(entity);
                    }
                }
            }

            return empty;
        }

        /// <summary>
        /// Whether an entity is a curve of no length, as <see cref="EraseEmptyObjects"/> says: its points one point to the
        /// rounding of their coordinates, and no thickness that would draw it as a segment.
        /// </summary>
        private static bool IsZeroLength(DxfEntity entity)
        {
            switch (entity)
            {
                case DxfLine line:
                    return line.Thickness == 0.0 && SamePoint(line.StartPoint, line.EndPoint);
                case DxfCircle circle:
                    return circle.Thickness == 0.0 && !(circle.Radius > RoundingOf(circle.Center));
                case DxfArc arc:
                    return arc.Thickness == 0.0 && !(arc.Radius > RoundingOf(arc.Center));
                case DxfEllipse ellipse:
                    return !(ellipse.MajorAxis.Length > RoundingOf(ellipse.Center));
                case DxfPolyline polyline:
                    if (polyline.Thickness != 0.0)
                    {
                        return false;
                    }

                    // The vertices in the polyline's own system, at its elevation: the move to the world is rigid, and is not
                    // worked out for each vertex.
                    double elevation = polyline.Elevation;
                    int count = polyline.NumberOfVertices;
                    GeoPoint2 first = count > 0 ? polyline.GetPoint2dAt(0) : default;

                    for (int i = 1; i < count; i++)
                    {
                        GeoPoint2 vertex = polyline.GetPoint2dAt(i);

                        if (!SamePoint(new GeoPoint3(first.X, first.Y, elevation), new GeoPoint3(vertex.X, vertex.Y, elevation)))
                        {
                            return false;
                        }
                    }

                    return true;
                case DxfPolyline2d polyline2d:
                    return polyline2d.Thickness == 0.0 && OnePoint(Positions(polyline2d));
                case DxfPolyline3d polyline3d:
                    return OnePoint(Positions(polyline3d));
                case DxfSpline spline:
                    return OnePoint(spline.ControlPoints.Concat(spline.FitPoints));
                default:
                    return false;
            }
        }

        /// <summary>The places of the vertices of a POLYLINE, one at a time.</summary>
        private static IEnumerable<GeoPoint3> Positions(IEnumerable<DxfVertex> vertices)
        {
            foreach (DxfVertex vertex in vertices)
            {
                yield return vertex is DxfVertex2d flat ? flat.Position : ((DxfPolylineVertex3d)vertex).Position;
            }
        }

        /// <summary>Whether points are none, or all one point, the first of them, as <see cref="SamePoint"/> says; the walk stops at the first that is not.</summary>
        private static bool OnePoint(IEnumerable<GeoPoint3> points)
        {
            bool any = false;
            GeoPoint3 first = default;

            foreach (GeoPoint3 point in points)
            {
                if (!any)
                {
                    first = point;
                    any = true;
                }
                else if (!SamePoint(first, point))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Whether two points are one to the rounding of their coordinates: no coordinate apart by more than
        /// <see cref="RoundingOf(GeoPoint3, GeoPoint3)"/>, 4 eps times the larger magnitude of their coordinates, so that a point
        /// a thousandth off another is another point wherever it is drawn.
        /// </summary>
        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b)
        {
            double bound = RoundingOf(a, b);
            return Math.Abs(a.X - b.X) <= bound && Math.Abs(a.Y - b.Y) <= bound && Math.Abs(a.Z - b.Z) <= bound;
        }

        /// <summary>The rounding of a point's coordinates, as <see cref="RoundingOf(GeoPoint3, GeoPoint3)"/> says.</summary>
        private static double RoundingOf(GeoPoint3 point) => RoundingOf(point, point);

        /// <summary>
        /// The rounding of the coordinates of two points: 4 eps times the largest of their magnitudes, eps the machine epsilon
        /// (2.2E-16), which is 4 to 8 units of the last place of that coordinate: 4.4E-13 for a coordinate of 500, nought for
        /// points at the origin.
        /// </summary>
        private static double RoundingOf(GeoPoint3 a, GeoPoint3 b)
        {
            double largest = Math.Max(Math.Max(Math.Max(Math.Abs(a.X), Math.Abs(a.Y)), Math.Abs(a.Z)), Math.Max(Math.Max(Math.Abs(b.X), Math.Abs(b.Y)), Math.Abs(b.Z)));
            return 4.0 * 2.220446049250313E-16 * largest;
        }

        /// <summary>Whether an entity is a TEXT or an MTEXT of no words, as <see cref="EraseEmptyObjects"/> says.</summary>
        private static bool IsEmptyText(DxfEntity entity)
        {
            if (entity is DxfMText mtext)
            {
                return string.IsNullOrWhiteSpace(mtext.Text);
            }

            return entity.Item.Type == "TEXT" && entity is DxfDBText text && string.IsNullOrWhiteSpace(text.TextString);
        }

        /// <summary>Refuses objects that are not of this drawing, or are erased or null.</summary>
        private void CheckOwn(IReadOnlyList<DxfDBObject> ids, string parameter)
        {
            foreach (DxfDBObject value in ids)
            {
                if (value == null)
                {
                    throw new ArgumentException("The objects hold a null.", parameter);
                }

                if (!value.IsResident || value.IsErased || value.Item.File != File)
                {
                    throw new ArgumentException("The " + value + " is not an object of this drawing: erased, of another drawing, or made in code.", parameter);
                }
            }
        }

        /// <summary>The hard references to an object, as <see cref="CountHardReferences"/> counts them.</summary>
        private static int References(DxfDBObject value, DxfFile.Uses uses)
        {
            int count = uses.OfHandle(value.Handle);

            switch (value)
            {
                case DxfLayerTableRecord layer:
                    return count + uses.OfName("LAYER", layer.Name);
                case DxfLinetypeTableRecord linetype:
                    return count + uses.OfName("LTYPE", linetype.Name);
                case DxfTextStyleTableRecord style:
                    return count + uses.OfName("STYLE", style.Name);
                case DxfDimStyleTableRecord dimensionStyle:
                    return count + uses.OfName("DIMSTYLE", dimensionStyle.Name);
                case DxfBlockTableRecord block:
                    return count + uses.OfName("BLOCK", block.Name);
                case DxfMlineStyle mlineStyle:
                    return count + uses.OfName("MLINESTYLE", mlineStyle.Name);
                case DxfEntity _:
                    return count + 1;
                default:
                    return count;
            }
        }

        /// <summary>Whether AutoCAD keeps an object whatever refers to it, as <see cref="Purge"/> says.</summary>
        private bool KeptByAutoCad(DxfDBObject value, string root)
        {
            switch (value)
            {
                case DxfSymbolTable _:
                    return true;
                case DxfSymbolTableRecord record when record.IsDependent:
                    return true;
                case DxfLayerTableRecord layer:
                    return layer.Name == "0" || string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase);
                case DxfLinetypeTableRecord linetype:
                    return IsOneOf(linetype.Name, "ByBlock", "ByLayer", "Continuous");
                case DxfTextStyleTableRecord style:
                    return style.IsShapeFile || IsOneOf(style.Name, "Standard");
                case DxfDimStyleTableRecord dimensionStyle:
                    return IsOneOf(dimensionStyle.Name, "Standard");
                case DxfBlockTableRecord block:
                    return block.IsLayout || block.IsFromExternalReference;
                case DxfMlineStyle mlineStyle:
                    return IsOneOf(mlineStyle.Name, "Standard");
                case DxfDBDictionary dictionary:
                    return root != null && (string.Equals(dictionary.Handle, root, StringComparison.OrdinalIgnoreCase) || string.Equals(dictionary.OwnerHandle, root, StringComparison.OrdinalIgnoreCase));
                case DxfEntity _:
                    return true;
                default:
                    return KeptObject(value);
            }
        }

        /// <summary>
        /// Whether an object of the OBJECTS section is one AutoCAD never purges, as <see cref="Purge"/> says: a variable of a
        /// dictionary of variables (DICTIONARYVAR), the variables of images and of wipeouts, every visual style, the materials
        /// Global, ByLayer and ByBlock, the multileader style and the table style Standard, and the plot style Normal.
        /// </summary>
        private bool KeptObject(DxfDBObject value)
        {
            switch (value.Item.Type)
            {
                case "DICTIONARYVAR":
                case "RASTERVARIABLES":
                case "WIPEOUTVARIABLES":
                case "VISUALSTYLE":
                    return true;
                case "MATERIAL":
                    return IsOneOf(DxfReader.NameOf(File.ValueOf(value.Item, 1) ?? string.Empty).Trim(), "Global", "ByLayer", "ByBlock");
                case "MLEADERSTYLE":
                case "TABLESTYLE":
                    return IsOneOf(EntryName(value), "Standard");
                case "ACDBPLACEHOLDER":
                    return IsOneOf(EntryName(value), "Normal");
                default:
                    return false;
            }
        }

        /// <summary>The name the dictionary that owns an object gives it; empty where no dictionary names it.</summary>
        private string EntryName(DxfDBObject value)
        {
            DxfItem owner = File.ObjectItem(value.OwnerHandle);
            return (owner == null ? null : (DxfDBObject.OfObject(owner) as DxfDBDictionary)?.NameOf(value)) ?? string.Empty;
        }

        /// <summary>Whether a name is one of those given, letters of either case the same.</summary>
        private static bool IsOneOf(string name, params string[] names) => Array.Exists(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The box of the extents of the entities of a space, least x, y, z then most; null where none has extents.</summary>
        private static double[] BoxOf(DxfBlockTableRecord space, out int skipped)
        {
            double[] box = null;
            skipped = 0;

            foreach (DxfEntity entity in space)
            {
                GeoAabb3 extents;

                try
                {
                    extents = entity.GeometricExtents;
                }
                catch (InvalidOperationException)
                {
                    // An entity with no shape the library reads has no extents to count, as AutoCAD passes over one.
                    skipped++;
                    continue;
                }

                double[] own = { extents.Min.X, extents.Min.Y, extents.Min.Z, extents.Max.X, extents.Max.Y, extents.Max.Z };

                if (box == null)
                {
                    box = own;
                    continue;
                }

                for (int i = 0; i < 3; i++)
                {
                    box[i] = Math.Min(box[i], own[i]);
                    box[i + 3] = Math.Max(box[i + 3], own[i + 3]);
                }
            }

            return box;
        }

        /// <summary>Writes a box as two variables of the header, AutoCAD's empty extents where there is none.</summary>
        private void SetBox(string least, string most, double[] box)
        {
            box = box ?? new[] { 1E+20, 1E+20, 1E+20, -1E+20, -1E+20, -1E+20 };
            SetPoint(least, new GeoPoint3(box[0], box[1], box[2]));
            SetPoint(most, new GeoPoint3(box[3], box[4], box[5]));
        }
    }
}
