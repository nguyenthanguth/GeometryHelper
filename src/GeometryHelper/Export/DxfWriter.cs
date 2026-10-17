using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using GeometryHelper.Internal;
using GeometryHelper.Meshing;

namespace GeometryHelper.Export
{
    /// <summary>
    /// Writes shapes as a DXF file of version R2000 (AC1015), which AutoCAD, Tekla Structures and every CAD viewer open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every shape added is drawn on a layer, named as given and made on first use in the colour AutoCAD calls white, with
    /// the continuous line; none given means layer 0. CAD colours by layer, so there is no colour to pass. The drawing is
    /// in millimetres. Numbers are written exactly, as a text that reads back to the same double on .NET Framework and on .NET 10
    /// alike (the digits may differ between the two). A shape with a number in it that is not finite (NaN or infinity) is refused with an
    /// <see cref="ArgumentException"/> before anything of it is written.
    /// </para>
    /// <para>
    /// In the plane a shape is written as the entity AutoCAD has for it, not as chords: a polygon, a rectangle, a triangle
    /// and each ring of a face as a closed lightweight polyline, a chain or loop with arcs as one too with the bulge of each
    /// arc edge, a circle as a CIRCLE, an arc as an ARC, an ellipse as an ELLIPSE and a ray as a RAY. A clockwise arc is
    /// written from its end to its start, since DXF arcs run counter-clockwise only. A face is its rings and not a filled
    /// area: <see cref="SvgWriter"/> fills.
    /// </para>
    /// <para>
    /// In space a segment, a point, a ray and an ellipse are written as they are, a chain and a polygon as a 3D polyline,
    /// and anything with a surface as 3DFACEs: one per triangle of a face or a body, one per face of a mesh that has three
    /// or four corners and turns one way, and six for a box, written from its corners. Where a face, a side of a body or a
    /// face of a mesh is cut into triangles, the edges between its own triangles are marked invisible, so a wireframe shows
    /// its outline and not the cut; triangles given as they are keep every edge.
    /// </para>
    /// <para>
    /// A curve in space (an arc, a circle, a chain or loop with arcs) is cut into chords no further from it than a chord
    /// tolerance the caller states, which has no default: a circle of radius 100 at 0.1 is 71 chords, one of radius 50 000
    /// at 0.001 is 15 708. A curve that would take more than 1 048 576 chords, counted over a whole chain or loop, is
    /// refused.
    /// </para>
    /// <code>
    /// new DxfWriter().Add(plate, "plate").Add(slot, "cut").Add(bar, 0.1, "bars").Save("part.dxf");
    /// </code>
    /// </remarks>
    public sealed class DxfWriter
    {
        // The tables and objects every file carries have handles of their own, below the 0x100 where those met while
        // adding shapes begin.
        private const int FirstHandle = 0x100;
        private const int BlockRecordTable = 0x1;
        private const int LayerTable = 0x2;
        private const int StyleTable = 0x3;
        private const int LinetypeTable = 0x5;
        private const int ViewTable = 0x6;
        private const int UcsTable = 0x7;
        private const int ViewportTable = 0x8;
        private const int AppIdTable = 0x9;
        private const int DimStyleTable = 0xA;
        private const int RootDictionary = 0xC;
        private const int GroupDictionary = 0xD;
        private const int LayoutDictionary = 0xE;
        private const int LayerZero = 0x10;
        private const int ByBlock = 0x14;
        private const int ByLayer = 0x15;
        private const int Continuous = 0x16;
        private const int ActiveViewport = 0x20;
        private const int StandardStyle = 0x21;
        private const int AcadAppId = 0x22;
        private const int StandardDimStyle = 0x23;
        private const int ModelSpace = 0x24;
        private const int PaperSpace = 0x25;
        private const int ModelBlock = 0x26;
        private const int ModelEnd = 0x27;
        private const int PaperBlock = 0x28;
        private const int PaperEnd = 0x29;
        private const int ModelLayout = 0x2A;
        private const int PaperLayout = 0x2B;
        private const int PlotStyleDictionary = 0x31;
        private const int PlotStylePlaceholder = 0x32;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        // The points of a unit circle at 0, 90, 180 and 270 degrees, exact.
        private static readonly double[] QuarterX = { 1.0, 0.0, -1.0, 0.0 };
        private static readonly double[] QuarterY = { 0.0, 1.0, 0.0, -1.0 };

        private readonly Groups _entities = new Groups();
        private readonly List<KeyValuePair<string, int>> _layers = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("0", LayerZero) };
        private readonly Dictionary<string, int> _layerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "0", 0 } };
        private int _nextHandle = FirstHandle;
        private double _minX = double.PositiveInfinity;
        private double _minY = double.PositiveInfinity;
        private double _minZ = double.PositiveInfinity;
        private double _maxX = double.NegativeInfinity;
        private double _maxY = double.NegativeInfinity;
        private double _maxZ = double.NegativeInfinity;

        #region The plane

        /// <summary>
        /// Adds a point.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="layer">The layer; layer 0 when none is given.</param>
        /// <returns>This writer, for adding the next shape.</returns>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPoint2 point, string layer = null)
        {
            Check(point, nameof(point));
            Begin("POINT", Layer(layer), "AcDbPoint");
            Place(0, point.X, point.Y, 0.0);
            return this;
        }

        /// <summary>
        /// Adds a segment.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoLine2 line, string layer = null)
        {
            Check(line.StartPoint, nameof(line));
            Check(line.EndPoint, nameof(line));
            Begin("LINE", Layer(layer), "AcDbLine");
            Place(0, line.StartPoint.X, line.StartPoint.Y, 0.0);
            Place(1, line.EndPoint.X, line.EndPoint.Y, 0.0);
            return this;
        }

        /// <summary>
        /// Adds a chain as an open lightweight polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPolyline2 polyline, string layer = null)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            Check(polyline.Vertices, nameof(polyline));
            return Lightweight(Layer(layer), polyline.Vertices, null, false);
        }

        /// <summary>
        /// Adds a polygon as a closed lightweight polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPolygon2 polygon, string layer = null)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Check(polygon.Vertices, nameof(polygon));
            return Lightweight(Layer(layer), polygon.Vertices, null, true);
        }

        /// <summary>
        /// Adds a rectangle as a closed lightweight polyline of its four corners.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoRectangle2 rectangle, string layer = null)
        {
            GeoPoint2[] corners = rectangle.GetVertices();
            Check(corners, nameof(rectangle));
            return Lightweight(Layer(layer), corners, null, true);
        }

        /// <summary>
        /// Adds a triangle as a closed lightweight polyline of its three corners.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoTriangle2 triangle, string layer = null)
        {
            GeoPoint2[] corners = { triangle.A, triangle.B, triangle.C };
            Check(corners, nameof(triangle));
            return Lightweight(Layer(layer), corners, null, true);
        }

        /// <summary>
        /// Adds a chain with arcs as an open lightweight polyline, each arc edge carrying its bulge.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate or a bulge is not a finite number.</exception>
        /// <remarks>
        /// The bulge is the tangent of a quarter of the angle the edge sweeps, positive counter-clockwise: the same number
        /// <see cref="GeoEdge2.Bulge"/> gives, so a semicircle is 1 and the quarter of a circle 0.4142.
        /// </remarks>
        public DxfWriter Add(GeoPolylineArc2 chain, string layer = null)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            Check(chain.Vertices, nameof(chain));
            Check(chain.Bulges, nameof(chain));
            return Lightweight(Layer(layer), chain.Vertices, chain.Bulges, false);
        }

        /// <summary>
        /// Adds a loop with arcs as a closed lightweight polyline, each arc edge carrying its bulge.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate or a bulge is not a finite number.</exception>
        public DxfWriter Add(GeoPolygonArc2 loop, string layer = null)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Check(loop.Vertices, nameof(loop));
            Check(loop.Bulges, nameof(loop));
            return Lightweight(Layer(layer), loop.Vertices, loop.Bulges, true);
        }

        /// <summary>
        /// Adds a face as a closed lightweight polyline for its boundary and one for each hole, all on the layer given and
        /// none filled.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoFace2 face, string layer = null)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            Check(face.Boundary.Vertices, nameof(face));

            foreach (GeoPolygon2 hole in face.Holes)
            {
                Check(hole.Vertices, nameof(face));
            }

            string name = Layer(layer);
            Lightweight(name, face.Boundary.Vertices, null, true);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                Lightweight(name, hole.Vertices, null, true);
            }

            return this;
        }

        /// <summary>
        /// Adds a circle.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate or the radius is not a finite number.</exception>
        public DxfWriter Add(GeoCircle2 circle, string layer = null)
        {
            Check(circle.Center, nameof(circle));
            Check(circle.Radius, nameof(circle));
            return Circle(Layer(layer), circle.Center.X, circle.Center.Y, circle.Radius);
        }

        /// <summary>
        /// Adds an arc, written counter-clockwise from its start angle to its end angle; a clockwise one from its end to its
        /// start, so that it covers the same points.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate, the radius or an angle is not a finite number.</exception>
        /// <remarks>
        /// An arc that turns a whole circle is written as a CIRCLE, since an ARC whose ends are one angle is nothing to
        /// AutoCAD.
        /// </remarks>
        public DxfWriter Add(GeoArc2 arc, string layer = null)
        {
            Check(arc.Center, nameof(arc));
            Check(arc.Radius, nameof(arc));
            Check(arc.StartAngle, nameof(arc));
            Check(arc.SweptAngle, nameof(arc));

            string name = Layer(layer);

            if (Math.Abs(arc.SweptAngle) >= Math.PI * 2.0)
            {
                return Circle(name, arc.Center.X, arc.Center.Y, arc.Radius);
            }

            Begin("ARC", name, "AcDbCircle");
            _entities.Point(0, arc.Center.X, arc.Center.Y, 0.0);
            _entities.Real(40, arc.Radius);
            _entities.Str(100, "AcDbArc");
            _entities.Real(50, Degrees(arc.IsClockwise ? arc.EndAngle : arc.StartAngle));
            _entities.Real(51, Degrees(arc.IsClockwise ? arc.StartAngle : arc.EndAngle));
            GrowArc(arc.Center, arc.Radius, arc.StartAngle, arc.SweptAngle);
            return this;
        }

        /// <summary>
        /// Adds an ellipse, whole, as an ELLIPSE with its centre, the end of its major axis, and the ratio of its minor radius
        /// to its major.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate or a radius is not a finite number.</exception>
        public DxfWriter Add(GeoEllipse2 ellipse, string layer = null)
        {
            Check(ellipse.Center, nameof(ellipse));
            Check(ellipse.MajorAxis, nameof(ellipse));
            Check(ellipse.MajorRadius, nameof(ellipse));
            Check(ellipse.MinorRadius, nameof(ellipse));

            Begin("ELLIPSE", Layer(layer), "AcDbEllipse");
            Place(0, ellipse.Center.X, ellipse.Center.Y, 0.0);
            _entities.Point(1, ellipse.MajorAxis.X * ellipse.MajorRadius, ellipse.MajorAxis.Y * ellipse.MajorRadius, 0.0);
            _entities.Triple(210, 0.0, 0.0, 1.0);
            Ratio(ellipse.MinorRadius / ellipse.MajorRadius);

            // The reach of the rim along an axis is the root of the squares of what the two radii each lend it.
            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double ux = ellipse.MajorAxis.X;
            double uy = ellipse.MajorAxis.Y;
            double reachX = Math.Sqrt(a * a * ux * ux + b * b * uy * uy);
            double reachY = Math.Sqrt(a * a * uy * uy + b * b * ux * ux);
            Grow(ellipse.Center.X - reachX, ellipse.Center.Y - reachY, 0.0);
            Grow(ellipse.Center.X + reachX, ellipse.Center.Y + reachY, 0.0);
            return this;
        }

        /// <summary>
        /// Adds a ray, which AutoCAD draws without end from its origin.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate or a direction is not a finite number.</exception>
        public DxfWriter Add(GeoRay2 ray, string layer = null)
        {
            Check(ray.Origin, nameof(ray));
            Check(ray.Direction, nameof(ray));

            Begin("RAY", Layer(layer), "AcDbRay");
            Place(0, ray.Origin.X, ray.Origin.Y, 0.0);
            _entities.Point(1, ray.Direction.X, ray.Direction.Y, 0.0);
            return this;
        }

        #endregion

        #region Space

        /// <summary>
        /// Adds a point in space.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPoint3 point, string layer = null)
        {
            Check(point, nameof(point));
            Begin("POINT", Layer(layer), "AcDbPoint");
            Place(0, point.X, point.Y, point.Z);
            return this;
        }

        /// <summary>
        /// Adds a segment in space.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoLine3 line, string layer = null)
        {
            Check(line.StartPoint, nameof(line));
            Check(line.EndPoint, nameof(line));
            Begin("LINE", Layer(layer), "AcDbLine");
            Place(0, line.StartPoint.X, line.StartPoint.Y, line.StartPoint.Z);
            Place(1, line.EndPoint.X, line.EndPoint.Y, line.EndPoint.Z);
            return this;
        }

        /// <summary>
        /// Adds a chain in space as an open 3D polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPolyline3 polyline, string layer = null)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            Check(polyline.Vertices, nameof(polyline));
            return Polyline3(Layer(layer), polyline.Vertices, false);
        }

        /// <summary>
        /// Adds a polygon in space as a closed 3D polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoPolygon3 polygon, string layer = null)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Check(polygon.Vertices, nameof(polygon));
            return Polyline3(Layer(layer), polygon.Vertices, true);
        }

        /// <summary>
        /// Adds a ray in space, which AutoCAD draws without end from its origin.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate or a direction is not a finite number.</exception>
        public DxfWriter Add(GeoRay3 ray, string layer = null)
        {
            Check(ray.Origin, nameof(ray));
            Check(ray.Direction, nameof(ray));

            Begin("RAY", Layer(layer), "AcDbRay");
            Place(0, ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
            _entities.Point(1, ray.Direction.X, ray.Direction.Y, ray.Direction.Z);
            return this;
        }

        /// <summary>
        /// Adds an ellipse in space, whole, as an ELLIPSE with its normal as the extrusion and its major axis in world
        /// coordinates.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate, a direction or a radius is not a finite number.</exception>
        public DxfWriter Add(GeoEllipse3 ellipse, string layer = null)
        {
            Check(ellipse.Center, nameof(ellipse));
            Check(ellipse.Normal, nameof(ellipse));
            Check(ellipse.MajorAxis, nameof(ellipse));
            Check(ellipse.MajorRadius, nameof(ellipse));
            Check(ellipse.MinorRadius, nameof(ellipse));

            Begin("ELLIPSE", Layer(layer), "AcDbEllipse");
            Place(0, ellipse.Center.X, ellipse.Center.Y, ellipse.Center.Z);
            GeoVector3 u = ellipse.MajorAxis;
            GeoVector3 v = ellipse.MinorAxis;
            _entities.Point(1, u.X * ellipse.MajorRadius, u.Y * ellipse.MajorRadius, u.Z * ellipse.MajorRadius);
            _entities.Triple(210, ellipse.Normal.X, ellipse.Normal.Y, ellipse.Normal.Z);
            Ratio(ellipse.MinorRadius / ellipse.MajorRadius);

            double a = ellipse.MajorRadius;
            double b = ellipse.MinorRadius;
            double reachX = Math.Sqrt(a * a * u.X * u.X + b * b * v.X * v.X);
            double reachY = Math.Sqrt(a * a * u.Y * u.Y + b * b * v.Y * v.Y);
            double reachZ = Math.Sqrt(a * a * u.Z * u.Z + b * b * v.Z * v.Z);
            Grow(ellipse.Center.X - reachX, ellipse.Center.Y - reachY, ellipse.Center.Z - reachZ);
            Grow(ellipse.Center.X + reachX, ellipse.Center.Y + reachY, ellipse.Center.Z + reachZ);
            return this;
        }

        /// <summary>
        /// Adds a triangle as a 3DFACE, every edge visible.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoTriangle3 triangle, string layer = null)
        {
            Check(triangle.A, nameof(triangle));
            Check(triangle.B, nameof(triangle));
            Check(triangle.C, nameof(triangle));
            Face(Layer(layer), triangle.A, triangle.B, triangle.C, triangle.C, 0);
            return this;
        }

        /// <summary>
        /// Adds a face as a 3DFACE for each triangle of its surface, holes left open and the edges between its triangles
        /// invisible.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <remarks>
        /// The triangles are cut at the global tolerance. <see cref="Add(GeoFace3, Tolerance, string)"/> states it.
        /// </remarks>
        public DxfWriter Add(GeoFace3 face, string layer = null) => Add(face, Tolerance.Global, layer);

        /// <summary>
        /// Adds a face as a 3DFACE for each triangle of its surface, cut within a tolerance, the edges between its triangles
        /// invisible.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoFace3 face, Tolerance tolerance, string layer = null)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Surfaces(new[] { face.TriangulateSurface(tolerance) }, layer);
        }

        /// <summary>
        /// Adds a body as a 3DFACE for each triangle of the surface where its material ends, openings cut in and the edges
        /// between the triangles of one side invisible. A box 100 by 100 by 20 with a hole 20 by 20 through it is 64
        /// triangles.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        /// <remarks>
        /// The triangles are cut at the global tolerance. <see cref="Add(GeoSolid3, Tolerance, string)"/> states it.
        /// </remarks>
        public DxfWriter Add(GeoSolid3 solid, string layer = null) => Add(solid, Tolerance.Global, layer);

        /// <summary>
        /// Adds a body as a 3DFACE for each triangle of its surface, cut within a tolerance, the edges between the triangles
        /// of one side invisible.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the body is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoSolid3 solid, Tolerance tolerance, string layer = null)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The sides of the material with the openings cut in, each cut into triangles as GeoSolid3.TriangulateSurface does.
            var sides = new List<GeoTriangle3[]>();

            if (Boolean3.TryCutOpenings(solid, out GeoSolid3 material, tolerance))
            {
                foreach (GeoFace3 side in material.Faces)
                {
                    sides.Add(side.TriangulateSurface(tolerance));
                }
            }

            return Surfaces(sides, layer);
        }

        /// <summary>
        /// Adds triangles, each as a 3DFACE with every edge visible.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the triangles are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(IEnumerable<GeoTriangle3> triangles, string layer = null)
        {
            if (triangles == null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            var all = new List<GeoTriangle3>(triangles);
            Check(all, nameof(triangles));

            string name = Layer(layer);

            foreach (GeoTriangle3 triangle in all)
            {
                Face(name, triangle.A, triangle.B, triangle.C, triangle.C, 0);
            }

            return this;
        }

        /// <summary>
        /// Adds a mesh of a flat shape: a face of three or four corners that turns right at none of them as one 3DFACE, so
        /// that the cells of a grid stay cells, and any other face as its triangles with the edges between them invisible.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the mesh is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoMesh3 mesh, string layer = null)
        {
            if (mesh == null)
            {
                throw new ArgumentNullException(nameof(mesh));
            }

            Check(mesh.Vertices, nameof(mesh));

            string name = Layer(layer);

            for (int f = 0; f < mesh.FaceCount; f++)
            {
                int[] corners = mesh.GetFaceIndices(f);

                if (corners.Length <= 4 && mesh.IsConvex(f))
                {
                    GeoPoint3 a = mesh.Vertices[corners[0]];
                    GeoPoint3 b = mesh.Vertices[corners[1]];
                    GeoPoint3 c = mesh.Vertices[corners[2]];
                    Face(name, a, b, c, corners.Length == 4 ? mesh.Vertices[corners[3]] : c, 0);
                    continue;
                }

                // A face with more corners, or one turning the wrong way at a corner, is drawn by the triangles that cover it
                // exactly; one 3DFACE would be drawn on the diagonal CAD chooses.
                Triangles(name, mesh.GetFaceTriangles(f));
            }

            return this;
        }

        /// <summary>
        /// Adds a box as six 3DFACEs of four corners, written from its corners with no tolerance, so a box as flat as
        /// 100 by 100 by 0.002, or by nothing, is written and not refused.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty, as <see cref="GeoAabb3.ToObb"/> says.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoAabb3 box, string layer = null)
        {
            if (box.IsEmpty)
            {
                throw new InvalidOperationException("An empty bounding box has no shape to write.");
            }

            GeoPoint3[] corners = box.GetCorners();
            Check(corners, nameof(box));
            return Box(Layer(layer), corners);
        }

        /// <summary>
        /// Adds an oriented box as six 3DFACEs of four corners, written from its corners with no tolerance, so a box as flat
        /// as 100 by 100 by 0.002, or by nothing, is written and not refused.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        public DxfWriter Add(GeoObb3 box, string layer = null)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3[] corners = box.GetCorners();
            Check(corners, nameof(box));
            return Box(Layer(layer), corners);
        }

        /// <summary>
        /// Adds an arc in space as an open 3D polyline of chords no further from the arc than a tolerance.
        /// </summary>
        /// <param name="arc">The arc.</param>
        /// <param name="chordTolerance">The largest gap allowed between a chord and the arc, in millimetres.</param>
        /// <param name="layer">The layer; layer 0 when none is given.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or the arc would take more than 1 048 576 chords at it.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate or the radius is not a finite number.</exception>
        public DxfWriter Add(GeoArc3 arc, double chordTolerance, string layer = null)
        {
            Chords.Require(chordTolerance);
            Check(arc.Center, nameof(arc));
            Check(arc.Normal, nameof(arc));
            Check(arc.Radius, nameof(arc));
            Check(arc.StartAngle, nameof(arc));
            Check(arc.SweptAngle, nameof(arc));

            List<GeoPoint3> points = Chords.Of(arc, chordTolerance);
            return Polyline3(Layer(layer), points, false);
        }

        /// <summary>
        /// Adds a circle in space as a closed 3D polyline of chords no further from the circle than a tolerance.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or the circle would take more than 1 048 576 chords at it.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate or the radius is not a finite number.</exception>
        public DxfWriter Add(GeoCircle3 circle, double chordTolerance, string layer = null)
        {
            Chords.Require(chordTolerance);
            Check(circle.Center, nameof(circle));
            Check(circle.Normal, nameof(circle));
            Check(circle.Radius, nameof(circle));

            // The chords come round to the first point again, which a closed polyline does not repeat.
            List<GeoPoint3> points = Chords.Of(circle, chordTolerance);
            points.RemoveAt(points.Count - 1);
            return Polyline3(Layer(layer), points, true);
        }

        /// <summary>
        /// Adds a chain with arcs in space as an open 3D polyline, its straight edges kept and each arc cut into chords no
        /// further from it than a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or the chain would take more than 1 048 576 chords at it
        /// in all.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate, a bulge or a direction is not a finite number.</exception>
        public DxfWriter Add(GeoPolylineArc3 chain, double chordTolerance, string layer = null)
        {
            if (chain == null)
            {
                throw new ArgumentNullException(nameof(chain));
            }

            Chords.Require(chordTolerance);
            Check(chain.Vertices, nameof(chain));
            Check(chain.Bulges, nameof(chain));
            Check(chain.Normals, nameof(chain));

            List<GeoPoint3> points = Chorded(chain.GetEdges(), chordTolerance, false);
            return Polyline3(Layer(layer), points, false);
        }

        /// <summary>
        /// Adds a loop with arcs in space as a closed 3D polyline, its straight edges kept and each arc cut into chords no
        /// further from it than a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when the chord tolerance is not a finite number above nought, or the loop would take more than 1 048 576 chords at it
        /// in all.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when a coordinate, a bulge or a direction is not a finite number.</exception>
        public DxfWriter Add(GeoPolygonArc3 loop, double chordTolerance, string layer = null)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            Chords.Require(chordTolerance);
            Check(loop.Vertices, nameof(loop));
            Check(loop.Bulges, nameof(loop));
            Check(loop.Normals, nameof(loop));

            List<GeoPoint3> points = Chorded(loop.GetEdges(), chordTolerance, true);
            return Polyline3(Layer(layer), points, true);
        }

        #endregion

        #region The file

        /// <summary>
        /// Gets the file as text.
        /// </summary>
        public override string ToString()
        {
            var file = new Groups();
            Header(file);
            Classes(file);
            Tables(file);
            Blocks(file);
            file.Str(0, "SECTION");
            file.Str(2, "ENTITIES");
            file.Append(_entities);
            file.Str(0, "ENDSEC");
            Objects(file);
            file.Str(0, "EOF");
            return file.ToString();
        }

        /// <summary>
        /// Writes the file to a writer.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the writer is null.</exception>
        public void WriteTo(TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            writer.Write(ToString());
        }

        /// <summary>
        /// Writes the file to a path, replacing whatever is there.
        /// </summary>
        /// <remarks>
        /// The file is plain ASCII, with no byte order mark: names beyond ASCII are written as DXF escapes, which is how
        /// AutoCAD reads them.
        /// </remarks>
        public void Save(string path) => File.WriteAllText(path, ToString(), new UTF8Encoding(false));

        private void Header(Groups file)
        {
            bool any = !double.IsInfinity(_minX);

            file.Str(0, "SECTION");
            file.Str(2, "HEADER");
            file.Str(9, "$ACADVER");
            file.Str(1, "AC1015");
            file.Str(9, "$HANDSEED");
            file.Hex(5, _nextHandle);
            file.Str(9, "$INSUNITS");
            file.Int(70, 4);
            file.Str(9, "$EXTMIN");
            file.Point(0, any ? _minX : 1E20, any ? _minY : 1E20, any ? _minZ : 1E20);
            file.Str(9, "$EXTMAX");
            file.Point(0, any ? _maxX : -1E20, any ? _maxY : -1E20, any ? _maxZ : -1E20);
            file.Str(0, "ENDSEC");
        }

        /// <summary>
        /// The classes of the objects below that are not plain DICTIONARYs, as AutoCAD lists them for R2000.
        /// </summary>
        private static void Classes(Groups file)
        {
            file.Str(0, "SECTION");
            file.Str(2, "CLASSES");
            Class(file, "ACDBDICTIONARYWDFLT", "AcDbDictionaryWithDefault");
            Class(file, "ACDBPLACEHOLDER", "AcDbPlaceHolder");
            Class(file, "LAYOUT", "AcDbLayout");
            file.Str(0, "ENDSEC");
        }

        private static void Class(Groups file, string name, string cppName)
        {
            file.Str(0, "CLASS");
            file.Str(1, name);
            file.Str(2, cppName);
            file.Str(3, "ObjectDBX Classes");
            file.Int(90, 0);
            file.Int(280, 0);
            file.Int(281, 0);
        }

        private void Tables(Groups file)
        {
            bool any = !double.IsInfinity(_minX);
            double width = any ? _maxX - _minX : 0.0;
            double height = any ? _maxY - _minY : 0.0;

            file.Str(0, "SECTION");
            file.Str(2, "TABLES");

            Table(file, "VPORT", ViewportTable, 1);
            Record(file, "VPORT", ActiveViewport, ViewportTable, "AcDbViewportTableRecord", "*Active");
            file.Int(70, 0);
            file.Real(10, 0.0);
            file.Real(20, 0.0);
            file.Real(11, 1.0);
            file.Real(21, 1.0);

            // Opened in plan, centred on what was drawn, so that the drawing is in view before the user zooms.
            file.Real(12, any ? (_minX + _maxX) / 2.0 : 0.0);
            file.Real(22, any ? (_minY + _maxY) / 2.0 : 0.0);
            file.Real(13, 0.0);
            file.Real(23, 0.0);
            file.Real(14, 10.0);
            file.Real(24, 10.0);
            file.Real(15, 10.0);
            file.Real(25, 10.0);
            file.Point(6, 0.0, 0.0, 1.0);
            file.Point(7, 0.0, 0.0, 0.0);
            file.Real(40, Math.Max(width, height) > 0.0 ? Math.Max(width, height) * 1.1 : 10.0);
            file.Real(41, 1.0);
            file.Real(42, 50.0);
            file.Real(43, 0.0);
            file.Real(44, 0.0);
            file.Real(50, 0.0);
            file.Real(51, 0.0);
            file.Int(71, 0);
            file.Int(72, 100);
            file.Int(73, 1);
            file.Int(74, 3);
            file.Int(75, 0);
            file.Int(76, 0);
            file.Int(77, 0);
            file.Int(78, 0);
            file.Str(0, "ENDTAB");

            Table(file, "LTYPE", LinetypeTable, 3);
            Linetype(file, ByBlock, "ByBlock", string.Empty);
            Linetype(file, ByLayer, "ByLayer", string.Empty);
            Linetype(file, Continuous, "Continuous", "Solid line");
            file.Str(0, "ENDTAB");

            Table(file, "LAYER", LayerTable, _layers.Count);

            foreach (KeyValuePair<string, int> layer in _layers)
            {
                Record(file, "LAYER", layer.Value, LayerTable, "AcDbLayerTableRecord", layer.Key);
                file.Int(70, 0);
                file.Int(62, 7);
                file.Str(6, "Continuous");
                file.Hex(390, PlotStylePlaceholder);
            }

            file.Str(0, "ENDTAB");

            Table(file, "STYLE", StyleTable, 1);
            Record(file, "STYLE", StandardStyle, StyleTable, "AcDbTextStyleTableRecord", "Standard");
            file.Int(70, 0);
            file.Real(40, 0.0);
            file.Real(41, 1.0);
            file.Real(50, 0.0);
            file.Int(71, 0);
            file.Real(42, 2.5);
            file.Str(3, "txt");
            file.Str(4, string.Empty);
            file.Str(0, "ENDTAB");

            Table(file, "VIEW", ViewTable, 0);
            file.Str(0, "ENDTAB");

            Table(file, "UCS", UcsTable, 0);
            file.Str(0, "ENDTAB");

            Table(file, "APPID", AppIdTable, 1);
            Record(file, "APPID", AcadAppId, AppIdTable, "AcDbRegAppTableRecord", "ACAD");
            file.Int(70, 0);
            file.Str(0, "ENDTAB");

            Table(file, "DIMSTYLE", DimStyleTable, 1);
            file.Str(100, "AcDbDimStyleTable");
            file.Int(71, 1);
            file.Str(0, "DIMSTYLE");
            file.Hex(105, StandardDimStyle);
            file.Hex(330, DimStyleTable);
            file.Str(100, "AcDbSymbolTableRecord");
            file.Str(100, "AcDbDimStyleTableRecord");
            file.Str(2, "Standard");
            file.Int(70, 0);
            file.Str(0, "ENDTAB");

            Table(file, "BLOCK_RECORD", BlockRecordTable, 2);
            Record(file, "BLOCK_RECORD", ModelSpace, BlockRecordTable, "AcDbBlockTableRecord", "*Model_Space");
            file.Hex(340, ModelLayout);
            Record(file, "BLOCK_RECORD", PaperSpace, BlockRecordTable, "AcDbBlockTableRecord", "*Paper_Space");
            file.Hex(340, PaperLayout);
            file.Str(0, "ENDTAB");

            file.Str(0, "ENDSEC");
        }

        private static void Table(Groups file, string name, int handle, int entries)
        {
            file.Str(0, "TABLE");
            file.Str(2, name);
            file.Hex(5, handle);
            file.Hex(330, 0);
            file.Str(100, "AcDbSymbolTable");
            file.Int(70, entries);
        }

        private static void Record(Groups file, string type, int handle, int table, string subclass, string name)
        {
            file.Str(0, type);
            file.Hex(5, handle);
            file.Hex(330, table);
            file.Str(100, "AcDbSymbolTableRecord");
            file.Str(100, subclass);
            file.Str(2, name);
        }

        private static void Linetype(Groups file, int handle, string name, string description)
        {
            Record(file, "LTYPE", handle, LinetypeTable, "AcDbLinetypeTableRecord", name);
            file.Int(70, 0);
            file.Str(3, description);
            file.Int(72, 65);
            file.Int(73, 0);
            file.Real(40, 0.0);
        }

        private static void Blocks(Groups file)
        {
            file.Str(0, "SECTION");
            file.Str(2, "BLOCKS");
            Block(file, ModelBlock, ModelEnd, ModelSpace, "*Model_Space", false);
            Block(file, PaperBlock, PaperEnd, PaperSpace, "*Paper_Space", true);
            file.Str(0, "ENDSEC");
        }

        private static void Block(Groups file, int begin, int end, int record, string name, bool paper)
        {
            file.Str(0, "BLOCK");
            file.Hex(5, begin);
            file.Hex(330, record);
            file.Str(100, "AcDbEntity");

            if (paper)
            {
                file.Int(67, 1);
            }

            file.Str(8, "0");
            file.Str(100, "AcDbBlockBegin");
            file.Str(2, name);
            file.Int(70, 0);
            file.Point(0, 0.0, 0.0, 0.0);
            file.Str(3, name);
            file.Str(1, string.Empty);
            file.Str(0, "ENDBLK");
            file.Hex(5, end);
            file.Hex(330, record);
            file.Str(100, "AcDbEntity");

            if (paper)
            {
                file.Int(67, 1);
            }

            file.Str(8, "0");
            file.Str(100, "AcDbBlockEnd");
        }

        private static void Objects(Groups file)
        {
            file.Str(0, "SECTION");
            file.Str(2, "OBJECTS");

            // The root names the three dictionaries the objects below belong to.
            Dictionary(file, RootDictionary, 0);
            RootEntry(file, "ACAD_GROUP", GroupDictionary);
            RootEntry(file, "ACAD_LAYOUT", LayoutDictionary);
            RootEntry(file, "ACAD_PLOTSTYLENAME", PlotStyleDictionary);
            Dictionary(file, GroupDictionary, RootDictionary);

            // The plot style names: one, Normal, which is a placeholder, and every layer points at it.
            file.Str(0, "ACDBDICTIONARYWDFLT");
            file.Hex(5, PlotStyleDictionary);
            file.Hex(330, RootDictionary);
            file.Str(100, "AcDbDictionary");
            file.Int(281, 1);
            file.Str(3, "Normal");
            file.Hex(350, PlotStylePlaceholder);
            file.Str(100, "AcDbDictionaryWithDefault");
            file.Hex(340, PlotStylePlaceholder);
            file.Str(0, "ACDBPLACEHOLDER");
            file.Hex(5, PlotStylePlaceholder);
            file.Hex(330, PlotStyleDictionary);

            Dictionary(file, LayoutDictionary, RootDictionary);
            file.Str(3, "Layout1");
            file.Hex(350, PaperLayout);
            file.Str(3, "Model");
            file.Hex(350, ModelLayout);

            Layout(file, ModelLayout, "Model", 0, ModelSpace, 1024);
            Layout(file, PaperLayout, "Layout1", 1, PaperSpace, 688);

            file.Str(0, "ENDSEC");
        }

        private static void RootEntry(Groups file, string name, int handle)
        {
            file.Str(3, name);
            file.Hex(350, handle);
        }

        private static void Dictionary(Groups file, int handle, int owner)
        {
            file.Str(0, "DICTIONARY");
            file.Hex(5, handle);
            file.Hex(330, owner);
            file.Str(100, "AcDbDictionary");
            file.Int(281, 1);
        }

        /// <summary>
        /// A layout with the page set-up left at nothing, which is all AutoCAD needs of one to show the model and a sheet.
        /// </summary>
        private static void Layout(Groups file, int handle, string name, int tab, int record, int plotFlags)
        {
            file.Str(0, "LAYOUT");
            file.Hex(5, handle);
            file.Hex(330, LayoutDictionary);
            file.Str(100, "AcDbPlotSettings");
            file.Str(1, string.Empty);
            file.Str(2, "none_device");
            file.Str(4, string.Empty);
            file.Str(6, string.Empty);

            foreach (int code in new[] { 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 140, 141 })
            {
                file.Real(code, 0.0);
            }

            file.Real(142, 1.0);
            file.Real(143, 1.0);
            file.Int(70, plotFlags);
            file.Int(72, 0);
            file.Int(73, 0);
            file.Int(74, 0);
            file.Str(7, string.Empty);
            file.Int(75, 16);
            file.Real(147, 1.0);
            file.Real(148, 0.0);
            file.Real(149, 0.0);
            file.Str(100, "AcDbLayout");
            file.Str(1, name);
            file.Int(70, 1);
            file.Int(71, tab);
            file.Real(10, 0.0);
            file.Real(20, 0.0);
            file.Real(11, 12.0);
            file.Real(21, 9.0);
            file.Point(2, 0.0, 0.0, 0.0);
            file.Point(4, 1E20, 1E20, 1E20);
            file.Point(5, -1E20, -1E20, -1E20);
            file.Real(146, 0.0);
            file.Point(3, 0.0, 0.0, 0.0);
            file.Point(6, 1.0, 0.0, 0.0);
            file.Point(7, 0.0, 1.0, 0.0);
            file.Int(76, 0);
            file.Hex(330, record);
        }

        #endregion

        #region Entities

        /// <summary>
        /// Opens an entity: its type, a handle, its owner (the model space unless said otherwise), its layer, and the
        /// subclass that holds its own groups (none for the end of a polyline). Gives the handle back.
        /// </summary>
        private int Begin(string type, string layer, string subclass, int owner = ModelSpace)
        {
            int handle = _nextHandle++;

            _entities.Str(0, type);
            _entities.Hex(5, handle);
            _entities.Hex(330, owner);
            _entities.Str(100, "AcDbEntity");
            _entities.Str(8, layer);

            if (subclass != null)
            {
                _entities.Str(100, subclass);
            }

            return handle;
        }

        /// <summary>
        /// Writes a point in the groups of its number (0 for 10, 20, 30; 1 for 11, 21, 31) and grows the drawing's extents by it.
        /// </summary>
        private void Place(int n, double x, double y, double z)
        {
            _entities.Point(n, x, y, z);
            Grow(x, y, z);
        }

        private void Grow(double x, double y, double z)
        {
            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _minZ = Math.Min(_minZ, z);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
            _maxZ = Math.Max(_maxZ, z);
        }

        /// <summary>
        /// Grows the extents by an arc, by its two ends and by those of the four points a circle reaches furthest that it
        /// covers.
        /// </summary>
        private void GrowArc(GeoPoint2 centre, double radius, double startAngle, double sweptAngle)
        {
            double from = sweptAngle >= 0.0 ? startAngle : startAngle + sweptAngle;
            double span = Math.Abs(sweptAngle);

            Grow(centre.X + radius * Math.Cos(from), centre.Y + radius * Math.Sin(from), 0.0);
            Grow(centre.X + radius * Math.Cos(from + span), centre.Y + radius * Math.Sin(from + span), 0.0);

            for (int quarter = 0; quarter < 4; quarter++)
            {
                double offset = (quarter * Math.PI / 2.0 - from) % (Math.PI * 2.0);

                if (offset < 0.0)
                {
                    offset += Math.PI * 2.0;
                }

                if (offset <= span)
                {
                    Grow(centre.X + radius * QuarterX[quarter], centre.Y + radius * QuarterY[quarter], 0.0);
                }
            }
        }

        /// <summary>
        /// Grows the extents by the bow of an edge, which its ends do not hold. An edge that bows less than its own arc is
        /// known to, as <see cref="Arc2.IsFlat"/> judges it, is read by its chord, which is its ends: its circle is too far
        /// off to be placed.
        /// </summary>
        private void GrowEdge(GeoPoint2 start, GeoPoint2 end, double bulge)
        {
            GeoArc2 arc;

            try
            {
                arc = new GeoEdge2(start, end, bulge).ToArc();
            }
            catch (ArgumentException)
            {
                // No arc to place: the ends are all there is to grow by, and they are grown already.
                return;
            }

            double sag = Math.Abs(bulge) * start.DistanceTo(end) * 0.5;

            bool placed = Guard.IsFinite(arc.Center.X) && Guard.IsFinite(arc.Center.Y) && Guard.IsFinite(arc.Radius);

            if (!placed || (Math.Abs(bulge) <= 1.0 && Arc2.IsFlat(sag, arc.Center, arc.Radius)))
            {
                return;
            }

            GrowArc(arc.Center, arc.Radius, arc.StartAngle, arc.SweptAngle);
        }

        private DxfWriter Circle(string layer, double x, double y, double radius)
        {
            Begin("CIRCLE", layer, "AcDbCircle");
            Place(0, x, y, 0.0);
            _entities.Real(40, radius);
            Grow(x - radius, y - radius, 0.0);
            Grow(x + radius, y + radius, 0.0);
            return this;
        }

        /// <summary>
        /// The last of an ELLIPSE's groups: the ratio of its radii and the parameters it runs between, nought to a whole turn.
        /// </summary>
        private void Ratio(double ratio)
        {
            _entities.Real(40, ratio);
            _entities.Real(41, 0.0);
            _entities.Real(42, Math.PI * 2.0);
        }

        /// <summary>
        /// A lightweight polyline. The bulge of each edge sits on the vertex the edge leaves, which for the closing edge of a
        /// loop is the last.
        /// </summary>
        private DxfWriter Lightweight(string layer, IReadOnlyList<GeoPoint2> vertices, IReadOnlyList<double> bulges, bool closed)
        {
            Begin("LWPOLYLINE", layer, "AcDbPolyline");
            _entities.Int(90, vertices.Count);
            _entities.Int(70, closed ? 1 : 0);

            for (int i = 0; i < vertices.Count; i++)
            {
                _entities.Real(10, vertices[i].X);
                _entities.Real(20, vertices[i].Y);
                Grow(vertices[i].X, vertices[i].Y, 0.0);

                double bulge = bulges != null && (closed || i < vertices.Count - 1) ? bulges[i] : 0.0;

                if (bulge != 0.0)
                {
                    _entities.Real(42, bulge);
                    GrowEdge(vertices[i], vertices[(i + 1) % vertices.Count], bulge);
                }
            }

            return this;
        }

        /// <summary>
        /// A 3D polyline: the polyline entity, a vertex for each point, and the end that closes the list. The vertices and the
        /// end belong to the polyline.
        /// </summary>
        private DxfWriter Polyline3(string layer, IReadOnlyList<GeoPoint3> vertices, bool closed)
        {
            int polyline = Begin("POLYLINE", layer, "AcDb3dPolyline");
            _entities.Int(66, 1);
            _entities.Point(0, 0.0, 0.0, 0.0);
            _entities.Int(70, closed ? 9 : 8);

            foreach (GeoPoint3 vertex in vertices)
            {
                Begin("VERTEX", layer, "AcDbVertex", polyline);
                _entities.Str(100, "AcDb3dPolylineVertex");
                Place(0, vertex.X, vertex.Y, vertex.Z);
                _entities.Int(70, 32);
            }

            Begin("SEQEND", layer, null, polyline);
            return this;
        }

        /// <summary>
        /// A 3DFACE. The flags hide edges: 1 for the first, 2, 4 and 8 for the next three; a triangle repeats its third corner,
        /// so its third edge is the fourth.
        /// </summary>
        private void Face(string layer, GeoPoint3 a, GeoPoint3 b, GeoPoint3 c, GeoPoint3 d, int hidden)
        {
            Begin("3DFACE", layer, "AcDbFace");
            Place(0, a.X, a.Y, a.Z);
            Place(1, b.X, b.Y, b.Z);
            Place(2, c.X, c.Y, c.Z);
            Place(3, d.X, d.Y, d.Z);

            if (hidden != 0)
            {
                _entities.Int(70, hidden);
            }
        }

        /// <summary>
        /// The triangles of one side, each a 3DFACE, with the edge hidden that two of them share.
        /// </summary>
        private void Triangles(string layer, IReadOnlyList<GeoTriangle3> side)
        {
            var seen = new Dictionary<Seam, int>();

            foreach (GeoTriangle3 t in side)
            {
                Count(seen, t.A, t.B);
                Count(seen, t.B, t.C);
                Count(seen, t.C, t.A);
            }

            foreach (GeoTriangle3 t in side)
            {
                int hidden = (seen[new Seam(t.A, t.B)] > 1 ? 1 : 0) | (seen[new Seam(t.B, t.C)] > 1 ? 2 : 0) | (seen[new Seam(t.C, t.A)] > 1 ? 8 : 0);
                Face(layer, t.A, t.B, t.C, t.C, hidden);
            }
        }

        private static void Count(Dictionary<Seam, int> seen, GeoPoint3 from, GeoPoint3 to)
        {
            var seam = new Seam(from, to);
            seen.TryGetValue(seam, out int times);
            seen[seam] = times + 1;
        }

        /// <summary>
        /// The sides of a surface, each cut into triangles, checked whole before any is written.
        /// </summary>
        private DxfWriter Surfaces(IReadOnlyList<GeoTriangle3[]> sides, string layer)
        {
            foreach (GeoTriangle3[] side in sides)
            {
                Check(side, nameof(sides));
            }

            string name = Layer(layer);

            foreach (GeoTriangle3[] side in sides)
            {
                Triangles(name, side);
            }

            return this;
        }

        /// <summary>
        /// Six faces of four corners, outwards, from the eight corners in the order of GeoAabb3.GetCorners: the four of the
        /// bottom counter-clockwise seen from above, then the four of the top above them.
        /// </summary>
        private DxfWriter Box(string layer, GeoPoint3[] c)
        {
            if (c.Length == 8)
            {
                Face(layer, c[0], c[3], c[2], c[1], 0);
                Face(layer, c[4], c[5], c[6], c[7], 0);
                Face(layer, c[0], c[1], c[5], c[4], 0);
                Face(layer, c[2], c[3], c[7], c[6], 0);
                Face(layer, c[1], c[2], c[6], c[5], 0);
                Face(layer, c[3], c[0], c[4], c[7], 0);
            }

            return this;
        }

        /// <summary>
        /// The points of a chain of edges with each arc cut into chords, the straight edges kept as they are. A loop's last
        /// point is its first, so it is left off. The chords of each arc are made, and counted into the chain's total, before
        /// the next arc's; a total past the limit is refused when it is reached, and nothing has been written by then.
        /// </summary>
        private static List<GeoPoint3> Chorded(IEnumerable<GeoEdge3> edges, double chordTolerance, bool closed)
        {
            var list = new List<GeoEdge3>(edges);
            var points = new List<GeoPoint3> { list[0].StartPoint };
            long total = 0;

            for (int i = 0; i < list.Count; i++)
            {
                GeoArc3 arc = list[i].IsArc ? list[i].ToArc() : default(GeoArc3);

                // An arc too shallow to place a circle for is read by its chord, as a straight edge is.
                if (!list[i].IsArc || !Guard.IsFinite(arc.Radius) || !Guard.IsFinite(arc.Center.X) || !Guard.IsFinite(arc.Center.Y) || !Guard.IsFinite(arc.Center.Z))
                {
                    points.Add(list[i].EndPoint);
                    continue;
                }

                List<GeoPoint3> along = Chords.Of(arc, chordTolerance);
                Chords.Add(ref total, along.Count - 1, chordTolerance);

                for (int j = 1; j < along.Count - 1; j++)
                {
                    points.Add(along[j]);
                }

                points.Add(list[i].EndPoint);
            }

            if (closed)
            {
                points.RemoveAt(points.Count - 1);
            }

            return points;
        }

        /// <summary>
        /// Prints a double: the one place every number of the file is made, so that how numbers are printed changes here
        /// alone. It is a text that reads back to exactly that double on either runtime, the shortest of 15, 16 and 17 digits that does.
        /// </summary>
        private static string Exact(double value) => Numbers.Exact(value);

        private static double Degrees(double radians) => radians * 180.0 / Math.PI;

        #endregion

        #region Numbers that must be finite

        private static void Check(double value, string name)
            => Guard.Finite(value, name, "A coordinate, size or direction has to be a finite number.");

        private static void Check(GeoPoint2 point, string name)
        {
            Check(point.X, name);
            Check(point.Y, name);
        }

        private static void Check(GeoPoint3 point, string name)
        {
            Check(point.X, name);
            Check(point.Y, name);
            Check(point.Z, name);
        }

        private static void Check(GeoVector2 vector, string name)
        {
            Check(vector.X, name);
            Check(vector.Y, name);
        }

        private static void Check(GeoVector3 vector, string name)
        {
            Check(vector.X, name);
            Check(vector.Y, name);
            Check(vector.Z, name);
        }

        private static void Check(IReadOnlyList<GeoPoint2> points, string name)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Check(points[i], name);
            }
        }

        private static void Check(IReadOnlyList<GeoPoint3> points, string name)
        {
            for (int i = 0; i < points.Count; i++)
            {
                Check(points[i], name);
            }
        }

        private static void Check(IReadOnlyList<double> numbers, string name)
        {
            for (int i = 0; i < numbers.Count; i++)
            {
                Check(numbers[i], name);
            }
        }

        private static void Check(IReadOnlyList<GeoVector3> vectors, string name)
        {
            for (int i = 0; i < vectors.Count; i++)
            {
                Check(vectors[i], name);
            }
        }

        private static void Check(IReadOnlyList<GeoTriangle3> triangles, string name)
        {
            for (int i = 0; i < triangles.Count; i++)
            {
                Check(triangles[i].A, name);
                Check(triangles[i].B, name);
                Check(triangles[i].C, name);
            }
        }

        #endregion

        #region Layers

        /// <summary>
        /// Gets the name a layer is written under, making the layer on first use. Layers are told apart by the name as given,
        /// cut and cleaned, with letters of any case the same, accented ones too, and the first spelling is kept.
        /// </summary>
        private string Layer(string layer)
        {
            string key = LayerKey(layer);

            if (_layerIndex.TryGetValue(key, out int index))
            {
                return _layers[index].Key;
            }

            string name = Escaped(key);
            _layerIndex.Add(key, _layers.Count);
            _layers.Add(new KeyValuePair<string, int>(name, _nextHandle++));
            return name;
        }

        /// <summary>
        /// A layer name cut to the 255 characters DXF allows before it is escaped, with the characters DXF refuses replaced.
        /// </summary>
        private static string LayerKey(string layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
            {
                return "0";
            }

            string trimmed = layer.Trim();
            int length = Math.Min(trimmed.Length, 255);

            // Not between the two halves of a letter written as a pair.
            if (length < trimmed.Length && char.IsHighSurrogate(trimmed[length - 1]))
            {
                length--;
            }

            // The cut can leave a space at the end, which a name may not have.
            string cut = trimmed.Substring(0, length).TrimEnd();
            var key = new StringBuilder(cut.Length);

            for (int i = 0; i < cut.Length; i++)
            {
                char c = cut[i];
                key.Append(c < 32 || "<>/\\\":;?*|=`,".IndexOf(c) >= 0 ? '_' : c);
            }

            return key.ToString();
        }

        /// <summary>
        /// A name with its letters beyond ASCII as the escapes R2000 reads: \U+00E9 for an e acute.
        /// </summary>
        private static string Escaped(string key)
        {
            var name = new StringBuilder(key.Length);

            foreach (char c in key)
            {
                if (c > 126)
                {
                    name.Append("\\U+").Append(((int)c).ToString("X4", Invariant));
                }
                else
                {
                    name.Append(c);
                }
            }

            return name.ToString();
        }

        #endregion

        /// <summary>
        /// A point that is the same as another only when every coordinate is the same to the bit, nought and minus nought alike.
        /// </summary>
        private readonly struct Corner : IEquatable<Corner>, IComparable<Corner>
        {
            private readonly double _x;
            private readonly double _y;
            private readonly double _z;

            public Corner(GeoPoint3 point)
            {
                _x = point.X + 0.0;
                _y = point.Y + 0.0;
                _z = point.Z + 0.0;
            }

            public bool Equals(Corner other)
                => BitConverter.DoubleToInt64Bits(_x) == BitConverter.DoubleToInt64Bits(other._x)
                && BitConverter.DoubleToInt64Bits(_y) == BitConverter.DoubleToInt64Bits(other._y)
                && BitConverter.DoubleToInt64Bits(_z) == BitConverter.DoubleToInt64Bits(other._z);

            public override bool Equals(object obj) => obj is Corner other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    long hash = BitConverter.DoubleToInt64Bits(_x);
                    hash = hash * 31 + BitConverter.DoubleToInt64Bits(_y);
                    hash = hash * 31 + BitConverter.DoubleToInt64Bits(_z);
                    return (int)(hash ^ (hash >> 32));
                }
            }

            public int CompareTo(Corner other)
            {
                int x = _x.CompareTo(other._x);

                if (x != 0)
                {
                    return x;
                }

                int y = _y.CompareTo(other._y);
                return y != 0 ? y : _z.CompareTo(other._z);
            }
        }

        /// <summary>
        /// An edge between two corners, the same whichever way round it is met.
        /// </summary>
        private readonly struct Seam : IEquatable<Seam>
        {
            private readonly Corner _first;
            private readonly Corner _second;

            public Seam(GeoPoint3 from, GeoPoint3 to)
            {
                var a = new Corner(from);
                var b = new Corner(to);

                _first = a.CompareTo(b) <= 0 ? a : b;
                _second = a.CompareTo(b) <= 0 ? b : a;
            }

            public bool Equals(Seam other) => _first.Equals(other._first) && _second.Equals(other._second);

            public override bool Equals(object obj) => obj is Seam other && Equals(other);

            public override int GetHashCode() => unchecked(_first.GetHashCode() * 397 ^ _second.GetHashCode());
        }

        /// <summary>
        /// The pairs of lines a DXF file is made of: a group code, then its value.
        /// </summary>
        private sealed class Groups
        {
            private readonly StringBuilder _text = new StringBuilder();

            public void Str(int code, string value)
                => _text.Append(code.ToString(Invariant).PadLeft(3)).Append('\n').Append(value).Append('\n');

            public void Int(int code, int value) => Str(code, value.ToString(Invariant));

            /// <summary>
            /// Writes a number as <see cref="Exact"/> prints it. Plus nought turns a negative nought into a plain one.
            /// </summary>
            public void Real(int code, double value) => Str(code, Exact(value + 0.0));

            public void Hex(int code, int handle) => Str(code, handle.ToString("X", Invariant));

            /// <summary>
            /// Writes a point in the groups of its number: 0 for 10, 20, 30, 1 for 11, 21, 31, and so on.
            /// </summary>
            public void Point(int n, double x, double y, double z) => Triple(10 + n, x, y, z);

            /// <summary>
            /// Writes three numbers in the groups from a first code up by tens: 210, 220, 230 for an extrusion.
            /// </summary>
            public void Triple(int code, double x, double y, double z)
            {
                Real(code, x);
                Real(code + 10, y);
                Real(code + 20, z);
            }

            public void Append(Groups other) => _text.Append(other._text);

            public override string ToString() => _text.ToString();
        }
    }
}
