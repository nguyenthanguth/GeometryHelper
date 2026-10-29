using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The follow-ups after A7: a LEADER met by IntersectWith on its own curve, the polyline of its vertices or the spline it
    /// is fitted to, as AutoCAD's IntersectWith meets a leader; a copy of a MULTILEADER appended back into its drawing after
    /// the attribute definition it gives a value to was erased; a MULTILEADER in a block copied by WblockCloneObjects with the
    /// block it places; and a splined leader fitted anew after an edit.
    /// </summary>
    public class DxfLeaderFollowUpTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        #region The geometry of a leader

        [Fact]
        public void AStraightLeader_IsMetOnThePolylineOfItsVertices_AsItsOwnCurveIs()
        {
            // The leader from (0, 0, 0) to (10, 5, 0) and the line x = 5 from (5, -10, 0) to (5, 10, 0): they meet at (5, 2.5, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var leader = new DxfLeader();
            leader.AppendVertex(new GeoPoint3(0, 0, 0));
            leader.AppendVertex(new GeoPoint3(10, 5, 0));
            var line = new DxfLine(new GeoPoint3(5, -10, 0), new GeoPoint3(5, 10, 0));
            db.ModelSpace.AppendEntity(leader);
            db.ModelSpace.AppendEntity(line);

            GeoPoint3 met = Assert.Single(leader.IntersectWith(line, DxfIntersect.OnBothOperands));
            GeoPoint3 back = Assert.Single(line.IntersectWith(leader, DxfIntersect.OnBothOperands));

            Assert.True(met.IsEqualTo(new GeoPoint3(5, 2.5, 0), Tol), met.ToString());
            Assert.True(back.IsEqualTo(met, Tol), back.ToString());
            Assert.True(leader.GetClosestPointTo(met, false).IsEqualTo(met, Tol));
            var shape = Assert.IsType<GeoPolyline3>(Assert.Single(leader.GetGeometry()));
            Assert.Equal(new[] { Bits(new GeoPoint3(0, 0, 0)), Bits(new GeoPoint3(10, 5, 0)) }, shape.Vertices.Select(Bits));
            Assert.Same(shape, leader.GetGeCurve());
            Assert.True(leader.IsPlanar);
        }

        [Fact]
        public void ALeaderOfThreeVertices_IsMetOnEachSegment_AndOneOffItsPlaneIsNotPlanar()
        {
            // Through (0, 0, 0), (10, 10, 0) and (20, 10, 0), met by the line y = 7 from (-5, 7, 0) to (25, 7, 0) at (7, 7, 0) only,
            // and by the line x = 15 at (15, 10, 0); its last vertex lifted 0.5 off its plane, GetPlane refuses and IsPlanar agrees.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");

            GeoPoint3 low = Assert.Single(leader.IntersectWith(new DxfLine(new GeoPoint3(-5, 7, 0), new GeoPoint3(25, 7, 0)), DxfIntersect.OnBothOperands));
            GeoPoint3 high = Assert.Single(leader.IntersectWith(new DxfLine(new GeoPoint3(15, -5, 0), new GeoPoint3(15, 25, 0)), DxfIntersect.OnBothOperands));

            Assert.True(low.IsEqualTo(new GeoPoint3(7, 7, 0), Tol), low.ToString());
            Assert.True(high.IsEqualTo(new GeoPoint3(15, 10, 0), Tol), high.ToString());
            Assert.True(leader.IsPlanar);

            leader.SetVertexAt(2, new GeoPoint3(20, 10, 0.5));

            Assert.Throws<InvalidOperationException>(() => leader.GetPlane());
            Assert.False(leader.IsPlanar);
        }

        [Fact]
        public void ASplinedLeader_IsMetOnTheSplineItExplodesInto_AndHasNoShapeWhereItsVerticesAreOnePoint()
        {
            // 89 splined through (0, 0, 0), (10, 10, 0) and (20, 10, 0): the line x = 15 meets it where it meets the spline
            // Explode gives, above y = 10, which the straight leader does not reach.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.IsSplined = true;
            var line = new DxfLine(new GeoPoint3(15, -5, 0), new GeoPoint3(15, 25, 0));
            var drawn = (DxfSpline)leader.Explode()[0];

            GeoPoint3 met = Assert.Single(leader.IntersectWith(line, DxfIntersect.OnBothOperands));
            GeoPoint3 expected = Assert.Single(drawn.IntersectWith(line, DxfIntersect.OnBothOperands));

            Assert.Equal(Bits(expected), Bits(met));
            Assert.True(met.Y > 10.0 + 0.1, met.ToString());
            Assert.True(leader.GetClosestPointTo(met, false).IsEqualTo(met, Tol));
            var shape = Assert.IsType<GeoSpline3>(Assert.Single(leader.GetGeometry()));
            Assert.Equal(Assert.IsType<GeoSpline3>(Assert.Single(drawn.GetGeometry())).ControlPoints.Select(Bits), shape.ControlPoints.Select(Bits));
            Assert.True(leader.IsPlanar);

            var dot = new DxfLeader();
            dot.AppendVertex(new GeoPoint3(5, 5, 0));
            dot.AppendVertex(new GeoPoint3(5, 5, 0));
            dot.IsSplined = true;
            db.ModelSpace.AppendEntity(dot);

            Assert.Empty(dot.GetGeometry());
            Assert.Empty(dot.IntersectWith(line, DxfIntersect.OnBothOperands));
        }

        [Fact]
        public void ASplinedLeaderMovedOrStraightened_IsWalkedOnItsNewCurve()
        {
            // The spline is kept between questions and fitted anew once the leader is changed: its middle vertex moved up by 5, the
            // point at the middle of its parameter is that of the spline fitted through the new vertices, its parameter run over
            // the lengths of its chords (check 63); straight again, its polyline.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");
            leader.IsSplined = true;
            GeoPoint3 before = leader.GetPointAtParameter(0.5 * leader.EndParam);
            Assert.Equal(Bits(before), Bits(leader.GetPointAtParameter(0.5 * leader.EndParam)));

            leader.SetVertexAt(1, new GeoPoint3(10, 15, 0));
            GeoPoint3 after = leader.GetPointAtParameter(0.5 * leader.EndParam);

            var drawn = (DxfSpline)leader.Explode()[0];
            Assert.NotEqual(Bits(before), Bits(after));
            Assert.True(drawn.GetPointAtParameter(0.5).IsEqualTo(after, new Tolerance(1E-12, 1E-12)));
            Assert.Equal(Bits(leader.Spline.GetPointAtParameter(0.5 * leader.EndParam)), Bits(after));
            Assert.Equal(Assert.IsType<GeoSpline3>(Assert.Single(drawn.GetGeometry())).ControlPoints.Select(Bits), Assert.IsType<GeoSpline3>(Assert.Single(leader.GetGeometry())).ControlPoints.Select(Bits));

            leader.IsSplined = false;

            Assert.IsType<GeoPolyline3>(Assert.Single(leader.GetGeometry()));
            Assert.True(leader.GetPointAtParameter(0.5).IsEqualTo(new GeoPoint3(5, 7.5, 0), Tol));
        }

        [Fact]
        public void ASplinedLeaderOfManyVertices_GivesThePointsOfTheSplineItExplodesInto_EntitiesMadeElsewhereBetween()
        {
            // 4 000 vertices, the spline kept between questions while lines are made in no file between them, which change
            // the file such entities are held in: each point is that of the spline Explode fits, its parameter run over the
            // lengths of the chords (check 63), to the rounding.
            var text = new StringBuilder("  0\nSECTION\n  2\nENTITIES\n  0\nLEADER\n  5\nC10\n100\nAcDbEntity\n  8\n0\n100\nAcDbLeader\n  3\nStandard\n 71\n1\n 72\n1\n 73\n3\n 76\n4000\n");

            for (int i = 0; i < 4000; i++)
            {
                text.Append(" 10\n" + (i * 10).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n 20\n" + ((i % 2) * 5).ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n 30\n0.0\n");
            }

            var leader = (DxfLeader)DxfDatabase.Parse(text.Append("  0\nENDSEC\n  0\nEOF\n").ToString()).ModelSpace.First();
            var points = new GeoPoint3[400];

            for (int i = 0; i < 400; i++)
            {
                points[i] = leader.GetPointAtParameter(i / 399.0 * leader.EndParam);
                Assert.NotNull(new DxfLine(new GeoPoint3(i, 0, 0), new GeoPoint3(i, 1, 0)));
            }

            var drawn = (DxfSpline)leader.Explode()[0];
            var rounding = new Tolerance(1E-8, 1E-8);

            Assert.True(drawn.GetPointAtParameter(100 / 399.0).IsEqualTo(points[100], rounding));
            Assert.True(drawn.GetPointAtParameter(1.0).IsEqualTo(points[399], rounding));
        }

        [Fact]
        public void ALeaderOfAColourItsSettersRefuse_RefusesToExplode_ThoughItsStyleDrawsItInAnother()
        {
            // B3 with the colour 257 of its own, which no setter takes, and DIMCLRD 7 of its overrides: giving its parts its own
            // look first, as Explode always did, refuses it as before, straight or splined.
            DxfLeader leader = B3With(" 62\n257\n");
            leader.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7);

            Assert.Contains("colour index", Assert.Throws<ArgumentOutOfRangeException>(() => leader.Explode()).Message, StringComparison.Ordinal);

            leader.IsSplined = true;

            Assert.Contains("colour index", Assert.Throws<ArgumentOutOfRangeException>(() => leader.Explode()).Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ALeaderOfALineweightItsSettersRefuse_RefusesToExplode_ThoughItsStyleDrawsItInAnother()
        {
            // B3 with the lineweight 7 of its own, which no setter takes, and DIMLWD 0.25 of its overrides: refused as before.
            DxfLeader leader = B3With("370\n7\n");
            leader.Dimlwd = DxfLineWeight.LineWeight025;

            Assert.Contains("lineweight", Assert.Throws<ArgumentOutOfRangeException>(() => leader.Explode()).Message, StringComparison.Ordinal);

            leader.IsSplined = true;

            Assert.Contains("lineweight", Assert.Throws<ArgumentOutOfRangeException>(() => leader.Explode()).Message, StringComparison.Ordinal);
        }

        #endregion

        #region A copy of a multileader

        [Fact]
        public void ACopyOfAMultileaderAppendedBack_AfterItsAttributeDefinitionWasErased_LeavesItsValueOut_TheOriginalKeepingIt()
        {
            // BA gives NUM (B9) of GKTAG the value 42. B9 erased, the copy appended back names no definition that is gone, as one
            // appended to another drawing does not; BA keeps its handle, as AutoCAD leaves the original.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader original = ByHandle<DxfMLeader>(db, "BA");
            var copy = (DxfMLeader)original.Clone();
            db.BlockTable["GKTAG"].OfType<DxfAttributeDefinition>().Single(a => a.Handle == "B9").Erase();
            var later = (DxfMLeader)original.Clone();

            db.ModelSpace.AppendEntity(copy);
            db.ModelSpace.AppendEntity(later);
            List<string> records = Records(db.ToText());

            Assert.Contains("330\nB9\n177\n0\n 44\n1.0\n302\n42\n", records.Single(r => HandleOf(r) == "BA"), StringComparison.Ordinal);

            foreach (DxfMLeader made in new[] { copy, later })
            {
                string record = records.Single(r => HandleOf(r) == made.Handle);
                Assert.DoesNotContain("330\nB9\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("302\n42\n", record, StringComparison.Ordinal);
                Assert.Equal("GKTAG", Assert.IsType<DxfBlockReference>(made.Explode()[0]).Name);
            }
        }

        [Fact]
        public void AMultileaderCopiedAloneOrBeforeAnInsertOfItsBlock_BringsItsBlock_AsAutoCADsDeepCloneFollowsItsPointer()
        {
            // BA places GKTAG, which ezdxf's drawing has not: copied alone, or by WblockCloneObjects before an INSERT of GKTAG,
            // it brings GKTAG and places it with NUM 42, as the INSERT first did.
            DxfDatabase source = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfMLeader original = ByHandle<DxfMLeader>(source, "BA");
            var insert = new DxfBlockReference(new GeoPoint3(50, 0, 0), source.BlockTable["GKTAG"]);
            source.ModelSpace.AppendEntity(insert);
            DxfDatabase alone = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var copy = (DxfMLeader)original.Clone();
            alone.ModelSpace.AppendEntity(copy);
            DxfDatabase together = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            IReadOnlyList<DxfEntity> copies = source.WblockCloneObjects(new DxfEntity[] { original, insert }, together.ModelSpace);

            foreach (DxfMLeader made in new[] { copy, (DxfMLeader)copies[0] })
            {
                DxfDatabase db = made == copy ? alone : together;
                Assert.Same(db.BlockTable["GKTAG"], made.BlockContent);
                var placed = Assert.IsType<DxfBlockReference>(made.Explode()[0]);
                Assert.Equal(new[] { "NUM=42" }, placed.AttributeCollection.Cast<DxfAttributeReference>().Select(a => a.Tag + "=" + a.TextString));
                var read = Assert.IsType<DxfBlockReference>(ByHandle<DxfMLeader>(DxfDatabase.Parse(db.ToText()), made.Handle).Explode()[0]);
                Assert.Equal(new[] { "NUM=42" }, read.AttributeCollection.Cast<DxfAttributeReference>().Select(a => a.Tag + "=" + a.TextString));
            }

            Assert.Equal("GKTAG", ((DxfBlockReference)copies[1]).Name);
            Assert.Single(together.BlockTable.Cast<DxfBlockTableRecord>(), b => b.Name == "GKTAG");
        }

        [Fact]
        public void AMultileaderInABlockCopiedByWblockCloneObjects_PlacesTheCopyOfItsBlock_ItsArrowsTooAndItsValueKept()
        {
            // Block W holds a copy of BA, its arrows set to GKTAG, its line 0 given the arrow GKTAG of its own (94, 345), and an
            // INSERT of GKTAG; an INSERT of W copied into ezdxf's drawing brings W and GKTAG, and the copy of BA in W places the
            // copy of GKTAG with NUM 42 and draws its arrows by it.
            string text = DxfHatchLeaderDrawings.R2018.Replace("\r\n", "\n");
            int at = text.IndexOf("330\nB9\n177\n", text.IndexOf("MULTILEADER\n  5\nBA\n", StringComparison.Ordinal), StringComparison.Ordinal);
            DxfDatabase source = DxfDatabase.Parse(text.Insert(at, " 94\n0\n345\nB5\n"));
            DxfBlockTableRecord gktag = source.BlockTable["GKTAG"];
            var w = new DxfBlockTableRecord { Name = "W" };
            source.BlockTable.Add(w);
            var inner = (DxfMLeader)ByHandle<DxfMLeader>(source, "BA").Clone();
            w.AppendEntity(inner);
            inner.ArrowSymbol = gktag;
            w.AppendEntity(new DxfBlockReference(new GeoPoint3(50, 0, 0), gktag));
            var placed = new DxfBlockReference(new GeoPoint3(0, 0, 0), w);
            source.ModelSpace.AppendEntity(placed);
            DxfDatabase target = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            source.WblockCloneObjects(new DxfEntity[] { placed }, target.ModelSpace);

            foreach (DxfDatabase db in new[] { target, DxfDatabase.Parse(target.ToText()) })
            {
                DxfMLeader copied = db.BlockTable["W"].OfType<DxfMLeader>().Single();
                Assert.Same(db.BlockTable["GKTAG"], copied.BlockContent);
                Assert.Same(db.BlockTable["GKTAG"], copied.ArrowSymbol);
                Assert.Contains(" 94\n0\n345\n" + db.BlockTable["GKTAG"].Handle + "\n", Records(db.ToText()).Single(r => HandleOf(r) == copied.Handle), StringComparison.Ordinal);
                var insert = Assert.IsType<DxfBlockReference>(copied.Explode()[0]);
                Assert.Equal("GKTAG", insert.Name);
                Assert.Equal(new[] { "NUM=42" }, insert.AttributeCollection.Cast<DxfAttributeReference>().Select(a => a.Tag + "=" + a.TextString));
            }
        }

        #endregion

        /// <summary>The leader B3 of <see cref="DxfHatchLeaderDrawings"/> with groups of its own look written after its layer.</summary>
        private static DxfLeader B3With(string groups)
        {
            const string Head = "LEADER\n  5\nB3\n330\n17\n100\nAcDbEntity\n  8\n0\n";
            string text = DxfHatchLeaderDrawings.R2018.Replace("\r\n", "\n");
            Assert.Contains(Head, text, StringComparison.Ordinal);
            return ByHandle<DxfLeader>(DxfDatabase.Parse(text.Replace(Head, Head + groups)), "B3");
        }
    }
}
