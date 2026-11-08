using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// Whether the objects of a model are those a drawing was made from, every coordinate to the bit, and what changed of one
    /// object in words, for the Update from model dialog.
    /// </summary>
    internal static class SourceComparer
    {
        /// <summary>Whether two sources hold the same objects: the same ids, and each the same kind, texts, frame and body.</summary>
        public static bool Same(DrawingSource a, DrawingSource b)
        {
            if (a.Objects.Count != b.Objects.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Objects.Count; i++)
            {
                if (!Same(a.Objects[i], b.Objects[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether two objects are the same: id, kind, texts, frame and body, every coordinate to the bit.</summary>
        public static bool Same(DrawingObject a, DrawingObject b)
        {
            if (!string.Equals(a.Id, b.Id, StringComparison.Ordinal) || a.Kind != b.Kind || a.IsMain != b.IsMain
                || !string.Equals(a.Name, b.Name, StringComparison.Ordinal) || !string.Equals(a.Mark, b.Mark, StringComparison.Ordinal)
                || !string.Equals(a.Profile, b.Profile, StringComparison.Ordinal) || !string.Equals(a.Material, b.Material, StringComparison.Ordinal)
                || !string.Equals(a.ParentId, b.ParentId, StringComparison.Ordinal) || a.Attributes.Count != b.Attributes.Count
                || !Same(a.Frame.Origin, b.Frame.Origin) || !Same(a.Frame.XAxis, b.Frame.XAxis) || !Same(a.Frame.YAxis, b.Frame.YAxis)
                || a.Solid.Faces.Count != b.Solid.Faces.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, string> attribute in a.Attributes)
            {
                if (!b.Attributes.TryGetValue(attribute.Key, out string value) || !string.Equals(value, attribute.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int f = 0; f < a.Solid.Faces.Count; f++)
            {
                GeoFace3 fa = a.Solid.Faces[f];
                GeoFace3 fb = b.Solid.Faces[f];

                if (!Same(fa.Boundary, fb.Boundary) || fa.Holes.Count != fb.Holes.Count)
                {
                    return false;
                }

                for (int h = 0; h < fa.Holes.Count; h++)
                {
                    if (!Same(fa.Holes[h], fb.Holes[h]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// What changed of an object, in words: its length along its own X where that changed ("Length 3 000 → 3 500"), else
        /// how far it moved, else that its shape or texts did.
        /// </summary>
        public static string Describe(DrawingObject before, DrawingObject after, Tolerance tolerance)
        {
            double was = LengthAlongX(before);
            double now = LengthAlongX(after);

            if (Math.Abs(was - now) > tolerance.EqualPoint)
            {
                return "Length " + DialogViewModel.Length(Math.Round(was, 1)) + " → " + DialogViewModel.Length(Math.Round(now, 1));
            }

            GeoVector3 moved = after.Frame.Origin - before.Frame.Origin;

            if (moved.Length > tolerance.EqualPoint)
            {
                return "Moved " + DialogViewModel.Length(Math.Round(moved.Length, 1));
            }

            return !string.Equals(before.Profile, after.Profile, StringComparison.Ordinal) ? "Profile " + before.Profile + " → " + after.Profile
                : !string.Equals(before.Material, after.Material, StringComparison.Ordinal) ? "Material " + before.Material + " → " + after.Material
                : "Shape or texts changed";
        }

        /// <summary>How long an object is along its frame's X: the spread of its corners along that axis.</summary>
        public static double LengthAlongX(DrawingObject item)
        {
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            GeoVector3 x = item.Frame.XAxis;
            GeoPoint3 origin = item.Frame.Origin;

            foreach (GeoFace3 face in item.Solid.Faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    double along = (p - origin).DotProduct(x);
                    min = Math.Min(min, along);
                    max = Math.Max(max, along);
                }
            }

            return max >= min ? max - min : 0.0;
        }

        private static bool Same(GeoPolygon3 a, GeoPolygon3 b)
        {
            if (a.VertexCount != b.VertexCount)
            {
                return false;
            }

            for (int i = 0; i < a.VertexCount; i++)
            {
                if (!Same(a.Vertices[i], b.Vertices[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(GeoPoint3 a, GeoPoint3 b) => Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);

        private static bool Same(GeoVector3 a, GeoVector3 b) => Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);

        private static bool Bits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
