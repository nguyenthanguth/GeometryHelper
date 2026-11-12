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
    /// The objects of a paste are drawn once and moved to the cursor, not drawn anew at each move: the preview at one cursor
    /// and then at another is what the paste makes at each, for every kind of item a drawing gives.
    /// </summary>
    public class PastePreviewMovedTests
    {
        private static readonly GeoPoint2[] Square =
        {
            new GeoPoint2(0, 0),
            new GeoPoint2(30, 0),
            new GeoPoint2(30, 20),
            new GeoPoint2(0, 20),
        };

        /// <summary>A drawing of every kind of item: lines, a circle, an arc, a polyline, a text, a block, an xline, a ray and two hatches.</summary>
        private static CadDocument Mixed()
        {
            CadDocument document = Plan();
            DxfDatabase database = document.Database;
            database.ModelSpace.AppendEntity(
                new DxfXline
                {
                    BasePoint = new GeoPoint3(0, 0, 0),
                    UnitDir = new GeoVector3(Math.Sqrt(0.5), Math.Sqrt(0.5), 0),
                }
            );
            database.ModelSpace.AppendEntity(
                new DxfRay { BasePoint = new GeoPoint3(10, 0, 0), UnitDir = new GeoVector3(0, 1, 0) }
            );
            var lines = new DxfHatch();
            lines.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            lines.PatternAngle = Math.PI / 4.0;
            lines.PatternSpace = 3.0;
            lines.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(lines);
            var solid = new DxfHatch();
            solid.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
            solid.AppendLoop(DxfHatchLoopTypes.External, Square, null);
            database.ModelSpace.AppendEntity(solid);
            return CadDocument.From(DxfDatabase.Parse(database.ToText()), "mixed.dxf");
        }

        [Fact]
        public void ThePastePreview_AtOneCursorThenAnother_IsWhatThePasteMakesAtEach()
        {
            CadDocument source = Mixed();
            var clipboard = new CadMemoryClipboard();
            string[] handles = source.ModelSpace.Block.Select(e => e.Handle).ToArray();
            CadCommandSession copy = Session(source);
            copy.Clipboard = clipboard;
            Run(copy, "COPYCLIP", handles, Enter);

            foreach (GeoPoint2 first in new[] { P(300, 200), P(-1250.5, 0.25) })
            {
                CadCommandSession session = Session(Blank());
                session.Clipboard = clipboard;
                Run(session, "PASTECLIP");
                CadPreview preview = session.Prompt.Preview;

                // Drawn first at another cursor, so that the items asked for are moved from those drawn once.
                Assert.NotEmpty(preview.Items(P(17, -3)));
                Assert.Contains(preview.Items(first), i => i is DisplaySegments2);
                Assert.Contains(preview.Items(first), i => i is DisplayFill2);
                Assert.Contains(preview.Items(first), i => i is DisplayRay2);
                Assert.Contains(preview.Items(first), i => i is DisplayArc2);
                Assert.Contains(preview.Items(first), i => i is DisplayText2);
                PreviewTests.PreviewMakes(session, first);
            }
        }

        /// <summary>
        /// A drawing of the words a paste draws by laying them out: a TEXT and an MTEXT in the presenter's own stroke font, the
        /// same two in a TrueType style, an MTEXT with a coloured run, a stack and a line under, a dimension and some lines.
        /// </summary>
        private static CadDocument Words()
        {
            var database = new DxfDatabase("AC1027");
            database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "ARIALS", FileName = "arial.ttf" });

            for (int i = 0; i < 4; i++)
            {
                var text = new DxfDBText
                {
                    TextString = "Plate " + i,
                    Position = new GeoPoint3(i * 60.0, 0.0, 0.0),
                    Height = 5.0,
                    Rotation = i == 3 ? 0.5 : 0.0,
                };
                database.ModelSpace.AppendEntity(text);

                if (i % 2 == 1)
                {
                    text.TextStyleName = "ARIALS";
                }

                var mtext = new DxfMText
                {
                    Contents = i == 0 ? "bolt M20\\Pgrade {\\C1;8.8} \\S1/2;\\L under\\l" : "steel plate\\Pwith weld",
                    Location = new GeoPoint3(i * 60.0, 40.0, 0.0),
                    TextHeight = 4.0,
                    Width = i == 2 ? 30.0 : 0.0,
                };
                database.ModelSpace.AppendEntity(mtext);

                if (i % 2 == 1)
                {
                    mtext.TextStyleName = "ARIALS";
                }

                database.ModelSpace.AppendEntity(
                    new DxfLine(new GeoPoint3(i * 60.0, -10.0, 0.0), new GeoPoint3(i * 60.0 + 50.0, -10.0, 0.0))
                );
            }

            CadDocument document = CadDocument.From(database, "words.dxf");
            Run(Session(document), "DIMLINEAR", P(0, 80), P(100, 80), P(50, 100));
            Assert.Contains(document.ModelSpace.Block, e => e is DxfDimension);
            return CadDocument.From(DxfDatabase.Parse(document.Database.ToText()), "words.dxf");
        }

        /// <summary>A blank drawing asking where to paste what a drawing of <see cref="Words"/> copied, the presenter's stroke font its fonts.</summary>
        private static CadCommandSession Pasting(CadMemoryClipboard clipboard)
        {
            CadDocument target = Blank();
            target.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);
            CadCommandSession session = Session(target);
            session.Clipboard = clipboard;
            Run(session, "PASTECLIP");
            return session;
        }

        private static CadMemoryClipboard Copied(CadDocument source)
        {
            var clipboard = new CadMemoryClipboard();
            CadCommandSession copy = Session(source);
            copy.Clipboard = clipboard;
            Run(copy, "COPYCLIP", source.ModelSpace.Block.Select(e => e.Handle).ToArray(), Enter);
            return clipboard;
        }

        [Fact]
        public void TextsMTextsAndADimension_AreMovedFromTheFirstDrawing_NotLaidOutAtEachCursor()
        {
            CadCommandSession session = Pasting(Copied(Words()));
            CadPreview preview = session.Prompt.Preview;

            IReadOnlyList<DisplayItem2> first = preview.Items(P(17, -3));
            Assert.Contains(first, i => i is DisplayText2 t && t.Strokes != null && t.Parts == null);
            Assert.Contains(first, i => i is DisplayText2 t && t.Strokes == null && t.Parts == null);
            Assert.Contains(first, i => i is DisplayText2 t && t.Parts != null);
            int drawn = preview.DrawnAnew;
            Assert.Equal(13, drawn);

            foreach (GeoPoint2 cursor in new[] { P(300, 200), P(-1250.5, 0.25), P(17, -3) })
            {
                Assert.Equal(first.Count, preview.Items(cursor).Count);
                Assert.Equal(drawn, preview.DrawnAnew);
            }
        }

        [Fact]
        public void TheWordsMovedToTheCursor_AreWhatThePasteDrawsThere_StrokesPartsAndBoxesToo()
        {
            CadMemoryClipboard clipboard = Copied(Words());

            foreach (GeoPoint2 cursor in new[] { P(300, 200), P(-1250.5, 0.25) })
            {
                CadCommandSession session = Pasting(clipboard);
                CadPreview preview = session.Prompt.Preview;

                // Drawn first at another cursor, so that the items asked for are moved from those drawn once.
                Assert.NotEmpty(preview.Items(P(17, -3)));
                var previewed = new List<object>();
                IReadOnlyList<DisplayItem2> items = preview.Items(cursor);

                foreach (DisplayItem2 item in items)
                {
                    Flatten(item, previewed);
                }

                CadDocument document = session.Document;
                HashSet<string> before = new HashSet<string>(
                    document.GetDisplay(document.ModelSpace).Items.Select(i => i.Handle ?? string.Empty)
                );
                Run(session, cursor);
                var pasted = new List<object>();
                int count = 0;

                foreach (
                    DisplayItem2 item in document
                        .GetDisplay(document.ModelSpace)
                        .Items.Where(i => i.Handle != null && !before.Contains(i.Handle))
                )
                {
                    Flatten(item, pasted);
                    count++;
                }

                Assert.Equal(count, items.Count);
                Assert.Equal(pasted.Count, previewed.Count);

                for (int i = 0; i < pasted.Count; i++)
                {
                    if (pasted[i] is double number)
                    {
                        Assert.Equal(number, Assert.IsType<double>(previewed[i]), 8);
                    }
                    else
                    {
                        Assert.Equal(pasted[i], previewed[i]);
                    }
                }
            }
        }

        /// <summary>Everything a canvas reads of an item, in order: its kind, colour and layer, its numbers, and those of its parts and strokes.</summary>
        private static void Flatten(DisplayItem2 item, List<object> into)
        {
            into.Add(item.GetType().Name);
            into.Add(item.Style.Rgb);
            into.Add(item.Layer);
            DisplayBox box = item.Bounds;
            into.AddRange(new object[] { box.MinX, box.MinY, box.MaxX, box.MaxY });

            switch (item)
            {
                case DisplayPolyline2 chain:
                    into.Add(chain.Closed);
                    into.AddRange(chain.Xy.Cast<object>());
                    break;
                case DisplaySegments2 segments:
                    into.AddRange(segments.Xy.Cast<object>());
                    break;
                case DisplayFill2 fill:
                    foreach (double[] ring in fill.Rings)
                    {
                        into.AddRange(ring.Cast<object>());
                    }

                    break;
                case DisplayText2 text:
                    into.AddRange(
                        new object[]
                        {
                            text.Text,
                            text.X,
                            text.Y,
                            text.Height,
                            text.Rotation,
                            text.WidthFactor,
                            text.Horizontal,
                            text.Vertical,
                            text.FontName,
                            text.Oblique,
                            text.IsBackward,
                            text.IsBold,
                            text.IsItalic,
                            text.EstimatedWidth,
                        }
                    );

                    if (text.Strokes != null)
                    {
                        into.Add(text.Strokes.Count);

                        foreach (double[] stroke in text.Strokes)
                        {
                            into.AddRange(stroke.Cast<object>());
                        }
                    }

                    if (text.Parts != null)
                    {
                        into.Add(text.Parts.Count);

                        foreach (DisplayItem2 part in text.Parts)
                        {
                            Flatten(part, into);
                        }
                    }

                    break;
            }
        }
    }
}
