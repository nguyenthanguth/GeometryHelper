using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Breaks what R12 cannot hold into what it can, in a drawing of a later version, before it is written as R12, as AutoCAD
    /// 2021 does when it saves one as R12: an LWPOLYLINE is a POLYLINE, an ellipse or a spline the 3D POLYLINE of 128 segments,
    /// a ray or an infinite line a LINE to the extents of the drawing, an MTEXT, a pattern HATCH, a LEADER, a MULTILEADER, an
    /// MLINE and a table the lines, texts and polylines they break into, a 3DSOLID and a REGION the lines, circles, arcs, points
    /// and 3D polylines of the edges of their faces; a space gets an INSERT of a block of those pieces where the entity stood, a
    /// block holds them where it stood. A dynamic block is given the anonymous name AutoCAD writes for it. What breaks into
    /// nothing is left out.
    /// </summary>
    /// <remarks>
    /// The pieces are made in a block of the drawing of their own (<c>GHR12_</c> and the handle), so that the text of the
    /// drawing has them as records, which <see cref="DxfVersionConverter"/> places where the entity stood.
    /// </remarks>
    internal static class DxfR12Explosion
    {
        /// <summary>The prefix of the blocks the pieces are made in.</summary>
        internal const string Scratch = "GHR12_";

        // The kinds R12 holds as they are.
        private static readonly HashSet<string> Kept = new HashSet<string>(StringComparer.Ordinal)
        {
            "LINE",
            "POINT",
            "CIRCLE",
            "ARC",
            "TRACE",
            "SOLID",
            "TEXT",
            "SHAPE",
            "INSERT",
            "ATTDEF",
            "3DFACE",
            "POLYLINE",
            "DIMENSION",
            "VIEWPORT",
        };

        // The kinds AutoCAD leaves out of an R12, whatever they could be broken into.
        private static readonly HashSet<string> Dropped = new HashSet<string>(StringComparer.Ordinal)
        {
            "TOLERANCE",
            "WIPEOUT",
            "IMAGE",
            "PDFUNDERLAY",
            "DWFUNDERLAY",
            "DGNUNDERLAY",
            "OLE2FRAME",
        };

        /// <summary>What an entity was broken into: where its pieces are, and what kind of block they go to.</summary>
        internal sealed class Piece
        {
            internal Piece(string type, string block, char letter, bool direct)
            {
                Type = type;
                Block = block;
                Letter = letter;
                Direct = direct;
            }

            /// <summary>The kind of the entity that was broken.</summary>
            internal string Type { get; }

            /// <summary>The name of the block of its pieces.</summary>
            internal string Block { get; }

            /// <summary>The letter of the anonymous block it gets where it stood in a space: <c>X</c> for a hatch of lines, <c>U</c> for the rest.</summary>
            internal char Letter { get; }

            /// <summary>Whether the one entity it became stands in its place with its handle, as an ellipse, a spline, a ray or an infinite line do, with no block.</summary>
            internal bool Direct { get; }
        }

        /// <summary>What was done to the drawing.</summary>
        internal sealed class Plan
        {
            /// <summary>The pieces of each entity broken, by its handle.</summary>
            internal Dictionary<string, Piece> Pieces { get; } =
                new Dictionary<string, Piece>(StringComparer.OrdinalIgnoreCase);

            /// <summary>The kind of each entity left out, by its handle.</summary>
            internal Dictionary<string, string> Left { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>How many layouts beyond the first of paper space hold entities, which R12 has no block for.</summary>
            internal int Layouts { get; set; }

            /// <summary>The anonymous name each dynamic block is given, by its own name in capitals: R12 has no dynamic blocks, and AutoCAD writes them as <c>*U</c> blocks.</summary>
            internal Dictionary<string, string> Renamed { get; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            /// <summary>How many uses of an edge by a face were left out of the lines of a REGION or a 3DSOLID, by kind, as the edge is a spline.</summary>
            internal SortedDictionary<string, int> Skipped { get; } =
                new SortedDictionary<string, int>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Breaks the entities of the drawing's spaces and blocks R12 cannot hold, in place where that is one record
        /// (LWPOLYLINE) and into a block of pieces where it is not.
        /// </summary>
        /// <param name="database">A drawing of its own, which is changed.</param>
        /// <returns>What was broken and what was left out.</returns>
        internal static Plan Prepare(DxfDatabase database)
        {
            var plan = new Plan();
            var extents = new Extents(database);
            NameDynamicBlocks(database, plan);

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                string name = block.Name ?? string.Empty;
                bool space =
                    string.Equals(name, DxfBlockTableRecord.ModelSpace, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, DxfBlockTableRecord.PaperSpace, StringComparison.OrdinalIgnoreCase);

                if (block.IsLayout && !space)
                {
                    if (block.Count > 0)
                    {
                        plan.Layouts++;
                    }

                    continue;
                }

                foreach (DxfEntity entity in block)
                {
                    string type = TypeOf(entity);

                    if (entity.Handle == null || Kept.Contains(type))
                    {
                        continue;
                    }

                    if (type == "LWPOLYLINE")
                    {
                        try
                        {
                            DxfPolylineConvert.Out((DxfPolyline)entity, true);
                        }
                        catch (Exception exception)
                            when (exception is ArgumentException
                                || exception is InvalidOperationException
                                || exception is NotSupportedException
                            )
                        {
                            GeometryHelperLog.Debug(
                                "DxfR12Explosion: LWPOLYLINE " + entity.Handle + " is left out: " + exception.Message
                            );
                            plan.Left[entity.Handle] = type;
                        }

                        continue;
                    }

                    var pieces = new List<DxfEntity>();
                    bool wireBody = false;

                    if (entity is DxfUnknownEntity solid && (type == "REGION" || type == "3DSOLID"))
                    {
                        // The edges of its faces as lines, arcs and 3D polylines, as AutoCAD writes them: none for a solid with
                        // no edges (a sphere) still gives its block, empty.
                        IReadOnlyList<DxfEntity> wires = solid.WireParts(out int skipped);

                        if (wires != null)
                        {
                            wireBody = true;

                            foreach (DxfEntity wire in wires)
                            {
                                Reduce(wire, extents, pieces);
                            }

                            if (skipped > 0)
                            {
                                plan.Skipped[type] =
                                    (plan.Skipped.TryGetValue(type, out int before) ? before : 0) + skipped;
                            }
                        }
                    }
                    else if (!Dropped.Contains(type))
                    {
                        Reduce(entity, extents, pieces);
                    }

                    var made = new List<DxfEntity>();
                    DxfBlockTableRecord scratch = null;

                    if (wireBody && pieces.Count == 0)
                    {
                        scratch = new DxfBlockTableRecord { Name = Scratch + entity.Handle };
                        database.BlockTable.Add(scratch);
                    }

                    foreach (DxfEntity piece in pieces)
                    {
                        if (scratch == null)
                        {
                            scratch = new DxfBlockTableRecord { Name = Scratch + entity.Handle };
                            database.BlockTable.Add(scratch);
                        }

                        try
                        {
                            scratch.AppendEntity(piece);
                            made.Add(piece);
                        }
                        catch (Exception exception)
                            when (exception is ArgumentException
                                || exception is InvalidOperationException
                                || exception is NotSupportedException
                            )
                        {
                            GeometryHelperLog.Debug(
                                "DxfR12Explosion: a piece of "
                                    + type
                                    + " "
                                    + entity.Handle
                                    + " is left out: "
                                    + exception.Message
                            );
                        }
                    }

                    if (made.Count == 0 && !(wireBody && scratch != null))
                    {
                        plan.Left[entity.Handle] = type;
                    }
                    else
                    {
                        char letter = entity is DxfHatch hatch && !hatch.IsSolidFill ? 'X' : 'U';
                        bool direct =
                            made.Count == 1
                            && (type == "ELLIPSE" || type == "SPLINE" || type == "XLINE" || type == "RAY");
                        plan.Pieces[entity.Handle] = new Piece(type, scratch.Name, letter, direct);
                    }
                }
            }

            return plan;
        }

        /// <summary>
        /// Gives each dynamic block an anonymous name, as AutoCAD 2021 does when it saves a drawing as R12, which has no dynamic
        /// blocks: the block table keeps its place, the name is <c>*U</c> and the first number above that of the anonymous block
        /// before it (the first number free where none comes before), so that <c>my-dynamic-block</c> between <c>*T16</c> and
        /// <c>*U18</c> becomes <c>*U17</c>. Its INSERTs name it so too.
        /// </summary>
        private static void NameDynamicBlocks(DxfDatabase database, Plan plan)
        {
            var used = new HashSet<int>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (AnonymousNumber(block.Name, out int number))
                {
                    used.Add(number);
                }
            }

            int before = 0;

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                string name = block.Name ?? string.Empty;

                if (AnonymousNumber(name, out int number))
                {
                    before = number;
                }
                else if (!block.IsLayout && !block.IsAnonymous && block.IsDynamicBlock)
                {
                    int free = before + 1;

                    while (!used.Add(free))
                    {
                        free++;
                    }

                    before = free;
                    plan.Renamed[name] = "*U" + free.ToString(CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>Whether a name is that of an anonymous block, <c>*U17</c> or <c>*D3</c>, and its number.</summary>
        private static bool AnonymousNumber(string name, out int number)
        {
            number = 0;
            return name != null
                && name.Length > 2
                && name[0] == '*'
                && char.IsLetter(name[1])
                && int.TryParse(name.Substring(2), NumberStyles.None, CultureInfo.InvariantCulture, out number);
        }

        /// <summary>The name of an entity's record type: LWPOLYLINE, MTEXT, ACAD_TABLE.</summary>
        private static string TypeOf(DxfEntity entity) => entity.Item.Type;

        /// <summary>Takes an entity down to the kinds R12 holds, appending them to the list: what breaks further does so.</summary>
        private static void Reduce(DxfEntity entity, Extents extents, List<DxfEntity> pieces)
        {
            string type = TypeOf(entity);

            if (Kept.Contains(type))
            {
                pieces.Add(entity);
                return;
            }

            if (Dropped.Contains(type))
            {
                return;
            }

            switch (type)
            {
                case "LWPOLYLINE":
                    pieces.Add(DxfPolylineConvert.Out((DxfPolyline)entity, false));
                    return;
                case "ELLIPSE":
                case "SPLINE":
                    pieces.Add(Sampled((DxfCurve)entity, type == "ELLIPSE" ? 128 : 127));
                    return;
                case "XLINE":
                case "RAY":
                    DxfEntity line = Clipped((DxfCurve)entity, extents);

                    if (line != null)
                    {
                        pieces.Add(line);
                    }

                    return;
            }

            IReadOnlyList<DxfEntity> parts;

            try
            {
                if (entity is DxfHatch fill && (fill.IsSolidFill || fill.IsGradient))
                {
                    // A hatch filled solid or with a gradient is SOLIDs, as AutoCAD writes one.
                    parts = fill.FillSolids();
                }
                else if (type == "MESH")
                {
                    // A mesh is the polylines of its edges, each a polyline of two vertices, as AutoCAD writes one.
                    parts = Edges(entity);
                }
                else
                {
                    parts = entity.Explode();
                }
            }
            catch (Exception exception)
                when (exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is ArgumentException
                )
            {
                GeometryHelperLog.Debug("DxfR12Explosion: " + type + " does not break: " + exception.Message);
                return;
            }

            foreach (DxfEntity part in parts)
            {
                Reduce(part, extents, pieces);
            }
        }

        /// <summary>The edges of what an entity breaks into, its 3DFACEs: each edge once, in the order the faces first give it, as a 3D polyline of two vertices.</summary>
        private static IReadOnlyList<DxfEntity> Edges(DxfEntity entity)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var edges = new List<DxfEntity>();

            foreach (DxfEntity part in entity.Explode())
            {
                if (!(part is DxfFace face))
                {
                    continue;
                }

                var corners = new List<GeoPoint3>(4);

                for (short i = 0; i < 4; i++)
                {
                    GeoPoint3 corner = face.GetVertexAt(i);

                    if (corners.Count == 0 || !Same(corners[corners.Count - 1], corner))
                    {
                        corners.Add(corner);
                    }
                }

                if (corners.Count > 1 && Same(corners[0], corners[corners.Count - 1]))
                {
                    corners.RemoveAt(corners.Count - 1);
                }

                for (int i = 0; i < corners.Count && corners.Count > 1; i++)
                {
                    GeoPoint3 a = corners[i];
                    GeoPoint3 b = corners[(i + 1) % corners.Count];
                    string key = Key(a, b);

                    if (seen.Add(key))
                    {
                        var edge = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { a, b }, false);
                        edge.SetPropertiesFrom(entity);
                        edges.Add(edge);
                    }
                }
            }

            return edges.AsReadOnly();
        }

        private static bool Same(GeoPoint3 a, GeoPoint3 b) =>
            Math.Abs(a.X - b.X) < 1E-6 && Math.Abs(a.Y - b.Y) < 1E-6 && Math.Abs(a.Z - b.Z) < 1E-6;

        /// <summary>The two ends of an edge as text, the lesser first, to the millionth, so that an edge two faces share is one.</summary>
        private static string Key(GeoPoint3 a, GeoPoint3 b)
        {
            string first = Round(a);
            string second = Round(b);
            return string.CompareOrdinal(first, second) <= 0 ? first + "|" + second : second + "|" + first;
        }

        private static string Round(GeoPoint3 point) =>
            Math.Round(point.X, 6).ToString("R", CultureInfo.InvariantCulture)
            + ","
            + Math.Round(point.Y, 6).ToString("R", CultureInfo.InvariantCulture)
            + ","
            + Math.Round(point.Z, 6).ToString("R", CultureInfo.InvariantCulture);

        /// <summary>
        /// The 3D polyline AutoCAD writes for an ellipse or a spline: 129 vertices for an ellipse, 128 for a spline, spread
        /// evenly in the parameter from its start to its end.
        /// </summary>
        private static DxfEntity Sampled(DxfCurve curve, int segments)
        {
            double start = curve.StartParam;
            double end = curve.EndParam;
            var points = new List<GeoPoint3>(segments + 1);

            for (int i = 0; i <= segments; i++)
            {
                points.Add(curve.GetPointAtParameter(i == segments ? end : start + (end - start) * i / segments));
            }

            var polyline = new DxfPolyline3d(DxfPoly3dType.SimplePoly, points, false);
            polyline.SetPropertiesFrom(curve);
            return polyline;
        }

        /// <summary>The line of a ray or an infinite line as far as the extents of the drawing; null where they miss it or the drawing has none.</summary>
        private static DxfEntity Clipped(DxfCurve curve, Extents box)
        {
            GeoPoint3 origin;
            GeoVector3 direction;
            bool both;

            if (curve is DxfXline xline)
            {
                origin = xline.BasePoint;
                direction = xline.UnitDir;
                both = true;
            }
            else
            {
                var ray = (DxfRay)curve;
                origin = ray.BasePoint;
                direction = ray.UnitDir;
                both = false;
            }

            if (!box.IsSet)
            {
                return null;
            }

            double near = both ? double.NegativeInfinity : 0.0;
            double far = double.PositiveInfinity;
            double[] o = { origin.X, origin.Y, origin.Z };
            double[] d = { direction.X, direction.Y, direction.Z };

            for (int axis = 0; axis < 3; axis++)
            {
                if (Math.Abs(d[axis]) < 1E-12)
                {
                    if (o[axis] < box.Min[axis] || o[axis] > box.Max[axis])
                    {
                        return null;
                    }

                    continue;
                }

                double a = (box.Min[axis] - o[axis]) / d[axis];
                double b = (box.Max[axis] - o[axis]) / d[axis];
                near = Math.Max(near, Math.Min(a, b));
                far = Math.Min(far, Math.Max(a, b));
            }

            if (double.IsInfinity(near) || double.IsInfinity(far) || far < near)
            {
                return null;
            }

            var line = new DxfLine(
                new GeoPoint3(origin.X + near * d[0], origin.Y + near * d[1], origin.Z + near * d[2]),
                new GeoPoint3(origin.X + far * d[0], origin.Y + far * d[1], origin.Z + far * d[2])
            );
            line.SetPropertiesFrom(curve);
            return line;
        }

        /// <summary>The box of the model space of a drawing, as <c>$EXTMIN</c> and <c>$EXTMAX</c> hold it once worked out anew; worked out when first asked.</summary>
        private sealed class Extents
        {
            private readonly DxfDatabase _database;
            private bool _loaded;
            private double[] _min;
            private double[] _max;

            internal Extents(DxfDatabase database)
            {
                _database = database;
            }

            internal double[] Min
            {
                get
                {
                    Load();
                    return _min;
                }
            }

            internal double[] Max
            {
                get
                {
                    Load();
                    return _max;
                }
            }

            internal bool IsSet => Min != null && Max != null;

            private void Load()
            {
                if (_loaded)
                {
                    return;
                }

                _loaded = true;

                try
                {
                    _database.UpdateExt(true);
                }
                catch (Exception exception)
                    when (exception is InvalidOperationException || exception is NotSupportedException)
                {
                    GeometryHelperLog.Debug(
                        "DxfR12Explosion: the extents could not be worked out: " + exception.Message
                    );
                }

                double[] least = Point(_database.File.HeaderOf("$EXTMIN"));
                double[] most = Point(_database.File.HeaderOf("$EXTMAX"));

                if (
                    least != null
                    && most != null
                    && Enumerable
                        .Range(0, 3)
                        .All(i => Math.Abs(least[i]) < 1E10 && Math.Abs(most[i]) < 1E10 && least[i] <= most[i])
                )
                {
                    _min = least;
                    _max = most;
                }
            }
        }

        private static double[] Point(IReadOnlyList<DxfTypedValue> groups)
        {
            if (groups == null)
            {
                return null;
            }

            var point = new double[3];
            int found = 0;

            foreach (DxfTypedValue group in groups)
            {
                int axis = group.TypeCode / 10 - 1;

                if (
                    axis >= 0
                    && axis < 3
                    && group.TypeCode % 10 == 0
                    && double.TryParse(group.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                )
                {
                    point[axis] = value;
                    found++;
                }
            }

            return found >= 2 ? point : null;
        }
    }
}
