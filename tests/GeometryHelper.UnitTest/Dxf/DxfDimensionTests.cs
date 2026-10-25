using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The dimensions: each kind read as ezdxf 1.4.5 reads it from <see cref="DxfDimensionDrawings"/>, its measurement worked
    /// out from its points as ezdxf works it out, its overrides read and written in the list DSTYLE of its data of ACAD, its
    /// points set and moved as ezdxf's <c>transform()</c> moves them, and its block left as AutoCAD drew it.
    /// </summary>
    public class DxfDimensionTests
    {
        private static T Dim<T>(DxfDatabase db, string handle) where T : DxfDimension => db.ModelSpace.OfType<T>().Single(d => d.Handle == handle);

        private static void Near(GeoPoint3 expected, GeoPoint3 actual, double tolerance = 1E-9)
        {
            Assert.Equal(expected.X, actual.X, tolerance);
            Assert.Equal(expected.Y, actual.Y, tolerance);
            Assert.Equal(expected.Z, actual.Z, tolerance);
        }

        #region Reading

        [Fact]
        public void EveryKindOfR2018_IsItsOwnClass_ItsPointsAsEzdxfReadsThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);

            DxfRotatedDimension rotated = Dim<DxfRotatedDimension>(db, "4D");
            Assert.Equal(Bits(new GeoPoint3(0.0, 0.0, 0.0)), Bits(rotated.XLine1Point));
            Assert.Equal(Bits(new GeoPoint3(100.0, 20.0, 0.0)), Bits(rotated.XLine2Point));
            Assert.Equal(Bits(new GeoPoint3(-10.490381056766577, 18.16987298107781, 0.0)), Bits(rotated.DimLinePoint));
            Assert.Equal(30.0 * Math.PI / 180.0, rotated.Rotation, 15);
            Assert.Equal(Bits(new GeoPoint3(60.0, 45.0, 0.0)), Bits(rotated.TextPosition));
            Assert.False(rotated.UsingDefaultTextPosition);
            Assert.Equal("<> TYP", rotated.DimensionText);
            Assert.Equal("GH_DIM", rotated.DimensionStyleName);
            Assert.Same(db.DimStyleTable["GH_DIM"], rotated.DimensionStyle);
            Assert.Equal("*D1", rotated.DimBlock.Name);
            Assert.Equal(DxfAttachmentPoint.MiddleCenter, rotated.TextAttachment);

            DxfRadialDimension radial = Dim<DxfRadialDimension>(db, "75");
            Assert.Equal(Bits(new GeoPoint3(150.0, 0.0, 0.0)), Bits(radial.Center));
            Assert.Equal(Bits(new GeoPoint3(167.6776695296637, 17.67766952966369, 0.0)), Bits(radial.ChordPoint));
            Assert.Equal(0.0, radial.LeaderLength);

            DxfDiametricDimension diametric = Dim<DxfDiametricDimension>(db, "80");
            Assert.Equal(Bits(new GeoPoint3(207.00961894323342, -7.499999999999999, 0.0)), Bits(diametric.ChordPoint));
            Assert.Equal(Bits(new GeoPoint3(232.99038105676658, 7.499999999999999, 0.0)), Bits(diametric.FarChordPoint));

            DxfLineAngularDimension2 lines = Dim<DxfLineAngularDimension2>(db, "87");
            Assert.Equal(Bits(new GeoPoint3(0.0, 100.0, 0.0)), Bits(lines.XLine1Start));
            Assert.Equal(Bits(new GeoPoint3(50.0, 100.0, 0.0)), Bits(lines.XLine1End));
            Assert.Equal(Bits(new GeoPoint3(0.0, 100.0, 0.0)), Bits(lines.XLine2Start));
            Assert.Equal(Bits(new GeoPoint3(40.0, 140.0, 0.0)), Bits(lines.XLine2End));
            Assert.Equal(Bits(new GeoPoint3(40.0, 120.0, 0.0)), Bits(lines.ArcPoint));

            DxfPoint3AngularDimension points = Dim<DxfPoint3AngularDimension>(db, "99");
            Assert.Equal(Bits(new GeoPoint3(120.0, 100.0, 0.0)), Bits(points.CenterPoint));
            Assert.Equal(Bits(new GeoPoint3(160.0, 100.0, 0.0)), Bits(points.XLine1Point));
            Assert.Equal(Bits(new GeoPoint3(120.0, 140.0, 0.0)), Bits(points.XLine2Point));
            Assert.Equal(Bits(new GeoPoint3(140.0, 130.0, 0.0)), Bits(points.ArcPoint));

            DxfArcDimension arc = Dim<DxfArcDimension>(db, "AA");
            Assert.Equal(Bits(new GeoPoint3(200.0, 100.0, 0.0)), Bits(arc.CenterPoint));
            Assert.Equal(Bits(new GeoPoint3(240.0, 100.0, 0.0)), Bits(arc.XLine1Point));
            Assert.Equal(Bits(new GeoPoint3(200.0, 140.0, 0.0)), Bits(arc.XLine2Point));
            Assert.Equal(Bits(new GeoPoint3(230.0, 130.0, 0.0)), Bits(arc.ArcPoint));
            Assert.False(arc.IsPartial);
            Assert.False(arc.HasLeader);
            Assert.Equal(0.0, arc.ArcStartParam);

            DxfOrdinateDimension x = Dim<DxfOrdinateDimension>(db, "BB");
            DxfOrdinateDimension y = Dim<DxfOrdinateDimension>(db, "C6");
            Assert.True(x.UsingXAxis);
            Assert.True(y.UsingYAxis);
            Assert.Equal(Bits(new GeoPoint3(280.0, 10.0, 0.0)), Bits(x.Origin));
            Assert.Equal(Bits(new GeoPoint3(300.0, 40.0, 0.0)), Bits(x.DefiningPoint));
            Assert.Equal(Bits(new GeoPoint3(300.0, 59.99999999999999, 0.0)), Bits(x.LeaderEndPoint));

            DxfRadialDimensionLarge jogged = Dim<DxfRadialDimensionLarge>(db, "E5");
            Assert.Equal(Bits(new GeoPoint3(300.0, -40.0, 0.0)), Bits(jogged.Center));
            Assert.Equal(Bits(new GeoPoint3(317.67766952966366, -57.67766952966369, 0.0)), Bits(jogged.ChordPoint));
            Assert.Equal(Bits(new GeoPoint3(305.0, -45.0, 0.0)), Bits(jogged.OverrideCenter));
            Assert.Equal(Bits(new GeoPoint3(311.0, -52.0, 0.0)), Bits(jogged.JogPoint));
            Assert.Equal(0.7853981633974483, jogged.JogAngle);

            Assert.Equal(11, db.ModelSpace.OfType<DxfDimension>().Count());
        }

        [Fact]
        public void TheMeasurement_IsWorkedOutFromThePoints_AsEzdxfWorksItOut()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfDimension[] dims = db.ModelSpace.OfType<DxfDimension>().ToArray();

                // ezdxf's get_measurement: 96.60254037844386, 89.44271909999163, 25, 30, 45 and 90 degrees, (20, 30) of the
                // ordinates, 40 of the tilted one; the arc's a quarter of a circle of radius 40.
                Assert.Equal(96.60254037844386, dims.OfType<DxfRotatedDimension>().First().Measurement, 12);
                Assert.Equal(89.44271909999163, dims.OfType<DxfRotatedDimension>().ElementAt(1).Measurement, 12);
                Assert.Equal(25.0, dims.OfType<DxfRadialDimension>().Single().Measurement, 12);
                Assert.Equal(30.0, dims.OfType<DxfDiametricDimension>().Single().Measurement, 12);
                Assert.Equal(Math.PI / 4.0, dims.OfType<DxfLineAngularDimension2>().Single().Measurement, 12);
                Assert.Equal(Math.PI / 2.0, dims.OfType<DxfPoint3AngularDimension>().Single().Measurement, 12);
                Assert.Equal(20.0, dims.OfType<DxfOrdinateDimension>().Single(d => d.UsingXAxis).Measurement, 12);
                Assert.Equal(30.0, dims.OfType<DxfOrdinateDimension>().Single(d => d.UsingYAxis).Measurement, 12);
                Assert.Equal(40.0, dims.OfType<DxfRotatedDimension>().Last().Measurement, 12);

                if (text != DxfDimensionDrawings.R12)
                {
                    Assert.Equal(20.0 * Math.PI, dims.OfType<DxfArcDimension>().Single().Measurement, 12);
                }
            }
        }

        [Fact]
        public void AnAngleOfTwoLines_MeasuresTheQuarterTheArcPointLiesIn()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfLineAngularDimension2 angle = Dim<DxfLineAngularDimension2>(db, "87");

            angle.ArcPoint = new GeoPoint3(-40.0, 120.0, 0.0);
            Assert.Equal(Math.PI * 3.0 / 4.0, angle.Measurement, 12);

            angle.ArcPoint = new GeoPoint3(-40.0, 80.0, 0.0);
            Assert.Equal(Math.PI / 4.0, angle.Measurement, 12);
        }

        [Fact]
        public void ADimensionInATiltedPlane_GivesItsTextInTheWorld()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension tilted = Dim<DxfRotatedDimension>(db, "D1");

            // Its 11 (20, 55, 30) in the system of (0, -0.8, 0.6): x (1, 0, 0), y (0, 0.6, 0.8).
            Assert.Equal(Bits(new GeoVector3(0.0, -0.8, 0.6)), Bits(tilted.Normal));
            Near(new GeoPoint3(20.0, 9.0, 62.0), tilted.TextPosition);
            Assert.Equal(29.999999999999996, tilted.Elevation);
        }

        [Fact]
        public void TheOverrides_AreReadFromDSTYLE_ElseFromTheStyle()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = Dim<DxfRotatedDimension>(db, "4D");

            // ezdxf: {'dimasz': 4.5, 'dimtxt': 2.0, 'dimclrd': 4, 'dimtad': 0, 'dimpost': '<>', 'dimlwd': 18, 'dimdsep': 46,
            // 'dimtmove': 2, 'dimblk': 'CLOSED', 'dimtxsty': 'GH_TXT2', 'dimltype': 'CENTER'}; the rest GH_DIM's.
            Assert.Equal(4.5, dim.Dimasz);
            Assert.Equal(2.0, dim.Dimtxt);
            Assert.Equal(4, dim.Dimclrd.ColorIndex);
            Assert.Equal(0, dim.Dimtad);
            Assert.Equal("<>", dim.Dimpost);
            Assert.Equal(DxfLineWeight.LineWeight018, dim.Dimlwd);
            Assert.Equal('.', dim.Dimdsep);
            Assert.Equal(2, dim.Dimtmove);
            Assert.Equal("_CLOSED", dim.Dimblk.Name);
            Assert.Equal("GH_TXT2", dim.TextStyle.Name);
            Assert.Equal("CENTER", dim.Dimltype.Name);
            Assert.Equal(0.75, dim.Dimexo);
            Assert.Equal("_DOT", dim.Dimblk1.Name);
            Assert.Equal(12, dim.Dimzin);
            Assert.True(dim.SuppressLeadingZeros);
            Assert.True(dim.SuppressTrailingZeros);
            Assert.True(dim.SuppressZeroFeet);
            Assert.True(dim.SuppressZeroInches);
            Assert.Equal(DxfDimensionCenterMarkType.Line, dim.CenterMarkType);
            Assert.Equal(2.0, dim.CenterMarkSize);
            Assert.Equal(string.Empty, dim.Prefix);
            Assert.Equal(string.Empty, dim.Suffix);

            DxfDimStyleTableRecord data = dim.GetDimstyleData();
            Assert.Null(data.Item);
            Assert.Equal(4.5, data.Dimasz);
            Assert.Equal(0.75, data.Dimexo);
            Assert.Equal("GH_DIM", data.Name);
        }

        #endregion

        #region Setting

        [Fact]
        public void EverySetterGivenItsOwnValue_ChangesNothing()
        {
            foreach (string text in new[] { DxfDimensionDrawings.R12, DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                byte[] before = Saved(db);
                int seen = 0;

                foreach (DxfDimension dim in db.ModelSpace.OfType<DxfDimension>().ToList())
                {
                    foreach (var property in dim.GetType().GetProperties().Where(p => p.CanWrite && p.CanRead && p.DeclaringType != typeof(DxfEntity) && p.DeclaringType != typeof(DxfDBObject)))
                    {
                        property.SetValue(dim, property.GetValue(dim));
                    }

                    dim.SetDimstyleData(dim.GetDimstyleData());
                    seen++;
                }

                Assert.True(seen >= 9);
                Assert.Equal(before, Saved(db));
            }
        }

        [Fact]
        public void APointSet_IsWrittenInItsRecordAlone_AndReadBack()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRotatedDimension dim = Dim<DxfRotatedDimension>(db, "61");

            dim.XLine2Point = new GeoPoint3(90.0, -60.0, 0.0);
            dim.Rotation = 0.0;
            dim.DimensionText = "<> MAX";
            dim.TextRotation = Math.PI / 2.0;

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "61");
            DxfRotatedDimension read = Dim<DxfRotatedDimension>(DxfDatabase.Parse(saved), "61");
            Assert.Equal(Bits(new GeoPoint3(90.0, -60.0, 0.0)), Bits(read.XLine2Point));
            Assert.Equal(90.0, read.Measurement, 12);
            Assert.Equal("<> MAX", read.DimensionText);
            Assert.Equal(Math.PI / 2.0, read.TextRotation, 15);
            Assert.Equal(0.0, read.Rotation);

            // The block AutoCAD drew is left as it was.
            Assert.Equal(Records(text).Where(r => r.Contains("\n330\n62\n")), Records(saved).Where(r => r.Contains("\n330\n62\n")));
        }

        [Fact]
        public void TheTextMoved_IsNoLongerAtItsDefaultPlace()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRadialDimension dim = Dim<DxfRadialDimension>(db, "75");
            Assert.True(dim.UsingDefaultTextPosition);

            dim.TextPosition = new GeoPoint3(180.0, 30.0, 0.0);

            DxfRadialDimension read = Dim<DxfRadialDimension>(DxfDatabase.Parse(db.ToText()), "75");
            Assert.False(read.UsingDefaultTextPosition);
            Assert.Equal(Bits(new GeoPoint3(180.0, 30.0, 0.0)), Bits(read.TextPosition));
            Assert.Contains("\n 70\n164\n", db.ToText(), StringComparison.Ordinal);

            read.UsingDefaultTextPosition = true;
            Assert.True(read.UsingDefaultTextPosition);
        }

        [Fact]
        public void AnOverrideSet_IsWrittenInDSTYLE_InPlaceOrLast_AndANewListMadeWhereThereIsNone()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRotatedDimension dim = Dim<DxfRotatedDimension>(db, "4D");
            DxfRadialDimension radius = Dim<DxfRadialDimension>(db, "75");

            dim.Dimasz = 6.0;
            dim.Dimgap = 2.0;
            dim.Dimblk = null;
            dim.Suffix = " TYP";
            radius.Dimtxt = 5.0;
            radius.Dimltex1 = db.LinetypeTable["DASHED"];

            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "4D", "75");
            string record = Records(saved).Single(r => HandleOf(r) == "4D");
            Assert.Contains("1070\n41\n1040\n6\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n342\n1005\n0\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n147\n1040\n2\n1002\n}\n", record.Replace("1070\n3\n1000\n<> TYP\n", string.Empty), StringComparison.Ordinal);
            Assert.EndsWith("1001\nACAD\n1000\nDSTYLE\n1002\n{\n1070\n140\n1040\n5\n1070\n346\n1005\n2F\n1002\n}\n", Records(saved).Single(r => HandleOf(r) == "75"), StringComparison.Ordinal);

            DxfDatabase read = DxfDatabase.Parse(saved);
            Assert.Equal(6.0, Dim<DxfRotatedDimension>(read, "4D").Dimasz);
            Assert.Null(Dim<DxfRotatedDimension>(read, "4D").Dimblk);
            Assert.Equal("<> TYP", Dim<DxfRotatedDimension>(read, "4D").Dimpost);
            Assert.Equal(5.0, Dim<DxfRadialDimension>(read, "75").Dimtxt);
            Assert.Equal("DASHED", Dim<DxfRadialDimension>(read, "75").Dimltex1.Name);
            Assert.Equal(2.5, read.DimStyleTable["Standard"].Dimtxt);
        }

        [Fact]
        public void TheStyleSet_IsWrittenByName_AndOneTheDrawingHasNotIsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            DxfRotatedDimension dim = Dim<DxfRotatedDimension>(db, "61");

            dim.DimensionStyle = db.DimStyleTable["GH_DIM"];
            Assert.Equal(3.5, dim.Dimasz);
            dim.DimensionStyleName = "gh_empty";
            Assert.Equal("GH_EMPTY", dim.DimensionStyleName);
            Refused<ArgumentException>(() => dim.DimensionStyleName = "NONE", "no dimension style named NONE");
            Refused<InvalidOperationException>(() => dim.Dimtfill = 1, "DIMTFILL, which came with R2007");
        }

        [Fact]
        public void AnAlignedDimension_IsReadAndMeasuredAlongItsPoints()
        {
            string text = DxfDimensionDrawings.R2018.Replace("  2\n*D2\n  3\nStandard\n 10\n4.4721359549995805\n 20\n-11.05572809000084\n 30\n0.0\n 11\n44.47213595499957\n 21\n-31.05572809000084\n 31\n0.0\n 70\n32\n", "  2\n*D2\n  3\nStandard\n 10\n4.4721359549995805\n 20\n-11.05572809000084\n 30\n0.0\n 11\n44.47213595499957\n 21\n-31.05572809000084\n 31\n0.0\n 70\n33\n");
            DxfAlignedDimension aligned = Dim<DxfAlignedDimension>(DxfDatabase.Parse(text), "61");

            Assert.Equal(89.44271909999163, aligned.Measurement, 12);
            Assert.Equal(0.0, aligned.Oblique);
            aligned.Oblique = Math.PI / 6.0;
            Assert.Equal(Math.PI / 6.0, aligned.Oblique, 15);
        }

        #endregion

        #region Moving

        [Fact]
        public void ADimensionMoved_GoesWhereEzdxfsTransformTakesIt_ItsBlockWithIt()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(10.0, 20.0, 5.0)) * GeoTransform3.RotationZ(30.0 * Math.PI / 180.0);

            DxfRotatedDimension rotated = Dim<DxfRotatedDimension>(db, "4D");
            rotated.TransformBy(move);
            DxfLineAngularDimension2 lines = Dim<DxfLineAngularDimension2>(db, "87");
            lines.TransformBy(move);
            DxfArcDimension arc = Dim<DxfArcDimension>(db, "AA");
            arc.TransformBy(move);

            // ezdxf's transform() of the same: a turn of 30 degrees about z, then a move by (10, 20, 5).
            Near(new GeoPoint3(-8.169872981077802, 30.490381056766584, 5.0), rotated.DimLinePoint);
            Near(new GeoPoint3(10.0, 20.0, 5.0), rotated.XLine1Point);
            Near(new GeoPoint3(86.60254037844388, 87.32050807568876, 5.0), rotated.XLine2Point);
            Near(new GeoPoint3(39.46152422706632, 88.97114317029974, 5.0), rotated.TextPosition);
            Assert.Equal(59.99999999999999 * Math.PI / 180.0, rotated.Rotation, 12);
            Near(new GeoPoint3(-15.358983848622444, 143.92304845413264, 5.0), lines.ArcPoint);
            Near(new GeoPoint3(-67.22463874686824, 71.12270039708939, 5.0), lines.TextPosition);
            Near(new GeoPoint3(133.20508075688775, 206.60254037844385, 5.0), arc.CenterPoint);
            Near(new GeoPoint3(10.0, 20.0, 5.0), arc.Leader1Point);
            Assert.Equal(96.60254037844386, rotated.Measurement, 9);

            // The block, the dimension's alone and placed at no point of its own, is moved with it, as ezdxf moves it.
            DxfLine line = rotated.DimBlock.OfType<DxfLine>().First();
            Near(new GeoPoint3(45.32754963085077, 111.15063509461098, 5.0), line.StartPoint);
            Near(new GeoPoint3(37.53332099679083, 115.65063509461098, 5.0), line.EndPoint);

            string saved = db.ToText();
            Assert.Equal(Bits(rotated.XLine2Point), Bits(Dim<DxfRotatedDimension>(DxfDatabase.Parse(saved), "4D").XLine2Point));
        }

        [Fact]
        public void ADimensionInATiltedPlane_TurnedAboutZ_KeepsItsTextWhereTheTurnTakesIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension tilted = Dim<DxfRotatedDimension>(db, "D1");
            GeoTransform3 move = GeoTransform3.Translation(new GeoVector3(10.0, 20.0, 5.0)) * GeoTransform3.RotationZ(30.0 * Math.PI / 180.0);
            GeoPoint3 text = move.Transform(tilted.TextPosition);
            DxfLine line = tilted.DimBlock.OfType<DxfLine>().First();
            GeoPoint3 start = move.Transform(OcsWorld(tilted.Normal, line.StartPoint));

            tilted.TransformBy(move);

            // ezdxf: extrusion (0.4, -0.6928, 0.6), 11 (38.66, 66.39, 23.14) in its system.
            Near(new GeoPoint3(0.39999999999999997, -0.692820323027551, 0.6), new GeoPoint3(tilted.Normal.X, tilted.Normal.Y, tilted.Normal.Z));
            Near(text, tilted.TextPosition);
            Near(new GeoPoint3(10.0, 20.0, 55.0), tilted.XLine1Point);

            // The block's entities stand in the system of the normal, and are moved so: in the world, the line starts where the
            // turn takes its old start.
            Near(start, OcsWorld(tilted.Normal, line.StartPoint));
        }

        [Fact]
        public void AJoggedRadius_MovesItsJogPointToo()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRadialDimensionLarge jogged = Dim<DxfRadialDimensionLarge>(db, "E5");

            jogged.TransformBy(GeoTransform3.Translation(new GeoVector3(1.0, 2.0, 3.0)));

            // ezdxf 1.4.5 leaves the jog point (its "Jog_point" a misspelled name in its transform()); the library moves it.
            Assert.Equal(Bits(new GeoPoint3(312.0, -50.0, 3.0)), Bits(jogged.JogPoint));
            Assert.Equal(Bits(new GeoPoint3(301.0, -38.0, 3.0)), Bits(jogged.Center));
        }

        #endregion

        #region Adding and erasing

        [Fact]
        public void ADimensionMadeInCode_NeedsItsBlock_AndIsWrittenWithIt()
        {
            string text = DxfDimensionDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            var made = new DxfRotatedDimension(Math.PI / 2.0, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), new GeoPoint3(-20, 25, 0), "<>", db.DimStyleTable["GH_DIM"]) { Layer = "DIMS" };

            Assert.Equal(50.0, made.Measurement, 12);
            Refused<ArgumentException>(() => db.ModelSpace.AppendEntity(made), "no block named by the dimension");
            Assert.False(made.IsResident);

            made.DimBlock = db.BlockTable["*D2"];
            made.Dimasz = 1.5;
            db.ModelSpace.AppendEntity(made);

            DxfRotatedDimension read = Dim<DxfRotatedDimension>(DxfDatabase.Parse(db.ToText()), made.Handle);
            Assert.Equal("*D2", read.DimBlock.Name);
            Assert.Equal("GH_DIM", read.DimensionStyleName);
            Assert.Equal(1.5, read.Dimasz);
            Assert.Equal(Math.PI / 2.0, read.Rotation, 15);
            Assert.Equal(50.0, read.Measurement, 12);
            Assert.Contains("100\nAcDbRotatedDimension\n", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ACopy_SharesItsBlockInItsOwnDrawing_AndBringsACopyOfItToAnother()
        {
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfArcDimension arc = Dim<DxfArcDimension>(source, "AA");

            DxfEntity same = arc.Clone();
            source.ModelSpace.AppendEntity(same);
            Assert.Equal("*D7", ((DxfArcDimension)same).DimBlock.Name);

            // A drawing with no *D7 is given a copy of it, every entity of it.
            DxfDatabase other = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var brought = (DxfArcDimension)arc.Clone();
            other.ModelSpace.AppendEntity(brought);
            Assert.StartsWith("*D", brought.DimBlock.Name, StringComparison.Ordinal);
            Assert.Equal(arc.DimBlock.Count, DxfDatabase.Parse(other.ToText()).BlockTable[brought.DimBlock.Name].Count);

            // An arc length written into a file older than R2018 is of kind 5, as ezdxf writes it.
            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            DxfEntity copy = arc.Clone();
            r2000.ModelSpace.AppendEntity(copy);
            Assert.Contains("\n 70\n37\n", Records(r2000.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void ACopyInItsOwnDrawing_SharesTheBlock_SoMovingItLeavesTheOriginalsPicture()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension original = Dim<DxfRotatedDimension>(db, "4D");
            var copy = (DxfRotatedDimension)original.Clone();
            db.ModelSpace.AppendEntity(copy);
            GeoPoint3 start = original.DimBlock.OfType<DxfLine>().First().StartPoint;

            copy.TransformBy(GeoTransform3.Translation(new GeoVector3(0.0, 100.0, 0.0)));

            Assert.Equal(Bits(start), Bits(original.DimBlock.OfType<DxfLine>().First().StartPoint));
            Assert.Equal(100.0 + 20.0, copy.XLine2Point.Y, 12);
            Assert.Contains("\n 70\n128\n", Records(db.ToText()).Single(r => HandleOf(r) == copy.Handle), StringComparison.Ordinal);
        }

        [Fact]
        public void ADimensionErased_IsGone_AndItsBlockKept()
        {
            string text = DxfDimensionDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRadialDimension dim = Dim<DxfRadialDimension>(db, "75");

            dim.Erase();

            Refused<InvalidOperationException>(() => dim.DimensionText = "x", "is erased");
            string saved = db.ToText();
            OnlyTheseChanged(text, saved, "75");
            Assert.Contains("\n  2\n*D3\n", saved, StringComparison.Ordinal);
        }

        #endregion

        #region Fix round 1

        [Fact]
        public void ARecordSetOnADimensionInNoDrawing_IsTakenToItsNamesake_AndOneTheDrawingHasNotIsRefused()
        {
            // Y and X are the same drawing, each given the linetype GH_LT and the style GH_TS, Y after records X has not, so that
            // their handles differ; X has no CENTER.
            DxfDatabase y = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            y.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "GH_PAD" });
            y.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_PAD" });
            y.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "GH_LT" });
            y.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TS" });
            DxfDatabase x = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace("  2\nCENTER\n", "  2\nCENTRE\n"));
            x.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "GH_LT" });
            x.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "GH_TS" });
            Assert.NotEqual(y.LinetypeTable["GH_LT"].Handle, x.LinetypeTable["GH_LT"].Handle);

            var made = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 5, 0), string.Empty, null);
            made.DimBlock = y.BlockTable["*D2"];
            made.Dimltex2 = y.LinetypeTable["GH_LT"];
            made.TextStyle = y.TextStyleTable["GH_TS"];
            made.Dimblk = null;

            Assert.Same(y.LinetypeTable["GH_LT"], made.Dimltex2);
            x.ModelSpace.AppendEntity(made);

            Assert.Same(x.LinetypeTable["GH_LT"], made.Dimltex2);
            Assert.Same(x.TextStyleTable["GH_TS"], made.TextStyle);
            Assert.Null(made.Dimblk);
            string record = Records(x.ToText()).Single(r => HandleOf(r) == made.Handle);
            Assert.Contains("1070\n347\n1005\n" + x.LinetypeTable["GH_LT"].Handle + "\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n340\n1005\n" + x.TextStyleTable["GH_TS"].Handle + "\n", record, StringComparison.Ordinal);

            // A drawing with no CENTER refuses the dimension that names one, and stays as it was; so does one too old for DIMLTYPE.
            var other = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 5, 0), string.Empty, null) { DimBlock = y.BlockTable["*D2"] };
            other.Dimltype = y.LinetypeTable["CENTER"];
            string before = x.ToText();
            Refused<ArgumentException>(() => x.ModelSpace.AppendEntity(other), "no linetype named CENTER");
            Assert.Equal(before, x.ToText());
            DxfDatabase r2000 = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            Refused<ArgumentException>(() => r2000.ModelSpace.AppendEntity(other), "DIMLTYPE, which came with R2007");

            // A dimension of a drawing takes no record of another.
            DxfRotatedDimension resident = Dim<DxfRotatedDimension>(x, "61");
            Refused<ArgumentException>(() => resident.Dimltype = y.LinetypeTable["DASHED"], "same drawing");
        }

        [Fact]
        public void ACopyForAnotherDrawing_KeepsItsOverridesOfRecords_TakenToTheirNamesakesThere()
        {
            // The target is the same drawing with no GH_TXT2 and its CENTER added after the rest, so that its handle differs.
            DxfDatabase source = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase target = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace("  2\nGH_TXT2\n", "  2\nGH_TXT3\n").Replace("  2\nCENTER\n", "  2\nCENTRE\n"));
            target.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "CENTER" });
            Assert.NotEqual(source.LinetypeTable["CENTER"].Handle, target.LinetypeTable["CENTER"].Handle);
            DxfEntity copy = Dim<DxfRotatedDimension>(source, "4D").Clone();

            target.ModelSpace.AppendEntity(copy);

            var read = (DxfRotatedDimension)DxfDatabase.Parse(target.ToText()).ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == copy.Handle);
            Assert.Equal(4.5, read.Dimasz);
            Assert.Equal('.', read.Dimdsep);
            Assert.Equal("GH_TXT2", read.TextStyle.Name);
            Assert.Equal("CENTER", read.Dimltype.Name);
            Assert.Equal("_CLOSED", read.Dimblk.Name);
            string record = Records(target.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains("1070\n345\n1005\n" + target.LinetypeTable["CENTER"].Handle + "\n", record, StringComparison.Ordinal);
            Assert.Contains("1070\n340\n1005\n" + target.TextStyleTable["GH_TXT2"].Handle + "\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void ACopyOfAnotherDrawing_GivenARecordInNoDrawing_KeepsIt_TakenToItsNamesakeThere()
        {
            // Y and X are the same drawing, X's DASHED added after the rest, so that its handle differs.
            DxfDatabase y = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDatabase x = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace("  2\nDASHED\n", "  2\nDASHES\n"));
            x.LinetypeTable.Add(new DxfLinetypeTableRecord { Name = "DASHED" });
            Assert.NotEqual(y.LinetypeTable["DASHED"].Handle, x.LinetypeTable["DASHED"].Handle);
            var copy = (DxfRotatedDimension)Dim<DxfRotatedDimension>(y, "61").Clone();

            copy.Dimltype = y.LinetypeTable["DASHED"];
            x.ModelSpace.AppendEntity(copy);

            Assert.Same(x.LinetypeTable["DASHED"], copy.Dimltype);
            string record = Records(x.ToText()).Single(r => HandleOf(r) == copy.Handle);
            Assert.Contains("1070\n345\n1005\n" + x.LinetypeTable["DASHED"].Handle + "\n", record, StringComparison.Ordinal);
            Assert.Equal("DASHED", Dim<DxfRotatedDimension>(DxfDatabase.Parse(x.ToText()), copy.Handle).Dimltype.Name);
        }

        [Fact]
        public void ACloneOfADimensionInNoDrawing_HoldsTheRecordsSetOnIt()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            var made = new DxfRotatedDimension(0.0, new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(5, 5, 0), string.Empty, null) { DimBlock = db.BlockTable["*D2"] };
            made.Dimltype = db.LinetypeTable["DASHED"];

            var clone = (DxfRotatedDimension)made.Clone();

            Assert.Same(db.LinetypeTable["DASHED"], clone.Dimltype);
            db.ModelSpace.AppendEntity(clone);
            Assert.Equal("DASHED", Dim<DxfRotatedDimension>(DxfDatabase.Parse(db.ToText()), clone.Handle).Dimltype.Name);
        }

        [Fact]
        public void AnOrdinate_MeasuresAlongTheUsersAxesOf51_AndAMoveOrATurnKeepsItsMeasurement()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfOrdinateDimension x = Dim<DxfOrdinateDimension>(db, "BB");
            DxfOrdinateDimension y = Dim<DxfOrdinateDimension>(db, "C6");
            GeoTransform3 turn = GeoTransform3.Translation(new GeoVector3(10.0, 20.0, 5.0)) * GeoTransform3.RotationZ(30.0 * Math.PI / 180.0);

            x.TransformBy(turn);
            y.TransformBy(turn);

            Assert.Equal(20.0, x.Measurement, 9);
            Assert.Equal(30.0, y.Measurement, 9);
            Assert.Equal(-30.0 * Math.PI / 180.0, x.HorizontalRotation, 12);
            Assert.Contains("\n 51\n-", Records(db.ToText()).Single(r => HandleOf(r) == "BB"), StringComparison.Ordinal);
            Assert.Equal(20.0, Dim<DxfOrdinateDimension>(DxfDatabase.Parse(db.ToText()), "BB").Measurement, 9);

            // An ordinate whose user's x stood at 90 degrees (51 of -90) measures along the world's y.
            DxfOrdinateDimension upright = Dim<DxfOrdinateDimension>(DxfDatabase.Parse(DxfDimensionDrawings.R2018), "BB");
            upright.HorizontalRotation = -Math.PI / 2.0;
            Assert.Equal(30.0, upright.Measurement, 12);
        }

        [Fact]
        public void ANewNormal_KeepsTheDirectionsOfTheAnglesInTheWorld()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfRotatedDimension dim = Dim<DxfRotatedDimension>(db, "4D");

            dim.Normal = new GeoVector3(0.0, 0.0, -1.0);

            // The system of (0, 0, -1) has x (-1, 0, 0): the direction at 30 degrees is at 150 there.
            Assert.Equal(150.0 * Math.PI / 180.0, dim.Rotation, 12);
            Assert.Equal(96.60254037844386, dim.Measurement, 9);
            Near(new GeoPoint3(60.0, 45.0, 0.0), dim.TextPosition);
        }

        [Fact]
        public void AnAttachmentTheFileHoldsOutOfRange_SetAsItIsRead_WritesNothing()
        {
            string text = DxfDimensionDrawings.R2018.Replace(" 70\n36\n 71\n5\n", " 70\n36\n 71\n0\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfRadialDimension dim = Dim<DxfRadialDimension>(db, "75");

            dim.TextAttachment = dim.TextAttachment;
            Assert.Equal(text, db.ToText());
            Refused<ArgumentOutOfRangeException>(() => dim.TextAttachment = (DxfAttachmentPoint)10, "nine points");
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }

        #endregion

        /// <summary>A point of the system of a normal in the world, by the arbitrary axis algorithm.</summary>
        private static GeoPoint3 OcsWorld(GeoVector3 normal, GeoPoint3 p)
        {
            GeoVector3 x = Math.Abs(normal.X) < 1.0 / 64.0 && Math.Abs(normal.Y) < 1.0 / 64.0 ? new GeoVector3(0, 1, 0).CrossProduct(normal) : new GeoVector3(0, 0, 1).CrossProduct(normal);
            x = x.Divide(x.Length);
            GeoVector3 y = normal.CrossProduct(x);
            y = y.Divide(y.Length);
            return new GeoPoint3(x.X * p.X + y.X * p.Y + normal.X * p.Z, x.Y * p.X + y.Y * p.Y + normal.Y * p.Z, x.Z * p.X + y.Z * p.Y + normal.Z * p.Z);
        }
    }
}
