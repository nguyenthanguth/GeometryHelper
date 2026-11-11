using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// MIRROR of INSERTs, MTEXTs and their attributes, and the words of a TEXT with spaces about them, as AutoCAD 2021 writes
    /// and draws them (probed in its console, mirror-insert GHMI1): an INSERT mirrored written with its x factor turned the
    /// other way rather than its y, turned to its mirrored y axis less a quarter turn; no normal written where it is the
    /// drawing's own; an attribute turned over about the middle of its ink.
    /// </summary>
    public class MirrorInsertTests
    {
        private const double Degree = Math.PI / 180.0;

        /// <summary>
        /// A drawing of an INSERT at (20, 10) of a block of a circle, scaled and turned, its attribute "Q1" 1.5 high at (21, 14)
        /// turned 30 degrees; at (19, 14) with its normal down the Z axis, as AutoCAD fills one of an INSERT scaled -1 by 1.
        /// </summary>
        private static CadDocument WithInsert(
            bool mirrtext,
            double sx,
            double sy,
            double degrees,
            string font = null,
            bool down = false
        )
        {
            var db = new DxfDatabase("AC1027");
            db.Mirrtext = mirrtext;

            if (font != null)
            {
                db.TextStyleTable["Standard"].FileName = font;
            }

            var block = new DxfBlockTableRecord { Name = "B1" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfCircle(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1), 1.0));
            var insert = new DxfBlockReference(new GeoPoint3(20, 10, 0), block);
            db.ModelSpace.AppendEntity(insert);
            insert.ScaleFactors = new GeoVector3(sx, sy, 1);
            insert.Rotation = degrees * Degree;
            var value = new DxfAttributeReference(new GeoPoint3(0, 0, 0), "Q1", "T3", null);
            insert.AttributeCollection.AppendAttribute(value);
            value.Height = 1.5;

            if (down)
            {
                value.Normal = new GeoVector3(0, 0, -1);
            }

            value.Rotation = 30 * Degree;
            value.Position = new GeoPoint3(down ? 19 : 21, 14, 0);
            return CadDocument.From(db, "insert.dxf");
        }

        /// <summary>Mirrors the one object of a kind across the line through two points, keeping it; gives the copy.</summary>
        private static T MirroredCopy<T>(CadDocument document, GeoPoint2 first, GeoPoint2 second)
            where T : DxfEntity
        {
            string source = All<T>(document.ModelSpace)[0].Handle;
            Run(Session(document), "MIRROR", new[] { source }, Enter, first, second, Enter);
            return All<T>(document.ModelSpace).Single(e => e.Handle != source);
        }

        /// <summary>The value of the first group of a code in the record of a handle (its INSERT's own part for an INSERT), trimmed; null where it has none.</summary>
        private static string Group(CadDocument document, string handle, int code)
        {
            string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
            bool found = false;

            for (int i = 0; i + 1 < lines.Length; i += 2)
            {
                int at = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                string value = lines[i + 1].Trim();

                if (at == 0 && found)
                {
                    return null;
                }

                if (at == 5 && value == handle)
                {
                    found = true;
                }
                else if (found && at == code)
                {
                    return value;
                }
            }

            return null;
        }

        private static double Number(string value) =>
            value == null ? 0.0 : double.Parse(value, CultureInfo.InvariantCulture);

        [Fact]
        public void AnInsertMirrored_IsWrittenWithItsXFactorTurnedTheOtherWay_TurnedToItsMirroredYAxis()
        {
            // AutoCAD 2021 (mirror-insert GHMI1, the INSERT at (20, 10)), as x factor, y factor, turn, mirror line and what it
            // wrote: 41, 42 and 50, none where AutoCAD left the group out.
            object[][] cases =
            {
                new object[] { 1.0, 1.0, 0.0, P(0, 100), P(10, 100), "-1.0", null, 180.0 },
                new object[] { 1.0, 1.0, 0.0, P(100, 0), P(100, 10), "-1.0", null, 0.0 },
                new object[] { 1.0, 1.0, 0.0, P(0, 0), P(10, 10), "-1.0", null, 270.0 },
                new object[] { 1.0, 1.0, 30.0, P(0, 0), P(10 * Math.Cos(30 * Degree), 5), "-1.0", null, 210.0 },
                new object[] { 2.0, 3.0, 30.0, P(100, 0), P(100, 10), "-2.0", "3.0", 330.0 },
                new object[] { -1.0, 1.0, 0.0, P(100, 0), P(100, 10), null, null, 0.0 },
                new object[] { 1.0, -1.0, 0.0, P(0, 100), P(10, 100), "-1.0", "-1.0", 180.0 },
                new object[] { 1.0, 1.0, 200.0, P(0, 0), P(10, 10), "-1.0", null, 70.0 },
            };

            foreach (bool mirrtext in new[] { false, true })
            {
                foreach (object[] c in cases)
                {
                    CadDocument document = WithInsert(mirrtext, (double)c[0], (double)c[1], (double)c[2]);

                    DxfBlockReference copy = MirroredCopy<DxfBlockReference>(
                        document,
                        (GeoPoint2)c[3],
                        (GeoPoint2)c[4]
                    );

                    string at = mirrtext + " " + string.Join(" ", c.Take(3));
                    Assert.True(
                        Group(document, copy.Handle, 41) == null == (c[5] == null)
                            && Number(Group(document, copy.Handle, 41)) == Number((string)c[5]),
                        at + ": 41 " + Group(document, copy.Handle, 41)
                    );
                    Assert.True(
                        Group(document, copy.Handle, 42) == null == (c[6] == null)
                            && Number(Group(document, copy.Handle, 42)) == Number((string)c[6]),
                        at + ": 42 " + Group(document, copy.Handle, 42)
                    );
                    Assert.True(
                        Math.Abs(Number(Group(document, copy.Handle, 50)) - (double)c[7]) < 1E-9,
                        at + ": 50 " + Group(document, copy.Handle, 50)
                    );
                    Assert.True(copy.Normal.Z > 0.0);
                }
            }
        }

        [Fact]
        public void AnInsertMirroredAcrossALevelLine_StandsWhereItsMirrorDoes_ItsAttributeOfNoNormalWritten()
        {
            foreach (bool mirrtext in new[] { false, true })
            {
                CadDocument document = WithInsert(mirrtext, 1, 1, 0);

                DxfBlockReference copy = MirroredCopy<DxfBlockReference>(document, P(0, 100), P(10, 100));

                // AutoCAD 2021 (GHMI1 INS|A|L): at (20, 190), its ATTRIBs with no 210, no 41 and no 50 of nought.
                Near(new GeoPoint3(20, 190, 0), copy.Position, 1E-9);
                DxfAttributeReference attribute = copy.AttributeCollection.Cast<DxfAttributeReference>().Single();
                Assert.Null(Group(document, attribute.Handle, 210));
                Assert.Null(Group(document, attribute.Handle, 41));
                Assert.True(attribute.Normal.Z > 0.0);
            }
        }

        [Fact]
        public void WithMirrtextOff_AnAttributeTurnedOverAcrossItsBaseline_IsTurnedOverAboutTheMiddleOfItsInk()
        {
            CadDocument document = WithInsert(false, 1, 1, 0, "arial.ttf");

            DxfBlockReference copy = MirroredCopy<DxfBlockReference>(document, P(0, 100), P(10, 100));

            // AutoCAD 2021 (GHMI1 INS|A|L|M0, T3): "Q1" of arial.ttf 1.5 high, whose Q reaches below its baseline, turned 30
            // degrees at (21, 14), stands at (20.2950204638472, 184.778939625087) turned 330, not at (20.25, 184.701), as
            // turned over about half its height.
            DxfAttributeReference attribute = copy.AttributeCollection.Cast<DxfAttributeReference>().Single();
            Near(new GeoPoint3(20.2950204638472, 184.778939625087, 0), attribute.Position, 1E-6);
            Assert.Equal(330.0, attribute.Rotation / Degree, 9);
        }

        [Fact]
        public void WithMirrtextOn_TheAttributeOfAnInsertAlreadyMirrored_KeepsItsNormalDown_AndIsWrittenBackwards()
        {
            CadDocument document = WithInsert(true, -1, 1, 0, down: true);
            DxfAttributeReference before = All<DxfBlockReference>(document.ModelSpace)[0]
                .AttributeCollection.Cast<DxfAttributeReference>()
                .Single();
            Assert.True(before.Normal.Z < 0.0);

            DxfBlockReference copy = MirroredCopy<DxfBlockReference>(document, P(100, 0), P(100, 10));

            // AutoCAD 2021 (GHMI1 INS|E|U|M1, T3): its normal still (0, 0, -1), 71 of 2, turned 330 in its plane, at (-181, 14)
            // in the system of its plane, (181, 14) in the world's.
            DxfAttributeReference attribute = copy.AttributeCollection.Cast<DxfAttributeReference>().Single();
            Assert.True(attribute.Normal.Z < 0.0);
            Assert.True(attribute.IsMirroredInX);
            Assert.Equal(330.0, attribute.Rotation / Degree, 9);
            Near(new GeoPoint3(181, 14, 0), attribute.Position, 1E-9);
        }

        [Fact]
        public void ATextWithSpacesAboutItsWords_IsDrawnJustifiedByThem_AsAutoCadDrawsIt()
        {
            // AutoCAD 2021 (GHMI1 SPACE): "ABC " of arial.ttf on the right of its baseline at the origin starts at -8.151, "ABC"
            // at -7.181, and " ABC" on the left has its words a space along. In a font whose I is a line up at 1 moving the pen
            // 4 and whose space moves it 5, "I " on the right at the origin has its I drawn at -8, not at -3; " I" on the left
            // at 6, not at 1.
            object[][] cases =
            {
                new object[] { "I ", DxfTextHorizontalMode.TextRight, -8.0 },
                new object[] { " I", DxfTextHorizontalMode.TextLeft, 6.0 },
            };

            foreach (object[] c in cases)
            {
                using (var folder = new FontFolder())
                {
                    folder.Write("ghtest.shx", ShxFiles.Unifont());
                    var db = new DxfDatabase("AC1027");
                    db.TextStyleTable["Standard"].FileName = "ghtest.shx";
                    var text = new DxfDBText();
                    db.ModelSpace.AppendEntity(text);
                    text.TextString = (string)c[0];
                    text.Height = 10;
                    text.HorizontalMode = (DxfTextHorizontalMode)c[1];
                    text.AlignmentPoint = new GeoPoint3(0, 0, 0);
                    CadDocument document = CadDocument.From(db, "spaces.dxf");
                    document.Fonts = new CadFonts(new[] { folder.FullPath }, null, null);

                    DisplayText2 shown = document.GetDisplay(document.ModelSpace).Items.OfType<DisplayText2>().Single();

                    double x = shown
                        .Strokes.SelectMany(s => Enumerable.Range(0, s.Length / 2).Select(i => s[2 * i]))
                        .Max();
                    Assert.Equal((double)c[2], x, 9);
                    Assert.Equal((string)c[0], shown.Text);
                }
            }
        }

        [Fact]
        public void WithMirrtextOff_AnMTextMirroredAcrossALevelLine_HasNoDirectionNorNormalWritten_ItsHeightAsItWas()
        {
            foreach (double degrees in new[] { 0.0, 30.0 })
            {
                var db = new DxfDatabase("AC1027");
                db.Mirrtext = false;
                var text = new DxfMText();
                db.ModelSpace.AppendEntity(text);
                text.Contents = "ABC\\PDEFG";
                text.TextHeight = 2.5;
                text.Width = 30;
                text.Location = new GeoPoint3(20, 10, 0);
                text.Rotation = degrees * Degree;
                CadDocument document = CadDocument.From(db, "mtext.dxf");

                DxfMText copy = MirroredCopy<DxfMText>(document, P(0, 100), P(10, 100));

                // AutoCAD 2021 (GHMI1 MT|TopLeft|0 and 30|L|M0): at (20, 190), 40 2.5, no 210, and no 11 for the one turned nought.
                Near(new GeoPoint3(20, 190, 0), copy.Location, 1E-9);
                Assert.Equal("2.5", Group(document, copy.Handle, 40));
                Assert.Null(Group(document, copy.Handle, 210));

                if (degrees == 0.0)
                {
                    Assert.Null(Group(document, copy.Handle, 11));
                }
            }
        }
    }
}
