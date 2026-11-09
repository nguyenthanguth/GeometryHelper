using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The presenter's own single-stroke font, GeometryHelper Simplex (<see cref="CadFonts.BuiltInName"/>): the Roman Simplex
    /// letters of the Hershey fonts, ASCII 32 to 126 and the degree sign, with a plus-minus and a diameter sign drawn here,
    /// made in code into the bytes of a compiled SHX unifont so that it is read and drawn as any SHX font is. Its capitals reach
    /// 21 units above the baseline and its descenders 7 below, as romans.shx's: a text 2.5 high draws its capitals 2.5 high.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The letters are those of the file rowmans.jhf of the hershey-fonts collection (github.com/kamalmostafa/hershey-fonts),
    /// whose terms let anyone use the Hershey fonts for any purpose, commercial or otherwise, carrying these acknowledgements
    /// with the font data:
    /// </para>
    /// <list type="bullet">
    /// <item>The Hershey Fonts were originally created by Dr. A. V. Hershey while working at the U. S. National Bureau of
    /// Standards.</item>
    /// <item>The format of the Font data in this distribution was originally created by James Hurt, Cognition, Inc., 900
    /// Technology Park Drive, Billerica, MA 01821 (mit-eddie!ci-dandelion!hurt).</item>
    /// </list>
    /// <para>
    /// The data is converted here into another format, as the terms allow (any format but the one distributed by the U.S.
    /// NTIS). Each letter is written as Hurt's file writes it: a pair of characters for its left and right ends and then a
    /// pair for each point, every value its character less 'R', y down, the baseline at 9 and the capitals' top at -12, and
    /// " R" lifting the pen.
    /// </para>
    /// </remarks>
    internal static class HersheySimplex
    {
        // ASCII 32 to 126 in order, then the degree sign (Hershey 718), from rowmans.jhf as it is.
        private static readonly string[] Ascii =
        {
            "JZ", // space
            "MWRFRT RRYQZR[SZRY", // !
            "JZNFNM RVFVM", // "
            "H]SBLb RYBRb RLOZO RKUYU", // #
            "H\\PBP_ RTBT_ RYIWGTFPFMGKIKKLMMNOOUQWRXSYUYXWZT[P[MZKX", // $
            "F^[FI[ RNFPHPJOLMMKMIKIIJGLFNFPGSHVHYG[F RWTUUTWTYV[X[ZZ[X[VYTWT", // %
            "E_\\O\\N[MZMYNXPVUTXRZP[L[JZIYHWHUISJRQNRMSKSIRGPFNGMIMKNNPQUXWZY[[[\\Z\\Y", // &
            "MWRHQGRFSGSIRKQL", // '
            "KYVBTDRGPKOPOTPYR]T`Vb", // (
            "KYNBPDRGTKUPUTTYR]P`Nb", // )
            "JZRFRR RMIWO RWIMO", // *
            "E_RIR[ RIR[R", // +
            "MWSZR[QZRYSZS\\R^Q_", // ,
            "E_IR[R", // -
            "MWRYQZR[SZRY", // .
            "G][BIb", // /
            "H\\QFNGLJKOKRLWNZQ[S[VZXWYRYOXJVGSFQF", // 0
            "H\\NJPISFS[", // 1
            "H\\LKLJMHNGPFTFVGWHXJXLWNUQK[Y[", // 2
            "H\\MFXFRNUNWOXPYSYUXXVZS[P[MZLYKW", // 3
            "H\\UFKTZT RUFU[", // 4
            "H\\WFMFLOMNPMSMVNXPYSYUXXVZS[P[MZLYKW", // 5
            "H\\XIWGTFRFOGMJLOLTMXOZR[S[VZXXYUYTXQVOSNRNOOMQLT", // 6
            "H\\YFO[ RKFYF", // 7
            "H\\PFMGLILKMMONSOVPXRYTYWXYWZT[P[MZLYKWKTLRNPQOUNWMXKXIWGTFPF", // 8
            "H\\XMWPURRSQSNRLPKMKLLINGQFRFUGWIXMXRWWUZR[P[MZLX", // 9
            "MWRMQNROSNRM RRYQZR[SZRY", // :
            "MWRMQNROSNRM RSZR[QZRYSZS\\R^Q_", // ;
            "F^ZIJRZ[", // <
            "E_IO[O RIU[U", // =
            "F^JIZRJ[", // >
            "I[LKLJMHNGPFTFVGWHXJXLWNVORQRT RRYQZR[SZRY", // ?
            "E`WNVLTKQKOLNMMPMSNUPVSVUUVS RQKOMNPNSOUPV RWKVSVUXVZV\\T]Q]O\\L[JYHWGTFQFNGLHJJILHOHRIUJWLYNZQ[T[WZYYZX RXKWSWUXV", // @
            "I[RFJ[ RRFZ[ RMTWT", // A
            "G\\KFK[ RKFTFWGXHYJYLXNWOTP RKPTPWQXRYTYWXYWZT[K[", // B
            "H]ZKYIWGUFQFOGMILKKNKSLVMXOZQ[U[WZYXZV", // C
            "G\\KFK[ RKFRFUGWIXKYNYSXVWXUZR[K[", // D
            "H[LFL[ RLFYF RLPTP RL[Y[", // E
            "HZLFL[ RLFYF RLPTP", // F
            "H]ZKYIWGUFQFOGMILKKNKSLVMXOZQ[U[WZYXZVZS RUSZS", // G
            "G]KFK[ RYFY[ RKPYP", // H
            "NVRFR[", // I
            "JZVFVVUYTZR[P[NZMYLVLT", // J
            "G\\KFK[ RYFKT RPOY[", // K
            "HYLFL[ RL[X[", // L
            "F^JFJ[ RJFR[ RZFR[ RZFZ[", // M
            "G]KFK[ RKFY[ RYFY[", // N
            "G]PFNGLIKKJNJSKVLXNZP[T[VZXXYVZSZNYKXIVGTFPF", // O
            "G\\KFK[ RKFTFWGXHYJYMXOWPTQKQ", // P
            "G]PFNGLIKKJNJSKVLXNZP[T[VZXXYVZSZNYKXIVGTFPF RSWY]", // Q
            "G\\KFK[ RKFTFWGXHYJYLXNWOTPKP RRPY[", // R
            "H\\YIWGTFPFMGKIKKLMMNOOUQWRXSYUYXWZT[P[MZKX", // S
            "JZRFR[ RKFYF", // T
            "G]KFKULXNZQ[S[VZXXYUYF", // U
            "I[JFR[ RZFR[", // V
            "F^HFM[ RRFM[ RRFW[ R\\FW[", // W
            "H\\KFY[ RYFK[", // X
            "I[JFRPR[ RZFRP", // Y
            "H\\YFK[ RKFYF RK[Y[", // Z
            "KYOBOb RPBPb ROBVB RObVb", // [
            "KYKFY^", // \
            "KYTBTb RUBUb RNBUB RNbUb", // ]
            "JZPLRITL RMORJWO RRJR[", // ^
            "JZJ]Z]", // _
            "MWSFRGQIQKRLSKRJ", // `
            "I\\XMX[ RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", // a
            "H[LFL[ RLPNNPMSMUNWPXSXUWXUZS[P[NZLX", // b
            "I[XPVNTMQMONMPLSLUMXOZQ[T[VZXX", // c
            "I\\XFX[ RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", // d
            "I[LSXSXQWOVNTMQMONMPLSLUMXOZQ[T[VZXX", // e
            "MYWFUFSGRJR[ ROMVM", // f
            "I\\XMX]W`VaTbQbOa RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", // g
            "I\\MFM[ RMQPNRMUMWNXQX[", // h
            "NVQFRGSFREQF RRMR[", // i
            "MWRFSGTFSERF RSMS^RaPbNb", // j
            "IZMFM[ RWMMW RQSX[", // k
            "NVRFR[", // l
            "CaGMG[ RGQJNLMOMQNRQR[ RRQUNWMZM\\N]Q][", // m
            "I\\MMM[ RMQPNRMUMWNXQX[", // n
            "I\\QMONMPLSLUMXOZQ[T[VZXXYUYSXPVNTMQM", // o
            "H[LMLb RLPNNPMSMUNWPXSXUWXUZS[P[NZLX", // p
            "I\\XMXb RXPVNTMQMONMPLSLUMXOZQ[T[VZXX", // q
            "KXOMO[ ROSPPRNTMWM", // r
            "J[XPWNTMQMNNMPNRPSUTWUXWXXWZT[Q[NZMX", // s
            "MYRFRWSZU[W[ ROMVM", // t
            "I\\MMMWNZP[S[UZXW RXMX[", // u
            "JZLMR[ RXMR[", // v
            "G]JMN[ RRMN[ RRMV[ RZMV[", // w
            "J[MMX[ RXMM[", // x
            "JZLMR[ RXMR[P_NaLbKb", // y
            "J[XMM[ RMMXM RM[X[", // z
            "KYTBRCQDPFPHQJRKSMSOQQ RRCQEQGRISJTLTNSPORSTTVTXSZR[Q]Q_Ra RQSSUSWRYQZP\\P^Q`RaTb", // {
            "NVRBRb", // |
            "KYPBRCSDTFTHSJRKQMQOSQ RRCSESGRIQJPLPNQPURQTPVPXQZR[S]S_Ra RSSQUQWRYSZT\\T^S`RaPb", // }
            "F^IUISJPLONOPPTSVTXTZS[Q RISJQLPNPPQTTVUXUZT[Q[O", // ~
            "KYQFOGNINKOMQNSNUMVKVIUGSFQF", // degree sign, Hershey 718
        };

        // Drawn here, as rowmans.jhf has neither: the plus-minus sign as the plus raised over a bar on the baseline, and the
        // diameter sign as a circle of radius 7 about the capitals' middle crossed by a stroke from the foot of its left to
        // above its right.
        private const string PlusMinus = "E_RIRW RIP[P RI[[[";
        private const string Diameter = "G]YPXSWUUVRWOVMULSKPLMMKOJRIUJWKXMYP RIY[G";

        /// <summary>The bytes of the font as a compiled SHX unifont 21 above and 7 below, its letters by their Unicode numbers.</summary>
        internal static byte[] Unifont()
        {
            var letters = new SortedDictionary<int, string>();

            for (int i = 0; i < Ascii.Length - 1; i++)
            {
                letters.Add(32 + i, Ascii[i]);
            }

            letters.Add(0xB0, Ascii[Ascii.Length - 1]);
            letters.Add(0xB1, PlusMinus);
            letters.Add(0x2205, Diameter);
            var bytes = new List<byte>(Encoding.ASCII.GetBytes("AutoCAD-86 unifont 1.0\r\n\x1A"));
            byte[] info = Encoding.ASCII.GetBytes("GeometryHelper Simplex\0").Concat(new byte[] { 21, 7, 0, 0 }).ToArray();
            Word(bytes, letters.Count + 1);
            Word(bytes, 0);
            Word(bytes, info.Length);
            bytes.AddRange(info);

            foreach (KeyValuePair<int, string> letter in letters)
            {
                byte[] record = Codes(letter.Value);
                Word(bytes, letter.Key);
                Word(bytes, record.Length);
                bytes.AddRange(record);
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// The record of a letter: an empty name, then for each stroke the pen lifted and moved to its first point and put down
        /// along the rest, each a move by signed bytes (code 8), and last the pen lifted and moved to the letter's right end on
        /// the baseline.
        /// </summary>
        private static byte[] Codes(string letter)
        {
            int left = letter[0] - 'R';
            int right = letter[1] - 'R';
            var codes = new List<byte> { 0 };
            int x = 0;
            int y = 0;
            bool lifted = true;

            for (int i = 2; i + 1 < letter.Length; i += 2)
            {
                if (letter[i] == ' ' && letter[i + 1] == 'R')
                {
                    lifted = true;
                    continue;
                }

                int px = letter[i] - 'R' - left;
                int py = 9 - (letter[i + 1] - 'R');

                if (lifted)
                {
                    codes.Add(2);
                }

                codes.Add(8);
                codes.Add((byte)(sbyte)(px - x));
                codes.Add((byte)(sbyte)(py - y));

                if (lifted)
                {
                    codes.Add(1);
                    lifted = false;
                }

                x = px;
                y = py;
            }

            codes.Add(2);
            codes.Add(8);
            codes.Add((byte)(sbyte)(right - left - x));
            codes.Add((byte)(sbyte)(-y));
            codes.Add(0);
            return codes.ToArray();
        }

        private static void Word(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value & 0xFF));
            bytes.Add((byte)(value >> 8));
        }
    }
}
