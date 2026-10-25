using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A loop of a HATCH, as AutoCAD's <c>HatchLoop</c>: what it is (<see cref="LoopType"/>) and its shape, in the world, as the
    /// library's shapes: a loop of a polyline as one <see cref="GeoPolygonArc3"/>, a loop of edges as its lines, arcs, ellipses
    /// and splines one after another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DxfHatch.GetLoopAt(int)"/> gives one, and <see cref="DxfHatch.AppendLoop(DxfHatchLoop)"/> takes one. A square
    /// 30 by 20 on the floor is <c>new DxfHatchLoop(DxfHatchLoopTypes.External, new GeoPolygonArc3(corners))</c>; a circle of
    /// radius 10, <c>new DxfHatchLoop(DxfHatchLoopTypes.External, new object[] { new GeoCircle3(centre, up, 10) })</c>.
    /// </para>
    /// <para>
    /// A loop read from a HATCH keeps its groups as the file has them, so that one appended to a HATCH in the same plane is
    /// written as it was read, an ellipse edge an ellipse edge and each number as it was; one made in code, or appended to a
    /// HATCH in another plane, is written from its shapes. A part of an ellipse is given as the <see cref="GeoSpline3"/> that is
    /// exactly that part, as <see cref="DxfReader"/> gives an ELLIPSE that is not whole.
    /// </para>
    /// </remarks>
    public sealed class DxfHatchLoop
    {
        private static readonly IReadOnlyList<object> NoCurves = Array.AsReadOnly(new object[0]);

        /// <summary>
        /// Initializes a loop of a polyline, as AutoCAD's <c>HatchLoop</c> of the type <see cref="DxfHatchLoopTypes.Polyline"/>:
        /// its vertices with their bulges, in the world, closed.
        /// </summary>
        /// <param name="loopType">What the loop is; <see cref="DxfHatchLoopTypes.Polyline"/> is added to it.</param>
        /// <param name="polyline">The loop.</param>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public DxfHatchLoop(DxfHatchLoopTypes loopType, GeoPolygonArc3 polyline)
            : this(loopType | DxfHatchLoopTypes.Polyline, (object)(polyline ?? throw new ArgumentNullException(nameof(polyline))), NoCurves)
        {
        }

        /// <summary>
        /// Initializes a loop of a polyline that may be open, as AutoCAD's <c>HatchLoop</c> of the type
        /// <see cref="DxfHatchLoopTypes.Polyline"/>: closed where its last vertex is on its first, within
        /// <see cref="Tolerance.Default"/>, and written open otherwise, as AutoCAD keeps one it cannot close.
        /// </summary>
        /// <param name="loopType">What the loop is; <see cref="DxfHatchLoopTypes.Polyline"/> is added to it.</param>
        /// <param name="polyline">The chain.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public DxfHatchLoop(DxfHatchLoopTypes loopType, GeoPolylineArc3 polyline)
            : this(loopType | DxfHatchLoopTypes.Polyline, (object)(polyline ?? throw new ArgumentNullException(nameof(polyline))), NoCurves)
        {
        }

        /// <summary>
        /// Initializes a loop of edges, as AutoCAD's <c>HatchLoop</c> with its <c>Curves</c>: in the world, one after another,
        /// each a <see cref="GeoLine3"/>, <see cref="GeoArc3"/>, <see cref="GeoCircle3"/>, <see cref="GeoEllipse3"/>,
        /// <see cref="GeoSpline3"/> or <see cref="GeoEdge3"/>; a <see cref="GeoPolylineArc3"/> or <see cref="GeoPolygonArc3"/>
        /// among them gives its segments.
        /// </summary>
        /// <param name="loopType">What the loop is; <see cref="DxfHatchLoopTypes.Polyline"/> is taken out of it.</param>
        /// <param name="curves">The edges, at least one.</param>
        /// <exception cref="ArgumentNullException">Thrown when the edges are null, or one of them is.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no edge, or an edge is a shape a loop of a HATCH has none of.</exception>
        public DxfHatchLoop(DxfHatchLoopTypes loopType, IEnumerable<object> curves)
            : this(loopType & ~DxfHatchLoopTypes.Polyline, null, Checked(curves))
        {
        }

        private DxfHatchLoop(DxfHatchLoopTypes loopType, object polyline, IReadOnlyList<object> curves)
        {
            LoopType = loopType;
            Polyline = polyline;
            Curves = curves;
        }

        /// <summary>A loop read from a HATCH: its shapes, and its groups as the file has them in the plane they were read in.</summary>
        internal DxfHatchLoop(DxfHatchLoopTypes loopType, object polyline, IReadOnlyList<object> curves, List<KeyValuePair<int, string>> groups, GeoVector3 normal, double elevation)
            : this(loopType, polyline, curves)
        {
            Groups = groups;
            Normal = normal;
            Elevation = elevation;
        }

        /// <summary>Gets what the loop is, as AutoCAD's <c>LoopType</c>: the flags of its group 92.</summary>
        public DxfHatchLoopTypes LoopType { get; }

        /// <summary>Gets whether the loop is one of a polyline, as AutoCAD's <c>IsPolyline</c>: <see cref="Polyline"/> is its shape.</summary>
        public bool IsPolyline => Polyline != null;

        /// <summary>
        /// Gets the shape of a loop of a polyline, as AutoCAD's <c>Polyline</c>: a <see cref="GeoPolygonArc3"/> for a closed one;
        /// a <see cref="GeoPolylineArc3"/> for one that is open, or so thin it closes no area the library takes, its first vertex
        /// again at its end where it is closed. Null for a loop of edges.
        /// </summary>
        public object Polyline { get; }

        /// <summary>Gets the edges of a loop of edges, as AutoCAD's <c>Curves</c>, in their order; none for a loop of a polyline.</summary>
        public IReadOnlyList<object> Curves { get; }

        /// <summary>The groups of the loop as the file has them, from its 92; null for one made in code.</summary>
        internal List<KeyValuePair<int, string>> Groups { get; }

        /// <summary>The normal of the HATCH the groups were read in.</summary>
        internal GeoVector3 Normal { get; }

        /// <summary>The elevation of the HATCH the groups were read in.</summary>
        internal double Elevation { get; }

        /// <summary>The edges given, checked: each a shape a loop of edges has.</summary>
        private static IReadOnlyList<object> Checked(IEnumerable<object> curves)
        {
            if (curves == null)
            {
                throw new ArgumentNullException(nameof(curves));
            }

            var list = new List<object>();

            foreach (object curve in curves)
            {
                switch (curve)
                {
                    case null:
                        throw new ArgumentNullException(nameof(curves), "An edge of a loop is null.");
                    case GeoLine3 _:
                    case GeoArc3 _:
                    case GeoCircle3 _:
                    case GeoEllipse3 _:
                    case GeoSpline3 _:
                    case GeoEdge3 _:
                    case GeoPolylineArc3 _:
                    case GeoPolygonArc3 _:
                        list.Add(curve);
                        break;
                    default:
                        throw new ArgumentException("An edge of a loop of a HATCH is a line, an arc, a circle, an ellipse, a spline or a chain of lines and arcs: a " + curve.GetType().Name + " is none of them.", nameof(curves));
                }
            }

            if (list.Count == 0)
            {
                throw new ArgumentException("A loop of edges has at least one edge.", nameof(curves));
            }

            return list.AsReadOnly();
        }
    }
}
