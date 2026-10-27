using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// Linetypes and multiline styles loaded from AutoCAD's files of them, a .lin and a .mln written by the tests in AutoCAD's
    /// own form, a compiled file of shapes beside them for the shapes a linetype names; ezdxf 1.4.5 read the probe's loaded
    /// linetypes and styles back, its recover and audit finding nothing.
    /// </summary>
    public class DxfSupportFileTests
    {
        private const string Linetypes = ";; Linetypes of GeometryHelper's tests, as acad.lin writes them.\r\n"
            + ";;\r\n"
            + "*DASHED,Dashed __ __ __ __ __ __ __ __ __ __ __ __ __ _\r\n"
            + "A,.5,-.25\r\n"
            + "\r\n"
            + "*GAS_LINE,Gas line ----GAS----GAS----GAS----GAS----GAS----GAS--\r\n"
            + "A,.5,-.2,[\"GAS\",STANDARD,S=.1,U=0.0,X=-0.1,Y=-.05],-.25\r\n"
            + "*FENCELINE1,Fenceline circle ----0-----0----0-----0----0-----0--\r\n"
            + "A,.25,-.1,[CIRC1,ltypeshp.shx,x=-.1,s=.1],-.1,1\r\n"
            + "*TRACKS,Tracks -|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-|-\r\n"
            + "A,.15,[132,ltypeshp.shx,s=.25,a=90],.15\r\n"
            + "*TURNED,Turned text\r\n"
            + "A,.5,[\"T\",STANDARD,R=11,S=.2],-.25\r\n";

        private const string Styles = "MLSTYLE\n  2\nSTANDARD\n 70\n     0\n  3\n\n 62\n   256\n 51\n90.000000\n 52\n90.000000\n 71\n     2\n"
            + " 49\n0.500000\n 62\n   256\n  6\nBYLAYER\n 49\n-0.500000\n 62\n   256\n  6\nBYLAYER\n"
            + "  0\nMLSTYLE\n  2\nGH_WALL\n 70\n   273\n  3\nWall 300\n 62\n     8\n 51\n90.000000\n 52\n45.000000\n 71\n     3\n"
            + " 49\n150.000000\n 62\n     1\n  6\nBYLAYER\n 49\n0.000000\n 62\n   256\n  6\nDASHED\n 49\n-150.000000\n 62\n     1\n  6\nBYBLOCK\n  0\n";

        private static DxfDatabase NewDrawing() => DxfDatabase.Parse(new DxfWriter().ToString());

        /// <summary>A compiled file of shapes of AutoCAD's form, its shapes TRACK1 (132) and CIRC1 (133), each a pen lifted and stopped.</summary>
        private static byte[] ShapeFile()
        {
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 shapes 1.0\r\n\u001A"));
            byte[][] shapes = { Shape("TRACK1"), Shape("CIRC1") };
            bytes.AddRange(new byte[] { 132, 0, 133, 0, 2, 0 });
            bytes.AddRange(new byte[] { 132, 0, (byte)shapes[0].Length, 0, 133, 0, (byte)shapes[1].Length, 0 });
            bytes.AddRange(shapes[0]);
            bytes.AddRange(shapes[1]);
            return bytes.ToArray();

            byte[] Shape(string name) => Encoding.ASCII.GetBytes(name).Concat(new byte[] { 0, 2, 0 }).ToArray();
        }

        private static void InFolder(Action<string> test)
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-support-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            try
            {
                File.WriteAllText(Path.Combine(folder, "gh.lin"), Linetypes, Encoding.ASCII);
                File.WriteAllText(Path.Combine(folder, "gh.mln"), Styles, Encoding.ASCII);
                test(folder);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]
        public void ALinetypeLoaded_IsTheFilesDescriptionAndDashes_ItsTextInTheStyleTheFileNames()
        {
            InFolder(folder =>
            {
                DxfDatabase db = NewDrawing();
                string text = db.ToText();

                db.LoadLineTypeFile("gas_line", Path.Combine(folder, "gh.lin"));
                db.LoadLineTypeFile("DASHED", Path.Combine(folder, "gh.lin"));

                string saved = db.ToText();
                string standard = db.TextStyleTable["Standard"].Handle;
                DxfLinetypeTableRecord gas = db.LinetypeTable["GAS_LINE"];
                Assert.EndsWith("  2\nGAS_LINE\n 70\n0\n  3\nGas line ----GAS----GAS----GAS----GAS----GAS----GAS--\n 72\n65\n 73\n3\n 40\n0.95\n"
                    + " 49\n0.5\n 74\n0\n 49\n-0.2\n 74\n2\n 75\n0\n340\n" + standard + "\n 46\n0.1\n 50\n0\n 44\n-0.1\n 45\n-0.05\n  9\nGAS\n 49\n-0.25\n 74\n0\n",
                    Records(saved).Single(r => HandleOf(r) == gas.Handle), StringComparison.Ordinal);
                OnlyTheseChanged(text, saved, gas.Handle, db.LinetypeTable["DASHED"].Handle, db.LinetypeTable.Handle);

                DxfLinetypeTableRecord read = DxfDatabase.Parse(saved).LinetypeTable["GAS_LINE"];
                Assert.Equal(3, read.NumDashes);
                Assert.Equal(0.95, read.PatternLength, 15);
                Assert.Equal("GAS", read.TextAt(1));
                Assert.Equal("Standard", read.ShapeStyleAt(1).Name);
                Assert.Equal(0.1, read.ShapeScaleAt(1));
                Assert.Equal(new GeoVector2(-0.1, -0.05), read.ShapeOffsetAt(1));
                Assert.False(read.ShapeIsUcsOrientedAt(1));
                DxfLinetypeTableRecord dashed = DxfDatabase.Parse(saved).LinetypeTable["DASHED"];
                Assert.Equal("Dashed __ __ __ __ __ __ __ __ __ __ __ __ __ _", dashed.AsciiDescription);
                Assert.Equal(new[] { 0.5, -0.25 }, Enumerable.Range(0, dashed.NumDashes).Select(dashed.DashLengthAt));
                Assert.Equal(0.75, dashed.PatternLength);

                // A rotation is written in the degrees the file gives, 11 and not 11.000000000000002 of a trip through radians.
                db.LoadLineTypeFile("TURNED", Path.Combine(folder, "gh.lin"));
                Assert.Contains(" 46\n0.2\n 50\n11\n", Records(db.ToText()).Single(r => HandleOf(r) == db.LinetypeTable["TURNED"].Handle), StringComparison.Ordinal);
            });
        }

        [Fact]
        public void AShapeOfALinetype_IsFoundByNameInItsCompiledFile_AndDrawnInAStyleOfThatFileWithNoName()
        {
            InFolder(folder =>
            {
                File.WriteAllBytes(Path.Combine(folder, "ltypeshp.shx"), ShapeFile());
                DxfDatabase db = NewDrawing();

                db.LoadLineTypeFile("FENCELINE1", Path.Combine(folder, "gh.lin"));
                db.LoadLineTypeFile("TRACKS", Path.Combine(folder, "gh.lin"));

                DxfDatabase read = DxfDatabase.Parse(db.ToText());
                DxfLinetypeTableRecord fence = read.LinetypeTable["FENCELINE1"];
                Assert.Equal(133, fence.ShapeNumberAt(1));
                Assert.True(fence.ShapeStyleAt(1).IsShapeFile);
                Assert.Equal("ltypeshp.shx", fence.ShapeStyleAt(1).FileName);
                Assert.Equal(string.Empty, fence.ShapeStyleAt(1).Name);
                Assert.Equal(0.1, fence.ShapeScaleAt(1));
                Assert.Equal(new[] { 0.25, -0.1, -0.1, 1.0 }, Enumerable.Range(0, fence.NumDashes).Select(fence.DashLengthAt));

                // A shape by its number needs no file; A= turns it from the x axis, in degrees.
                DxfLinetypeTableRecord tracks = read.LinetypeTable["TRACKS"];
                Assert.Equal(132, tracks.ShapeNumberAt(0));
                Assert.True(tracks.ShapeIsUcsOrientedAt(0));
                Assert.Equal(Math.PI / 2, tracks.ShapeRotationAt(0), 12);
                Assert.Same(fence.ShapeStyleAt(1), tracks.ShapeStyleAt(0));
                Assert.Single(read.TextStyleTable, s => s.IsShapeFile);
            });
        }

        [Fact]
        public void EveryLinetypeOfAFile_IsLoadedByAWildcard_ThoseThatCannotBeLeftOut()
        {
            InFolder(folder =>
            {
                DxfDatabase db = NewDrawing();
                db.LoadLineTypeFile("DASHED", Path.Combine(folder, "gh.lin"));

                // DASHED is there already, and the file of shapes FENCELINE1 names by name is not beside the file.
                db.LoadLineTypeFile("*", Path.Combine(folder, "gh.lin"));

                Assert.Equal(new[] { "ByBlock", "ByLayer", "Continuous", "DASHED", "GAS_LINE", "TRACKS", "TURNED" }, db.LinetypeTable.Select(l => l.Name));
                Assert.Throws<ArgumentException>(() => db.LoadLineTypeFile("FENCELINE1", Path.Combine(folder, "gh.lin")));
                Assert.Throws<ArgumentException>(() => db.LoadLineTypeFile("dashed", Path.Combine(folder, "gh.lin")));
                Assert.Throws<ArgumentException>(() => db.LoadLineTypeFile("HIDDEN", Path.Combine(folder, "gh.lin")));
                db.LoadLineTypeFile("NONE*", Path.Combine(folder, "gh.lin"));
                Assert.Throws<FileNotFoundException>(() => db.LoadLineTypeFile("DASHED", Path.Combine(folder, "none.lin")));
                Assert.Throws<ArgumentNullException>(() => db.LoadLineTypeFile(null, Path.Combine(folder, "gh.lin")));
                Assert.Throws<ArgumentNullException>(() => db.LoadLineTypeFile("DASHED", null));
            });
        }

        [Fact]
        public void ADrawingOfR12_TakesTheSimpleLinetypes_AndRefusesTheComplexOnes()
        {
            InFolder(folder =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);
                string text = db.ToText();

                Assert.Throws<ArgumentException>(() => db.LoadLineTypeFile("GAS_LINE", Path.Combine(folder, "gh.lin")));
                Assert.Equal(text, db.ToText());
                db.LoadLineTypeFile("*", Path.Combine(folder, "gh.lin"));

                DxfDatabase read = DxfDatabase.Parse(db.ToText());
                Assert.Equal(new[] { "ByBlock", "ByLayer", "Continuous", "DASHED" }, read.LinetypeTable.Select(l => l.Name));
                Assert.DoesNotContain(" 74\n", Records(db.ToText()).Single(r => r.Contains("\n  2\nDASHED\n") && r.StartsWith("  0\nLTYPE\n", StringComparison.Ordinal)), StringComparison.Ordinal);
            });
        }

        [Fact]
        public void ALinetypeOfShapesADrawingCannotPointAt_IsRefused_AndNoStyleOfShapesIsLeftBehind()
        {
            // A drawing of R2000 that writes no handles, so that no dash can point at the style of ltypeshp.shx.
            const string text = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1015\n  0\nENDSEC\n"
                + "  0\nSECTION\n  2\nTABLES\n"
                + "  0\nTABLE\n  2\nLTYPE\n 70\n1\n  0\nLTYPE\n  2\nCONTINUOUS\n 70\n0\n  3\nSolid line\n 72\n65\n 73\n0\n 40\n0.0\n  0\nENDTAB\n"
                + "  0\nTABLE\n  2\nSTYLE\n 70\n1\n  0\nSTYLE\n  2\nSTANDARD\n 70\n0\n 40\n0.0\n 41\n1.0\n 50\n0.0\n 71\n0\n 42\n2.5\n  3\ntxt\n  4\n\n  0\nENDTAB\n"
                + "  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n  0\nENDSEC\n  0\nEOF\n";

            InFolder(folder =>
            {
                File.WriteAllBytes(Path.Combine(folder, "ltypeshp.shx"), ShapeFile());
                DxfDatabase db = DxfDatabase.Parse(text);
                string before = db.ToText();

                Assert.Throws<ArgumentException>(() => db.LoadLineTypeFile("TRACKS", Path.Combine(folder, "gh.lin")));

                Assert.Equal(before, db.ToText());
                Assert.Single(db.TextStyleTable);
            });
        }

        [Fact]
        public void AMultilineStyleLoaded_IsAnMlineStyleOfTheDictionaryOfStyles_WrittenAsTheFileGivesIt()
        {
            InFolder(folder =>
            {
                DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
                string mln = Path.Combine(folder, "gh.mln");

                // An element of GH_WALL is drawn with DASHED, which the drawing has not yet.
                Assert.Throws<ArgumentException>(() => db.LoadMlineStyleFile("GH_WALL", mln));
                db.LoadLineTypeFile("DASHED", Path.Combine(folder, "gh.lin"));
                db.LoadMlineStyleFile("gh_wall", mln);

                DxfDBDictionary styles = db.MLStyleDictionary;
                var wall = (DxfMlineStyle)styles.GetAt("GH_WALL");
                Assert.Equal(styles.Handle, wall.OwnerHandle);
                Assert.EndsWith("100\nAcDbMlineStyle\n  2\nGH_WALL\n 70\n273\n  3\nWall 300\n 62\n8\n 51\n90.000000\n 52\n45.000000\n 71\n3\n"
                    + " 49\n150.000000\n 62\n1\n  6\nBYLAYER\n 49\n0.000000\n 62\n256\n  6\nDASHED\n 49\n-150.000000\n 62\n1\n  6\nBYBLOCK\n",
                    Records(db.ToText()).Single(r => HandleOf(r) == wall.Handle), StringComparison.Ordinal);

                var read = (DxfMlineStyle)DxfDatabase.Parse(db.ToText()).MLStyleDictionary.GetAt("GH_WALL");
                Assert.Equal("GH_WALL", read.Name);
                Assert.Equal("Wall 300", read.Description);
                Assert.True(read.Filled);
                Assert.True(read.StartSquareCap);
                Assert.True(read.EndSquareCap);
                Assert.False(read.ShowMiters);
                Assert.False(read.StartRoundCap);
                Assert.Equal(8, read.FillColor.ColorIndex);
                Assert.Equal(Math.PI / 2, read.StartAngle, 15);
                Assert.Equal(Math.PI / 4, read.EndAngle, 15);
                Assert.Equal(new[] { 150.0, 0.0, -150.0 }, read.Elements.Select(e => e.Offset));
                Assert.Equal(new[] { "BYLAYER", "DASHED", "BYBLOCK" }, read.Elements.Select(e => e.Linetype));
                Assert.True(read.Elements[1].Color.IsByLayer);
                Assert.Equal(1, read.Elements[0].Color.ColorIndex);

                Assert.Throws<ArgumentException>(() => db.LoadMlineStyleFile("GH_WALL", mln));
                Assert.Throws<ArgumentException>(() => db.LoadMlineStyleFile("STANDARD", mln));
                Assert.Throws<ArgumentException>(() => db.LoadMlineStyleFile("GH_NONE", mln));
                Assert.Throws<InvalidOperationException>(() => DxfDatabase.Parse(DxfFileEzdxfDrawings.R12).LoadMlineStyleFile("STANDARD", mln));
            });
        }

        [Fact]
        public void ADrawingWithNoDictionaryOfMultilineStyles_IsGivenOne()
        {
            InFolder(folder =>
            {
                DxfDatabase db = NewDrawing();
                Assert.Null(db.MLStyleDictionary);

                db.LoadMlineStyleFile("STANDARD", Path.Combine(folder, "gh.mln"));

                DxfDatabase read = DxfDatabase.Parse(db.ToText());
                Assert.Equal(read.NamedObjectsDictionary.Handle, read.MLStyleDictionary.OwnerHandle);
                var standard = (DxfMlineStyle)read.MLStyleDictionary.GetAt("STANDARD");
                Assert.Equal(new[] { 0.5, -0.5 }, standard.Elements.Select(e => e.Offset));
                Assert.True(standard.FillColor.IsByLayer);
            });
        }

        [Fact]
        public void TheStylesOfMultilinesOfADrawing_AreReadAsMlineStyles()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            var standard = (DxfMlineStyle)db.MLStyleDictionary.GetAt("Standard");

            Assert.Equal("Standard", standard.Name);
            Assert.Equal(2, standard.Elements.Count);
            Assert.Same(standard, db.CmlstyleID);
            Assert.Equal("MLINESTYLE " + standard.Handle + " Standard", standard.ToString());
        }
    }
}
