using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>The edges of a loop made from the entities of a drawing.</summary>
    internal static class AcisInput
    {
        /// <summary>
        /// The edges of an entity, in the order it runs: a line one, an arc one, a circle or a whole ellipse one closed edge, a
        /// polyline one for each segment.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown for an entity that cannot be an edge of a flat loop.</exception>
        internal static List<AcisCurve> From(DxfCurve curve)
        {
            var edges = new List<AcisCurve>();

            switch (curve)
            {
                case DxfLine line:
                    edges.Add(AcisCurve.Line(Vec(line.StartPoint), Vec(line.EndPoint)));
                    break;
                case DxfArc arc:
                    edges.Add(AcisCurve.Arc(Vec(arc.Center), Vec(arc.Normal), Vec(arc.StartPoint), arc.TotalAngle));
                    break;
                case DxfCircle circle:
                    edges.Add(
                        AcisCurve.Whole(
                            Vec(circle.Center),
                            Vec(circle.Normal),
                            Vec(circle.GetPointAtParameter(0.0)) - Vec(circle.Center),
                            1.0
                        )
                    );
                    break;
                case DxfEllipse ellipse when ellipse.Closed:
                    edges.Add(
                        AcisCurve.Whole(
                            Vec(ellipse.Center),
                            Vec(ellipse.Normal),
                            Vec(ellipse.MajorAxis),
                            ellipse.RadiusRatio
                        )
                    );
                    break;
                case DxfEllipse ellipse:
                    edges.Add(
                        AcisCurve.Ellipse(
                            Vec(ellipse.Center),
                            Vec(ellipse.Normal),
                            Vec(ellipse.MajorAxis),
                            ellipse.RadiusRatio,
                            ellipse.StartParam,
                            ellipse.EndParam
                        )
                    );
                    break;
                case DxfPolyline polyline:
                    Segments(polyline, edges);
                    break;
                case DxfPolyline2d old:
                    var converted = new DxfPolyline();
                    converted.ConvertFrom(old, false);
                    Segments(converted, edges);
                    break;
                default:
                    throw new ArgumentException(
                        "A "
                            + curve.GetType().Name
                            + " cannot bound a region: a region is made of lines, arcs, circles, ellipses and polylines."
                    );
            }

            return edges;
        }

        private static void Segments(DxfPolyline polyline, List<AcisCurve> edges)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                switch (polyline.GetSegmentType(i))
                {
                    case DxfSegmentType.Line:
                        GeoLine3 line = polyline.GetLineSegmentAt(i);
                        edges.Add(AcisCurve.Line(Vec(line.StartPoint), Vec(line.EndPoint)));
                        break;
                    case DxfSegmentType.Arc:
                        GeoArc3 arc = polyline.GetArcSegmentAt(i);
                        edges.Add(AcisCurve.Arc(Vec(arc.Center), Vec(arc.Normal), Vec(arc.StartPoint), arc.SweptAngle));
                        break;
                    case DxfSegmentType.Point:
                        throw new ArgumentException("A polyline of one vertex cannot bound a region.");
                }
            }
        }

        private static AcisVec Vec(GeoPoint3 p) => new AcisVec(p.X, p.Y, p.Z);

        private static AcisVec Vec(GeoVector3 v) => new AcisVec(v.X, v.Y, v.Z);
    }
}
