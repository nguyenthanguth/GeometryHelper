using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Core;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of speed16 in the core: a closed loop of arcs in space (<see cref="GeoPolygonArc3"/>, which a closed polyline
    /// with a bulge is read as) keeps no array of normals where its arcs share one, and makes the array, the same one for
    /// every call, when <c>Normals</c> is asked for; and the style of a TEXT and the block of an INSERT are read as the one
    /// string the records before them gave, where they spell it alike. The answers are held to the bit against the normal an
    /// edge of its own makes of the one given, and the sizes by the bytes allocated, which do not vary with the machine as a
    /// time does.
    /// </summary>
    public class DxfSpeed23Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static void AssertSameBits(GeoVector3 expected, GeoVector3 got, string step)
        {
            if (Bits(expected.X) != Bits(got.X) || Bits(expected.Y) != Bits(got.Y) || Bits(expected.Z) != Bits(got.Z))
            {
                Assert.Fail(step + ": got " + got + " where " + expected + " is wanted");
            }
        }

        private static GeoPoint3[] Rectangle(double width, double height) =>
            new[]
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(width, 0.0, 0.0),
                new GeoPoint3(width, height, 0.0),
                new GeoPoint3(0.0, height, 0.0),
            };

        /// <summary>
        /// The normal each edge of a loop reads about, as an edge of its own makes it of the normal given: none for a bulge of
        /// nought, the unit vector for another.
        /// </summary>
        private static GeoVector3[] Wanted(GeoPoint3[] vertices, double[] bulges, GeoVector3[] normals)
        {
            var wanted = new GeoVector3[vertices.Length];

            for (int i = 0; i < wanted.Length; i++)
            {
                wanted[i] = new GeoEdge3(
                    vertices[i],
                    vertices[(i + 1) % vertices.Length],
                    bulges[i],
                    normals[i],
                    Tol
                ).Normal;
            }

            return wanted;
        }

        private static void AssertNormals(GeoPolygonArc3 loop, GeoVector3[] wanted, string step)
        {
            Assert.Equal(wanted.Length, loop.Normals.Count);

            for (int i = 0; i < wanted.Length; i++)
            {
                AssertSameBits(wanted[i], loop.GetNormalAt(i), step + " GetNormalAt " + i);
                AssertSameBits(wanted[i], loop.Normals[i], step + " Normals " + i);

                // The edge is made of the unit normal the loop holds, which its constructor makes a unit of again.
                var again = new GeoEdge3(loop[i], loop[(i + 1) % wanted.Length], loop.GetBulgeAt(i), wanted[i]);
                AssertSameBits(again.Normal, loop.GetEdgeAt(i).Normal, step + " GetEdgeAt " + i);
            }
        }

        #region The normals of a loop

        [Fact]
        public void ALoopWhoseArcsShareOneNormal_GivesEveryEdgeItsUnitNormalOrNone_ToTheBit()
        {
            // 400 loops of three to nine vertices turned about two axes and given arcs on some edges, the normal of each arc the
            // loop's own but for its length, which is 2 or 5: every edge reads the normal an edge of its own makes of it.
            var random = new Random(23);
            int checkedLoops = 0;

            for (int n = 0; n < 400; n++)
            {
                int count = 3 + random.Next(7);
                double radius = 5.0 + random.NextDouble() * 50.0;
                var flat = new GeoPoint3[count];
                var bulges = new double[count];
                var given = new GeoVector3[count];
                double scale = random.Next(2) == 0 ? 2.0 : 5.0;
                bool clockwise = random.Next(2) == 0;

                for (int i = 0; i < count; i++)
                {
                    double angle = (clockwise ? -1.0 : 1.0) * (i + 0.3 * random.NextDouble()) * 2.0 * Math.PI / count;
                    flat[i] = new GeoPoint3(radius * Math.Cos(angle), radius * Math.Sin(angle), 0.0);
                    bulges[i] = random.Next(3) == 0 ? (random.NextDouble() - 0.5) * 1.2 : 0.0;
                    given[i] = bulges[i] != 0.0 ? new GeoVector3(0.0, 0.0, scale) : default;
                }

                double a = random.NextDouble() * 6.28;
                double b = random.NextDouble() * 6.28;
                GeoPoint3[] stood = flat.Select(p => Stand(p, a, b)).ToArray();
                GeoVector3[] normals = given.Select(v => v.Length == 0.0 ? v : Stand(v, a, b)).ToArray();
                GeoPolygonArc3 loop;

                try
                {
                    loop = new GeoPolygonArc3(stood, bulges, normals, Tol);
                }
                catch (ArgumentException)
                {
                    // Corners too close, or an arc out of the plane by the planar tolerance: refused as before.
                    continue;
                }

                AssertNormals(loop, Wanted(stood, bulges, normals), "loop " + n);
                checkedLoops++;
            }

            Assert.True(checkedLoops > 300, "only " + checkedLoops + " loops were made");
        }

        private static GeoPoint3 Stand(GeoPoint3 p, double a, double b)
        {
            GeoVector3 v = Stand(new GeoVector3(p.X, p.Y, p.Z), a, b);
            return new GeoPoint3(v.X + 120.0, v.Y - 45.0, v.Z + 300.0);
        }

        private static GeoVector3 Stand(GeoVector3 v, double a, double b)
        {
            double x1 = v.X;
            double y1 = v.Y * Math.Cos(a) - v.Z * Math.Sin(a);
            double z1 = v.Y * Math.Sin(a) + v.Z * Math.Cos(a);
            return new GeoVector3(x1 * Math.Cos(b) + z1 * Math.Sin(b), y1, -x1 * Math.Sin(b) + z1 * Math.Cos(b));
        }

        [Fact]
        public void ALoopOfStraightEdges_HasNoNormalOnAnyEdge()
        {
            var loop = new GeoPolygonArc3(Rectangle(20.0, 10.0), null, null, Tol);

            Assert.Equal(4, loop.Normals.Count);

            for (int i = 0; i < 4; i++)
            {
                AssertSameBits(default, loop.GetNormalAt(i), "edge " + i);
                AssertSameBits(default, loop.Normals[i], "normals " + i);
            }
        }

        [Fact]
        public void ALoopWhoseArcsGiveNormalsOfOneDirectionButNotOneLength_ReadsTheUnitVectorOnEach()
        {
            // (0, 0, 1) and (0, 0, 2) are two normals to the bit and one to an edge: both arcs read (0, 0, 1).
            GeoPoint3[] corners = Rectangle(20.0, 20.0);
            double[] bulges = { 0.5, 0.0, 0.3, 0.0 };
            GeoVector3[] normals = { new GeoVector3(0.0, 0.0, 1.0), default, new GeoVector3(0.0, 0.0, 2.0), default };

            var loop = new GeoPolygonArc3(corners, bulges, normals, Tol);

            AssertNormals(loop, Wanted(corners, bulges, normals), "two lengths");
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), loop.GetNormalAt(2), "second arc");
        }

        [Fact]
        public void ALoopWhoseArcsFaceOppositeWays_KeepsTheNormalOfEach()
        {
            // A bulge about (0, 0, 1) and one about (0, 0, -1): each edge keeps its own, as the arrays held them.
            GeoPoint3[] corners = Rectangle(20.0, 20.0);
            double[] bulges = { 0.5, 0.0, -0.3, 0.0 };
            GeoVector3[] normals = { new GeoVector3(0.0, 0.0, 1.0), default, new GeoVector3(0.0, 0.0, -1.0), default };

            var loop = new GeoPolygonArc3(corners, bulges, normals, Tol);

            AssertNormals(loop, Wanted(corners, bulges, normals), "opposite");
            Assert.Equal(1.0, loop.GetNormalAt(0).Z);
            Assert.Equal(-1.0, loop.GetNormalAt(2).Z);
        }

        [Fact]
        public void AVertexOnItsNeighbour_IsDropped_AndTheKeptOneTakesTheBulgeAndNormalOfTheEdgeRunningOn()
        {
            // The vertex at (20, 0.0004) is dropped, a thousandth being the tolerance: the arc leaving it, 0.3, is the one the
            // vertex (20, 0) keeps in place of its own straight edge, and the arc before it (0.5) is kept.
            GeoPoint3[] corners =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(20.0, 0.0, 0.0),
                new GeoPoint3(20.0, 0.0004, 0.0),
                new GeoPoint3(20.0, 20.0, 0.0),
                new GeoPoint3(0.0, 20.0, 0.0),
            };
            double[] bulges = { 0.5, 0.0, 0.3, 0.0, 0.0 };
            GeoVector3[] normals =
            {
                new GeoVector3(0.0, 0.0, 1.0),
                default,
                new GeoVector3(0.0, 0.0, 1.0),
                default,
                default,
            };

            var loop = new GeoPolygonArc3(corners, bulges, normals, Tol);

            Assert.Equal(4, loop.VertexCount);
            Assert.Equal(new[] { 0.5, 0.3, 0.0, 0.0 }, loop.Bulges);
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), loop.GetNormalAt(0), "first arc");
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), loop.GetNormalAt(1), "second arc");
            AssertSameBits(default, loop.GetNormalAt(2), "straight");
            AssertSameBits(default, loop.GetNormalAt(3), "straight");
        }

        [Fact]
        public void AnArcDroppedWithItsVertex_LeavesTheEdgeStraight_AndTheNormalNone()
        {
            // The first arc's vertex is dropped by a vertex on it whose edge is straight: no arc is left, and no normal.
            GeoPoint3[] corners =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0004, 0.0, 0.0),
                new GeoPoint3(20.0, 0.0, 0.0),
                new GeoPoint3(20.0, 20.0, 0.0),
                new GeoPoint3(0.0, 20.0, 0.0),
            };
            double[] bulges = { 0.5, 0.0, 0.0, 0.0, 0.0 };
            GeoVector3[] normals = { new GeoVector3(0.0, 0.0, 1.0), default, default, default, default };

            var loop = new GeoPolygonArc3(corners, bulges, normals, Tol);

            Assert.Equal(4, loop.VertexCount);
            Assert.Equal(new[] { 0.0, 0.0, 0.0, 0.0 }, loop.Bulges);

            for (int i = 0; i < 4; i++)
            {
                AssertSameBits(default, loop.GetNormalAt(i), "edge " + i);
                AssertSameBits(default, loop.Normals[i], "normals " + i);
            }
        }

        [Fact]
        public void TheNormalsOfALoopAskedFor_AreOneArray_EachCallAndEveryThread()
        {
            var loop = new GeoPolygonArc3(
                Rectangle(20.0, 20.0),
                new[] { 0.5, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                Tol
            );
            var seen = new IReadOnlyList<GeoVector3>[16];

            System.Threading.Tasks.Parallel.For(0, seen.Length, i => seen[i] = loop.Normals);

            Assert.All(seen, s => Assert.Same(seen[0], s));
            Assert.Same(seen[0], loop.Normals);
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), seen[0][0], "arc");
            AssertSameBits(default, seen[0][1], "straight");
        }

        [Fact]
        public void ACopyOfALoop_AndTheLoopMoved_ReadTheNormalsOfTheLoop_WhetherItsNormalsWereAskedFor()
        {
            GeoPolygonArc3 Make() =>
                new GeoPolygonArc3(
                    Rectangle(20.0, 20.0),
                    new[] { 0.5, 0.0, 0.25, 0.0 },
                    new[] { new GeoVector3(0.0, 0.0, 1.0), default, new GeoVector3(0.0, 0.0, 1.0), default },
                    Tol
                );

            GeoPolygonArc3 untouched = Make();
            GeoPolygonArc3 asked = Make();
            _ = asked.Normals;

            foreach (GeoPolygonArc3 loop in new[] { untouched, asked })
            {
                GeoPolygonArc3 clone = loop.Clone();
                GeoPolygonArc3 moved = loop.Translate(new GeoVector3(5.0, -3.0, 0.0));

                Assert.True(clone.Equals(loop));
                Assert.True(loop.Equals(clone));
                Assert.Equal(loop.GetHashCode(), clone.GetHashCode());

                for (int i = 0; i < 4; i++)
                {
                    AssertSameBits(loop.GetNormalAt(i), clone.GetNormalAt(i), "clone " + i);
                    AssertSameBits(loop.GetNormalAt(i), moved.GetNormalAt(i), "moved " + i);
                    Assert.Equal(Bits(loop.GetBulgeAt(i)), Bits(moved.GetBulgeAt(i)));
                }
            }

            Assert.True(untouched.Equals(asked));
            Assert.True(asked.Equals(untouched));
        }

        [Fact]
        public void AnArcWithANormalOfNoLength_IsRefusedAsBefore_ThoughNoArrayIsKept()
        {
            Assert.Throws<ArgumentException>(() =>
                new GeoPolygonArc3(Rectangle(20.0, 20.0), new[] { 0.5, 0.0, 0.0, 0.0 }, null, Tol)
            );
            Assert.Throws<ArgumentException>(() =>
                new GeoPolygonArc3(
                    Rectangle(20.0, 20.0),
                    new[] { 0.5, 0.0, 0.3, 0.0 },
                    new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                    Tol
                )
            );
        }

        [Fact]
        public void AnArcOutOfThePlaneOfTheLoop_IsRefusedAsBefore()
        {
            // The arc bulges about the X axis, out of the plane z = 0 the four corners stand in.
            Assert.Throws<ArgumentException>(() =>
                new GeoPolygonArc3(
                    Rectangle(20.0, 20.0),
                    new[] { 0.5, 0.0, 0.0, 0.0 },
                    new[] { new GeoVector3(1.0, 0.0, 0.0), default, default, default },
                    Tol
                )
            );
        }

        [Fact]
        public void ALoopOfFourEdgesWithOneArc_HoldsNoArrayOfNormals()
        {
            // The object (96 bytes with the one normal it holds), the vertices (112) and the bulges (48): 256 bytes where the
            // array of normals (112) made 360 before; 264 as measured, and the 200 000 loops of a drawing of 1 000 000 entities hold 20 MB less.
            GeoPoint3[] corners = Rectangle(20.0, 20.0);
            double[] bulges = { 0.0, 0.5, 0.0, 0.0 };
            GeoVector3[] normals = { default, new GeoVector3(0.0, 0.0, 1.0), default, default };
            _ = new GeoPolygonArc3(corners, bulges, normals, Tol);

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                _ = new GeoPolygonArc3(corners, bulges, normals, Tol);
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(used / 1_000 <= 280, "allocated " + used / 1_000 + " bytes a loop");
        }

        #endregion

        #region The names a record repeats

        private static string Texts(IEnumerable<string> styles)
        {
            var text = new StringBuilder();
            int i = 0;

            foreach (string style in styles)
            {
                text.Append(G(0, "TEXT", 5, (0x100 + i).ToString("X"), 8, "0", 100, "AcDbEntity", 100, "AcDbText"));
                text.Append(G(10, 1.0 * i, 20, 2.0, 30, 0.0, 40, 2.5, 1, "T" + i));

                if (style != null)
                {
                    text.Append(G(7, style));
                }

                text.Append(G(100, "AcDbText"));
                i++;
            }

            return text.ToString();
        }

        [Fact]
        public void TextsOfOneStyle_ReadTheStyleAsOneString_AndTextsOfSeveralEachReadTheirOwn()
        {
            string[] names = { "Standard", "Notes", "ARIAL", "romans", "Standard2", "Standard" };
            // Six styles in turn, more than the four kept, and a style left out, which reads Standard.
            string[] styles = Enumerable
                .Range(0, 60)
                .Select(i => i % 11 == 10 ? null : names[i % names.Length])
                .ToArray();
            DxfDatabase db = DxfDatabase.Parse(R2000(Texts(styles)));
            DxfDBText[] texts = db.ModelSpace.OfType<DxfDBText>().ToArray();

            Assert.Equal(styles.Length, texts.Length);

            for (int i = 0; i < texts.Length; i++)
            {
                string wanted = styles[i] ?? "Standard";

                Assert.Equal(wanted, texts[i].ReadForDisplay().TextStyleName);
                Assert.Equal(texts[i].TextStyleName, texts[i].ReadForDisplay().TextStyleName);
            }

            // Two neighbours of one style read the one string.
            DxfDatabase twin = DxfDatabase.Parse(R2000(Texts(new[] { "Notes", "Notes", "Notes" })));
            string[] read = twin.ModelSpace.OfType<DxfDBText>().Select(t => t.ReadForDisplay().TextStyleName).ToArray();

            Assert.Same(read[0], read[1]);
            Assert.Same(read[1], read[2]);
        }

        [Fact]
        public void AStyleThatIsNotPlainASCII_OrIsLong_OrHasSpaces_IsReadAsItWasWritten()
        {
            string longName = new string('S', 70);
            string[] styles =
            {
                "Étude",
                "Étude",
                longName,
                longName,
                " Notes ",
                " Notes ",
                "Notes",
                "Ünï",
                "Standard",
            };
            DxfDatabase db = DxfDatabase.Parse(R2000(Texts(styles)));
            DxfDBText[] texts = db.ModelSpace.OfType<DxfDBText>().ToArray();

            for (int i = 0; i < texts.Length; i++)
            {
                Assert.Equal(texts[i].TextStyleName, texts[i].ReadForDisplay().TextStyleName);
            }

            Assert.Equal(longName, texts[2].ReadForDisplay().TextStyleName);
            Assert.Equal("Notes", texts[4].ReadForDisplay().TextStyleName);
            Assert.Equal("Notes", texts[6].ReadForDisplay().TextStyleName);
        }

        [Fact]
        public void TwentyThousandTextsInOneStyle_MakeNoStringForTheStyle()
        {
            // The style of each read as a string of its own: "Standard" is 40 bytes, 0.8 MB of 20 000 texts.
            DxfDatabase db = DxfDatabase.Parse(R2000(Texts(Enumerable.Repeat("Standard", 20_000))));
            DxfDBText[] texts = db.ModelSpace.OfType<DxfDBText>().ToArray();
            string last = null;

            // Warm: the first read of each makes the groups' places and the strings once.
            foreach (DxfDBText text in texts.Take(100))
            {
                last = text.ReadForDisplay().TextStyleName;
            }

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            foreach (DxfDBText text in texts)
            {
                string style = text.ReadForDisplay().TextStyleName;
                Assert.Equal("Standard", style);
                last = style;
            }

            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / texts.Length;

            Assert.Equal("Standard", last);

            // Left: the words of each text, "T" and a number, as a string: 32 to 40 bytes, and the string of the style made 40.
            Assert.True(used < 60, "allocated " + used + " bytes a text");
        }

        private static string InsertBlocks() =>
            G(0, "SECTION", 2, "BLOCKS")
            + string.Concat(
                new[] { "B", "Bolt", "PLATE-1" }.Select(name =>
                    G(0, "BLOCK", 8, "0", 2, name, 70, 0, 10, 0.0, 20, 0.0, 30, 0.0, 3, name)
                    + G(1, string.Empty)
                    + G(0, "LINE", 8, "0", 10, 0.0, 20, 0.0, 30, 0.0, 11, 1.0, 21, 1.0, 31, 0.0)
                    + G(0, "ENDBLK", 8, "0")
                )
            )
            + G(0, "ENDSEC");

        [Fact]
        public void InsertsOfSeveralBlocksInTurn_EachReadTheirOwnBlock()
        {
            string[] names = { "B", "Bolt", "PLATE-1", "B", "B", "Bolt" };
            var entities = new StringBuilder();

            for (int i = 0; i < 24; i++)
            {
                entities.Append(
                    G(0, "INSERT", 5, (0x200 + i).ToString("X"), 8, "0", 100, "AcDbEntity", 100, "AcDbBlockReference")
                        + G(2, names[i % names.Length], 10, 3.0 * i, 20, 1.0, 30, 0.0)
                );
            }

            DxfDatabase db = DxfDatabase.Parse(File("AC1015", 4, InsertBlocks(), entities.ToString()));
            DxfBlockReference[] inserts = db.ModelSpace.OfType<DxfBlockReference>().ToArray();

            Assert.Equal(24, inserts.Length);

            for (int i = 0; i < inserts.Length; i++)
            {
                Assert.True(inserts[i].TryReadForDisplay(out DxfBlockReference.DisplayRead read));
                Assert.Equal(names[i % names.Length], read.Block.Name);
                Assert.Same(inserts[i].BlockTableRecord, read.Block);
            }
        }

        #endregion
    }
}
