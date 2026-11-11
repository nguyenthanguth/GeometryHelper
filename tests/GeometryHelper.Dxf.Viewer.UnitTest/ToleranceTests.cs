using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// TOLERANCE, its feature control frame on the canvas and under MOVE, COPY, ROTATE, SCALE and MIRROR, and the frames of
    /// LEADER and QLEADER, as AutoCAD 2021 makes and lays them out (accoreconsole probes GHTOL1 to GHTOL4, 2026-11-12): rows
    /// twice DIMTXT high whatever DIMGAP, each cell its ink and DIMTXT wide, a cell of nothing taking no room; a frame on a
    /// leader to the left set back its width.
    /// </summary>
    public class ToleranceTests
    {
        private const string Full = "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%vA%%vB%%vC";

        // The first row of board GeomToleranceFilled as the dialog writes it: the second tolerance left empty between its cells.
        private const string Filled = "{\\Fgdt;j}%%v{\\Fgdt;n}0.1{\\Fgdt;m}%%v%%vA%%vB%%vC";

        private static readonly Tolerance Tol = Tolerance.Default;

        /// <summary>A metric drawing (ISO-25: DIMTXT 2.5, DIMGAP 0.625), its style's DIMTAD nought.</summary>
        private static CadDocument Metric()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.DimStyleTable[database.Dimstyle].Dimtad = 0;
            database.Dimtad = 0;
            return CadDocument.From(database, "metric.dxf");
        }

        /// <summary>Adds a frame of words at a point along the world's x, in the drawing's dimension style.</summary>
        private static DxfTolerance Frame(
            CadDocument document,
            string text,
            GeoPoint3 at,
            Action<DxfTolerance> then = null
        )
        {
            DxfTolerance frame = null;
            document.ApplyEdit("frame", null, s =>
            {
                frame = new DxfTolerance { Text = text, Location = at };
                s.Append(document.ModelSpace, frame);
                if (s.Database.DimStyleTable.Has(s.Database.Dimstyle))
                {
                    frame.DimensionStyleName = s.Database.Dimstyle;
                }

                then?.Invoke(frame);
            });
            return frame;
        }

        /// <summary>The box of what the canvas draws of an entity: min x, min y, max x, max y.</summary>
        private static double[] Drawn(CadDocument document, string handle)
        {
            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle)
                .ToList();
            Assert.NotEmpty(items);
            return new[]
            {
                items.Min(i => i.Bounds.MinX),
                items.Min(i => i.Bounds.MinY),
                items.Max(i => i.Bounds.MaxX),
                items.Max(i => i.Bounds.MaxY),
            };
        }

        /// <summary>Answers the Geometric Tolerance dialog with the first row of board GeomToleranceFilled, OK.</summary>
        private static void AnswerFilled(CadCommandSession session)
        {
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    CadToleranceRow row = request.Rows[0];
                    row.Symbol = 'j';
                    row.Tolerance1.Diameter = true;
                    row.Tolerance1.Value = "0.1";
                    row.Tolerance1.Material = CadMaterialCondition.Maximum;
                    row.Datums[0].Letter = "A";
                    row.Datums[1].Letter = "B";
                    row.Datums[2].Letter = "C";
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
        }

        // ---- The words of the dialog

        [Fact]
        public void TheDialog_WritesItsBoxesAsAutoCadsGroup1_ARowOfNothingLeftOut()
        {
            CadDocument document = Metric();
            var session = Session(document);
            CadToleranceRequest asked = null;
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    asked = request;
                    e.Handled = true;
                }
            };
            Run(session, "TOLERANCE");

            Assert.NotNull(asked);
            Assert.Equal(string.Empty, asked.Text);
            CadToleranceRow row = asked.Rows[0];
            row.Symbol = 'j';
            row.Tolerance1.Diameter = true;
            row.Tolerance1.Value = "0.5";
            row.Tolerance1.Material = CadMaterialCondition.Maximum;
            row.Datums[0].Letter = "A";
            row.Datums[1].Letter = "B";

            // As the frame AutoCAD 2021 wrote in the key of the DWG tests: an empty second tolerance kept between its cells.
            Assert.Equal("{\\Fgdt;j}%%v{\\Fgdt;n}0.5{\\Fgdt;m}%%v%%vA%%vB", asked.Text);

            asked.Rows[1].Symbol = 'c';
            asked.Rows[1].Tolerance1.Value = "0.05";
            asked.Height = "5";
            asked.ProjectedZone = true;
            asked.DatumIdentifier = "D";
            Assert.Equal(
                "{\\Fgdt;j}%%v{\\Fgdt;n}0.5{\\Fgdt;m}%%v%%vA%%vB\n{\\Fgdt;c}%%v0.05\n5{\\Fgdt;p}\nD",
                asked.Text
            );
        }

        // ---- TOLERANCE

        [Fact]
        public void Tolerance_AsksTheDialog_ThenTheLocation_AndPutsTheFrameThere()
        {
            CadDocument document = Metric();
            var session = Session(document);
            AnswerFilled(session);

            Run(session, "TOLERANCE");
            Assert.Contains("Enter tolerance location", session.Prompt.Text);
            Assert.NotEmpty(session.Prompt.Preview.Items(P(10, 20)));
            Assert.Empty(All<DxfTolerance>(document.ModelSpace));

            Run(session, P(10, 20));
            DxfTolerance frame = Assert.Single(All<DxfTolerance>(document.ModelSpace));
            Assert.Equal(Filled, frame.Text);
            Near(new GeoPoint3(10, 20, 0), frame.Location);
            Assert.True(frame.Direction.IsEqualTo(new GeoVector3(1, 0, 0), Tol));
            Assert.Equal(document.Database.Dimstyle, frame.DimensionStyleName);
            Assert.Single(CadGrips.Of(frame));
            Near(new GeoPoint2(10, 20), CadGrips.Of(frame)[0].Point);
        }

        [Fact]
        public void Tolerance_Cancelled_EmptyOrWithNoWindow_MakesNoFrame()
        {
            CadDocument document = Metric();
            var session = Session(document);
            Run(session, "TOLERANCE");
            Assert.Contains(session.History, l => l.Contains("Geometric Tolerance is shown by the window"));

            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadToleranceRequest request)
                {
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };
            Run(session, "TOLERANCE");
            Assert.Empty(All<DxfTolerance>(document.ModelSpace));
            Assert.Null(session.Prompt);
        }

        // ---- The frame on the canvas

        [Fact]
        public void TheFrame_IsTwiceDimtxtHighARow_EachCellItsInkAndDimtxtWide_WhateverDimgap()
        {
            // The symbols alone, whose room is AutoCAD's: the position 1.5 and the flatness 2.071429 text heights, each
            // cell a text height more; at DIMTXT 2.5, 6.25 and 7.678571, and the row 5 high about the insertion point.
            CadDocument document = Metric();
            DxfTolerance one = Frame(document, "{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(0, 0, 0));
            double[] box = Drawn(document, one.Handle);
            Assert.Equal(0.0, box[0], 6);
            Assert.Equal(-2.5, box[1], 6);
            Assert.Equal(6.25 + 7.678571, box[2], 5);
            Assert.Equal(2.5, box[3], 6);

            // A cell of nothing takes no room: the empty symbol and second tolerance of AutoCAD's frames.
            DxfTolerance gaps = Frame(document, "%%v{\\Fgdt;j}%%v%%v{\\Fgdt;c}", new GeoPoint3(0, 50, 0));
            box = Drawn(document, gaps.Handle);
            Assert.Equal(6.25 + 7.678571, box[2] - box[0], 5);

            // A second row stands under the first on the same left side, its own width; DIMGAP changes nothing.
            DxfTolerance rows = Frame(document, "{\\Fgdt;c}%%v{\\Fgdt;n}\n{\\Fgdt;j}", new GeoPoint3(0, 100, 0), f =>
                f.XData = new[]
                {
                    new DxfTypedValue(1001, "ACAD"),
                    new DxfTypedValue(1000, "DSTYLE"),
                    new DxfTypedValue(1002, "{"),
                    new DxfTypedValue(1070, "147"),
                    new DxfTypedValue(1040, "2"),
                    new DxfTypedValue(1002, "}"),
                }
            );
            Assert.Equal(2.0, rows.GetDimstyleData().Dimgap);
            box = Drawn(document, rows.Handle);
            Assert.Equal(0.0, box[0], 6);
            Assert.Equal(100.0 - 2.5 - 5.0, box[1], 6);
            Assert.Equal(7.678571 + 5.0, box[2], 5);
            Assert.Equal(102.5, box[3], 6);
        }

        [Fact]
        public void AFrameOfWords_IsAsWideAsItsInk_AndItsWordsStandInsideIt()
        {
            CadDocument document = Metric();
            DxfTolerance frame = Frame(document, Full, new GeoPoint3(0, 0, 0));
            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == frame.Handle)
                .ToList();
            double[] box = Drawn(document, frame.Handle);

            // The words A, B and C and the value, each a TEXT on a baseline half DIMTXT below the middle of the row.
            List<DisplayText2> words = items.OfType<DisplayText2>().ToList();
            Assert.Contains(words, w => w.Text == "0.1");
            Assert.Contains(words, w => w.Text == "A");
            Assert.Contains(words, w => w.Text == "M");

            foreach (DisplayText2 word in words)
            {
                Assert.True(word.X > box[0] && word.X < box[2]);
            }

            Assert.Equal(-1.25, words.First(w => w.Text == "A").Y, 6);

            // Five cells: the position 6.25 wide, then the value and the datums each wider than DIMTXT.
            Assert.True(box[2] > 6.25 + 4.0 * 2.5);
            Assert.Equal(5.0, box[3] - box[1], 6);
        }

        [Fact]
        public void InArial_TheFramesAreAsWideAsAutoCadsToTheMillionth_ThoseOfMixedCellsWithinFiveThousandths()
        {
            // AutoCAD 2021's console (GHTOL1), DIMTXT 0.18 in Arial: the widths of its frames' bounding points.
            var database = new DxfDatabase("AC1032");
            database.TextStyleTable["Standard"].FileName = "arial.ttf";
            EditingDrawings.StandardCurrent(database);
            CadDocument document = CadDocument.From(database, "arial.dxf");
            var widths = new[]
            {
                new KeyValuePair<string, double>("{\\Fgdt;j}%%v0.1", 0.92296043656207372),
                new KeyValuePair<string, double>("{\\Fgdt;c}%%v0.05", 1.2017657376729685),
                new KeyValuePair<string, double>("%%v0.1", 0.4729604365620737),
                new KeyValuePair<string, double>("{\\Fgdt;j}%%v0.1%%v0.2", 1.4288267394270124),
                new KeyValuePair<string, double>("{\\Fgdt;j}%%vWIDE-VALUE-123%%vAB", 3.1857298772169171),
            };
            double y = 0.0;

            foreach (KeyValuePair<string, double> width in widths)
            {
                DxfTolerance frame = Frame(document, width.Key, new GeoPoint3(0, y, 0));
                double[] box = Drawn(document, frame.Handle);
                Assert.Equal(width.Value, box[2] - box[0], 6);
                y += 10.0;
            }

            // A cell of the gdt font and words together: AutoCAD 2.462701, ours 0.0043 wider.
            DxfTolerance full = Frame(document, Full, new GeoPoint3(0, y, 0));
            double[] wide = Drawn(document, full.Handle);
            Assert.True(Math.Abs(wide[2] - wide[0] - 2.462701227830832) < 0.005, (wide[2] - wide[0]).ToString());
        }

        // ---- Moved

        [Fact]
        public void AFrame_IsMovedCopiedTurnedAndScaled_AsAutoCadMovesOne()
        {
            CadDocument document = Metric();
            var session = Session(document);
            DxfTolerance frame = Frame(document, Full, new GeoPoint3(10, 20, 0));

            Run(session, "MOVE", new[] { frame.Handle }, Enter, P(0, 0), P(5, 5));
            Near(new GeoPoint3(15, 25, 0), Entity<DxfTolerance>(document, frame.Handle).Location);

            Run(session, "COPY", new[] { frame.Handle }, Enter, P(0, 0), P(0, 30), Enter);
            Assert.Equal(2, All<DxfTolerance>(document.ModelSpace).Count);
            Near(new GeoPoint3(15, 55, 0), All<DxfTolerance>(document.ModelSpace)[1].Location);

            // ROTATE 30 about the origin, as AutoCAD 2021's: the direction turned with it.
            Run(session, "ROTATE", new[] { frame.Handle }, Enter, P(0, 0), "30");
            DxfTolerance turned = Entity<DxfTolerance>(document, frame.Handle);
            Assert.True(
                turned.Direction.IsEqualTo(new GeoVector3(Math.Sqrt(3) / 2.0, 0.5, 0), Tol),
                turned.Direction.ToString()
            );

            // SCALE 2: the text height and gap written twice the style's, DIMTXT then DIMGAP.
            Run(session, "SCALE", new[] { frame.Handle }, Enter, P(0, 0), "2");
            DxfDimStyleTableRecord data = Entity<DxfTolerance>(document, frame.Handle).GetDimstyleData();
            Assert.Equal(5.0, data.Dimtxt, 9);
            Assert.Equal(1.25, data.Dimgap, 9);
        }

        [Fact]
        public void Mirrored_WithMirrtextNought_ItKeepsItsNormal_AndWithOne_ItIsTurnedOver()
        {
            // AutoCAD 2021 (GHTOL4): the frame at (10, 20) along (0.6, 0.8) mirrored across the y axis stands at (-10, 20)
            // along (-0.6, 0.8), its normal (0, 0, 1) under MIRRTEXT 0 and (0, 0, -1) under 1.
            foreach (bool mirrtext in new[] { false, true })
            {
                CadDocument document = Metric();
                document.ApplyEdit("mirrtext", null, s => s.Database.Mirrtext = mirrtext);
                var session = Session(document);
                DxfTolerance frame = Frame(document, Full, new GeoPoint3(10, 20, 0));
                document.ApplyEdit("turn", null, s =>
                    ((DxfTolerance)s.Document.FindEntity(frame.Handle)).Direction = new GeoVector3(0.6, 0.8, 0)
                );

                Run(session, "MIRROR", new[] { frame.Handle }, Enter, P(0, 0), P(0, 10), Enter);

                DxfTolerance copy = All<DxfTolerance>(document.ModelSpace).Last();
                Assert.NotEqual(frame.Handle, copy.Handle);
                Near(new GeoPoint3(-10, 20, 0), copy.Location);
                Assert.True(copy.Direction.IsEqualTo(new GeoVector3(-0.6, 0.8, 0), Tol), copy.Direction.ToString());
                Assert.True(
                    copy.Normal.IsEqualTo(new GeoVector3(0, 0, mirrtext ? -1 : 1), Tol),
                    copy.Normal.ToString()
                );
            }
        }

        // ---- LEADER and QLEADER

        [Fact]
        public void LeaderTolerance_PutsTheFrameAtTheEnd_ItsLeftSideOnIt_TheLeaderPointingToIt()
        {
            CadDocument document = Metric();
            var session = Session(document);
            AnswerFilled(session);

            Run(session, "LEADER", P(0, 0), P(10, 5), Enter, Enter, "T");

            DxfLeader leader = Assert.Single(All<DxfLeader>(document.ModelSpace));
            DxfTolerance frame = Assert.Single(All<DxfTolerance>(document.ModelSpace));
            Assert.Equal(Filled, frame.Text);
            Near(new GeoPoint3(10, 5, 0), frame.Location);
            Assert.Equal(frame.Handle, leader.Annotation?.Handle);
            Assert.Equal(DxfAnnotationType.FeatureControlFrame, leader.AnnoType);

            // A steep last segment: a hook said, as AutoCAD 2021 writes it, but no vertex of its own.
            Assert.Equal(2, leader.NumVertices);
            Assert.True(leader.HasHookLine);
        }

        [Fact]
        public void ACopiedFrame_OnALeaderToTheLeft_IsSetBackItsWidth_AsAutoCadSetsItBack()
        {
            // AutoCAD 2021 (GHTOL4): {\Fgdt;j}%%v0.1 at (200, 200) copied to a leader from (100, 100) to (90, 105) stands at
            // (89.07704, 105), its right side on the end; to (110, 105), at (110, 105).
            CadDocument document = Metric();
            var session = Session(document);
            DxfTolerance source = Frame(document, "{\\Fgdt;j}%%v{\\Fgdt;c}", new GeoPoint3(200, 200, 0));
            double width = Drawn(document, source.Handle)[2] - 200.0;

            Run(session, "LEADER", P(100, 100), P(90, 105), Enter, Enter, "C", new[] { source.Handle });
            DxfTolerance left = All<DxfTolerance>(document.ModelSpace).Last();
            Assert.NotEqual(source.Handle, left.Handle);
            Near(new GeoPoint3(90.0 - width, 105, 0), left.Location, 1E-9);
            Assert.Equal(6.25 + 7.678571, width, 5);
            Assert.Equal(left.Handle, All<DxfLeader>(document.ModelSpace).Last().Annotation?.Handle);

            Run(session, "LEADER", P(100, 100), P(110, 100), Enter, Enter, "C", new[] { source.Handle });
            DxfTolerance right = All<DxfTolerance>(document.ModelSpace).Last();
            Near(new GeoPoint3(110, 100, 0), right.Location);
            Assert.False(All<DxfLeader>(document.ModelSpace).Last().HasHookLine);
        }

        [Fact]
        public void ACopiedFrame_OnTheLeft_StandsWhereAutoCadPutsIt_InArial()
        {
            // AutoCAD 2021 (GHTOL4), DIMTXT 0.18 in Arial: the copy of {\Fgdt;j}%%v0.1 at (89.07704, 105).
            var database = new DxfDatabase("AC1032");
            database.TextStyleTable["Standard"].FileName = "arial.ttf";
            EditingDrawings.StandardCurrent(database);
            CadDocument document = CadDocument.From(database, "arial.dxf");
            var session = Session(document);
            DxfTolerance source = Frame(document, "{\\Fgdt;j}%%v0.1", new GeoPoint3(200, 200, 0));

            Run(session, "LEADER", P(100, 100), P(90, 105), Enter, Enter, "C", new[] { source.Handle });
            DxfTolerance copy = All<DxfTolerance>(document.ModelSpace).Last();
            Near(new GeoPoint3(89.07704, 105, 0), copy.Location, 1E-5);
        }

        [Fact]
        public void QLeaderTolerance_ShowsTheDialogAfterThePoints_AndReuseCurrentCopiesTheFrame()
        {
            CadDocument document = Metric();
            var session = Session(document);
            AnswerFilled(session);
            session.UiRequested += (s, e) =>
            {
                if (e.Payload is CadQLeaderRequest request)
                {
                    request.Settings.Annotation = CadQLeaderAnnotation.Tolerance;
                    request.Settings.Reuse = CadQLeaderReuse.ReuseNext;
                    request.Action = CadDialogAction.Ok;
                    e.Handled = true;
                }
            };

            Run(session, "QLEADER", "S", P(0, 0), P(10, 10), P(20, 10));
            DxfTolerance frame = Assert.Single(All<DxfTolerance>(document.ModelSpace));
            Near(new GeoPoint3(20, 10, 0), frame.Location);
            Assert.Equal(frame.Handle, Assert.Single(All<DxfLeader>(document.ModelSpace)).Annotation?.Handle);

            // Reuse Next turned to Reuse Current on the frame: the next leader copies it, no dialog asked.
            CadQLeaderSettings settings = CadQLeaderSettings.Read(document.Database);
            Assert.Equal(CadQLeaderReuse.ReuseCurrent, settings.Reuse);
            Assert.Equal(frame.Handle, settings.ReusedHandle);
            Run(session, "QLEADER", P(0, 50), P(10, 60), P(20, 60));
            Assert.Equal(2, All<DxfTolerance>(document.ModelSpace).Count);
            DxfTolerance copy = All<DxfTolerance>(document.ModelSpace).Last();
            Assert.Equal(Filled, copy.Text);
            Near(new GeoPoint3(20, 60, 0), copy.Location);
        }
    }
}
