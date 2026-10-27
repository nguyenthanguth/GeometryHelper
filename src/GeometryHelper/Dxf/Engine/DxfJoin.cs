using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Curves joined into a source curve, as AutoCAD's <c>JoinEntity</c> and <c>JoinEntities</c> join them by the rules of its
    /// JOIN: lines along the line of a LINE and arcs of the circle of an ARC, or of the ellipse of an ELLIPSE, gaps and all;
    /// lines, arcs and polylines of its plane end to end into a polyline of the plane; lines and straight polylines into a 3D
    /// polyline; and lines, arcs, elliptical arcs, polylines and splines into a SPLINE. The source is written anew; the others
    /// are left as they are.
    /// </summary>
    /// <remarks>
    /// Ends meet where they are within the point tolerance. Pieces joined end to end are chained on from the source's end, then
    /// back from its start, each time the one of lowest index of those with an end at the end reached, found through an index
    /// of their ends in cells as wide as the tolerance, so that 10 000 pieces in any order are chained in time along their count.
    /// </remarks>
    internal static class DxfJoin
    {
        private const double Turn = 2.0 * Math.PI;

        /// <summary>
        /// Joins entities into a source, as <see cref="DxfEntity.JoinEntities"/> says: the indices of those joined, in increasing
        /// order. Where <paramref name="single"/>, the one entity given is joined or refused with why.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an entity that is null, erased or the source itself; where single, for
        /// one that is not joined.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the source is erased, or has no ends to join at.</exception>
        /// <exception cref="NotSupportedException">Thrown for a source of a class AutoCAD's JOIN takes no others into.</exception>
        internal static List<int> Join(DxfEntity source, IReadOnlyList<DxfEntity> others, bool single)
        {
            if (source.IsErased)
            {
                throw new InvalidOperationException("The " + source.GetType().Name + " is erased, and nothing is joined to it.");
            }

            string parameter = single ? "secondaryEntity" : "otherEntities";

            // An entity given twice is joined once: the later ones stand as none. No class of entity has an equality of its own.
            var seen = new HashSet<DxfEntity>();
            var taken = new List<DxfEntity>(others.Count);

            for (int i = 0; i < others.Count; i++)
            {
                DxfEntity other = others[i];

                if (other == null)
                {
                    throw new ArgumentException("The entity at " + Number(i) + " is null.", parameter);
                }

                if (ReferenceEquals(other, source))
                {
                    throw new ArgumentException("An entity is not joined to itself.", parameter);
                }

                if (other.IsErased)
                {
                    throw new ArgumentException("The " + other.GetType().Name + " at " + Number(i) + " is erased.", parameter);
                }

                taken.Add(seen.Add(other) ? other : null);
            }

            var work = new Work(taken, single, parameter, Tolerance.Default);

            switch (source)
            {
                case DxfLine line:
                    return Lines(line, work);

                case DxfArc arc:
                    return Turning(Round.Of(arc), work);

                case DxfEllipse ellipse:
                    return Turning(Round.Of(ellipse), work);

                case DxfPolyline polyline:
                    return Flat(polyline, work);

                case DxfPolyline2d polyline:
                    return Flat(polyline, work);

                case DxfPolyline3d polyline:
                    return Space(polyline, work);

                case DxfSpline spline:
                    return Splines(spline, work);

                default:
                    throw new NotSupportedException("A " + source.GetType().Name + " takes no others: AutoCAD's JOIN joins into a line, an arc, an elliptical arc, a polyline or a spline.");
            }
        }

        #region Lines, arcs and ellipses

        /// <summary>
        /// Lines along the line of a LINE, overlapping or with gaps between them: the line run from the lowest of all their ends
        /// along it to the highest, in its own direction, the end of another taken as it is.
        /// </summary>
        private static List<int> Lines(DxfLine line, Work work)
        {
            Tolerance tolerance = work.Tolerance;
            GeoPoint3 start = line.StartPoint;
            GeoPoint3 end = line.EndPoint;
            GeoVector3 delta = start.GetVectorTo(end);
            double length = delta.Length;

            if (!(length > tolerance.EqualPoint))
            {
                throw new InvalidOperationException("A line of no length has no direction for others to run along.");
            }

            GeoVector3 unit = delta.Divide(length);
            double low = 0.0;
            double high = length;
            GeoPoint3 first = start;
            GeoPoint3 last = end;
            var joined = new List<int>();

            for (int i = 0; i < work.Others.Count; i++)
            {
                DxfEntity entity = work.Others[i];

                if (entity == null)
                {
                    continue;
                }

                if (!(entity is DxfLine other))
                {
                    work.Refuse("A LINE takes only lines, as AutoCAD's JOIN does, and was given a " + entity.GetType().Name + ".");
                    continue;
                }

                GeoPoint3 a = other.StartPoint;
                GeoPoint3 b = other.EndPoint;

                if (!Along(start, unit, a, tolerance) || !Along(start, unit, b, tolerance))
                {
                    work.Refuse("The line from " + a + " to " + b + " is not along the line from " + start + " to " + end + " within the point tolerance.");
                    continue;
                }

                foreach (GeoPoint3 p in new[] { a, b })
                {
                    double t = start.GetVectorTo(p).DotProduct(unit);

                    if (t < low)
                    {
                        low = t;
                        first = p;
                    }

                    if (t > high)
                    {
                        high = t;
                        last = p;
                    }
                }

                joined.Add(i);
            }

            if (joined.Count > 0)
            {
                line.SetFromGeCurve(new GeoLine3(first, last));
            }

            return joined;
        }

        /// <summary>Whether a point is within the tolerance of the infinite line through a point along a unit direction.</summary>
        private static bool Along(GeoPoint3 origin, GeoVector3 unit, GeoPoint3 point, Tolerance tolerance)
        {
            GeoVector3 d = origin.GetVectorTo(point);
            return d.Subtract(unit.Multiply(d.DotProduct(unit))).Length <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Arcs of the circle of an ARC, or elliptical arcs of the ellipse of an ELLIPSE, overlapping or with gaps between them,
        /// joined counter-clockwise from the source's start as AutoCAD's JOIN joins them: the source run on to the far end of
        /// each, or back to its start where it reaches over the source's start; refused where they would make the whole curve.
        /// Several are taken in the order of their starts counter-clockwise from the source's end, the lower index first where
        /// two start at one angle, so that the result does not hang on the order they are given in.
        /// </summary>
        private static List<int> Turning(Round source, Work work)
        {
            Tolerance tolerance = work.Tolerance;

            if (!(source.A > tolerance.EqualPoint) || !(source.B > tolerance.EqualPoint))
            {
                throw new InvalidOperationException("The " + source.Entity.GetType().Name + " is of no radius, and has no curve for others to run round.");
            }

            double angular = tolerance.EqualPoint / Math.Max(source.A, source.B);

            if ((source.Entity is DxfEllipse whole && whole.Closed) || !(source.Sweep < Turn - angular))
            {
                throw new InvalidOperationException("A whole " + (source.Entity is DxfArc ? "circle" : "ellipse") + " has no ends, and nothing is joined to it.");
            }

            // Each other in the source's parameters: from the point it starts at seen counter-clockwise about the source's
            // normal, its end where its own normal is the other way, and the angle it sweeps.
            var others = new List<KeyValuePair<int, double[]>>();

            for (int i = 0; i < work.Others.Count; i++)
            {
                DxfEntity entity = work.Others[i];

                if (entity == null)
                {
                    continue;
                }

                if (entity.GetType() != source.Entity.GetType())
                {
                    work.Refuse("A " + source.Entity.GetType().Name + " takes only others of its kind, as AutoCAD's JOIN does, and was given a " + entity.GetType().Name + ".");
                    continue;
                }

                if (entity is DxfEllipse closed && closed.Closed)
                {
                    work.Refuse(Closed(entity));
                    continue;
                }

                Round other = entity is DxfArc arc ? Round.Of(arc) : Round.Of((DxfEllipse)entity);

                if (!source.IsSameCurve(other, tolerance))
                {
                    work.Refuse("The " + entity.GetType().Name + " is not of the same " + (entity is DxfArc ? "circle" : "ellipse") + " within the point tolerance.");
                    continue;
                }

                bool same = other.Normal.DotProduct(source.Normal) > 0.0;
                double from = source.ParameterOf(same ? other.Entity.StartPoint : other.Entity.EndPoint);
                others.Add(new KeyValuePair<int, double[]>(i, new[] { from, Math.Min(other.Sweep, Turn), Wrap(from - source.Start - source.Sweep) }));
            }

            others.Sort((x, y) => x.Value[2] != y.Value[2] ? x.Value[2].CompareTo(y.Value[2]) : x.Key.CompareTo(y.Key));
            double start = source.Start;
            double end = source.Start + source.Sweep;
            bool moved = false;
            var joined = new List<int>();

            foreach (KeyValuePair<int, double[]> one in others)
            {
                DxfEntity entity = work.Others[one.Key];
                double sweep = end - start;
                double at = Wrap(one.Value[0] - start);
                double reach;
                double back = 0.0;

                if (at > Turn - angular)
                {
                    at = 0.0;
                }

                if (at <= sweep + angular)
                {
                    reach = Math.Max(sweep, at + one.Value[1]);
                }
                else if (at + one.Value[1] >= Turn - angular)
                {
                    back = at - Turn;
                    reach = Math.Max(sweep, at + one.Value[1] - Turn);
                }
                else
                {
                    reach = at + one.Value[1];
                }

                if (reach - back >= Turn - angular)
                {
                    work.Refuse("The " + entity.GetType().Name + " would make the source the whole curve, which it is not: AutoCAD's JOIN makes that a " + (entity is DxfArc ? "CIRCLE" : "whole ELLIPSE") + ".");
                    continue;
                }

                if (reach != sweep || back != 0.0)
                {
                    end = start + reach;
                    start += back;
                    moved = true;
                }

                joined.Add(one.Key);
            }

            joined.Sort();

            // Where either end moves both are written, the start within a turn and the end that start and the sweep on, so
            // that parameters the file holds outside a turn come out as one span.
            if (moved)
            {
                double first = Wrap(start);
                double last = first + (end - start);

                if (source.Entity is DxfArc written)
                {
                    written.StartAngle = first;
                    written.EndAngle = last;
                }
                else
                {
                    var ellipse = (DxfEllipse)source.Entity;
                    ellipse.StartParam = first;
                    ellipse.EndParam = last > Turn ? last - Turn : last;
                }
            }

            return joined;
        }

        /// <summary>An angle brought from nought up to a whole turn.</summary>
        private static double Wrap(double angle)
        {
            double wrapped = angle % Turn;

            if (wrapped < 0.0)
            {
                wrapped += Turn;
            }

            return wrapped >= Turn ? wrapped - Turn : wrapped;
        }

        /// <summary>
        /// An arc or an elliptical arc as the joining reads it: its centre, the unit axes of its plane its parameter turns from
        /// and towards, its radii along them, its normal, and the parameter it starts at and the one it sweeps.
        /// </summary>
        private sealed class Round
        {
            private Round(DxfCurve entity, GeoPoint3 center, GeoVector3 u, GeoVector3 v, double a, double b, GeoVector3 normal, double start, double sweep)
            {
                Entity = entity;
                Center = center;
                U = u;
                V = v;
                A = a;
                B = b;
                Normal = normal;
                Start = start;
                Sweep = sweep;
            }

            internal DxfCurve Entity { get; }

            internal GeoPoint3 Center { get; }

            internal GeoVector3 U { get; }

            internal GeoVector3 V { get; }

            internal double A { get; }

            internal double B { get; }

            internal GeoVector3 Normal { get; }

            internal double Start { get; }

            internal double Sweep { get; }

            /// <summary>An arc: the axes of the system of its normal, its angle the parameter.</summary>
            internal static Round Of(DxfArc arc)
            {
                GeoTransform3 ecs = arc.Ecs;
                double radius = arc.Radius;
                return new Round(arc, arc.Center, ecs.Transform(GeoVector3.XAxis), ecs.Transform(GeoVector3.YAxis), radius, radius, arc.Normal, arc.StartAngle, arc.TotalAngle);
            }

            /// <summary>An ellipse: its major and minor axes, its eccentric angle the parameter.</summary>
            internal static Round Of(DxfEllipse ellipse)
            {
                GeoVector3 major = ellipse.MajorAxis;
                GeoVector3 minor = ellipse.MinorAxis;
                double a = major.Length;
                double b = minor.Length;
                double start = ellipse.StartParam;
                return new Round(ellipse, ellipse.Center, a > 0.0 ? major.Divide(a) : major, b > 0.0 ? minor.Divide(b) : minor, a, b, ellipse.Normal, start, ellipse.EndParam - start);
            }

            /// <summary>The parameter of a point of the curve, from minus a half turn up to a half turn.</summary>
            internal double ParameterOf(GeoPoint3 point)
            {
                GeoVector3 d = Center.GetVectorTo(point);
                return Math.Atan2(d.DotProduct(V) / B, d.DotProduct(U) / A);
            }

            /// <summary>
            /// Whether another is of the same circle or ellipse: its centre and radii within the tolerance, its axes in this one's
            /// plane, and, of an ellipse, the end of its major axis on this one's whole ellipse.
            /// </summary>
            internal bool IsSameCurve(Round other, Tolerance tolerance)
            {
                double slack = tolerance.EqualPoint;

                if (!Center.IsEqualTo(other.Center, tolerance) || !(Math.Abs(A - other.A) <= slack) || !(Math.Abs(B - other.B) <= slack))
                {
                    return false;
                }

                if (!(Math.Abs(other.U.Multiply(other.A).DotProduct(Normal)) <= slack) || !(Math.Abs(other.V.Multiply(other.B).DotProduct(Normal)) <= slack))
                {
                    return false;
                }

                if (Entity is DxfEllipse ellipse)
                {
                    GeoPoint3 vertex = Center.Add(other.U.Multiply(other.A));
                    return ellipse.GetClosestPointTo(vertex, true).IsEqualTo(vertex, tolerance);
                }

                return true;
            }
        }

        #endregion

        #region Polylines

        /// <summary>
        /// Lines, arcs and polylines of the plane of a LWPOLYLINE or a POLYLINE of the plane end to end into it: each a run of
        /// vertices in the system of its normal at its elevation, the arc of a piece whose normal is the other way turned over;
        /// the polyline closed where the chain comes back to its start.
        /// </summary>
        private static List<int> Flat(DxfCurve source, Work work)
        {
            Tolerance tolerance = work.Tolerance;
            var light = source as DxfPolyline;
            var plane = source as DxfPolyline2d;
            List<DxfPolyVertex> own;
            List<List<KeyValuePair<int, string>>> groups = null;
            double startWidth;
            double endWidth;
            double elevation;

            if (light != null)
            {
                Open(light.Closed, false, "polyline");
                own = light.Model();
                groups = light.VertexGroups(out startWidth);
                endWidth = startWidth;
                elevation = light.Elevation;
            }
            else
            {
                Open(plane.Closed, plane.PolyType != DxfPoly2dType.SimplePoly, "polyline");
                own = plane.Model(out _);
                startWidth = plane.DefaultStartWidth;
                endWidth = plane.DefaultEndWidth;
                elevation = plane.Elevation;
            }

            Vertices(own.Count);
            var frame = new Frame(source);
            var runs = new List<List<DxfPolyVertex>>();
            var links = new List<Link>();

            for (int i = 0; i < work.Others.Count; i++)
            {
                if (work.Others[i] == null)
                {
                    continue;
                }

                string why = FlatRun(work.Others[i], frame, elevation, tolerance, out List<DxfPolyVertex> run);

                if (why == null)
                {
                    why = Linked(i, work.Others[i], run, tolerance, links);
                }

                if (why != null)
                {
                    work.Refuse(why);
                    continue;
                }

                runs.Add(run);
            }

            Chain chain = Chain.Of(links, Point(own[0]), Point(own[own.Count - 1]), TailOf(own, false), TailOf(own, true), tolerance);
            List<int> joined = work.Joined(links);

            if (chain.Attached == 0)
            {
                return joined;
            }

            var model = new List<DxfPolyVertex>(own.Count + links.Count);
            var from = new List<int>(own.Count + links.Count);

            for (int k = chain.Before.Count - 1; k >= 0; k--)
            {
                Link link = chain.Before[k];
                List<DxfPolyVertex> run = Oriented(runs[link.Position], link.Reversed);

                for (int j = 0; j < run.Count - 1; j++)
                {
                    model.Add(Widths(run[j], startWidth, endWidth));
                    from.Add(-1);
                }
            }

            for (int j = 0; j < own.Count; j++)
            {
                model.Add(own[j]);
                from.Add(j);
            }

            foreach (Link link in chain.After)
            {
                List<DxfPolyVertex> run = Oriented(runs[link.Position], link.Reversed);
                DxfPolyVertex leaving = Widths(run[0], startWidth, endWidth);
                DxfPolyVertex joint = model[model.Count - 1];
                joint.Bulge = leaving.Bulge;
                joint.StartWidth = leaving.StartWidth;
                joint.EndWidth = leaving.EndWidth;
                joint.Widths = leaving.Widths;
                model[model.Count - 1] = joint;

                for (int j = 1; j < run.Count; j++)
                {
                    model.Add(Widths(run[j], startWidth, endWidth));
                    from.Add(-1);
                }
            }

            if (chain.Closed)
            {
                model.RemoveAt(model.Count - 1);
                from.RemoveAt(from.Count - 1);
            }

            if (light != null)
            {
                var was = new List<List<KeyValuePair<int, string>>>(model.Count);

                foreach (int j in from)
                {
                    was.Add(j >= 0 ? groups[j] : new List<KeyValuePair<int, string>>());
                }

                light.WriteModel(model, was, chain.Closed);
            }
            else
            {
                for (int j = 0; j < model.Count; j++)
                {
                    DxfPolyVertex q = model[j];
                    q.Z = elevation;
                    model[j] = q;
                }

                plane.TakeVertices(model);
                plane.Closed = chain.Closed;
            }

            return joined;
        }

        /// <summary>
        /// The run of vertices of a piece in the system of a polyline of the plane, each at the height it has there, so that
        /// its ends are measured in space; null with why where the polyline does not take it: a LINE, an ARC, a LWPOLYLINE or a
        /// POLYLINE of the plane, open, straight or fitted not, and in its plane within the tolerance, the whole of each arc.
        /// </summary>
        private static string FlatRun(DxfEntity entity, Frame frame, double elevation, Tolerance tolerance, out List<DxfPolyVertex> run)
        {
            run = null;
            double slack = tolerance.EqualPoint;

            switch (entity)
            {
                case DxfLine line:
                {
                    GeoPoint3 a = frame.Local(line.StartPoint);
                    GeoPoint3 b = frame.Local(line.EndPoint);

                    if (!(Math.Abs(a.Z - elevation) <= slack) || !(Math.Abs(b.Z - elevation) <= slack))
                    {
                        return OffThePlane(entity);
                    }

                    run = new List<DxfPolyVertex> { Vertex(a.X, a.Y, a.Z, 0.0), Vertex(b.X, b.Y, b.Z, 0.0) };
                    return null;
                }

                case DxfArc arc:
                {
                    double total = arc.TotalAngle;
                    double radius = arc.Radius;
                    GeoTransform3 ecs = arc.Ecs;
                    GeoPoint3 a = frame.Local(arc.StartPoint);
                    GeoPoint3 b = frame.Local(arc.EndPoint);
                    double off = Farthest(frame.Local(arc.Center).Z - elevation, radius * ecs.Transform(GeoVector3.XAxis).DotProduct(frame.N), radius * ecs.Transform(GeoVector3.YAxis).DotProduct(frame.N), arc.StartAngle, total);

                    if (!(off <= slack))
                    {
                        return OffThePlane(entity);
                    }

                    double bulge = Math.Tan(0.25 * total) * (arc.Normal.DotProduct(frame.N) < 0.0 ? -1.0 : 1.0);
                    run = new List<DxfPolyVertex> { Vertex(a.X, a.Y, a.Z, bulge), Vertex(b.X, b.Y, b.Z, 0.0) };
                    return null;
                }

                case DxfPolyline light:
                    if (light.Closed)
                    {
                        return Closed(entity);
                    }

                    run = Placed(light.Model(), new Frame(light), frame, elevation, tolerance);
                    return run == null ? OffThePlane(entity) : null;

                case DxfPolyline2d plane:
                    if (plane.Closed)
                    {
                        return Closed(entity);
                    }

                    if (plane.PolyType != DxfPoly2dType.SimplePoly)
                    {
                        return "A fitted polyline is drawn through vertices the fitting made: straighten it to join it.";
                    }

                    run = Placed(plane.Model(out _), new Frame(plane), frame, elevation, tolerance);
                    return run == null ? OffThePlane(entity) : null;

                default:
                    return "A polyline of the plane takes lines, arcs and polylines of the plane, as AutoCAD's JOIN does, and was given a " + entity.GetType().Name + ".";
            }
        }

        /// <summary>
        /// The vertices of a polyline of the plane taken into the system of another, each at the height it has there, each arc
        /// turned over where the normals are the other way; null where a vertex, or any point of an arc, is off the plane at the
        /// elevation by more than the tolerance. Of the same normal, to the bit, the vertices keep their coordinates.
        /// </summary>
        private static List<DxfPolyVertex> Placed(List<DxfPolyVertex> vertices, Frame own, Frame frame, double elevation, Tolerance tolerance)
        {
            double slack = tolerance.EqualPoint;
            bool same = own.IsSameAs(frame);
            double sign = own.N.DotProduct(frame.N) < 0.0 ? -1.0 : 1.0;
            var run = new List<DxfPolyVertex>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                DxfPolyVertex q = vertices[i];
                GeoPoint3 at = same ? new GeoPoint3(q.X, q.Y, q.Z) : frame.Local(own.World(q.X, q.Y, q.Z));

                if (!(Math.Abs(at.Z - elevation) <= slack))
                {
                    return null;
                }

                // The arc of a plane tilted to this one: the height of its points is that of its centre and a wave of the angle.
                if (!same && i + 1 < vertices.Count && DxfPolylineEdit.IsArc(q, vertices[i + 1]))
                {
                    DxfPolylineEdit.Circle(q, vertices[i + 1], out double cx, out double cy, out double r, out double a0, out double theta);
                    double off = Farthest(frame.Local(own.World(cx, cy, q.Z)).Z - elevation, r * own.U.DotProduct(frame.N), r * own.V.DotProduct(frame.N), theta < 0.0 ? a0 + theta : a0, Math.Abs(theta));

                    if (!(off <= slack))
                    {
                        return null;
                    }
                }

                run.Add(new DxfPolyVertex(at.X, at.Y, at.Z, q.Bulge * sign, q.StartWidth, q.EndWidth, q.Widths));
            }

            return run;
        }

        /// <summary>
        /// Lines and polylines with no arcs end to end into a 3D polyline: each a run of vertices in the world; the polyline
        /// closed where the chain comes back to its start.
        /// </summary>
        private static List<int> Space(DxfPolyline3d source, Work work)
        {
            Tolerance tolerance = work.Tolerance;
            Open(source.Closed, source.PolyType != DxfPoly3dType.SimplePoly, "polyline");
            List<DxfPolyVertex> own = source.Model(out _);
            Vertices(own.Count);
            var runs = new List<List<DxfPolyVertex>>();
            var links = new List<Link>();

            for (int i = 0; i < work.Others.Count; i++)
            {
                if (work.Others[i] == null)
                {
                    continue;
                }

                string why = SpaceRun(work.Others[i], out List<DxfPolyVertex> run);

                if (why == null)
                {
                    why = Linked(i, work.Others[i], run, tolerance, links);
                }

                if (why != null)
                {
                    work.Refuse(why);
                    continue;
                }

                runs.Add(run);
            }

            Chain chain = Chain.Of(links, Point(own[0]), Point(own[own.Count - 1]), TailOf(own, false), TailOf(own, true), tolerance);
            List<int> joined = work.Joined(links);

            if (chain.Attached == 0)
            {
                return joined;
            }

            var model = new List<DxfPolyVertex>(own.Count + links.Count);

            for (int k = chain.Before.Count - 1; k >= 0; k--)
            {
                List<DxfPolyVertex> run = Oriented(runs[chain.Before[k].Position], chain.Before[k].Reversed);
                model.AddRange(run.GetRange(0, run.Count - 1));
            }

            model.AddRange(own);

            foreach (Link link in chain.After)
            {
                List<DxfPolyVertex> run = Oriented(runs[link.Position], link.Reversed);
                model.AddRange(run.GetRange(1, run.Count - 1));
            }

            if (chain.Closed)
            {
                model.RemoveAt(model.Count - 1);
            }

            source.TakeVertices(model);
            source.Closed = chain.Closed;
            return joined;
        }

        /// <summary>
        /// The run of vertices of a piece in the world; null with why where a 3D polyline does not take it: a LINE, a 3D
        /// polyline, or a polyline of the plane with no arc, open and not fitted. A curved piece is refused, as AutoCAD's JOIN
        /// makes a spline of a 3D polyline joined to one, which a POLYLINE does not become in place.
        /// </summary>
        private static string SpaceRun(DxfEntity entity, out List<DxfPolyVertex> run)
        {
            run = null;
            List<DxfPolyVertex> vertices;
            Frame own;

            switch (entity)
            {
                case DxfLine line:
                    run = new List<DxfPolyVertex> { Vertex(line.StartPoint), Vertex(line.EndPoint) };
                    return null;

                case DxfPolyline3d space:
                    if (space.Closed)
                    {
                        return Closed(entity);
                    }

                    if (space.PolyType != DxfPoly3dType.SimplePoly)
                    {
                        return "A fitted polyline is drawn through vertices the fitting made: straighten it to join it.";
                    }

                    run = space.Model(out _);
                    return null;

                case DxfPolyline light:
                    if (light.Closed)
                    {
                        return Closed(entity);
                    }

                    vertices = light.Model();
                    own = new Frame(light);
                    break;

                case DxfPolyline2d plane:
                    if (plane.Closed)
                    {
                        return Closed(entity);
                    }

                    if (plane.PolyType != DxfPoly2dType.SimplePoly)
                    {
                        return "A fitted polyline is drawn through vertices the fitting made: straighten it to join it.";
                    }

                    vertices = plane.Model(out _);
                    own = new Frame(plane);
                    break;

                default:
                    return "A 3D polyline takes lines and polylines with no arcs, and was given a " + entity.GetType().Name + ": AutoCAD's JOIN makes a spline of one joined to a curve, which a POLYLINE does not become in place.";
            }

            run = new List<DxfPolyVertex>(vertices.Count);

            for (int i = 0; i < vertices.Count; i++)
            {
                DxfPolyVertex q = vertices[i];

                if (i + 1 < vertices.Count && DxfPolylineEdit.IsArc(q, vertices[i + 1]))
                {
                    run = null;
                    return "A 3D polyline takes no arc, and the " + entity.GetType().Name + " has one: AutoCAD's JOIN makes a spline of it.";
                }

                run.Add(Vertex(own.World(q.X, q.Y, q.Z)));
            }

            return null;
        }

        /// <summary>
        /// A run of vertices turned the other way, as <see cref="DxfPolyline.ReverseCurve"/> turns a polyline: in the other
        /// order, each segment's bulge turned over and its widths swapped; the run itself where it is not to be turned.
        /// </summary>
        private static List<DxfPolyVertex> Oriented(List<DxfPolyVertex> run, bool reversed)
        {
            if (!reversed)
            {
                return run;
            }

            int n = run.Count;
            var turned = new List<DxfPolyVertex>(n);

            for (int j = 0; j < n; j++)
            {
                DxfPolyVertex at = run[n - 1 - j];

                if (j < n - 1)
                {
                    DxfPolyVertex leaves = run[n - 2 - j];
                    turned.Add(new DxfPolyVertex(at.X, at.Y, at.Z, -leaves.Bulge, leaves.EndWidth, leaves.StartWidth, leaves.Widths));
                }
                else
                {
                    turned.Add(new DxfPolyVertex(at.X, at.Y, at.Z, 0.0, 0.0, 0.0, false));
                }
            }

            return turned;
        }

        /// <summary>A vertex of a piece with its widths written where they are not those a vertex of the polyline with none takes.</summary>
        private static DxfPolyVertex Widths(DxfPolyVertex q, double startWidth, double endWidth)
        {
            q.Widths = q.Widths || q.StartWidth != startWidth || q.EndWidth != endWidth;
            return q;
        }

        private static DxfPolyVertex Vertex(double x, double y, double z, double bulge) => new DxfPolyVertex(x, y, z, bulge, 0.0, 0.0, false);

        private static DxfPolyVertex Vertex(GeoPoint3 p) => new DxfPolyVertex(p.X, p.Y, p.Z, 0.0, 0.0, 0.0, false);

        private static GeoPoint3 Point(DxfPolyVertex q) => new GeoPoint3(q.X, q.Y, q.Z);

        /// <summary>
        /// The link of a run of vertices, put among the links: a point where every vertex is within the tolerance of the first;
        /// why it is refused where it has no vertices, or where its ends meet and it is not a point, as a closed curve.
        /// </summary>
        private static string Linked(int index, DxfEntity entity, List<DxfPolyVertex> run, Tolerance tolerance, List<Link> links)
        {
            if (run.Count == 0)
            {
                return NoVertices(entity);
            }

            GeoPoint3 first = Point(run[0]);
            GeoPoint3 last = Point(run[run.Count - 1]);
            bool point = run.TrueForAll(q => Point(q).IsEqualTo(first, tolerance));

            if (!point && first.IsEqualTo(last, tolerance))
            {
                return "The curve's ends meet: it is closed, and AutoCAD's JOIN joins no closed curve.";
            }

            links.Add(new Link(index, links.Count, first, last, point ? first : MiddleOf(run), point));
            return null;
        }

        /// <summary>
        /// The segment at one end of a source's run of vertices as the chaining sees it: the vertex at its other end and its
        /// middle; none for a run of one vertex.
        /// </summary>
        private static Tail TailOf(List<DxfPolyVertex> own, bool atEnd)
        {
            int n = own.Count;

            if (n < 2)
            {
                return null;
            }

            List<DxfPolyVertex> segment = atEnd ? own.GetRange(n - 2, 2) : own.GetRange(0, 2);
            return new Tail(Point(atEnd ? own[n - 2] : own[1]), MiddleOf(segment));
        }

        /// <summary>
        /// The point half way along a run of vertices, by the lengths of its lines and arcs, the height read straight along
        /// the segment it falls in: the same point whichever way the run is walked.
        /// </summary>
        private static GeoPoint3 MiddleOf(List<DxfPolyVertex> run)
        {
            var lengths = new double[Math.Max(0, run.Count - 1)];
            double total = 0.0;

            for (int i = 0; i + 1 < run.Count; i++)
            {
                DxfPolyVertex a = run[i];
                DxfPolyVertex b = run[i + 1];

                if (DxfPolylineEdit.IsArc(a, b))
                {
                    DxfPolylineEdit.Circle(a, b, out _, out _, out double r, out _, out double theta);
                    lengths[i] = r * Math.Abs(theta);
                }
                else
                {
                    lengths[i] = Point(a).DistanceTo(Point(b));
                }

                total += lengths[i];
            }

            double half = 0.5 * total;

            for (int i = 0; i < lengths.Length; i++)
            {
                if (half > lengths[i] && i + 1 < lengths.Length)
                {
                    half -= lengths[i];
                    continue;
                }

                DxfPolyVertex a = run[i];
                DxfPolyVertex b = run[i + 1];
                double f = lengths[i] > 0.0 ? Math.Min(1.0, half / lengths[i]) : 0.0;
                double z = a.Z + (b.Z - a.Z) * f;

                if (DxfPolylineEdit.IsArc(a, b))
                {
                    DxfPolylineEdit.Circle(a, b, out double cx, out double cy, out double r, out double a0, out double theta);
                    double angle = a0 + theta * f;
                    return new GeoPoint3(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle), z);
                }

                return new GeoPoint3(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, z);
            }

            return Point(run[0]);
        }

        /// <summary>
        /// The farthest from nought of k + a cos t + b sin t over the angles t from a start through a sweep: the height off a
        /// plane of the points of an arc whose centre stands k off it, a and b its radius along its two axes seen square to the
        /// plane; at the ends of the arc, or where the wave peaks within it.
        /// </summary>
        private static double Farthest(double k, double a, double b, double start, double sweep)
        {
            double farthest = Math.Max(Math.Abs(k + a * Math.Cos(start) + b * Math.Sin(start)), Math.Abs(k + a * Math.Cos(start + sweep) + b * Math.Sin(start + sweep)));
            double peak = Math.Atan2(b, a);

            foreach (double t in new[] { peak, peak + Math.PI })
            {
                if (Wrap(t - start) <= sweep)
                {
                    farthest = Math.Max(farthest, Math.Abs(k + a * Math.Cos(t) + b * Math.Sin(t)));
                }
            }

            return farthest;
        }

        #endregion

        #region Splines

        /// <summary>
        /// Curves end to end into a SPLINE, each as the spline that is exactly it (<see cref="DxfCurve.Spline"/>): all raised to
        /// the highest degree among them, each knot vector run on from where the one before it ends, the source's kept, and the
        /// weights of each scaled to meet the one before; the spline closed where the chain comes back to its start, and given
        /// by its control points from then on.
        /// </summary>
        private static List<int> Splines(DxfSpline source, Work work)
        {
            Tolerance tolerance = work.Tolerance;

            if (source.Closed)
            {
                throw new InvalidOperationException("A closed spline has no ends, and nothing is joined to it.");
            }

            source.Clamped(out int degree, out List<double> knots, out List<double[]> points);
            var pieces = new List<Nurbs>();
            var links = new List<Link>();

            for (int i = 0; i < work.Others.Count; i++)
            {
                if (work.Others[i] == null)
                {
                    continue;
                }

                string why = Piece(work.Others[i], out Nurbs piece);

                if (why == null)
                {
                    GeoPoint3 first = piece.First;
                    GeoPoint3 last = piece.Last;
                    bool point = piece.Points.TrueForAll(q => Euclidean(q).IsEqualTo(first, tolerance));

                    if (!point && first.IsEqualTo(last, tolerance))
                    {
                        why = "The curve's ends meet: it is closed, and AutoCAD's JOIN joins no closed curve.";
                    }
                    else
                    {
                        links.Add(new Link(i, links.Count, first, last, point ? first : piece.Middle, point));
                        pieces.Add(piece);
                    }
                }

                if (why != null)
                {
                    work.Refuse(why);
                }
            }

            // A piece runs back over the source where it is the source's whole curve run back.
            var own = new Nurbs(degree, knots, points);
            Chain chain = Chain.Of(links, own.First, own.Last, new Tail(own.Last, own.Middle), new Tail(own.First, own.Middle), tolerance);
            List<int> joined = work.Joined(links);

            if (chain.Attached == 0)
            {
                return joined;
            }

            // The pieces in their order along the joined spline, each turned to run along it, the source among them.
            var order = new List<Nurbs>();

            for (int k = chain.Before.Count - 1; k >= 0; k--)
            {
                order.Add(pieces[chain.Before[k].Position].Turned(chain.Before[k].Reversed));
            }

            int at = order.Count;
            order.Add(own);

            foreach (Link link in chain.After)
            {
                order.Add(pieces[link.Position].Turned(link.Reversed));
            }

            int highest = 1;

            foreach (Nurbs piece in order)
            {
                highest = Math.Max(highest, piece.Degree);
            }

            for (int k = 0; k < order.Count; k++)
            {
                order[k] = order[k].Raised(highest);
            }

            // Out from the source both ways: each knot vector run on from the one it follows, or back to the one it leads into,
            // and its weights scaled to the weight it meets there.
            for (int k = at + 1; k < order.Count; k++)
            {
                order[k].Follow(order[k - 1]);
            }

            for (int k = at - 1; k >= 0; k--)
            {
                order[k].Lead(order[k + 1]);
            }

            var u = new List<double>(order[0].Knots);
            var pw = new List<double[]>(order[0].Points);

            for (int k = 1; k < order.Count; k++)
            {
                Nurbs next = order[k];
                u.RemoveAt(u.Count - 1);

                for (int j = highest + 1; j < next.Knots.Count; j++)
                {
                    u.Add(next.Knots[j]);
                }

                // At a joint on the source's side the source's own point is kept, the piece's dropped.
                if (k <= at)
                {
                    pw.RemoveAt(pw.Count - 1);
                    pw.AddRange(next.Points);
                }
                else
                {
                    pw.AddRange(next.Points.GetRange(1, next.Points.Count - 1));
                }
            }

            if (chain.Closed)
            {
                double[] first = pw[0];
                double w = pw[pw.Count - 1][3];
                pw[pw.Count - 1] = new[] { first[0] / first[3] * w, first[1] / first[3] * w, first[2] / first[3] * w, w };
            }

            bool planar = true;
            List<GeoPoint3> euclidean = pw.ConvertAll(Euclidean);

            if (source.IsPlanar)
            {
                GeoVector3 normal = source.Ecs.Transform(GeoVector3.ZAxis);
                planar = DxfCurveTools.TryPlane(euclidean, normal, out GeoVector3 found) && found.IsParallelTo(normal, tolerance);
            }

            source.TakeJoined(highest, u, pw, chain.Closed, planar, Straight(euclidean, tolerance));
            return joined;
        }

        /// <summary>
        /// The spline of a piece a SPLINE takes, clamped, in homogeneous form; null with why where it takes none: a line, an
        /// arc, an elliptical arc, a polyline or a spline, open, as AutoCAD's JOIN names them; a line is the spline of degree 1
        /// over its length.
        /// </summary>
        private static string Piece(DxfEntity entity, out Nurbs piece)
        {
            piece = null;
            var curve = entity as DxfCurve;
            bool closed;
            int count = 2;

            switch (curve)
            {
                case DxfLine line:
                {
                    GeoPoint3 a = line.StartPoint;
                    GeoPoint3 b = line.EndPoint;
                    double length = a.DistanceTo(b);
                    piece = new Nurbs(1, new List<double> { 0.0, 0.0, length, length }, new List<double[]> { new[] { a.X, a.Y, a.Z, 1.0 }, new[] { b.X, b.Y, b.Z, 1.0 } });
                    return null;
                }

                case DxfPolyline light:
                    closed = light.Closed;
                    count = light.NumberOfVertices;
                    break;

                case DxfPolyline2d plane:
                    closed = plane.Closed;
                    count = plane.Model(out _).Count;
                    break;

                case DxfPolyline3d space:
                    closed = space.Closed;
                    count = space.Model(out _).Count;
                    break;

                case DxfSpline spline:
                    closed = spline.Closed;
                    break;

                case DxfArc _:
                case DxfEllipse _:
                    closed = curve.Closed;
                    break;

                default:
                    return "A SPLINE takes lines, arcs, elliptical arcs, polylines and splines, as AutoCAD's JOIN does, and was given a " + entity.GetType().Name + ".";
            }

            if (closed)
            {
                return Closed(entity);
            }

            // A polyline of one vertex is a point, joined where it touches an end.
            if (count < 2)
            {
                if (count == 0)
                {
                    return NoVertices(entity);
                }

                GeoPoint3 at = curve.StartPoint;
                piece = new Nurbs(1, new List<double> { 0.0, 0.0, 0.0, 0.0 }, new List<double[]> { new[] { at.X, at.Y, at.Z, 1.0 }, new[] { at.X, at.Y, at.Z, 1.0 } });
                return null;
            }

            try
            {
                DxfSpline spline = curve as DxfSpline ?? curve.Spline;
                spline.Clamped(out int degree, out List<double> knots, out List<double[]> points);
                piece = new Nurbs(degree, knots, points);
                return null;
            }
            catch (InvalidOperationException e)
            {
                return e.Message;
            }
            catch (NotSupportedException e)
            {
                return e.Message;
            }
        }

        private static GeoPoint3 Euclidean(double[] q) => new GeoPoint3(q[0] / q[3], q[1] / q[3], q[2] / q[3]);

        /// <summary>A clamped spline in homogeneous form, as <see cref="DxfNurbsEdit"/> works on one.</summary>
        private sealed class Nurbs
        {
            internal Nurbs(int degree, List<double> knots, List<double[]> points)
            {
                Degree = degree;
                Knots = knots;
                Points = points;
            }

            internal int Degree { get; }

            internal List<double> Knots { get; }

            internal List<double[]> Points { get; }

            internal GeoPoint3 First => Euclidean(Points[0]);

            internal GeoPoint3 Last => Euclidean(Points[Points.Count - 1]);

            /// <summary>The point at the middle of its knots, the same point whichever way it runs.</summary>
            internal GeoPoint3 Middle
            {
                get
                {
                    DxfNurbsEdit.Split(Points, out List<GeoPoint3> points, out List<double> weights);
                    var nurbs = new DxfNurbs(Degree, Knots, points, weights);
                    return nurbs.PointAt(0.5 * (nurbs.Start + nurbs.End));
                }
            }

            /// <summary>The spline turned to run the other way where asked, as <see cref="DxfSpline.ReverseCurve"/> turns one: each knot u made a + b - u and the order of both turned.</summary>
            internal Nurbs Turned(bool reversed)
            {
                if (!reversed)
                {
                    return this;
                }

                double a = Knots[0];
                double b = Knots[Knots.Count - 1];
                var knots = new List<double>(Knots.Count);

                for (int i = Knots.Count - 1; i >= 0; i--)
                {
                    knots.Add(a + b - Knots[i]);
                }

                var points = new List<double[]>(Points);
                points.Reverse();
                return new Nurbs(Degree, knots, points);
            }

            /// <summary>The spline raised to a degree, the curve kept exactly; itself where it is of that degree.</summary>
            internal Nurbs Raised(int degree)
            {
                if (degree == Degree)
                {
                    return this;
                }

                DxfNurbsEdit.ElevateDegree(Degree, Knots, Points, degree - Degree, out List<double> knots, out List<double[]> points);
                return new Nurbs(degree, knots, points);
            }

            /// <summary>Runs the knots on from the end of the one before, its clamped start made that knot to the bit, and scales the weights to the one it meets.</summary>
            internal void Follow(Nurbs before)
            {
                double joint = before.Knots[before.Knots.Count - 1];
                double shift = joint - Knots[0];

                for (int i = 0; i < Knots.Count; i++)
                {
                    Knots[i] = i <= Degree ? joint : Math.Max(Knots[i] + shift, Knots[i - 1]);
                }

                Scale(before.Points[before.Points.Count - 1][3] / Points[0][3]);
            }

            /// <summary>Runs the knots back from the start of the one after, its clamped end made that knot to the bit, and scales the weights to the one it meets.</summary>
            internal void Lead(Nurbs after)
            {
                double joint = after.Knots[0];
                double shift = joint - Knots[Knots.Count - 1];
                int n = Knots.Count;

                for (int i = n - 1; i >= 0; i--)
                {
                    Knots[i] = i >= n - 1 - Degree ? joint : Math.Min(Knots[i] + shift, Knots[i + 1]);
                }

                Scale(after.Points[0][3] / Points[Points.Count - 1][3]);
            }

            /// <summary>Scales every weight, which leaves the curve as it is.</summary>
            private void Scale(double factor)
            {
                if (factor == 1.0)
                {
                    return;
                }

                for (int i = 0; i < Points.Count; i++)
                {
                    double[] q = Points[i];
                    Points[i] = new[] { q[0] * factor, q[1] * factor, q[2] * factor, q[3] * factor };
                }
            }
        }

        #endregion

        #region Chaining

        /// <summary>Refuses a source that is closed, or fitted, which has no ends of its own to join at.</summary>
        private static void Open(bool closed, bool fitted, string what)
        {
            if (closed)
            {
                throw new InvalidOperationException("A closed " + what + " has no ends, and nothing is joined to it.");
            }

            if (fitted)
            {
                throw new InvalidOperationException("A fitted polyline is drawn through vertices the fitting made: straighten it to join to it.");
            }
        }

        /// <summary>Refuses a polyline of no vertices, which has no ends.</summary>
        private static void Vertices(int count)
        {
            if (count == 0)
            {
                throw new InvalidOperationException("A polyline of no vertices has no ends, and nothing is joined to it.");
            }
        }

        private static string NoVertices(DxfEntity entity) => "The " + entity.GetType().Name + " has no vertices, and nothing of it is joined.";

        /// <summary>Whether points lie along one line within the tolerance: that through the first and the one farthest from it.</summary>
        private static bool Straight(List<GeoPoint3> points, Tolerance tolerance)
        {
            GeoPoint3 first = points[0];
            GeoPoint3 farthest = first;

            foreach (GeoPoint3 p in points)
            {
                if (p.DistanceTo(first) > farthest.DistanceTo(first))
                {
                    farthest = p;
                }
            }

            double length = first.DistanceTo(farthest);

            if (!(length > tolerance.EqualPoint))
            {
                return true;
            }

            GeoVector3 unit = first.GetVectorTo(farthest).Divide(length);
            return points.TrueForAll(p => Along(first, unit, p, tolerance));
        }

        private static string Closed(DxfEntity entity) => "The " + entity.GetType().Name + " is closed, and AutoCAD's JOIN joins no closed curve.";

        private static string OffThePlane(DxfEntity entity) => "The " + entity.GetType().Name + " is not in the plane of the polyline within the point tolerance.";

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>The entities to join, as the joining walks them, and what it does with one it does not join.</summary>
        private sealed class Work
        {
            private readonly bool _single;
            private readonly string _parameter;

            internal Work(List<DxfEntity> others, bool single, string parameter, Tolerance tolerance)
            {
                Others = others;
                _single = single;
                _parameter = parameter;
                Tolerance = tolerance;
            }

            /// <summary>The entities given, in their order; null for one given before.</summary>
            internal List<DxfEntity> Others { get; }

            internal Tolerance Tolerance { get; }

            /// <summary>Throws why an entity is not joined where one alone is joined; nothing where several are, as that one is left out.</summary>
            internal void Refuse(string why)
            {
                if (_single)
                {
                    throw new ArgumentException(why, _parameter);
                }
            }

            /// <summary>
            /// The indices of the links taken in a chain, in increasing order; where one alone is joined and its link is not taken,
            /// refused, as it meets neither end.
            /// </summary>
            internal List<int> Joined(List<Link> links)
            {
                var joined = new List<int>();

                foreach (Link link in links)
                {
                    if (link.Used && !link.RunsBack)
                    {
                        joined.Add(link.Index);
                    }
                }

                if (_single && joined.Count == 0 && links.Count > 0 && links[0].RunsBack)
                {
                    throw new ArgumentException("The " + Others[links[0].Index].GetType().Name + " runs back over the piece before it at the end it meets, as a line drawn twice does, and is not joined.", _parameter);
                }

                if (_single && joined.Count == 0 && links.Count > 0)
                {
                    throw new ArgumentException("The " + Others[links[0].Index].GetType().Name + " meets neither end within the point tolerance: AutoCAD's JOIN joins it end to end.", _parameter);
                }

                return joined;
            }
        }

        /// <summary>
        /// A piece to chain: its index among those given, its place among the links, its ends, its middle (the same point
        /// whichever way it runs), and how it was taken.
        /// </summary>
        private sealed class Link
        {
            internal Link(int index, int position, GeoPoint3 start, GeoPoint3 end, GeoPoint3 middle, bool point)
            {
                Index = index;
                Position = position;
                Start = start;
                End = end;
                Middle = middle;
                IsPoint = point;
            }

            internal int Index { get; }

            internal int Position { get; }

            internal GeoPoint3 Start { get; }

            internal GeoPoint3 End { get; }

            internal GeoPoint3 Middle { get; }

            /// <summary>Whether it is of no length: joined where it touches an end, adding nothing.</summary>
            internal bool IsPoint { get; }

            internal bool Used { get; set; }

            /// <summary>Whether it runs the other way along the chain.</summary>
            internal bool Reversed { get; set; }

            /// <summary>Whether it was taken to run back over the piece before it, and left out.</summary>
            internal bool RunsBack { get; set; }
        }

        /// <summary>
        /// What lies at an end of a chain, seen from that end: the far end of the piece there, or of the source's segment there,
        /// and its middle; a piece that would run from the end to that far end through that middle runs back over it.
        /// </summary>
        private sealed class Tail
        {
            internal Tail(GeoPoint3 far, GeoPoint3 middle)
            {
                Far = far;
                Middle = middle;
            }

            internal GeoPoint3 Far { get; }

            internal GeoPoint3 Middle { get; }
        }

        /// <summary>
        /// The pieces chained to a source end to end: those put before its start, the nearest first, each turned to end where
        /// the next begins; those put after its end, the nearest first, each turned to start where the one before ends; and
        /// whether the chain came back to its start.
        /// </summary>
        private sealed class Chain
        {
            private Chain()
            {
            }

            internal List<Link> Before { get; } = new List<Link>();

            internal List<Link> After { get; } = new List<Link>();

            internal bool Closed { get; private set; }

            /// <summary>How many pieces of some length were put on.</summary>
            internal int Attached => Before.Count + After.Count;

            /// <summary>
            /// Chains links to the ends of a source: on from its end while a link has an end there, then back from its start,
            /// each time the link of lowest index that meets the end reached, by its start where both of its ends do on from
            /// the end and by its end back from the start; a link of no length taken where it touches, adding nothing; and
            /// stopped, closed, where a link brings the two ends together. A link that would run back over what lies at the end,
            /// to its far end through its middle, is taken and left out.
            /// </summary>
            internal static Chain Of(List<Link> links, GeoPoint3 start, GeoPoint3 end, Tail startTail, Tail endTail, Tolerance tolerance)
            {
                var chain = new Chain();

                if (links.Count == 0)
                {
                    return chain;
                }

                var ends = new Ends(links, tolerance.EqualPoint > 0.0 ? tolerance.EqualPoint : 1.0);

                foreach (bool onward in new[] { true, false })
                {
                    while (!chain.Closed)
                    {
                        Link link = ends.Take(onward ? end : start, tolerance, !onward, out bool byEnd);

                        if (link == null)
                        {
                            break;
                        }

                        if (link.IsPoint)
                        {
                            continue;
                        }

                        // On from the end a link meeting it by its end runs the other way; back from the start, one meeting it
                        // by its start.
                        link.Reversed = onward == byEnd;
                        GeoPoint3 far = byEnd ? link.Start : link.End;
                        Tail tail = onward ? endTail : startTail;

                        // A piece that would run back over the one at the end, as a line drawn twice, is left out.
                        if (tail != null && far.IsEqualTo(tail.Far, tolerance) && link.Middle.IsEqualTo(tail.Middle, tolerance))
                        {
                            link.RunsBack = true;
                            continue;
                        }

                        if (onward)
                        {
                            chain.After.Add(link);
                            endTail = new Tail(end, link.Middle);
                            end = far;
                        }
                        else
                        {
                            chain.Before.Add(link);
                            startTail = new Tail(start, link.Middle);
                            start = far;
                        }

                        chain.Closed = start.IsEqualTo(end, tolerance);
                    }
                }

                return chain;
            }
        }

        /// <summary>
        /// The ends of the links in cells of a size, a cell to each triple of whole numbers of sizes along the axes: an end within
        /// the size of a point is in the cell of the point or one next to it.
        /// </summary>
        private sealed class Ends
        {
            private readonly List<Link> _links;
            private readonly double _size;
            private readonly Dictionary<Cell, List<int>> _cells = new Dictionary<Cell, List<int>>();

            internal Ends(List<Link> links, double size)
            {
                _links = links;
                _size = size;

                // Each end an entry, twice the link's place and one for its end, in increasing order in each cell.
                for (int i = 0; i < links.Count; i++)
                {
                    Put(links[i].Start, 2 * i);
                    Put(links[i].End, 2 * i + 1);
                }
            }

            /// <summary>
            /// Takes the link of lowest place with an end within the tolerance of a point, by its end where asked and both of its
            /// ends meet the point, else by the one that does; null where none does. The links taken before are dropped from
            /// the cells walked.
            /// </summary>
            internal Link Take(GeoPoint3 at, Tolerance tolerance, bool preferEnd, out bool byEnd)
            {
                Cell cell = CellOf(at);
                int best = -1;
                byEnd = false;

                for (long dx = -1; dx <= 1; dx++)
                {
                    for (long dy = -1; dy <= 1; dy++)
                    {
                        for (long dz = -1; dz <= 1; dz++)
                        {
                            if (!_cells.TryGetValue(new Cell(cell.X + dx, cell.Y + dy, cell.Z + dz), out List<int> entries))
                            {
                                continue;
                            }

                            int kept = 0;

                            for (int r = 0; r < entries.Count; r++)
                            {
                                int entry = entries[r];
                                Link link = _links[entry >> 1];

                                if (link.Used)
                                {
                                    continue;
                                }

                                entries[kept++] = entry;
                                int place = entry >> 1;
                                bool isEnd = (entry & 1) == 1;

                                if (best >= 0 && (place > best || (place == best && isEnd != preferEnd)))
                                {
                                    continue;
                                }

                                if ((isEnd ? link.End : link.Start).IsEqualTo(at, tolerance))
                                {
                                    best = place;
                                    byEnd = isEnd;
                                }
                            }

                            entries.RemoveRange(kept, entries.Count - kept);
                        }
                    }
                }

                if (best < 0)
                {
                    return null;
                }

                _links[best].Used = true;
                return _links[best];
            }

            private void Put(GeoPoint3 point, int entry)
            {
                Cell cell = CellOf(point);

                if (!_cells.TryGetValue(cell, out List<int> entries))
                {
                    entries = new List<int>();
                    _cells[cell] = entries;
                }

                entries.Add(entry);
            }

            private Cell CellOf(GeoPoint3 point) => new Cell(Whole(point.X), Whole(point.Y), Whole(point.Z));

            /// <summary>The whole number of sizes below a coordinate, held to the range of a long.</summary>
            private long Whole(double value)
            {
                double floor = Math.Floor(value / _size);
                return floor >= long.MaxValue ? long.MaxValue : floor <= long.MinValue ? long.MinValue : (long)floor;
            }
        }

        /// <summary>A cell of <see cref="Ends"/>, with an equality and a hash of its own, its three numbers mixed.</summary>
        private struct Cell : IEquatable<Cell>
        {
            internal Cell(long x, long y, long z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            internal long X { get; }

            internal long Y { get; }

            internal long Z { get; }

            public bool Equals(Cell other) => X == other.X && Y == other.Y && Z == other.Z;

            public override bool Equals(object obj) => obj is Cell other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    ulong h = (ulong)X * 0x9E3779B97F4A7C15UL;
                    h = (h ^ (h >> 31) ^ (ulong)Y) * 0xBF58476D1CE4E5B9UL;
                    h = (h ^ (h >> 29) ^ (ulong)Z) * 0x94D049BB133111EBUL;
                    return (int)(h ^ (h >> 32));
                }
            }
        }

        /// <summary>The system of an entity's normal, as the arbitrary axis algorithm makes it: a point taken into it and out.</summary>
        private struct Frame
        {
            internal Frame(DxfEntity entity)
            {
                GeoTransform3 ecs = entity.Ecs;
                U = ecs.Transform(GeoVector3.XAxis);
                V = ecs.Transform(GeoVector3.YAxis);
                N = ecs.Transform(GeoVector3.ZAxis);
            }

            internal GeoVector3 U { get; }

            internal GeoVector3 V { get; }

            internal GeoVector3 N { get; }

            internal GeoPoint3 Local(GeoPoint3 p)
            {
                var d = new GeoVector3(p.X, p.Y, p.Z);
                return new GeoPoint3(d.DotProduct(U), d.DotProduct(V), d.DotProduct(N));
            }

            internal GeoPoint3 World(double x, double y, double z)
                => new GeoPoint3(U.X * x + V.X * y + N.X * z, U.Y * x + V.Y * y + N.Y * z, U.Z * x + V.Z * y + N.Z * z);

            /// <summary>Whether another is the same system to the bit.</summary>
            internal bool IsSameAs(Frame other)
                => U.X == other.U.X && U.Y == other.U.Y && U.Z == other.U.Z && N.X == other.N.X && N.Y == other.N.Y && N.Z == other.N.Z;
        }

        #endregion
    }
}
