using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The VERTEX records that follow a POLYLINE, read in one pass over its groups, and the reading and writing of the own
    /// groups of one: what the polyline classes walk their vertices with, where asking each vertex object would read the
    /// polyline's records once for each.
    /// </summary>
    internal static class DxfFollowers
    {
        /// <summary>
        /// The VERTEX records of an entity, each its index among the records (1 the first that follows the entity) and its own
        /// groups: from its first subclass marker other than AcDbEntity, or from its start in a file of R12, outside the groups
        /// of the applications and before its extended data, as the plumbing reads the own part of a record.
        /// </summary>
        internal static List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>> Vertices(DxfItem item)
        {
            var vertices = new List<KeyValuePair<int, IReadOnlyList<DxfTypedValue>>>();
            List<DxfTypedValue> current = null;
            int record = 0;

            foreach (DxfTypedValue group in item.Groups)
            {
                if (group.TypeCode == 0)
                {
                    record++;

                    if (current != null)
                    {
                        vertices.Add(new KeyValuePair<int, IReadOnlyList<DxfTypedValue>>(record - 1, OwnPart(current)));
                    }

                    current = group.Value.Trim() == "VERTEX" ? new List<DxfTypedValue>() : null;
                    continue;
                }

                current?.Add(group);
            }

            if (current != null)
            {
                vertices.Add(new KeyValuePair<int, IReadOnlyList<DxfTypedValue>>(record, OwnPart(current)));
            }

            return vertices;
        }

        /// <summary>
        /// The groups that move the point (10, 20, 30) of each VERTEX record a filter takes by a transformation in the world, for
        /// one writing of the entity: a 3D polyline's vertices, a mesh's points.
        /// </summary>
        internal static List<KeyValuePair<int, KeyValuePair<int, string>[]>> Moved(DxfItem item, GeoTransform3 transform, Func<IReadOnlyList<DxfTypedValue>, bool> takes)
        {
            var origin = new GeoPoint3(0.0, 0.0, 0.0);
            var records = new List<KeyValuePair<int, KeyValuePair<int, string>[]>>();

            foreach (KeyValuePair<int, IReadOnlyList<DxfTypedValue>> vertex in Vertices(item))
            {
                if (takes(vertex.Value))
                {
                    GeoPoint3 moved = transform.Transform(Point(vertex.Value, 10, origin));
                    records.Add(new KeyValuePair<int, KeyValuePair<int, string>[]>(vertex.Key, PointChanges(vertex.Value, 10, moved, origin, 3)));
                }
            }

            return records;
        }

        /// <summary>The own part of a record's groups (its group 0 left out), as the plumbing reads it.</summary>
        private static IReadOnlyList<DxfTypedValue> OwnPart(List<DxfTypedValue> record)
        {
            int data = record.FindIndex(g => g.TypeCode == 1001);
            data = data < 0 ? record.Count : data;
            int body = -1;
            bool braced = false;

            for (int i = 0; i < data && body < 0; i++)
            {
                if (record[i].TypeCode == 102)
                {
                    braced = record[i].Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced && record[i].TypeCode == 100 && record[i].Value.Trim() != "AcDbEntity")
                {
                    body = i;
                }
            }

            var own = new List<DxfTypedValue>();
            braced = false;

            for (int i = body >= 0 ? body : 0; i < data; i++)
            {
                if (record[i].TypeCode == 102)
                {
                    braced = record[i].Value.Trim().StartsWith("{", StringComparison.Ordinal);
                }
                else if (!braced)
                {
                    own.Add(record[i]);
                }
            }

            return own.AsReadOnly();
        }

        /// <summary>The text of the first group of a code; null where there is none.</summary>
        internal static string Text(IReadOnlyList<DxfTypedValue> groups, int code)
        {
            foreach (DxfTypedValue group in groups)
            {
                if (group.TypeCode == code)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>The number of the first group of a code, as the reader reads one; the fallback where there is none or it is not finite.</summary>
        internal static double Number(IReadOnlyList<DxfTypedValue> groups, int code, double fallback)
            => Numbers.TryRead(Text(groups, code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>The whole number of the first group of a code; the fallback where there is none, or it is not a whole number.</summary>
        internal static int Whole(IReadOnlyList<DxfTypedValue> groups, int code, int fallback)
            => int.TryParse(Text(groups, code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>The point of a code and the codes 10 and 20 above it, as the reader reads one: a coordinate left out is nought, the fallback where all three are.</summary>
        internal static GeoPoint3 Point(IReadOnlyList<DxfTypedValue> groups, int code, GeoPoint3 fallback)
        {
            bool x = TryNumber(groups, code, out double px);
            bool y = TryNumber(groups, code + 10, out double py);
            bool z = TryNumber(groups, code + 20, out double pz);
            return x || y || z ? new GeoPoint3(px, py, pz) : fallback;
        }

        /// <summary>
        /// The groups that set a point of a record (the code and those 10 and 20 above it, the first two alone where the point
        /// is of the plane): each coordinate that differs from the one read, to the bit, and those the record leaves out with
        /// them; none where none differs.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when a coordinate is not a finite number.</exception>
        internal static KeyValuePair<int, string>[] PointChanges(IReadOnlyList<DxfTypedValue> groups, int code, GeoPoint3 value, GeoPoint3 fallback, int dimensions)
        {
            if (!value.IsValid)
            {
                throw new ArgumentException("A point has to be of finite numbers: " + value + " is not.", nameof(value));
            }

            GeoPoint3 now = Point(groups, code, fallback);
            double[] wanted = { value.X, value.Y, value.Z };
            double[] read = { now.X, now.Y, now.Z };
            bool differs = false;

            for (int i = 0; i < dimensions; i++)
            {
                differs |= !Same(wanted[i], read[i]);
            }

            if (!differs)
            {
                return new KeyValuePair<int, string>[0];
            }

            var changes = new List<KeyValuePair<int, string>>();

            for (int i = 0; i < dimensions; i++)
            {
                int at = code + 10 * i;

                if (!Same(wanted[i], read[i]) || Text(groups, at) == null)
                {
                    changes.Add(new KeyValuePair<int, string>(at, Numbers.Exact(wanted[i] + 0.0)));
                }
            }

            return changes.ToArray();
        }

        /// <summary>The group that sets a number, none where the number read is the same to the bit; taken out at the fallback where asked.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the number is not finite.</exception>
        internal static KeyValuePair<int, string>[] NumberChange(IReadOnlyList<DxfTypedValue> groups, int code, double value, double fallback, bool dropAtFallback)
        {
            Guard.Finite(value, nameof(value), "The number has to be a finite number.");

            if (Same(value, Number(groups, code, fallback)))
            {
                return new KeyValuePair<int, string>[0];
            }

            string text = dropAtFallback && Same(value + 0.0, fallback) ? null : Numbers.Exact(value + 0.0);
            return new[] { new KeyValuePair<int, string>(code, text) };
        }

        /// <summary>The group that sets a whole number, none where it is the one read.</summary>
        internal static KeyValuePair<int, string>[] WholeChange(IReadOnlyList<DxfTypedValue> groups, int code, int value, int fallback)
            => value == Whole(groups, code, fallback)
                ? new KeyValuePair<int, string>[0]
                : new[] { new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture)) };

        /// <summary>Whether two numbers are the same to the bit.</summary>
        internal static bool Same(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        private static bool TryNumber(IReadOnlyList<DxfTypedValue> groups, int code, out double value)
        {
            if (Numbers.TryRead(Text(groups, code), out value) && Guard.IsFinite(value))
            {
                return true;
            }

            value = 0.0;
            return false;
        }
    }
}
