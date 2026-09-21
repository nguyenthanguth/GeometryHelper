using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Carries flat geometry between the plane and space: a shape lying in a plane in space is laid out in
    /// two dimensions, worked on there, and put back.
    /// <para>
    /// Everything goes through a <see cref="GeoCoordinateSystem3"/>, the frame the flattening is measured in.
    /// Hold on to it: the same frame has to be used to put the answer back, or it lands somewhere else. A
    /// frame built from a shape by <see cref="FrameOf(GeoPolygon3)"/> turns with the shape, so the local
    /// coordinates of a plate are the same whichever way the plate is oriented in the model.
    /// </para>
    /// <para>
    /// Flattening drops the local Z, so a point off the plane is a question rather than an answer. The
    /// <c>Try</c> forms refuse one farther from the plane than <see cref="Tolerance.EqualPlanar"/>; the plain
    /// forms project it onto the plane and say so in their name.
    /// </para>
    /// </summary>
    public static class PlanarMap
    {
        #region Frames

        /// <summary>
        /// Gets the frame of a polygon: its plane, with the origin at the first vertex and the first axis
        /// along the first edge that has a direction, so the frame turns with the polygon.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <returns>The frame, with its Z axis along the polygon normal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoCoordinateSystem3 FrameOf(GeoPolygon3 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return FrameOf(polygon.Vertices, polygon.Normal);
        }

        /// <summary>
        /// Gets the frame of a face: the frame of its boundary, so its holes are laid out with it.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <returns>The frame, with its Z axis along the face normal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoCoordinateSystem3 FrameOf(GeoFace3 face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            return FrameOf(face.Boundary);
        }

        /// <summary>
        /// Gets a frame of a plane, with the origin at the plane origin and the axes the plane itself fixes.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <returns>The frame, with its Z axis along the plane normal.</returns>
        /// <remarks>
        /// A plane does not say which way its first axis runs, so this frame does not turn with any shape
        /// lying in it. It is what to use when several shapes have to share one set of local coordinates.
        /// </remarks>
        public static GeoCoordinateSystem3 FrameOf(GeoPlane3 plane) => new GeoCoordinateSystem3(plane);

        /// <summary>
        /// Gets the frame of a run of points that share a plane, with the origin at the first of them.
        /// </summary>
        private static GeoCoordinateSystem3 FrameOf(IReadOnlyList<GeoPoint3> points, GeoVector3 normal)
        {
            GeoPoint3 origin = points[0];

            for (int i = 1; i < points.Count; i++)
            {
                GeoVector3 along = origin.GetVectorTo(points[i]);

                if (along.TryGetNormal(out GeoVector3 unit))
                {
                    return new GeoCoordinateSystem3(origin, unit, normal.CrossProduct(unit));
                }
            }

            // Every point sits on the first one; no shape this library builds can be in that state, but the
            // plane of the normal still gives a frame rather than a failure.
            return new GeoCoordinateSystem3(new GeoPlane3(origin, normal));
        }

        #endregion

        #region Points and vectors

        /// <summary>
        /// Lays a point out in a frame, dropping its distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="point">The point.</param>
        /// <returns>The point in the plane of the frame.</returns>
        public static GeoPoint2 ProjectToPoint2(GeoCoordinateSystem3 frame, GeoPoint3 point)
        {
            GeoPoint3 local = frame.ToLocal(point);

            return new GeoPoint2(local.X, local.Y);
        }

        /// <summary>
        /// Lays a point out in a frame when it lies on the plane of that frame, using the default tolerance.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="point">The point.</param>
        /// <param name="result">The point in the plane, or the origin when the method returns false.</param>
        /// <returns>true if the point lies on the plane of the frame; otherwise, false.</returns>
        public static bool TryToPoint2(GeoCoordinateSystem3 frame, GeoPoint3 point, out GeoPoint2 result)
        {
            return TryToPoint2(frame, point, out result, Tolerance.Global);
        }

        /// <summary>
        /// Lays a point out in a frame when it lies on the plane of that frame, within a tolerance.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="point">The point.</param>
        /// <param name="result">The point in the plane, or the origin when the method returns false.</param>
        /// <param name="tolerance">The tolerance: <see cref="Tolerance.EqualPlanar"/> decides how far off the plane still counts as on it.</param>
        /// <returns>true if the point lies on the plane of the frame; otherwise, false.</returns>
        public static bool TryToPoint2(GeoCoordinateSystem3 frame, GeoPoint3 point, out GeoPoint2 result, Tolerance tolerance)
        {
            GeoPoint3 local = frame.ToLocal(point);

            if (Math.Abs(local.Z) > tolerance.EqualPlanar)
            {
                result = new GeoPoint2(0.0, 0.0);
                return false;
            }

            result = new GeoPoint2(local.X, local.Y);
            return true;
        }

        /// <summary>
        /// Puts a point of the plane back into space.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="point">The point in the plane.</param>
        /// <returns>The point in space, on the plane of the frame.</returns>
        public static GeoPoint3 ToPoint3(GeoCoordinateSystem3 frame, GeoPoint2 point)
        {
            return frame.ToGlobal(new GeoPoint3(point.X, point.Y, 0.0));
        }

        /// <summary>
        /// Lays a vector out in a frame, dropping the part square to the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="vector">The vector.</param>
        /// <returns>The vector in the plane of the frame.</returns>
        public static GeoVector2 ProjectToVector2(GeoCoordinateSystem3 frame, GeoVector3 vector)
        {
            GeoVector3 local = frame.ToLocal(vector);

            return new GeoVector2(local.X, local.Y);
        }

        /// <summary>
        /// Puts a vector of the plane back into space.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="vector">The vector in the plane.</param>
        /// <returns>The vector in space, parallel to the plane of the frame.</returns>
        public static GeoVector3 ToVector3(GeoCoordinateSystem3 frame, GeoVector2 vector)
        {
            return frame.ToGlobal(new GeoVector3(vector.X, vector.Y, 0.0));
        }

        #endregion

        #region Shapes

        /// <summary>
        /// Lays a segment out in a frame, dropping each end's distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="line">The segment.</param>
        /// <returns>The segment in the plane of the frame.</returns>
        public static GeoLine2 ProjectToLine2(GeoCoordinateSystem3 frame, GeoLine3 line)
        {
            return new GeoLine2(ProjectToPoint2(frame, line.StartPoint), ProjectToPoint2(frame, line.EndPoint));
        }

        /// <summary>
        /// Puts a segment of the plane back into space.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="line">The segment in the plane.</param>
        /// <returns>The segment in space, on the plane of the frame.</returns>
        public static GeoLine3 ToLine3(GeoCoordinateSystem3 frame, GeoLine2 line)
        {
            return new GeoLine3(ToPoint3(frame, line.StartPoint), ToPoint3(frame, line.EndPoint));
        }

        /// <summary>
        /// Lays a chain out in a frame, dropping each vertex's distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="polyline">The chain.</param>
        /// <returns>The chain in the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolyline2 ProjectToPolyline2(GeoCoordinateSystem3 frame, GeoPolyline3 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return new GeoPolyline2(Flatten(frame, polyline.Vertices));
        }

        /// <summary>
        /// Puts a chain of the plane back into space.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="polyline">The chain in the plane.</param>
        /// <returns>The chain in space, on the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPolyline3 ToPolyline3(GeoCoordinateSystem3 frame, GeoPolyline2 polyline)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return new GeoPolyline3(Lift(frame, polyline.Vertices));
        }

        /// <summary>
        /// Lays a polygon out in a frame, dropping each vertex's distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="polygon">The polygon.</param>
        /// <returns>
        /// The polygon in the plane of the frame. It runs counter-clockwise when the frame Z axis agrees with
        /// the polygon normal, and clockwise when it opposes it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPolygon2 ProjectToPolygon2(GeoCoordinateSystem3 frame, GeoPolygon3 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return new GeoPolygon2(Flatten(frame, polygon.Vertices));
        }

        /// <summary>
        /// Puts a polygon of the plane back into space.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="polygon">The polygon in the plane.</param>
        /// <returns>The polygon in space, on the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the polygon encloses no area, which no plane of space can carry.</exception>
        public static GeoPolygon3 ToPolygon3(GeoCoordinateSystem3 frame, GeoPolygon2 polygon)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return new GeoPolygon3(Lift(frame, polygon.Vertices));
        }

        /// <summary>
        /// Lays a face and its holes out in a frame, dropping each vertex's distance from the plane.
        /// </summary>
        /// <param name="frame">The frame to lay it out in.</param>
        /// <param name="face">The face.</param>
        /// <returns>The face in the plane of the frame, holes and all.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoFace2 ProjectToFace2(GeoCoordinateSystem3 frame, GeoFace3 face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            var holes = new List<GeoPolygon2>(face.Holes.Count);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                holes.Add(ProjectToPolygon2(frame, hole));
            }

            return new GeoFace2(ProjectToPolygon2(frame, face.Boundary), holes);
        }

        /// <summary>
        /// Puts a face of the plane back into space, holes and all.
        /// </summary>
        /// <param name="frame">The frame it was laid out in.</param>
        /// <param name="face">The face in the plane.</param>
        /// <returns>The face in space, on the plane of the frame.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoFace3 ToFace3(GeoCoordinateSystem3 frame, GeoFace2 face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            var holes = new List<GeoPolygon3>(face.Holes.Count);

            foreach (GeoPolygon2 hole in face.Holes)
            {
                holes.Add(ToPolygon3(frame, hole));
            }

            return new GeoFace3(ToPolygon3(frame, face.Boundary), holes);
        }

        #endregion

        #region Helpers

        private static List<GeoPoint2> Flatten(GeoCoordinateSystem3 frame, IReadOnlyList<GeoPoint3> points)
        {
            var flat = new List<GeoPoint2>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                flat.Add(ProjectToPoint2(frame, points[i]));
            }

            return flat;
        }

        private static List<GeoPoint3> Lift(GeoCoordinateSystem3 frame, IReadOnlyList<GeoPoint2> points)
        {
            var lifted = new List<GeoPoint3>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                lifted.Add(ToPoint3(frame, points[i]));
            }

            return lifted;
        }

        #endregion
    }
}
