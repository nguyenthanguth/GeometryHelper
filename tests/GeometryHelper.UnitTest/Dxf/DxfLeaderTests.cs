using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The LEADER as a <see cref="DxfLeader"/>: read as ezdxf 1.4.5 reads it, the overrides of its dimension style as ezdxf's
    /// <c>override().get</c> gives them, its vertices and annotation set and read back, walked as a curve, made in code and
    /// appended, erased, and moved as ezdxf moves one; every other record of the file keeping its bytes.
    /// </summary>
    public class DxfLeaderTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static string[] Drawings => new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheLeadersOfEzdxf_AreReadAsEzdxfReadsThem_TheirOverridesAsItsOverrideGives()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader leader = ByHandle<DxfLeader>(db, "B2");

                Assert.Equal(3, leader.NumVertices);
                Assert.Equal(Bits(new GeoPoint3(610, 20, 0)), Bits(leader.VertexAt(1)));
                Assert.Equal(Bits(new GeoPoint3(600, 0, 0)), Bits(leader.FirstVertex));
                Assert.Equal(Bits(new GeoPoint3(618, 30, 0)), Bits(leader.LastVertex));
                Assert.True(leader.HasArrowHead);
                Assert.True(leader.HasHookLine);
                Assert.True(leader.IsSplined);
                Assert.Equal(DxfAnnotationType.MText, leader.AnnoType);
                Assert.Equal(2.5, leader.AnnoHeight);
                Assert.Equal(12.0, leader.AnnoWidth);
                Assert.Equal("B1", Assert.IsType<DxfMText>(leader.Annotation).Handle);
                Assert.Equal(Bits(new GeoVector3(-2.0, -1.25, 0.0)), Bits(leader.AnnotationOffset));
                Assert.Equal("Standard", leader.DimensionStyleName);
                Assert.Equal("Standard", leader.DimensionStyle);
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(leader.Normal));

                // The overrides of the extended data of ACAD (DSTYLE).
                Assert.Equal(3.5, leader.Dimasz);
                Assert.Equal(1.25, leader.Dimgap);
                Assert.Equal(1, leader.Dimtad);
                Assert.Equal(1, leader.Dimclrd.ColorIndex);
                Assert.Equal(2.0, leader.Dimscale);
                Assert.Equal(DxfLineWeight.LineWeight035, leader.Dimlwd);
                Assert.True(leader.Dimsah);
                Assert.Equal(2.5, leader.Dimtxt);
                Assert.Null(leader.Dimldrblk);

                // A leader with none: those of its style Standard.
                DxfLeader plain = ByHandle<DxfLeader>(db, "B3");
                Assert.False(plain.HasArrowHead);
                Assert.False(plain.HasHookLine);
                Assert.False(plain.IsSplined);
                Assert.Equal(DxfAnnotationType.NoAnnotation, plain.AnnoType);
                Assert.Null(plain.Annotation);
                Assert.Equal(2.5, plain.Dimasz);
                Assert.Equal(0.625, plain.Dimgap);
                Assert.Equal(1, plain.Dimtad);
                Assert.True(plain.Dimclrd.IsByBlock);
                Assert.Equal(1.0, plain.Dimscale);
                Assert.Equal(DxfLineWeight.ByBlock, plain.Dimlwd);
                Assert.False(plain.Dimsah);
                Assert.Equal(2.5, plain.Dimtxt);
                Assert.Throws<ArgumentOutOfRangeException>(() => plain.VertexAt(3));
            }
        }

        [Fact]
        public void AStraightLeader_IsWalkedByVertexIndex_AndASplinedOneAlongItsSpline()
        {
            // 89: (0, 0, 0), (10, 10, 0), (20, 10, 0): 10 root 2 and 10 long, 1.5 at (15, 10, 0).
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfLeader leader = ByHandle<DxfLeader>(db, "89");

            Assert.Equal(2.0, leader.EndParam);
            Assert.False(leader.Closed);
            Assert.Equal(Bits(new GeoPoint3(20, 10, 0)), Bits(leader.EndPoint));
            Assert.True(leader.GetPointAtParameter(1.5).IsEqualTo(new GeoPoint3(15, 10, 0), Tol));
            Assert.Equal(10.0 * Math.Sqrt(2.0) + 10.0, leader.GetDistanceAtParameter(2.0), 12);
            Assert.Equal(1.5, leader.GetParameterAtPoint(new GeoPoint3(15, 10, 0)), 12);
            Assert.True(leader.GetClosestPointTo(new GeoPoint3(15, 12, 0), false).IsEqualTo(new GeoPoint3(15, 10, 0), Tol));
            Assert.Equal(new[] { 0.0, 0.0, 1.0, 2.0, 2.0 }, leader.Spline.Knots);

            DxfLeader splined = ByHandle<DxfLeader>(db, "B2");
            Assert.Equal(Bits(new GeoPoint3(600, 0, 0)), Bits(splined.StartPoint));
            Assert.True(splined.GetPointAtParameter(splined.EndParam).IsEqualTo(splined.LastVertex, Tol));
            Assert.Equal(3, splined.Spline.NurbsData.Degree);
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings.Concat(new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 }))
            {
                SettingWhatItHasChangesNothing<DxfLeader>(text, l =>
                {
                    l.HasArrowHead = l.HasArrowHead;
                    l.IsSplined = l.IsSplined;
                    l.Annotation = l.Annotation;
                    l.AnnotationOffset = l.AnnotationOffset;
                    l.DimensionStyleName = l.DimensionStyleName;
                    l.DimensionStyle = l.DimensionStyle;
                    l.Dimasz = l.Dimasz;
                    l.Dimgap = l.Dimgap;
                    l.Dimscale = l.Dimscale;
                    l.Dimtxt = l.Dimtxt;
                    l.Dimtad = l.Dimtad;
                    l.Dimsah = l.Dimsah;
                    l.Dimlwd = l.Dimlwd;
                    l.Dimclrd = l.Dimclrd;
                    l.Dimldrblk = l.Dimldrblk;

                    for (int i = 0; i < l.NumVertices; i++)
                    {
                        Assert.True(l.SetVertexAt(i, l.VertexAt(i)));
                    }

                    l.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Set and read back

        [Fact]
        public void TheVertices_AreAppendedMovedAndTakenOut_WithTheirCount()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfLeader leader = ByHandle<DxfLeader>(db, "B3");

                Assert.True(leader.AppendVertex(new GeoPoint3(740, 15, 1)));
                Assert.True(leader.SetVertexAt(0, new GeoPoint3(701.5, 0.25, 1)));
                OnlyTheseChanged(text, db.ToText(), "B3");
                string record = Records(db.ToText()).Single(r => HandleOf(r) == "B3");
                Assert.Contains("\n 76\n4\n", record, StringComparison.Ordinal);
                Assert.Contains("\n 10\n710.0\n 20\n10.0\n 30\n1.0\n", record, StringComparison.Ordinal);

                DxfLeader back = ByHandle<DxfLeader>(DxfDatabase.Parse(db.ToText()), "B3");
                Assert.Equal(4, back.NumVertices);
                Assert.Equal(Bits(new GeoPoint3(701.5, 0.25, 1)), Bits(back.FirstVertex));
                Assert.Equal(Bits(new GeoPoint3(740, 15, 1)), Bits(back.LastVertex));
                back.RemoveLastVertex();
                back.RemoveLastVertex();
                Assert.Equal(2, back.NumVertices);
                Assert.Throws<InvalidOperationException>(() => back.RemoveLastVertex());
            }
        }

        [Fact]
        public void TheOverrides_AreWrittenInTheExtendedDataOfAcad_AsAutoCADWritesThem()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader plain = ByHandle<DxfLeader>(db, "B3");
            DxfLeader overridden = ByHandle<DxfLeader>(db, "B2");

            plain.Dimasz = 3.0;
            plain.Dimclrd = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 5);
            plain.Dimldrblk = db.BlockTable["GKTAG"];
            overridden.Dimasz = 4.0;
            overridden.Dimsah = false;
            OnlyTheseChanged(text, db.ToText(), "B2", "B3");

            string record = Records(db.ToText()).Single(r => HandleOf(r) == "B3");
            Assert.Contains("1001\nACAD\n1000\nDSTYLE\n1002\n{\n1070\n41\n1040\n3\n1070\n176\n1070\n5\n1070\n341\n1005\nB5\n1002\n}\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n41\n1040\n4\n1070\n147\n", Records(db.ToText()).Single(r => HandleOf(r) == "B2"), StringComparison.Ordinal);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            Assert.Equal(3.0, ByHandle<DxfLeader>(back, "B3").Dimasz);
            Assert.Equal(5, ByHandle<DxfLeader>(back, "B3").Dimclrd.ColorIndex);
            Assert.Equal("GKTAG", ByHandle<DxfLeader>(back, "B3").Dimldrblk.Name);
            Assert.Equal(4.0, ByHandle<DxfLeader>(back, "B2").Dimasz);
            Assert.False(ByHandle<DxfLeader>(back, "B2").Dimsah);
            Assert.Equal(1.25, ByHandle<DxfLeader>(back, "B2").Dimgap);
            Assert.Throws<ArgumentException>(() => plain.Dimclrd = DxfColor.FromRgb(1, 2, 3));
            Assert.Throws<ArgumentException>(() => plain.DimensionStyleName = "NOPE");
        }

        [Fact]
        public void TheAnnotation_IsSetByItsHandle_AndTakenOff()
        {
            string text = DxfHatchLeaderDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader leader = ByHandle<DxfLeader>(db, "B3");
            DxfMText note = ByHandle<DxfMText>(db, "B1");

            leader.Annotation = note;
            leader.IsSplined = true;
            leader.HasArrowHead = true;
            leader.AnnotationOffset = new GeoVector3(1, 2, 0);
            ByHandle<DxfLeader>(db, "B2").Annotation = null;
            OnlyTheseChanged(text, db.ToText(), "B1", "B2", "B3");

            // The MTEXT lists among its reactors the leader that points to it now, not the one that pointed to it before.
            string words = Records(db.ToText()).Single(r => HandleOf(r) == "B1");
            Assert.Contains("\n102\n{ACAD_REACTORS\n330\nB3\n102\n}\n", words, StringComparison.Ordinal);

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            DxfLeader again = ByHandle<DxfLeader>(back, "B3");
            Assert.Equal("B1", again.Annotation.Handle);
            Assert.Equal(DxfAnnotationType.MText, again.AnnoType);
            Assert.True(again.IsSplined);
            Assert.True(again.HasArrowHead);
            Assert.Equal(Bits(new GeoVector3(1, 2, 0)), Bits(again.AnnotationOffset));
            Assert.Null(ByHandle<DxfLeader>(back, "B2").Annotation);
            Assert.Equal(DxfAnnotationType.NoAnnotation, ByHandle<DxfLeader>(back, "B2").AnnoType);
            Assert.Throws<ArgumentException>(() => again.Annotation = ByHandle<DxfLeader>(back, "89"));
        }

        #endregion

        #region Made in code, appended and erased

        [Fact]
        public void ALeaderMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var leader = new DxfLeader { ColorIndex = 1 };
                leader.AppendVertex(new GeoPoint3(0, 0, 0));
                leader.AppendVertex(new GeoPoint3(5, 5, 0));
                leader.Dimasz = 1.5;

                if (text == DxfFileEzdxfDrawings.R12)
                {
                    Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(leader));
                    continue;
                }

                db.ModelSpace.AppendEntity(leader);
                DxfLeader back = ByHandle<DxfLeader>(DxfDatabase.Parse(db.ToText()), leader.Handle);
                Assert.Equal(2, back.NumVertices);
                Assert.Equal(1.5, back.Dimasz);
                Assert.Equal("Standard", back.DimensionStyleName);
                Assert.True(back.HasArrowHead);
                Assert.Equal(DxfAnnotationType.NoAnnotation, back.AnnoType);
            }
        }

        [Fact]
        public void AnErasedLeader_IsGone_TheRestKept_AndRefusesToBeSet()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfLeader leader = ByHandle<DxfLeader>(db, "B3");

            leader.Erase();
            OnlyTheseChanged(text, db.ToText(), "B3");
            Assert.Throws<InvalidOperationException>(() => leader.AppendVertex(new GeoPoint3(0, 0, 0)));
        }

        #endregion

        #region Moved

        [Fact]
        public void ALeaderMoved_HasItsVerticesNormalAndHookMovedAsEzdxfMovesThem()
        {
            // Turned 30 degrees, scaled 2 and moved by (5, -7, 3), as ezdxf's transform() moves it (cmptransform.py): the
            // vertices moved, the direction of the hook (1, 0, 0) turned and scaled to (2 cos 30, 2 sin 30, 0); past ezdxf, the
            // offset of the annotation turned and scaled.
            double c = Math.Sqrt(3.0);
            var transform = new GeoTransform3(new double[,] { { c, -1, 0, 5 }, { 1, c, 0, -7 }, { 0, 0, 2, 3 }, { 0, 0, 0, 1 } });

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                ByHandle<DxfLeader>(db, "B2").TransformBy(transform);
                OnlyTheseChanged(text, db.ToText(), "B2");

                DxfLeader back = ByHandle<DxfLeader>(DxfDatabase.Parse(db.ToText()), "B2");
                Assert.True(back.FirstVertex.IsEqualTo(new GeoPoint3(600 * c + 5, 600 - 7, 3), Tol));
                Assert.True(back.VertexAt(1).IsEqualTo(transform.Transform(new GeoPoint3(610, 20, 0)), Tol));
                Assert.Contains("\n211\n1.7320508075688772\n221\n1\n", Records(db.ToText()).Single(r => HandleOf(r) == "B2"), StringComparison.Ordinal);
                Assert.True(back.AnnotationOffset.IsEqualTo(new GeoVector3(-2 * c + 1.25, -2 - 1.25 * c, 0), Tol));
                Assert.Equal(3.5, back.Dimasz);
            }
        }

        #endregion

        #region Review round 1

        [Fact]
        public void ThePlaneAndExtentsOfALeader_AreThoseOfItsVertices()
        {
            // B3's vertices: its box from the least to the greatest, its plane that of its normal through the first; a leader
            // with a vertex off that plane has none, as AutoCAD answers eNotPlanar.
            DxfLeader leader = ByHandle<DxfLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B3");
            DxfEntity entity = leader;
            GeoAabb3 box = entity.GeometricExtents;

            for (int i = 0; i < leader.NumVertices; i++)
            {
                Assert.True(box.Contains(leader.VertexAt(i), Tol));
            }

            GeoPoint3[] vertices = Enumerable.Range(0, leader.NumVertices).Select(leader.VertexAt).ToArray();
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(vertices.Min(p => p.X), vertices.Min(p => p.Y), vertices.Min(p => p.Z)), Tol));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(vertices.Max(p => p.X), vertices.Max(p => p.Y), vertices.Max(p => p.Z)), Tol));
            Assert.True(entity.GetPlane().Normal.IsEqualTo(leader.Normal, Tol));

            var bent = new DxfLeader();
            bent.AppendVertex(new GeoPoint3(0, 0, 0));
            bent.AppendVertex(new GeoPoint3(10, 0, 0));
            bent.AppendVertex(new GeoPoint3(10, 5, 1));
            Assert.Throws<InvalidOperationException>(() => ((DxfEntity)bent).GetPlane());
            Assert.Throws<InvalidOperationException>(() => ((DxfEntity)new DxfLeader()).GeometricExtents);

            // B2 is splined: its box is that of the spline it explodes into with no arrow.
            DxfLeader splined = ByHandle<DxfLeader>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "B2");
            splined.HasArrowHead = false;
            Assert.Equal(Bits(Assert.Single(splined.Explode().OfType<DxfSpline>()).GeometricExtents.Max), Bits(((DxfEntity)splined).GeometricExtents.Max));
        }

        [Fact]
        public void ALeaderOfFewerThanTwoVertices_IsRefusedByTheDrawing_AndOneOfNoneIsNoCurveToWalk()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var leader = new DxfLeader();
            var none = Assert.Throws<InvalidOperationException>(() => leader.GetClosestPointTo(new GeoPoint3(0, 0, 0), false));
            Assert.Contains("no vertex", none.Message, StringComparison.Ordinal);
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(leader));

            leader.AppendVertex(new GeoPoint3(0, 0, 0));
            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(leader));
            Assert.Equal(Records(DxfHatchLeaderDrawings.R2018).Count, Records(db.ToText()).Count);

            leader.AppendVertex(new GeoPoint3(5, 5, 0));
            db.ModelSpace.AppendEntity(leader);
            Assert.Equal(2, ByHandle<DxfLeader>(DxfDatabase.Parse(db.ToText()), leader.Handle).NumVertices);
        }

        #endregion
    }
}
