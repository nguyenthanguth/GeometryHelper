using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Bodies made by moving a flat profile: straight out, along a path, or round an axis.
    /// </summary>
    /// <remarks>
    /// Every body is built face by face and checked by its signed volume, so it always comes back wound
    /// outwards whatever way round the profile was drawn. The faces along a path or round an axis are flat:
    /// a profile carried along a straight piece of path sweeps flat sides, and one turned round an axis sweeps
    /// trapezia, so a curve comes back as the facets of a chord tolerance, the way a circle does.
    /// </remarks>
    internal static class Sweep3
    {
        /// <summary>
        /// How far a path may turn at one vertex, as the cosine of the angle between its two pieces: a bend
        /// sharper than about 170 degrees would put the mitre at one vertex out beyond any sensible distance.
        /// </summary>
        private const double SharpestTurnCosine = -0.985;

        #region Straight out

        internal static GeoSolid3 Extrude(GeoFace3 profile, GeoVector3 direction, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            GeoVector3 normal = profile.Boundary.Normal;
            double rise = direction.DotProduct(normal);

            if (!(Math.Abs(rise) > tolerance.EqualPlanar))
            {
                throw new ArgumentException("The direction lies in the plane of the profile, so the profile would sweep out no volume.", nameof(direction));
            }

            // Every ring is wound counter-clockwise about the way the profile moves; a hole the same way as
            // the outline, as a face keeps it.
            bool along = rise > 0.0;
            List<GeoPoint3> outline = Ring(profile.Boundary.Vertices, along);
            var holes = new List<List<GeoPoint3>>();

            foreach (GeoPolygon3 hole in profile.Holes)
            {
                holes.Add(Ring(hole.Vertices, along));
            }

            var faces = new List<GeoFace3>();

            faces.Add(Face(Reversed(outline), ReversedAll(holes)));
            faces.Add(Face(Moved(outline, direction), MovedAll(holes, direction)));
            AddSides(faces, outline, direction, false);

            foreach (List<GeoPoint3> hole in holes)
            {
                AddSides(faces, hole, direction, true);
            }

            return Outwards(faces);
        }

        internal static GeoSolid3 Extrude(GeoFace2 profile, GeoCoordinateSystem3 placement, double length, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Guard.Finite(length, nameof(length), "A length has to be a number.");

            var holes = new List<GeoPolygon3>();

            foreach (GeoPolygon2 hole in profile.Holes)
            {
                holes.Add(Placed(hole, placement));
            }

            return Extrude(new GeoFace3(Placed(profile.Boundary, placement), holes), placement.ZAxis.Multiply(length), tolerance);
        }

        internal static GeoSolid3 Cylinder(GeoPoint3 start, GeoPoint3 end, double radius, int segments, Tolerance tolerance)
        {
            Guard.Positive(radius, nameof(radius), "A cylinder must have a positive radius.");
            Tessellation.RequireSegmentCount(segments, 3);

            GeoVector3 axis = start.GetVectorTo(end);

            if (!axis.TryGetNormal(out GeoVector3 unit, tolerance))
            {
                throw new ArgumentException("A cylinder needs two different ends.", nameof(end));
            }

            GetBasis(unit, out GeoVector3 b1, out GeoVector3 b2);

            var circle = new GeoPoint3[segments];

            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                circle[i] = start.Add(b1.Multiply(radius * Math.Cos(angle))).Add(b2.Multiply(radius * Math.Sin(angle)));
            }

            return Extrude(new GeoFace3(new GeoPolygon3(circle)), axis, tolerance);
        }

        #endregion

        #region Along a path

        internal static GeoSolid3 Sweep(GeoPolygon2 profile, IReadOnlyList<GeoPoint3> path, GeoVector3? up, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            var points = new List<GeoPoint3>();

            foreach (GeoPoint3 point in path)
            {
                if (points.Count == 0 || points[points.Count - 1].DistanceTo(point) > tolerance.EqualPoint)
                {
                    points.Add(point);
                }
            }

            if (points.Count < 2)
            {
                throw new ArgumentException("A path needs two points some way apart.", nameof(path));
            }

            // The profile counter-clockwise in its own plane, so that with the frame right-handed about the
            // path every section is wound counter-clockwise about the way it travels.
            var corners = new List<GeoPoint2>(profile.Vertices);

            if (profile.SignedArea < 0.0)
            {
                corners.Reverse();
            }

            var directions = new GeoVector3[points.Count - 1];

            // Neighbouring points are further apart than the point tolerance of this sweep, so every step has a
            // length to divide by; Normalize would judge it against the default tolerance instead.
            for (int i = 0; i < directions.Length; i++)
            {
                GeoVector3 step = points[i].GetVectorTo(points[i + 1]);
                directions[i] = step.Divide(step.Length);
            }

            GeoVector3 y = StartUp(directions[0], up, tolerance);
            GeoVector3 x = y.CrossProduct(directions[0]);

            var sections = new List<GeoPoint3[]> { Section(points[0], x, y, corners, null, directions[0]) };

            for (int i = 1; i < points.Count - 1; i++)
            {
                GeoVector3 into = directions[i - 1];
                GeoVector3 outOf = directions[i];
                double cosine = into.DotProduct(outOf);

                if (cosine > 1.0 - 1E-12)
                {
                    // Straight on: nothing turns here, and a section would only split the sides in two.
                    continue;
                }

                if (cosine < SharpestTurnCosine)
                {
                    throw new ArgumentException("The path turns back on itself at vertex " + i + ", so no section can be set there.", nameof(path));
                }

                // The section at a bend lies on the plane halving it, where the sides of the piece before
                // meet the sides of the piece after.
                GeoVector3 mitre = into.Add(outOf).Normalize();
                sections.Add(Section(points[i], x, y, corners, mitre, into));

                // The frame turns with the path about the axis of the bend and nothing else, so the profile
                // does not twist along it. The turn is more than straight on, so the cross product of the two
                // unit directions, the sine of the turn, has a length to divide by: Normalize would read that sine
                // as a length against the vector tolerance and refuse a gentle bend.
                GeoVector3 turn = into.CrossProduct(outOf);
                GeoVector3 axis = turn.Divide(turn.Length);
                double angle = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cosine)));

                x = Rotate(x, axis, angle);
                y = Rotate(y, axis, angle);
            }

            sections.Add(Section(points[points.Count - 1], x, y, corners, null, directions[directions.Length - 1]));

            var faces = new List<GeoFace3>();

            faces.Add(new GeoFace3(new GeoPolygon3(Reversed(new List<GeoPoint3>(sections[0])))));
            faces.Add(new GeoFace3(new GeoPolygon3(sections[sections.Count - 1])));

            for (int s = 0; s + 1 < sections.Count; s++)
            {
                AddBetween(faces, sections[s], sections[s + 1]);
            }

            return Outwards(faces);
        }

        /// <summary>
        /// A regular polygon of a radius about the origin, counter-clockwise, with enough sides that no side
        /// strays further than a chord tolerance from the circle.
        /// </summary>
        internal static GeoPolygon2 Circle(double radius, double chordTolerance)
        {
            Guard.Positive(radius, nameof(radius), "A radius has to be a positive number.");

            int segments = Math.Max(3, Tessellation.SegmentsForChordTolerance(radius, 2.0 * Math.PI, chordTolerance));
            var corners = new GeoPoint2[segments];

            for (int i = 0; i < segments; i++)
            {
                double angle = 2.0 * Math.PI * i / segments;
                corners[i] = new GeoPoint2(radius * Math.Cos(angle), radius * Math.Sin(angle));
            }

            return new GeoPolygon2(corners);
        }

        private static GeoPoint3[] Section(GeoPoint3 at, GeoVector3 x, GeoVector3 y, List<GeoPoint2> corners, GeoVector3? mitre, GeoVector3 travel)
        {
            var section = new GeoPoint3[corners.Count];

            for (int k = 0; k < corners.Count; k++)
            {
                GeoVector3 offset = x.Multiply(corners[k].X).Add(y.Multiply(corners[k].Y));

                if (mitre.HasValue)
                {
                    // Slid along the piece arriving here until it reaches the mitre plane.
                    double slide = -mitre.Value.DotProduct(offset) / mitre.Value.DotProduct(travel);
                    offset = offset.Add(travel.Multiply(slide));
                }

                section[k] = at.Add(offset);
            }

            return section;
        }

        private static GeoVector3 StartUp(GeoVector3 travel, GeoVector3? up, Tolerance tolerance)
        {
            GeoVector3 wanted;

            if (up.HasValue)
            {
                wanted = up.Value;
            }
            else
            {
                // The profile's up is the world's up where the path allows it, so a beam along the ground keeps
                // its profile upright; a path starting straight up takes the world's Y instead.
                wanted = Math.Abs(travel.Z) < 0.9 ? GeoVector3.ZAxis : GeoVector3.YAxis;
            }

            GeoVector3 across = wanted.Subtract(travel.Multiply(wanted.DotProduct(travel)));

            if (!across.TryGetNormal(out GeoVector3 unit, tolerance))
            {
                throw new ArgumentException("The up direction runs along the start of the path, so it says nothing about which way the profile stands.", nameof(up));
            }

            return unit;
        }

        #endregion

        #region Round an axis

        internal static GeoSolid3 Revolve(GeoPolygon2 profile, GeoCoordinateSystem3 placement, double angle, double chordTolerance, Tolerance tolerance)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            Guard.Positive(angle, nameof(angle), "An angle to revolve through has to be a positive number.");

            if (angle > 2.0 * Math.PI + 1E-9)
            {
                throw new ArgumentOutOfRangeException(nameof(angle), angle, "A profile revolves through a whole turn at most.");
            }

            bool whole = angle >= 2.0 * Math.PI - 1E-9;
            double reach = 0.0;

            foreach (GeoPoint2 corner in profile.Vertices)
            {
                if (corner.X < -tolerance.EqualPoint)
                {
                    throw new ArgumentException("The profile reaches across the axis; it has to lie on one side of it, X nought or more.", nameof(profile));
                }

                reach = Math.Max(reach, corner.X);
            }

            if (!(reach > tolerance.EqualPoint))
            {
                throw new ArgumentException("The profile lies along the axis, so it would sweep out no volume.", nameof(profile));
            }

            int steps = Math.Max(whole ? 3 : 1, Tessellation.SegmentsForChordTolerance(reach, angle, chordTolerance));

            // Turned positively about the Y axis, a profile drawn in the XY plane moves towards -Z; wound
            // clockwise in its plane it is counter-clockwise about the way it moves.
            var corners = new List<GeoPoint2>(profile.Vertices);

            if (profile.SignedArea > 0.0)
            {
                corners.Reverse();
            }

            int count = whole ? steps : steps + 1;
            var sections = new GeoPoint3[count][];

            for (int j = 0; j < count; j++)
            {
                double turn = angle * j / steps;
                double cos = Math.Cos(turn);
                double sin = Math.Sin(turn);
                GeoVector3 radial = placement.XAxis.Multiply(cos).Subtract(placement.ZAxis.Multiply(sin));

                sections[j] = new GeoPoint3[corners.Count];

                for (int k = 0; k < corners.Count; k++)
                {
                    double u = Math.Max(0.0, corners[k].X);
                    sections[j][k] = placement.Origin.Add(placement.YAxis.Multiply(corners[k].Y)).Add(radial.Multiply(u));
                }
            }

            var faces = new List<GeoFace3>();

            for (int j = 0; j < steps; j++)
            {
                AddBetween(faces, sections[j], sections[(j + 1) % count]);
            }

            if (!whole)
            {
                faces.Add(new GeoFace3(new GeoPolygon3(Distinct(Reversed(new List<GeoPoint3>(sections[0])), tolerance))));
                faces.Add(new GeoFace3(new GeoPolygon3(Distinct(new List<GeoPoint3>(sections[count - 1]), tolerance))));
            }

            return Outwards(faces);
        }

        #endregion

        #region Faces

        private static List<GeoPoint3> Ring(IReadOnlyList<GeoPoint3> vertices, bool asDrawn)
        {
            var ring = new List<GeoPoint3>(vertices);

            if (!asDrawn)
            {
                ring.Reverse();
            }

            return ring;
        }

        private static List<GeoPoint3> Reversed(List<GeoPoint3> ring)
        {
            var reversed = new List<GeoPoint3>(ring);
            reversed.Reverse();
            return reversed;
        }

        private static List<List<GeoPoint3>> ReversedAll(List<List<GeoPoint3>> rings)
        {
            var all = new List<List<GeoPoint3>>(rings.Count);

            foreach (List<GeoPoint3> ring in rings)
            {
                all.Add(Reversed(ring));
            }

            return all;
        }

        private static List<GeoPoint3> Moved(List<GeoPoint3> ring, GeoVector3 by)
        {
            var moved = new List<GeoPoint3>(ring.Count);

            foreach (GeoPoint3 point in ring)
            {
                moved.Add(point.Add(by));
            }

            return moved;
        }

        private static List<List<GeoPoint3>> MovedAll(List<List<GeoPoint3>> rings, GeoVector3 by)
        {
            var all = new List<List<GeoPoint3>>(rings.Count);

            foreach (List<GeoPoint3> ring in rings)
            {
                all.Add(Moved(ring, by));
            }

            return all;
        }

        private static GeoFace3 Face(List<GeoPoint3> outline, List<List<GeoPoint3>> holes)
        {
            var polygons = new List<GeoPolygon3>(holes.Count);

            foreach (List<GeoPoint3> hole in holes)
            {
                polygons.Add(new GeoPolygon3(hole));
            }

            return new GeoFace3(new GeoPolygon3(outline), polygons);
        }

        /// <summary>
        /// The sides a ring sweeps moving by a vector: outward for an outline wound counter-clockwise about the
        /// way it moves, and turned round for a hole, whose outside is the hole.
        /// </summary>
        private static void AddSides(List<GeoFace3> faces, List<GeoPoint3> ring, GeoVector3 by, bool hole)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                GeoPoint3 a = ring[i];
                GeoPoint3 b = ring[(i + 1) % ring.Count];

                faces.Add(new GeoFace3(hole
                    ? new GeoPolygon3(b, a, a.Add(by), b.Add(by))
                    : new GeoPolygon3(a, b, b.Add(by), a.Add(by))));
            }
        }

        /// <summary>
        /// The sides between two sections of the same number of points, a corner of one to the same corner of the
        /// next; where two corners of a side meet — a profile corner on the axis of a revolution — the side is a
        /// triangle, and where it has no area left it is left out.
        /// </summary>
        private static void AddBetween(List<GeoFace3> faces, GeoPoint3[] from, GeoPoint3[] to)
        {
            for (int k = 0; k < from.Length; k++)
            {
                int next = (k + 1) % from.Length;
                List<GeoPoint3> side = Distinct(new List<GeoPoint3> { from[k], from[next], to[next], to[k] }, Tolerance.Global);

                if (side.Count >= 3)
                {
                    faces.Add(new GeoFace3(new GeoPolygon3(side)));
                }
            }
        }

        private static List<GeoPoint3> Distinct(List<GeoPoint3> ring, Tolerance tolerance)
        {
            var kept = new List<GeoPoint3>(ring.Count);

            foreach (GeoPoint3 point in ring)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(point, tolerance))
                {
                    kept.Add(point);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            return kept;
        }

        /// <summary>
        /// The body the faces bound, wound outwards: turned inside out when its signed volume says the faces were
        /// built the other way round.
        /// </summary>
        private static GeoSolid3 Outwards(List<GeoFace3> faces) => new GeoSolid3(faces).TurnOutwards();

        private static GeoPolygon3 Placed(GeoPolygon2 polygon, GeoCoordinateSystem3 placement)
        {
            var corners = new GeoPoint3[polygon.VertexCount];

            for (int i = 0; i < corners.Length; i++)
            {
                GeoPoint2 corner = polygon[i];
                corners[i] = placement.ToGlobal(new GeoPoint3(corner.X, corner.Y, 0.0));
            }

            return new GeoPolygon3(corners);
        }

        private static GeoVector3 Rotate(GeoVector3 v, GeoVector3 axis, double angle)
        {
            double cos = Math.Cos(angle);
            double sin = Math.Sin(angle);

            return v.Multiply(cos)
                .Add(axis.CrossProduct(v).Multiply(sin))
                .Add(axis.Multiply(axis.DotProduct(v) * (1.0 - cos)));
        }

        private static void GetBasis(GeoVector3 axis, out GeoVector3 b1, out GeoVector3 b2)
        {
            GeoVector3 helper = Math.Abs(axis.X) < 0.9 ? GeoVector3.XAxis : GeoVector3.YAxis;

            b1 = helper.Subtract(axis.Multiply(helper.DotProduct(axis))).Normalize();
            b2 = axis.CrossProduct(b1);
        }

        #endregion
    }
}
