using System;
using System.Collections.Generic;
using System.Reflection;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using Xunit;

namespace GeometryHelper.SolidGeometry.UnitTest.Core
{
    /// <summary>
    /// The contracts every type in the library is supposed to keep, none of which the geometric sweeps
    /// could reach: equal things hash alike, a copy is its own object, and nothing is changed by being
    /// asked a question or by being transformed.
    /// </summary>
    public class TypeContractTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        /// <summary>One example of every geometry type, built the same way each run.</summary>
        private static IEnumerable<object> Samples()
        {
            var a = new GeoPoint3(1, 2, 3);
            var b = new GeoPoint3(4, -5, 6);
            var c = new GeoPoint3(-7, 8, 0);

            yield return a;
            yield return new GeoVector3(1, 2, 3);
            yield return new GeoLine3(a, b);
            yield return new GeoRay3(a, new GeoVector3(1, 1, 1));
            yield return new GeoPlane3(a, GeoVector3.ZAxis);
            yield return new GeoTriangle3(a, b, c);
            yield return new GeoCircle3(a, GeoVector3.ZAxis, 5);
            yield return new GeoAabb3(a, b);
            yield return new GeoObb3(a, 2, 3, 4);
            yield return new GeoCoordinateSystem3(a, GeoVector3.XAxis, GeoVector3.YAxis);
            yield return GeoTransform3.Translation(new GeoVector3(1, 2, 3));
            yield return new GeoPolyline3(a, b, c);
            yield return new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 4, 0), new GeoPoint3(0, 4, 0));
            yield return new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 4, 0), new GeoPoint3(0, 4, 0)));
        }

        /// <summary>Builds a second, separately constructed instance equal to the first.</summary>
        private static object Twin(object sample)
        {
            switch (sample)
            {
                case GeoPoint3 _: return new GeoPoint3(1, 2, 3);
                case GeoVector3 _: return new GeoVector3(1, 2, 3);
                case GeoLine3 _: return new GeoLine3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, -5, 6));
                case GeoRay3 _: return new GeoRay3(new GeoPoint3(1, 2, 3), new GeoVector3(1, 1, 1));
                case GeoPlane3 _: return new GeoPlane3(new GeoPoint3(1, 2, 3), GeoVector3.ZAxis);
                case GeoTriangle3 _: return new GeoTriangle3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, -5, 6), new GeoPoint3(-7, 8, 0));
                case GeoCircle3 _: return new GeoCircle3(new GeoPoint3(1, 2, 3), GeoVector3.ZAxis, 5);
                case GeoAabb3 _: return new GeoAabb3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, -5, 6));
                case GeoObb3 _: return new GeoObb3(new GeoPoint3(1, 2, 3), 2, 3, 4);
                case GeoCoordinateSystem3 _: return new GeoCoordinateSystem3(new GeoPoint3(1, 2, 3), GeoVector3.XAxis, GeoVector3.YAxis);
                case GeoTransform3 _: return GeoTransform3.Translation(new GeoVector3(1, 2, 3));
                case GeoPolyline3 _: return new GeoPolyline3(new GeoPoint3(1, 2, 3), new GeoPoint3(4, -5, 6), new GeoPoint3(-7, 8, 0));
                case GeoPolygon3 _: return new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 4, 0), new GeoPoint3(0, 4, 0));
                case GeoFace3 _: return new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(4, 0, 0), new GeoPoint3(4, 4, 0), new GeoPoint3(0, 4, 0)));
                default: throw new InvalidOperationException("no twin for " + sample.GetType().Name);
            }
        }

        [Fact]
        public void ThingsThatAreEqualHashAlike()
        {
            int checked_ = 0;

            foreach (object sample in Samples())
            {
                object twin = Twin(sample);

                Assert.True(sample.Equals(twin), $"{sample.GetType().Name}: two identical builds are not equal");
                Assert.True(twin.Equals(sample), $"{sample.GetType().Name}: equality is not symmetric");

                // The contract that matters: a dictionary or a set must find one by the other.
                Assert.Equal(sample.GetHashCode(), twin.GetHashCode());

                // And equality is reflexive.
                Assert.True(sample.Equals(sample));
                Assert.False(sample.Equals(null));
                Assert.False(sample.Equals("not a shape"));

                checked_++;
            }

            Assert.Equal(14, checked_);
        }

        [Fact]
        public void ACopyIsItsOwnObjectAndEqualToWhatItCameFrom()
        {
            int checked_ = 0;

            foreach (object sample in Samples())
            {
                MethodInfo clone = sample.GetType().GetMethod("Clone", Type.EmptyTypes);
                Assert.NotNull(clone);

                object copy = clone.Invoke(sample, null);

                Assert.True(sample.Equals(copy), $"{sample.GetType().Name}: a copy is not equal to its original");
                Assert.Equal(sample.GetHashCode(), copy.GetHashCode());

                // A reference type must hand back a different object, or it is not a copy at all. A value
                // type has nothing to share, so the question does not arise for it.
                if (!sample.GetType().IsValueType)
                {
                    Assert.False(ReferenceEquals(sample, copy), $"{sample.GetType().Name}: Clone returned the same object");
                }

                checked_++;
            }

            Assert.Equal(14, checked_);
        }

        [Fact]
        public void TransformingLeavesTheOriginalAlone()
        {
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(100, -50, 25))
                                 * GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7);

            int checked_ = 0;

            foreach (object sample in Samples())
            {
                if (sample is GeoTransform3) { continue; }

                MethodInfo transform = sample.GetType().GetMethod("TransformBy", new[] { typeof(GeoTransform3) });
                Assert.NotNull(transform);

                // A copy of what the shape was before it was asked to move.
                object before = Twin(sample);

                object moved = transform.Invoke(sample, new object[] { move });

                Assert.True(sample.Equals(before),
                            $"{sample.GetType().Name}: TransformBy changed the shape it was called on");
                Assert.False(sample.Equals(moved),
                             $"{sample.GetType().Name}: TransformBy handed back something unchanged");

                checked_++;
            }

            Assert.Equal(13, checked_);
        }

        [Fact]
        public void AskingAShapeAQuestionDoesNotChangeIt()
        {
            GeoSolid3 body = new GeoObb3(new GeoPoint3(1, 2, 3), 4, 5, 6).ToSolid();
            GeoSolid3 before = body.Clone();

            // Everything that measures, tests or meshes the body, run once each.
            double _ = body.Volume + body.SurfaceArea + body.NetVolume;
            body.GetAabb();
            body.Centroid.ToString();
            body.IsClosed();
            body.Triangulate();
            body.Contains(new GeoPoint3(1, 2, 3));
            body.Locate(new GeoPoint3(50, 50, 50));
            body.DistanceTo(new GeoPoint3(50, 50, 50));

            Assert.True(body.Equals(before), "measuring a body changed it");

            var chain = new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(5, 0, 0), new GeoPoint3(5, 5, 0));
            GeoPolyline3 chainBefore = chain.Clone();

            double __ = chain.Length;
            chain.GetAabb();
            chain.GetEdges();
            chain.TrySplitBy(new GeoPoint3(5, 0, 0), out GeoPolyline3[] ___);
            chain.Reverse();

            Assert.True(chain.Equals(chainBefore), "asking a chain about itself changed it");
        }

        [Fact]
        public void AShapeDoesNotShareItsVerticesWithTheListItWasBuiltFrom()
        {
            var vertices = new List<GeoPoint3>
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(4, 0, 0),
                new GeoPoint3(4, 4, 0),
                new GeoPoint3(0, 4, 0)
            };

            var polygon = new GeoPolygon3(vertices);
            var polyline = new GeoPolyline3(vertices);

            double areaBefore = polygon.Area;
            double lengthBefore = polyline.Length;

            // Changing the list afterwards must not reach inside shapes already built from it.
            vertices[2] = new GeoPoint3(400, 400, 0);
            vertices.Add(new GeoPoint3(9, 9, 9));

            Assert.Equal(areaBefore, polygon.Area, 9);
            Assert.Equal(lengthBefore, polyline.Length, 9);
            Assert.Equal(4, polygon.VertexCount);
            Assert.Equal(4, polyline.VertexCount);
        }
    }
}
