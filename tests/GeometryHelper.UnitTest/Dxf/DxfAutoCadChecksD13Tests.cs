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
    /// What followed from AutoCAD 2021's answers to plugin run v5 of the checks of 30 October 2026: a table scaled gives each
    /// cell its height (point 54), a face of a PFACE explodes on the mesh's layer (58), a MESH face of many corners breaks
    /// from its corners turned to face out of a closed mesh (64x), an anonymous group emptied by an erase is kept (27x), and
    /// the definition of an attribute of several lines writes its words with their line breaks (06). Each drawing is one of
    /// ezdxf's or the dimension drawing of the tests, or built here as text, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD13Tests
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

        /// <summary>The groups of a record of a text, in order.</summary>
        private static List<KeyValuePair<int, string>> Groups(string record)
        {
            string[] lines = record.Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(new KeyValuePair<int, string>(int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture), lines[i + 1]));
            }

            return groups;
        }

        private static string RecordOf(DxfDatabase db, string handle) => Records(db.ToText()).Single(r => HandleOf(r) == handle);

        #region Point 54: a table scaled gives each cell the height of its kind of row

        /// <summary>The dimension drawing of a version, its table F6 of rows 10, 8 and 8 naming no table style, as point 54's.</summary>
        private static string TableDrawing(string version) => (version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018).Replace("\r\n", "\n");

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        private static string TableRecord(DxfDatabase db) => Records(db.ToText()).Single(r => r.StartsWith("  0\nACAD_TABLE\n", StringComparison.Ordinal));

        /// <summary>The heights of the table's six cells of its own, row by row, null where a cell has none.</summary>
        private static double?[] CellHeights(DxfTable table)
        {
            var heights = new List<double?>();

            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 2; col++)
                {
                    heights.Add(table.TextHeight(row, col));
                }
            }

            return heights.ToArray();
        }

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ATableScaledByTwo_GivesEachCellWithoutAHeightItsKindOfRowsHeightOfTheStyleScaled_AsAutoCadKeepsIt(string version)
        {
            // Plugin v5, 54: AutoCAD 2021 read the heights D12 wrote after the table's last cell as 0.18, the inch default; its
            // own table scaled by 2 keeps 12, 12, 7, 9, 9 and 9 in its six cells, the title of AutoCAD's Standard 6 and the header
            // and the data 4.5, doubled, the cell (1, 0) its own 3.5 doubled, and writes no flag of the table's own heights.
            DxfDatabase db = DxfDatabase.Parse(TableDrawing(version));

            Table(db).TransformBy(GeoTransform3.Scaling(new GeoPoint3(300, -100, 0), 2.0));

            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(new double?[] { 12, 12, 7, 9, 9, 9 }, CellHeights(read));
            Assert.Empty(read.TableStyleOverrides());
            Assert.DoesNotContain("304\nACVALUE_END\n140\n12\n140\n9\n140\n9\n", TableRecord(db), StringComparison.Ordinal);
            Assert.Equal(20.0, read.RowHeight(0));
        }

        [Fact]
        public void ATableNamingAStyleOfItsDrawing_GivesItsCellsThatStylesHeightsScaled_ItsDataTitleAndHeaderInTheStylesOrder()
        {
            // The style gives the data 2.5, the title 5 and the header 3.5, in that order, as AutoCAD writes its Standard; scaled
            // by 3, the title's cells 15, the header's 10.5 (the cell (1, 0) its own 3.5 tripled) and the data's 7.5.
            string style = Rec("TABLESTYLE", "5=200 330=15 100=AcDbTableStyle 280=0 3=Parts 70=0 71=0 40=1.5 41=1.5 280=0 281=0 7=Standard 140=2.5 170=2 62=0 63=7 283=0 90=512 91=0 7=Standard 140=5.0 170=5 62=0 63=7 283=0 90=512 91=0 7=Standard 140=3.5 170=5 62=0 63=7 283=0 90=512 91=0");
            string text = TableDrawing("R2018")
                .Replace("  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n", "  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n  3\nParts\n350\n200\n" + style)
                .Replace("100\nAcDbTable\n280\n0\n343\nE6\n", "100\nAcDbTable\n280\n0\n342\n200\n343\nE6\n")
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\n201\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).TransformBy(GeoTransform3.Scaling(3.0));

            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(new double?[] { 15, 15, 10.5, 10.5, 7.5, 7.5 }, CellHeights(read));
            Assert.Empty(read.TableStyleOverrides());
        }

        [Fact]
        public void ATableWithItsOwnTitleHeight_GivesItsTitlesCellsThatScaled_AndADrawingOfInchesGivesTheRestAutoCadsStandardOfInches()
        {
            // The table's own title height 5 (93 bit 20, its 140 after the last cell) is doubled where it stands and given to the
            // title's cells; the header and the data take the Standard of a drawing of inches ($MEASUREMENT 0), 0.18, doubled.
            string text = TableDrawing("R2018")
                .Replace("$MEASUREMENT\n 70\n1\n", "$MEASUREMENT\n 70\n0\n")
                .Replace(" 92\n2\n 93\n0\n 94\n0\n", " 92\n2\n 93\n1048576\n 94\n0\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nACAD_TABLE\n  5\nF6\n", StringComparison.Ordinal), StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text.Insert(end, "140\n5.0\n"));

            Table(db).TransformBy(GeoTransform3.Scaling(2.0));

            Assert.EndsWith("304\nACVALUE_END\n140\n0.36\n140\n10\n", TableRecord(db), StringComparison.Ordinal);
            Assert.Contains("\n 93\n1048576\n", TableRecord(db), StringComparison.Ordinal);
            Assert.Equal(new double?[] { 10, 10, 7, 0.36, 0.36, 0.36 }, CellHeights(Table(DxfDatabase.Parse(db.ToText()))));
        }

        #endregion

        #region Point 58(c): a face record of a layer of its own

        [Fact]
        public void AFaceRecordOfALayerOfItsOwn_ExplodesOnThePolyfaceMeshsLayer_AsAutoCadGivesIt()
        {
            // 58-e: AutoCAD 2021's Explode gave every face of the PFACE on INPUT_PFACE_LAYER the mesh's layer, those whose face
            // records stand on FACE_OWN_LAYER too.
            string pface = Rec("POLYLINE", "5=C01 330=17 100=AcDbEntity 8=INPUT_PFACE_LAYER 100=AcDbPolyFaceMesh 66=1 10=0.0 20=0.0 30=0.0 70=64 71=3 72=2")
                + Rec("VERTEX", "5=C02 330=C01 100=AcDbEntity 8=INPUT_PFACE_LAYER 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=0.0 20=0.0 30=0.0 70=192")
                + Rec("VERTEX", "5=C03 330=C01 100=AcDbEntity 8=INPUT_PFACE_LAYER 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=10.0 20=0.0 30=0.0 70=192")
                + Rec("VERTEX", "5=C04 330=C01 100=AcDbEntity 8=INPUT_PFACE_LAYER 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=5.0 20=5.0 30=8.0 70=192")
                + Rec("VERTEX", "5=C05 330=C01 100=AcDbEntity 8=INPUT_PFACE_LAYER 100=AcDbFaceRecord 10=0 20=0 30=0 70=128 71=1 72=2 73=3")
                + Rec("VERTEX", "5=C06 330=C01 100=AcDbEntity 8=FACE_OWN_LAYER 100=AcDbFaceRecord 10=0 20=0 30=0 70=128 71=3 72=2 73=1")
                + Rec("SEQEND", "5=C07 330=C01 100=AcDbEntity 8=INPUT_PFACE_LAYER");
            DxfDatabase db = DxfDatabase.Parse(With(pface));

            DxfFace[] faces = ByHandle<DxfPolyFaceMesh>(db, "C01").Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(2, faces.Length);
            Assert.All(faces, face => Assert.Equal("INPUT_PFACE_LAYER", face.Layer));
        }

        #endregion

        #region Point 64x: a MESH face of many corners, from its corners turned to face out

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

        /// <summary>Checks the 3DFACEs of an explode against triangles of the points' numbers, each ending on its third corner.</summary>
        private static void AreTriangles(GeoPoint3[] points, IReadOnlyList<DxfEntity> parts, params int[][] triangles)
        {
            Assert.Equal(triangles.Length, parts.Count);

            for (int f = 0; f < triangles.Length; f++)
            {
                var face = Assert.IsType<DxfFace>(parts[f]);

                for (short k = 0; k < 4; k++)
                {
                    GeoPoint3 wanted = points[triangles[f][Math.Min((int)k, 2)]];
                    Assert.True(wanted.IsEqualTo(face.GetVertexAt(k), Tol), "triangle " + f + " corner " + k + ": " + face.GetVertexAt(k));
                }
            }
        }

        [Fact]
        public void AnOpenMeshOfTwoFacesOfManyCorners_BreaksEachFromItsFirstTwoCorners_AsAutoCadExplodesIt()
        {
            // Plugin v5, 64x-a, MESH A3: a face of six corners 0 to 5 and one of five (2, 1, 6, 7, 8) sharing the edge 1-2, both
            // facing up; AutoCAD 2021's Explode gave each the strip of a face alone, from its first two corners.
            GeoPoint3[] points =
            {
                P3(120, 0, 0), P3(130, 0, 0), P3(135, 8, 0), P3(130, 16, 0), P3(120, 16, 0), P3(115, 8, 0), P3(140, -4, 0), P3(146, 4, 0), P3(142, 10, 0),
            };

            IReadOnlyList<DxfEntity> parts = Mesh(points, new[] { 0, 1, 2, 3, 4, 5 }, new[] { 2, 1, 6, 7, 8 }).Explode();

            AreTriangles(points, parts, new[] { 0, 1, 5 }, new[] { 2, 5, 1 }, new[] { 5, 2, 4 }, new[] { 3, 4, 2 }, new[] { 2, 1, 8 }, new[] { 6, 8, 1 }, new[] { 8, 6, 7 });
        }

        [Fact]
        public void APyramidWhoseBaseFacesOut_BreaksItsBaseFromItsFirstTwoCorners_AsAutoCadExplodesThePyramidItMade()
        {
            // Plugin v4, 64x: the pyramid AutoCAD 2021 made through its API keeps its base as (5, 4, 3, 2, 1, 0), facing down,
            // out of the pyramid, and its sides (k, k + 1, 6) facing out; its Explode broke the base into (5, 4, 0), (3, 0, 4),
            // (0, 3, 1) and (2, 1, 3), the strip of the base from its first two corners as it keeps them.
            GeoPoint3[] points = { P3(0, 0, 0), P3(10, 0, 0), P3(16, 8, 0), P3(10, 16, 0), P3(0, 16, 0), P3(-6, 8, 0), P3(5, 8, 10) };
            var faces = new List<int[]> { new[] { 5, 4, 3, 2, 1, 0 } };

            for (int k = 0; k < 6; k++)
            {
                faces.Add(new[] { k, (k + 1) % 6, 6 });
            }

            IReadOnlyList<DxfEntity> parts = Mesh(points, faces.ToArray()).Explode();

            AreTriangles(points, parts.Take(4).ToList(), new[] { 5, 4, 0 }, new[] { 3, 0, 4 }, new[] { 0, 3, 1 }, new[] { 2, 1, 3 });
            Assert.Equal(10, parts.Count);
        }

        [Fact]
        public void APyramidWhoseBaseOfFiveFacesIn_BreaksItsBaseTurnedToFaceOut_AsAutoCadExplodedMesh8B()
        {
            // Plugin v3 and v4, 64-a, MESH 8B: the base (0, 1, 2, 3, 4) faces up, into the pyramid, its sides out; AutoCAD 2021's
            // Explode gave (4, 3, 0), (2, 0, 3) and (0, 2, 1), the strip of the base turned over, from (4, 3).
            GeoPoint3[] points = { P3(30, 0, 0), P3(40, 0, 0), P3(43, 9, 0), P3(35, 15, 0), P3(27, 9, 0), P3(35, 7, 8) };
            var faces = new List<int[]> { new[] { 0, 1, 2, 3, 4 } };

            for (int k = 0; k < 5; k++)
            {
                faces.Add(new[] { k, (k + 1) % 5, 5 });
            }

            IReadOnlyList<DxfEntity> parts = Mesh(points, faces.ToArray()).Explode();

            AreTriangles(points, parts.Take(3).ToList(), new[] { 4, 3, 0 }, new[] { 2, 0, 3 }, new[] { 0, 2, 1 });
            AreTriangles(points, parts.Skip(3).Take(1).ToList(), new[] { 0, 1, 5 });
        }

        #endregion

        #region Point 27x: an anonymous group emptied by an erase

        [Fact]
        public void AnAnonymousGroupWhoseOnlyMemberIsErased_IsKeptOnSaving_AsAutoCadKeepsIt()
        {
            // Plugin v5, 27x: AutoCAD 2021 erased the line of the anonymous group GH_ANON_ERASE, saved, opened again, and had the
            // group, anonymous, of no member.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var anonymous = (DxfGroup)db.GroupDictionary["*A1"];
            DxfEntity member = Assert.Single(anonymous.GetAllEntityIds());

            member.Erase();

            string saved = db.ToText();
            Assert.False(anonymous.IsErased);
            Assert.Equal(0, anonymous.NumEntities);
            var read = (DxfGroup)DxfDatabase.Parse(saved).GroupDictionary["*A1"];
            Assert.True(read.IsAnonymous);
            Assert.Equal(0, read.NumEntities);
            Assert.Contains(Records(saved), r => HandleOf(r) == anonymous.Handle);
        }

        #endregion

        #region Point 06: the default value of the definition of an attribute of several lines

        [Fact]
        public void TheDefinitionOfAnAttributeOfSeveralLines_WritesItsWordsWithTheirLineBreaks_AndAnAttribFilledFromItTheWordsJoined()
        {
            // Plugin v5, 06: AutoCAD 2021's own ATTDEF of three lines writes its group 1 'LINE ONE\PLINE TWO\PLINE THREE'; the
            // ATTRIB keeps the words joined by spaces (the maintainer's decision), which AutoCAD reads as its own. The TextString
            // of both is the words joined (plugin v6). Made one of one line, the definition keeps the first.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfAttributeDefinition definition = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

            definition.MTextAttributeDefinition = new DxfMText { Contents = "LINE ONE\\PLINE TWO\\PLINE THREE", Location = new GeoPoint3(0, -1, 0), TextHeight = 2.5 };

            Assert.Equal("LINE ONE\\PLINE TWO\\PLINE THREE", Groups(RecordOf(db, definition.Handle)).First(g => g.Key == 1).Value);
            DxfAttributeDefinition read = DxfDatabase.Parse(db.ToText()).BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            Assert.Equal("LINE ONE LINE TWO LINE THREE", read.TextString);
            Assert.Equal("LINE ONE\\PLINE TWO\\PLINE THREE", read.MTextAttributeDefinition.Contents);

            DxfBlockReference bolt = Everything(db).OfType<DxfBlockReference>().Single(b => b.Owner == db.ModelSpace && b.Name == "BOLT" && !(b is DxfMInsertBlock));
            DxfAttributeReference mark = bolt.AttributeCollection.Single();
            mark.SetAttributeFromBlock(definition, bolt.BlockTransform);
            Assert.Equal("LINE ONE LINE TWO LINE THREE", Groups(RecordOf(db, mark.Handle)).First(g => g.Key == 1).Value);

            definition.IsMTextAttributeDefinition = false;
            Assert.Equal("LINE ONE", definition.TextString);
            Assert.Equal("LINE ONE", Groups(RecordOf(db, definition.Handle)).First(g => g.Key == 1).Value);
        }

        #endregion

        #region Point 17-b: the vertices of a spline made a polyline of a precision

        /// <summary>The three cubics of point 17 (17-c, ONE_SPAN and TWO_SPANS, and after-d9's 17-b), uniform within, clamped at the ends.</summary>
        private static DxfSpline Cubic(string name)
        {
            switch (name)
            {
                case "one span":
                    return new DxfSpline(3, false, false, false, new[] { P3(0, 0, 0), P3(10, 10, 0), P3(20, 10, 0), P3(30, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 1, 1, 1 }, null, 0.0, 0.0);
                case "two spans":
                    return new DxfSpline(3, false, false, false, new[] { P3(0, 40, 0), P3(10, 50, 0), P3(20, 50, 0), P3(30, 40, 0), P3(40, 30, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 2, 2, 2 }, null, 0.0, 0.0);
                default:
                    return new DxfSpline(3, false, false, false, new[] { P3(0, 0, 0), P3(10, 10, 0), P3(20, 10, 0), P3(30, 0, 0), P3(40, -10, 0), P3(50, 0, 0) }, new[] { 0.0, 0, 0, 0, 1, 2, 3, 3, 3, 3 }, null, 0.0, 0.0);
            }
        }

        [Theory]
        [InlineData("one span", "0:33 1:65 2:65 3:129 4:129 5:129 10:257 20:513 30:1025 50:2049 99:4097")]
        [InlineData("two spans", "0:50 1:99 2:144 3:197 4:237 5:288 10:513 20:973 30:1519 50:2413 99:4750")]
        [InlineData("three spans", "0:78 1:156 2:224 5:447 10:835 20:1594 50:3888 99:7562")]
        public void ASplineMadeAPolylineOfAPrecision_HasAsManyVerticesAsAutoCadGivesIt(string name, string counts)
        {
            // Plugin v5, 17-b (one and two spans) and v4 (three): AutoCAD 2021's ToPolylineWithPrecision(p, false, true) of each
            // cubic, the vertices of its LWPOLYLINE at each precision tried.
            DxfSpline spline = Cubic(name);

            foreach (string pair in counts.Split(' '))
            {
                int precision = int.Parse(pair.Split(':')[0], CultureInfo.InvariantCulture);
                int wanted = int.Parse(pair.Split(':')[1], CultureInfo.InvariantCulture);

                var polyline = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precision, false, true));

                Assert.Equal(name + " at " + precision + ": " + wanted, name + " at " + precision + ": " + polyline.NumberOfVertices);
            }
        }

        [Fact]
        public void ASplineMadeAPolyline_HasItsVerticesOnTheSpline_FromItsStartToItsEnd()
        {
            // The two spans at 10: 513 vertices, each on the spline, the first and last at its ends, no segment's middle farther
            // from the spline than 1 / (30 sqrt 5 * 11^2), the bend of its sharpest point, 67.08, by the square of 11.
            DxfSpline spline = Cubic("two spans");

            var polyline = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(10, false, true));

            Assert.True(polyline.StartPoint.IsEqualTo(P3(0, 40, 0), Tol));
            Assert.True(polyline.EndPoint.IsEqualTo(P3(40, 30, 0), Tol));
            double bound = 1.0 / (30 * Math.Sqrt(5) * 121);

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                GeoPoint3 corner = polyline.GetPoint3dAt(i);
                Assert.True(spline.GetClosestPointTo(corner, false).DistanceTo(corner) < 1E-9, "vertex " + i);

                if (i > 0)
                {
                    GeoPoint3 middle = polyline.GetPointAtParameter(i - 0.5);
                    Assert.True(spline.GetClosestPointTo(middle, false).DistanceTo(middle) <= bound, "segment " + (i - 1));
                }
            }
        }

        #endregion
    }
}
