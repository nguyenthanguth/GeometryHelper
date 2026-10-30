using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// An entity of a type the library has no class for, such as an IMAGE, a WIPEOUT or an ACAD_PROXY_ENTITY: kept as the
    /// file has it, its groups read as they are, its look editable and the entity erasable.
    /// </summary>
    /// <remarks>
    /// Its <see cref="DxfEntity.Layer"/>, colour, linetype, lineweight and visibility are set as any entity's are, every other
    /// group of its record kept as it was; its geometry is not changed, so <see cref="DxfEntity.TransformBy(GeoTransform3)"/> is refused.
    /// It is copied as it is by <see cref="DxfEntity.Clone"/>, and by <see cref="DxfEntity.GetTransformedCopy"/> with the
    /// identity. <see cref="DxfEntity.GetGeometry"/> gives what <see cref="DxfReader"/> reads of it, nothing for a type it
    /// does not read. An MLINE is exploded into its lines and caps, and a MESH into the 3DFACEs of its faces (<see cref="Explode"/>).
    /// </remarks>
    public sealed class DxfUnknownEntity : DxfEntity
    {
        internal DxfUnknownEntity(DxfItem item)
            : base(item)
        {
        }

        /// <summary>
        /// Gets the type of the entity as the file names it in its group 0, as AutoCAD's class names its <c>DxfName</c>:
        /// <c>IMAGE</c>, <c>WIPEOUT</c>, <c>ACAD_PROXY_ENTITY</c>.
        /// </summary>
        public string DxfName => Item.Type;

        /// <summary>
        /// Gets every group of the entity's record, in the order of the file, after the group 0 that names its type, each value
        /// the text the file prints; a new list on each call. Of a POLYLINE they run on through its vertices and of an INSERT
        /// through its attributes, each opened by its own group 0. A comment (999) is not among them, though the file keeps it.
        /// </summary>
        public IReadOnlyList<DxfTypedValue> Groups => Item.Groups;

        /// <summary>
        /// Gets the entities an MLINE breaks into, as AutoCAD's <c>Explode</c>, as ezdxf's <c>virtual_entities</c> gives them, from
        /// the geometry its record holds and the MLINESTYLE it names: for an open one its start caps, then a LINE for each element
        /// along each segment, the miter lines at each joint where its style draws them, then its end caps. A cap across the
        /// elements is two LINEs, from the outer elements to the point halfway between them; an outer cap a half circle from
        /// the outer element on the left round to the one on the right, two quarter ARCs where their colours differ; inner arcs
        /// join each pair of inner elements, the second and the last but one, the third and the last but two, as AutoCAD's
        /// MLSTYLE draws them. A LINE has the element's colour and linetype, an ARC its colour, each the MLINE's look besides,
        /// its true colour left off. An element broken along a segment (MLEDIT) gives a LINE for each piece left, and caps
        /// the MLINE leaves off (flags 4 and 8 of its group 71) are not drawn; a line of no length is left out, and the fill
        /// of a filled style too. The MLINE of the style Standard, two elements 0.5 either side, through four vertices gives
        /// six LINEs. A MESH not smoothed (level 0) breaks into a 3DFACE for each face, in the order of its list of faces, each
        /// with the MESH's look and every edge drawn, as AutoCAD's EXPLODE breaks one and ezdxf's <c>MeshBuilder</c> renders it:
        /// a face of four corners a 3DFACE of them, of three one ending on its third; a cube 10 by 10 by 10 of eight vertices
        /// and six faces gives six 3DFACEs. A face of more than four corners breaks into a strip of triangles, as AutoCAD
        /// 2021's <c>Explode</c> breaks one: from two corners on, the rest taken by turns from the far end and the near one, each
        /// triangle after the first the other way round; from the face's first two corners in a MESH of that one face, from its
        /// last two back the other way in a MESH of more. The face of the corners 0 to 4 alone gives the triangles (0, 1, 4),
        /// (2, 4, 1) and (4, 2, 3), and as the base of a pyramid (4, 3, 0), (2, 0, 3) and (0, 2, 1); of 0 to 5, (0, 1, 5),
        /// (2, 5, 1), (5, 2, 4) and (3, 4, 2) alone and (5, 4, 0), (3, 0, 4), (0, 3, 1) and (2, 1, 3) as a base. A face given a colour of its own (an
        /// override of the MESH, its subentity 1 the first face) keeps it on its 3DFACEs, as AutoCAD keeps it. A MESH smoothed,
        /// and one that gives a face a material or a transparency of its own, or an edge or a vertex anything of its own, is
        /// refused. Any other type refuses: a REGION, a 3DSOLID, a BODY and a surface, which AutoCAD breaks with its solid
        /// modeler, and every type AutoCAD does not break.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for an MLINE whose drawing has no MLINESTYLE of its handle or name,
        /// or whose vertices hold another count of elements than its style; and for a MESH missing the count of its vertices,
        /// faces, edges or creases, or holding fewer than it counts, as ezdxf refuses one, or with a face of fewer than three
        /// corners, past the end of its list of faces or naming a vertex it has not.</exception>
        /// <exception cref="NotSupportedException">Thrown for a MESH smoothed, or with overrides other than the colours of its
        /// faces, and for any other type.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            switch (DxfName)
            {
                case "MLINE":
                    return MlineParts().AsReadOnly();
                case "REGION":
                case "3DSOLID":
                case "BODY":
                case "SURFACE":
                case "PLANESURFACE":
                case "EXTRUDEDSURFACE":
                case "LOFTEDSURFACE":
                case "REVOLVEDSURFACE":
                case "SWEPTSURFACE":
                case "NURBSURFACE":
                    throw new NotSupportedException("A " + DxfName + " is broken by AutoCAD's solid modeler (ACIS), which the library has not: explode it in AutoCAD.");
                case "MESH":
                    return MeshParts().AsReadOnly();
                default:
                    return base.Explode();
            }
        }

        /// <summary>
        /// The faces of a MESH, as <see cref="Explode"/> sets them out, read in one pass over its record (AcDbSubDMesh) by its
        /// counts, as AutoCAD writes them: its level of smoothing (91), its vertices (92 and a point 10 for each), its faces (93,
        /// the count of the groups 90 after it, each face the count of its corners and their indices), its edges (94 and two
        /// indices each) and creases (95 and a value each), then the count of its overrides (90), each a subentity (91), the
        /// count of its properties (92), and each property its kind (90) and value: a colour (0) its 63, and its 420 where it
        /// is true.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown where a count the record should hold is missing, below nought or
        /// more than the groups after it, as ezdxf refuses such a MESH.</exception>
        private List<DxfEntity> MeshParts()
        {
            IReadOnlyList<DxfTypedValue> groups = GetOwnGroups(0);
            int n = groups.Count;
            int i = 0;
            int level = 0;
            string handle = Handle ?? string.Empty;

            while (i < n && !(groups[i].TypeCode == 100 && string.Equals(groups[i].Value?.Trim(), "AcDbSubDMesh", StringComparison.Ordinal)))
            {
                i++;
            }

            for (i++; i < n && groups[i].TypeCode != 92; i++)
            {
                level = groups[i].TypeCode == 91 ? DxfLeaderGroups.Whole(groups[i].Value, 0) : level;
            }

            if (level != 0)
            {
                throw new NotSupportedException("The MESH " + handle + " is smoothed to level " + level.ToString(CultureInfo.InvariantCulture) + ", and breaks into the faces its smoothing makes, which the library does not work out: explode it in AutoCAD, or take its smoothing off first.");
            }

            // A count of the record, and the groups it counts after it, checked to be there.
            int Count(int code, string what, int each)
            {
                int count = i < n && groups[i].TypeCode == code ? DxfLeaderGroups.Whole(groups[i].Value, -1) : -1;

                if (count < 0 || (long)count * each > n - i - 1)
                {
                    throw new InvalidOperationException("The MESH " + handle + (count < 0 ? " holds no count of its " + what + " (" + code.ToString(CultureInfo.InvariantCulture) + ") where its record should" : " counts more " + what + " than its record holds") + ", as ezdxf refuses one.");
                }

                i++;
                return count;
            }

            void Expect(int code, string what)
            {
                if (i >= n || groups[i].TypeCode != code)
                {
                    throw new InvalidOperationException("The MESH " + handle + " holds fewer " + what + " than it counts, as ezdxf refuses one.");
                }
            }

            int vertices = Count(92, "vertices", 1);
            var points = new List<GeoPoint3>(vertices);

            for (int k = 0; k < vertices; k++)
            {
                Expect(10, "vertices");
                double[] point = { DxfLeaderGroups.Num(groups[i].Value, 0.0), 0.0, 0.0 };

                for (i++; i < n && (groups[i].TypeCode == 20 || groups[i].TypeCode == 30); i++)
                {
                    point[groups[i].TypeCode / 10 - 1] = DxfLeaderGroups.Num(groups[i].Value, 0.0);
                }

                points.Add(new GeoPoint3(point[0], point[1], point[2]));
            }

            int size = Count(93, "groups of its list of faces", 1);
            var list = new List<int>(size);

            for (int k = 0; k < size; k++, i++)
            {
                Expect(90, "groups of its list of faces");
                list.Add(DxfLeaderGroups.Whole(groups[i].Value, 0));
            }

            // The edges, two indices each, and their creases, which a face of level 0 does not need.
            for (int k = 2 * Count(94, "edges", 2); k > 0; k--, i++)
            {
                Expect(90, "edges");
            }

            for (int k = Count(95, "creases", 1); k > 0; k--, i++)
            {
                Expect(140, "creases");
            }

            int overrides = i < n && groups[i].TypeCode == 90 ? DxfLeaderGroups.Whole(groups[i].Value, 0) : 0;
            i++;

            // The colours of the faces of their own, by the subentity: 1 the first face, as AutoCAD 2021 writes one.
            var colours = new Dictionary<int, int[]>();

            for (int o = 0; o < overrides; o++)
            {
                int subentity = i < n && groups[i].TypeCode == 91 ? DxfLeaderGroups.Whole(groups[i].Value, 0) : 0;
                int properties = i + 1 < n && groups[i + 1].TypeCode == 92 ? DxfLeaderGroups.Whole(groups[i + 1].Value, 0) : 0;
                i += 2;
                int index = -1;
                int truth = -1;

                for (int q = 0; q < properties; q++)
                {
                    bool colour = i + 1 < n && groups[i].TypeCode == 90 && DxfLeaderGroups.Whole(groups[i].Value, -1) == 0 && groups[i + 1].TypeCode == 63;

                    if (!colour || subentity < 1)
                    {
                        throw new NotSupportedException("The MESH " + handle + " gives " + overrides.ToString(CultureInfo.InvariantCulture) + " of its faces, edges or vertices a material, a transparency or a look of their own other than a face's colour, which AutoCAD keeps on the faces it breaks into and the library does not read: explode it in AutoCAD.");
                    }

                    index = DxfLeaderGroups.Whole(groups[i + 1].Value, 256);
                    i += 2;

                    if (i < n && groups[i].TypeCode == 420)
                    {
                        truth = DxfLeaderGroups.Whole(groups[i].Value, -1);
                        i++;
                    }
                }

                colours[subentity - 1] = new[] { index, truth };
            }

            var faces = new List<int[]>();

            for (int at = 0; at < list.Count; at += list[at] + 1)
            {
                int count = list[at];
                string face = "The face " + faces.Count.ToString(CultureInfo.InvariantCulture) + " of the MESH " + handle + " has " + count.ToString(CultureInfo.InvariantCulture) + " corners";

                if (count < 3 || count > list.Count - at - 1)
                {
                    throw new InvalidOperationException(face + (count < 3 ? ", where a face has three at least." : ", and its list of faces ends before them."));
                }

                var corners = new int[count];

                for (int k = 0; k < count; k++)
                {
                    corners[k] = list[at + 1 + k];

                    if (corners[k] < 0 || corners[k] >= points.Count)
                    {
                        throw new InvalidOperationException(face + ", one of them the vertex " + corners[k].ToString(CultureInfo.InvariantCulture) + ", and the MESH has " + points.Count.ToString(CultureInfo.InvariantCulture) + " from nought.");
                    }
                }

                faces.Add(corners);
            }

            int beyond = -1;

            foreach (int face in colours.Keys)
            {
                beyond = Math.Max(beyond, face);
            }

            if (beyond >= faces.Count)
            {
                throw new NotSupportedException("The MESH " + handle + " gives " + overrides.ToString(CultureInfo.InvariantCulture) + " of its faces, edges or vertices a colour of their own, the subentity " + (beyond + 1).ToString(CultureInfo.InvariantCulture) + " among them where it has " + faces.Count.ToString(CultureInfo.InvariantCulture) + " faces: an edge's or a vertex's colour is not read by the library: explode it in AutoCAD.");
            }

            var look = new Look(this);
            var parts = new List<DxfEntity>(faces.Count);

            for (int f = 0; f < faces.Count; f++)
            {
                int[] corners = faces[f];
                var made = new List<DxfFace>();

                if (corners.Length <= 4)
                {
                    made.Add(new DxfFace(points[corners[0]], points[corners[1]], points[corners[2]], points[corners[corners.Length - 1]], true, true, true, true));
                }
                else
                {
                    // A strip of triangles, as AutoCAD 2021's Explode gives one: from two corners on, the rest taken by turns from
                    // the far end and the near one, each triangle after the first the other way round; from the face's first two
                    // corners in a mesh of that one face, from its last two back the other way in a mesh of more. A hexagon alone
                    // gives (0, 1, 5), (2, 5, 1), (5, 2, 4), (3, 4, 2); the base of a pyramid (5, 4, 0), (3, 0, 4), (0, 3, 1), (2, 1, 3).
                    int count = corners.Length;
                    var strip = new List<int>(count);
                    int[] round = new int[count];

                    for (int k = 0; k < count; k++)
                    {
                        round[k] = faces.Count > 1 ? corners[count - 1 - k] : corners[k];
                    }

                    strip.Add(round[0]);
                    strip.Add(round[1]);

                    for (int near = 2, far = count - 1, turn = 0; near <= far; turn++)
                    {
                        strip.Add(turn % 2 == 0 ? round[far--] : round[near++]);
                    }

                    for (int t = 0; t + 2 < strip.Count; t++)
                    {
                        int a = t % 2 == 0 ? strip[t] : strip[t + 2];
                        int b = strip[t + 1];
                        int c = t % 2 == 0 ? strip[t + 2] : strip[t];
                        made.Add(new DxfFace(points[a], points[b], points[c], points[c], true, true, true, true));
                    }
                }

                foreach (DxfFace part in made)
                {
                    look.GiveTo(part);

                    if (colours.TryGetValue(f, out int[] colour))
                    {
                        part.ColorIndex = colour[0];
                        part.TrueColor = colour[1] >= 0 ? colour[1] : (int?)null;
                    }

                    parts.Add(part);
                }
            }

            return parts;
        }

        /// <summary>Refuses: the geometry of a type the library keeps as it is is not changed.</summary>
        /// <param name="transform">The transformation.</param>
        /// <exception cref="NotSupportedException">Thrown always.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
            => throw new NotSupportedException("A " + DxfName + " is kept as the file has it, and is not moved: the library has no class for it yet.");

        /// <summary>The parts of an MLINE, as <see cref="Explode"/> sets them out, its record and its style read once.</summary>
        private List<DxfEntity> MlineParts()
        {
            DxfMline mline = DxfMline.Read(GetOwnGroups(0));
            var parts = new List<DxfEntity>();

            if (mline.Vertices.Count < 2)
            {
                return parts;
            }

            DxfMline.Style style = MlineStyle(mline);
            int count = style.Elements.Count;

            foreach (DxfMline.Vertex vertex in mline.Vertices)
            {
                if (count == 0 || vertex.Parameters.Count != count)
                {
                    throw new InvalidOperationException("The MLINE " + (Handle ?? string.Empty) + " holds " + vertex.Parameters.Count.ToString(CultureInfo.InvariantCulture) + " elements at a vertex, and its style " + mline.StyleName + " " + count.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }

            var look = new Look(this);
            GeoVector3 normal = GetNormal();
            double tolerance = Tolerance.Default.EqualPoint;
            bool closed = (mline.Flags & 2) != 0;
            List<int> ordered = style.Ordered();
            int bottom = ordered[0];
            int top = ordered[ordered.Count - 1];

            // Where each element stands at each vertex, the first vertex again at the end of a closed MLINE.
            var miters = new List<GeoPoint3[]>();

            foreach (DxfMline.Vertex vertex in mline.Vertices)
            {
                var at = new GeoPoint3[count];

                for (int k = 0; k < count; k++)
                {
                    at[k] = vertex.PointOf(k);
                }

                miters.Add(at);
            }

            if (closed)
            {
                miters.Add(miters[0]);
            }

            void Line(GeoPoint3 start, GeoPoint3 end, int element)
            {
                if (start.DistanceTo(end) > tolerance)
                {
                    var line = new DxfLine(start, end);
                    look.GiveTo(line);
                    Colour(line, style.Elements[element].Colour);
                    line.Linetype = style.Elements[element].Linetype;
                    parts.Add(line);
                }
            }

            void Across(GeoPoint3[] at)
            {
                GeoPoint3 upper = at[top];
                GeoPoint3 lower = at[bottom];
                var half = new GeoPoint3(lower.X + (upper.X - lower.X) * 0.5, lower.Y + (upper.Y - lower.Y) * 0.5, lower.Z + (upper.Z - lower.Z) * 0.5);
                Line(upper, half, top);
                Line(lower, half, bottom);
            }

            void Round(GeoPoint3[] at, int from, int to)
            {
                GeoPoint3 start = at[from];
                GeoPoint3 end = at[to];
                var center = new GeoPoint3(start.X + (end.X - start.X) * 0.5, start.Y + (end.Y - start.Y) * 0.5, start.Z + (end.Z - start.Z) * 0.5);
                double radius = start.DistanceTo(end) / 2.0;

                if (!(radius > tolerance))
                {
                    return;
                }

                GeoPoint3 middle = WorldToOcs(normal, center);
                GeoPoint3 first = WorldToOcs(normal, start);
                double angle = Math.Atan2(first.Y - middle.Y, first.X - middle.X);
                int one = style.Elements[from].Colour;
                int two = style.Elements[to].Colour;

                foreach (double[] span in one == two ? new[] { new[] { 0.0, Math.PI } } : new[] { new[] { 0.0, Math.PI / 2.0 }, new[] { Math.PI / 2.0, Math.PI } })
                {
                    var arc = new DxfArc(center, normal, radius, angle + span[0], angle + span[1]);
                    look.GiveTo(arc);
                    Colour(arc, span[0] == 0.0 ? one : two);
                    parts.Add(arc);
                }
            }

            void Inner(GeoPoint3[] at, bool start)
            {
                for (int i = 1, j = ordered.Count - 2; i < j; i++, j--)
                {
                    Round(at, start ? ordered[j] : ordered[i], start ? ordered[i] : ordered[j]);
                }
            }

            if (!closed && (mline.Flags & 4) == 0)
            {
                if ((style.Flags & 16) != 0)
                {
                    Across(miters[0]);
                }

                if ((style.Flags & 64) != 0)
                {
                    Round(miters[0], top, bottom);
                }

                if ((style.Flags & 32) != 0)
                {
                    Inner(miters[0], true);
                }
            }

            for (int i = 0; i + 1 < miters.Count; i++)
            {
                DxfMline.Vertex vertex = mline.Vertices[i];

                for (int k = 0; k < count; k++)
                {
                    Pieces(miters[i][k], miters[i + 1][k], vertex.Parameters[k], k, Line);
                }
            }

            if ((style.Flags & 2) != 0)
            {
                for (int i = closed ? 0 : 1; i < miters.Count - 1; i++)
                {
                    Across(miters[i]);
                }
            }

            if (!closed && (mline.Flags & 8) == 0)
            {
                GeoPoint3[] last = miters[miters.Count - 1];

                if ((style.Flags & 256) != 0)
                {
                    Across(last);
                }

                if ((style.Flags & 1024) != 0)
                {
                    Round(last, bottom, top);
                }

                if ((style.Flags & 512) != 0)
                {
                    Inner(last, false);
                }
            }

            return parts;
        }

        /// <summary>
        /// The pieces of an element along a segment, from where it stands at the vertex the segment starts to where it stands at
        /// the next: the whole of it where its parameters say no more than its offset and a start of nought, else each piece from
        /// a start to the stop after it, the last to the end, measured along the segment from where it stands.
        /// </summary>
        private static void Pieces(GeoPoint3 start, GeoPoint3 end, List<double> parameters, int element, Action<GeoPoint3, GeoPoint3, int> line)
        {
            if (parameters.Count <= 2 && (parameters.Count < 2 || parameters[1] == 0.0))
            {
                line(start, end, element);
                return;
            }

            double length = start.DistanceTo(end);

            if (!(length > 0.0))
            {
                return;
            }

            GeoVector3 along = start.GetVectorTo(end).Multiply(1.0 / length);

            for (int i = 1; i < parameters.Count; i += 2)
            {
                double from = Math.Max(0.0, Math.Min(length, parameters[i]));
                double to = i + 1 < parameters.Count ? Math.Max(from, Math.Min(length, parameters[i + 1])) : length;
                line(from == 0.0 ? start : start.Add(along.Multiply(from)), to == length ? end : start.Add(along.Multiply(to)), element);
            }
        }

        /// <summary>Gives a part of an MLINE the colour of its element, its true colour left off; one outside 0 to 256 leaves the MLINE's.</summary>
        private static void Colour(DxfEntity part, int colour)
        {
            part.TrueColor = null;

            if (colour >= 0 && colour <= 256)
            {
                part.ColorIndex = colour;
            }
        }

        /// <summary>
        /// The MLINESTYLE an MLINE is drawn by: the object of its handle (340) in its drawing, or the one of its drawing for one in
        /// no file; else the entry of its name in the drawing's ACAD_MLINESTYLE dictionary.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when there is none.</exception>
        private DxfMline.Style MlineStyle(DxfMline mline)
        {
            DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;
            DxfDBObject found = null;

            if (database != null && mline.StyleHandle != null && database.TryGetObject(mline.StyleHandle, out DxfDBObject byHandle) && byHandle.Item?.Type == "MLINESTYLE")
            {
                found = byHandle;
            }

            DxfDBDictionary styles = found == null ? database?.MLStyleDictionary : null;

            if (styles != null && styles.Contains(mline.StyleName) && styles.GetAt(mline.StyleName) is DxfDBObject byName && byName.Item?.Type == "MLINESTYLE")
            {
                found = byName;
            }

            if (found == null)
            {
                throw new InvalidOperationException("The MLINE " + (Handle ?? string.Empty) + " is drawn by the style " + mline.StyleName + ", which its drawing has not: it has no elements to break into.");
            }

            return DxfMline.Style.Read(found.Item.File.ObjectGroupsOf(found.Item));
        }
    }
}
