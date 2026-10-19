using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// What the import tests compare shapes by: the numbers in the data of an item, to the bit or to a stated precision.
    /// </summary>
    internal static class Imp
    {
        /// <summary>
        /// Every number in the data, in document order; a text in it is not a number and is left out.
        /// </summary>
        internal static List<double> Numbers(JsonElement element)
        {
            var list = new List<double>();
            Collect(element, list);
            return list;
        }

        private static void Collect(JsonElement element, List<double> list)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    list.Add(element.GetDouble());
                    break;

                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        Collect(child, list);
                    }

                    break;

                case JsonValueKind.Object:
                    foreach (JsonProperty property in element.EnumerateObject())
                    {
                        Collect(property.Value, list);
                    }

                    break;
            }
        }

        /// <summary>
        /// The numbers a library shape is written to a scene as.
        /// </summary>
        internal static List<double> Of(object geometry) => Numbers(GeometryEncoder.Encode(geometry));

        internal static List<double> Of(SceneItem item) => Numbers(item.Data);

        /// <summary>
        /// Asserts that two lists of numbers are the same to the bit, minus nought and nought apart.
        /// </summary>
        internal static void SameBits(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string what = "")
        {
            Assert.True(expected.Count == actual.Count, what + ": " + expected.Count + " numbers expected, " + actual.Count + " found");

            for (int i = 0; i < expected.Count; i++)
            {
                if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                {
                    Assert.Fail(what + ": number " + i + " is " + actual[i].ToString("R") + " where " + expected[i].ToString("R") + " was written");
                }
            }
        }

        /// <summary>
        /// Asserts that two lists of numbers agree to a precision, relative to the larger of the two and one.
        /// </summary>
        internal static void Near(IReadOnlyList<double> expected, IReadOnlyList<double> actual, double relative, string what = "")
        {
            Assert.True(expected.Count == actual.Count, what + ": " + expected.Count + " numbers expected, " + actual.Count + " found");

            for (int i = 0; i < expected.Count; i++)
            {
                double scale = Math.Max(1.0, Math.Max(Math.Abs(expected[i]), Math.Abs(actual[i])));

                if (Math.Abs(expected[i] - actual[i]) > relative * scale)
                {
                    Assert.Fail(what + ": number " + i + " is " + actual[i].ToString("R") + " where " + expected[i].ToString("R") + " was written (allowed " + relative + ")");
                }
            }
        }

        internal static double[] P2(GeoPoint2 p) => new[] { p.X, p.Y };

        internal static double[] P3(GeoPoint3 p) => new[] { p.X, p.Y, p.Z };

        /// <summary>
        /// A point in the plane as the three numbers it has in space.
        /// </summary>
        internal static double[] Flat(GeoPoint2 p) => new[] { p.X, p.Y, 0.0 };

        /// <summary>
        /// The points of a mesh item in order, three numbers each.
        /// </summary>
        internal static Vec3[] MeshPoints(SceneItem item) => DataJson.Points(item.Data, "points", 3);

        /// <summary>
        /// The faces of a mesh item as the corners they name, in the order of the faces.
        /// </summary>
        internal static List<Vec3[]> MeshFaces(SceneItem item)
        {
            Vec3[] points = MeshPoints(item);
            return GeometryDecoder.Faces(item.Data).Select(face => face.Select(i => points[i]).ToArray()).ToList();
        }

        /// <summary>
        /// The corners of a face as a text of their bits, so that two faces are the same when their corners are the same to the bit and in the same order.
        /// </summary>
        internal static string Key(IEnumerable<Vec3> corners)
            => string.Join("|", corners.Select(c => Bits(c.X) + "," + Bits(c.Y) + "," + Bits(c.Z)));

        internal static string Key(params Vec3[] corners) => Key((IEnumerable<Vec3>)corners);

        internal static string Key(IEnumerable<GeoPoint3> corners) => Key(corners.Select(c => new Vec3(c.X, c.Y, c.Z)));

        internal static string Key(params GeoPoint3[] corners) => Key((IEnumerable<GeoPoint3>)corners);

        private static string Bits(double v) => BitConverter.DoubleToInt64Bits(v + 0.0).ToString("X");

        internal static List<string> Keys(IEnumerable<GeoTriangle3> triangles) => triangles.Select(t => Key(t.A, t.B, t.C)).ToList();

        internal static List<string> Keys(SceneItem mesh) => MeshFaces(mesh).Select(face => Key((IEnumerable<Vec3>)face)).ToList();

        internal static double Distance(Vec3 a, GeoPoint3 b)
            => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)) + ((a.Z - b.Z) * (a.Z - b.Z)));

        /// <summary>
        /// Asserts that every point lies on the circle round a centre in the plane of a normal, to a billionth of the radius.
        /// </summary>
        internal static void AssertOnCircle(IEnumerable<Vec3> points, GeoPoint3 centre, GeoVector3 normal, double radius)
        {
            foreach (Vec3 p in points)
            {
                Assert.InRange(Distance(p, centre) / radius, 1.0 - 1E-9, 1.0 + 1E-9);
                double offPlane = ((p.X - centre.X) * normal.X) + ((p.Y - centre.Y) * normal.Y) + ((p.Z - centre.Z) * normal.Z);
                Assert.InRange(Math.Abs(offPlane) / radius, 0.0, 1E-9);
            }
        }

        /// <summary>
        /// The single item of a scene, after checking that nothing was left out.
        /// </summary>
        internal static SceneItem Only(Scene scene)
        {
            Assert.Empty(scene.LoadWarnings);
            return Assert.Single(scene.Items);
        }

        /// <summary>
        /// Counts the items of a kind.
        /// </summary>
        internal static int Count(Scene scene, string kind) => scene.Items.Count(i => i.Kind == kind);

        /// <summary>
        /// Asserts that a scene survives being written and read as a scene file, and that every item can be drawn.
        /// </summary>
        internal static void IsAValidScene(Scene scene)
        {
            string json = new SceneWriter(scene).ToJson();
            Scene again = SceneReader.ReadJson(json);
            Assert.Equal(scene.Items.Count, again.Items.Count);

            for (int i = 0; i < scene.Items.Count; i++)
            {
                Assert.Equal(scene.Items[i].Kind, again.Items[i].Kind);
                SameBits(Of(scene.Items[i]), Of(again.Items[i]), scene.Items[i].Name);
            }

            Primitives primitives = Primitives.Build(scene);
            Assert.Empty(primitives.Warnings);
        }
    }
}
