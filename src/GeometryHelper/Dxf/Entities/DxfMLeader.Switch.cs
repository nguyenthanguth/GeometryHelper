using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        #region Turned to the other side by MLEADERALIGN

        /// <summary>
        /// Turns the one leader of the MULTILEADER to meet its content on the other side, as AutoCAD 2021's MLEADERALIGN does when
        /// an arrowhead comes to stand past the middle of the box of the content (probed in its console, groups of the context
        /// data before and after): the words or the block stay where they are. Words held at their top left (171 of 1) for a
        /// leader on their left are held at their top right (3) for one on their right at the right end of their ink, which is
        /// <paramref name="width"/> from where they were held, the content lined up as they are held (176 of 2), the dogleg
        /// against the plane's x axis from a landing the landing gap and the dogleg past the ink, as far below the top of the
        /// words as the right attachment says (<see cref="Below(Ctx, int, double)"/>); the other way round for a leader turned to
        /// the left. The base point of the content (10 of the context data) is at the left of the box, short of the ink by the
        /// landing gap, as far below the top as the left attachment says; a block's stays, its leader landed where the line
        /// along x through the middle of its box meets it (<see cref="LandAnew"/>). The leader takes the next index (90): a leader
        /// of index 0 on the left of a block 10 wide at (20.36, 57, 0), its landing at (18.36, 60, 0) by a dogleg of 2, is
        /// index 1 with its landing at (32.36, 60, 0) and its dogleg against x.
        /// </summary>
        /// <param name="onRight">Whether the leader is to meet the content on its right.</param>
        /// <param name="width">The width of the words' ink, from where they start, as AutoCAD's ActualWidth.</param>
        /// <param name="lines">The indexes of the leader's lines whose arrowheads are on the other side, or null for all of them.
        /// Where they are some of the lines, as AutoCAD 2021 does when the arrowheads of a leader stand on both sides of the
        /// content, they go to a new leader on that side (the next index, landed as the turned leader would be) and the words
        /// are held as they were, the leader keeping the others (arrowheads at -30 and 40 with the middle of the box at 17: the
        /// line of -30 stays on the leader on the left, the line of 40 goes to a new leader on the right).</param>
        /// <returns>False, and nothing changed, where the MULTILEADER has not one leader that meets its content on the other
        /// side along the plane's x axis, words held at their top left or top right, or a block.</returns>
        internal bool SwitchSide(bool onRight, double width, ICollection<int> lines = null)
        {
            Rec rec = Read();
            Ctx ctx = Context(rec);

            if (ctx.Leaders.Count != 1 || Reversed(ctx))
            {
                return false;
            }

            LeaderPart leader = ctx.Leaders[0];
            Axes(ctx, out GeoVector3 x, out GeoVector3 y);
            int side = Side(leader, x);

            if (side == 0 || (side > 0) == onRight)
            {
                return false;
            }

            GeoVector3 dogleg = onRight ? x.Negate() : x;
            int index = leader.Index + 1;
            bool split = lines != null && lines.Count < leader.Lines.Count;

            if (lines != null && lines.Count == 0)
            {
                return false;
            }

            if (HasSection(ctx, 296, BlockCodes))
            {
                if (split)
                {
                    return false;
                }

                PutPoint(leader.Head, 11, Pt(dogleg), LeaderOrder);
                LandAnew(rec, ctx, new List<LeaderPart> { leader });
                Put(leader.Head, 90, index.ToString(CultureInfo.InvariantCulture), LeaderOrder);
                Write(rec);
                return true;
            }

            Section(ctx.Head, 290, MTextCodes, out int from, out int to);

            if (from < 0)
            {
                return false;
            }

            List<KeyValuePair<int, string>> m = ctx.Head.GetRange(from, to - from);
            int held = Whole(Text(m, 171), 1);
            GeoVector3 along = Unit(PointOf(m, 13, new GeoPoint3(1.0, 0.0, 0.0)).ToVector());

            if (held != (onRight ? 1 : 3) || along.Subtract(x).Length > 1E-9 || Whole(Text(rec.After, 271), 0) != 0)
            {
                return false;
            }

            double height = Height(ctx);
            double belowLeft = Below(ctx, AttachmentOf(rec, true), height);
            double belowRight = Below(ctx, AttachmentOf(rec, false), height);

            if (double.IsNaN(belowLeft) || double.IsNaN(belowRight))
            {
                return false;
            }

            GeoPoint3 was = PointOf(m, 12, new GeoPoint3(0.0, 0.0, 0.0));
            GeoPoint3 place = was.Add(x.Multiply(onRight ? width : -width));
            double gap = Num(Text(ctx.Head, 145), 2.0);
            double length = Num(Text(leader.Head, 40), 0.0);
            GeoPoint3 inkLeft = onRight ? was : place;
            GeoPoint3 landing = onRight
                ? place.Add(x.Multiply(gap + length)).Add(y.Multiply(-belowRight))
                : place.Add(x.Multiply(-(gap + length))).Add(y.Multiply(-belowLeft));

            if (split)
            {
                // The words stay as they are held; some lines go to a new leader on the other side.
                AddLeaderAt(rec, ctx, landing, dogleg);
                LeaderPart added = ctx.Leaders[ctx.Leaders.Count - 1];

                foreach (LinePart line in leader.Lines.FindAll(l => lines.Contains(l.Index)))
                {
                    leader.Lines.Remove(line);
                    added.Lines.Add(line);
                }

                Write(rec);
                return true;
            }

            Put(ctx.Head, 176, (onRight ? 2 : 0).ToString(CultureInfo.InvariantCulture), ContextOrder);
            PutPoint(ctx.Head, 10, inkLeft.Add(x.Multiply(-gap)).Add(y.Multiply(-belowLeft)), ContextOrder);
            Section(ctx.Head, 290, MTextCodes, out from, out to);
            PutPoint(ctx.Head, 12, place, ContextOrder, from, to);
            Section(ctx.Head, 290, MTextCodes, out from, out to);
            Put(ctx.Head, 171, (onRight ? 3 : 1).ToString(CultureInfo.InvariantCulture), ContextOrder, from, to);
            PutPoint(leader.Head, 10, landing, LeaderOrder);
            PutPoint(leader.Head, 11, Pt(dogleg), LeaderOrder);
            Put(leader.Head, 90, index.ToString(CultureInfo.InvariantCulture), LeaderOrder);
            Write(rec);
            return true;
        }

        #endregion
    }
}
