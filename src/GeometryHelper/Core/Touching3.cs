using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Puts the faces of one body that lie parallel to the faces of another, within a contact distance, onto those faces'
    /// planes; see <see cref="SolidBooleanOptions.Contact"/>.
    /// </summary>
    internal static class Touching3
    {
        /// <summary>
        /// Gets the second body with each face that lies parallel to a face of the first, either way round, every corner of
        /// it within the contact distance of that face's plane and the boxes of the two meeting, put onto the nearest such
        /// plane, unless it lies on one within the tolerance already; the second body as it is where no face is moved, or
        /// where moving them would leave it open.
        /// </summary>
        /// <param name="first">The body whose faces stay where they are.</param>
        /// <param name="second">The body whose faces are moved.</param>
        /// <param name="contact">How far a face is moved at most.</param>
        /// <param name="tolerance">The tolerance: its angle decides what is parallel, and the faces are built again within it.</param>
        internal static GeoSolid3 PutOnto(GeoSolid3 first, GeoSolid3 second, double contact, Tolerance tolerance)
        {
            if (!(contact > 0.0) || first.Faces.Count == 0 || second.Faces.Count == 0)
            {
                return second;
            }

            var reach = new Tolerance(contact, contact, tolerance.EqualAngleRad, contact);
            GeoAabb3 firstBox = first.GetAabb();

            if (!firstBox.CollidesWith(second.GetAabb(), reach))
            {
                return second;
            }

            double parallel = Math.Cos(tolerance.EqualAngleRad);

            // How near a plane a face lies on it, as the boolean reads it.
            double on = Boolean3.ForWork(tolerance).EqualPlanar;
            var targets = new List<(GeoFace3 Face, GeoPlane3 Plane, GeoAabb3 Box)>(first.Faces.Count);

            foreach (GeoFace3 face in first.Faces)
            {
                targets.Add((face, face.GetPlane(), face.GetAabb()));
            }

            // Where each corner of the second body is moved to; a corner several faces share moves with all of them.
            var moved = new Dictionary<GeoPoint3, GeoPoint3>();
            var corners = new List<GeoPoint3>();

            foreach (GeoFace3 face in second.Faces)
            {
                GeoAabb3 box = face.GetAabb();

                if (!box.CollidesWith(firstBox, reach))
                {
                    continue;
                }

                corners.Clear();
                corners.AddRange(face.Boundary.Vertices);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    corners.AddRange(hole.Vertices);
                }

                int nearest = -1;
                double nearestOff = double.MaxValue;
                bool lies = false;

                for (int t = 0; t < targets.Count; t++)
                {
                    if (Math.Abs(targets[t].Face.Normal.DotProduct(face.Normal)) < parallel || !targets[t].Box.CollidesWith(box, reach))
                    {
                        continue;
                    }

                    // Every corner within the contact, and not every one on the plane already.
                    double furthest = 0.0;

                    foreach (GeoPoint3 corner in corners)
                    {
                        furthest = Math.Max(furthest, Math.Abs(targets[t].Plane.SignedDistanceTo(At(moved, corner))));

                        if (furthest > contact)
                        {
                            break;
                        }
                    }

                    // A face lying on a plane of the first body already, within the tolerance, is where it should be,
                    // whatever else is near: the boolean takes it as lying there. Put onto it exactly, it changes nothing
                    // the boolean reads but how the planes of the two lie on each other: a tool's face 0.0001 off the
                    // plane of a slab's face out of flat, read as triangles each a few ten-thousandths off it, was put onto
                    // it, and the plane, crossing the slab elsewhere, could not cut it; the difference took the cell whole,
                    // 18 litres beyond the tool.
                    if (furthest <= on)
                    {
                        lies = true;
                        break;
                    }

                    if (furthest > contact || furthest >= nearestOff)
                    {
                        continue;
                    }

                    nearest = t;
                    nearestOff = furthest;
                }

                if (nearest < 0 || lies)
                {
                    continue;
                }

                GeoPlane3 plane = targets[nearest].Plane;

                foreach (GeoPoint3 corner in corners)
                {
                    GeoPoint3 at = At(moved, corner);
                    moved[corner] = at.Add(plane.Normal.Multiply(-plane.SignedDistanceTo(at)));
                }
            }

            if (moved.Count == 0)
            {
                return second;
            }

            var faces = new List<GeoFace3>(second.Faces.Count + 8);

            foreach (GeoFace3 face in second.Faces)
            {
                if (!Touches(face, moved))
                {
                    faces.Add(face);
                    continue;
                }

                var boundary = new List<GeoPoint3>(face.Boundary.VertexCount);

                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    boundary.Add(At(moved, corner));
                }

                var holes = new List<IEnumerable<GeoPoint3>>(face.Holes.Count);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    var ring = new List<GeoPoint3>(hole.VertexCount);

                    foreach (GeoPoint3 corner in hole.Vertices)
                    {
                        ring.Add(At(moved, corner));
                    }

                    holes.Add(ring);
                }

                faces.AddRange(Loops3.ToFaces(boundary, holes, tolerance));
            }

            var put = new GeoSolid3(faces, second.Openings);

            // A face no wider than the contact put onto one plane at both its sides has no area left, and its neighbours may
            // no longer meet across it: such a body is no better a tool than the one given.
            return put.IsClosed(tolerance) || !second.IsClosed(tolerance) ? put : second;
        }

        private static GeoPoint3 At(Dictionary<GeoPoint3, GeoPoint3> moved, GeoPoint3 corner)
            => moved.TryGetValue(corner, out GeoPoint3 at) ? at : corner;

        private static bool Touches(GeoFace3 face, Dictionary<GeoPoint3, GeoPoint3> moved)
        {
            foreach (GeoPoint3 corner in face.Boundary.Vertices)
            {
                if (moved.ContainsKey(corner))
                {
                    return true;
                }
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                foreach (GeoPoint3 corner in hole.Vertices)
                {
                    if (moved.ContainsKey(corner))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
