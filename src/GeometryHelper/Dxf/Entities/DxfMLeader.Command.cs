using System.Globalization;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        /// <summary>
        /// Gives a multileader the groups AutoCAD 2021's MLEADER command writes where the library's template (ezdxf's) writes
        /// others, for the presenter's MLEADER of words (probed in AutoCAD's console, the style Annotative at 1:50 and Standard):
        /// the flow of the words by their style (172 of 5 in the context data), a background colour of -1073741824 (91), a
        /// background scale factor of 0 (141) and no automatic height of their columns (295 of 0, and the 295 of 0 that ends the
        /// record); the content type and the alignment as the command gives them (bits 10 and 14 of the overrides), the colour of
        /// the words the style's (92 and no bit 15). Of an <paramref name="annotative"/> one the
        /// scale shows in the context alone (40 of 50 at 1:50): the record keeps the style's <paramref name="styleScale"/> (45) and
        /// its override of the scale (bit 24) is clear.
        /// </summary>
        internal void AsTheCommandMakesIt(double styleScale, bool annotative)
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);
            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from >= 0)
            {
                Put(ctx.Head, 172, "5", ContextOrder, from, to);
                Put(ctx.Head, 91, "-1073741824", ContextOrder, from, to);
                PutNumber(ctx.Head, 141, 0.0, 1.5, ContextOrder, from, to);
                Put(ctx.Head, 295, "0", ContextOrder, from, to);
                SetBit(rec, 10);
                SetBit(rec, 14);

                // The words' colour is the style's, not an override of it (bit 15 clear, 92 the style's 93).
                string colour = StyleOf(rec, out _)?.Text(93)?.Trim();

                if (!string.IsNullOrEmpty(colour))
                {
                    Put(rec.After, 92, colour, TopOrder);
                }

                ClearBits(rec, 1 << 15);
            }

            if (HasSection(ctx, 290, MTextCodes))
            {
                Put(rec.After, 295, "0", TopOrder);
            }

            if (annotative)
            {
                PutNumber(rec.After, 45, styleScale, 1.0, TopOrder);
                ClearBits(rec, 1 << 24);
            }

            Write(rec);
        }

        /// <summary>Clears bits of the overrides of the style (90).</summary>
        private static void ClearBits(Rec rec, int bits)
        {
            int flags = Whole(Text(rec.After, 90), 0);

            if ((flags & bits) != 0)
            {
                Put(rec.After, 90, (flags & ~bits).ToString(CultureInfo.InvariantCulture), TopOrder);
            }
        }
    }
}
