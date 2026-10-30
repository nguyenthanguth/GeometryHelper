using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to plugin run v6 of the checks of 30 October 2026: every face of a closed
    /// MESH that faces in is turned, a triangle and a quad as well as a face of many corners (point 64x); the words of an
    /// attribute of several lines are those of its MTEXT joined by spaces, and setting them sets its MTEXT (06); a spline made
    /// a polyline of arcs has the vertices of its lines, each arc bent by a quarter of the turn of the spline's tangent along
    /// it (17-b); and a drawing that has no <c>$PLINECONVERTMODE</c> is not given one. Each drawing is one of ezdxf's, or built
    /// here as text, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD14Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A record built from its groups as "code=value" apart by spaces, as the file writes them.</summary>
        private static string Rec(string type, string groups)
        {
            var text = new StringBuilder("  0\n" + type + "\n");

            foreach (string group in groups.Split(' '))
            {
                int at = group.IndexOf('=');
                text.Append(group.Substring(0, at).PadLeft(3)).Append('\n').Append(group.Substring(at + 1)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>ezdxf's drawing of R2018 with entities of their own put last in its ENTITIES section, the handles from C00 free.</summary>
        private static string With(string entities)
        {
            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nC00\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nENTITIES\n", StringComparison.Ordinal), StringComparison.Ordinal);
            return text.Insert(end, entities);
        }

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The value of the first group of a code of a record of a drawing.</summary>
        private static string GroupOf(DxfDatabase db, string handle, int code)
        {
            string[] lines = Records(db.ToText()).Single(r => HandleOf(r) == handle).Split('\n');

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                if (int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture) == code)
                {
                    return lines[i + 1];
                }
            }

            return null;
        }

        #region Point 64x: every face of a closed MESH that faces in is turned

        /// <summary>A MESH C01 of its vertices and faces, each face its corners' numbers, as the library writes one.</summary>
        private static DxfEntity Mesh(GeoPoint3[] points, params int[][] faces)
        {
            var groups = new StringBuilder("5=C01 330=17 100=AcDbEntity 8=0 100=AcDbSubDMesh 71=2 72=0 91=0 92=" + points.Length);

            foreach (GeoPoint3 p in points)
            {
                groups.Append(" 10=" + N(p.X) + " 20=" + N(p.Y) + " 30=" + N(p.Z));
            }

            var list = new List<int>();

            foreach (int[] face in faces)
            {
                list.Add(face.Length);
                list.AddRange(face);
            }

            groups.Append(" 93=" + list.Count + string.Concat(list.Select(n => " 90=" + n)) + " 94=0 95=0 90=0");
            return ByHandle<DxfUnknownEntity>(DxfDatabase.Parse(With(Rec("MESH", groups.ToString()))), "C01");
        }

        /// <summary>Checks the 3DFACEs of an explode against faces of the points' numbers, a triangle ending on its third corner.</summary>
        private static void AreFaces(GeoPoint3[] points, IReadOnlyList<DxfEntity> parts, params int[][] faces)
        {
            Assert.Equal(faces.Length, parts.Count);

            for (int f = 0; f < faces.Length; f++)
            {
                var face = Assert.IsType<DxfFace>(parts[f]);

                for (short k = 0; k < 4; k++)
                {
                    GeoPoint3 wanted = points[faces[f][Math.Min((int)k, faces[f].Length - 1)]];
                    Assert.True(wanted.IsEqualTo(face.GetVertexAt(k), Tol), "face " + f + " corner " + k + ": " + face.GetVertexAt(k));
                }
            }
        }

        /// <summary>The square pyramid of 64x's MESH A5: its base 20 by 20 at the height 0 round (250, 0), its apex 10 above.</summary>
        private static readonly GeoPoint3[] Square = { P3(240, -10, 0), P3(260, -10, 0), P3(260, 10, 0), P3(240, 10, 0), P3(250, 0, 10) };

        [Fact]
        public void ASquarePyramidWithItsBaseAndOneSideFacingIn_ExplodesThatQuadAndThatTriangleTurned_AsAutoCadExplodedMeshA5()
        {
            // Plugin v6, 64x-a, MESH A5: the base (0, 1, 2, 3) faces up, into the pyramid, and so does the side (0, 3, 4); the
            // other three sides face out. AutoCAD 2021's Explode gave the quad (3, 2, 1, 0) and the triangle (4, 3, 0), each
            // read from its last corner back, and the three sides as they are held.
            IReadOnlyList<DxfEntity> parts = Mesh(Square, new[] { 0, 1, 2, 3 }, new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 0, 3, 4 }).Explode();

            AreFaces(Square, parts, new[] { 3, 2, 1, 0 }, new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 4, 3, 0 });
        }

        [Fact]
        public void ASquarePyramidWhoseFacesAllFaceOut_ExplodesEachFaceAsItIsHeld()
        {
            // The pyramid of A5 with its base held (3, 2, 1, 0) and its side (4, 3, 0), as AutoCAD keeps them once it has read
            // A5: every face faces out, and each 3DFACE has the corners of its face in their order.
            IReadOnlyList<DxfEntity> parts = Mesh(Square, new[] { 3, 2, 1, 0 }, new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 4, 3, 0 }).Explode();

            AreFaces(Square, parts, new[] { 3, 2, 1, 0 }, new[] { 0, 1, 4 }, new[] { 1, 2, 4 }, new[] { 2, 3, 4 }, new[] { 4, 3, 0 });
        }

        [Fact]
        public void APyramidOnFiveCornersWhoseFacesAllFaceIn_ExplodesEveryFaceTurned_AsAutoCadExplodedMeshA6()
        {
            // Plugin v6, 64x-a, MESH A6: a pyramid on the five corners of a pentagon of radius 10 round (300, 0), its apex 10
            // above, its base (0, 1, 2, 3, 4) and its sides (k + 1, k, 5) all facing in. AutoCAD 2021's Explode broke the base
            // from its last two corners back, (4, 3, 0), (2, 0, 3) and (0, 2, 1), and gave each side turned, (5, k, k + 1).
            var points = new GeoPoint3[6];

            for (int k = 0; k < 5; k++)
            {
                double angle = k * 2.0 * Math.PI / 5.0;
                points[k] = P3(300 + 10 * Math.Cos(angle), 10 * Math.Sin(angle), 0);
            }

            points[5] = P3(300, 0, 10);
            var faces = new List<int[]> { new[] { 0, 1, 2, 3, 4 } };

            for (int k = 0; k < 5; k++)
            {
                faces.Add(new[] { (k + 1) % 5, k, 5 });
            }

            IReadOnlyList<DxfEntity> parts = Mesh(points, faces.ToArray()).Explode();

            AreFaces(points, parts, new[] { 4, 3, 0 }, new[] { 2, 0, 3 }, new[] { 0, 2, 1 }, new[] { 5, 0, 1 }, new[] { 5, 1, 2 }, new[] { 5, 2, 3 }, new[] { 5, 3, 4 }, new[] { 5, 4, 0 });
        }

        #endregion

        #region Point 06: the words of an attribute of several lines

        /// <summary>ezdxf's drawing of R2018 whose block BOLT's definition is made one of three lines, and the ATTRIB of the INSERT of BOLT filled from it.</summary>
        private static DxfDatabase ThreeLines(out DxfAttributeDefinition definition, out DxfAttributeReference mark)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            definition = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            definition.MTextAttributeDefinition = new DxfMText { Contents = "LINE ONE\\PLINE TWO\\PLINE THREE", Location = new GeoPoint3(0, -1, 0), TextHeight = 2.5 };
            DxfBlockReference bolt = Bolt(db);
            mark = bolt.AttributeCollection.Single();
            mark.SetAttributeFromBlock(definition, bolt.BlockTransform);
            return db;
        }

        /// <summary>The INSERT of BOLT in the model space of ezdxf's drawing.</summary>
        private static DxfBlockReference Bolt(DxfDatabase db)
            => Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));

        /// <summary>The ATTRIB of the INSERT of BOLT.</summary>
        private static DxfAttributeReference Mark(DxfDatabase db) => Bolt(db).AttributeCollection.Single();

        [Fact]
        public void TheWordsOfAnAttributeOfSeveralLines_AreThoseOfItsLinesJoinedBySpaces_AsAutoCadReadsTheDefinitionAndTheAttrib()
        {
            // Plugin v6, 06: AutoCAD 2021 read the TextString of the ATTDEF of three lines, whose group 1 is
            // 'LINE ONE\PLINE TWO\PLINE THREE', and of the ATTRIB filled from it, as 'LINE ONE LINE TWO LINE THREE' both.
            DxfDatabase db = ThreeLines(out DxfAttributeDefinition definition, out DxfAttributeReference mark);

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            DxfAttributeDefinition definitionRead = read.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            DxfAttributeReference markRead = Mark(read);

            Assert.Equal("LINE ONE LINE TWO LINE THREE", definitionRead.TextString);
            Assert.Equal("LINE ONE LINE TWO LINE THREE", markRead.TextString);
            Assert.Equal("LINE ONE\\PLINE TWO\\PLINE THREE", GroupOf(read, definition.Handle, 1));
            Assert.Equal("LINE ONE LINE TWO LINE THREE", GroupOf(read, mark.Handle, 1));
            Assert.Equal("LINE ONE\\PLINE TWO\\PLINE THREE", definitionRead.MTextAttributeDefinition.Contents);
        }

        [Fact]
        public void AnAttribOfSeveralLinesWithNoWordsInItsText_ReadsTheWordsOfItsMText_AsAutoCadReadsTheOneItWrites()
        {
            // AutoCAD 2021 writes the group 1 of an ATTRIB of several lines it makes empty (plugin v5, 06) and reads its words
            // from the MTEXT embedded in it.
            DxfDatabase db = ThreeLines(out _, out _);
            string text = db.ToText().Replace("  1\nLINE ONE LINE TWO LINE THREE\n", "  1\n\n");

            DxfAttributeReference read = Mark(DxfDatabase.Parse(text));

            Assert.Equal("LINE ONE LINE TWO LINE THREE", read.TextString);
        }

        [Fact]
        public void TheWordsOfAnAttributeOfSeveralLinesSet_AreTheWordsOfItsMText_ItsPlaceKept()
        {
            // Setting the words of the definition and of the ATTRIB of three lines sets the words of the MTEXT each is made of,
            // its backslashes and braces written as codes, so that the words read back are those set; each stays one of several
            // lines, its MTEXT where it stood.
            DxfDatabase db = ThreeLines(out DxfAttributeDefinition definition, out DxfAttributeReference mark);
            GeoPoint3 definitionAt = definition.MTextAttributeDefinition.Location;
            GeoPoint3 markAt = mark.MTextAttribute.Location;

            definition.TextString = "M24 GALV";
            mark.TextString = "M30 {HOT} C:\\BOLTS";

            Assert.True(definition.IsMTextAttributeDefinition);
            Assert.Equal("M24 GALV", definition.MTextAttributeDefinition.Contents);
            Assert.Equal("M24 GALV", definition.TextString);
            Assert.Equal("M24 GALV", GroupOf(db, definition.Handle, 1));
            Assert.True(definition.MTextAttributeDefinition.Location.IsEqualTo(definitionAt, Tol));
            Assert.True(mark.IsMTextAttribute);
            Assert.Equal("M30 \\{HOT\\} C:\\\\BOLTS", mark.MTextAttribute.Contents);
            Assert.Equal("M30 {HOT} C:\\BOLTS", mark.TextString);
            Assert.Equal("M30 {HOT} C:\\BOLTS", GroupOf(db, mark.Handle, 1));
            Assert.True(mark.MTextAttribute.Location.IsEqualTo(markAt, Tol));

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.Equal("M30 {HOT} C:\\BOLTS", Mark(read).TextString);
            Assert.Equal("M24 GALV", read.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single().TextString);
        }

        #endregion

        #region Point 17-b: a spline made a polyline of arcs

        /// <summary>The splines of point 17 of plugin v6, each as AutoCAD 2021 held it.</summary>
        private static DxfSpline Spline(string name)
        {
            switch (name)
            {
                case "one span":
                    return new DxfSpline(3, false, false, false, new[] { P3(0, 0, 0), P3(10, 10, 0), P3(20, 10, 0), P3(30, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 0.0, 0.0);
                case "two spans":
                    return new DxfSpline(3, false, false, false, new[] { P3(0, 40, 0), P3(10, 50, 0), P3(20, 50, 0), P3(30, 40, 0), P3(40, 30, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 2, 2, 2 }, null, 0.0, 0.0);
                case "three spans":
                    return new DxfSpline(3, false, false, false, new[] { P3(0, -40, 0), P3(10, -30, 0), P3(20, -30, 0), P3(30, -40, 0), P3(40, -50, 0), P3(50, -40, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }, null, 0.0, 0.0);
                case "degree 2":
                    return new DxfSpline(2, false, false, false, new[] { P3(0, -120, 0), P3(10, -110, 0), P3(20, -110, 0), P3(30, -120, 0), P3(40, -130, 0) }, new[] { 0.0, 0, 0, 1, 2, 3, 3, 3 }, null, 0.0, 0.0);
                default:
                    return new DxfSpline(3, false, false, false, new[] { P3(0, -200, 0), P3(10, -190, 0), P3(20, -190, 0), P3(30, -200, 0), P3(40, -210, 0), P3(50, -200, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 2, 2, 2, 2 }, null, 0.0, 0.0);
            }
        }

        [Theory]
        [InlineData("one span", "0:33 10:257 99:4097")]
        [InlineData("two spans", "0:50 10:513 99:4750")]
        [InlineData("three spans", "0:78 10:835 99:7562")]
        [InlineData("degree 2", "0:29 10:342 99:2732")]
        [InlineData("double knot", "0:76 10:933 99:8011")]
        public void ASplineMadeAPolylineOfArcs_HasTheVerticesOfItsPolylineOfLines_AsAutoCadGivesThem(string name, string counts)
        {
            // Plugin v6, 17-b: AutoCAD 2021's ToPolylineWithPrecision(p, true, true) of each spline had as many vertices as
            // ToPolylineWithPrecision(p, false, true), at the same points: 33, 257 and 4 097 for the cubic of one span at 0, 10
            // and 99; the quadratic of three spans and the cubic of a double knot are new in v6.
            DxfSpline spline = Spline(name);

            foreach (string pair in counts.Split(' '))
            {
                int precision = int.Parse(pair.Split(':')[0], CultureInfo.InvariantCulture);
                int wanted = int.Parse(pair.Split(':')[1], CultureInfo.InvariantCulture);

                var lines = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precision, false, true));
                var arcs = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precision, true, true));

                Assert.Equal(name + " at " + precision + ": " + wanted + ", " + wanted, name + " at " + precision + ": " + lines.NumberOfVertices + ", " + arcs.NumberOfVertices);

                for (int i = 0; i < wanted; i++)
                {
                    Assert.Equal(0.0, lines.GetBulgeAt(i));
                    Assert.True(arcs.GetPoint2dAt(i).IsEqualTo(lines.GetPoint2dAt(i), Tol), name + " at " + precision + ", vertex " + i);
                }
            }
        }

        [Theory]
        [InlineData("one span", 0, "0:-0.008061895260693201 1:-0.00858053090783035 15:-0.015605969239118582 16:-0.015605969239118582 31:-0.008061895260693201 32:0")]
        [InlineData("one span", 10, "1:-0.0009880764463862885 128:-0.001953087748404437 255:-0.00098038744529271")]
        [InlineData("two spans", 0, "0:-0.004063404428971927 10:-0.008943032331517952 25:-0.018207241047351882 48:-3.149011194866816E-05")]
        [InlineData("three spans", 0, "0:-0.003005579246705369 40:-0.008187364639020476 76:0.006058887650709392")]
        [InlineData("degree 2", 0, "0:-0.006301269729154377 12:-0.04466212890204985 13:-0.04021233903382102 27:0")]
        [InlineData("double knot", 0, "0:-0.007936008093456638 30:-0.015590751321220033 74:0.007965474087371752")]
        public void EachArcOfASplineMadeAPolyline_IsBentByAQuarterOfTheTurnOfTheSplinesTangentAlongIt_AsAutoCadBendsIt(string name, int precision, string bulges)
        {
            // Plugin v6, 17-b: the bulges of AutoCAD 2021's ToPolylineWithPrecision(p, true, true), each the tangent of a quarter
            // of the angle the spline's tangent turns through from the segment's start to its end: on the cubic of one span at
            // 0, -0.00806 from (0, 0) to (0.9375, 0.908), its tangent turning from 45 degrees to 43.15.
            var arcs = Assert.IsType<DxfPolyline>(Spline(name).ToPolylineWithPrecision(precision, true, true));

            foreach (string pair in bulges.Split(' '))
            {
                int vertex = int.Parse(pair.Split(':')[0], CultureInfo.InvariantCulture);
                double wanted = double.Parse(pair.Split(':')[1], NumberStyles.Float, CultureInfo.InvariantCulture);

                Assert.Equal(wanted, arcs.GetBulgeAt(vertex), 12);
            }
        }

        #endregion

        #region $PLINECONVERTMODE, which AutoCAD 2021 does not know

        [Fact]
        public void ADrawingWithoutPlineConvertMode_IsSavedWithoutIt_AndOneGivenItKeepsIt()
        {
            // AutoCAD 2021 logs "Unknown header variable $PLINECONVERTMODE ignored" for 17-d, whose maker gave it the variable;
            // the library writes it only where the drawing has it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfSpline spline = Spline("one span");
            db.ModelSpace.AppendEntity(spline);
            db.ModelSpace.AppendEntity(spline.ToPolylineWithPrecision(10, false, true));

            Assert.DoesNotContain("$PLINECONVERTMODE", db.ToText(), StringComparison.Ordinal);

            db.SetHeader("$PLINECONVERTMODE", new DxfTypedValue(70, "0"));
            Assert.Contains("$PLINECONVERTMODE", DxfDatabase.Parse(db.ToText()).ToText(), StringComparison.Ordinal);
        }

        #endregion
    }
}
