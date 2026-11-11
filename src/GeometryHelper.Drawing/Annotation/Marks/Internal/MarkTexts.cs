using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// The texts of marks, made from the fields of a <see cref="MarkFormat"/> and what an object holds, and the sizes of
    /// bolts read from their bodies where their attributes give none.
    /// </summary>
    internal static class MarkTexts
    {
        /// <summary>The attribute a bolt's group is read from.</summary>
        internal const string BoltGroupAttribute = "BoltGroup";

        /// <summary>The attribute a bolt's or a hole's diameter is read from.</summary>
        internal const string DiameterAttribute = "Diameter";

        /// <summary>The attribute an object's length is read from.</summary>
        internal const string LengthAttribute = "Length";

        /// <summary>
        /// The text of a mark: each field of the format with something to show, the separator between them. The count is
        /// the group's for a bolt and the parts of the same mark among all for a part.
        /// </summary>
        internal static string Text(MarkFormat format, DrawingObject item, int count)
        {
            var text = new StringBuilder();

            foreach (MarkField field in format.Fields)
            {
                string value = Value(field, item, count);

                if (value.Length == 0)
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.Append(format.Separator);
                }

                text.Append(value);
            }

            return text.ToString();
        }

        /// <summary>
        /// The key bolts are grouped by: their attribute BoltGroup where they have one; otherwise their assembly with their
        /// size and length, so that bolts of one size in one assembly are one group.
        /// </summary>
        internal static string BoltGroupKey(DrawingObject bolt) =>
            bolt.Attributes.TryGetValue(BoltGroupAttribute, out string group)
                ? "G\u0001" + group
                : "P\u0001" + (bolt.ParentId ?? string.Empty) + "\u0001" + Diameter(bolt) + "\u0001" + Length(bolt);

        /// <summary>How far a body reaches along a frame's X, from its least to its most; false for a body of no corners.</summary>
        internal static bool Extent(GeoSolid3 solid, GeoCoordinateSystem3 own, out double min, out double max)
        {
            min = double.PositiveInfinity;
            max = double.NegativeInfinity;

            foreach (GeoFace3 face in solid.Faces)
            {
                foreach (GeoPoint3 vertex in face.Boundary.Vertices)
                {
                    double along = own.XAxis.DotProduct(own.Origin.GetVectorTo(vertex));
                    min = Math.Min(min, along);
                    max = Math.Max(max, along);
                }
            }

            return min <= max;
        }

        /// <summary>The middle of a bolt's axis, its frame's X, half way between the ends of its body.</summary>
        internal static GeoPoint3 AxisCentre(DrawingObject bolt)
        {
            GeoCoordinateSystem3 own = bolt.Frame;
            return Extent(bolt.Solid, own, out double min, out double max)
                ? own.Origin.Add(own.XAxis.Multiply(0.5 * (min + max)))
                : own.Origin;
        }

        /// <summary>Twice the furthest a corner of a body stands off its frame's X axis: the diameter of a faceted bolt.</summary>
        internal static double AxisDiameter(DrawingObject item)
        {
            GeoCoordinateSystem3 own = item.Frame;
            double furthest = 0.0;

            foreach (GeoFace3 face in item.Solid.Faces)
            {
                foreach (GeoPoint3 vertex in face.Boundary.Vertices)
                {
                    GeoVector3 from = own.Origin.GetVectorTo(vertex);
                    GeoVector3 across = from.Subtract(own.XAxis.Multiply(own.XAxis.DotProduct(from)));
                    furthest = Math.Max(furthest, across.Length);
                }
            }

            return 2.0 * furthest;
        }

        private static string Value(MarkField field, DrawingObject item, int count)
        {
            switch (field.Kind)
            {
                case MarkFieldKind.Mark:
                    return item.Mark;
                case MarkFieldKind.Name:
                    return item.Name;
                case MarkFieldKind.Profile:
                    return item.Profile;
                case MarkFieldKind.Material:
                    return item.Material;
                case MarkFieldKind.Length:
                    return Length(item);
                case MarkFieldKind.Count:
                    return count.ToString(CultureInfo.InvariantCulture);
                case MarkFieldKind.Diameter:
                    string diameter = Diameter(item);
                    return diameter.Length == 0 ? string.Empty : "M" + diameter;
                default:
                    return item.Attributes.TryGetValue(field.AttributeName, out string value) ? value : string.Empty;
            }
        }

        /// <summary>The length: the attribute Length as it is written, or the reach of the body along its frame's X to the millimetre.</summary>
        private static string Length(DrawingObject item)
        {
            if (item.Attributes.TryGetValue(LengthAttribute, out string length))
            {
                return length;
            }

            return Extent(item.Solid, item.Frame, out double min, out double max)
                ? DrawingDimension.Format(max - min, 1.0)
                : string.Empty;
        }

        /// <summary>The diameter of a bolt or a hole, without its M: the attribute Diameter, or the body's to a tenth; empty for a part.</summary>
        private static string Diameter(DrawingObject item)
        {
            if (item.Kind != DrawingObjectKind.Bolt && item.Kind != DrawingObjectKind.Hole)
            {
                return string.Empty;
            }

            return item.Attributes.TryGetValue(DiameterAttribute, out string diameter)
                ? diameter
                : DrawingDimension.Format(AxisDiameter(item), 0.1);
        }
    }
}
