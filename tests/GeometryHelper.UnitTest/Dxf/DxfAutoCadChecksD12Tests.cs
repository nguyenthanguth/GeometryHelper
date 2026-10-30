using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the checks of 30 October 2026: a closed polyline that crosses itself has no
    /// offset (point 79, the maintainer's decision), a face of a PFACE written by GeometryHelper 13.0.0 or 13.1.0 with
    /// AcDbVertex before its AcDbFaceRecord is saved again as AutoCAD reads it (point 58), and a table scaled doubles the text
    /// heights its table style gives as well as its own (point 54). Each drawing is one of ezdxf's or the dimension drawing
    /// of the tests, or built here as text, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD12Tests
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

        /// <summary>The messages logged while an action runs, every level, each after its level.</summary>
        private static List<string> Logged(Action action)
        {
            var written = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;
            bool enabled = GeometryHelperLog.Enable;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add(level + " " + message);
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
                GeometryHelperLog.Enable = enabled;
            }

            return written;
        }

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        /// <summary>A closed LWPOLYLINE of straight segments through the points.</summary>
        private static DxfPolyline Closed(params GeoPoint2[] points)
        {
            var polyline = new DxfPolyline();

            for (int i = 0; i < points.Length; i++)
            {
                polyline.AddVertexAt(i, points[i], 0, 0, 0);
            }

            polyline.Closed = true;
            return polyline;
        }

        /// <summary>The three closed polylines of point 79: the bow tie, the bow tie 0.02 high at its ends, and the square whose bottom curls into a loop.</summary>
        private static GeoPoint2[] Loop79(string name)
        {
            switch (name)
            {
                case "bow tie":
                    return new[] { P(-10, 1), P(10, -1), P(10, 1), P(-10, -1) };
                case "thin bow tie":
                    return new[] { P(-10, 0.01), P(10, -0.01), P(10, 0.01), P(-10, -0.01) };
                default:
                    return new[] { P(0, 0), P(6, 0.06), P(4, 0.06), P(10, 0), P(10, 10), P(0, 10) };
            }
        }

        #region Files AutoCAD still refused (plugin v4)

        [Fact]
        public void ADimensionCopiedIntoR2000_LeavesOutItsVersion280_WhichAutoCadRefusesThere_AndKeepsItInR2018()
        {
            // 29-b and 30-b: "in AcDbRotatedDimension ... Unexpected DXF group code: 280". R2010 brought the dimension's version.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase r2000 = DxfDatabase.Parse(new DxfWriter().ToString());
            DxfDatabase r2018 = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var dimension = ByHandle<DxfRotatedDimension>(source, "4D");
            Assert.Contains("100\nAcDbDimension\n280\n0\n", Records(source.ToText()).Single(r => HandleOf(r) == "4D"), StringComparison.Ordinal);

            DxfEntity old = source.WblockCloneObjects(new DxfEntity[] { dimension }, r2000.ModelSpace).Single();
            DxfEntity kept = source.WblockCloneObjects(new DxfEntity[] { dimension }, r2018.ModelSpace).Single();

            string record = Records(r2000.ToText()).Single(r => HandleOf(r) == old.Handle);
            Assert.Contains("100\nAcDbDimension\n  2\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n280\n", record, StringComparison.Ordinal);
            Assert.Contains("100\nAcDbDimension\n280\n0\n", Records(r2018.ToText()).Single(r => HandleOf(r) == kept.Handle), StringComparison.Ordinal);
            Assert.Equal(dimension.DimensionText, ((DxfDimension)DxfDatabase.Parse(r2000.ToText()).ModelSpace.Single(e => e.Handle == old.Handle)).DimensionText);
        }

        [Fact]
        public void ADimensionStyleCopiedIntoR12_WritesItsTextsAndEveryArrowByName_EmptyWhereItHasNone_AsAutoCadAsksForThem()
        {
            // 55-b and 55-c: "Omitted group 5": AutoCAD's own R12 writes 3, 4, 5, 6 and 7 after the flags, empty where none.
            // The drawing without its lineweights and its polylines exploded, which R12 has not, as the test of point 55 has it.
            string fixture = DxfDimensionDrawings.R2000;
            int blocks = fixture.IndexOf("\n  2\nBLOCKS\n", StringComparison.Ordinal);
            DxfDatabase source = DxfDatabase.Parse(fixture.Substring(0, blocks) + System.Text.RegularExpressions.Regex.Replace(fixture.Substring(blocks), "\n370\n-?[0-9]+\n", "\n"));
            var dimension = ByHandle<DxfRotatedDimension>(source, "4D");

            foreach (DxfBlockTableRecord block in source.BlockTable.Where(b => b.OfType<DxfPolyline>().Any()).ToList())
            {
                foreach (DxfPolyline polyline in block.OfType<DxfPolyline>().ToList())
                {
                    foreach (DxfEntity part in polyline.Explode())
                    {
                        block.AppendEntity(part);
                    }

                    polyline.Erase();
                }
            }

            DxfDimStyleTableRecord style = source.DimStyleTable[dimension.DimensionStyleName];
            style.Dimblk = null;
            style.Dimblk1 = null;
            style.Dimblk2 = null;
            style.Dimldrblk = null;
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            source.WblockCloneObjects(new DxfEntity[] { dimension }, target.ModelSpace);

            string record = Records(target.ToText()).Single(r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\n" + dimension.DimensionStyleName + "\n"));
            Assert.Matches("\n 70\n0\n  3\n[^\n]*\n  4\n[^\n]*\n  5\n\n  6\n\n  7\n\n 40\n", record);
        }

        #endregion

        #region Point 79: the offset of a closed polyline that crosses itself

        [Theory]
        [InlineData("bow tie", 1.0)]
        [InlineData("bow tie", -1.0)]
        [InlineData("thin bow tie", 1.0)]
        [InlineData("thin bow tie", -1.0)]
        [InlineData("curled square", 1.0)]
        [InlineData("curled square", -1.0)]
        public void AClosedPolylineThatCrossesItself_HasNoOffsetEitherWay_AsAutoCadGivesNoneOfTheBowTie(string name, double distance)
        {
            // AutoCAD 2021 gave no curve of either bow tie either way, and of the curled square none inwards and outwards one
            // that ran out to x = 206 and -196 on a square 10 wide; the curl's first and third bottom sides cross at (5, 0.05).
            DxfPolyline polyline = Closed(Loop79(name));

            Assert.Empty(polyline.GetOffsetCurves(distance));
            Assert.Empty(polyline.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, 1), distance));
            Assert.Empty(polyline.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, -1), distance));
        }

        [Fact]
        public void TheOffsetRefused_IsSaidInOneDebugLine()
        {
            DxfPolyline bow = Closed(Loop79("bow tie"));

            string line = Assert.Single(Logged(() => bow.GetOffsetCurves(1.0)));

            Assert.StartsWith("Debug ", line, StringComparison.Ordinal);
            Assert.Contains("crosses itself", line, StringComparison.Ordinal);
        }

        [Fact]
        public void APolylineOfThePlaneAndOneInSpace_ThatCrossThemselves_HaveNoOffsetEither()
        {
            var flat = new DxfPolyline2d(DxfPoly2dType.SimplePoly, Loop79("bow tie").Select(p => P3(p.X, p.Y, 0)), 0.0, true, 0.0, 0.0, null);
            var standing = new DxfPolyline3d(DxfPoly3dType.SimplePoly, Loop79("bow tie").Select(p => P3(2, p.X, p.Y)), true);

            Assert.Empty(flat.GetOffsetCurves(1.0));
            Assert.Empty(flat.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(0, 0, -1), 1.0));
            Assert.Empty(standing.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(1, 0, 0), 1.0));
            Assert.Empty(standing.GetOffsetCurvesGivenPlaneNormal(new GeoVector3(1, 0, 0), -1.0));
        }

        [Fact]
        public void ALoopThatOnlyTouchesItself_AnOpenPolylineThatCrossesItself_AndASquareWithItsFirstCornerAgainAtTheEnd_AreOffsetAsBefore()
        {
            // The corner (5, 0) lies on the bottom side with both its sides above it: the loop touches itself there and does not
            // cross. An open polyline is not a loop, and a corner given twice is one corner.
            DxfPolyline touching = Closed(P(0, 0), P(10, 0), P(10, 10), P(5, 0), P(0, 10));
            var open = new DxfPolyline();
            open.AddVertexAt(0, P(0, 0), 0, 0, 0);
            open.AddVertexAt(1, P(10, 10), 0, 0, 0);
            open.AddVertexAt(2, P(10, 0), 0, 0, 0);
            open.AddVertexAt(3, P(0, 10), 0, 0, 0);
            DxfPolyline repeated = Closed(P(0, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0));

            Assert.NotEmpty(touching.GetOffsetCurves(1.0));
            Assert.NotEmpty(touching.GetOffsetCurves(-1.0));
            Assert.NotEmpty(open.GetOffsetCurves(1.0));
            var grown = (DxfPolyline)Assert.Single(repeated.GetOffsetCurves(1.0));
            Assert.Equal(144.0, grown.Area, 9);
        }

        #endregion

        #region Point 58: a face of a PFACE as older versions of the library wrote it

        // A PFACE of three points and one face, its face record as GeometryHelper 13.0.0 and 13.1.0 wrote it: AcDbVertex before
        // its AcDbFaceRecord, which AutoCAD 2021 refuses ("Class separator for class AcDbFaceRecord expected").
        private static readonly string OldPface =
            Rec("POLYLINE", "5=C01 330=1F 100=AcDbEntity 8=0 100=AcDbPolyFaceMesh 66=1 10=0.0 20=0.0 30=0.0 70=64 71=3 72=1")
            + Rec("VERTEX", "5=C02 330=C01 100=AcDbEntity 8=0 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=0.0 20=0.0 30=0.0 70=192")
            + Rec("VERTEX", "5=C03 330=C01 100=AcDbEntity 8=0 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=10.0 20=0.0 30=0.0 70=192")
            + Rec("VERTEX", "5=C04 330=C01 100=AcDbEntity 8=0 100=AcDbVertex 100=AcDbPolyFaceMeshVertex 10=5.0 20=5.0 30=8.0 70=192")
            + Rec("VERTEX", "5=C05 330=C01 100=AcDbEntity 8=0 100=AcDbVertex 100=AcDbFaceRecord 10=0 20=0 30=0 70=128 71=1 72=2 73=-3")
            + Rec("SEQEND", "5=C06 330=C01 100=AcDbEntity 8=0");

        [Fact]
        public void AFaceOfAPolyfaceMeshWithAcDbVertexBeforeItsMarker_IsSavedWithItsOwnMarkerAlone_EverythingElseAsItWas()
        {
            string text = With(OldPface);

            DxfDatabase db = DxfDatabase.Parse(text);
            string saved;

            using (var stream = new MemoryStream())
            {
                db.Save(stream);
                saved = Encoding.UTF8.GetString(stream.ToArray());
            }

            Differ(text, saved, out List<string> gone, out List<string> come);
            Assert.Equal("C05", HandleOf(Assert.Single(gone)));
            Assert.Equal(
                "  0\nVERTEX\n  5\nC05\n330\nC01\n100\nAcDbEntity\n  8\n0\n100\nAcDbFaceRecord\n 10\n0\n 20\n0\n 30\n0\n 70\n128\n 71\n1\n 72\n2\n 73\n-3\n",
                Assert.Single(come));
            Assert.Equal(saved, db.ToText());
        }

        [Fact]
        public void AFaceOfTheOlderForm_ReadsItsCornersAsBefore_AndAMeshOfAutoCadsFormIsSavedByteForByte()
        {
            DxfDatabase db = DxfDatabase.Parse(With(OldPface));
            DxfFaceRecord face = ByHandle<DxfPolyFaceMesh>(db, "C01").OfType<DxfFaceRecord>().Single();

            Assert.Equal(new short[] { 1, 2, 3, 0 }, Enumerable.Range(0, 4).Select(i => face.GetVertexAt((short)i)).ToArray());
            Assert.False(face.IsEdgeVisibleAt(2));

            string current = With(OldPface.Replace("100\nAcDbVertex\n100\nAcDbFaceRecord\n", "100\nAcDbFaceRecord\n"));
            Assert.Equal(current, DxfDatabase.Parse(current).ToText());
        }

        #endregion

        #region Point 54: a table scaled takes the text heights of its table style with it

        /// <summary>The dimension drawing of a version, its table F6 of rows 10, 8 and 8 naming no table style, as point 54's.</summary>
        private static string TableDrawing(string version) => (version == "R2000" ? DxfDimensionDrawings.R2000 : DxfDimensionDrawings.R2018).Replace("\r\n", "\n");

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        /// <summary>The table's own part after its last cell: the groups from the first after the last cell's value or overrides to the end of its record.</summary>
        private static string TableRecord(DxfDatabase db) => Records(db.ToText()).Single(r => r.StartsWith("  0\nACAD_TABLE\n", StringComparison.Ordinal));

        [Theory]
        [InlineData("R2000")]
        [InlineData("R2018")]
        public void ATableScaledByTwo_DoublesTheHeightsOfAutoCadsStandardTableStyle_AsItsOwnForEachKindOfRow(string version)
        {
            // Point 54: no table style in the drawing, of millimetres ($MEASUREMENT 1), so AutoCAD's Standard gives the title 6
            // and the header and the data 4.5; AutoCAD 2021's TransformBy by 2 read them 12 and 9, the cell (1, 0) its own 7.
            DxfDatabase db = DxfDatabase.Parse(TableDrawing(version));
            DxfTable table = Table(db);

            table.TransformBy(GeoTransform3.Scaling(new GeoPoint3(300, -100, 0), 2.0));

            string record = TableRecord(db);
            Assert.EndsWith("140\n12\n140\n9\n140\n9\n", record, StringComparison.Ordinal);
            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(
                new[] { DxfTableStyleOverride.TitleRowTextHeight, DxfTableStyleOverride.HeaderRowTextHeight, DxfTableStyleOverride.DataRowTextHeight },
                read.TableStyleOverrides());
            Assert.Equal(7.0, read.TextHeight(1, 0));
            Assert.Null(read.TextHeight(2, 0));
            Assert.Equal(20.0, read.RowHeight(0));
        }

        [Fact]
        public void ATableNamingAStyleOfItsDrawing_TakesThatStylesHeights_ItsDataTitleAndHeaderInTheOrderTheStyleGivesThem()
        {
            // The style gives the data 2.5, the title 5 and the header 3.5, in that order, as AutoCAD writes its Standard.
            string style = Rec("TABLESTYLE", "5=200 330=15 100=AcDbTableStyle 280=0 3=Parts 70=0 71=0 40=1.5 41=1.5 280=0 281=0 7=Standard 140=2.5 170=2 62=0 63=7 283=0 90=512 91=0 7=Standard 140=5.0 170=5 62=0 63=7 283=0 90=512 91=0 7=Standard 140=3.5 170=5 62=0 63=7 283=0 90=512 91=0");
            string text = TableDrawing("R2018")
                .Replace("  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n", "  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n  3\nParts\n350\n200\n" + style)
                .Replace("100\nAcDbTable\n280\n0\n343\nE6\n", "100\nAcDbTable\n280\n0\n342\n200\n343\nE6\n")
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\n201\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            Assert.Equal("Parts", table.TableStyleName);

            table.TransformBy(GeoTransform3.Scaling(3.0));

            Assert.EndsWith("140\n15\n140\n10.5\n140\n7.5\n", TableRecord(db), StringComparison.Ordinal);
        }

        [Fact]
        public void ATableWithItsOwnTitleHeight_KeepsItsOwnScaled_AndADrawingOfInches_TakesAutoCadsStandardOfInches()
        {
            // The table's own title height 5 (93 bit 20, its 140 after the last cell) is doubled as it is, and the header and
            // the data take the Standard of a drawing of inches ($MEASUREMENT 0), 0.18, doubled 0.36.
            string text = TableDrawing("R2018")
                .Replace("$MEASUREMENT\n 70\n1\n", "$MEASUREMENT\n 70\n0\n")
                .Replace(" 92\n2\n 93\n0\n 94\n0\n", " 92\n2\n 93\n1048576\n 94\n0\n");
            int end = text.IndexOf("  0\nENDSEC\n", text.IndexOf("\nACAD_TABLE\n  5\nF6\n", StringComparison.Ordinal), StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text.Insert(end, "140\n5.0\n"));

            Table(db).TransformBy(GeoTransform3.Scaling(2.0));

            Assert.EndsWith("140\n10\n140\n0.36\n140\n0.36\n", TableRecord(db), StringComparison.Ordinal);
            Assert.Contains("\n 93\n7340032\n", TableRecord(db), StringComparison.Ordinal);
        }

        [Fact]
        public void ATableMovedOrMirrored_IsGivenNoHeightOfItsStyle()
        {
            DxfDatabase db = DxfDatabase.Parse(TableDrawing("R2018"));
            DxfTable table = Table(db);

            table.TransformBy(GeoTransform3.Translation(new GeoVector3(5, 0, 0)));
            table.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(280, 0, 0), new GeoVector3(1, 0, 0))));

            Assert.Empty(table.TableStyleOverrides());
            Assert.True(table.Position.IsEqualTo(new GeoPoint3(185, -100, 0), Tol));
        }

        [Theory]
        [InlineData(0.0, -150.0, 0.0, 1.0, 300.0, -200.0, 1.0, 0.0)]
        [InlineData(280.0, -100.0, 1.0, -1.0, 280.0, -80.0, 0.0, 1.0)]
        public void ATableMirroredInALevelLineOrOneAt45Degrees_StandsAtTheImageOfItsPoint_ItsRowsRunningUp_AsAutoCadLeavesIt(
            double x, double y, double nx, double ny, double px, double py, double dx, double dy)
        {
            // Plugin v4, 54x: AutoCAD 2021's TransformBy of the table at (300, -100) mirrored in y = -150 stands at (300, -200)
            // along (1, 0, 0), and in the line at 45 degrees through (280, -100) at (280, -80) along (0, 1, 0), its normal
            // (0, 0, 1), its flow up (70 of 1, flag 0x04 of 93), as its own saves have it.
            DxfDatabase db = DxfDatabase.Parse(TableDrawing("R2018"));
            DxfTable table = Table(db);

            table.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(x, y, 0), new GeoVector3(nx, ny, 0))));

            DxfTable read = Table(DxfDatabase.Parse(db.ToText()));
            Assert.True(read.Position.IsEqualTo(new GeoPoint3(px, py, 0), Tol), read.Position.ToString());
            Assert.True(read.Direction.IsEqualTo(new GeoVector3(dx, dy, 0), Tol), read.Direction.ToString());
            Assert.True(read.Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal(DxfFlowDirection.BottomToTop, read.FlowDirection);
            Assert.Contains("\n 93\n4\n 94\n0\n 95\n0\n 96\n0\n 70\n1\n141\n", TableRecord(db), StringComparison.Ordinal);
            Assert.Equal(new[] { DxfTableStyleOverride.FlowDirection }, read.TableStyleOverrides());
        }

        #endregion

        #region Points 52 and 61: the fit data of an exploded leader's spline

        [Fact]
        public void ASplinedLeaderExploded_GivesItsSplineTheFitPointsAndTangentsAutoCadGivesIt_ItsControlPointsAsBefore()
        {
            // 52, leader 98 moved to the origin: AutoCAD 2021's SPLINE has 74 of 3, the fit points (1.77, 1.77), (10, 10) and
            // (20, 10), and the tangents (0.7071, 0.7071) and (1, 0), its control points those of the library (D11).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(P3(0, 0, 0));
            leader.AppendVertex(P3(10, 10, 0));
            leader.AppendVertex(P3(20, 10, 0));
            db.ModelSpace.AppendEntity(leader);
            leader.IsSplined = true;
            leader.Dimasz = 2.5;

            var spline = Assert.IsType<DxfSpline>(leader.Explode()[1]);

            Assert.Equal(3, spline.NumFitPoints);
            GeoPoint3[] fit = { P3(1.767767, 1.767767, 0), P3(10, 10, 0), P3(20, 10, 0) };

            for (int i = 0; i < 3; i++)
            {
                Assert.True(fit[i].DistanceTo(spline.GetFitPointAt(i)) <= 1E-6, spline.GetFitPointAt(i).ToString());
            }

            Assert.True(spline.StartFitTangent.IsEqualTo(new GeoVector3(Math.Sqrt(0.5), Math.Sqrt(0.5), 0), Tol));
            Assert.True(spline.EndFitTangent.IsEqualTo(new GeoVector3(1, 0, 0), Tol));
            Assert.Equal(5, spline.NumControlPoints);
            Assert.True(P3(8.476311, 12.357023, 0).DistanceTo(spline.NurbsData.GetControlPoints()[2]) <= 1E-6);
            Assert.Equal(0, leader.Spline.NumFitPoints);
        }

        #endregion

        #region Plugin v4: the snap of R12, emptied groups, exploded meshes

        [Fact]
        public void TheSnapAngleOfAViewportOfR12_IsWrittenAndReadInRadians_AsAutoCadWritesIt()
        {
            // 33-b: AutoCAD 2021 read the library's 30 as 30 radians, and writes a snap of pi/6 as 0.5235987755982988.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "35");

            viewport.SnapAngle = Math.PI / 6.0;

            Assert.Contains("1040\n0.5235987755982988\n1040\n5", Records(db.ToText()).Single(r => HandleOf(r) == "35"), StringComparison.Ordinal);
            Assert.Equal(Math.PI / 6.0, ByHandle<DxfViewport>(DxfDatabase.Parse(db.ToText()), "35").SnapAngle, 15);
        }

        [Fact]
        public void AnAnonymousGroupEmptied_IsKeptOnSaving_AsAutoCadKeepsIt()
        {
            // 27x: AutoCAD 2021 kept *A1, *A2 and *A3, emptied or made empty, anonymous, after saving and opening again.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var anonymous = (DxfGroup)db.GroupDictionary["*A1"];
            var made = new DxfGroup("never filled", true);
            db.GroupDictionary.SetAt("*", made);

            anonymous.Clear();

            DxfDatabase read = DxfDatabase.Parse(db.ToText());
            Assert.False(anonymous.IsErased);
            Assert.False(made.IsErased);
            Assert.All(new[] { anonymous.Name, made.Name }, name => Assert.True(((DxfGroup)read.GroupDictionary[name]).IsAnonymous));
            Assert.All(new[] { anonymous.Name, made.Name }, name => Assert.Equal(0, ((DxfGroup)read.GroupDictionary[name]).NumEntities));
        }

        [Fact]
        public void AFaceOfAPolyfaceMeshOfItsOwnColour_ExplodesInTheMeshsColour_AsAutoCadGivesIt()
        {
            // 58: AutoCAD 2021's Explode gave the face of colour 1 of its own the mesh's BYLAYER.
            string text = With(OldPface.Replace("  8\n0\n100\nAcDbVertex\n100\nAcDbFaceRecord\n", "  8\n0\n 62\n1\n100\nAcDbFaceRecord\n"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Assert.Contains("\n 62\n1\n100\nAcDbFaceRecord\n", text, StringComparison.Ordinal);

            DxfFace face = Assert.IsType<DxfFace>(Assert.Single(ByHandle<DxfPolyFaceMesh>(db, "C01").Explode()));

            Assert.Equal(256, face.ColorIndex);
        }

        [Fact]
        public void AMeshSmoothedAtSixAndSix_ExplodesAsAGridOfSixBySixOfItsPoints_AsAutoCadExplodesIt()
        {
            // 58, mesh 9B: AutoCAD 2021's Explode of the 7 by 7 points of the open mesh 4 by 4 smoothed at 6 and 6: 25 faces,
            // the first of the points 0, 1, 7 and 6, the last of 28, 29, 35 and 34.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var grid = new List<GeoPoint3>();

            for (int i = 0; i < 16; i++)
            {
                grid.Add(new GeoPoint3(i % 4 * 10, i / 4 * 10, i == 5 || i == 6 || i == 9 || i == 10 ? 8 : 0));
            }

            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 4, 4, grid, false, false);
            db.ModelSpace.AppendEntity(mesh);
            mesh.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 6, 6);
            List<GeoPoint3> points = mesh.Cast<DxfPolygonMeshVertex>().Skip(16).Select(v => v.Position).ToList();

            DxfFace[] faces = mesh.Explode().Cast<DxfFace>().ToArray();

            Assert.Equal(25, faces.Length);
            int[][] expected = { new[] { 0, 1, 7, 6 }, new[] { 28, 29, 35, 34 } };
            DxfFace[] ends = { faces[0], faces[24] };

            for (int f = 0; f < 2; f++)
            {
                for (short k = 0; k < 4; k++)
                {
                    Assert.True(points[expected[f][k]].IsEqualTo(ends[f].GetVertexAt(k), Tol), "face " + f + " corner " + k);
                }
            }
        }

        [Theory]
        [InlineData(5, false, "0,1,4 2,4,1 4,2,3")]
        [InlineData(6, false, "0,1,5 2,5,1 5,2,4 3,4,2")]
        [InlineData(6, true, "5,4,0 3,0,4 0,3,1 2,1,3")]
        public void AMeshFaceOfManyCorners_BreaksIntoAutoCadsStripOfTriangles_AloneOrAsTheBaseOfAPyramid(int corners, bool pyramid, string triangles)
        {
            // Plugin v4, 64x: AutoCAD 2021's Explode of a MESH of one face of five or six corners, and of the base of a pyramid
            // on six corners, each triangle given as the numbers of its corners in the face.
            var groups = new StringBuilder("5=C01 330=17 100=AcDbEntity 8=0 100=AcDbSubDMesh 71=2 72=0 91=0 92=" + (corners + (pyramid ? 1 : 0)));
            var place = new List<GeoPoint3>();

            for (int k = 0; k < corners; k++)
            {
                double angle = 2.0 * Math.PI * k / corners;
                place.Add(P3(Math.Round(10 * Math.Cos(angle), 6), Math.Round(10 * Math.Sin(angle), 6), 0));
                groups.Append(" 10=" + place[k].X.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 20=" + place[k].Y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 30=0");
            }

            var list = new List<int> { corners };
            list.AddRange(Enumerable.Range(0, corners));

            if (pyramid)
            {
                groups.Append(" 10=0 20=0 30=10");

                for (int k = 0; k < corners; k++)
                {
                    list.AddRange(new[] { 3, k, (k + 1) % corners, corners });
                }
            }

            groups.Append(" 93=" + list.Count + string.Concat(list.Select(n => " 90=" + n)) + " 94=0 95=0 90=0");
            DxfDatabase db = DxfDatabase.Parse(With(Rec("MESH", groups.ToString())));

            DxfFace[] faces = ByHandle<DxfUnknownEntity>(db, "C01").Explode().Cast<DxfFace>().ToArray();

            string[] wanted = triangles.Split(' ');
            Assert.Equal(wanted.Length + (pyramid ? corners : 0), faces.Length);

            for (int f = 0; f < wanted.Length; f++)
            {
                int[] numbers = wanted[f].Split(',').Select(int.Parse).ToArray();

                for (short k = 0; k < 4; k++)
                {
                    Assert.True(place[numbers[Math.Min((int)k, 2)]].IsEqualTo(faces[f].GetVertexAt(k), Tol), "triangle " + f + " corner " + k);
                }
            }
        }

        #endregion
    }
}
