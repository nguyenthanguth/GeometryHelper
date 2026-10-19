using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    internal sealed partial class DiagnosticRun
    {
        /// <summary>
        /// The turn, in degrees, past which a vertex is a needle: the loop goes back almost along the edge it came by.
        /// </summary>
        internal const double NeedleTurnDegrees = 179.9;

        private const int MaxWindingArrows = 64;

        private enum CrossingClass
        {
            Crossing = 0,
            Touch = 1,
            Tangent = 2,
        }

        private struct Crossing
        {
            internal Vec3 At;
            internal int FirstLoop;
            internal int First;
            internal int SecondLoop;
            internal int Second;
            internal CrossingClass How;
        }

        private void LoopChecks(ItemModel m, LoopData loop)
        {
            bool meshFace = loop.Unit != null && loop.Unit.Label == "mesh";
            bool alone = loop.Unit == null;
            bool ofFace = loop.Unit != null && loop.Unit.Label == "face";
            List<Crossing> crossings = null;

            if (!meshFace && loop.Points.Length >= 3 && (On(DiagnosticChecks.Crossings) || On(DiagnosticChecks.Slivers)))
            {
                crossings = FindCrossings(m, loop);
                loop.Crossings = crossings.Count;
            }

            if (On(DiagnosticChecks.Crossings) && crossings != null)
            {
                CrossingFindings(m, loop, crossings);
            }

            if (On(DiagnosticChecks.Needles))
            {
                Needles(m, loop);
            }

            if (On(DiagnosticChecks.Duplicates) && !meshFace)
            {
                LoopDuplicates(m, loop);
            }

            if (On(DiagnosticChecks.Slivers) && loop.Closed && loop.Points.Length >= 3 && !(loop.Unit != null && loop.Unit.LibrarySlivers) && (crossings == null || crossings.Count == 0))
            {
                Sliver(m, loop);
            }

            if (On(DiagnosticChecks.Winding) && (alone || ofFace) && loop.Closed && loop.Points.Length >= 3)
            {
                Winding(m, loop);
            }

            if (alone && loop.Dimension == 3 && loop.Closed && loop.Points.Length >= 3)
            {
                if (On(DiagnosticChecks.Flatness) && loop.Points.Length >= 4)
                {
                    LoneFlatness(m, loop);
                }

                if (On(DiagnosticChecks.Normals))
                {
                    LoneNormal(m, loop);
                }
            }
        }

        #region Crossings

        /// <summary>
        /// Finds where the edges of a loop meet one another, or where the edges of the loops of a face meet those of another loop.
        /// </summary>
        private List<Crossing> FindCrossings(ItemModel m, LoopData loop) => FindCrossings(m, new List<LoopData> { loop }, false);

        private struct CurveRef
        {
            internal SceneCurve Curve;
            internal int Loop;
            internal int Edge;
        }

        /// <summary>
        /// Finds where curves meet. Two curves meet where they come within the point tolerance; whether they cross there, touch at an end or
        /// are tangent, coming together without changing side, is said apart. Points within the tolerance of one another, joined one to the next
        /// in any chain, are one place.
        /// </summary>
        /// <param name="m">The item.</param>
        /// <param name="loops">The loops.</param>
        /// <param name="betweenLoopsOnly">false to look at each loop against itself, true to look at each loop against the others.</param>
        private List<Crossing> FindCrossings(ItemModel m, List<LoopData> loops, bool betweenLoopsOnly)
        {
            var found = new List<Crossing>();
            var refs = new List<CurveRef>();
            var pre = new double[loops.Count][];

            for (int l = 0; l < loops.Count; l++)
            {
                SceneCurve[] curves = loops[l].Curves(m.Index);
                pre[l] = new double[curves.Length + 1];

                for (int k = 0; k < curves.Length; k++)
                {
                    pre[l][k + 1] = pre[l][k] + (curves[k] == null ? 0.0 : (curves[k].End - curves[k].Start).Length);

                    if (curves[k] != null)
                    {
                        refs.Add(new CurveRef { Curve = curves[k], Loop = l, Edge = k });
                    }
                }
            }

            if (refs.Count < 2)
            {
                return found;
            }

            var lo = new double[3 * refs.Count];
            var hi = new double[3 * refs.Count];

            for (int s = 0; s < refs.Count; s++)
            {
                LoopMath.CurveBox(refs[s].Curve, out Vec3 min, out Vec3 max);
                lo[3 * s] = min.X;
                lo[3 * s + 1] = min.Y;
                lo[3 * s + 2] = min.Z;
                hi[3 * s] = max.X;
                hi[3 * s + 1] = max.Y;
                hi[3 * s + 2] = max.Z;
            }

            var pairs = new List<(int, int)>();
            // At a tolerance finer than the rounding of the numbers the curves cannot be told to meet, so the tolerance is raised as CurveIntersections raises it.
            double magnitude = 0.0;

            for (int s = 0; s < lo.Length; s++)
            {
                magnitude = Math.Max(magnitude, Math.Max(Math.Abs(lo[s]), Math.Abs(hi[s])));
            }

            double tol = Math.Max(_tol, CurveIntersections.MinimumUlps * 2.220446049250313E-16 * magnitude);
            Sweep.Pairs(lo, hi, refs.Count, tol, (a, b) => pairs.Add((a, b)), _cancel);
            pairs.Sort((x, y) => x.Item1 != y.Item1 ? x.Item1.CompareTo(y.Item1) : x.Item2.CompareTo(y.Item2));

            int tested = 0;
            var normals = new Vec3?[loops.Count];

            foreach ((int a, int b) in pairs)
            {
                if ((++tested & 1023) == 0)
                {
                    _cancel.ThrowIfCancellationRequested();
                }

                CurveRef ra = refs[a];
                CurveRef rb = refs[b];
                bool same = ra.Loop == rb.Loop;

                if (same == betweenLoopsOnly)
                {
                    continue;
                }

                SceneCurve ci = ra.Curve;
                SceneCurve cj = rb.Curve;
                double forward = double.PositiveInfinity;
                double wrapped = double.PositiveInfinity;

                if (same)
                {
                    // Edges joined by edges no longer than the tolerance in all meet where the loop is joined, and that is not a crossing; the
                    // wrap round the end of a closed loop is such a join too.
                    double[] p = pre[ra.Loop];
                    forward = p[rb.Edge] - p[ra.Edge + 1];
                    wrapped = loops[ra.Loop].Closed ? p[p.Length - 1] - (p[rb.Edge + 1] - p[ra.Edge]) : double.PositiveInfinity;
                }

                foreach (Vec3 hit in CurveIntersections.Find(ci, cj, tol))
                {
                    if (forward <= tol && (hit - ci.End).Length <= forward + 2.0 * tol)
                    {
                        continue;
                    }

                    if (wrapped <= tol && (hit - ci.Start).Length <= wrapped + 2.0 * tol)
                    {
                        continue;
                    }

                    CrossingClass how;

                    if (Near(hit, ci.Start, tol) || Near(hit, ci.End, tol) || Near(hit, cj.Start, tol) || Near(hit, cj.End, tol))
                    {
                        how = CrossingClass.Touch;
                    }
                    else
                    {
                        Vec3 normal = (normals[ra.Loop] ?? (normals[ra.Loop] = LoopNormal(loops[ra.Loop]))).Value;
                        how = ChangesSide(ci, cj, hit, normal) ? CrossingClass.Crossing : CrossingClass.Tangent;
                    }

                    found.Add(new Crossing { At = hit, FirstLoop = ra.Loop, First = ra.Edge, SecondLoop = rb.Loop, Second = rb.Edge, How = how });
                }
            }

            // Edges ending at a vertex that lies on a third meet it at one point twice, and a tangent can be found a few times along its
            // length: the points joined within the tolerance, one to the next, are one place, whichever way they sort.
            found.Sort((x, y) =>
            {
                int c = x.At.X.CompareTo(y.At.X);
                c = c != 0 ? c : x.At.Y.CompareTo(y.At.Y);
                c = c != 0 ? c : x.At.Z.CompareTo(y.At.Z);
                c = c != 0 ? c : x.FirstLoop.CompareTo(y.FirstLoop);
                c = c != 0 ? c : x.First.CompareTo(y.First);
                c = c != 0 ? c : x.SecondLoop.CompareTo(y.SecondLoop);
                return c != 0 ? c : x.Second.CompareTo(y.Second);
            });

            if (found.Count < 2)
            {
                return found;
            }

            int[] cluster = Cluster(found.Select(c => c.At).ToArray(), tol);
            var merged = new List<Crossing>();
            var at = new Dictionary<int, int>();

            for (int i = 0; i < found.Count; i++)
            {
                if (cluster[i] == i)
                {
                    at[i] = merged.Count;
                    merged.Add(found[i]);
                }
                else
                {
                    // A place that is crossed anywhere is a crossing; failing that, one that is an end touching, a touch; else a tangent.
                    int k = at[cluster[i]];
                    Crossing kept = merged[k];
                    kept.How = (CrossingClass)Math.Min((int)kept.How, (int)found[i].How);
                    merged[k] = kept;
                }
            }

            return merged;
        }

        private static Vec3 LoopNormal(LoopData loop)
            => loop.Dimension == 2 ? new Vec3(0.0, 0.0, 1.0) : LoopMath.Newell(loop.Points).Normalized();

        private static bool Near(Vec3 a, Vec3 b, double tolerance) => (a - b).Length <= tolerance;

        /// <summary>
        /// Says whether a curve passes from one side of another to the other where they meet, by looking at two points of it either side of the
        /// place, a few tolerances away. Two that are tangent, or that come within the tolerance of each other without crossing, have both on one side.
        /// </summary>
        private bool ChangesSide(SceneCurve first, SceneCurve second, Vec3 hit, Vec3 normal)
        {
            double step = 10.0 * Math.Max(_tol, 1E-9);

            if (!SidePoints(second, hit, step, out Vec3 p, out Vec3 q))
            {
                return true;
            }

            double sp = Side(first, hit, p, normal);
            double sq = Side(first, hit, q, normal);

            if (double.IsNaN(sp) || double.IsNaN(sq))
            {
                return true;
            }

            const double Noise = 1E-9;
            return (sp > Noise && sq < -Noise) || (sp < -Noise && sq > Noise);
        }

        /// <summary>
        /// Gets how far a point is to one side of a curve at a place on it: positive to its left seen along the normal, for a segment, and
        /// outside it for a circle; not a number for an ellipse.
        /// </summary>
        private static double Side(SceneCurve curve, Vec3 hit, Vec3 point, Vec3 normal)
        {
            if (curve.Shape == CurveShape.Segment)
            {
                Vec3 d = (curve.End - curve.Start).Normalized();
                return Vec3.Dot(Vec3.Cross(d, point - hit), normal);
            }

            return curve.IsCircular ? curve.GapFromCircle(point, out _) : double.NaN;
        }

        /// <summary>
        /// Gets two points of a curve either side of a place on it, a step along the curve from it, or half the way to an end where that is nearer.
        /// </summary>
        private static bool SidePoints(SceneCurve curve, Vec3 hit, double step, out Vec3 before, out Vec3 after)
        {
            before = after = hit;

            if (curve.Shape == CurveShape.Segment)
            {
                Vec3 d = curve.End - curve.Start;
                double length = d.Length;
                Vec3 dir = d * (1.0 / length);
                double u = Vec3.Dot(hit - curve.Start, dir);
                double reach = Math.Min(step, 0.5 * Math.Min(u, length - u));

                if (!(reach > 0.0))
                {
                    return false;
                }

                before = hit - dir * reach;
                after = hit + dir * reach;
                return true;
            }

            double radius = Math.Max(curve.RadiusU, curve.RadiusV);
            double t = curve.ParameterOf(hit);
            double sign = curve.Sweep >= 0.0 ? 1.0 : -1.0;
            double da = step / radius;

            if (!curve.IsFull)
            {
                double along = (t - curve.StartAngle) * sign % (2.0 * Math.PI);

                if (along < 0.0)
                {
                    along += 2.0 * Math.PI;
                }

                double extent = Math.Abs(curve.Sweep);

                if (along > extent)
                {
                    return false;
                }

                da = Math.Min(da, 0.5 * Math.Min(along, extent - along));
            }

            if (!(da > 0.0))
            {
                return false;
            }

            before = curve.PointAt(t - sign * da);
            after = curve.PointAt(t + sign * da);
            return true;
        }

        private void CrossingFindings(ItemModel m, LoopData loop, List<Crossing> crossings)
        {
            string library = null;
            bool? simple = null;

            // A loop on its own is asked of the library always; a face of a body, of which there can be thousands, only where the viewer found
            // it crossing itself, so that the two can be compared.
            if (_options.UseLibrary && loop.Closed && loop.Points.Length >= 4 && (loop.Unit == null || crossings.Count > 0))
            {
                simple = LibrarySimple(loop, out library);
            }

            string agree = simple == null ? string.Empty : simple.Value ? "; " + library + " says the loop is simple" : "; " + library + " agrees: not simple";

            foreach (Crossing c in crossings)
            {
                string text = loop.Role + ": edges " + Number(c.First) + " and " + Number(c.Second) + MeetText(c, " cross at ");
                DiagnosticFinding f = Add(m, DiagnosticChecks.Crossings, DiagnosticKind.SelfCrossing, true, text, "viewer: exact curves, CurveIntersections" + agree, c.At, 0.0, 0.0);
                f.Drawing.Dots.Add(new DiagnosticDot(c.At, DiagnosticColors.Crossing, 13.0));
            }

            if (simple == false && crossings.Count == 0)
            {
                Add(m, DiagnosticChecks.Crossings, DiagnosticKind.SelfCrossing, true, loop.Role + ": " + library + " says the loop is not simple, though the exact curves of its edges meet nowhere but where neighbours join", library, loop.Points[0], 0.0, 0.0);
            }
        }

        /// <summary>
        /// Looks at each face with holes for a hole that crosses or touches its outline or another hole.
        /// </summary>
        private void FaceLoopCrossings(ItemModel m, BodyUnit unit)
        {
            for (int f = 0; f < unit.Faces.Count; f++)
            {
                List<LoopData> loops = unit.Faces[f];

                if (loops.Count < 2)
                {
                    continue;
                }

                foreach (Crossing c in FindCrossings(m, loops, true))
                {
                    string text = (unit.Label == "face" ? string.Empty : unit.Label + ": ") + loops[c.FirstLoop].Role + " (edge " + Number(c.First) + ") and " + loops[c.SecondLoop].Role + " (edge " + Number(c.Second) + ")" + MeetText(c, " cross at ");
                    DiagnosticFinding finding = Add(m, DiagnosticChecks.Crossings, DiagnosticKind.SelfCrossing, true, text, "viewer: exact curves, CurveIntersections", c.At, 0.0, 0.0);
                    finding.Drawing.Dots.Add(new DiagnosticDot(c.At, DiagnosticColors.Crossing, 13.0));
                }
            }
        }

        private string MeetText(Crossing c, string cross)
            => c.How == CrossingClass.Crossing ? cross + Where(c.At)
                : c.How == CrossingClass.Touch ? " touch at " + Where(c.At) + " (an end of one lies on the other within EqualPoint " + N(_tol) + ")"
                : " touch tangentially at " + Where(c.At) + " (they come within EqualPoint " + N(_tol) + " of each other without changing side)";

        /// <summary>
        /// Asks the library whether the loop crosses itself, with the loop rebuilt as the kind of polygon it is.
        /// </summary>
        private bool? LibrarySimple(LoopData loop, out string source)
        {
            source = null;

            try
            {
                if (loop.Dimension == 2)
                {
                    if (loop.HasArcs)
                    {
                        source = "GeoPolygonArc2.IsSimple(tolerance)";
                        return new GeoPolygonArc2(loop.Points.Select(p => new GeoPoint2(p.X, p.Y)), loop.Bulges).IsSimple(_scene.Tolerance);
                    }

                    source = "GeoPolygon2.IsSimple(tolerance)";
                    return new GeoPolygon2(loop.Points.Select(p => new GeoPoint2(p.X, p.Y))).IsSimple(_scene.Tolerance);
                }

                if (loop.HasArcs)
                {
                    source = "GeoPolygonArc3.IsSimple(tolerance)";
                    var normals = new GeoVector3[loop.Points.Length];

                    for (int k = 0; loop.Normals != null && k < normals.Length && k < loop.Normals.Length; k++)
                    {
                        normals[k] = new GeoVector3(loop.Normals[k].X, loop.Normals[k].Y, loop.Normals[k].Z);
                    }

                    return new GeoPolygonArc3(loop.Points.Select(p => new GeoPoint3(p.X, p.Y, p.Z)), loop.Bulges, normals).IsSimple(_scene.Tolerance);
                }

                source = "GeoPolygon3.IsSimple(tolerance)";
                return new GeoPolygon3(loop.Points.Select(p => new GeoPoint3(p.X, p.Y, p.Z))).IsSimple(_scene.Tolerance);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException)
            {
                // The library keeps no loop of fewer than three vertices, or one off its plane: it has no reading.
                source = null;
                return null;
            }
        }

        #endregion

        #region Needles, duplicates, slivers

        private int _needleFacts;
        private double _needleNarrowest;
        private Vec3 _needleAt;
        private double _needleTurn;
        private string _needleWhere;

        /// <summary>
        /// Tells in one line the needles of an item that are wider than the tolerance where they end: how many, and the narrowest.
        /// </summary>
        private void NeedleFacts(ItemModel m)
        {
            if (_needleFacts > 0)
            {
                string text = Count(_needleFacts, "vertex") + " of the item turn back by more than " + N(NeedleTurnDegrees) + " degrees but the spike is wider than EqualPoint " + N(_tol)
                    + " where its shorter side ends, a thin shape and no fault; the narrowest is " + N(_needleNarrowest) + " wide at " + _needleWhere;
                DiagnosticFinding f = Add(m, DiagnosticChecks.Needles, DiagnosticKind.Needle, false, text, "viewer: tangents of the exact curves at the vertex", _needleAt, _needleTurn, _needleNarrowest);
                f.Drawing.Dots.Add(new DiagnosticDot(_needleAt, DiagnosticColors.Needle, 12.0));
            }

            _needleFacts = 0;
        }

        private void Needles(ItemModel m, LoopData loop)
        {
            SceneCurve[] curves = loop.Curves(m.Index);
            int n = loop.Points.Length;
            int e = curves.Length;

            if (e < 2)
            {
                return;
            }

            // A loop that is itself a sliver has the same fault at every corner, and is told once as the sliver it is.
            if (On(DiagnosticChecks.Slivers) && loop.Closed && (n == 3 || loop.Crossings == 0) && Math.Abs(LoopMath.SignedArea(loop, out _)) < _tol * LoopMath.LongestChord(loop))
            {
                return;
            }

            for (int k = loop.Closed ? 0 : 1; k < (loop.Closed ? n : n - 1); k++)
            {
                // A tip given twice is one tip, told at the first of the two: the edge of no length between them is stepped over.
                int previousVertex = loop.Closed ? (k - 1 + n) % n : k - 1;

                if (loop.Points[previousVertex].Equals(loop.Points[k]))
                {
                    continue;
                }

                SceneCurve before = null;
                SceneCurve after = null;

                for (int j = 1; j <= e && before == null; j++)
                {
                    int at = k - j;

                    if (at < 0 && !loop.Closed)
                    {
                        break;
                    }

                    before = curves[(at % e + e) % e];
                }

                for (int j = 0; j < e && after == null; j++)
                {
                    int at = k + j;

                    if (at >= e && !loop.Closed)
                    {
                        break;
                    }

                    after = curves[at % e];
                }

                if (before == null || after == null || ReferenceEquals(before, after))
                {
                    continue;
                }

                Vec3 incoming = LoopMath.Tangent(before, true);
                Vec3 outgoing = LoopMath.Tangent(after, false);
                double sine = Vec3.Cross(incoming, outgoing).Length;
                double turn = Math.Atan2(sine, Vec3.Dot(incoming, outgoing)) * 180.0 / Math.PI;

                if (turn <= NeedleTurnDegrees)
                {
                    continue;
                }

                // How wide the spike is where its shorter side ends: a spike a hair thinner than the tolerance is a fault, one a few hundredths
                // wide at that point, though it turns back as far, is a thin shape the library keeps.
                double shorter = Math.Min((before.End - before.Start).Length, (after.End - after.Start).Length);
                double width = shorter * sine;
                bool fault = width <= _tol;

                // A spike wider than that is a thin shape the library keeps, and there can be thousands of them: they are counted, and the narrowest is told.
                if (!fault)
                {
                    if (_needleFacts++ == 0 || width < _needleNarrowest)
                    {
                        _needleNarrowest = width;
                        _needleAt = loop.Points[k];
                        _needleTurn = turn;
                        _needleWhere = loop.Role + ", vertex " + Number(k);
                    }

                    continue;
                }

                string text = loop.Role + ": vertex " + Number(k) + " turns back by " + N(turn) + " degrees, a needle; the shorter edge to it is " + N(shorter) + " long and the spike is "
                    + N(width) + " wide where it ends, " + (fault ? "no wider than" : "wider than") + " EqualPoint " + N(_tol);
                DiagnosticFinding f = Add(m, DiagnosticChecks.Needles, DiagnosticKind.Needle, fault, text, "viewer: tangents of the exact curves at the vertex", loop.Points[k], turn, shorter);
                f.Drawing.Line(DiagnosticColors.Needle, 3.0, before.Start, loop.Points[k], after.End);
                f.Drawing.Dots.Add(new DiagnosticDot(loop.Points[k], DiagnosticColors.Needle, 12.0));
            }
        }

        private void LoopDuplicates(ItemModel m, LoopData loop)
        {
            int n = loop.Points.Length;

            foreach ((int first, int second, double distance) in Sweep.ClosePairs(loop.Points, _tol, _cancel))
            {
                bool neighbours = second == first + 1 || (loop.Closed && first == 0 && second == n - 1);
                Vec3 a = loop.Points[first];
                Vec3 b = loop.Points[second];
                string text = loop.Role + ": vertices " + Number(first) + " and " + Number(second) + (neighbours ? " (neighbours)" : " (not neighbours)") + " are " + N(distance) + " apart, within EqualPoint " + N(_tol);
                DiagnosticFinding f = Add(m, DiagnosticChecks.Duplicates, DiagnosticKind.DuplicateVertex, true, text, "viewer: vertices closer than EqualPoint", (a + b) * 0.5, distance, distance);
                f.Drawing.Line(DiagnosticColors.Duplicate, 4.0, a, b);
                f.Drawing.Dots.Add(new DiagnosticDot((a + b) * 0.5, DiagnosticColors.Duplicate, 11.0));
            }
        }

        private void Sliver(ItemModel m, LoopData loop)
        {
            double area = Math.Abs(LoopMath.SignedArea(loop, out _));
            double longest = LoopMath.LongestChord(loop);

            if (!(area < _tol * longest))
            {
                return;
            }

            double perimeter = LoopMath.Perimeter(loop);
            double width = perimeter > 0.0 ? 2.0 * area / perimeter : 0.0;
            Vec3 centre = default(Vec3);

            foreach (Vec3 p in loop.Points)
            {
                centre = centre + p;
            }

            centre = centre * (1.0 / loop.Points.Length);
            string text = loop.Role + ": area " + N(area) + " against a longest edge of " + N(longest) + ", about " + N(width) + " wide; under EqualPoint " + N(_tol) + " times the longest edge, " + N(_tol * longest);
            DiagnosticFinding f = Add(m, DiagnosticChecks.Slivers, DiagnosticKind.Sliver, true, text, "viewer: area against the longest edge", centre, area, longest * 0.6);
            f.Drawing.Loop(DiagnosticColors.Sliver, 3.5, loop.Points);
        }

        #endregion

        #region Winding, flatness, normals of a loop on its own

        private void Winding(ItemModel m, LoopData loop)
        {
            double area = LoopMath.SignedArea(loop, out Vec3 normal);
            string source = "viewer: signed area, right-hand rule";
            bool? libraryClockwise = null;

            // The polygons of the plane have a reading in the library, which is asked first.
            if (_options.UseLibrary && loop.Dimension == 2 && loop.Ring == 0 && (loop.Kind == "GeoPolygon2" || loop.Kind == "GeoPolygonArc2"))
            {
                try
                {
                    if (loop.Kind == "GeoPolygonArc2")
                    {
                        var p = new GeoPolygonArc2(loop.Points.Select(v => new GeoPoint2(v.X, v.Y)), loop.Bulges);
                        area = p.SignedArea;
                        libraryClockwise = p.IsClockwise;
                        source = "GeoPolygonArc2.SignedArea, IsClockwise";
                    }
                    else
                    {
                        var p = new GeoPolygon2(loop.Points.Select(v => new GeoPoint2(v.X, v.Y)));
                        area = p.SignedArea;
                        libraryClockwise = p.IsClockwise;
                        source = "GeoPolygon2.SignedArea, IsClockwise";
                    }
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    libraryClockwise = null;
                }
            }

            bool clockwise = libraryClockwise ?? area < 0.0;
            bool sameAsOuter = false;

            if (loop.Ring > 0 && loop.Unit != null && loop.Face >= 0 && loop.Face < loop.Unit.Faces.Count)
            {
                LoopData outer = loop.Unit.Faces[loop.Face][0];
                double outerArea = LoopMath.SignedArea(outer, out Vec3 outerNormal);
                sameAsOuter = loop.Dimension == 2 ? (outerArea < 0.0) == clockwise : Vec3.Dot(normal, outerNormal) > 0.0;
            }

            string way = loop.Dimension == 2
                ? (clockwise ? "clockwise" : "counter-clockwise") + " (signed area " + N(area) + ")"
                : "counter-clockwise seen from its normal " + normal + " (area " + N(Math.Abs(area)) + ")";
            // The library turns the hole of a GeoFace3 to run the way its outline does (GeoFace3's constructor flips a hole that runs against it) and
            // reads a hole of a GeoFace2 either way: a hole that runs the way its outline does is a fact to know, never a fault.
            string text = loop.Role + " runs " + way + (sameAsOuter ? "; a hole runs the same way as its outline, which the library stores for a GeoFace3 and reads for a GeoFace2" : string.Empty);
            Vec3 first = loop.Points[0];
            DiagnosticFinding f = Add(m, DiagnosticChecks.Winding, DiagnosticKind.LoopWinding, false, text, source, first, area, 0.0);

            SceneCurve[] curves = loop.Curves(m.Index);
            int stride = Math.Max(1, (curves.Length + MaxWindingArrows - 1) / MaxWindingArrows);
            double perimeter = LoopMath.Perimeter(loop);
            Vec3 up = normal.Length > 0.0 ? normal : new Vec3(0.0, 0.0, 1.0);

            for (int k = 0; k < curves.Length; k += stride)
            {
                SceneCurve c = curves[k];

                if (c == null)
                {
                    continue;
                }

                Vec3 mid = c.Middle ?? (c.Start + c.End) * 0.5;
                Vec3 dir = LoopMath.MiddleTangent(c);
                double half = 0.5 * Math.Min(0.35 * (c.End - c.Start).Length, 0.12 * perimeter);
                LoopMath.Arrow(f.Drawing, DiagnosticColors.Direction, 2.0, mid - dir * half, mid + dir * half, Vec3.Cross(up, dir).Normalized());
            }
        }

        private void LoneFlatness(ItemModel m, LoopData loop)
        {
            double worst = OffPlane(new List<LoopData> { loop }, out Vec3 at);

            if (worst > _planar)
            {
                string text = loop.Role + ": a corner is " + N(worst) + " off the plane through the first corner, more than EqualPlanar " + N(_planar);
                DiagnosticFinding f = Add(m, DiagnosticChecks.Flatness, DiagnosticKind.NotFlat, true, text, "viewer, as the library's polygon reads it: distance from the plane through the first corner", at, worst, 0.0);
                f.Drawing.Loop(DiagnosticColors.NotFlat, 3.0, loop.Points);
            }
        }

        private void LoneNormal(ItemModel m, LoopData loop)
        {
            Vec3 vector = LoopMath.Newell(loop.Points);
            double area = vector.Length;

            if (!(area > 0.0))
            {
                return;
            }

            Vec3 centre = default(Vec3);

            foreach (Vec3 p in loop.Points)
            {
                centre = centre + p;
            }

            centre = centre * (1.0 / loop.Points.Length);
            Vec3 normal = vector * (1.0 / area);
            DiagnosticFinding f = Add(m, DiagnosticChecks.Normals, DiagnosticKind.Normals, false, loop.Role + ": normal " + normal + " by the right-hand rule round the outline", "viewer: right-hand rule round the outline", centre, area, 0.0);
            LoopMath.Arrow(f.Drawing, DiagnosticColors.Direction, 2.0, centre, centre + normal * (0.5 * Math.Sqrt(area)), LoopMath.Perpendicular(normal));
        }

        /// <summary>
        /// Gets how far the farthest corner of a face is from its plane as the library reads it, and which corner it is: the plane through the
        /// first corner of the outline with the right-hand normal of the outline, as <c>GeoPolygon3</c> checks its corners and <c>Validate()</c>
        /// measures a face.
        /// </summary>
        private static double OffPlane(List<LoopData> loops, out Vec3 at)
        {
            LoopData outer = loops[0];
            at = outer.Points[0];
            Vec3 normal = LoopMath.Newell(outer.Points).Normalized();

            if (normal.Length == 0.0)
            {
                return 0.0;
            }

            Vec3 centre = outer.Points[0];
            double worst = 0.0;

            foreach (LoopData loop in loops)
            {
                foreach (Vec3 p in loop.Points)
                {
                    double off = Math.Abs(Vec3.Dot(p - centre, normal));

                    if (off > worst)
                    {
                        worst = off;
                        at = p;
                    }
                }
            }

            return worst;
        }

        #endregion
    }
}
