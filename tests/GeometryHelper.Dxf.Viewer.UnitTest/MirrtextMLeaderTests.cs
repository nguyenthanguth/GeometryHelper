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
    /// MIRROR of a MULTILEADER, as AutoCAD 2021 runs it (probed in its console, mirror-mleader GHMM1 and GHMM3): the leaders
    /// mirrored, the plane kept the drawing's (its x axis turned back where the mirror turns it, 297 of nought), and the
    /// words, at MIRRTEXT 0, kept reading along the plane's x axis at the mirrored landing, held by their other side where the
    /// mirror turns the plane's x axis (171 and 176 turned, bit 14 of the overrides), the base point the left of the words
    /// short of them by the landing gap; at MIRRTEXT 1 mirrored, held by their other side too, and said to run against the
    /// plane's x axis (294 of 1). Every MULTILEADER here is drawn by MLD in the style Standard: words 4 high in a font 10 above
    /// its baseline, an I a line up at 1 moving the pen 4, so that "II" is inked from 0.4 to 2, a line 1.6 wide as an MTEXT
    /// sets it by its ink, and a landing gap of 2.
    /// </summary>
    public sealed class MirrtextMLeaderTests : IDisposable
    {
        private readonly FontFolder _folder = new FontFolder();

        public void Dispose() => _folder.Dispose();

        /// <summary>A drawing of a MULTILEADER drawn by MLD from an arrow at a point to a landing at (30, 20), its words "II".</summary>
        private CadDocument Drawn(bool mirrtext, GeoPoint2 arrow)
        {
            CadDocument document = BlankWithMLeaderStyle();
            document.Database.Mirrtext = mirrtext;
            _folder.Write("ghtest.shx", ShxFiles.Unifont());
            document.Database.TextStyleTable["Standard"].FileName = "ghtest.shx";
            document.Fonts = new CadFonts(new[] { _folder.FullPath }, null, null);
            Run(Session(document), "MLD", arrow, P(30, 20), "II");
            return document;
        }

        /// <summary>Mirrors the one MULTILEADER across the line through two points, keeping it; gives the groups of it and of its copy.</summary>
        private static Groups Mirrored(CadDocument document, GeoPoint2 first, GeoPoint2 second, out Groups copy)
        {
            string source = Assert.Single(All<DxfMLeader>(document.ModelSpace)).Handle;
            var before = new Groups(document, source);
            Run(Session(document), "MIRROR", new[] { source }, Enter, first, second, Enter);
            copy = new Groups(document, All<DxfMLeader>(document.ModelSpace).Single(m => m.Handle != source).Handle);
            return before;
        }

        [Fact]
        public void WordsRightOfTheLanding_MirroredAcrossAnUpright_ReadAlongXHeldByTheirRight_TheBasePointLeftOfThem()
        {
            CadDocument document = Drawn(false, P(10, 0));

            Groups source = Mirrored(document, P(100, 0), P(100, 10), out Groups copy);

            // AutoCAD 2021 (GHMM1 MLEADER|right, X100, MIRRTEXT 0): normal and direction kept, the top left made the top right
            // at the mirrored place, 176 made 2, the plane kept, 297 nought, bit 14 set, 294 nought.
            Assert.Equal(1, source.Whole(171));
            Near(new GeoPoint3(0, 0, 1), copy.Point(11));
            Near(new GeoPoint3(1, 0, 0), copy.Point(13));
            Assert.Equal(3, copy.Whole(171));
            Near(Mirror(source.Point(12)), copy.Point(12));
            Assert.Equal(2, copy.Whole(176));
            Near(new GeoPoint3(1, 0, 0), copy.Point(111));
            Near(new GeoPoint3(0, 1, 0), copy.Point(112));
            Assert.Equal(0, copy.Whole(297));
            Assert.Equal(0, copy.Own(294));
            Assert.NotEqual(0, copy.Own(90) & (1 << 14));

            // The base point at the left of the words less the gap, level with the base point mirrored: "II" 1.6 wide.
            GeoPoint3 at = copy.Point(12);
            Near(new GeoPoint3(at.X - 1.6 - 2.0, source.Point(10).Y, 0), copy.Point(10));
        }

        [Fact]
        public void WordsLeftOfTheLanding_MirroredAcrossAnUpright_AreHeldByTheirLeft_TheBasePointTheGapShortOfThem()
        {
            CadDocument document = Drawn(false, P(50, 0));

            Groups source = Mirrored(document, P(100, 0), P(100, 10), out Groups copy);

            // AutoCAD 2021 (GHMM1 MLEADER|left, X100, MIRRTEXT 0): held by the other side, the top right made the top left and
            // the top left the top right, 176 of 2 made nought and nought 2; the base point the gap short of their left.
            int held = source.Whole(171);
            Assert.Equal(4 - held, copy.Whole(171));
            Assert.Equal(2 - source.Whole(176), copy.Whole(176));
            Near(new GeoPoint3(1, 0, 0), copy.Point(13));
            Near(Mirror(source.Point(12)), copy.Point(12));
            double left = copy.Point(12).X - (held == 1 ? 1.6 : 0.0);
            Near(new GeoPoint3(left - 2.0, source.Point(10).Y, 0), copy.Point(10));
        }

        [Fact]
        public void WithMirrtextOn_TheWordsAreMirrored_HeldByTheirRightAtTheirFarSide_AndSayTheyRunAgainstThePlane()
        {
            CadDocument document = Drawn(true, P(10, 0));

            Groups source = Mirrored(document, P(100, 0), P(100, 10), out Groups copy);

            // AutoCAD 2021 (GHMM1 MLEADER|right, X100, MIRRTEXT 1): normal and direction mirrored, the top right at the mirrored
            // place less the words' width, 294 of 1; the plane, 176 and the base point as at MIRRTEXT 0.
            Near(new GeoPoint3(0, 0, -1), copy.Point(11));
            Near(new GeoPoint3(-1, 0, 0), copy.Point(13));
            Assert.Equal(3, copy.Whole(171));
            GeoPoint3 mirrored = Mirror(source.Point(12));
            Near(new GeoPoint3(mirrored.X - 1.6, mirrored.Y, 0), copy.Point(12));
            Assert.Equal(1, copy.Own(294));
            Assert.Equal(2, copy.Whole(176));
            Near(new GeoPoint3(1, 0, 0), copy.Point(111));
            Assert.Equal(0, copy.Whole(297));
            Near(new GeoPoint3(mirrored.X - 1.6 - 2.0, source.Point(10).Y, 0), copy.Point(10));
        }

        [Fact]
        public void MirroredAcrossALevelLine_TheWordsStandAsBeforeOnTheMirroredLanding()
        {
            CadDocument document = Drawn(false, P(10, 0));

            Groups source = Mirrored(document, P(0, 100), P(10, 100), out Groups copy);

            // AutoCAD 2021 (GHMM3 MLEADER|right|AttachmentTopOfTop, Y100, MIRRTEXT 0): the words as far from the landing as before,
            // not mirrored, held as they were; the plane kept, 297 nought, 176 kept.
            GeoPoint3 landing = source.Point(10);
            GeoPoint3 at = source.Point(12);
            Near(new GeoPoint3(at.X, 200 - landing.Y + (at.Y - landing.Y), 0), copy.Point(12));
            Assert.Equal(source.Whole(171), copy.Whole(171));
            Near(new GeoPoint3(0, 0, 1), copy.Point(11));
            Near(new GeoPoint3(1, 0, 0), copy.Point(13));
            Assert.Equal(0.0, copy.Number(42));
            Near(new GeoPoint3(0, 1, 0), copy.Point(112));
            Assert.Equal(0, copy.Whole(297));
            Assert.Equal(source.Whole(176), copy.Whole(176));
            Near(new GeoPoint3(landing.X, 200 - landing.Y, 0), copy.Point(10));
        }

        [Fact]
        public void WithMirrtextOn_MirroredAcrossALevelLine_TheWordsAreMirrored_ThePlaneKept()
        {
            CadDocument document = Drawn(true, P(10, 0));

            Groups source = Mirrored(document, P(0, 100), P(10, 100), out Groups copy);

            // AutoCAD 2021 (GHMM1 MLEADER|right, Y100, MIRRTEXT 1): the words mirrored, turned half round in their mirrored
            // system; the plane's y axis still up, 297 nought.
            Near(Mirror(source.Point(12), level: true), copy.Point(12));
            Near(new GeoPoint3(0, 0, -1), copy.Point(11));
            Assert.Equal(Math.PI, copy.Number(42), 9);
            Near(new GeoPoint3(0, 1, 0), copy.Point(112));
            Assert.Equal(0, copy.Whole(297));
        }

        [Fact]
        public void AMultileaderOfABlock_CopiedAcrossAnUpright_IsBasedAtTheLeftOfTheBlockAndWritesItsMatrix()
        {
            CadDocument document = BlankWithMLeaderStyle();
            DxfDatabase db = document.Database;
            var block = new DxfBlockTableRecord { Name = "GH_BOX" };
            db.BlockTable.Add(block);
            block.AppendEntity(new DxfLine(new GeoPoint3(-10, -5, 0), new GeoPoint3(20, -5, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(20, -5, 0), new GeoPoint3(20, 15, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(20, 15, 0), new GeoPoint3(-10, 15, 0)));
            block.AppendEntity(new DxfLine(new GeoPoint3(-10, 15, 0), new GeoPoint3(-10, -5, 0)));
            var boxed = new DxfMLeader();
            db.ModelSpace.AppendEntity(boxed);
            boxed.BlockContent = block;
            boxed.BlockPosition = new GeoPoint3(30, 20, 0);
            boxed.AddLeaderLine(new GeoPoint3(-20, -20, 0));
            document = CadDocument.From(db, "boxed.dxf");

            Mirrored(document, P(100, 0), P(100, 10), out Groups copy);

            // AutoCAD 2021 (GHMM1 MLEADER|block|fromleft, X100, either MIRRTEXT): the box from 150 to 180, met by the leader from
            // the left at (150, 25); its matrix that of the block where it now stands, scaled -1 across at (170, 20).
            Near(new GeoPoint3(150, 25, 0), copy.Point(10));
            double[] matrix = copy.Numbers(47);
            Assert.Equal(16, matrix.Length);
            Assert.Equal(-1.0, matrix[0], 9);
            Assert.Equal(170.0, matrix[3], 9);
            Assert.Equal(20.0, matrix[7], 9);
        }

        [Fact]
        public void WordsRightOfTheLanding_MirroredAcrossALineAtFortyFiveDegrees_AreHeldByTheSameSide_ThePlaneTurnedNotBack()
        {
            foreach (bool mirrtext in new[] { false, true })
            {
                CadDocument document = Drawn(mirrtext, P(10, 0));

                Mirrored(document, P(0, 0), P(10, 10), out Groups copy);

                // AutoCAD 2021 (mirror-insert GHMI1 ML|one|right|S45, either MIRRTEXT): the mirror turns the plane's x axis
                // square to itself, not back: the plane's x axis (0, 1, 0), its y axis (-1, 0, 0), the words still held by their
                // top left (171 of 1), 176 nought, bit 14 not set.
                Near(new GeoPoint3(0, 1, 0), copy.Point(111));
                Near(new GeoPoint3(-1, 0, 0), copy.Point(112));
                Assert.Equal(1, copy.Whole(171));
                Assert.Equal(0, copy.Own(90) & (1 << 14));
            }
        }

        private static GeoPoint3 Mirror(GeoPoint3 p, bool level = false) =>
            level ? new GeoPoint3(p.X, 200 - p.Y, p.Z) : new GeoPoint3(200 - p.X, p.Y, p.Z);

        /// <summary>
        /// The groups of a MULTILEADER as the drawing writes it: those of its context data before its first leader, by which the
        /// MTEXT, plane and base point are read, and its own after the context data.
        /// </summary>
        private sealed class Groups
        {
            private readonly List<KeyValuePair<int, string>> _context = new List<KeyValuePair<int, string>>();
            private readonly List<KeyValuePair<int, string>> _own = new List<KeyValuePair<int, string>>();

            public Groups(CadDocument document, string handle)
            {
                string[] lines = document.Database.ToText().Replace("\r\n", "\n").Split('\n');
                bool found = false;
                int part = 0;

                for (int i = 0; i + 1 < lines.Length; i += 2)
                {
                    int code = int.Parse(lines[i].Trim(), CultureInfo.InvariantCulture);
                    string value = lines[i + 1].Trim();

                    if (code == 0)
                    {
                        if (found)
                        {
                            break;
                        }

                        continue;
                    }

                    if (code == 5 && value == handle && part == 0)
                    {
                        found = true;
                    }

                    if (!found)
                    {
                        continue;
                    }

                    if (code == 300)
                    {
                        part = 1;
                    }
                    else if (code == 302 && part == 1)
                    {
                        part = 2;
                    }
                    else if (code == 301)
                    {
                        part = 3;
                    }
                    else if (part == 1)
                    {
                        _context.Add(new KeyValuePair<int, string>(code, value));
                    }
                    else if (part == 3)
                    {
                        _own.Add(new KeyValuePair<int, string>(code, value));
                    }
                }

                Assert.True(found, "No MULTILEADER " + handle + ".");
            }

            public GeoPoint3 Point(int code)
            {
                int at = _context.FindIndex(g => g.Key == code);
                Assert.True(at >= 0, "No group " + code + ".");
                return new GeoPoint3(Num(_context[at].Value), Num(_context[at + 1].Value), Num(_context[at + 2].Value));
            }

            public double Number(int code) => Num(_context.First(g => g.Key == code).Value);

            public double[] Numbers(int code) => _context.Where(g => g.Key == code).Select(g => Num(g.Value)).ToArray();

            public int Whole(int code) => (int)Num(_context.First(g => g.Key == code).Value);

            public int Own(int code) => (int)Num(_own.First(g => g.Key == code).Value);

            private static double Num(string value) => double.Parse(value, CultureInfo.InvariantCulture);
        }

        private static void Near(GeoPoint3 expected, GeoPoint3 actual) => EditingDrawings.Near(expected, actual, 1E-6);
    }
}
