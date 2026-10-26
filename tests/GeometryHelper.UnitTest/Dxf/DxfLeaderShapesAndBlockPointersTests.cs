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
    /// The DXF leftovers D1 to D3 of the plan to 13.0.0: a MULTILEADER met by IntersectWith on the lines of its leaders; a
    /// pointer to a block copied by WblockCloneObjects after the entity that holds it (an ACAD_TABLE's picture and cell block,
    /// the arrow block of an override of a dimension style) naming the copy, whichever comes first; and a LEADER read by
    /// <see cref="DxfReader"/> as its curve, so that an INSERT of a block holding one is met on it.
    /// </summary>
    public class DxfLeaderShapesAndBlockPointersTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        #region The lines of a multileader

        [Fact]
        public void AMultileader_IsMetOnItsLeaderLinesAndDogleg_FromWhereItsArrowPoints_AndNotOnItsWords()
        {
            // B4: words at (800, 0, 0); leader 0 of two lines, from (780, -10, 0) through (790, -5, 0) and from (780, 10, 0), both
            // to its last point (790, -1.25, 0) and its dogleg to (798, -1.25, 0); leader 1 from (850, -10, 0) to (828.38, -1.25, 0)
            // and its dogleg to (820.38, -1.25, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader multileader = ByHandle<DxfMLeader>(db, "B4");

            GeoPoint3 up = Assert.Single(multileader.IntersectWith(Line(785, -3, 795, -3), DxfIntersect.OnBothOperands));
            GeoPoint3 dogleg = Assert.Single(multileader.IntersectWith(Line(794, -5, 794, 5), DxfIntersect.OnBothOperands));
            GeoPoint3 right = Assert.Single(multileader.IntersectWith(Line(824, -5, 824, 5), DxfIntersect.OnBothOperands));
            GeoPoint3 arrow = Assert.Single(multileader.IntersectWith(Line(781, -12, 781, -8), DxfIntersect.OnBothOperands));

            Assert.True(up.IsEqualTo(new GeoPoint3(790, -3, 0), Tol), up.ToString());
            Assert.True(dogleg.IsEqualTo(new GeoPoint3(794, -1.25, 0), Tol), dogleg.ToString());
            Assert.True(right.IsEqualTo(new GeoPoint3(824, -1.25, 0), Tol), right.ToString());
            Assert.True(arrow.IsEqualTo(new GeoPoint3(781, -9.5, 0), Tol), arrow.ToString());
            Assert.Empty(multileader.IntersectWith(Line(805, 0.5, 815, 0.5), DxfIntersect.OnBothOperands));

            IReadOnlyList<object> shapes = multileader.GetGeometry();
            Assert.Equal(3, shapes.Count);
            var first = Assert.IsType<GeoPolyline3>(shapes[0]);
            Assert.Equal(
                new[] { Bits(new GeoPoint3(780, -10, 0)), Bits(new GeoPoint3(790, -5, 0)), Bits(new GeoPoint3(790, -1.25, 0)), Bits(new GeoPoint3(798, -1.25, 0)) },
                first.Vertices.Select(Bits));
            Assert.Same(shapes, multileader.GetGeometry());
            Assert.True(multileader.IsPlanar);
        }

        [Fact]
        public void AMultileaderWithABlock_IsMetOnItsLeaderLine_AndOneOfInvisibleLeadersOnNothing()
        {
            // BA places GKTAG at (900, 0, 0); its line runs from (880, -15, 0) to (882.613, -4.659, 0), its dogleg on to the words.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader multileader = ByHandle<DxfMLeader>(db, "BA");
            DxfLine across = Line(870, -10, 890, -10);

            GeoPoint3 met = Assert.Single(multileader.IntersectWith(across, DxfIntersect.OnBothOperands));

            Assert.Equal(-10.0, met.Y, 9);
            Assert.True(met.X > 880 && met.X < 882.613, met.ToString());
            Assert.Empty(multileader.IntersectWith(Line(895, -5, 905, 5), DxfIntersect.OnBothOperands));

            multileader.LeaderLineType = DxfLeaderType.InVisibleLeader;

            Assert.Empty(multileader.GetGeometry());
            Assert.Empty(multileader.IntersectWith(across, DxfIntersect.OnBothOperands));
        }

        [Fact]
        public void ASplinedMultileader_IsMetOnTheSplinesFittedThroughItsLines_AndAMovedVertexMovesThem()
        {
            // B4 splined: each line a spline from where its arrow points to the end of its dogleg, through its vertices.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader multileader = ByHandle<DxfMLeader>(db, "B4");
            multileader.LeaderLineType = DxfLeaderType.SplineLeader;

            List<GeoSpline3> splines = multileader.GetGeometry().Select(s => Assert.IsType<GeoSpline3>(s)).ToList();

            Assert.Equal(3, splines.Count);
            Assert.Equal(Bits(new GeoPoint3(780, -10, 0)), Bits(splines[0].StartPoint));
            Assert.Equal(Bits(new GeoPoint3(798, -1.25, 0)), Bits(splines[0].EndPoint));
            Assert.True(splines[0].DistanceTo(new GeoPoint3(790, -5, 0), Tol) <= Tol.EqualPoint);
            GeoPoint3 met = Assert.Single(multileader.IntersectWith(Line(783, -7.5, 795, -7.5), DxfIntersect.OnBothOperands));
            Assert.Equal(-7.5, met.Y, 9);
            Assert.True(splines[0].DistanceTo(met, Tol) <= Tol.EqualPoint, met.ToString());

            multileader.SetVertex(multileader.GetLeaderLineIndexes(0)[0], 1, new GeoPoint3(790, -6, 0));

            GeoSpline3 moved = Assert.IsType<GeoSpline3>(multileader.GetGeometry()[0]);
            Assert.True(moved.DistanceTo(new GeoPoint3(790, -6, 0), Tol) <= Tol.EqualPoint);
        }

        #endregion

        #region A block copied after what points to it

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ALeaderInABlock_WhoseArrowOverrideNamesABlockCopiedAfterIt_NamesTheCopy_WhicheverComesFirst(bool insertFirst)
        {
            // W holds the leader 89 with DIMLDRBLK GKTAG and an INSERT of GKTAG, in one order or the other; ezdxf's drawing has no
            // GKTAG. An INSERT of W copied brings W, then GKTAG: the copy of 89 in W names the copy of GKTAG.
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfBlockTableRecord gktag = source.BlockTable["GKTAG"];
            var leader = (DxfLeader)ByHandle<DxfLeader>(source, "89").Clone();
            DxfEntity placed = Holding(source, leader, gktag, insertFirst);
            leader.Dimldrblk = gktag;
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            Assert.False(target.BlockTable.Has("GKTAG"));

            source.WblockCloneObjects(new[] { placed }, target.ModelSpace);

            foreach (DxfDatabase db in new[] { target, DxfDatabase.Parse(target.ToText()) })
            {
                DxfLeader copied = db.BlockTable["W"].OfType<DxfLeader>().Single();
                Assert.Same(db.BlockTable["GKTAG"], copied.Dimldrblk);
                Assert.Contains("1070\n341\n1005\n" + db.BlockTable["GKTAG"].Handle + "\n", Records(db.ToText()).Single(r => HandleOf(r) == copied.Handle), StringComparison.Ordinal);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ALeaderCopiedWithAnInsertOfItsArrowBlock_NamesTheCopy_InEitherOrder(bool insertFirst)
        {
            // The leader 89 with DIMLDRBLK GKTAG and an INSERT of GKTAG copied together by WblockCloneObjects, in one order or the other.
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfBlockTableRecord gktag = source.BlockTable["GKTAG"];
            DxfLeader leader = ByHandle<DxfLeader>(source, "89");
            leader.Dimldrblk = gktag;
            var insert = new DxfBlockReference(new GeoPoint3(50, 0, 0), gktag);
            source.ModelSpace.AppendEntity(insert);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(insertFirst ? new DxfEntity[] { insert, leader } : new DxfEntity[] { leader, insert }, target.ModelSpace);

            DxfLeader copied = copies.OfType<DxfLeader>().Single();
            Assert.Same(target.BlockTable["GKTAG"], copied.Dimldrblk);
            Assert.Equal("GKTAG", copies.OfType<DxfBlockReference>().Single().Name);
            Assert.Single(target.BlockTable.Cast<DxfBlockTableRecord>(), b => b.Name == "GKTAG");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ADimensionInABlock_WhoseArrowOverrideNamesABlockCopiedAfterIt_NamesTheCopy_AndDrawsByTheCopyOfItsPicture(bool insertFirst)
        {
            // W holds a copy of the dimension 4D, which draws by *D1, with DIMBLK BOLT, and an INSERT of BOLT; ezdxf's drawing of
            // objects has no BOLT. The copy in W names the copy of BOLT and draws by the copy of *D1, both copied after W.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfBlockTableRecord bolt = source.BlockTable["BOLT"];
            var dimension = (DxfDimension)ByHandle<DxfDimension>(source, "4D").Clone();
            DxfEntity placed = Holding(source, dimension, bolt, insertFirst);
            dimension.Dimblk = bolt;
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            Assert.False(target.BlockTable.Has("BOLT"));

            source.WblockCloneObjects(new[] { placed }, target.ModelSpace);

            foreach (DxfDatabase db in new[] { target, DxfDatabase.Parse(target.ToText()) })
            {
                DxfDimension copied = db.BlockTable["W"].OfType<DxfDimension>().Single();
                Assert.Same(db.BlockTable["BOLT"], copied.Dimblk);
                Assert.NotNull(copied.DimBlock);
                Assert.StartsWith("*D", copied.DimBlock.Name, StringComparison.Ordinal);
                Assert.Equal(source.BlockTable["*D1"].Count(), copied.DimBlock.Count());
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ADimensionInABlockCopiedIntoR12_NamesTheArrowBlockCopiedAfterIt_AsR12Does(bool insertFirst)
        {
            // W holding the dimension 61 so, copied into a drawing of R12, which names an arrow block of an override by its name
            // (1070 5, 1000 BOLT).
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfBlockTableRecord bolt = source.BlockTable["BOLT"];
            var dimension = (DxfDimension)ByHandle<DxfDimension>(source, "61").Clone();
            DxfEntity placed = Holding(source, dimension, bolt, insertFirst);
            dimension.Dimblk = bolt;
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R12);
            Assert.False(target.BlockTable.Has("BOLT"));

            source.WblockCloneObjects(new[] { placed }, target.ModelSpace);

            DxfDimension copied = target.BlockTable["W"].OfType<DxfDimension>().Single();
            Assert.Contains("1070\n5\n1000\nBOLT\n", Records(target.ToText()).Single(r => HandleOf(r) == copied.Handle), StringComparison.Ordinal);
            DxfDimension read = DxfDatabase.Parse(target.ToText()).BlockTable["W"].OfType<DxfDimension>().Single();
            Assert.Equal("BOLT", read.Dimblk.Name);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ATableInABlock_PointsToTheCopiesOfItsPictureAndOfTheBlockOfItsCell_WhicheverComesFirst(bool insertFirst)
        {
            // W holds a copy of the table F6, which draws by *T12 and shows BOLT in its cell (2, 1), and an INSERT of BOLT. The
            // copy in W points to the copy of *T12 (343) and of BOLT (340), both copied after W, and keeps M12 for SIZE.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfBlockTableRecord bolt = source.BlockTable["BOLT"];
            DxfEntity placed = Holding(source, ByHandle<DxfTable>(source, "F6").Clone(), bolt, insertFirst);
            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);

            source.WblockCloneObjects(new[] { placed }, target.ModelSpace);

            foreach (DxfDatabase db in new[] { target, DxfDatabase.Parse(target.ToText()) })
            {
                DxfTable copied = db.BlockTable["W"].OfType<DxfTable>().Single();
                string record = Records(db.ToText()).Single(r => HandleOf(r) == copied.Handle);
                Assert.Contains("343\n" + copied.BlockTableRecord.Handle + "\n", record, StringComparison.Ordinal);
                Assert.Same(db.BlockTable["BOLT"], copied.GetBlockTableRecord(2, 1));
                Assert.Equal("M12", copied.GetBlockAttributeValue(2, 1, db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
            }
        }

        [Fact]
        public void ATableCopiedAlone_BringsTheBlockOfItsCell_ThoughItsPictureDoesNotInsertIt()
        {
            // *T12 no longer inserts BOLT: the table copied alone brings BOLT by the pointer of its cell, as AutoCAD's deep clone
            // follows it, where it lost the cell's block and the value M12.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);

            foreach (DxfBlockReference insert in source.BlockTable["*T12"].OfType<DxfBlockReference>().ToList())
            {
                insert.Erase();
            }

            DxfDatabase target = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfEntity copy = ByHandle<DxfTable>(source, "F6").Clone();

            target.ModelSpace.AppendEntity(copy);

            foreach (DxfDatabase db in new[] { target, DxfDatabase.Parse(target.ToText()) })
            {
                DxfTable copied = db.ModelSpace.OfType<DxfTable>().Single();
                Assert.Same(db.BlockTable["BOLT"], copied.GetBlockTableRecord(2, 1));
                Assert.Equal("M12", copied.GetBlockAttributeValue(2, 1, db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
            }
        }

        #endregion

        #region The reader reads a leader

        [Fact]
        public void TheReader_ReadsALeaderAsItsCurve_StraightAsThePolylineOfItsVertices_SplinedAsTheSplineItExplodesInto()
        {
            // 89 through (0, 0, 0), (10, 10, 0) and (20, 10, 0); B2 splined; B3 straight, with no arrow.
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });
            DxfDocument plan = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol, PlanAs2D = true });
            DxfDatabase db = DxfDatabase.Parse(text);

            DxfShape straight = read.Shapes.Single(s => s.Handle == "89");
            Assert.Equal("LEADER", straight.Type);
            Assert.Equal(
                new[] { Bits(new GeoPoint3(0, 0, 0)), Bits(new GeoPoint3(10, 10, 0)), Bits(new GeoPoint3(20, 10, 0)) },
                Assert.IsType<GeoPolyline3>(straight.Shape).Vertices.Select(Bits));
            Assert.IsType<GeoPolyline2>(plan.Shapes.Single(s => s.Handle == "89").Shape);
            var spline = Assert.IsType<GeoSpline3>(read.Shapes.Single(s => s.Handle == "B2").Shape);
            var drawn = (DxfSpline)ByHandle<DxfLeader>(db, "B2").Explode()[0];
            Assert.Equal(Assert.IsType<GeoSpline3>(Assert.Single(drawn.GetGeometry())).ControlPoints.Select(Bits), spline.ControlPoints.Select(Bits));
            Assert.Equal(Assert.IsType<GeoSpline3>(Assert.Single(drawn.GetGeometry())).Knots.Select(Bits), spline.Knots.Select(Bits));
            Assert.Contains(read.Shapes, s => s.Handle == "B3" && s.Shape is GeoPolyline3);
            Assert.False(read.Skipped.ContainsKey("LEADER"));

            foreach (string handle in new[] { "89", "B2", "B3" })
            {
                Assert.Equal(Shapes(read, handle), ByHandle<DxfLeader>(db, handle).GetGeometry().Select(Describe));
            }
        }

        [Fact]
        public void AnInsertOfABlockHoldingALeader_IsMetOnTheLeaderPlaced_AndItsExtentsHoldIt()
        {
            // W holds 89; its INSERT at (100, 0, 0) scaled 2 places it through (100, 0, 0), (120, 20, 0) and (140, 20, 0): the line
            // x = 130 meets it at (130, 20, 0), and its box reaches it.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var w = new DxfBlockTableRecord { Name = "W" };
            db.BlockTable.Add(w);
            w.AppendEntity(ByHandle<DxfLeader>(db, "89").Clone());
            var insert = new DxfBlockReference(new GeoPoint3(100, 0, 0), w) { ScaleFactors = new GeoVector3(2, 2, 2) };
            db.ModelSpace.AppendEntity(insert);

            GeoPoint3 met = Assert.Single(insert.IntersectWith(Line(130, -5, 130, 30), DxfIntersect.OnBothOperands));

            Assert.True(met.IsEqualTo(new GeoPoint3(130, 20, 0), Tol), met.ToString());
            var placed = Assert.IsType<GeoPolyline3>(Assert.Single(insert.GetGeometry()));
            Assert.True(placed.Vertices[2].IsEqualTo(new GeoPoint3(140, 20, 0), Tol));
            GeoAabb3 box = insert.GeometricExtents;
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(100, 0, 0), Tol) && box.Max.IsEqualTo(new GeoPoint3(140, 20, 0), Tol), box.ToString());
        }

        [Fact]
        public void ALeaderAppended_GrowsTheExtentsOfTheHeader_AsALineDoes()
        {
            // A drawing of the line from (0, 0, 0) to (1, 1, 0); the leader from (-5, 0, 0) to (90, 70, 0) appended to it.
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(-5, 0, 0));
            leader.AppendVertex(new GeoPoint3(90, 70, 0));

            db.ModelSpace.AppendEntity(leader);

            Assert.Equal(new GeoPoint3(-5, 0, 0), db.Extmin);
            Assert.Equal(new GeoPoint3(90, 70, 0), db.Extmax);
        }

        [Fact]
        public void AnInsertScaledUnlike_PlacesTheSplineItsLeaderIsFittedToInItsBlock()
        {
            // W holds B2, splined; its INSERT scaled 2 along x places the spline fitted where the block has its vertices, as
            // AutoCAD draws the leader in its block and places what it draws: the control points of B2's own spline, scaled.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var w = new DxfBlockTableRecord { Name = "W" };
            db.BlockTable.Add(w);
            DxfLeader leader = ByHandle<DxfLeader>(db, "B2");
            w.AppendEntity(leader.Clone());
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), w) { ScaleFactors = new GeoVector3(2, 1, 1) };
            db.ModelSpace.AppendEntity(insert);

            var own = Assert.IsType<GeoSpline3>(Assert.Single(leader.GetGeometry()));
            var placed = Assert.IsType<GeoSpline3>(Assert.Single(insert.GetGeometry()));

            Assert.Equal(own.ControlPoints.Count, placed.ControlPoints.Count);

            for (int i = 0; i < own.ControlPoints.Count; i++)
            {
                GeoPoint3 p = own.ControlPoints[i];
                Assert.True(placed.ControlPoints[i].IsEqualTo(new GeoPoint3(2 * p.X, p.Y, p.Z), new Tolerance(1E-9, 1E-9, 1E-9)), i + ": " + placed.ControlPoints[i]);
            }

            Assert.Equal(own.Knots.Select(Bits), placed.Knots.Select(Bits));
        }

        [Fact]
        public void ALeaderEdited_GrowsTheExtentsOfTheHeaderByWhereItIsNow()
        {
            // The leader from (-5, 0, 0) to (90, 70, 0), splined, its last vertex moved to (500, 400, 0) and a vertex appended at
            // (600, -50, 0): the extents reach both.
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(-5, 0, 0));
            leader.AppendVertex(new GeoPoint3(90, 70, 0));
            db.ModelSpace.AppendEntity(leader);

            leader.IsSplined = true;
            leader.SetVertexAt(1, new GeoPoint3(500, 400, 0));
            leader.AppendVertex(new GeoPoint3(600, -50, 0));

            Assert.True(db.Extmax.X >= 600 && db.Extmax.Y >= 400 && db.Extmin.X <= -5 && db.Extmin.Y <= -50, db.Extmin + " " + db.Extmax);
            Assert.Equal(Bits(db.Extmax), Bits(DxfDatabase.Parse(db.ToText()).Extmax));
        }

        [Fact]
        public void TheReader_ReadsAMultileaderAsTheLinesOfItsLeaders_TheShapesItsGeometryGives()
        {
            // B4's three leader lines, BA's one; GetGeometry gives what the reader reads, to the bit.
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Equal(3, read.Shapes.Count(s => s.Handle == "B4" && s.Type == "MULTILEADER" && s.Shape is GeoPolyline3));
            Assert.Single(read.Shapes, s => s.Handle == "BA");
            Assert.False(read.Skipped.ContainsKey("MULTILEADER"));

            foreach (string handle in new[] { "B4", "BA" })
            {
                Assert.Equal(Shapes(read, handle), ByHandle<DxfMLeader>(db, handle).GetGeometry().Select(Describe));
            }
        }

        [Fact]
        public void AnInsertOfABlockHoldingAMultileader_IsMetOnItsLinesPlaced()
        {
            // W holds B4; its INSERT at (0, 100, 0) places the line up to the dogleg at x = 790 from y = 95 to y = 98.75: the line
            // y = 97 meets it at (790, 97, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var w = new DxfBlockTableRecord { Name = "W" };
            db.BlockTable.Add(w);
            w.AppendEntity(ByHandle<DxfMLeader>(db, "B4").Clone());
            var insert = new DxfBlockReference(new GeoPoint3(0, 100, 0), w);
            db.ModelSpace.AppendEntity(insert);

            GeoPoint3 met = Assert.Single(insert.IntersectWith(Line(785, 97, 795, 97), DxfIntersect.OnBothOperands));

            Assert.True(met.IsEqualTo(new GeoPoint3(790, 97, 0), Tol), met.ToString());
            Assert.Equal(3, insert.GetGeometry().Count);
        }

        [Fact]
        public void AMultileaderWithoutSubclassMarkers_IsReadAsTheLinesOfItsLeaders_AsOneWithThem()
        {
            // B4 with its markers AcDbEntity and AcDbMLeader taken out: its three lines all the same, to the bit.
            const string Head = "MULTILEADER\n  5\nB4\n330\n17\n100\nAcDbEntity\n  8\n0\n100\nAcDbMLeader\n";
            string text = DxfHatchLeaderDrawings.R2018.Replace("\r\n", "\n");
            Assert.Contains(Head, text, StringComparison.Ordinal);
            DxfDatabase bare = DxfDatabase.Parse(text.Replace(Head, "MULTILEADER\n  5\nB4\n330\n17\n  8\n0\n"));
            DxfDatabase marked = DxfDatabase.Parse(text);

            IReadOnlyList<object> shapes = ByHandle<DxfMLeader>(bare, "B4").GetGeometry();

            Assert.Equal(3, shapes.Count);
            Assert.Equal(ByHandle<DxfMLeader>(marked, "B4").GetGeometry().Select(Describe), shapes.Select(Describe));
        }

        [Fact]
        public void AnInvisibleMultileaderOfABlock_CountsNothingAgainstMaxPlacedEntities()
        {
            // BUD holds a LINE, B4 and a copy of B4 whose leaders are invisible: 4 shapes; two INSERTs of it fit in 8.
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var bud = new DxfBlockTableRecord { Name = "BUD" };
            db.BlockTable.Add(bud);
            bud.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            DxfMLeader b4 = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");
            bud.AppendEntity(b4.Clone());
            var hidden = (DxfMLeader)b4.Clone();
            bud.AppendEntity(hidden);
            hidden.LeaderLineType = DxfLeaderType.InVisibleLeader;
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), bud));
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 0, 0), bud));

            DxfDocument read = DxfReader.Parse(db.ToText(), new DxfReadOptions { MaxPlacedEntities = 8, Tolerance = Tol });

            Assert.Equal(8, read.Shapes.Count(s => s.BlockPath.Count > 0));
            Assert.DoesNotContain(read.Warnings, w => w.Contains("not placed"));
        }

        [Fact]
        public void AnInsertScaledUnlikeExploded_LeavesItsMultileaderOut_AsBefore_ThoughItIsMetOnItsLines()
        {
            // W holds B4 and a LINE; an INSERT of W scaled 2 along x: the multileader cannot hold that placing and is left out of
            // what Explode gives, as it was, not drawn as the lines the reader reads of it; IntersectWith meets those lines.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var w = new DxfBlockTableRecord { Name = "W" };
            db.BlockTable.Add(w);
            w.AppendEntity(ByHandle<DxfMLeader>(db, "B4").Clone());
            w.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), w) { ScaleFactors = new GeoVector3(2, 1, 1) };
            db.ModelSpace.AppendEntity(insert);

            IReadOnlyList<DxfEntity> parts = insert.Explode();

            Assert.IsType<DxfLine>(Assert.Single(parts));
            Assert.Equal(4, insert.GetGeometry().Count);
        }

        [Theory]
        [InlineData("LEADER", 11)]
        [InlineData("MULTILEADER", 10)]
        public void ALeaderOrAMultileaderOfABlock_CountsAgainstMaxPlacedEntities_AsItIsPlaced(string type, int each)
        {
            // BUD holds a LINE and ten leaders, or three copies of B4 of three lines each; two INSERTs of it, the bound that of one.
            DxfDatabase db = DxfDatabase.Parse(new DxfWriter().Add(new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 1, 0))).ToString());
            var bud = new DxfBlockTableRecord { Name = "BUD" };
            db.BlockTable.Add(bud);
            bud.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            DxfMLeader b4 = ByHandle<DxfMLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B4");

            for (int i = 0; i < (type == "LEADER" ? 10 : 3); i++)
            {
                if (type == "LEADER")
                {
                    var leader = new DxfLeader();
                    leader.AppendVertex(new GeoPoint3(i, 0, 0));
                    leader.AppendVertex(new GeoPoint3(i, 5, 0));
                    bud.AppendEntity(leader);
                }
                else
                {
                    bud.AppendEntity(b4.Clone());
                }
            }

            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), bud));
            db.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(100, 0, 0), bud));

            DxfDocument read = DxfReader.Parse(db.ToText(), new DxfReadOptions { MaxPlacedEntities = each, Tolerance = Tol });

            Assert.Equal(each, read.Shapes.Count(s => s.BlockPath.Count > 0));
            Assert.Contains(read.Warnings, w => w.Contains("had they placed the " + each + " more they would have taken"));
        }

        [Fact]
        public void ALeaderReadFirst_SpellsTheLayerOfTheEntitiesAfterIt_WhereThereIsNoLayerTable()
        {
            // The reader names a layer as it first meets it: the LEADER on WALL, then the LINE on Wall, are both on WALL.
            string text = "  0\nSECTION\n  2\nENTITIES\n  0\nLEADER\n  8\nWALL\n100\nAcDbLeader\n 72\n0\n 10\n0\n 20\n0\n 30\n0\n 10\n10\n 20\n5\n 30\n0\n"
                + "  0\nLINE\n  8\nWall\n 10\n0\n 20\n0\n 30\n0\n 11\n1\n 21\n1\n 31\n0\n  0\nENDSEC\n  0\nEOF\n";

            DxfDocument read = DxfReader.Parse(text, new DxfReadOptions { Tolerance = Tol });

            Assert.Equal(new[] { "WALL", "WALL" }, read.Shapes.Select(s => s.Layer));
            Assert.Equal(new[] { "WALL" }, read.Layers);
        }

        [Fact]
        public void ALeaderGivenNoShape_IsLeftAsItWas_AndOneGivenAShape_IsRefusedNamingWhy()
        {
            // As before a LEADER was read: a shape of null changes nothing; a line is refused, the leader not written from one.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            string before = db.ToText();

            leader.Item.Shape = null;

            Assert.Equal(before, db.ToText());
            var refused = Assert.Throws<InvalidOperationException>(() => leader.Item.Shape = new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0)));
            Assert.Contains("a LEADER and a MULTILEADER are read as their lines but not written from a shape", refused.Message, StringComparison.Ordinal);
            Assert.Equal(before, db.ToText());
        }

        #endregion

        /// <summary>A line of the floor from one point to another.</summary>
        private static DxfLine Line(double x1, double y1, double x2, double y2) => new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        /// <summary>
        /// A block W of a drawing holding an entity and an INSERT of a block, the INSERT first where asked, and an INSERT of W in
        /// model space, which is given back.
        /// </summary>
        private static DxfEntity Holding(DxfDatabase db, DxfEntity entity, DxfBlockTableRecord block, bool insertFirst)
        {
            var w = new DxfBlockTableRecord { Name = "W" };
            db.BlockTable.Add(w);
            var insert = new DxfBlockReference(new GeoPoint3(50, 0, 0), block);

            if (insertFirst)
            {
                w.AppendEntity(insert);
            }

            w.AppendEntity(entity);

            if (!insertFirst)
            {
                w.AppendEntity(insert);
            }

            var placed = new DxfBlockReference(new GeoPoint3(0, 0, 0), w);
            db.ModelSpace.AppendEntity(placed);
            return placed;
        }

        /// <summary>The shapes the reader read of the entity of a handle, each as <see cref="Describe"/> writes it.</summary>
        private static IEnumerable<string> Shapes(DxfDocument read, string handle) => read.Shapes.Where(s => s.Handle == handle).Select(s => Describe(s.Shape));

        /// <summary>A shape as its type and the bits of its points.</summary>
        private static string Describe(object shape)
        {
            switch (shape)
            {
                case GeoPolyline3 chain:
                    return "polyline " + string.Join(" ", chain.Vertices.Select(Bits));
                case GeoSpline3 spline:
                    return "spline " + string.Join(" ", spline.ControlPoints.Select(Bits)) + " knots " + string.Join(" ", spline.Knots.Select(Bits));
                default:
                    return shape.GetType().Name;
            }
        }
    }
}
