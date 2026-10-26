using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The codes of an MTEXT as AutoCAD's <c>MText</c> names them, and its words as RTF and back: what is carried over, what is
    /// left out, and that RTF the library writes comes back as the same words in the same look.
    /// </summary>
    public class DxfMTextRtfTests
    {
        [Fact]
        public void TheCodesOfAnMText_AreAutoCADs()
        {
            Assert.Equal(@"\A", DxfMText.AlignChange);
            Assert.Equal("{", DxfMText.BlockBegin);
            Assert.Equal("}", DxfMText.BlockEnd);
            Assert.Equal(@"\C", DxfMText.ColorChange);
            Assert.Equal(@"\F", DxfMText.FontChange);
            Assert.Equal(@"\H", DxfMText.HeightChange);
            Assert.Equal(@"\P", DxfMText.LineBreak);
            Assert.Equal(@"\~", DxfMText.NonBreakSpace);
            Assert.Equal(@"\Q", DxfMText.ObliqueChange);
            Assert.Equal(@"\o", DxfMText.OverlineOff);
            Assert.Equal(@"\O", DxfMText.OverlineOn);
            Assert.Equal(@"\P", DxfMText.ParagraphBreak);
            Assert.Equal(@"\S", DxfMText.StackStart);
            Assert.Equal(@"\k", DxfMText.StrikethroughOff);
            Assert.Equal(@"\K", DxfMText.StrikethroughOn);
            Assert.Equal(@"\T", DxfMText.TrackChange);
            Assert.Equal(@"\l", DxfMText.UnderlineOff);
            Assert.Equal(@"\L", DxfMText.UnderlineOn);
            Assert.Equal(@"\W", DxfMText.WidthChange);

            var mtext = new DxfMText { Contents = "B-12" + DxfMText.ParagraphBreak + DxfMText.BlockBegin + DxfMText.HeightChange + "2x;" + DxfMText.UnderlineOn + "M16" + DxfMText.BlockEnd };
            Assert.Equal("B-12\nM16", mtext.Text);
        }

        [Fact]
        public void TheWordsOfAnMText_AreWrittenAsRtf_TheCodesRtfHasCarriedOver()
        {
            var mtext = new DxfMText { Contents = @"B-12\P{\C1;\LM16\l}" };
            Assert.Equal(@"{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}}{\colortbl ;\red255\green0\blue0;}B-12\par {\cf1 \ul M16\ulnone }\par}", mtext.ContentsRTF);

            mtext.Contents = @"{\fArial|b1|i0|c0|p34;Bold} {\Fromans.shx;shx} {\fArial|b0|i1;it} \S1^2; %%d %%C \U+00E9 a\~b \H2x;\W0.8;\Q15;\T1.2;\A1;\pxqc;big \Oover\o \Kgone\k ^Itab \\ \{x\} \C256;\X\Nnext \M+1A4A1";
            Assert.Equal(
                @"{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}{\f1 Arial;}{\f2{\*\ghfontfile}romans.shx;}}{\f1\b\i0 Bold} {\f2 shx} {\f1\b0\i it} 1/2 \u176? \u8960? \u233? a\~b big over \strike gone\strike0  \tab tab \\ \{x\} \cf0 \line \column next \\M+1A4A1\par}",
                mtext.ContentsRTF);
        }

        [Fact]
        public void TheRtfTheLibraryWrites_ComesBackAsTheSameWordsInTheSameLook()
        {
            var mtext = new DxfMText();
            string[][] contents =
            {
                new[] { @"B-12\P{\C1;\LM16\l}", @"B-12\P\C1;\LM16" },
                new[] { @"{\fArial|b1|i0;Bold} plain {\Fromans.shx;shx} \Kstruck\k end", @"\fArial|b1|i0;Bold\Ftxt; plain \Fromans.shx;shx\Ftxt; \Kstruck\k end" },
                new[] { @"a\~b \\ \{c\} ^Itab é", @"a\~b \\ \{c\} ^Itab é" },
                new[] { string.Empty, string.Empty },
                new[] { @"abc\P", @"abc\P" },
                new[] { @"a\Pb\P\P", @"a\Pb\P\P" },
                new[] { @"\Pa", @"\Pa" },
                new[] { @"\Fromans;abc", @"\Fromans;abc" },
                new[] { @"\Fromans|c0;abc", @"\Fromans;abc" },
                new[] { @"x {\Fromans;abc} y", @"x \Fromans;abc\Ftxt; y" },
            };

            foreach (string[] pair in contents)
            {
                mtext.Contents = pair[0];
                string text = mtext.Text;
                int length = mtext.SetContentsRtf(mtext.ContentsRTF);

                Assert.Equal(pair[1], mtext.Contents);
                Assert.Equal(text, mtext.Text);
                Assert.Equal(mtext.Contents.Length, length);
            }

            mtext.Contents = @"B-12\P{\C1;\LM16\l}";
            mtext.ContentsRTF = mtext.ContentsRTF;
            Assert.Equal(@"B-12\P\C1;\LM16", mtext.Contents);
        }

        [Fact]
        public void TheRtfOfAMillionLetters_IsWrittenInTimeLinearInThem()
        {
            // 320 000 paragraphs and no semicolon: a search for one at each code took six seconds.
            var mtext = new DxfMText { Contents = string.Concat(Enumerable.Repeat(@"ab\P", 320000)) };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string rtf = mtext.ContentsRTF;
            watch.Stop();

            Assert.True(watch.ElapsedMilliseconds < 2000, "ContentsRTF of 1 280 000 letters took " + watch.ElapsedMilliseconds + " ms.");
            Assert.EndsWith(@"ab\par \par}", rtf, StringComparison.Ordinal);
            Assert.Equal(mtext.Contents.Length, mtext.SetContentsRtf(rtf));
        }

        [Fact]
        public void RtfThatIsNotWhole_IsReadWithoutThrowing()
        {
            var mtext = new DxfMText();
            string[] broken =
            {
                string.Empty, "{", "}", "}}}{", @"\", @"{\rtf1", @"{\rtf1 \'", @"{\rtf1 \'z9 a}", @"{\rtf1 \u", @"{\rtf1 \u-", @"{\rtf1 \u99999999999 x}",
                @"{\rtf1 \uc-5 \u233 ab}", @"{\rtf1{\fonttbl{\f99999999999 a;}}\f99999999999 x}", @"{\rtf1{\colortbl;\red999\green-4;}\cf1 x\cf9 y\cf-1 z}",
                @"{\rtf1{\fonttbl{\f0 a", @"{\*", @"{\rtf1 \par\par\par", @"plain words", @"{\rtf1\b-\i- x}", "{\\rtf1 \\\r\n x}",
            };

            foreach (string rtf in broken)
            {
                int length = mtext.SetContentsRtf(rtf);
                Assert.Equal(mtext.Contents.Length, length);
                Assert.DoesNotContain('\n', mtext.Contents);
            }

            Assert.Equal("plain words", mtext.SetContentsRtf("plain words") > 0 ? mtext.Contents : null);
        }

        [Fact]
        public void RtfAsAWordProcessorWritesIt_IsReadIntoTheCodesOfAnMText()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            string rtf = "{\\rtf1\\ansi\\ansicpg1252\\deff0\\nouicompat{\\fonttbl{\\f0\\fnil\\fcharset0 Calibri;}{\\f1\\fswiss{\\*\\panose 020b0604020202020204}Arial;}}\r\n"
                + "{\\colortbl ;\\red255\\green0\\blue0;\\red0\\green0\\blue255;}\r\n{\\*\\generator Riched20 10.0.22621}{\\info{\\author GH}}\\viewkind4\\uc1 \r\n"
                + "\\pard\\sa200\\sl276\\slmult1\\f0\\fs22\\lang9 Plain \\b bold\\b0  \\i italic\\i0  \\ul under\\ulnone  \\strike strike\\strike0\\par\r\n"
                + "\\cf1 red\\cf0  \\cf2 blue\\cf0  \\f1 arial\\f0  caf\\'e9 \\u8364? \\'80 \\emdash\\~x\\tab y \\{z\\} \\\\ a^b\\line end\\par\r\n}";

            int length = mtext.SetContentsRtf(rtf);

            string expected = @"\fCalibri|b0|i0;Plain \fCalibri|b1|i0;bold\fCalibri|b0|i0; \fCalibri|b0|i1;italic\fCalibri|b0|i0; \Lunder\l \Kstrike\P"
                + @"\C1;\kred\C256; \C5;blue\C256; \fArial|b0|i0;arial\fCalibri|b0|i0; café € € —\~x^Iy \{z\} \\ a^ b\Pend";
            Assert.Equal(expected, mtext.Contents);
            Assert.Equal(expected.Length, length);
            Assert.Equal("Plain bold italic under strike\nred blue arial café € € — x\ty {z} \\ a^b\nend", mtext.Text);

            // The style's font is written by no code; a font's file by \F.
            mtext.SetContentsRtf(@"{\rtf1{\fonttbl{\f0 txt;}{\f1 romans.shx;}}\f0 one {\f1 two} {\b three}}");
            Assert.Equal(@"one \Fromans.shx;two\Ftxt; three", mtext.Contents);

            Assert.Throws<ArgumentNullException>(() => mtext.SetContentsRtf(null));
            mtext.Erase();
            Assert.Throws<InvalidOperationException>(() => mtext.SetContentsRtf(@"{\rtf1 x}"));
        }
    }
}
