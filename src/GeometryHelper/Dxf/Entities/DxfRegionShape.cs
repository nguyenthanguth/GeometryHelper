using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// One loop of a REGION: the curves that bound it, joined end to end in the order the loop runs, as AutoCAD's <c>Explode</c>
    /// gives them for the region.
    /// </summary>
    /// <remarks>
    /// An outer loop runs counter-clockwise about the normal of its region and a hole clockwise. A whole circle or ellipse is one
    /// <see cref="DxfCircle"/> or <see cref="DxfEllipse"/>; each straight piece is a <see cref="DxfLine"/>, each piece of a circle
    /// a <see cref="DxfArc"/>, each piece of an ellipse a <see cref="DxfEllipse"/> of the parameters it spans. The curves are in no
    /// drawing: <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/> adds them to one.
    /// </remarks>
    public sealed class DxfRegionLoop
    {
        private readonly AcisLoop _loop;
        private readonly AcisVec _normal;
        private IReadOnlyList<DxfCurve> _curves;

        internal DxfRegionLoop(AcisLoop loop, AcisVec normal)
        {
            _loop = loop;
            _normal = normal;
        }

        internal AcisLoop Loop => _loop;

        /// <summary>Gets whether the loop is a hole in the face it bounds.</summary>
        public bool IsHole => _loop.IsHole;

        /// <summary>Gets the area the loop encloses, not taking away its holes: a square of side 10 gives 100, hole or not.</summary>
        public double Area => _loop.Area(_normal);

        /// <summary>Gets the length of the loop: a circle of radius 7 gives 43.9823.</summary>
        public double Perimeter => _loop.Perimeter;

        /// <summary>The curves of the loop made anew, as <see cref="Curves"/> gives them, for the parts an Explode returns.</summary>
        internal IReadOnlyList<DxfCurve> NewCurves() =>
            _loop.Curves.Select(c => DxfRegionShape.ToEntity(c, _normal)).ToList().AsReadOnly();

        /// <summary>
        /// Gets the curves of the loop in the order it runs, each a new entity in no drawing; the same list on each call.
        /// </summary>
        public IReadOnlyList<DxfCurve> Curves
        {
            get
            {
                if (_curves == null)
                {
                    _curves = _loop.Curves.Select(c => DxfRegionShape.ToEntity(c, _normal)).ToList().AsReadOnly();
                }

                return _curves;
            }
        }
    }

    /// <summary>
    /// The shape of a planar REGION: the loops that bound its faces, outer loops and holes, with the area and the perimeter
    /// AutoCAD's <c>Region</c> gives for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A box 40 by 30 with a circular hole of radius 5 in its middle has two loops, the circle first, a hole, then the box, and
    /// area 1 121.46 (1 200 less 78.54) and perimeter 171.42 (140 and 31.42). <see cref="Create"/> makes the shape of closed loops
    /// of lines, arcs, circles, ellipses and polylines with bulges; <see cref="DxfUnknownEntity.GetRegionShape"/> reads the
    /// shape of a REGION that AutoCAD or this library wrote, and <see cref="DxfUnknownEntity.CreateRegion"/> makes a REGION of
    /// one.
    /// </para>
    /// <para>
    /// A region of AutoCAD whose ACIS body holds a face that is not flat, or an edge that is a spline, has no shape here.
    /// </para>
    /// </remarks>
    public sealed class DxfRegionShape
    {
        private readonly List<DxfRegionLoop> _loops;

        internal DxfRegionShape(AcisBody body)
        {
            Body = body;
            _loops = new List<DxfRegionLoop>();

            foreach (AcisFace face in body.Faces)
            {
                foreach (AcisLoop loop in face.Loops)
                {
                    _loops.Add(new DxfRegionLoop(loop, face.Normal));
                }
            }
        }

        internal AcisBody Body { get; }

        /// <summary>Gets the loops of every face, the holes of a face before its outer loop.</summary>
        public IReadOnlyList<DxfRegionLoop> Loops => _loops.AsReadOnly();

        /// <summary>Gets the unit normal of the plane of the first face, as AutoCAD's <c>Region.Normal</c>: (0, 0, 1) for a region lying flat on the XY plane.</summary>
        public GeoVector3 Normal =>
            new GeoVector3(Body.Faces[0].Normal.X, Body.Faces[0].Normal.Y, Body.Faces[0].Normal.Z);

        /// <summary>Gets the area of the region: that of its outer loops less that of its holes, as AutoCAD's <c>Region.Area</c>.</summary>
        public double Area => Body.Area;

        /// <summary>Gets the perimeter of the region: the length of every loop, holes too, as AutoCAD's <c>Region.Perimeter</c>.</summary>
        public double Perimeter => Body.Perimeter;

        /// <summary>
        /// Makes the shape of one flat face from the curves of its outer loop and of each hole. The curves of a loop may come in
        /// any order and any way round, the lines and arcs of a loop joining end to end within the point tolerance; a circle,
        /// a whole ellipse or a closed polyline is a loop of itself. The outer loop is made to run counter-clockwise about the
        /// normal of its plane (+Z for a loop that lies flat on the XY plane) and a hole clockwise.
        /// </summary>
        /// <param name="outer">The curves of the outer loop: <see cref="DxfLine"/>, <see cref="DxfArc"/>, <see cref="DxfCircle"/>,
        /// <see cref="DxfEllipse"/>, <see cref="DxfPolyline"/> or <see cref="DxfPolyline2d"/>, the polylines with their bulges.</param>
        /// <param name="holes">The curves of each hole, or null for none. A hole is not checked to lie inside the outer loop.</param>
        /// <param name="tolerance">The tolerance: two ends within its point tolerance meet, and a point within it of the plane lies in it.</param>
        /// <returns>The shape.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the outer curves or a curve is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a curve is of a type that cannot bound a region (a spline, a ray, a
        /// polyline of one vertex), a loop does not close, or the loops do not lie in one plane.</exception>
        /// <example>
        /// A box 40 by 30 with a hole of radius 5: <c>DxfRegionShape.Create(new[] { box }, new[] { new[] { circle } },
        /// Tolerance.Default)</c> has area 1 121.46.
        /// </example>
        public static DxfRegionShape Create(
            IEnumerable<DxfCurve> outer,
            IEnumerable<IEnumerable<DxfCurve>> holes,
            Tolerance tolerance
        )
        {
            if (outer == null)
            {
                throw new ArgumentNullException(nameof(outer));
            }

            var holeCurves = new List<IReadOnlyList<AcisCurve>>();

            foreach (IEnumerable<DxfCurve> hole in holes ?? Enumerable.Empty<IEnumerable<DxfCurve>>())
            {
                holeCurves.Add(Curves(hole, nameof(holes)));
            }

            AcisBody body = AcisBody.Create(Curves(outer, nameof(outer)), holeCurves, tolerance.EqualPoint);
            return new DxfRegionShape(body);
        }

        private static IReadOnlyList<AcisCurve> Curves(IEnumerable<DxfCurve> curves, string name)
        {
            if (curves == null)
            {
                throw new ArgumentNullException(name);
            }

            var made = new List<AcisCurve>();

            foreach (DxfCurve curve in curves)
            {
                if (curve == null)
                {
                    throw new ArgumentNullException(name, "A curve of a loop is null.");
                }

                made.AddRange(AcisInput.From(curve));
            }

            return made;
        }

        /// <summary>The entity of a curve of a loop: a line, a whole circle or ellipse about the face's normal, an arc, or an elliptical arc.</summary>
        internal static DxfCurve ToEntity(AcisCurve curve, AcisVec faceNormal)
        {
            if (curve.IsLine)
            {
                return new DxfLine(Point(curve.Start), Point(curve.End));
            }

            bool circle = Math.Abs(curve.Ratio - 1.0) < 1E-12;

            if (curve.IsFull)
            {
                if (circle)
                {
                    return new DxfCircle(Point(curve.Center), Vector(faceNormal), curve.Major.Length);
                }

                // As AutoCAD's Explode gives it: the major axis of the curve, the parameter running from -pi to pi, so that the
                // ellipse starts where the loop does.
                var whole = new DxfEllipse(
                    Point(curve.Center),
                    Vector(faceNormal),
                    Vector(curve.Major),
                    curve.Ratio,
                    0.0,
                    2.0 * Math.PI
                );
                whole.StartParam = curve.T0;
                whole.EndParam = curve.T1;
                return whole;
            }

            if (circle)
            {
                AcisVec x = AcisBody.ArbitraryX(curve.Normal);
                AcisVec y = curve.Normal.Cross(x);
                double Angle(AcisVec p) => Math.Atan2((p - curve.Center).Dot(y), (p - curve.Center).Dot(x));
                return new DxfArc(
                    Point(curve.Center),
                    Vector(curve.Normal),
                    curve.Major.Length,
                    Angle(curve.Start),
                    Angle(curve.End)
                );
            }

            var ellipse = new DxfEllipse(
                Point(curve.Center),
                Vector(curve.Normal),
                Vector(curve.Major),
                curve.Ratio,
                0.0,
                2.0 * Math.PI
            );
            ellipse.StartParam = curve.T0;
            ellipse.EndParam = curve.T1;
            return ellipse;
        }

        private static GeoPoint3 Point(AcisVec v) => new GeoPoint3(v.X, v.Y, v.Z);

        private static GeoVector3 Vector(AcisVec v) => new GeoVector3(v.X, v.Y, v.Z);
    }
}
