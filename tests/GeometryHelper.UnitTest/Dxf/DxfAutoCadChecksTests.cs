using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What AutoCAD 2021 itself did with the files of the checks of <c>dxf-autocad-checks.md</c> where it disagreed with the
    /// library: each test holds the library to what AutoCAD read, refused or wrote.
    /// </summary>
    public class DxfAutoCadChecksTests
    {
        /// <summary>The groups of a record of a saved text, as code and value, the codes trimmed.</summary>
        private static List<KeyValuePair<int, string>> Groups(string record)
        {
            string[] lines = record.Split('\n');
            var groups = new List<KeyValuePair<int, string>>();

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                groups.Add(new KeyValuePair<int, string>(int.Parse(lines[i].Trim(), System.Globalization.CultureInfo.InvariantCulture), lines[i + 1]));
            }

            return groups;
        }

        /// <summary>The record of a handle in a drawing's saved text.</summary>
        private static string RecordOf(DxfDatabase db, string handle) => Records(db.ToText()).Single(r => HandleOf(r) == handle);

        /// <summary>The codes of a record from the first of one code on, as far as a count goes.</summary>
        private static int[] CodesAfter(string record, int code, int count)
        {
            List<KeyValuePair<int, string>> groups = Groups(record);
            int at = groups.FindIndex(g => g.Key == code);
            Assert.True(at >= 0, "The record has no group " + code + ".");
            return groups.Skip(at).Take(count).Select(g => g.Key).ToArray();
        }

        #region Point 07: an MTEXT's background

        [Fact]
        public void AnMTextGivenABackground_WritesItsColourAndScale_WithoutWhichAutoCADRefusesTheFile()
        {
            // AutoCAD 2021 opens no file with an MTEXT whose 90 turns the background on and that has no 63 and 45 ("Error
            // Decrypting Data", DxfIn eMissingDxfField); with 63 256 and 45 1.5 it opens and reads them as BYLAYER and 1.5,
            // which is what it reads for an MTEXT that has none of them.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.BackgroundFill = true;

            int[] codes = CodesAfter(RecordOf(db, "73"), 90, 4);
            Assert.Equal(new[] { 90, 63, 45, 441 }, codes);
            List<KeyValuePair<int, string>> groups = Groups(RecordOf(db, "73"));
            Assert.Equal("256", groups.First(g => g.Key == 63).Value.Trim());
            Assert.Equal(1.5, double.Parse(groups.First(g => g.Key == 45).Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("0", groups.First(g => g.Key == 441).Value.Trim());

            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.True(again.BackgroundFill);
            Assert.True(again.BackgroundFillColor.IsByLayer);
            Assert.Equal(1.5, again.BackgroundScaleFactor);
            Assert.True(again.BackgroundTransparency.IsByLayer);

            // A colour and a scale of its own are kept; the colour taken off while the background is on leaves 63 at 256.
            mtext.BackgroundFillColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 2);
            mtext.BackgroundScaleFactor = 2.0;
            mtext.UseBackgroundColor = true;
            Assert.Equal(2, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").BackgroundFillColor.ColorIndex);
            mtext.BackgroundFillColor = null;
            Assert.Equal("256", Groups(RecordOf(db, "73")).First(g => g.Key == 63).Value.Trim());
        }

        [Fact]
        public void AnMTextOfR2004GivenABackground_WritesItsColourAndScale_AndNoTransparencyItsVersionHasNot()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1018"));
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.UseBackgroundColor = true;

            Assert.Equal(new[] { 90, 63, 45 }, CodesAfter(RecordOf(db, "73"), 90, 3));
            Assert.DoesNotContain(Groups(RecordOf(db, "73")), g => g.Key == 441);
        }

        [Fact]
        public void AnMTextGivenAFrameAlone_WritesNoBackgroundColour_AsAutoCADReadsOneWithout()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.ShowBorders = true;

            Assert.DoesNotContain(Groups(RecordOf(db, "73")), g => g.Key == 63 || g.Key == 45);
        }

        #endregion

        #region Point 36: a HATCH of a pattern with a background colour

        [Fact]
        public void AHatchOfAPatternOfNoLinesGivenABackgroundColour_KeepsItsCountOfLines_WithoutWhichAutoCADRefusesTheFile()
        {
            // AutoCAD's DxfIn refuses a pattern HATCH whose 77 is not followed by 78 ("expected group code 78").
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, new double[] { 0, 0, 0, 0 });
            db.ModelSpace.AppendEntity(hatch);
            Assert.Equal(new[] { 77, 78, 98 }, CodesAfter(RecordOf(db, hatch.Handle), 77, 3));

            hatch.BackgroundColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5);

            Assert.Equal(new[] { 77, 78, 98 }, CodesAfter(RecordOf(db, hatch.Handle), 77, 3));
            Assert.Equal(5, ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), hatch.Handle).BackgroundColor.ColorIndex);
        }

        #endregion

        #region Points 13 to 15: the look of the vertices of a POLYLINE

        /// <summary>The records of the vertices of a POLYLINE in a drawing's saved text, from its own record to its SEQEND.</summary>
        private static List<string> VerticesOf(DxfDatabase db, string handle)
        {
            List<string> records = Records(db.ToText());
            int at = records.FindIndex(r => HandleOf(r) == handle);
            var vertices = new List<string>();

            for (int i = at + 1; records[i].StartsWith("  0\nVERTEX\n", StringComparison.Ordinal); i++)
            {
                vertices.Add(records[i]);
            }

            return vertices;
        }

        [Fact]
        public void APolylineOfAColourMadeInCode_GivesItsVerticesItsLook_AsAutoCADsAuditWantsIt()
        {
            // AUDIT of AutoCAD 2021 reports every vertex whose colour is not its POLYLINE's: "AcDb2dVertex(8F) color != owner's".
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0) }, 0, false, 0, 0, null)
            {
                Layer = "GH_INPUT",
                ColorIndex = 7,
                LineWeight = DxfLineWeight.LineWeight035,
            };
            db.ModelSpace.AppendEntity(polyline);

            List<string> vertices = VerticesOf(db, polyline.Handle);
            Assert.Equal(3, vertices.Count);
            Assert.All(vertices, v => Assert.Contains("100\nAcDbEntity\n  8\nGH_INPUT\n 62\n7\n370\n35\n100\nAcDbVertex\n", v, StringComparison.Ordinal));

            // Its colour and lineweight changed after, every vertex follows them.
            polyline.ColorIndex = 1;
            polyline.LineWeight = DxfLineWeight.ByLayer;
            Assert.All(VerticesOf(db, polyline.Handle), v => Assert.Contains("  8\nGH_INPUT\n 62\n1\n370\n-1\n100\nAcDbVertex\n", v, StringComparison.Ordinal));
            Assert.Contains("  8\nGH_INPUT\n 62\n1\n370\n-1\n100\nAcDb2dPolyline\n", RecordOf(db, polyline.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void APolylineOfAColourFittedOrGivenAVertex_GivesTheNewVerticesItsLook()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            db.Splinesegs = 8;
            var flat = new DxfPolyline2d(DxfPoly2dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 0) }, 0, false, 0, 0, null);
            var spatial = new DxfPolyline3d(DxfPoly3dType.SimplePoly, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 1), new GeoPoint3(20, 0, 2), new GeoPoint3(30, 10, 3) }, false);
            var grid = new List<GeoPoint3>();

            for (int i = 0; i < 16; i++)
            {
                grid.Add(new GeoPoint3(i % 4 * 10, i / 4 * 10, i == 5 || i == 6 || i == 9 || i == 10 ? 8 : 0));
            }

            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 4, 4, grid, false, false);

            foreach (DxfEntity entity in new DxfEntity[] { flat, spatial, mesh })
            {
                db.ModelSpace.AppendEntity(entity);
                entity.ColorIndex = 3;
            }

            flat.SplineFit();
            spatial.SplineFit();
            mesh.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 6, 6);
            flat.AppendVertex(new DxfVertex2d(new GeoPoint3(40, 0, 0), 0, 0, 0, 0));

            foreach (DxfEntity entity in new DxfEntity[] { flat, spatial, mesh })
            {
                List<string> vertices = VerticesOf(db, entity.Handle);
                Assert.True(vertices.Count > 8, entity.GetType().Name + " has " + vertices.Count + " vertices.");
                Assert.All(vertices, v => Assert.Contains("\n 62\n3\n100\nAcDbVertex\n", v, StringComparison.Ordinal));
            }
        }

        [Fact]
        public void AnOpenMeshSmoothedAtSixAndSix_HasSevenRowsOfSevenPoints_AsAutoCADsPeditMakes()
        {
            // AutoCAD 2021's PEDIT Smooth of the open mesh 4 by 4 at SURFTYPE 6, SURFU 6 and SURFV 6: 65 vertices, 16 of the
            // frame and 49 of the surface. Its corners stay on the frame's corners, and its explosion has 6 by 6 faces.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var grid = new List<GeoPoint3>();

            for (int i = 0; i < 16; i++)
            {
                grid.Add(new GeoPoint3(i % 4 * 10, i / 4 * 10, i == 5 || i == 6 || i == 9 || i == 10 ? 8 : 0));
            }

            var mesh = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 4, 4, grid, false, false);
            db.ModelSpace.AppendEntity(mesh);

            mesh.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 6, 6);

            List<DxfPolygonMeshVertex> vertices = mesh.Cast<DxfPolygonMeshVertex>().ToList();
            Assert.Equal(65, vertices.Count);
            var tolerance = new Tolerance(1E-9, 1E-9, 1E-9);
            Assert.True(vertices[16].Position.IsEqualTo(new GeoPoint3(0, 0, 0), tolerance));
            Assert.True(vertices[64].Position.IsEqualTo(new GeoPoint3(30, 30, 0), tolerance));
            Assert.Equal(6, mesh.MSurfaceDensity);

            // AutoCAD 2021's Explode reads the 49 points as a grid of 6 by 6 (plugin v4, point 58): 25 faces.
            Assert.Equal(25, mesh.Explode().Count);

            // Closed along N, the 4 points of a row round it: 7 rows of 6, exploded as a grid of 6 by 6 closed along N.
            var ring = new DxfPolygonMesh(DxfPolyMeshType.SimpleMesh, 4, 4, grid, false, true);
            db.ModelSpace.AppendEntity(ring);
            ring.SurfaceFit(DxfPolyMeshType.CubicSurfaceMesh, 6, 6);
            Assert.Equal(16 + 42, ring.Cast<DxfEntity>().Count());
            Assert.Equal(30, ring.Explode().Count);
        }

        [Fact]
        public void TheSplineOfASplineFittedPolyline_IsTheCubicOfItsFrame_AsAutoCADGivesIt()
        {
            // AutoCAD 2021's Polyline2d.Spline of the polyline (0, 0), (10, 10), (20, 0), (30, 10), (40, 0) fitted by a cubic:
            // degree 3, its 5 control points the frame (17 vertices drawn).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            GeoPoint3[] frame = { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(30, 10, 0), new GeoPoint3(40, 0, 0) };
            var polyline = new DxfPolyline2d(DxfPoly2dType.SimplePoly, frame, 0, false, 0, 0, null);
            db.ModelSpace.AppendEntity(polyline);
            polyline.SplineFit(DxfPoly2dType.CubicSplinePoly, 8);

            DxfSpline spline = polyline.Spline;

            Assert.Equal(3, spline.Degree);
            Assert.Equal(frame.Select(Bits), spline.ControlPoints.Select(Bits));
            var tolerance = new Tolerance(1E-9, 1E-9, 1E-9);
            Assert.All(polyline.Where(v => v.VertexType == DxfVertex2dType.SplineFitVertex), v => Assert.True(spline.GetClosestPointTo(v.Position, false).IsEqualTo(v.Position, tolerance)));
        }

        [Fact]
        public void APolyfaceMeshGivenAColour_LeavesTheColourOfItsFacesTheirOwn()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var mesh = new DxfPolyFaceMesh();
            db.ModelSpace.AppendEntity(mesh);
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(0, 0, 0)));
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(10, 0, 0)));
            mesh.AppendVertex(new DxfPolyFaceMeshVertex(new GeoPoint3(0, 10, 0)));
            mesh.AppendFaceRecord(new DxfFaceRecord(1, 2, 3, 0) { ColorIndex = 5 });

            mesh.ColorIndex = 2;

            List<string> vertices = VerticesOf(db, mesh.Handle);
            Assert.Equal(4, vertices.Count);
            Assert.All(vertices.Take(3), v => Assert.Contains("\n 62\n2\n", v, StringComparison.Ordinal));
            Assert.Contains("\n 62\n5\n", vertices[3], StringComparison.Ordinal);
            Assert.DoesNotContain("\n 62\n2\n", vertices[3], StringComparison.Ordinal);
        }

        #endregion

        #region Point 02: a handle of extended data copied into another drawing

        [Fact]
        public void AHandleOfExtendedDataCopiedIntoAnotherDrawing_KeepsItsPlace_NullOrNamingTheCopy()
        {
            // AutoCAD 2021, LINE with GH_P2: 1000 before, 1005 of a CIRCLE, 1000 after, 1070 7. Copied alone: 1005 0; copied
            // with the CIRCLE: the 1005 names the CIRCLE's copy.
            DxfDatabase source = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var circle = new DxfCircle(new GeoPoint3(50, 0, 0), new GeoVector3(0, 0, 1), 5);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 40, 0));
            source.ModelSpace.AppendEntity(circle);
            source.ModelSpace.AppendEntity(line);
            line.XData = new[]
            {
                new DxfTypedValue(1001, "GH_P2"), new DxfTypedValue(1000, "before"), new DxfTypedValue(1005, circle.Handle),
                new DxfTypedValue(1000, "after"), new DxfTypedValue(1070, "7"),
            };

            DxfDatabase alone = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfEntity copy = source.WblockCloneObjects(new DxfEntity[] { line }, alone.ModelSpace).Single();
            Assert.Equal(new[] { "1001 GH_P2", "1000 before", "1005 0", "1000 after", "1070 7" }, copy.XData.Select(g => g.ToString()));

            DxfDatabase together = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(new DxfEntity[] { line, circle }, together.ModelSpace);
            Assert.Equal(new[] { "1001 GH_P2", "1000 before", "1005 " + copies[1].Handle, "1000 after", "1070 7" }, copies[0].XData.Select(g => g.ToString()));
            Assert.Equal("1005 " + copies[1].Handle, ByHandle<DxfLine>(DxfDatabase.Parse(together.ToText()), copies[0].Handle).XData[2].ToString());

            // The CIRCLE copied first, the same.
            DxfDatabase first = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            copies = source.WblockCloneObjects(new DxfEntity[] { circle, line }, first.ModelSpace);
            Assert.Equal("1005 " + copies[0].Handle, copies[1].XData[2].ToString());
        }

        #endregion

        #region Point 06: an attribute of several lines

        [Fact]
        public void AnAttributeOfSeveralLines_WritesEveryLineInItsGroupOne_JoinedBySpaces()
        {
            // AutoCAD 2021's ATTRIB made from a definition of three lines writes 1 = LINE ONE LINE TWO LINE THREE, its embedded
            // MTEXT LINE ONE\PLINE TWO\PLINE THREE.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var block = new DxfBlockTableRecord { Name = "TAG3" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 10));
            var insert = new DxfBlockReference(new GeoPoint3(50, 50, 0), block);
            db.ModelSpace.AppendEntity(insert);
            var attribute = new DxfAttributeReference(new GeoPoint3(42, 54, 0), "LINE ONE", "NOTE", db.TextStyleTable["Standard"]) { Height = 2.5 };
            insert.AttributeCollection.AppendAttribute(attribute);

            attribute.MTextAttribute = new DxfMText { Location = new GeoPoint3(42, 54, 0), TextHeight = 2.5, Contents = "LINE ONE\\PLINE TWO\\PLINE THREE" };

            List<KeyValuePair<int, string>> groups = Groups(RecordOf(db, attribute.Handle));
            Assert.Equal("LINE ONE LINE TWO LINE THREE", groups.First(g => g.Key == 1).Value);
            Assert.Contains(groups, g => g.Key == 1 && g.Value == "LINE ONE\\PLINE TWO\\PLINE THREE");

            // Filled from a definition of the three lines, as AutoCAD made it, the same.
            var definition = new DxfAttributeDefinition(new GeoPoint3(-8, 4, 0), "LINE ONE", "NOTE", "Note", db.TextStyleTable["Standard"]) { Height = 2.5 };
            block.AppendEntity(definition);
            definition.MTextAttributeDefinition = new DxfMText { Location = new GeoPoint3(-8, 4, 0), TextHeight = 2.5, Contents = "LINE ONE\\PLINE TWO\\PLINE THREE" };
            var filled = new DxfAttributeReference(new GeoPoint3(42, 54, 0), "x", "NOTE", db.TextStyleTable["Standard"]) { Height = 2.5 };
            insert.AttributeCollection.AppendAttribute(filled);
            filled.SetAttributeFromBlock(definition, insert.BlockTransform);
            Assert.Equal("LINE ONE LINE TWO LINE THREE", Groups(RecordOf(db, filled.Handle)).First(g => g.Key == 1).Value);
            Assert.Equal("LINE ONE\\PLINE TWO\\PLINE THREE", Groups(RecordOf(db, definition.Handle)).First(g => g.Key == 1).Value);
        }

        #endregion

        #region Point 11: the side a line offsets to

        [Fact]
        public void ALineOffsetByFive_GoesToTheLeftOfItsWay_AsAutoCADOffsetsIt()
        {
            // AutoCAD 2021's Offset(5) of the LINE (0, 0)-(30, 40) starts at (-4, 3, 0); the RAY and XLINE are offset the same way.
            var tolerance = new Tolerance(1E-9, 1E-9, 1E-9);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 40, 0));
            var moved = (DxfLine)line.GetOffsetCurves(5).Single();
            Assert.True(moved.StartPoint.IsEqualTo(new GeoPoint3(-4, 3, 0), tolerance));
            Assert.True(moved.EndPoint.IsEqualTo(new GeoPoint3(26, 43, 0), tolerance));
            Assert.True(((DxfLine)line.GetOffsetCurves(-5).Single()).StartPoint.IsEqualTo(new GeoPoint3(4, -3, 0), tolerance));

            var up = new GeoVector3(0, 0, 1);
            var ray = new DxfRay { UnitDir = new GeoVector3(0.6, 0.8, 0) };
            Assert.True(((DxfRay)ray.GetOffsetCurvesGivenPlaneNormal(up, 5).Single()).BasePoint.IsEqualTo(new GeoPoint3(-4, 3, 0), tolerance));
            var xline = new DxfXline { UnitDir = new GeoVector3(0.6, 0.8, 0) };
            Assert.True(((DxfXline)xline.GetOffsetCurvesGivenPlaneNormal(up, 5).Single()).BasePoint.IsEqualTo(new GeoPoint3(-4, 3, 0), tolerance));
        }

        #endregion

        #region Point 33: the twist of a view of R12

        [Fact]
        public void TheTwistOfAViewOfR12_IsWrittenInRadians_AsAutoCADReadsIt()
        {
            // AutoCAD 2021 read the 30 the library wrote in MVIEW for 30 degrees as 1 718.87 degrees, 30 radians.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            DxfViewport viewport = ByHandle<DxfViewport>(db, "35");

            viewport.TwistAngle = Math.PI / 6.0;

            DxfViewport read = ByHandle<DxfViewport>(DxfDatabase.Parse(db.ToText()), "35");
            Assert.Equal(Math.PI / 6.0, read.TwistAngle, 15);
            double[] reals = read.XData.Where(g => g.TypeCode == 1040).Select(g => double.Parse(g.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.Contains(reals, r => Math.Abs(r - Math.PI / 6.0) < 1E-15);
        }

        #endregion

        #region Point 63: a splined leader as a curve

        [Fact]
        public void ASplinedLeader_RunsOverTheLengthsOfItsChords_AndIsBoxedByItsVertices_AsAutoCADReadsIt()
        {
            // AutoCAD 2021, the splined LEADER through (0, 0, 0), (10, 10, 0) and (20, 10, 0): StartParam 0, EndParam 24.142136,
            // the point at 12.071068 (8.2766504294, 9.1605339059, 0), length 24.373747, box (0, 0, 0) to (20, 10, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.IsSplined = true;

            Assert.Equal(0.0, leader.StartParam);
            Assert.Equal(24.142136, leader.EndParam, 6);
            Assert.True(leader.GetPointAtParameter(12.071067811865476).IsEqualTo(new GeoPoint3(8.2766504294495533, 9.1605339059327378, 0), new Tolerance(1E-6, 1E-6)));
            // The length within two millionths of AutoCAD's: 24.373771, the length of the curve sampled at 200 000 chords.
            double length = leader.GetDistanceAtParameter(leader.EndParam);
            Assert.True(Math.Abs(length - 24.373747) < 24.373747 * 2E-6, length.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            double sampled = 0.0;
            GeoPoint3 last = leader.StartPoint;

            for (int i = 1; i <= 200000; i++)
            {
                GeoPoint3 next = leader.GetPointAtParameter(leader.EndParam * i / 200000.0);
                sampled += last.DistanceTo(next);
                last = next;
            }

            Assert.Equal(sampled, length, 7);
            GeoAabb3 box = leader.GeometricExtents;
            Assert.Equal(Bits(new GeoPoint3(0, 0, 0)), Bits(box.Min));
            Assert.Equal(Bits(new GeoPoint3(20, 10, 0)), Bits(box.Max));
        }

        #endregion

        #region Faults found making the files

        [Fact]
        public void ADimensionCopiedIntoR12_BringsItsDimensionStyleInTheFormOfR12()
        {
            // 55-b and 55-c: the copy into R12 named a DIMSTYLE the drawing did not have.
            string fixture = DxfDimensionDrawings.R2000;
            int blocks = fixture.IndexOf("\n  2\nBLOCKS\n", StringComparison.Ordinal);
            DxfDatabase source = DxfDatabase.Parse(fixture.Substring(0, blocks) + System.Text.RegularExpressions.Regex.Replace(fixture.Substring(blocks), "\n370\n-?[0-9]+\n", "\n"));
            var dimension = ByHandle<DxfRotatedDimension>(source, "4D");
            DxfDimStyleTableRecord style = source.DimStyleTable[dimension.DimensionStyleName];
            style.Dimblk = null;
            style.Dimblk1 = null;
            style.Dimblk2 = null;
            style.Dimldrblk = null;

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

            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
            Assert.False(target.DimStyleTable.Has(dimension.DimensionStyleName));

            var copy = (DxfDimension)source.WblockCloneObjects(new DxfEntity[] { dimension }, target.ModelSpace).Single();

            DxfDatabase read = DxfDatabase.Parse(target.ToText());
            Assert.True(read.DimStyleTable.Has(dimension.DimensionStyleName));
            Assert.Equal(style.Dimasz, read.DimStyleTable[dimension.DimensionStyleName].Dimasz);
            Assert.Equal(style.Dimtxt, read.DimStyleTable[dimension.DimensionStyleName].Dimtxt);
            string record = Records(target.ToText()).Single(r => r.StartsWith("  0\nDIMSTYLE\n", StringComparison.Ordinal) && r.Contains("\n  2\n" + dimension.DimensionStyleName + "\n"));
            Assert.DoesNotContain("\n100\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n340\n", record, StringComparison.Ordinal);
            Assert.Equal(dimension.DimensionStyleName, copy.DimensionStyleName);
        }

        [Fact]
        public void AnAnonymousGroupRenamedByItsDictionary_IsAnonymousNoLonger_AsOneRenamedByItsName()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var line = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0));
            db.ModelSpace.AppendEntity(line);
            var group = new DxfGroup("an unnamed group", true);
            group.SetAnonymous();
            db.GroupDictionary.SetAt("*", group);
            group.Append(line);
            string name = group.Name;
            Assert.True(group.IsAnonymous);

            db.GroupDictionary.SetName(name, "NAMED");

            var read = (DxfGroup)DxfDatabase.Parse(db.ToText()).GroupDictionary.GetAt("NAMED");
            Assert.False(read.IsAnonymous);
            Assert.False(((DxfGroup)db.GroupDictionary.GetAt("NAMED")).IsAnonymous);
        }

        #endregion

        #region Points 69, 70 and 77: layer filters, layer states and a link let go of

        /// <summary>The R2018 drawing whose layer table's extension dictionary holds dictionaries of XRECORDs of the groups given, by name.</summary>
        private static DxfDatabase WithLayerDictionaries(params Tuple<string, string, DxfTypedValue[]>[] records)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            db.LayerTable.CreateExtensionDictionary();
            DxfDBDictionary extension = db.LayerTable.ExtensionDictionary;

            foreach (Tuple<string, string, DxfTypedValue[]> record in records)
            {
                if (!extension.Contains(record.Item1))
                {
                    extension.SetAt(record.Item1, new DxfDBDictionary { TreatElementsAsHard = true });
                }

                ((DxfDBDictionary)extension.GetAt(record.Item1)).SetAt(record.Item2, new DxfXrecord { Data = record.Item3 });
            }

            return DxfDatabase.Parse(db.ToText());
        }

        private static DxfTypedValue V(int code, string value) => new DxfTypedValue(code, value);

        [Fact]
        public void TheFiltersOfAutoCAD2005AndLater_AreReadFromAcLyDictionary_ThenAutoCADsOwn_AsAutoCADListsThem()
        {
            // AutoCAD 2021 keeps the property filter GH_STEEL in ACAD_LAYERFILTERS and in ACLYDICTIONARY (*A1), the group GH_NOTES
            // in ACLYDICTIONARY alone (*A2); its NestedFilters: GH_STEEL, GH_NOTES, All Used Layers, Unreconciled New Layers,
            // Viewport Overrides.
            DxfDatabase db = WithLayerDictionaries(
                Tuple.Create("ACAD_LAYERFILTERS", "GH_STEEL", new[] { V(1, "GH_STEEL"), V(1, "STEEL*"), V(1, "*"), V(1, "*"), V(70, "0"), V(1, "*"), V(1, "*") }),
                Tuple.Create("ACLYDICTIONARY", "*A1", new[] { V(1, "AcLyLayerFilter"), V(90, "1"), V(300, "GH_STEEL"), V(301, "NAME==\"STEEL*\"") }),
                Tuple.Create("ACLYDICTIONARY", "*A2", new[] { V(1, "AcLyLayerGroup"), V(90, "1"), V(300, "GH_NOTES") }));

            DxfLayerFilter root = db.LayerFilters.Root;

            Assert.Equal(new[] { "GH_STEEL", "GH_NOTES", "All Used Layers", "Unreconciled New Layers", "Viewport Overrides" }, root.NestedFilters.Select(f => f.Name));
            Assert.Equal(new[] { false, false, true, true, true }, root.NestedFilters.Select(f => f.DynamicallyGenerated));
        }

        [Fact]
        public void AHiddenLayerStateAndOneOfAnXref_AreToldByTheirRecords_AsAutoCADListsThem()
        {
            // AutoCAD 2021: GH_HIDDEN saved of the mask 0x8001 (91 of 32769); the state of an attached xref names its layers by 8.
            DxfTypedValue[] Plain(int mask, int layerCode, string layer) => new[] { V(91, mask.ToString(System.Globalization.CultureInfo.InvariantCulture)), V(301, string.Empty), V(290, "0"), V(302, "0"), V(layerCode, layer), V(90, "8"), V(62, "7"), V(370, "-3"), V(1, "Color_7"), V(440, "0") };
            DxfDatabase db = WithLayerDictionaries(
                Tuple.Create("ACAD_LAYERSTATES", "70-b-xref-source__GH_XR_STATE", Plain(1535, 8, "70-b-xref-source|XR_WALLS")),
                Tuple.Create("ACAD_LAYERSTATES", "GH_HIDDEN", Plain(32769, 330, "10")),
                Tuple.Create("ACAD_LAYERSTATES", "GH_NOTES_OFF", Plain(1535, 330, "10")),
                Tuple.Create("ACAD_LAYERSTATES", "GH_PLAN", Plain(1535, 330, "10")));
            DxfLayerStateManager states = db.LayerStateManager;

            Assert.Equal(new[] { "GH_NOTES_OFF", "GH_PLAN" }, states.GetLayerStateNames(false, false));
            Assert.Equal(new[] { "GH_HIDDEN", "GH_NOTES_OFF", "GH_PLAN" }, states.GetLayerStateNames(true, false));
            Assert.Equal(new[] { "70-b-xref-source__GH_XR_STATE", "GH_NOTES_OFF", "GH_PLAN" }, states.GetLayerStateNames(false, true));
            Assert.Equal(new[] { "70-b-xref-source__GH_XR_STATE", "GH_HIDDEN", "GH_NOTES_OFF", "GH_PLAN" }, states.GetLayerStateNames(true, true));
            Assert.Equal(states.GetLayerStateNames(true, true), states.GetLayerStateNames());
        }

        #endregion

        #region Points 68 and 72: a new GEODATA, one in paper space, and the angle of north

        [Fact]
        public void AGeoDataMadeInCode_IsAutoCADsNewOne()
        {
            // AutoCAD 2021's new GeoLocationData(): CoordinateTypeUnknown, Meters and Meters, ScaleEstMethodReferencePoint.
            var geo = new DxfGeoData();

            Assert.Equal(DxfTypeOfCoordinates.Unknown, geo.TypeOfCoordinates);
            Assert.Equal(DxfUnits.Meters, geo.HorizontalUnits);
            Assert.Equal(DxfUnits.Meters, geo.VerticalUnits);
            Assert.Equal(DxfScaleEstimationMethod.ReferencePoint, geo.ScaleEstimationMethod);
            Assert.Equal(0.0, geo.NorthDirection);

            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            geo.BlockTableRecord = db.ModelSpace;
            geo.PostToDb();
            DxfGeoData read = DxfDatabase.Parse(db.ToText()).GeoDataObject;
            Assert.Equal(DxfTypeOfCoordinates.Unknown, read.TypeOfCoordinates);
            Assert.Equal(DxfUnits.Meters, read.VerticalUnits);
            Assert.Equal(DxfScaleEstimationMethod.ReferencePoint, read.ScaleEstimationMethod);
        }

        [Fact]
        public void AGeoDataGivenPaperSpace_IsRefused_AsAutoCADRefusesIt()
        {
            // AutoCAD 2021: GeoLocationData.BlockTableRecordId = paper space throws eInvalidInput.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            string text = db.ToText();

            Assert.Throws<ArgumentException>(() => new DxfGeoData { BlockTableRecord = db.PaperSpace });
            Assert.Null(db.GeoDataObject);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void TheAngleOfNorth_IsFromYTowardsX_AsAutoCADGivesIt()
        {
            // AutoCAD 2021's NorthDirection of the vectors (0, 1), (1, 0) and (-1, 0): 0, 90 and -90 degrees.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            var geo = new DxfGeoData { BlockTableRecord = db.ModelSpace };
            geo.PostToDb();

            geo.NorthDirectionVector = new GeoVector2(1, 0);
            Assert.Equal(Math.PI / 2.0, geo.NorthDirection, 15);
            geo.NorthDirectionVector = new GeoVector2(-1, 0);
            Assert.Equal(-Math.PI / 2.0, geo.NorthDirection, 15);
            geo.NorthDirectionVector = new GeoVector2(1, 1);
            Assert.Equal(Math.PI / 4.0, geo.NorthDirection, 15);
        }

        #endregion
    }
}
