using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfLines;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A block of texts placed every way an INSERT places it, written by hand, read by <see cref="DxfReader"/> and held
    /// against what ezdxf 1.4.5 makes of it: each text of the block carried by ezdxf's own transform of a TEXT and an MTEXT,
    /// worked out from the same text and written here as numbers. The block TXT, drawn about its base point (2, 1, 0), has a
    /// TEXT aligned left with its signs, one centred, one in the middle, one top right, one on the extrusion down and one on
    /// a tilted extrusion, an MTEXT turned by its direction, one by its angle and one on a tilted extrusion. It is placed
    /// turned and evenly scaled, scaled along x and along y, mirrored, on a tilted extrusion, mirrored and scaled, inside the
    /// block OUTER placed evenly and unevenly, and mirrored along z.
    /// </summary>
    /// <remarks>
    /// The two differ only where a placement shears a text, which ezdxf leans by an oblique angle: there ezdxf takes the
    /// height square to the baseline, and the reader the length of the carried y axis, so the reader's height times the
    /// cosine of ezdxf's oblique angle is ezdxf's, and its width factor over that cosine; an MTEXT's height times the sine of
    /// the angle between its carried axes.
    /// </remarks>
    public class DxfReaderTextEzdxfTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        // How far a point may stand from where ezdxf puts it: the two work in doubles, by different arithmetic.
        private const double Near = 1E-9;

        private static readonly string Fixture = string.Empty
            + G(0, "SECTION", 2, "HEADER", 9, "$ACADVER", 1, "AC1015", 9, "$INSUNITS", 70, "4")
            + G(0, "ENDSEC")
            + G(0, "SECTION", 2, "BLOCKS")
            + G(0, "BLOCK", 8, "0", 2, "TXT", 70, "0", 10, "2", 20, "1", 30, "0", 3, "TXT")
            + G(0, "TEXT", 5, "D1", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "1", 20, "2", 30, "0", 40, "2.5", 1, "%%c20 at 45%%d", 50, "30", 41, "0.8", 100, "AcDbText")
            + G(0, "TEXT", 5, "D2", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "0", 20, "0", 30, "0", 40, "3", 1, "centre", 72, "1", 11, "5", 21, "5", 31, "0", 100, "AcDbText")
            + G(0, "TEXT", 5, "D3", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "0", 20, "0", 30, "0", 40, "2", 1, "middle", 50, "90", 72, "4", 11, "7", 21, "1", 31, "0", 100, "AcDbText")
            + G(0, "TEXT", 5, "D4", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "0", 20, "0", 30, "0", 40, "1.5", 1, "top right", 72, "2", 11, "-3", 21, "4", 31, "0", 100, "AcDbText", 73, "3")
            + G(0, "TEXT", 5, "D5", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "3", 20, "4", 30, "0", 40, "2", 1, "under", 50, "45", 210, "0", 220, "0", 230, "-1", 100, "AcDbText")
            + G(0, "TEXT", 5, "D6", 100, "AcDbEntity", 8, "0", 100, "AcDbText", 10, "1", 20, "1", 30, "1", 40, "2", 1, "tilted", 50, "10", 210, "0.6", 220, "0", 230, "0.8", 100, "AcDbText")
            + G(0, "MTEXT", 5, "D7", 100, "AcDbEntity", 8, "0", 100, "AcDbMText", 10, "1", 20, "1", 30, "0", 40, "2", 41, "20", 71, "1", 1, "{\\fArial|b1;Bold}\\Pline", 11, "1", 21, "1", 31, "0")
            + G(0, "MTEXT", 5, "D8", 100, "AcDbEntity", 8, "0", 100, "AcDbMText", 10, "0", 20, "5", 30, "0", 40, "1.5", 71, "5", 1, "up", 50, "90")
            + G(0, "MTEXT", 5, "D9", 100, "AcDbEntity", 8, "0", 100, "AcDbMText", 10, "2", 20, "2", 30, "2", 40, "1", 1, "tilted m", 210, "0.6", 220, "0", 230, "0.8", 11, "0", 21, "1", 31, "0")
            + G(0, "ENDBLK", 8, "0")
            + G(0, "BLOCK", 8, "0", 2, "OUTER", 70, "0", 10, "0", 20, "0", 30, "0", 3, "OUTER")
            + G(0, "INSERT", 5, "E1", 8, "0", 2, "TXT", 10, "10", 20, "0", 30, "0", 50, "90")
            + G(0, "ENDBLK", 8, "0")
            + G(0, "ENDSEC")
            + G(0, "SECTION", 2, "ENTITIES")
            + G(0, "INSERT", 5, "C1", 8, "0", 2, "TXT", 10, "100", 20, "50", 30, "0", 41, "2", 42, "2", 43, "2", 50, "30")
            + G(0, "INSERT", 5, "C2", 8, "0", 2, "TXT", 10, "0", 20, "-100", 30, "0", 41, "3", 42, "1", 43, "1")
            + G(0, "INSERT", 5, "C3", 8, "0", 2, "TXT", 10, "0", 20, "200", 30, "0", 41, "1", 42, "2.5", 43, "1", 50, "45")
            + G(0, "INSERT", 5, "C4", 8, "0", 2, "TXT", 10, "-100", 20, "0", 30, "0", 41, "-1", 42, "1", 43, "1")
            + G(0, "INSERT", 5, "C5", 8, "0", 2, "TXT", 10, "10", 20, "10", 30, "10", 41, "1.5", 42, "1.5", 43, "1.5", 50, "60", 210, "0.3", 220, "-0.5", 230, "0.8")
            + G(0, "INSERT", 5, "C6", 8, "0", 2, "TXT", 10, "0", 20, "-300", 30, "0", 41, "-2", 42, "1", 43, "1", 50, "20")
            + G(0, "INSERT", 5, "C7", 8, "0", 2, "OUTER", 10, "0", 20, "500", 30, "0", 41, "0.5", 42, "0.5", 43, "0.5", 50, "45")
            + G(0, "INSERT", 5, "C8", 8, "0", 2, "OUTER", 10, "0", 20, "-500", 30, "0", 41, "1", 42, "2", 43, "1")
            + G(0, "INSERT", 5, "C9", 8, "0", 2, "TXT", 10, "300", 20, "0", 30, "0", 41, "1", 42, "1", 43, "-1")
            + G(0, "ENDSEC")
            + G(0, "EOF");

        // The texts of the drawing as ezdxf 1.4.5 places them, each INSERT's in turn, an INSERT inside a block placed by the
        // product of the two matrices ezdxf gives.
        private static readonly Ez[] Expected =
        {
            Ez.Text(P(97.26794919243113, 50.73205080756888, 0.0), P(0.0, 0.0, 1.0), 59.99999999999999, 4.999999999999999, 0.8000000000000003, 0.0),
            Ez.Text(P(101.19615242270663, 59.928203230275514, 0.0), P(0.0, 0.0, 1.0), 29.999999999999996, 6.0, 1.0, 0.0),
            Ez.Text(P(108.6602540378444, 55.0, 0.0), P(0.0, 0.0, 1.0), 119.99999999999999, 4.0, 1.0, 0.0),
            Ez.Text(P(88.33974596215562, 50.19615242270663, 0.0), P(0.0, 0.0, 1.0), 29.999999999999996, 3.0, 1.0, 0.0),
            Ez.Text(P(88.33974596215562, 50.19615242270663, 0.0), P(-0.0, 0.0, -1.0), 15.000000000000005, 3.9999999999999996, 1.0000000000000002, 0.0),
            Ez.Text(P(96.18948822334849, 47.80000000000001, 2.8000000000000043), P(0.5196152422706632, 0.29999999999999993, 0.8), 10.000000000000002, 4.0, 1.0, 0.0),
            Ez.MText(P(98.26794919243113, 49.0, 0.0), P(0.0, -0.0, 1.0), P(0.2588190451025208, 0.9659258262890682, 0.0), 3.9999999999999996, 39.99999999999999, 1.0),
            Ez.MText(P(92.53589838486225, 54.928203230275514, 0.0), P(0.0, -0.0, 1.0), P(-0.4999999999999999, 0.8660254037844387, 0.0), 2.9999999999999996, 0.0, 1.0),
            Ez.MText(P(99.0, 51.73205080756888, 4.0), P(0.5196152422706632, 0.29999999999999993, 0.8), P(-0.49999999999999994, 0.8660254037844387, 0.0), 2.0, 0.0, 1.0),
            Ez.Text(P(-3.0, -99.0, 0.0), P(0.0, 0.0, 1.0), 10.893394649130904, 2.834733547569204, 1.866666666666667, -49.106605350869074),
            Ez.Text(P(9.0, -96.0, 0.0), P(0.0, 0.0, 1.0), 0.0, 3.0, 3.0, 1.4210854715202004e-14),
            Ez.Text(P(15.0, -100.0, 0.0), P(0.0, 0.0, 1.0), 89.99999999999999, 6.0, 0.3333333333333333, 0.0),
            Ez.Text(P(-15.0, -97.0, 0.0), P(0.0, 0.0, 1.0), 0.0, 1.5, 3.0, 1.4210854715202004e-14),
            Ez.Text(P(-15.0, -97.0, 0.0), P(0.0, 0.0, -1.0), 18.43494882292201, 2.6832815729997463, 1.6666666666666676, -53.130102354155994),
            Ez.Text(P(-6.600000000000001, -100.0, 1.4000000000000004), P(0.24253562503633294, -0.0, 0.970142500145332), 23.567256221874274, 4.60500401943623, 0.4666332434850578, 19.490339285918125),
            Ez.MText(P(-3.0, -100.0, 0.0), P(0.0, 0.0, 1.0), P(0.9486832980505138, 0.31622776601683794, 0.0), 2.6832815729997472, 44.72135954999579, 0.6),
            Ez.MText(P(-6.0, -96.0, 0.0), P(0.0, 0.0, 1.0), P(1.8369701987210297e-16, 1.0, 0.0), 4.5, 0.0, 1.0),
            Ez.MText(P(0.0, -99.0, 2.0), P(0.24253562503633294, -0.0, 0.970142500145332), P(0.0, 1.0, 0.0), 2.4738633753705965, 0.0, 1.0),
            Ez.Text(P(-2.474873734152917, 201.06066017177983, 0.0), P(0.0, 0.0, 1.0), 100.28499604605177, 4.109974682633933, 0.7399999999999999, 42.28108410322898),
            Ez.Text(P(-4.949747468305833, 209.19238815542514, 0.0), P(0.0, 0.0, 1.0), 45.0, 7.5, 0.4, 0.0),
            Ez.Text(P(3.5355339059327378, 203.53553390593274, 0.0), P(0.0, 0.0, 1.0), 135.0, 2.0, 2.5, 360.0),
            Ez.Text(P(-8.838834764831844, 201.76776695296638, 0.0), P(0.0, 0.0, 1.0), 45.0, 3.75, 0.4, 0.0),
            Ez.Text(P(-8.838834764831844, 201.76776695296638, 0.0), P(-0.0, 0.0, -1.0), 23.198590513648185, 2.6261286571944518, 1.45, 46.39718102729637),
            Ez.Text(P(-1.555634918610366, 198.44436508138958, 1.3999999999999915), P(0.4242640687119284, 0.4242640687119284, 0.8), 4.034435683650859, 2.02582065845859, 2.436677251825204, -19.75428670774319),
            Ez.MText(P(-0.7071067811865477, 199.29289321881345, 0.0), P(0.0, -0.0, 1.0), P(-0.3939192985791677, 0.9191450300180579, 0.0), 2.6261286571944513, 38.07886552931954, 0.6896551724137931),
            Ez.MText(P(-8.485281374238571, 205.65685424949237, 0.0), P(0.0, -0.0, 1.0), P(-0.7071067811865476, 0.7071067811865476, 0.0), 1.5000000000000002, 0.0, 1.0),
            Ez.MText(P(-1.767766952966369, 201.76776695296638, 2.0), P(0.4242640687119284, 0.4242640687119284, 0.8), P(-0.7071067811865476, 0.7071067811865476, 0.0), 1.0, 0.0, 1.0),
            Ez.Text(P(-99.0, 1.0, 0.0), P(0.0, 0.0, -1.0), 29.999999999999996, 2.4999999999999996, 0.8000000000000003, 0.0),
            Ez.Text(P(-103.0, 4.0, 0.0), P(0.0, 0.0, -1.0), 0.0, 3.0, 1.0, 0.0),
            Ez.Text(P(-105.0, 0.0, 0.0), P(0.0, 0.0, -1.0), 90.0, 2.0, 1.0, 0.0),
            Ez.Text(P(-95.0, 3.0, 0.0), P(0.0, 0.0, -1.0), 0.0, 1.5, 1.0, 0.0),
            Ez.Text(P(-95.0, 3.0, 0.0), P(0.0, 0.0, 1.0), 45.0, 2.0, 1.0, 0.0),
            Ez.Text(P(-97.80000000000001, 0.0, 1.3999999999999915), P(0.6, 0.0, -0.8), 10.0, 1.9999999999999998, 1.0, 0.0),
            Ez.MText(P(-99.0, 0.0, 0.0), P(0.0, 0.0, -1.0), P(-0.7071067811865475, 0.7071067811865475, 0.0), 1.9999999999999998, 20.0, 1.0),
            Ez.MText(P(-98.0, 4.0, 0.0), P(0.0, 0.0, -1.0), P(-6.123233995736766e-17, 1.0, 0.0), 1.5, 0.0, 1.0),
            Ez.MText(P(-100.0, 1.0, 2.0), P(0.6, 0.0, -0.8), P(0.0, 1.0, 0.0), 1.0, 0.0, 1.0),
            Ez.Text(P(5.918874153104431, 5.589101852675104, 13.647979521272362), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), 90.0, 3.7500000000000004, 0.8, 0.0),
            Ez.Text(P(2.053678289341002, 10.287418150021225, 18.03387565602498), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), 60.00000000000001, 4.500000000000001, 1.0, 0.0),
            Ez.Text(P(7.962691628098672, 13.45404347120984, 17.79713647973373), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), 150.0, 3.0000000000000004, 1.0, 0.0),
            Ez.Text(P(2.6553324981047832, 0.1476335803148432, 11.470889971672065), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), 60.00000000000001, 2.2500000000000004, 1.0, 0.0),
            Ez.Text(P(2.6553324981047832, 0.1476335803148432, 11.470889971672065), P(-0.30304576336566325, 0.5050762722761054, -0.8081220356417687), -14.999999999999998, 3.0000000000000004, 1.0000000000000002, 0.0),
            Ez.Text(P(7.857403765676615, 3.133809831191729, 13.985090823991246), P(0.2836412754419724, 0.11035973041457618, 0.9525588468795372), 85.38576714542629, 3.0, 1.0, 0.0),
            Ez.MText(P(7.344621656857047, 5.737732247677932, 13.206218204241896), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), P(-0.6235436556740971, 0.5361856256359635, 0.5689448869002635), 3.0000000000000004, 30.000000000000004, 1.0),
            Ez.MText(P(1.5386199799729798, 3.857158797077971, 14.208110426448446), P(0.30304576336566325, -0.5050762722761054, 0.8081220356417687), P(-0.9504983358350768, -0.09908693000188484, 0.2945075446869757), 2.2500000000000004, 0.0, 1.0),
            Ez.MText(P(6.931023105075026, 5.35992490643544, 16.83749867411297), P(0.2836412754419723, 0.11035973041457615, 0.952558846879537), P(-0.9504983358350768, -0.0990869300018849, 0.29450754468697565), 1.5000000000000002, 0.0, 1.0),
            Ez.Text(P(1.537365098246148, -298.3762670925628, 0.0), P(-0.0, 0.0, -1.0), -3.897886248013986, 2.773500981126145, 1.3000000000000007, -33.004491598883064),
            Ez.Text(P(-7.006236298018126, -298.2933503768104, 0.0), P(-0.0, 0.0, -1.0), -20.0, 3.0, 2.0, 0.0),
            Ez.Text(P(-9.396926207859085, -303.4202014332567, 0.0), P(-0.0, 0.0, -1.0), 70.0, 4.0, 0.5, 0.0),
            Ez.Text(P(8.370865777882077, -293.7607207043856, 0.0), P(-0.0, 0.0, -1.0), -20.0, 1.5, 2.0, 0.0),
            Ez.Text(P(8.370865777882077, -293.7607207043856, 0.0), P(0.0, 0.0, 1.0), 46.56505117707799, 2.529822128134704, 1.25, -36.86989764584402),
            Ez.Text(P(4.134647531457963, -298.495111369367, 1.3999999999999986), P(0.32994810704556354, 0.12009128981706377, -0.9363291775690445), 16.76795741235565, 3.32276878465651, 0.6190862712491777, 10.876596415609626),
            Ez.MText(P(1.8793852415718164, -299.31595971334866, 0.0), P(-0.0, 0.0, -1.0), P(-0.9934426892429725, 0.11433119954627813, 0.0), 2.529822128134702, 31.622776601683793, 0.7999999999999998),
            Ez.MText(P(2.3906899098409586, -294.8731489435537, 0.0), P(-0.0, 0.0, -1.0), P(-0.3420201433256688, 0.9396926207859084, 0.0), 3.0, 0.0, 1.0),
            Ez.MText(P(-0.3420201433256693, -299.0603073792141, 2.0), P(0.32994810704556354, 0.12009128981706377, -0.9363291775690445), P(-0.3420201433256687, 0.9396926207859084, 0.0), 1.7088007490635064, 0.0, 1.0),
            Ez.Text(P(3.535533905932738, 502.8284271247462, 0.0), P(0.0, 0.0, 1.0), 165.0, 1.25, 0.8, 0.0),
            Ez.Text(P(1.0606601717798219, 503.1819805153395, 0.0), P(0.0, 0.0, 1.0), 135.0, 1.5, 1.0, 0.0),
            Ez.Text(P(1.7677669529663693, 505.30330085889915, 0.0), P(0.0, 0.0, 1.0), -135.0, 1.0, 1.0, 0.0),
            Ez.Text(P(4.242640687119286, 500.70710678118655, 0.0), P(0.0, 0.0, 1.0), 135.0, 0.75, 1.0, 0.0),
            Ez.Text(P(4.242640687119286, 500.70710678118655, 0.0), P(0.0, -0.0, -1.0), -90.0, 1.0, 1.0000000000000002, 0.0),
            Ez.Text(P(4.313351365237992, 502.75771644662757, 0.7000000000000455), P(-0.4242640687119285, 0.4242640687119285, 0.8000000000000002), 9.999999999999995, 1.0, 1.0, 0.0),
            Ez.MText(P(3.889087296526012, 503.1819805153395, 0.0), P(0.0, 0.0, 1.0), P(-1.0, 0.0, 0.0), 1.0, 10.0, 1.0),
            Ez.MText(P(2.8284271247461907, 501.4142135623731, 0.0), P(0.0, 0.0, 1.0), P(-0.7071067811865476, -0.7071067811865476, 0.0), 0.7500000000000001, 0.0, 1.0),
            Ez.MText(P(3.1819805153394647, 503.1819805153395, 1.0), P(-0.4242640687119285, 0.4242640687119285, 0.8000000000000002), P(-0.7071067811865476, -0.7071067811865476, 0.0), 0.5, 0.0, 1.0),
            Ez.Text(P(9.0, -502.0, 0.0), P(0.0, 0.0, 1.0), 106.10211375198601, 2.7735009811261446, 1.3000000000000007, 326.9955084011169),
            Ez.Text(P(6.0, -494.0, 0.0), P(0.0, 0.0, 1.0), 90.0, 3.0, 2.0, 0.0),
            Ez.Text(P(10.0, -490.0, 0.0), P(0.0, 0.0, 1.0), 180.0, 4.0, 0.5, 360.0),
            Ez.Text(P(7.0, -510.0, 0.0), P(0.0, 0.0, 1.0), 90.0, 1.5, 2.0, 0.0),
            Ez.Text(P(7.0, -510.0, 0.0), P(-0.0, 0.0, -1.0), -63.43494882292201, 2.5298221281347035, 1.25, -36.86989764584402),
            Ez.Text(P(9.999999999999996, -504.4, 1.3999999999999204), P(4.3000219884682655e-17, 0.3511234415883917, 0.9363291775690447), 16.767957412355656, 3.322768784656509, 0.6190862712491778, 10.876596415609626),
            Ez.MText(P(10.0, -502.0, 0.0), P(0.0, -0.0, 1.0), P(-0.44721359549995787, 0.8944271909999159, 0.0), 2.529822128134704, 31.622776601683793, 0.8000000000000002),
            Ez.MText(P(6.0, -504.0, 0.0), P(0.0, -0.0, 1.0), P(-1.0, 2.4492935982947064e-16, 0.0), 3.0, 0.0, 1.0),
            Ez.MText(P(9.0, -500.0, 2.0), P(4.300021988468264e-17, 0.35112344158839165, 0.9363291775690444), P(-1.0, 1.2246467991473532e-16, 0.0), 1.7088007490635064, 0.0, 1.0),
            Ez.Text(P(299.0, 1.0, 0.0), P(0.0, 0.0, 1.0), 29.999999999999996, 2.4999999999999996, 0.8000000000000003, 0.0),
            Ez.Text(P(303.0, 4.0, 0.0), P(0.0, 0.0, 1.0), 0.0, 3.0, 1.0, 0.0),
            Ez.Text(P(305.0, 0.0, 0.0), P(0.0, 0.0, 1.0), 90.0, 2.0, 1.0, 0.0),
            Ez.Text(P(295.0, 3.0, 0.0), P(0.0, 0.0, 1.0), 0.0, 1.5, 1.0, 0.0),
            Ez.Text(P(295.0, 3.0, 0.0), P(0.0, 0.0, -1.0), 45.0, 2.0, 1.0, 0.0),
            Ez.Text(P(297.8, 0.0, -1.4000000000000057), P(-0.6, 0.0, 0.8), -170.0, 1.9999999999999998, 1.0, 0.0),
            Ez.MText(P(299.0, 0.0, 0.0), P(0.0, 0.0, 1.0), P(0.7071067811865475, 0.7071067811865475, 0.0), 1.9999999999999998, 20.0, 1.0),
            Ez.MText(P(298.0, 4.0, 0.0), P(0.0, 0.0, 1.0), P(6.123233995736766e-17, 1.0, 0.0), 1.5, 0.0, 1.0),
            Ez.MText(P(300.0, 1.0, -2.0), P(-0.6, 0.0, 0.8), P(0.0, 1.0, 0.0), 1.0, 0.0, 1.0)
        };

        private static DxfDocument Read(bool planAs2D) => DxfReader.Parse(Fixture, new DxfReadOptions { PlanAs2D = planAs2D, Tolerance = Tol });

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EveryTextTheInsertsPlace_StandsWhereEzdxfPlacesIt_FacingAndTurnedAsItIs(bool planAs2D)
        {
            DxfDocument read = Read(planAs2D);

            Assert.Equal(Expected.Length, read.Shapes.Count);
            Assert.Empty(read.Skipped);

            for (int i = 0; i < Expected.Length; i++)
            {
                DxfShape entity = read.Shapes[i];
                Expected[i].Check(Assert.IsType<DxfText>(entity.Shape), "text " + i + ", the " + entity.Type + " " + entity.Handle + " placed by " + string.Join(" > ", entity.InsertHandles));
            }
        }

        [Fact]
        public void TheWordsOfATextOfABlock_AreTheSameWhereverItIsPlaced()
        {
            DxfDocument read = Read(false);

            Assert.All(read.Shapes.Where(e => e.Handle == "D1"), e => Assert.Equal("⌀20 at 45°", ((DxfText)e.Shape).Value));
            Assert.All(read.Shapes.Where(e => e.Handle == "D7"), e => Assert.Equal("Bold\nline", ((DxfText)e.Shape).Value));
            Assert.Equal(9, read.Shapes.Count(e => e.Handle == "D1"));
        }

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static void Same(GeoPoint3 expected, GeoPoint3 actual, string what)
            => Assert.True(expected.DistanceTo(actual) <= Near, what + ": " + actual + " where ezdxf has " + expected);

        private static void Same(GeoPoint3 expected, GeoVector3 actual, string what)
            => Assert.True(Math.Abs(expected.X - actual.X) <= 1E-12 && Math.Abs(expected.Y - actual.Y) <= 1E-12 && Math.Abs(expected.Z - actual.Z) <= 1E-12, what + ": " + actual + " where ezdxf has " + expected);

        private static void Close(double expected, double actual, string what)
            => Assert.True(Math.Abs(expected - actual) <= Near * Math.Max(1.0, Math.Abs(expected)), what + ": " + actual.ToString("R") + " where ezdxf has " + expected.ToString("R"));

        /// <summary>The direction a text reads along: its rotation from the x axis of its normal's own system.</summary>
        private static GeoVector3 Reading(DxfText text)
        {
            GeoVector3 n = text.Normal;
            bool nearZ = Math.Abs(n.X) < 1.0 / 64.0 && Math.Abs(n.Y) < 1.0 / 64.0;
            GeoVector3 a = nearZ ? new GeoVector3(n.Z, 0.0, -n.X) : new GeoVector3(-n.Y, n.X, 0.0);
            GeoVector3 u = a.Multiply(1.0 / a.Length);
            GeoVector3 b = n.CrossProduct(u);
            GeoVector3 v = b.Multiply(1.0 / b.Length);
            return u.Multiply(Math.Cos(text.Rotation)).Add(v.Multiply(Math.Sin(text.Rotation)));
        }

        private abstract class Ez
        {
            internal abstract void Check(DxfText text, string what);

            internal static Ez Text(GeoPoint3 at, GeoPoint3 extrusion, double rotationDegrees, double height, double widthFactor, double obliqueDegrees)
                => new EzText { At = at, Extrusion = extrusion, Rotation = rotationDegrees, Height = height, WidthFactor = widthFactor, Oblique = obliqueDegrees };

            internal static Ez MText(GeoPoint3 at, GeoPoint3 extrusion, GeoPoint3 direction, double height, double width, double sine)
                => new EzMText { At = at, Extrusion = extrusion, Direction = direction, Height = height, Width = width, Sine = sine };
        }

        private sealed class EzText : Ez
        {
            internal GeoPoint3 At;
            internal GeoPoint3 Extrusion;
            internal double Rotation;
            internal double Height;
            internal double WidthFactor;
            internal double Oblique;

            internal override void Check(DxfText text, string what)
            {
                Assert.False(text.IsMultiline, what);
                Same(At, text.Position, what + ", position");
                Same(Extrusion, text.Normal, what + ", normal");

                double turn = (text.Rotation - (Rotation * Math.PI / 180.0)) % (Math.PI * 2.0);
                turn = Math.Min(Math.Abs(turn), (Math.PI * 2.0) - Math.Abs(turn));
                Assert.True(turn <= Near, what + ", rotation " + text.Rotation.ToString("R") + " where ezdxf has " + Rotation.ToString("R") + " degrees");

                double lean = Math.Cos(Oblique * Math.PI / 180.0);
                Close(Height, text.Height * lean, what + ", height");
                Close(WidthFactor, text.WidthFactor / lean, what + ", width factor");
            }
        }

        private sealed class EzMText : Ez
        {
            internal GeoPoint3 At;
            internal GeoPoint3 Extrusion;
            internal GeoPoint3 Direction;
            internal double Height;
            internal double Width;
            internal double Sine;

            internal override void Check(DxfText text, string what)
            {
                Assert.True(text.IsMultiline, what);
                Same(At, text.Position, what + ", position");
                Same(Extrusion, text.Normal, what + ", normal");
                Same(Direction, Reading(text), what + ", direction");
                Close(Height, text.Height * Sine, what + ", height");
                Close(Width, text.Width, what + ", width");
                Assert.Equal(1.0, text.WidthFactor);
            }
        }
    }
}
