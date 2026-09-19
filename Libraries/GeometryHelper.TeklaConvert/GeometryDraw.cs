using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Draws geometry into the Tekla model view as control polycurves, to see where a result lies.
    /// <para>
    /// A control polycurve is a temporary line of the model view rather than a part, and like everything the Tekla
    /// API is given it is placed in the current work plane. Geometry read in that same plane — <c>Part.GetSolid()</c>,
    /// or the <c>ToGeoSolids</c> of <see cref="ReferenceModelObjectConvert"/> and <see cref="ReferenceModelConvert"/> —
    /// is therefore drawn where it came from. A face is drawn with its outer boundary in red and its holes in white
    /// unless other colours are given, so that a hole stands out from the edge it is cut in.
    /// </para>
    /// <para>
    /// The view shows what was drawn after <c>Model.CommitChanges()</c>. These methods leave that to the caller, so
    /// that drawing a thousand faces costs one commit rather than a thousand. Each returns the polycurves it
    /// inserted, which <c>Delete()</c> removes again. A polyline with fewer than two distinct points, or one Tekla
    /// will not insert, is skipped rather than thrown on; a polyline Tekla refuses is written to
    /// <see cref="GeometryHelperLog"/>.
    /// </para>
    /// </summary>
    public static class GeometryDraw
    {
        // Consecutive points closer than this, in millimetres, are one point: a segment of no length is not a curve.
        private const double SamePointDistance = 1e-6;

        /// <summary>
        /// Draws a polyline through Tekla points as a control polycurve.
        /// </summary>
        /// <param name="points">The points, in the current work plane.</param>
        /// <param name="closed">true to draw the edge from the last point back to the first as well.</param>
        /// <param name="color">The colour of the polycurve.</param>
        /// <param name="lineType">The line type of the polycurve.</param>
        /// <returns>The inserted polycurve, or null when there was nothing to draw or Tekla did not insert it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static ControlPolycurve DrawPolyline(
            this IEnumerable<TSG.Point> points,
            bool closed = false,
            ControlObjectColorEnum color = ControlObjectColorEnum.RED,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            TSG.Polycurve geometry = ToPolycurve(points, closed);
            if (geometry == null)
            {
                return null;
            }

            try
            {
                ControlPolycurve polycurve = new ControlPolycurve(geometry)
                {
                    Color = color,
                    LineType = lineType
                };

                if (polycurve.Insert())
                {
                    return polycurve;
                }

                GeometryHelperLog.Warn("Tekla did not insert a control polycurve; it is left out.");
                return null;
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("A control polycurve could not be inserted; it is left out.", exception);
                return null;
            }
        }

        /// <summary>
        /// Draws a polyline through GeometryHelper points as a control polycurve.
        /// </summary>
        /// <param name="points">The points, in the current work plane.</param>
        /// <param name="closed">true to draw the edge from the last point back to the first as well.</param>
        /// <param name="color">The colour of the polycurve.</param>
        /// <param name="lineType">The line type of the polycurve.</param>
        /// <returns>The inserted polycurve, or null when there was nothing to draw or Tekla did not insert it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="points"/> is null.</exception>
        public static ControlPolycurve DrawPolyline(
            this IEnumerable<GeoPoint3> points,
            bool closed = false,
            ControlObjectColorEnum color = ControlObjectColorEnum.RED,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            return DrawPolyline(points.ToTeklaPoint(), closed, color, lineType);
        }

        /// <summary>
        /// Draws an open polyline as a control polycurve.
        /// </summary>
        /// <param name="polyline">The polyline, in the current work plane.</param>
        /// <param name="color">The colour of the polycurve.</param>
        /// <param name="lineType">The line type of the polycurve.</param>
        /// <returns>The inserted polycurve, or null when there was nothing to draw or Tekla did not insert it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polyline"/> is null.</exception>
        public static ControlPolycurve DrawToTekla(
            this GeoPolyline3 polyline,
            ControlObjectColorEnum color = ControlObjectColorEnum.RED,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return DrawPolyline(polyline.Vertices, false, color, lineType);
        }

        /// <summary>
        /// Draws a polygon as a closed control polycurve.
        /// </summary>
        /// <param name="polygon">The polygon, in the current work plane.</param>
        /// <param name="color">The colour of the polycurve.</param>
        /// <param name="lineType">The line type of the polycurve.</param>
        /// <returns>The inserted polycurve, or null when there was nothing to draw or Tekla did not insert it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="polygon"/> is null.</exception>
        public static ControlPolycurve DrawToTekla(
            this GeoPolygon3 polygon,
            ControlObjectColorEnum color = ControlObjectColorEnum.RED,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return DrawPolyline(polygon.Vertices, true, color, lineType);
        }

        /// <summary>
        /// Draws a face as closed control polycurves: its outer boundary in one colour and every hole in another.
        /// </summary>
        /// <param name="face">The face, in the current work plane.</param>
        /// <param name="boundaryColor">The colour of the outer boundary.</param>
        /// <param name="holeColor">The colour of the holes.</param>
        /// <param name="lineType">The line type of the polycurves.</param>
        /// <returns>The inserted polycurves, the boundary first.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="face"/> is null.</exception>
        public static ControlPolycurve[] DrawToTekla(
            this GeoFace3 face,
            ControlObjectColorEnum boundaryColor = ControlObjectColorEnum.RED,
            ControlObjectColorEnum holeColor = ControlObjectColorEnum.WHITE,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return FaceRings(face, boundaryColor, holeColor)
                .Select(ring => ring.Ring.DrawToTekla(ring.Color, lineType))
                .Where(polycurve => polycurve != null)
                .ToArray();
        }

        /// <summary>
        /// Draws every face of a body as closed control polycurves: outer boundaries in one colour, holes in another.
        /// </summary>
        /// <param name="solid">The body, in the current work plane.</param>
        /// <param name="boundaryColor">The colour of the outer boundaries.</param>
        /// <param name="holeColor">The colour of the holes.</param>
        /// <param name="lineType">The line type of the polycurves.</param>
        /// <returns>The inserted polycurves.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solid"/> is null.</exception>
        /// <remarks>A curved surface arrives triangulated, so a round tube draws hundreds of polycurves.</remarks>
        public static ControlPolycurve[] DrawToTekla(
            this GeoSolid3 solid,
            ControlObjectColorEnum boundaryColor = ControlObjectColorEnum.RED,
            ControlObjectColorEnum holeColor = ControlObjectColorEnum.WHITE,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return solid.Faces.SelectMany(face => face.DrawToTekla(boundaryColor, holeColor, lineType)).ToArray();
        }

        /// <summary>
        /// Draws every face of several bodies as closed control polycurves: outer boundaries in one colour, holes in
        /// another.
        /// </summary>
        /// <param name="solids">The bodies, in the current work plane. Null entries are skipped.</param>
        /// <param name="boundaryColor">The colour of the outer boundaries.</param>
        /// <param name="holeColor">The colour of the holes.</param>
        /// <param name="lineType">The line type of the polycurves.</param>
        /// <returns>The inserted polycurves.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="solids"/> is null.</exception>
        public static ControlPolycurve[] DrawToTekla(
            this IEnumerable<GeoSolid3> solids,
            ControlObjectColorEnum boundaryColor = ControlObjectColorEnum.RED,
            ControlObjectColorEnum holeColor = ControlObjectColorEnum.WHITE,
            ControlObjectLineType lineType = ControlObjectLineType.SolidLine)
        {
            if (solids == null)
            {
                throw new ArgumentNullException(nameof(solids));
            }

            return solids
                .Where(solid => solid != null)
                .SelectMany(solid => solid.DrawToTekla(boundaryColor, holeColor, lineType))
                .ToArray();
        }

        /// <summary>
        /// The rings of a face in the order they are drawn, each with its colour: the outer boundary, then the holes.
        /// </summary>
        internal static IEnumerable<(GeoPolygon3 Ring, ControlObjectColorEnum Color)> FaceRings(
            GeoFace3 face, ControlObjectColorEnum boundaryColor, ControlObjectColorEnum holeColor)
        {
            yield return (face.Boundary, boundaryColor);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                yield return (hole, holeColor);
            }
        }

        /// <summary>
        /// The Tekla curve a polyline is drawn with, or null when there is nothing to draw.
        /// </summary>
        internal static TSG.Polycurve ToPolycurve(IEnumerable<TSG.Point> points, bool closed)
        {
            List<TSG.Point> outline = Outline(points, closed);
            if (outline == null)
            {
                return null;
            }

            try
            {
                return new TSG.Polycurve(new TSG.PolyLine(outline));
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn($"Tekla could not build a polycurve through {outline.Count} points; it is left out.", exception);
                return null;
            }
        }

        /// <summary>
        /// The points a polyline is drawn through: repeats and null points left out, and for a closed ring of at least
        /// three points the first point repeated at the end, since a PolyLine is open and would otherwise not draw the
        /// closing edge. Null when fewer than two distinct points are left.
        /// </summary>
        internal static List<TSG.Point> Outline(IEnumerable<TSG.Point> points, bool closed)
        {
            List<TSG.Point> outline = new List<TSG.Point>();

            foreach (TSG.Point point in points)
            {
                if (point != null && (outline.Count == 0 || !IsSamePoint(outline[outline.Count - 1], point)))
                {
                    outline.Add(new TSG.Point(point.X, point.Y, point.Z));
                }
            }

            // A ring given with its first point repeated at the end is the same ring.
            if (closed && outline.Count > 1 && IsSamePoint(outline[0], outline[outline.Count - 1]))
            {
                outline.RemoveAt(outline.Count - 1);
            }

            if (outline.Count < 2)
            {
                return null;
            }

            if (closed && outline.Count >= 3)
            {
                outline.Add(new TSG.Point(outline[0].X, outline[0].Y, outline[0].Z));
            }

            return outline;
        }

        private static bool IsSamePoint(TSG.Point a, TSG.Point b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            double dz = a.Z - b.Z;

            return dx * dx + dy * dy + dz * dz <= SamePointDistance * SamePointDistance;
        }
    }
}
