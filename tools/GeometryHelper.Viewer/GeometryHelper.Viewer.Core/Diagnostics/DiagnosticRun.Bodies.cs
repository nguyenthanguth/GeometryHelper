using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    internal sealed partial class DiagnosticRun
    {
        private const string OwnEdges = "viewer: edge use counts, corners within EqualPoint taken as one";

        private int _rebuiltFor = -1;
        private GeoSolid3 _rebuilt;
        private bool _refused;

        private static Vec3 V(GeoPoint3 p) => new Vec3(p.X, p.Y, p.Z);

        private static string Where(Vec3 p) => p.ToString();

        private static Bounds3 BoxOf(Vec3[] points)
        {
            Bounds3 box = default(Bounds3);

            foreach (Vec3 p in points)
            {
                box = box.Include(p);
            }

            return box;
        }

        /// <summary>
        /// Rebuilds the body of an item with the library and asks it to validate, at the tolerance of the scene.
        /// </summary>
        /// <returns>true when the library's reading stands for the viewer's own checks on this unit.</returns>
        private bool LibraryBody(ItemModel m, BodyUnit unit)
        {
            if (_rebuiltFor != m.Index)
            {
                _rebuiltFor = m.Index;
                _rebuilt = null;
                _refused = false;

                try
                {
                    _rebuilt = _scene.Rebuild(m.Item, out string note) as GeoSolid3;

                    if (note != null)
                    {
                        Add(m, DiagnosticChecks.Report, DiagnosticKind.Note, false, note, "Scene.Rebuild", default(Vec3), 0.0, 0.0);
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException || e is FormatException)
                {
                    Add(m, DiagnosticChecks.Report, DiagnosticKind.Note, false, "the library refused the body (" + e.Message + "); the viewer's own checks are made alone", "GeoSolid3 constructor", default(Vec3), 0.0, 0.0);
                    _refused = true;
                }
            }

            GeoSolid3 solid = _rebuilt == null ? null : unit.Opening < 0 ? _rebuilt : unit.Opening < _rebuilt.Openings.Count ? _rebuilt.Openings[unit.Opening] : null;

            if (solid == null)
            {
                return false;
            }

            try
            {
                unit.Solid = solid;
                unit.Validation = solid.Validate(_scene.Tolerance);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException)
            {
                Add(m, DiagnosticChecks.Report, DiagnosticKind.Note, false, unit.Label + ": Validate() refused the body (" + e.Message + "); the viewer's own checks are made alone", "GeoSolid3.Validate", default(Vec3), 0.0, 0.0);
                return false;
            }

            unit.LibrarySlivers = true;
            return true;
        }

        private void BodyChecks(ItemModel m, BodyUnit unit, bool library)
        {
            bool single = unit.Faces.Count < 2 && !unit.IsSolid;

            if (library)
            {
                if (On(DiagnosticChecks.Report))
                {
                    LibraryReport(m, unit);
                }

                if (On(DiagnosticChecks.Edges) || On(DiagnosticChecks.Slivers) || On(DiagnosticChecks.Flatness))
                {
                    LibraryIssues(m, unit);
                }
            }
            else
            {
                if (On(DiagnosticChecks.Edges) && !single && unit.Faces.Count > 0)
                {
                    EdgeUse(m, unit);
                }

                if (On(DiagnosticChecks.Flatness) || (_refused && _rebuiltFor == m.Index))
                {
                    Flatness(m, unit, _refused && _rebuiltFor == m.Index);
                }
            }

            if (On(DiagnosticChecks.Duplicates))
            {
                UnitDuplicates(m, unit);
            }

            if (On(DiagnosticChecks.Crossings))
            {
                FaceLoopCrossings(m, unit);
            }

            if (On(DiagnosticChecks.Normals) && unit.Dimension == 3)
            {
                UnitNormals(m, unit);
            }
        }

        #region The library's reading of a body

        private void LibraryReport(ItemModel m, BodyUnit unit)
        {
            SolidValidation3 v = unit.Validation;
            string closed = unit.Solid.IsClosed(_scene.Tolerance) ? "yes" : "no";
            string net = unit.Solid.TryGetVolume(out double volume, _scene.Tolerance) ? N(volume) : "not given (the body is not closed or not valid)";
            string counts = string.Join(", ", v.Issues.GroupBy(i => i.Kind).OrderBy(g => g.Key).Select(g => g.Key + " " + g.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)));
            string inside = v.Issues.Any(i => i.Kind == SolidIssueKind.InsideOut) ? " (wound inwards)" : v.Issues.Any(i => i.Kind == SolidIssueKind.NoVolume) ? " (no volume)" : string.Empty;

            string text = unit.Label + ": Validate() says " + (v.IsValid ? "valid" : "not valid")
                + "; IsClosed() " + closed
                + "; wound alike " + (v.IsWoundAlike ? "yes" : "no")
                + "; signed volume " + N(v.SignedVolume) + inside
                + "; GetVolume() " + net
                + (counts.Length > 0 ? "; found: " + counts : string.Empty);

            Bounds3 box = BoxOf(unit.Vertices);
            Add(m, DiagnosticChecks.Report, DiagnosticKind.Report, !v.IsValid, text, "GeoSolid3.Validate(), IsClosed(), TryGetVolume()", box.IsEmpty ? default(Vec3) : box.Center, v.SignedVolume, box.IsEmpty ? 0.0 : box.Radius * 1.1);
        }

        private void LibraryIssues(ItemModel m, BodyUnit unit)
        {
            foreach (SolidIssue3 issue in unit.Validation.Issues)
            {
                Vec3 at = V(issue.Location);

                switch (issue.Kind)
                {
                    case SolidIssueKind.OpenEdge:
                    case SolidIssueKind.SameWayEdge:
                    case SolidIssueKind.NonManifoldEdge:
                    {
                        if (!On(DiagnosticChecks.Edges) || !issue.Edge.HasValue)
                        {
                            break;
                        }

                        int uses = issue.Forward + issue.Backward;
                        DiagnosticKind kind = uses == 1 ? DiagnosticKind.OpenEdge
                            : uses >= 3 ? DiagnosticKind.NonManifoldEdge
                            : uses == 2 ? DiagnosticKind.InconsistentWinding
                            : issue.Kind == SolidIssueKind.OpenEdge ? DiagnosticKind.OpenEdge : issue.Kind == SolidIssueKind.SameWayEdge ? DiagnosticKind.InconsistentWinding : DiagnosticKind.NonManifoldEdge;
                        Vec3 a = V(issue.Edge.Value.StartPoint);
                        Vec3 b = V(issue.Edge.Value.EndPoint);
                        string faces = string.Join(", ", issue.Faces.Select(f => Number(f)));
                        string what = kind == DiagnosticKind.OpenEdge ? "is used by one face"
                            : kind == DiagnosticKind.NonManifoldEdge ? "is used by " + Count(uses, "face")
                            : "is run the same way by both faces on it";
                        string text = unit.Label + ": the stretch of edge from " + Where(a) + " to " + Where(b) + ", " + N(issue.Size) + " long, " + what
                            + " (faces " + faces + "; " + issue.Forward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " one way, " + issue.Backward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " the other)";
                        // Edges four or more faces meet on, as many running it each way, leave a body valid: the library notes them, so they are a fact.
                        DiagnosticFinding f = Add(m, DiagnosticChecks.Edges, kind, issue.Kind != SolidIssueKind.NonManifoldEdge, text, "GeoSolid3.Validate() " + issue.Kind, at, issue.Size, issue.Size);
                        f.Drawing.Line(EdgeColor(kind), 4.0, a, b);
                        break;
                    }

                    case SolidIssueKind.SliverFace:
                    {
                        if (On(DiagnosticChecks.Slivers) && issue.Faces.Count == 1)
                        {
                            string text = unit.Label + ": face " + Number(issue.Faces[0]) + " is " + N(issue.Size) + " wide on average (twice its area over its perimeter), no wider than EqualPoint " + N(_tol) + ": the library calls a face a sliver when its area is at most half of EqualPoint times its perimeter";
                            DiagnosticFinding f = Add(m, DiagnosticChecks.Slivers, DiagnosticKind.Sliver, true, text, "GeoSolid3.Validate() SliverFace", at, issue.Size, 0.0);
                            OutlineFace(f.Drawing, unit, issue.Faces[0], DiagnosticColors.Sliver);
                        }

                        break;
                    }

                    case SolidIssueKind.NotFlatFace:
                    {
                        if (On(DiagnosticChecks.Flatness) && issue.Faces.Count == 1)
                        {
                            string text = unit.Label + ": face " + Number(issue.Faces[0]) + " has a corner " + N(issue.Size) + " off the plane through its first corner, more than EqualPlanar " + N(_planar);
                            DiagnosticFinding f = Add(m, DiagnosticChecks.Flatness, DiagnosticKind.NotFlat, true, text, "GeoSolid3.Validate() NotFlatFace", at, issue.Size, 0.0);
                            OutlineFace(f.Drawing, unit, issue.Faces[0], DiagnosticColors.NotFlat);
                        }

                        break;
                    }
                }
            }
        }

        private static string EdgeColor(DiagnosticKind kind)
            => kind == DiagnosticKind.OpenEdge ? DiagnosticColors.Open : kind == DiagnosticKind.NonManifoldEdge ? DiagnosticColors.NonManifold : DiagnosticColors.Winding;

        private static void OutlineFace(DiagnosticOverlay overlay, BodyUnit unit, int face, string color)
        {
            if (face < 0 || face >= unit.Faces.Count)
            {
                return;
            }

            foreach (LoopData loop in unit.Faces[face])
            {
                overlay.Loop(color, 3.0, loop.Points);
            }
        }

        #endregion

        #region The viewer's own reading of faces

        private struct EdgeRecord
        {
            internal int Lo;
            internal int Hi;
            internal int Direction;
            internal int Face;
            internal int Sequence;
            internal int From;
            internal int To;
        }

        /// <summary>
        /// One edge as the faces use it: the corners it joins, how many faces use it and which way they run it.
        /// </summary>
        private sealed class EdgeGroup
        {
            internal DiagnosticKind Kind;
            internal int Uses;
            internal int Forward;
            internal int Backward;
            internal readonly List<int> Faces = new List<int>();
            internal Vec3 A;
            internal Vec3 B;
            internal bool Resolved;
        }

        /// <summary>
        /// Counts how many faces use each edge and which way they run it. Corners within the point tolerance are one corner, so two faces
        /// meet on an edge when their corners agree to the tolerance. In a body an edge used once is also matched, as the library matches
        /// edges, by overlap: where it is covered along its whole length, to within the tolerance, by edges of other faces that run
        /// the other way along the same line, a corner of each stands on it, and that is a T-junction, a fact and not an open edge. A mesh
        /// is read by its indexes alone.
        /// </summary>
        private void EdgeUse(ItemModel m, BodyUnit unit)
        {
            int[] weld = Weld(unit.Vertices);
            var records = new List<EdgeRecord>();
            int sequence = 0;

            for (int f = 0; f < unit.Faces.Count; f++)
            {
                List<LoopData> loops = unit.Faces[f];
                Vec3 faceNormal = loops[0].Dimension == 3 ? LoopMath.Newell(loops[0].Points) : default(Vec3);

                foreach (LoopData loop in loops)
                {
                    // A hole runs the other way round its face; a hole stored the same way as its outline is turned over, so that the
                    // face is on the left of every edge of it, as the library reads them.
                    bool reverse = loop.Ring > 0 && Vec3.Dot(LoopMath.Newell(loop.Points), faceNormal) > 0.0;
                    int n = loop.Ids.Length;

                    for (int k = 0; k < n; k++)
                    {
                        int from = loop.Ids[k];
                        int to = loop.Ids[(k + 1) % n];

                        if (reverse)
                        {
                            int swap = from;
                            from = to;
                            to = swap;
                        }

                        int a = weld[from];
                        int b = weld[to];

                        if (a != b)
                        {
                            records.Add(new EdgeRecord { Lo = Math.Min(a, b), Hi = Math.Max(a, b), Direction = a < b ? 1 : -1, Face = f, Sequence = sequence, From = from, To = to });
                        }

                        sequence++;
                    }
                }
            }

            records.Sort((x, y) =>
            {
                int c = x.Lo.CompareTo(y.Lo);
                c = c != 0 ? c : x.Hi.CompareTo(y.Hi);
                c = c != 0 ? c : x.Face.CompareTo(y.Face);
                return c != 0 ? c : x.Sequence.CompareTo(y.Sequence);
            });

            var groups = new List<EdgeGroup>();

            for (int start = 0; start < records.Count;)
            {
                int end = start + 1;

                while (end < records.Count && records[end].Lo == records[start].Lo && records[end].Hi == records[start].Hi)
                {
                    end++;
                }

                var group = new EdgeGroup { Uses = end - start };

                for (int r = start; r < end; r++)
                {
                    group.Forward += records[r].Direction > 0 ? 1 : 0;

                    if (!group.Faces.Contains(records[r].Face))
                    {
                        group.Faces.Add(records[r].Face);
                    }
                }

                group.Backward = group.Uses - group.Forward;
                group.A = unit.Vertices[records[start].From];
                group.B = unit.Vertices[records[start].To];

                if (group.Uses == 1)
                {
                    group.Kind = DiagnosticKind.OpenEdge;
                }
                else if (group.Uses >= 3)
                {
                    group.Kind = DiagnosticKind.NonManifoldEdge;
                }
                else if (group.Forward != group.Backward)
                {
                    group.Kind = DiagnosticKind.InconsistentWinding;
                }
                else
                {
                    start = end;
                    continue;
                }

                groups.Add(group);
                start = end;
            }

            if (unit.IsSolid)
            {
                TJunctions(m, unit, groups);
            }

            foreach (EdgeGroup group in groups)
            {
                if (group.Resolved)
                {
                    continue;
                }

                DiagnosticKind kind = group.Kind;
                double length = (group.B - group.A).Length;

                // Edges four or more faces meet on, as many running it each way, leave a body valid: a fact.
                bool balanced = group.Uses % 2 == 0 && group.Forward == group.Backward;
                bool fault = kind == DiagnosticKind.OpenEdge ? unit.IsSolid : kind == DiagnosticKind.NonManifoldEdge ? !balanced : true;
                string list = string.Join(", ", group.Faces.Select(f => Number(f)));
                string what = kind == DiagnosticKind.OpenEdge ? (unit.IsSolid ? "is used by one face" : "is used by one face: the outline of the mesh")
                    : kind == DiagnosticKind.NonManifoldEdge ? "is used by " + Count(group.Uses, "face")
                    : "is run the same way by both faces on it";
                string text = unit.Label + ": the edge from " + Where(group.A) + " to " + Where(group.B) + ", " + N(length) + " long, " + what
                    + " (" + (group.Faces.Count == 1 ? "face " : "faces ") + list + "; " + group.Forward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " one way, " + group.Backward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " the other)";
                DiagnosticFinding finding = Add(m, DiagnosticChecks.Edges, kind, fault, text, OwnEdges, (group.A + group.B) * 0.5, length, length);
                finding.Drawing.Line(EdgeColor(kind), 4.0, group.A, group.B);
            }
        }

        /// <summary>
        /// Reads the open edges as the library reads the stretches of an edge. The open edges that lie along one line and overlap are one
        /// group; the line is cut at every end of every edge of the group, and on each piece the edges over it are counted, those that run one
        /// way along the line and those that run the other. A piece with as many each way is met (a T-junction where the line was cut, a
        /// fact); one with an odd number of faces is open, one with an even number and unequal numbers is run the same way by faces
        /// on it. Neighbouring pieces with the same counts and the same faces are one stretch, as the library makes one.
        /// </summary>
        private void TJunctions(ItemModel m, BodyUnit unit, List<EdgeGroup> groups)
        {
            var open = groups.Where(g => g.Kind == DiagnosticKind.OpenEdge).ToList();

            if (open.Count < 2)
            {
                return;
            }

            var lo = new double[3 * open.Count];
            var hi = new double[3 * open.Count];

            for (int i = 0; i < open.Count; i++)
            {
                Vec3 a = open[i].A;
                Vec3 b = open[i].B;
                lo[3 * i] = Math.Min(a.X, b.X);
                lo[3 * i + 1] = Math.Min(a.Y, b.Y);
                lo[3 * i + 2] = Math.Min(a.Z, b.Z);
                hi[3 * i] = Math.Max(a.X, b.X);
                hi[3 * i + 1] = Math.Max(a.Y, b.Y);
                hi[3 * i + 2] = Math.Max(a.Z, b.Z);
            }

            var parent = new int[open.Count];

            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            Sweep.Pairs(
                lo,
                hi,
                open.Count,
                _tol,
                (x, y) =>
                {
                    if (OverlapAlongOneLine(open[x], open[y]) > _tol)
                    {
                        int rx = Find(x);
                        int ry = Find(y);

                        if (rx != ry)
                        {
                            parent[Math.Max(rx, ry)] = Math.Min(rx, ry);
                        }
                    }
                },
                _cancel);

            var members = new List<EdgeGroup>[open.Count];

            for (int i = 0; i < open.Count; i++)
            {
                int root = Find(i);
                (members[root] ?? (members[root] = new List<EdgeGroup>())).Add(open[i]);
            }

            foreach (List<EdgeGroup> line in members)
            {
                if (line != null && line.Count >= 2)
                {
                    ReadLine(m, unit, line);
                }
            }
        }

        /// <summary>
        /// Gets how far two edges overlap along one line, within the tolerance of it; nought where they do not lie along one line. The longer
        /// of the two gives the line.
        /// </summary>
        private double OverlapAlongOneLine(EdgeGroup first, EdgeGroup second)
        {
            EdgeGroup edge = (first.B - first.A).Length >= (second.B - second.A).Length ? first : second;
            EdgeGroup other = ReferenceEquals(edge, first) ? second : first;
            Vec3 along = edge.B - edge.A;
            double length = along.Length;

            if (!(length > 0.0))
            {
                return 0.0;
            }

            Vec3 d = along * (1.0 / length);

            if (Vec3.Cross(other.A - edge.A, d).Length > _tol || Vec3.Cross(other.B - edge.A, d).Length > _tol)
            {
                return 0.0;
            }

            double t1 = Vec3.Dot(other.A - edge.A, d);
            double t2 = Vec3.Dot(other.B - edge.A, d);
            return Math.Min(length, Math.Max(t1, t2)) - Math.Max(0.0, Math.Min(t1, t2));
        }

        private struct LineCount
        {
            internal double From;
            internal double To;
            internal int Forward;
            internal int Backward;
            internal List<int> Faces;
        }

        private void ReadLine(ItemModel m, BodyUnit unit, List<EdgeGroup> line)
        {
            EdgeGroup reference = line[0];

            foreach (EdgeGroup g in line)
            {
                if ((g.B - g.A).Length > (reference.B - reference.A).Length)
                {
                    reference = g;
                }
            }

            Vec3 origin = reference.A;
            Vec3 d = (reference.B - reference.A).Normalized();
            int count = line.Count;
            var start = new double[count];
            var end = new double[count];
            var forward = new bool[count];
            var cuts = new List<double>();

            for (int i = 0; i < count; i++)
            {
                double t0 = Vec3.Dot(line[i].A - origin, d);
                double t1 = Vec3.Dot(line[i].B - origin, d);
                forward[i] = t1 >= t0;
                start[i] = Math.Min(t0, t1);
                end[i] = Math.Max(t0, t1);
                cuts.Add(start[i]);
                cuts.Add(end[i]);
            }

            cuts.Sort();
            var places = new List<double>();

            foreach (double c in cuts)
            {
                if (places.Count == 0 || c > places[places.Count - 1] + _tol)
                {
                    places.Add(c);
                }
            }

            var pieces = new List<LineCount>();
            var unmet = new bool[count];

            for (int p = 0; p + 1 < places.Count; p++)
            {
                var piece = new LineCount { From = places[p], To = places[p + 1], Faces = new List<int>() };

                for (int i = 0; i < count; i++)
                {
                    if (start[i] <= piece.From + _tol && end[i] >= piece.To - _tol)
                    {
                        if (forward[i])
                        {
                            piece.Forward++;
                        }
                        else
                        {
                            piece.Backward++;
                        }

                        foreach (int face in line[i].Faces)
                        {
                            if (!piece.Faces.Contains(face))
                            {
                                piece.Faces.Add(face);
                            }
                        }
                    }
                }

                piece.Faces.Sort();

                if (piece.Forward + piece.Backward == 0)
                {
                    continue;
                }

                // Neighbouring pieces the same faces meet on, the same number of them each way, are one stretch.
                if (pieces.Count > 0)
                {
                    LineCount last = pieces[pieces.Count - 1];

                    if (last.Forward == piece.Forward && last.Backward == piece.Backward && last.Faces.SequenceEqual(piece.Faces) && last.To >= piece.From - _tol)
                    {
                        last.To = piece.To;
                        pieces[pieces.Count - 1] = last;
                        continue;
                    }
                }

                pieces.Add(piece);
            }

            foreach (LineCount piece in pieces)
            {
                if (piece.Forward == piece.Backward)
                {
                    continue;
                }

                int uses = piece.Forward + piece.Backward;
                DiagnosticKind kind = uses == 1 ? DiagnosticKind.OpenEdge : uses % 2 == 1 ? DiagnosticKind.NonManifoldEdge : DiagnosticKind.InconsistentWinding;
                Vec3 a = origin + d * piece.From;
                Vec3 b = origin + d * piece.To;
                double length = piece.To - piece.From;

                for (int i = 0; i < count; i++)
                {
                    // Any edge along an unmet stretch is part of it, whole or in part.
                    if (start[i] < piece.To - _tol && end[i] > piece.From + _tol)
                    {
                        unmet[i] = true;
                    }
                }

                string what = kind == DiagnosticKind.OpenEdge ? "is used by one face"
                    : kind == DiagnosticKind.NonManifoldEdge ? "is used by " + Count(uses, "face")
                    : "is run the same way by the faces on it";
                string text = unit.Label + ": the stretch of edge from " + Where(a) + " to " + Where(b) + ", " + N(length) + " long, " + what
                    + " (" + (piece.Faces.Count == 1 ? "face " : "faces ") + string.Join(", ", piece.Faces.Select(f => Number(f))) + "; "
                    + piece.Forward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " one way, " + piece.Backward.ToString(System.Globalization.CultureInfo.InvariantCulture) + " the other)";
                DiagnosticFinding finding = Add(m, DiagnosticChecks.Edges, kind, true, text, OwnEdges + "; edges along one line counted by stretch, as the library counts", (a + b) * 0.5, length, length);
                finding.Drawing.Line(EdgeColor(kind), 4.0, a, b);
            }

            for (int i = 0; i < count; i++)
            {
                line[i].Resolved = true;

                int inside = places.Count(c => c > start[i] + _tol && c < end[i] - _tol);

                if (unmet[i] || inside == 0)
                {
                    continue;
                }

                double length = end[i] - start[i];
                string text = unit.Label + ": the edge from " + Where(line[i].A) + " to " + Where(line[i].B) + ", " + N(length) + " long, of face " + Number(line[i].Faces[0])
                    + " is met by edges of other faces cut at " + Count(inside, "corner") + " along it, as many running each way over every stretch: a T-junction the library takes as meeting";
                DiagnosticFinding f = Add(m, DiagnosticChecks.Edges, DiagnosticKind.TJunction, false, text, OwnEdges, (line[i].A + line[i].B) * 0.5, length, length);
                f.Drawing.Line(DiagnosticColors.TJunction, 3.0, line[i].A, line[i].B);
            }
        }

        /// <summary>
        /// Numbers the distinct points among some: points that are the same to the bit, or within 64 units in the last place of the largest
        /// coordinate, which is rounding and not a difference, are one point. A cone's apex that a thousand faces share is one point to the
        /// sweeps after it, not half a million pairs.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <param name="uniqueOf">For each point, the number of its distinct point.</param>
        /// <returns>The distinct points, in order of place.</returns>
        private Vec3[] Unique(Vec3[] points, out int[] uniqueOf)
        {
            int n = points.Length;
            var order = new int[n];

            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int c = points[a].X.CompareTo(points[b].X);
                c = c != 0 ? c : points[a].Y.CompareTo(points[b].Y);
                c = c != 0 ? c : points[a].Z.CompareTo(points[b].Z);
                return c != 0 ? c : a.CompareTo(b);
            });

            uniqueOf = new int[n];
            var distinct = new List<Vec3>();
            double magnitude = 0.0;

            for (int k = 0; k < n; k++)
            {
                Vec3 p = points[order[k]];
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(p.X), Math.Max(Math.Abs(p.Y), Math.Abs(p.Z))));

                if (distinct.Count > 0 && distinct[distinct.Count - 1].X == p.X && distinct[distinct.Count - 1].Y == p.Y && distinct[distinct.Count - 1].Z == p.Z)
                {
                    uniqueOf[order[k]] = distinct.Count - 1;
                }
                else
                {
                    distinct.Add(p);
                    uniqueOf[order[k]] = distinct.Count - 1;
                }
            }

            double rounding = 64.0 * 2.220446049250313E-16 * magnitude;

            if (!(rounding > 0.0) || distinct.Count < 2)
            {
                return distinct.ToArray();
            }

            int[] root = Cluster(distinct.ToArray(), rounding);
            var number = new int[distinct.Count];
            var kept = new List<Vec3>();

            for (int i = 0; i < distinct.Count; i++)
            {
                if (root[i] == i)
                {
                    number[i] = kept.Count;
                    kept.Add(distinct[i]);
                }
            }

            for (int i = 0; i < n; i++)
            {
                uniqueOf[i] = number[root[uniqueOf[i]]];
            }

            return kept.ToArray();
        }

        /// <summary>
        /// Gives each point the lowest index of the points joined to it by steps no longer than a distance.
        /// </summary>
        internal int[] Cluster(Vec3[] points, double distance)
        {
            var parent = new int[points.Length];

            for (int i = 0; i < parent.Length; i++)
            {
                parent[i] = i;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            foreach ((int first, int second, double _) in Sweep.ClosePairs(points, distance, _cancel))
            {
                int a = Find(first);
                int b = Find(second);

                if (a != b)
                {
                    // The lowest index is the root, so the representative of a cluster does not depend on the order the pairs came in.
                    parent[Math.Max(a, b)] = Math.Min(a, b);
                }
            }

            var result = new int[points.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Find(i);
            }

            return result;
        }

        /// <summary>
        /// Gives each vertex the number of the corner it is, vertices within the point tolerance being one corner.
        /// </summary>
        private int[] Weld(Vec3[] vertices)
        {
            Vec3[] distinct = Unique(vertices, out int[] uniqueOf);
            int[] cluster = Cluster(distinct, _tol);
            var result = new int[vertices.Length];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = cluster[uniqueOf[i]];
            }

            return result;
        }

        /// <summary>
        /// Looks for a face with a corner further off its plane than the planar tolerance, as the library measures it: the distance from the
        /// plane through the first corner of the outline, with the right-hand normal of the outline, of every corner of the outline and the holes.
        /// A body the library refused is read this way face by face, so that the faces that made it refuse are named.
        /// </summary>
        private void Flatness(ItemModel m, BodyUnit unit, bool refused)
        {
            if (unit.Dimension != 3)
            {
                return;
            }

            string source = (refused ? "viewer, as the library's constructor reads it: " : unit.IsSolid ? "viewer, as Validate() reads it: " : "viewer, as the library's polygon reads it: ") + "distance from the plane through the first corner";

            for (int f = 0; f < unit.Faces.Count; f++)
            {
                if (unit.Faces[f][0].Points.Length < 4 && unit.Faces[f].Count == 1)
                {
                    continue;
                }

                double worst = OffPlane(unit.Faces[f], out Vec3 at);

                if (worst > _planar)
                {
                    string text = (unit.Label == "face" ? string.Empty : unit.Label + ": ") + "face " + Number(f) + " has a corner " + N(worst) + " off the plane through its first corner, more than EqualPlanar " + N(_planar)
                        + (refused ? "; the library refuses a face like this" : string.Empty);
                    DiagnosticFinding finding = Add(m, DiagnosticChecks.Flatness, DiagnosticKind.NotFlat, true, text, source, at, worst, 0.0);
                    OutlineFace(finding.Drawing, unit, f, DiagnosticColors.NotFlat);
                }
            }
        }

        /// <summary>
        /// Looks for corners of a body that are one point to the library and two to the data: within the tolerance of one another, in two
        /// loops, and not the same point to within rounding; and for the vertices of a mesh that are not one vertex though they are within the
        /// tolerance. In a body the library calls valid the library welds them, and the finding is a fact.
        /// </summary>
        private void UnitDuplicates(ItemModel m, BodyUnit unit)
        {
            if (unit.Faces.Count < 2 || unit.Vertices.Length < 2)
            {
                return;
            }

            if (!unit.IsSolid)
            {
                foreach ((int first, int second, double distance) in Sweep.ClosePairs(unit.Vertices, _tol, _cancel))
                {
                    Vec3 a = unit.Vertices[first];
                    Vec3 b = unit.Vertices[second];
                    string text = unit.Label + ": vertices " + Number(first) + " and " + Number(second) + " are " + N(distance) + " apart, within EqualPoint " + N(_tol) + ", and are not joined";
                    DuplicateFinding(m, text, a, b, distance, true);
                }

                return;
            }

            var loopOf = new int[unit.Vertices.Length];
            int loopNumber = 0;

            foreach (List<LoopData> face in unit.Faces)
            {
                foreach (LoopData loop in face)
                {
                    foreach (int id in loop.Ids)
                    {
                        loopOf[id] = loopNumber;
                    }

                    loopNumber++;
                }
            }

            Vec3[] distinct = Unique(unit.Vertices, out int[] uniqueOf);
            var firstLoop = new int[distinct.Length];
            var shared = new bool[distinct.Length];

            for (int i = 0; i < firstLoop.Length; i++)
            {
                firstLoop[i] = -1;
            }

            for (int i = 0; i < uniqueOf.Length; i++)
            {
                int u = uniqueOf[i];

                if (firstLoop[u] < 0)
                {
                    firstLoop[u] = loopOf[i];
                }
                else if (firstLoop[u] != loopOf[i])
                {
                    shared[u] = true;
                }
            }

            bool valid = unit.Validation != null && unit.Validation.IsValid;

            foreach ((int first, int second, double distance) in Sweep.ClosePairs(distinct, _tol, _cancel))
            {
                // Two corners that are in one loop alone are the loop's own, looked at with the loop.
                if (!shared[first] && !shared[second] && firstLoop[first] == firstLoop[second])
                {
                    continue;
                }

                Vec3 a = distinct[first];
                Vec3 b = distinct[second];
                string text = unit.Label + ": the corners " + Where(a) + " and " + Where(b) + " of two faces are " + N(distance) + " apart, within EqualPoint " + N(_tol) + " but not the same point"
                    + (valid ? "; Validate() calls the body valid, so the library welds them" : string.Empty);
                DuplicateFinding(m, text, a, b, distance, !valid);
            }
        }

        private void DuplicateFinding(ItemModel m, string text, Vec3 a, Vec3 b, double distance, bool fault)
        {
            DiagnosticFinding f = Add(m, DiagnosticChecks.Duplicates, DiagnosticKind.DuplicateVertex, fault, text, "viewer: vertices closer than EqualPoint", (a + b) * 0.5, distance, distance);
            f.Drawing.Line(DiagnosticColors.Duplicate, 4.0, a, b);
            f.Drawing.Dots.Add(new DiagnosticDot((a + b) * 0.5, DiagnosticColors.Duplicate, 11.0));
        }

        private void UnitNormals(ItemModel m, BodyUnit unit)
        {
            const int Cap = 5000;
            var drawing = new DiagnosticOverlay();
            int drawn = 0;
            Vec3 first = default(Vec3);

            for (int f = 0; f < unit.Faces.Count && drawn < Cap; f++)
            {
                LoopData outer = unit.Faces[f][0];
                Vec3 vector = LoopMath.Newell(outer.Points);
                double area = vector.Length;

                if (!(area > 0.0))
                {
                    continue;
                }

                Vec3 centre = default(Vec3);

                foreach (Vec3 p in outer.Points)
                {
                    centre = centre + p;
                }

                centre = centre * (1.0 / outer.Points.Length);
                Vec3 normal = vector * (1.0 / area);

                if (drawn == 0)
                {
                    first = centre;
                }

                LoopMath.Arrow(drawing, DiagnosticColors.Direction, 2.0, centre, centre + normal * (0.5 * Math.Sqrt(area)), LoopMath.Perpendicular(normal));
                drawn++;
            }

            if (drawn > 0)
            {
                string text = unit.Label + ": " + Count(drawn, "face normal") + " drawn, each by the right-hand rule round its outline" + (drawn >= Cap ? " (the first " + Cap.ToString(System.Globalization.CultureInfo.InvariantCulture) + " faces)" : string.Empty);
                DiagnosticFinding finding = Add(m, DiagnosticChecks.Normals, DiagnosticKind.Normals, false, text, "viewer: right-hand rule round each outline", first, 0.0, 0.0);
                finding.Drawing.Strokes.AddRange(drawing.Strokes);
            }
        }

        #endregion
    }
}
