using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Spatial
{
    /// <summary>
    /// A body made ready to be asked many questions: its openings cut in once, its surface meshed and indexed
    /// once, its box kept.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every question a <see cref="GeoSolid3"/> answers takes its openings into account, and the questions that
    /// need the material cut it each time they are asked — a distance cuts every opening, since the nearest
    /// material can sit on the rim of one the probe never comes near. A body is a value and keeps no cache of
    /// its own, so a plate asked about a hundred bolts is cut a hundred times. Preparing it pays for that once,
    /// together with the mesh and the index a large body is searched through.
    /// </para>
    /// <para>
    /// The answers are the body's own: whatever the prepared body says, the body says too, only slower. It is a
    /// snapshot — immutable, safe to share between threads, and holding the body it was made from.
    /// </para>
    /// </remarks>
    public sealed class GeoPreparedSolid3
    {
        /// <summary>
        /// The directions a ray is cast in to tell inside from outside, the same five <c>Containment3</c> uses,
        /// fixed so that the same question always gets the same answer.
        /// </summary>
        private static readonly GeoVector3[] RayDirections =
        {
            new GeoVector3(0.5773502691896258, 0.5773502691896258, 0.5773502691896258),
            new GeoVector3(-0.2672612419124244, 0.5345224838248488, 0.8017837257372732),
            new GeoVector3(0.8017837257372732, -0.2672612419124244, 0.5345224838248488),
            new GeoVector3(0.4082482904638631, 0.8164965809277261, -0.4082482904638631),
            new GeoVector3(-0.6666666666666666, 0.3333333333333333, 0.6666666666666666)
        };

        private readonly GeoTriangle3[] _surface;

        /// <summary>
        /// Prepares a body, using the default tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public GeoPreparedSolid3(GeoSolid3 solid) : this(solid, Tolerance.Global)
        {
        }

        /// <summary>
        /// Prepares a body, within a tolerance.
        /// </summary>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the surface is meshed to.</param>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        public GeoPreparedSolid3(GeoSolid3 solid, Tolerance tolerance)
        {
            Solid = solid ?? throw new ArgumentNullException(nameof(solid));

            // Openings that take all the material leave the gross body, as every query of the body does.
            Material = Material3.Whole(solid, tolerance);
            Box = Material.GetAabb();
            _surface = Material.Triangulate(tolerance);
            Index = new GeoBvh3(_surface);
        }

        /// <summary>
        /// Gets the body as it was given, openings and all.
        /// </summary>
        public GeoSolid3 Solid { get; }

        /// <summary>
        /// Gets the body with its openings cut in: where its material actually ends.
        /// </summary>
        public GeoSolid3 Material { get; }

        /// <summary>
        /// Gets the axis-aligned box round the material.
        /// </summary>
        public GeoAabb3 Box { get; }

        /// <summary>
        /// Gets the mesh of where the material ends, every triangle lying on it.
        /// </summary>
        public IReadOnlyList<GeoTriangle3> Surface => _surface;

        /// <summary>
        /// Gets the index over <see cref="Surface"/>.
        /// </summary>
        public GeoBvh3 Index { get; }

        /// <inheritdoc/>
        public override string ToString() => $"GeoPreparedSolid3[{_surface.Length} triangles, {Solid.Openings.Count} openings cut]";

        #region A point

        /// <summary>
        /// Locates a point, using the default tolerance.
        /// </summary>
        public PointLocation Locate(GeoPoint3 point) => Locate(point, Tolerance.Global);

        /// <summary>
        /// Locates a point: inside the material, on where it ends, or outside it, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray is cast through the index and its crossings counted, as <c>Containment3</c> counts them face by
        /// face. Two crossings landing together mean the ray ran through a seam of the mesh — an edge or a corner
        /// the index names once per triangle — and the ray is cast again in another direction; should every
        /// direction land on a seam, the body's own test decides.
        /// </remarks>
        public PointLocation Locate(GeoPoint3 point, Tolerance tolerance)
        {
            if (Box.DistanceTo(point) > tolerance.EqualPoint)
            {
                return PointLocation.OutSide;
            }

            if (Index.DistanceTo(point, tolerance) <= tolerance.EqualPoint)
            {
                return PointLocation.OnSide;
            }

            double seam = tolerance.EqualPoint * 100.0;

            foreach (GeoVector3 direction in RayDirections)
            {
                var ray = new GeoRay3(point, direction);
                List<double> reaches = Reaches(ray, tolerance);
                bool clean = true;

                for (int i = 1; i < reaches.Count && clean; i++)
                {
                    clean = reaches[i] - reaches[i - 1] > seam;
                }

                if (clean)
                {
                    return reaches.Count % 2 == 1 ? PointLocation.Inside : PointLocation.OutSide;
                }
            }

            return Containment3.Locate(Material, point, tolerance);
        }

        /// <summary>
        /// Checks whether the material holds a point, on where it ends included, using the default tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point) => Contains(point, Tolerance.Global);

        /// <summary>
        /// Checks whether the material holds a point, on where it ends included, within a tolerance.
        /// </summary>
        public bool Contains(GeoPoint3 point, Tolerance tolerance) => Locate(point, tolerance) != PointLocation.OutSide;

        /// <summary>
        /// Gets how far a point is from the material, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets how far a point is from the material, within a tolerance; nought for a point it holds.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance)
            => Locate(point, tolerance) == PointLocation.OutSide ? Index.DistanceTo(point, tolerance) : 0.0;

        /// <summary>
        /// Gets how far a point is from where the material ends, negative inside it, using the default tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point) => SignedDistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets how far a point is from where the material ends, negative inside it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance)
        {
            PointLocation where = Locate(point, tolerance);

            if (where == PointLocation.OnSide)
            {
                return 0.0;
            }

            double reach = Index.DistanceTo(point, tolerance);

            return where == PointLocation.Inside ? -reach : reach;
        }

        /// <summary>
        /// Gets the point where the material ends nearest a point, using the default tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetClosestPointOnBoundary(point, Tolerance.Global);

        /// <summary>
        /// Gets the point where the material ends nearest a point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Index.GetClosestPoint(point, tolerance);

        #endregion

        #region A ray

        /// <summary>
        /// Gets where a ray passes where the material ends, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoRay3 ray) => GetIntersections(ray, Tolerance.Global);

        /// <summary>
        /// Gets where a ray passes where the material ends, in the order the ray meets them, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A crossing landing on a seam of the mesh arrives once per triangle and is reported once, as the body
        /// reports it.
        /// </remarks>
        public GeoPoint3[] GetIntersections(GeoRay3 ray, Tolerance tolerance)
        {
            var hits = new List<GeoPoint3>();

            foreach (double reach in Reaches(ray, tolerance))
            {
                if (hits.Count > 0 && reach - ray.GetDistanceAtPoint(hits[hits.Count - 1]) <= tolerance.EqualPoint)
                {
                    continue;
                }

                hits.Add(ray.GetPointAtDistance(reach));
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Checks whether a ray starts in the material or passes through where it ends, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray) => CollidesWith(ray, Tolerance.Global);

        /// <summary>
        /// Checks whether a ray starts in the material or passes through where it ends, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoRay3 ray, Tolerance tolerance)
            => Contains(ray.Origin, tolerance) || Index.GetIntersections(ray, tolerance).Length > 0;

        /// <summary>
        /// The distances along a ray of every crossing the index reports, sorted, one per triangle.
        /// </summary>
        private List<double> Reaches(GeoRay3 ray, Tolerance tolerance)
        {
            var reaches = new List<double>();

            foreach (GeoPoint3 hit in Index.GetIntersections(ray, tolerance))
            {
                reaches.Add(ray.GetDistanceAtPoint(hit));
            }

            reaches.Sort();

            return reaches;
        }

        #endregion

        #region Another body

        /// <summary>
        /// Checks whether this body and another touch or overlap, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoPreparedSolid3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this body and another touch or overlap, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two surfaces that meet settle it through the two indexes; two that do not can still be one body inside
        /// the other, which a corner of the inner one settles.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other body is null.</exception>
        public bool CollidesWith(GeoPreparedSolid3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (!Box.CollidesWith(other.Box, tolerance))
            {
                return false;
            }

            if (Index.CollidesWith(other.Index, tolerance))
            {
                return true;
            }

            return (_surface.Length > 0 && other.Locate(_surface[0].A, tolerance) == PointLocation.Inside)
                || (other._surface.Length > 0 && Locate(other._surface[0].A, tolerance) == PointLocation.Inside);
        }

        /// <summary>
        /// Checks whether this body and a body not prepared touch or overlap, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 other) => CollidesWith(other, Tolerance.Global);

        /// <summary>
        /// Checks whether this body and a body not prepared touch or overlap, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSolid3 other, Tolerance tolerance) => Collision3.CollidesWith(Material, other, tolerance);

        /// <summary>
        /// Gets how far this body is from another, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPreparedSolid3 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Gets how far this body is from another, within a tolerance; nought where they touch or overlap.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other body is null.</exception>
        public double DistanceTo(GeoPreparedSolid3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return CollidesWith(other, tolerance) ? 0.0 : Index.DistanceTo(other.Index, tolerance);
        }

        /// <summary>
        /// Gets how far this body is from a body not prepared, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 other) => DistanceTo(other, Tolerance.Global);

        /// <summary>
        /// Gets how far this body is from a body not prepared, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 other, Tolerance tolerance) => Distance3.DistanceTo(Material, other, tolerance);

        /// <summary>
        /// Gets the shortest segment from this body to another, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPreparedSolid3 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment from this body to another, within a tolerance; of no length where they touch.
        /// </summary>
        /// <remarks>
        /// The two surfaces are meshed and indexed already, so the answer is found by walking the two indexes,
        /// nearest boxes first: the segment
        /// <see cref="Projection3.GetShortestLineTo(GeoSolid3, GeoSolid3, Tolerance)"/> finds on the two materials,
        /// without meshing them again and weighing every pair of faces. Where several pairs are nearest alike, as
        /// between two parallel faces, either may be the one given.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other body is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPreparedSolid3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (Index.TryGetShortestLineTo(other.Index, double.PositiveInfinity, tolerance, out GeoLine3 line))
            {
                return line;
            }

            // A surface with nothing on it: answered as it always was.
            return Projection3.GetShortestLineTo(Material, other.Material, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment from this body's surface to another's when it is shorter than a reach.
        /// </summary>
        /// <param name="other">The other body.</param>
        /// <param name="reach">How short the segment has to be.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="line">The segment, as <see cref="GetShortestLineTo(GeoPreparedSolid3, Tolerance)"/> gives it.</param>
        /// <returns>false when the two surfaces come no nearer than the reach.</returns>
        /// <remarks>
        /// One walk of the two indexes answers both whether the bodies come within the reach and where, and two
        /// bodies farther apart than the reach cost no more than the test of their outer boxes. It measures surface
        /// to surface: the caller settles first whether the bodies touch or one holds the other.
        /// </remarks>
        internal bool TryGetShortestLineWithin(GeoPreparedSolid3 other, double reach, Tolerance tolerance, out GeoLine3 line)
            => Index.TryGetShortestLineTo(other.Index, reach, tolerance, out line);

        /// <summary>
        /// Gets one body per region this body shares with another, using the default tolerance.
        /// </summary>
        public GeoSolid3[] Intersect(GeoPreparedSolid3 other) => Intersect(other, Tolerance.Global);

        /// <summary>
        /// Gets one body per region this body shares with another, within a tolerance; empty when they share no
        /// volume.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other body is null.</exception>
        public GeoSolid3[] Intersect(GeoPreparedSolid3 other, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Box.CollidesWith(other.Box, tolerance)
                ? Boolean3.Intersect(Material, other.Material, tolerance)
                : new GeoSolid3[0];
        }

        /// <summary>
        /// Gets where this body and another lie against each other, face to face, using the default tolerance.
        /// </summary>
        public bool TryGetContact(GeoPreparedSolid3 other, out GeoFace3[] contact) => TryGetContact(other, out contact, Tolerance.Global);

        /// <summary>
        /// Gets where this body and another lie against each other, face to face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the other body is null.</exception>
        public bool TryGetContact(GeoPreparedSolid3 other, out GeoFace3[] contact, Tolerance tolerance)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return Boolean3.TryGetContact(Material, other.Material, out contact, tolerance);
        }

        #endregion
    }
}
