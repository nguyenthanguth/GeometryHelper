using System;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.UnitTest.EditingDrawings;

namespace GeometryHelper.Cad.UnitTest
{
    /// <summary>
    /// The first point (group 10) of a TEXT whose alignment <c>entmod</c>, <c>entmake</c> and the object functions change, as
    /// the routines JL, JR and JC of the maintainer's file set it: the words move back by half their width for the centre and by
    /// all of it for the right, and down by half the height for the middle. AutoCAD 2021 gave, for "short", "a longer text" and
    /// "mid" 2.5 high in txt.shx, held at x 50 by JC, 44.1667, 34.7917 and 47.5, and by JR 38.3333, 19.5833 and 45.0 (probe
    /// l8/probe/fonts-jc and fonts-jr). The tests with the real txt.shx run where the machine has one (GH_FONTS, or AutoCAD 2021's
    /// own Fonts folder) and end at once where it has none: AutoCAD's font files are not in this repository.
    /// </summary>
    public sealed class LispTextAlignTests : IDisposable
    {
        private static readonly Tolerance Tolerance = new Tolerance(1E-3, 1E-5);

        private readonly FontFolder _folder = new FontFolder();

        public void Dispose()
        {
            _folder.Dispose();
            WaitingRoutines.EndAll();
        }

        /// <summary>The Fonts folder of AutoCAD 2021 where this machine has txt.shx in it; null where it has not.</summary>
        private static string RealFonts()
        {
            string[] places =
            {
                Environment.GetEnvironmentVariable("GH_FONTS"),
                "D:/Autodesk/AutoCAD/AutoCAD 2021/Fonts",
                "C:/Program Files/Autodesk/AutoCAD 2021/Fonts",
            };

            foreach (string place in places)
            {
                if (!string.IsNullOrEmpty(place) && File.Exists(Path.Combine(place, "txt.shx")))
                {
                    return place;
                }
            }

            return null;
        }

        private static CadCommandSession Texts(string fontFolder, params string[] words)
        {
            CadDocument document = Blank();

            if (fontFolder != null)
            {
                document.Fonts = new CadFonts(new[] { fontFolder }, null, null);
            }

            CadCommandSession session = Session(document);
            Run(session, "TEXT", "0,0", "2.5", "0", words.Length > 0 ? words[0] : "short", Enter);
            Run(session, "TEXT", "0,10", "2.5", "0", words.Length > 1 ? words[1] : "a longer text", Enter);
            Run(session, "TEXT", "5,20", "2.5", "0", words.Length > 2 ? words[2] : "mid", Enter);
            return session;
        }

        // What JC and JR do to every text: the whole list given back with 72, 73 and 11 changed.
        private static void Justify(CadCommandSession session, int across, double x)
        {
            string at = x.ToString("R", CultureInfo.InvariantCulture);
            LispEntityTests.Value(session, "(setq ss (ssget \"_X\" '((0 . \"TEXT\"))))");
            LispEntityTests.Value(
                session,
                "(repeat (sslength ss) (setq e (ssname ss 0)) (ssdel e ss) (setq pt (cdr (assoc 10 (entget e)))) "
                    + "(setq tent (entget e)) (setq tent (subst (cons 73 2) (assoc 73 tent) tent)) "
                    + "(setq tent (subst (cons 72 "
                    + across
                    + ") (assoc 72 tent) tent)) (setq tent (subst (cons 11 (list "
                    + at
                    + " (cadr pt) 0.0)) (assoc 11 tent) tent)) (entmod tent))"
            );
        }

        private static GeoPoint3 FirstPointOf(CadCommandSession session, int index)
        {
            string line = LispEntityTests.Value(
                session,
                "(progn (setq e (entnext)) (repeat " + index + " (setq e (entnext e))) (cdr (assoc 10 (entget e))))"
            );
            string[] part = line.Trim('(', ')').Split(' ');
            return new GeoPoint3(
                double.Parse(part[0], CultureInfo.InvariantCulture),
                double.Parse(part[1], CultureInfo.InvariantCulture),
                double.Parse(part[2], CultureInfo.InvariantCulture)
            );
        }

        private static void AssertAt(CadCommandSession session, int index, double x, double y)
        {
            GeoPoint3 first = FirstPointOf(session, index);
            Assert.True(
                Math.Abs(first.X - x) <= Tolerance.EqualPoint && Math.Abs(first.Y - y) <= Tolerance.EqualPoint,
                "the text " + index + " starts at (" + first.X + ", " + first.Y + "), not at (" + x + ", " + y + ")"
            );
        }

        [Fact]
        public void ThreeTextsCentredByTheirMiddleAtX50_StartHalfTheirWidthBack_InTheRealTxtShx()
        {
            string fonts = RealFonts();

            if (fonts == null)
            {
                return;
            }

            CadCommandSession session = Texts(fonts);
            Justify(session, 1, 50.0);

            // AutoCAD 2021 (jc): (44.1667 -1.25), (34.7917 8.75), (47.5 18.75)
            AssertAt(session, 0, 44.1667, -1.25);
            AssertAt(session, 1, 34.7917, 8.75);
            AssertAt(session, 2, 47.5, 18.75);
        }

        [Fact]
        public void ThreeTextsRightAlignedAtX50_StartAllOfTheirWidthBack_InTheRealTxtShx()
        {
            string fonts = RealFonts();

            if (fonts == null)
            {
                return;
            }

            CadCommandSession session = Texts(fonts);
            Justify(session, 2, 50.0);

            // AutoCAD 2021 (jr): (38.3333 -1.25), (19.5833 8.75), (45.0 18.75)
            AssertAt(session, 0, 38.3333, -1.25);
            AssertAt(session, 1, 19.5833, 8.75);
            AssertAt(session, 2, 45.0, 18.75);
        }

        [Fact]
        public void ATextMadeByEntmakeCentredOnTheRealTxtShx_StartsWhereEntmodWouldPutIt()
        {
            string fonts = RealFonts();

            if (fonts == null)
            {
                return;
            }

            CadCommandSession session = Session(Blank());
            session.Document.Fonts = new CadFonts(new[] { fonts }, null, null);
            LispEntityTests.Value(
                session,
                "(entmake '((0 . \"TEXT\") (10 0.0 0.0 0.0) (40 . 2.5) (1 . \"short\") (72 . 1) (73 . 2) (11 50.0 0.0 0.0)))"
            );

            AssertAt(session, 0, 44.1667, -1.25);
        }

        [Fact]
        public void AWordMeasuredInAFontFound_MovesTheFirstPointByHalfItsMeasuredWidthWhateverTheFontIs()
        {
            // txt is the Unifont here, whose words are as wide as the measure says
            _folder.Write("txt.shx", ShxFiles.Unifont());
            CadCommandSession session = Texts(_folder.FullPath, "IOI", "IOIOIOI", "OO");
            Justify(session, 1, 50.0);

            // the Unifont is 6 wide for "IOI" 4 high (LispEntityTests): 3.75 for 2.5 high, so the first point is 1.875 back; the 7 letters of the second 10.25, 5.125 back
            AssertAt(session, 0, 50.0 - 1.875, -1.25);
            AssertAt(session, 1, 50.0 - 5.125, 8.75);
        }

        [Fact]
        public void ATextWhoseFontIsNotFound_KeepsItsFirstPointAtTheHeldPointAcross_AndShiftsItDownByHalfItsHeight()
        {
            CadCommandSession session = Texts(null);
            Justify(session, 1, 50.0);

            AssertAt(session, 0, 50.0, -1.25);
            AssertAt(session, 1, 50.0, 8.75);
        }
    }
}
