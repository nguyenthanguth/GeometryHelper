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
    /// The HATCH as a <see cref="DxfHatch"/>: read as ezdxf 1.4.5 reads it, its loops given as the library's shapes and its area
    /// worked out exactly, its pattern, gradient, association and seeds set and read back, loops added and taken out, made in
    /// code and appended, erased, and moved as ezdxf moves one; every other record of the file keeping its bytes.
    /// </summary>
    public class DxfHatchTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static string[] Drawings => new[] { DxfHatchLeaderDrawings.R2000, DxfHatchLeaderDrawings.R2018 };

        #region Read as ezdxf reads it

        [Fact]
        public void TheHatchesOfEzdxf_AreReadAsEzdxfReadsThem()
        {
            // As ezdxf 1.4.5 reads them (dump3b.py of the A3b round).
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfHatch ansi = ByHandle<DxfHatch>(db, "AC");

                Assert.Equal("ANSI31", ansi.PatternName);
                Assert.Equal(DxfHatchPatternType.PreDefined, ansi.PatternType);
                Assert.False(ansi.IsSolidFill);
                Assert.Equal(2.0, ansi.PatternScale);
                Assert.Equal(Bits(45.0 * (Math.PI / 180.0)), Bits(ansi.PatternAngle));
                Assert.False(ansi.PatternDouble);
                Assert.Equal(DxfHatchStyle.Outer, ansi.HatchStyle);
                Assert.True(ansi.Associative);
                Assert.Equal(0.0, ansi.Elevation);
                Assert.Equal(Bits(new GeoVector3(0, 0, 1)), Bits(ansi.Normal));
                Assert.Equal(1, ansi.NumberOfLoops);
                Assert.Equal(DxfHatchLoopTypes.External | DxfHatchLoopTypes.Polyline, ansi.LoopTypeAt(0));
                Assert.Equal(2, ansi.NumSeedPoints);
                Assert.Equal(new GeoPoint2(120, 5), ansi.GetSeedPointAt(1));
                Assert.Equal(1, ansi.NumberOfPatternDefinitions);
                DxfPatternDefinition line = ansi.GetPatternDefinitionAt(0);
                Assert.Equal(Bits(90.0 * (Math.PI / 180.0)), Bits(line.Angle));
                Assert.Equal(-6.3500000001, line.OffsetX);
                Assert.Equal(0.0, line.OffsetY);
                Assert.Empty(line.GetDashes());
                Assert.Equal(new[] { "AB" }, ansi.GetAssociatedObjects().Select(e => e.Handle));
                Assert.IsType<DxfPolyline>(Assert.Single(ansi.GetAssociatedObjectsAt(0)));
                Assert.False(ansi.IsGradient);
                Assert.True(ansi.IsHatch);
                Assert.True(ansi.BackgroundColor.IsNone);
                Assert.Throws<ArgumentOutOfRangeException>(() => ansi.GetLoopAt(1));

                DxfHatch user = ByHandle<DxfHatch>(db, "AE");
                Assert.Equal("_USER", user.PatternName);
                Assert.Equal(DxfHatchPatternType.UserDefined, user.PatternType);
                Assert.Equal(3.0, user.PatternSpace);
                Assert.Equal(1.0, user.PatternScale);
                Assert.Equal(Bits(30.0 * (Math.PI / 180.0)), Bits(user.PatternAngle));
                Assert.True(user.PatternDouble);
                Assert.Equal(5.0, user.Elevation);
                Assert.Equal(Bits(new GeoVector3(0.26690419858597586, -0.5348080383651577, 0.8017122369511335)), Bits(user.Normal));
                Assert.Equal(2, user.NumberOfPatternDefinitions);

                DxfHatch solid = ByHandle<DxfHatch>(db, "AD");
                Assert.True(solid.IsSolidFill);
                Assert.Equal("SOLID", solid.PatternName);
                Assert.Equal(0, solid.NumberOfPatternDefinitions);
                Assert.Equal(DxfHatchLoopTypes.External, solid.LoopTypeAt(0));
                Assert.Equal(DxfHatchLoopTypes.Outermost | DxfHatchLoopTypes.Polyline, solid.LoopTypeAt(1));

                DxfHatch background = ByHandle<DxfHatch>(db, "B0");
                Assert.Equal(DxfColorMethod.ByColor, background.BackgroundColor.ColorMethod);
                Assert.Equal(0x0A141E, background.BackgroundColor.ColorValue);
                Assert.Equal(0.5, background.PatternScale);
            }
        }

        [Fact]
        public void TheGradientOfEzdxf_IsReadWithItsColours_AndTheHatchOfR2000HasNone()
        {
            DxfHatch gradient = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AF");

            Assert.True(gradient.IsGradient);
            Assert.Equal(DxfHatchObjectType.GradientObject, gradient.HatchObjectType);
            Assert.Equal("CYLINDER", gradient.GradientName);
            Assert.Equal(DxfGradientPatternType.PreDefinedGradient, gradient.GradientType);
            Assert.Equal(Bits(0.5235987755982988), Bits(gradient.GradientAngle));
            Assert.Equal(1f, gradient.GradientShift);
            Assert.Equal(0f, gradient.ShadeTintValue);
            Assert.False(gradient.GradientOneColorMode);
            DxfGradientColor[] colours = gradient.GetGradientColors();
            Assert.Equal(new[] { 0xFF0000, 0x0000FF }, colours.Select(c => c.Color.ColorValue));
            Assert.Equal(new[] { 0f, 1f }, colours.Select(c => c.Value));

            DxfHatch none = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AF");
            Assert.False(none.IsGradient);
            Assert.Empty(none.GetGradientColors());
            Assert.Equal(string.Empty, none.GradientName);
        }

        #endregion

        #region The loops as shapes, and the area

        [Fact]
        public void ALoopOfEdges_IsItsLinesArcsEllipseAndSpline_EachRunningOnFromTheLast()
        {
            // Lines, an arc counter-clockwise, one clockwise (73 of 0, its angles the complements), half an ellipse and a spline:
            // each edge starts where the one before it ends, and the loop closes.
            DxfHatch hatch = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AD");
            DxfHatchLoop loop = hatch.GetLoopAt(0);

            Assert.False(loop.IsPolyline);
            Assert.Null(loop.Polyline);
            Assert.Equal(new[] { typeof(GeoLine3), typeof(GeoArc3), typeof(GeoLine3), typeof(GeoArc3), typeof(GeoLine3), typeof(GeoSpline3), typeof(GeoSpline3) }, loop.Curves.Select(c => c.GetType()));

            var ends = loop.Curves.Select(Ends).ToList();

            for (int i = 0; i < ends.Count; i++)
            {
                Assert.True(ends[i][1].IsEqualTo(ends[(i + 1) % ends.Count][0], Tol), "Edge " + i + " ends at " + ends[i][1] + ", the next starts at " + ends[(i + 1) % ends.Count][0] + ".");
            }

            var notch = (GeoArc3)loop.Curves[3];
            Assert.True(notch.MidPoint.IsEqualTo(new GeoPoint3(225, 15, 0), Tol));
            var ellipse = (GeoSpline3)loop.Curves[5];
            Assert.True(ellipse.GetPointAtKnot((ellipse.DomainStart + ellipse.DomainEnd) / 2).IsEqualTo(new GeoPoint3(210, 22.5, 0), Tol));

            GeoPolygonArc3 island = Assert.IsType<GeoPolygonArc3>(hatch.GetLoopAt(1).Polyline);
            Assert.Equal(50.0, island.Area, 9);
        }

        [Fact]
        public void APolylineLoopOfTwoVertices_IsItsCircle_AndATiltedCircleEdgeLiesInTheHatchsPlane()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            GeoPolygonArc3 circle = Assert.IsType<GeoPolygonArc3>(ByHandle<DxfHatch>(db, "AF").GetLoopAt(0).Polyline);
            Assert.Equal(3, circle.VertexCount);
            Assert.Equal(Math.PI * 100.0, circle.Area, 9);

            DxfHatch tilted = ByHandle<DxfHatch>(db, "AE");
            var edge = Assert.IsType<GeoCircle3>(Assert.Single(tilted.GetLoopAt(0).Curves));
            Assert.Equal(10.0, edge.Radius);
            Assert.True(edge.Normal.IsParallelTo(tilted.Normal, Tol));

            var whole = Assert.IsType<GeoEllipse3>(Assert.Single(ByHandle<DxfHatch>(db, "B0").GetLoopAt(0).Curves));
            Assert.Equal(8.0, whole.MajorRadius);
            Assert.Equal(2.0, whole.MinorRadius);
            Assert.Equal(Bits(new GeoVector3(0, 1, 0)), Bits(whole.MajorAxis));
        }

        [Fact]
        public void TheArea_IsThatOfTheLoops_TheIslandTakenOff_ExactlyOnTheirArcsEllipsesAndSplines()
        {
            // AD: 750 of its polygon, 5.25 more between its spline and the chord, half a circle of radius 10 out, a notch of
            // radius 5 in, half an ellipse 5 by 2.5 out, and the island of 50 taken off: 755.25 + 43.75 pi - 50, worked out by
            // fractions. AC: the rectangle 30 by 20 and the segment of its arc of bulge 0.5, radius 12.5.
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            double angle = 4.0 * Math.Atan(0.5);

            Assert.Equal(100.0, ByHandle<DxfHatch>(db, "74").Area, 12);
            Assert.Equal(600.0 + 0.5 * 12.5 * 12.5 * (angle - Math.Sin(angle)), ByHandle<DxfHatch>(db, "AC").Area, 10);
            Assert.Equal(755.25 + 43.75 * Math.PI - 50.0, ByHandle<DxfHatch>(db, "AD").Area, 10);
            Assert.Equal(100.0 * Math.PI, ByHandle<DxfHatch>(db, "AE").Area, 10);
            Assert.Equal(100.0 * Math.PI, ByHandle<DxfHatch>(db, "AF").Area, 10);
            Assert.Equal(16.0 * Math.PI, ByHandle<DxfHatch>(db, "B0").Area, 10);
        }

        [Fact]
        public void TheStyle_DecidesWhatAnIslandWithinAnIslandAddsToTheArea()
        {
            // Squares of side 30, 20 and 10 about one centre: normal fills the outer ring and the inner square, 900 - 400 + 100;
            // outer the ring alone, 500; ignore all of it, 900.
            var hatch = new DxfHatch();

            foreach (double half in new[] { 15.0, 10.0, 5.0 })
            {
                hatch.AppendLoop(DxfHatchLoopTypes.Default, new[] { new GeoPoint2(-half, -half), new GeoPoint2(half, -half), new GeoPoint2(half, half), new GeoPoint2(-half, half) }, null);
            }

            Assert.Equal(600.0, hatch.Area, 12);
            hatch.HatchStyle = DxfHatchStyle.Outer;
            Assert.Equal(500.0, hatch.Area, 12);
            hatch.HatchStyle = DxfHatchStyle.Ignore;
            Assert.Equal(900.0, hatch.Area, 12);
        }

        #endregion

        #region Nothing changed

        [Fact]
        public void EverySetterGivenTheValueItGives_LeavesTheFileByteForByte()
        {
            foreach (string text in Drawings.Concat(new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 }))
            {
                SettingWhatItHasChangesNothing<DxfHatch>(text, h =>
                {
                    h.Elevation = h.Elevation;
                    h.Normal = h.Normal;
                    h.PatternScale = h.PatternScale;
                    h.PatternAngle = h.PatternAngle;
                    h.PatternSpace = h.PatternSpace;
                    h.PatternDouble = h.PatternDouble;
                    h.HatchStyle = h.HatchStyle;
                    h.Associative = h.Associative;
                    h.SetHatchPattern(h.PatternType, h.PatternName);
                    h.HatchObjectType = h.HatchObjectType;
                    h.BackgroundColor = h.BackgroundColor;

                    if (h.IsGradient)
                    {
                        h.SetGradient(h.GradientType, h.GradientName);
                        h.GradientAngle = h.GradientAngle;
                        h.GradientShift = h.GradientShift;
                        h.ShadeTintValue = h.ShadeTintValue;
                        h.GradientOneColorMode = h.GradientOneColorMode;
                        h.SetGradientColors(h.GetGradientColors());
                    }

                    h.TransformBy(GeoTransform3.Identity);
                });
            }
        }

        #endregion

        #region Set and read back

        [Fact]
        public void ThePatternsScaleAndAngle_AreWritten_AndItsLinesScaledAndTurnedWithThem()
        {
            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");

                hatch.PatternScale = 3.0;
                hatch.PatternAngle = Math.PI / 2.0;
                OnlyTheseChanged(text, db.ToText(), "AC");

                DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AC");
                Assert.Equal(3.0, back.PatternScale);
                Assert.Equal(Bits(Math.PI / 2.0), Bits(back.PatternAngle));
                DxfPatternDefinition line = back.GetPatternDefinitionAt(0);

                // ANSI31's line at 90 degrees, 6.35 apart at the scale 2: at 3 and turned on by 45, 135 degrees, 9.525 apart.
                Assert.Equal(135.0, line.Angle * (180.0 / Math.PI), 9);
                Assert.Equal(-6.3500000001 * 1.5 * Math.Cos(Math.PI / 4.0), line.OffsetX, 9);
                Assert.Equal(-6.3500000001 * 1.5 * Math.Sin(Math.PI / 4.0), line.OffsetY, 9);
                Assert.Throws<InvalidOperationException>(() => back.PatternSpace = 2.0);
            }
        }

        [Fact]
        public void APatternMadeByTheUser_IsGivenItsLines_AsItsSpacingAngleAndDoubleSay()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "74");

            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            Assert.False(hatch.IsSolidFill);
            Assert.Equal(1, hatch.NumberOfPatternDefinitions);
            hatch.PatternSpace = 2.0;
            hatch.PatternAngle = Math.PI / 6.0;
            hatch.PatternDouble = true;

            DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "74");
            Assert.Equal(2, back.NumberOfPatternDefinitions);
            DxfPatternDefinition first = back.GetPatternDefinitionAt(0);
            DxfPatternDefinition second = back.GetPatternDefinitionAt(1);
            Assert.Equal(30.0, first.Angle * (180.0 / Math.PI), 9);
            Assert.Equal(120.0, second.Angle * (180.0 / Math.PI), 9);
            Assert.Equal(2.0, Math.Sqrt(first.OffsetX * first.OffsetX + first.OffsetY * first.OffsetY), 12);
            Assert.Equal(-2.0 * Math.Sin(Math.PI / 6.0), first.OffsetX, 12);
            Assert.Throws<InvalidOperationException>(() => back.PatternScale = 2.0);
        }

        [Fact]
        public void SolidFill_TakesThePatternOut_AndAPatternCannotBeScaledOnIt()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");

            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            OnlyTheseChanged(text, db.ToText(), "AC");
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "AC");
            Assert.Contains("\n  2\nSOLID\n 70\n1\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n 78\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n 52\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain("\n 41\n", record, StringComparison.Ordinal);

            DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AC");
            Assert.True(back.IsSolidFill);
            Assert.Equal(1.0, back.PatternScale);
            Assert.Throws<InvalidOperationException>(() => back.PatternScale = 2.0);
            Assert.Throws<InvalidOperationException>(() => back.PatternAngle = 1.0);

            // A pattern of AutoCAD's file the drawing holds no lines of is written by its name, its lines AutoCAD's to give.
            back.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI37");
            Assert.False(back.IsSolidFill);
            Assert.Equal("ANSI37", back.PatternName);
            Assert.Equal(0, back.NumberOfPatternDefinitions);
        }

        [Fact]
        public void TheAssociation_IsTakenAway_WithTheHandles_WhenSetFalseOrTheLoopsChange()
        {
            foreach (Action<DxfHatch> change in new Action<DxfHatch>[] { h => h.Associative = false, h => h.RemoveAssociatedObjects(), h => h.RemoveLoopAt(0) })
            {
                string text = DxfHatchLeaderDrawings.R2018;
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");

                change(hatch);
                OnlyTheseChanged(text, db.ToText(), "AC", "AB");
                string record = Records(db.ToText()).Single(r => HandleOf(r) == "AC");
                Assert.Contains("\n 71\n0\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("\n330\nAB\n", record, StringComparison.Ordinal);

                // The circle it followed no longer has it among its reactors, as AutoCAD takes it out: its list, left empty, gone.
                Assert.Contains("{ACAD_REACTORS", Records(text).Single(r => HandleOf(r) == "AB"), StringComparison.Ordinal);
                Assert.DoesNotContain("{ACAD_REACTORS", Records(db.ToText()).Single(r => HandleOf(r) == "AB"), StringComparison.Ordinal);

                DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AC");
                Assert.False(back.Associative);
                Assert.Empty(back.GetAssociatedObjects());
            }
        }

        [Fact]
        public void ALoopReadFromAHatch_IsAppendedAsItWasRead_AndOneOfShapesFromItsShapes()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "AD");
            string before = Records(text).Single(r => HandleOf(r) == "AD");

            hatch.AppendLoop(hatch.GetLoopAt(0));
            Assert.Equal(3, hatch.NumberOfLoops);
            string after = Records(db.ToText()).Single(r => HandleOf(r) == "AD");
            int from = before.IndexOf(" 92\n1\n", StringComparison.Ordinal);
            string loop = before.Substring(from, before.IndexOf(" 92\n18\n", StringComparison.Ordinal) - from);
            Assert.Contains(loop + " 75\n0\n", after, StringComparison.Ordinal);

            // A circle of radius 2 inside the island and a square of shapes outside: the circle an island of the island.
            hatch.InsertLoopAt(0, new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoCircle3(new GeoPoint3(215, 7.5, 0), new GeoVector3(0, 0, 1), 2) }));
            hatch.RemoveLoopAt(3);
            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.External, new GeoPolygonArc3(new[] { new GeoPoint3(300, 0, 0), new GeoPoint3(310, 0, 0), new GeoPoint3(310, 10, 0), new GeoPoint3(300, 10, 0) })));
            OnlyTheseChanged(text, db.ToText(), "AD");

            DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AD");
            Assert.Equal(4, back.NumberOfLoops);
            Assert.Equal(DxfHatchLoopTypes.External | DxfHatchLoopTypes.Polyline, back.LoopTypeAt(3));
            Assert.Equal(755.25 + 43.75 * Math.PI - 50.0 + 4.0 * Math.PI + 100.0, back.Area, 9);
            Assert.Throws<ArgumentException>(() => back.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoLine3(new GeoPoint3(0, 0, 1), new GeoPoint3(1, 0, 1)) })));
        }

        [Fact]
        public void TheGradient_IsSetFromR2004On_ItsAngleAndColoursWritten()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");

            hatch.SetGradient(DxfGradientPatternType.PreDefinedGradient, "spherical");
            hatch.GradientAngle = 1.0;
            hatch.SetGradientColors(new[] { new DxfGradientColor(DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1), 0f), new DxfGradientColor(DxfColor.FromRgb(0, 128, 0), 1f) });
            hatch.GradientOneColorMode = true;

            DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AC");
            Assert.True(back.IsGradient);
            Assert.True(back.IsSolidFill);
            Assert.Equal("SPHERICAL", back.GradientName);
            Assert.Equal(1.0, back.GradientAngle);
            Assert.True(back.GradientOneColorMode);
            DxfGradientColor[] colours = back.GetGradientColors();
            Assert.Equal(DxfColorMethod.ByAci, colours[0].Color.ColorMethod);
            Assert.Equal(1, colours[0].Color.ColorIndex);
            Assert.Equal(0x008000, colours[1].Color.ColorValue);
            Assert.Equal(0, back.NumberOfPatternDefinitions);
            Assert.Throws<ArgumentException>(() => back.SetGradient(DxfGradientPatternType.PreDefinedGradient, "RAINBOW"));

            back.HatchObjectType = DxfHatchObjectType.HatchObject;
            Assert.False(back.IsGradient);
            Assert.Equal("SOLID", back.PatternName);

            DxfHatch old = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AC");
            Assert.Throws<InvalidOperationException>(() => old.SetGradient(DxfGradientPatternType.PreDefinedGradient, "LINEAR"));
            Assert.Throws<InvalidOperationException>(() => old.GradientAngle = 1.0);
        }

        [Fact]
        public void TheBackgroundColourAndTheSeeds_AreWrittenAndTakenOut()
        {
            string text = DxfHatchLeaderDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "AC");

            hatch.BackgroundColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3);
            hatch.AppendSeedPoint(new GeoPoint2(101, 1));
            hatch.RemoveSeedPointAt(0);
            ByHandle<DxfHatch>(db, "B0").BackgroundColor = DxfColor.FromColorIndex(DxfColorMethod.None, 0);
            OnlyTheseChanged(text, db.ToText(), "AC", "B0");

            DxfDatabase back = DxfDatabase.Parse(db.ToText());
            DxfHatch again = ByHandle<DxfHatch>(back, "AC");
            Assert.Equal(DxfColorMethod.ByAci, again.BackgroundColor.ColorMethod);
            Assert.Equal(3, again.BackgroundColor.ColorIndex);
            Assert.Equal(new[] { new GeoPoint2(120, 5), new GeoPoint2(101, 1) }, Enumerable.Range(0, again.NumSeedPoints).Select(again.GetSeedPointAt));
            Assert.True(ByHandle<DxfHatch>(back, "B0").BackgroundColor.IsNone);
            Assert.Null(ByHandle<DxfHatch>(back, "B0").GetXDataForApplication("HATCHBACKGROUNDCOLOR"));
        }

        #endregion

        #region Made in code, appended and erased

        [Fact]
        public void AHatchMadeInCode_IsAppendedFromR2000On_AndRefusedByR12()
        {
            foreach (string text in new[] { DxfFileEzdxfDrawings.R12, DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var hatch = new DxfHatch { Layer = "steel", ColorIndex = 4 };
                hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
                hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, new[] { 0.0, 0.0, 0.0, 0.0 });
                hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoCircle3(new GeoPoint3(15, 10, 0), new GeoVector3(0, 0, 1), 5) }));

                if (text == DxfFileEzdxfDrawings.R12)
                {
                    Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(hatch));
                    continue;
                }

                db.ModelSpace.AppendEntity(hatch);
                Assert.NotNull(hatch.Handle);
                DxfHatch back = ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), hatch.Handle);
                Assert.Equal("ANSI31", back.PatternName);
                Assert.Equal(2, back.NumberOfLoops);
                Assert.Equal(600.0 - 25.0 * Math.PI, back.Area, 10);
                Assert.Equal(4, back.ColorIndex);
                Assert.Equal("*Model_Space", back.BlockName);
            }
        }

        [Fact]
        public void AnErasedHatch_IsGone_TheRestKept_AndRefusesToBeSet()
        {
            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfHatch hatch = ByHandle<DxfHatch>(db, "B0");

            hatch.Erase();
            OnlyTheseChanged(text, db.ToText(), "B0");
            Assert.Throws<InvalidOperationException>(() => hatch.PatternScale = 2.0);
            Assert.Throws<InvalidOperationException>(() => hatch.AppendSeedPoint(new GeoPoint2(0, 0)));
        }

        #endregion

        #region Moved

        [Fact]
        public void AHatchMoved_HasItsLoopsPatternAndSeedsMovedAsEzdxfMovesThem()
        {
            // Turned 30 degrees, scaled 2 and moved by (5, -7, 3): ezdxf's transform() gives the same groups but for the seed
            // points, which ezdxf leaves, and the gradient's angle, which it does not turn (the A3b oracle, cmptransform.py).
            double c = Math.Sqrt(3.0);
            var transform = new GeoTransform3(new double[,] { { c, -1, 0, 5 }, { 1, c, 0, -7 }, { 0, 0, 2, 3 }, { 0, 0, 0, 1 } });

            foreach (string text in Drawings)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var areas = db.ModelSpace.OfType<DxfHatch>().ToDictionary(h => h.Handle, h => h.Area, StringComparer.Ordinal);
                DxfHatch hatch = ByHandle<DxfHatch>(db, "AD");
                List<GeoPoint3> starts = hatch.GetLoopAt(0).Curves.Select(e => transform.Transform(Ends(e)[0])).ToList();

                foreach (DxfHatch each in db.ModelSpace.OfType<DxfHatch>().ToList())
                {
                    each.TransformBy(transform);
                }

                DxfDatabase back = DxfDatabase.Parse(db.ToText());
                DxfHatch moved = ByHandle<DxfHatch>(back, "AD");
                List<GeoPoint3> now = moved.GetLoopAt(0).Curves.Select(e => Ends(e)[0]).ToList();

                for (int i = 0; i < starts.Count; i++)
                {
                    Assert.True(starts[i].IsEqualTo(now[i], Tol), "Edge " + i + " starts at " + now[i] + ", not " + starts[i] + ".");
                }

                foreach (DxfHatch each in back.ModelSpace.OfType<DxfHatch>())
                {
                    Assert.Equal(areas[each.Handle] * 4.0, each.Area, 8);
                }

                DxfHatch ansi = ByHandle<DxfHatch>(back, "AC");
                Assert.Equal(4.0, ansi.PatternScale, 12);
                Assert.Equal(75.0, ansi.PatternAngle * (180.0 / Math.PI), 9);
                Assert.True(new GeoPoint2(110 * c - 10 + 5, 110 + 10 * c - 7).IsEqualTo(ansi.GetSeedPointAt(0), Tol));
                Assert.Equal(3.0, ansi.Elevation, 12);
            }
        }

        [Fact]
        public void AHatchScaledUnlikeAlongItsAxes_HasItsArcsMadeEllipses_AndItsAreaScaledSo()
        {
            // Twice as wide: each hatch on the floor fills twice its area, its arcs and bulges ellipse edges, as ezdxf makes them.
            var transform = new GeoTransform3(new double[,] { { 2, 0, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            foreach (string handle in new[] { "74", "AC", "AD", "AF", "B0" })
            {
                DxfHatch hatch = ByHandle<DxfHatch>(db, handle);
                double area = hatch.Area;
                hatch.TransformBy(transform);
                Assert.Equal(2.0 * area, ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), handle).Area, 8);
            }

            Assert.All(ByHandle<DxfHatch>(db, "AF").GetLoopAt(0).Curves, e => Assert.IsType<GeoSpline3>(e));
            Assert.True(ByHandle<DxfHatch>(db, "AC").Associative);
        }

        #endregion

        #region Review round 1

        [Fact]
        public void AHatchSheared_HasItsArcsAndEllipsesMadeTheEllipsesTheyBecome_AndItsAreaTwice()
        {
            // S(2, 1) after R(45 degrees): the axes stay as long as each other but are no longer square, a circle becomes an
            // ellipse, and every area doubles (the determinant is 2). The ellipse of B0 (major 8 along y, ratio 0.25) becomes the
            // one whose half axes are the singular values of the map of its half diameters.
            double h = Math.Sqrt(0.5);
            var transform = new GeoTransform3(new double[,] { { 2 * h, -2 * h, 0, 0 }, { h, h, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } });
            DxfDatabase db = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);

            foreach (string handle in new[] { "74", "AC", "AD", "AF", "B0" })
            {
                DxfHatch hatch = ByHandle<DxfHatch>(db, handle);
                double area = hatch.Area;
                hatch.TransformBy(transform);
                Assert.Equal(2.0 * area, ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), handle).Area, 8);
            }

            Assert.Equal(200.0 * Math.PI, ByHandle<DxfHatch>(db, "AF").Area, 8);
            var ellipse = Assert.IsType<GeoEllipse3>(Assert.Single(ByHandle<DxfHatch>(db, "B0").GetLoopAt(0).Curves));
            Assert.Equal(Ratio(transform, new GeoVector3(0, 8, 0), new GeoVector3(-2, 0, 0)), ellipse.MinorRadius / ellipse.MajorRadius, 9);
            Assert.Equal(0.195, ellipse.MinorRadius / ellipse.MajorRadius, 3);

            // AD's half ellipse, major (5, 0) and ratio 0.5, its third ellipse edge after its two arcs made ellipses: the major
            // axis and the ratio its record is given.
            string record = Records(db.ToText()).Single(r => HandleOf(r) == "AD");
            int at = -1;

            for (int k = 0; k < 3; k++)
            {
                at = record.IndexOf("\n 72\n3\n", at + 1, StringComparison.Ordinal);
            }

            string[] lines = record.Substring(at + 1).Split('\n');
            double ax = double.Parse(lines[7], System.Globalization.CultureInfo.InvariantCulture);
            double ay = double.Parse(lines[9], System.Globalization.CultureInfo.InvariantCulture);
            double ratio = double.Parse(lines[11], System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(" 40", lines[10]);
            Assert.Equal(Ratio(transform, new GeoVector3(5, 0, 0), new GeoVector3(0, 2.5, 0)), ratio, 9);
            Assert.Equal(0.362, ratio, 3);
            Assert.Equal(7.84, Math.Abs(ax), 2);
            Assert.Equal(2.75, Math.Abs(ay), 2);
        }

        [Theory]
        [InlineData("triangle", 87.5)]
        [InlineData("notch", 90.0)]
        [InlineData("corner", 75.0)]
        [InlineData("diamond", 50.0)]
        [InlineData("crossing", 200.0)]
        public void AnIslandTouchingTheOuterLoop_IsTakenOff_AndCrossingLoopsAreNotCut(string island, double area)
        {
            // The square 10 by 10, and an island whose corners or sides lie on its edge; two squares that overlap by a quarter
            // are each within the other by none of their points, and fill 200, as the remarks of Area say.
            var islands = new Dictionary<string, GeoPoint2[]>(StringComparer.Ordinal)
            {
                ["triangle"] = new[] { new GeoPoint2(10, 5), new GeoPoint2(5, 10), new GeoPoint2(5, 5) },
                ["notch"] = new[] { new GeoPoint2(3, 10), new GeoPoint2(3, 8), new GeoPoint2(8, 8), new GeoPoint2(8, 10) },
                ["corner"] = new[] { new GeoPoint2(5, 5), new GeoPoint2(10, 5), new GeoPoint2(10, 10), new GeoPoint2(5, 10) },
                ["diamond"] = new[] { new GeoPoint2(5, 0), new GeoPoint2(10, 5), new GeoPoint2(5, 10), new GeoPoint2(0, 5) },
                ["crossing"] = new[] { new GeoPoint2(5, 5), new GeoPoint2(15, 5), new GeoPoint2(15, 15), new GeoPoint2(5, 15) },
            };
            var hatch = new DxfHatch();
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) }, null);
            hatch.AppendLoop(DxfHatchLoopTypes.Default, islands[island], null);

            Assert.Equal(area, hatch.Area, 12);
        }

        [Fact]
        public void ThePlaneAndExtentsOfAHatch_AreThoseOfItsLoops_AtItsElevation()
        {
            // A square 30 by 20 round a circle of radius 5, 5 above the floor: its plane z = 5, its box the square's, as the
            // entity's virtual members give them; one of no loops has no extents.
            var hatch = new DxfHatch { Elevation = 5.0 };
            DxfEntity entity = hatch;
            Assert.Throws<InvalidOperationException>(() => entity.GeometricExtents);

            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);
            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoCircle3(new GeoPoint3(15, 10, 5), new GeoVector3(0, 0, 1), 5) }));
            GeoAabb3 box = entity.GeometricExtents;
            Assert.True(box.Min.IsEqualTo(new GeoPoint3(0, 0, 5), Tol));
            Assert.True(box.Max.IsEqualTo(new GeoPoint3(30, 20, 5), Tol));

            GeoPlane3 plane = entity.GetPlane();
            Assert.True(plane.Normal.IsEqualTo(new GeoVector3(0, 0, 1), Tol));
            Assert.Equal(5.0, plane.SignedDistanceTo(new GeoPoint3(0, 0, 10)), 12);

            // The circle's top at y 15 reaches past nothing; one of radius 12 past the square's top, to y 22.
            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoCircle3(new GeoPoint3(15, 10, 5), new GeoVector3(0, 0, 1), 12) }));
            Assert.Equal(22.0, entity.GeometricExtents.Max.Y, 9);
        }

        [Fact]
        public void AThousandIslands_AreReadAndMeasured_EachLoopOnce()
        {
            // A square 1000 by 1000 round a thousand squares 10 by 10 on a grid: 1 000 000 less 100 000.
            var hatch = new DxfHatch();
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(1000, 0), new GeoPoint2(1000, 1000), new GeoPoint2(0, 1000) }, null);

            for (int i = 0; i < 1000; i++)
            {
                double x = 20 + (i % 40) * 24;
                double y = 20 + (i / 40) * 24;
                hatch.AppendLoop(DxfHatchLoopTypes.Default, new[] { new GeoPoint2(x, y), new GeoPoint2(x + 10, y), new GeoPoint2(x + 10, y + 10), new GeoPoint2(x, y + 10) }, null);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            Assert.Equal(900000.0, hatch.Area, 6);
            double sum = 0.0;

            for (int i = 1; i < hatch.NumberOfLoops; i++)
            {
                sum += ((GeoPolygonArc3)hatch.GetLoopAt(i).Polyline).Area;
            }

            Assert.Equal(100000.0, sum, 6);
            Assert.True(watch.Elapsed.TotalSeconds < 30.0, "A thousand islands took " + watch.Elapsed + ".");
        }

        [Fact]
        public void ALoopCopiedAcrossVersions_IsGivenTheCountOfFitPointsOfItsSplineAsTheVersionWritesIt()
        {
            // The spline edge of AD: R2018 writes 97 0 after its control points, R2000 nothing; each written as the target writes it.
            DxfHatchLoop old = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AD").GetLoopAt(0);
            DxfHatchLoop late = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AD").GetLoopAt(0);

            DxfDatabase r2018 = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            ByHandle<DxfHatch>(r2018, "74").AppendLoop(old);
            Assert.Contains("\n 10\n200.0\n 20\n0.0\n 97\n0\n 97\n0\n 75\n", Records(r2018.ToText()).Single(r => HandleOf(r) == "74"), StringComparison.Ordinal);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000);
            ByHandle<DxfHatch>(r2000, "74").AppendLoop(late);
            Assert.Contains("\n 10\n200.0\n 20\n0.0\n 97\n0\n 75\n", Records(r2000.ToText()).Single(r => HandleOf(r) == "74"), StringComparison.Ordinal);
            Assert.Equal(ByHandle<DxfHatch>(r2000, "74").Area, ByHandle<DxfHatch>(DxfDatabase.Parse(r2018.ToText()), "74").Area, 9);
        }

        [Fact]
        public void ALoopOfEdgesThatDoNotRunEndToEnd_IsRefused()
        {
            var hatch = new DxfHatch();
            var apart = new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)), new GeoLine3(new GeoPoint3(10, 1, 0), new GeoPoint3(0, 0, 0)) });
            Assert.Throws<ArgumentException>(() => hatch.AppendLoop(apart));
            Assert.Equal(0, hatch.NumberOfLoops);

            hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)), new GeoLine3(new GeoPoint3(10, 0.0004, 0), new GeoPoint3(0, 5, 0)), new GeoLine3(new GeoPoint3(0, 5, 0), new GeoPoint3(0, 0, 0)) }));
            Assert.Equal(25.0, hatch.Area, 2);
        }

        /// <summary>The ratio of the axes of the ellipse of two half diameters carried by the linear part of a transformation: its singular values.</summary>
        private static double Ratio(GeoTransform3 transform, GeoVector3 u, GeoVector3 v)
        {
            GeoVector3 a = transform.Transform(u);
            GeoVector3 b = transform.Transform(v);
            double p = a.X * a.X + b.X * b.X;
            double q = a.X * a.Y + b.X * b.Y;
            double r = a.Y * a.Y + b.Y * b.Y;
            double mean = (p + r) / 2.0;
            double spread = Math.Sqrt((p - r) * (p - r) / 4.0 + q * q);
            return Math.Sqrt((mean - spread) / (mean + spread));
        }

        #endregion

        #region Review round 2

        [Fact]
        public void ALoopGivenTwice_CountsOnceWithinTheOther_AsTheEvenOddRuleCountsIt()
        {
            // A square 10 by 10 given twice fills nothing; round an island 2 by 2 given twice it fills 100; AD given its outer
            // loop again fills what its inner loop fills alone.
            var square = new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10), new GeoPoint2(0, 10) };
            var island = new[] { new GeoPoint2(2, 2), new GeoPoint2(4, 2), new GeoPoint2(4, 4), new GeoPoint2(2, 4) };

            var twice = new DxfHatch();
            twice.AppendLoop(DxfHatchLoopTypes.External, square, null);
            twice.AppendLoop(DxfHatchLoopTypes.External, square, null);
            Assert.Equal(0.0, twice.Area, 12);

            var islands = new DxfHatch();
            islands.AppendLoop(DxfHatchLoopTypes.External, square, null);
            islands.AppendLoop(DxfHatchLoopTypes.Default, island, null);
            islands.AppendLoop(DxfHatchLoopTypes.Default, island, null);
            Assert.Equal(100.0, islands.Area, 12);

            DxfHatch ad = ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AD");
            var inner = new DxfHatch();
            inner.AppendLoop(ad.GetLoopAt(1));
            ad.AppendLoop(ad.GetLoopAt(0));
            Assert.True(inner.Area > 0.0);
            Assert.Equal(inner.Area, ad.Area, 9);
        }

        [Fact]
        public void ThreeThousandIslandsAppendedOneAtATime_AreSplicedIn_TheOtherLoopsKeepingTheirBytes()
        {
            // Each loop put in or taken out costs a copy of the bytes of the record, so that three thousand islands go in within
            // a few seconds, where writing every group anew each time took 24 s; a loop put in and taken out again leaves the
            // record as it was, byte for byte.
            var hatch = new DxfHatch();
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(1400, 0), new GeoPoint2(1400, 1400), new GeoPoint2(0, 1400) }, null);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 3000; i++)
            {
                double x = 20 + (i % 55) * 24;
                double y = 20 + (i / 55) * 24;
                hatch.AppendLoop(DxfHatchLoopTypes.Default, new[] { new GeoPoint2(x, y), new GeoPoint2(x + 10, y), new GeoPoint2(x + 10, y + 10), new GeoPoint2(x, y + 10) }, null);
            }

            Assert.True(watch.Elapsed.TotalSeconds < 10.0, "Three thousand islands took " + watch.Elapsed + ".");
            Assert.Equal(3001, hatch.NumberOfLoops);
            Assert.Equal(1400.0 * 1400.0 - 3000 * 100.0, hatch.Area, 6);

            string text = DxfHatchLeaderDrawings.R2018;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfHatch ad = ByHandle<DxfHatch>(db, "AD");
            DxfHatchLoop outer = ad.GetLoopAt(0);
            ad.InsertLoopAt(1, outer);
            Assert.Equal(3, ad.NumberOfLoops);
            Assert.Equal(3, ByHandle<DxfHatch>(DxfDatabase.Parse(db.ToText()), "AD").NumberOfLoops);
            ad.RemoveLoopAt(1);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void AHatchCopiedIntoADrawingOfAnotherVersion_IsWrittenAsThatVersionWritesIt()
        {
            // AD's spline edge: copied from R2000 into R2018 it is given its 97 0 before the 97 0 of its loop's boundary; from
            // R2018 into R2000 it has it taken out. AF's gradient: kept in R2018, taken out in R2000, the HATCH filled solid.
            DxfDatabase r2018 = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var fromOld = (DxfHatch)ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AD").Clone();
            r2018.ModelSpace.AppendEntity(fromOld);
            string later = Records(r2018.ToText()).Single(r => HandleOf(r) == fromOld.Handle);
            Assert.Contains("\n 10\n200.0\n 20\n0.0\n 97\n0\n 97\n0\n 92\n", later, StringComparison.Ordinal);

            DxfDatabase r2000 = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000);
            var fromNew = (DxfHatch)ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AD").Clone();
            var gradient = (DxfHatch)ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AF").Clone();
            r2000.ModelSpace.AppendEntity(fromNew);
            r2000.ModelSpace.AppendEntity(gradient);
            string earlier = Records(r2000.ToText()).Single(r => HandleOf(r) == fromNew.Handle);
            Assert.Contains("\n 10\n200.0\n 20\n0.0\n 97\n0\n 92\n", earlier, StringComparison.Ordinal);
            Assert.Equal(ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2000), "AD").Area, ByHandle<DxfHatch>(DxfDatabase.Parse(r2018.ToText()), fromOld.Handle).Area, 9);

            DxfHatch solid = ByHandle<DxfHatch>(DxfDatabase.Parse(r2000.ToText()), gradient.Handle);
            Assert.False(solid.IsGradient);
            Assert.True(solid.IsSolidFill);
            Assert.DoesNotContain("\n450\n", Records(r2000.ToText()).Single(r => HandleOf(r) == gradient.Handle), StringComparison.Ordinal);

            DxfDatabase same = DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018);
            var kept = (DxfHatch)ByHandle<DxfHatch>(DxfDatabase.Parse(DxfHatchLeaderDrawings.R2018), "AF").Clone();
            same.ModelSpace.AppendEntity(kept);
            Assert.Equal("CYLINDER", ByHandle<DxfHatch>(DxfDatabase.Parse(same.ToText()), kept.Handle).GradientName);
        }

        #endregion

        /// <summary>Where a curve of a loop starts and ends, as it runs.</summary>
        private static GeoPoint3[] Ends(object curve)
        {
            switch (curve)
            {
                case GeoLine3 l: return new[] { l.StartPoint, l.EndPoint };
                case GeoArc3 a: return new[] { a.StartPoint, a.EndPoint };
                case GeoSpline3 s: return new[] { s.GetPointAtKnot(s.DomainStart), s.GetPointAtKnot(s.DomainEnd) };
                case GeoCircle3 c: return new[] { c.GetPointAtDistance(0), c.GetPointAtDistance(0) };
                case GeoEllipse3 e: return new[] { e.Center, e.Center };
                default: throw new ArgumentException(curve.GetType().Name);
            }
        }
    }
}
