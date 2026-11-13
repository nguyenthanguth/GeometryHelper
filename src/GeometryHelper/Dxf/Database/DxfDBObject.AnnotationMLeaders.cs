using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        /// <summary>One leader of a multileader's context: where its dogleg starts (the landing), the direction it runs to the content, and its length.</summary>
        private sealed class ContextLeader
        {
            internal double[] Landing = new double[3];
            internal double[] Along = { 1.0, 0.0, 0.0 };
            internal double Dogleg = double.NaN;

            /// <summary>Where the dogleg ends, at the content.</summary>
            internal double[] End(double length)
            {
                var end = new double[3];

                for (int i = 0; i < 3; i++)
                {
                    end[i] = Landing[i] + Along[i] * length;
                }

                return end;
            }
        }

        /// <summary>
        /// The context of a multileader at another scale, laid out as AutoCAD 2021 lays it out (probed on multileaders of an
        /// annotative style, text 0.2 high at 1:50 and 1:100): the scale (40) is 1 over the new scale's, the sizes (41 the text
        /// height, 140 the arrowhead, 145 the landing gap, 43 and 44 the box, 142 and 143 the columns, and the dogleg's length 40
        /// inside each LEADER{) multiplied by <paramref name="factor"/>, the old scale's over the new one's; the leaders' lines
        /// stay where they are. The dogleg of a leader ends at the content (its landing moved along its direction by its
        /// length). A multileader of one leader keeps its landing: the content moves with the dogleg's end, its base (10) and
        /// its location (12 for text, 15 for a block) at the old offsets from the old end multiplied by the factor. A
        /// multileader of two leaders (one on each side of the text) keeps its base: the text and the ends of the doglegs are
        /// at the old offsets from it multiplied by the factor, and each landing is the end moved back by the new length. Text
        /// 0.2 high at 1:50 with the landing (100, 100) and a dogleg of 0.36 has at 1:100 the base (100.72, 100) and the text
        /// 9.72 right of it, 0.4 high; the same with a second leader on the other side keeps the base at (100.36, 100) and
        /// moves the first landing to (99.64, 100). A block content has its scale (16) and the diagonal of its matrix (47)
        /// multiplied by the factor, its translation as it was.
        /// </summary>
        internal static List<KeyValuePair<int, string>> MLeaderLaidOut(
            List<KeyValuePair<int, string>> inner,
            DxfAnnotationScale scale,
            double factor
        )
        {
            var leaders = new List<ContextLeader>();
            bool content = false;
            int depth = 0;
            var oldBase = new double[3];
            var oldText = new double[3];
            var oldPlace = new double[3];
            bool block = false;

            foreach (KeyValuePair<int, string> g in inner)
            {
                int code = g.Key;
                string value = g.Value.Trim();
                double number = double.NaN;
                Numbers.TryRead(value, out number);

                if (code == 302 && value == "LEADER{")
                {
                    leaders.Add(new ContextLeader());
                    depth = 1;
                }
                else if (code == 304 && depth >= 1 && value == "LEADER_LINE{")
                {
                    depth = 2;
                }
                else if (code == 305)
                {
                    depth = 1;
                }
                else if (code == 303)
                {
                    depth = 0;
                }
                else if (depth == 0 && code == 304)
                {
                    content = true;
                }
                else if (depth == 0 && code == 341)
                {
                    content = true;
                    block = true;
                }
                else if (depth == 0 && (code == 10 || code == 20 || code == 30))
                {
                    oldBase[(code - 10) / 10] = number;
                }
                else if (depth == 0 && (code == 12 || code == 22 || code == 32))
                {
                    oldText[(code - 12) / 10] = number;
                }
                else if (depth == 0 && (code == 15 || code == 25 || code == 35))
                {
                    oldPlace[(code - 15) / 10] = number;
                }
                else if (depth == 1 && (code == 10 || code == 20 || code == 30))
                {
                    leaders[leaders.Count - 1].Landing[(code - 10) / 10] = number;
                }
                else if (depth == 1 && (code == 11 || code == 21 || code == 31))
                {
                    leaders[leaders.Count - 1].Along[(code - 11) / 10] = number;
                }
                else if (depth == 1 && code == 40 && double.IsNaN(leaders[leaders.Count - 1].Dogleg))
                {
                    leaders[leaders.Count - 1].Dogleg = number;
                }
            }

            if (
                leaders.Count < 1
                || leaders.Count > 2
                || !content
                || leaders.Exists(l => !Guard.IsFinite(l.Dogleg) || !Guard.IsFinite(l.Landing[0]))
            )
            {
                throw new NotSupportedException(
                    "A multileader is shown at a second scale here only when it has one or two leaders and holds text or a block: AutoCAD lays others out again, which the library does not do."
                );
            }

            // One leader keeps its landing and the content follows the end of its dogleg; two keep the base and the landings move.
            // The content runs on from the base along the dogleg's direction (text to the right of a leader coming from the
            // left) or back from the end of the dogleg (text to the left): in the first the base is the end, in the second the
            // text stands at the end less its gap and the base at the old offset from it, as AutoCAD 2021 lays them out.
            ContextLeader first = leaders[0];
            double[] oldEnd = first.End(first.Dogleg);
            double[] newEnd = leaders.Count == 1 ? first.End(first.Dogleg * factor) : oldBase;
            double[] held = block ? oldPlace : oldText;
            double along = 0.0;

            for (int i = 0; i < 3; i++)
            {
                along += first.Along[i] * (held[i] - oldBase[i]);
            }

            var newBase = new double[3];
            var newText = new double[3];
            var newPlace = new double[3];

            for (int i = 0; i < 3; i++)
            {
                if (leaders.Count == 2 || along >= 0.0)
                {
                    newBase[i] = newEnd[i];
                    newText[i] = newEnd[i] + (oldText[i] - oldBase[i]) * factor;
                    newPlace[i] = newEnd[i] + (oldPlace[i] - oldBase[i]) * factor;
                }
                else
                {
                    newText[i] = newEnd[i] + (oldText[i] - oldEnd[i]) * factor;
                    newPlace[i] = newEnd[i] + (oldPlace[i] - oldEnd[i]) * factor;
                    newBase[i] = (block ? newPlace[i] : newText[i]) + (oldBase[i] - held[i]) * factor;
                }
            }

            var newLanding = new List<double[]>();

            foreach (ContextLeader leader in leaders)
            {
                var landing = new double[3];
                double[] end = leader.End(leader.Dogleg);

                for (int i = 0; i < 3; i++)
                {
                    landing[i] =
                        leaders.Count == 1
                            ? leader.Landing[i]
                            : oldBase[i] + (end[i] - oldBase[i]) * factor - leader.Along[i] * leader.Dogleg * factor;
                }

                newLanding.Add(landing);
            }

            var result = new List<KeyValuePair<int, string>>(inner.Count);
            depth = 0;
            int leaderAt = -1;
            bool scaleDone = false;
            bool doglegDone = false;
            int matrixAt = 0;

            foreach (KeyValuePair<int, string> g in inner)
            {
                int code = g.Key;
                string value = g.Value;
                string trimmed = value.Trim();

                if (code == 302 && trimmed == "LEADER{")
                {
                    depth = 1;
                    leaderAt++;
                    doglegDone = false;
                }
                else if (code == 304 && depth >= 1 && trimmed == "LEADER_LINE{")
                {
                    depth = 2;
                }
                else if (code == 305)
                {
                    depth = 1;
                }
                else if (code == 303)
                {
                    depth = 0;
                }
                else if (depth == 0 && code == 40 && !scaleDone)
                {
                    scaleDone = true;
                    value = Numbers.Exact(1.0 / scale.Scale + 0.0);
                }
                else if (
                    depth == 0
                    && (
                        code == 41
                        || code == 140
                        || code == 145
                        || code == 43
                        || code == 44
                        || code == 142
                        || code == 143
                    )
                )
                {
                    value = Scaled(value, factor);
                }
                else if (depth == 0 && (code == 10 || code == 20 || code == 30))
                {
                    value = Numbers.Exact(newBase[(code - 10) / 10] + 0.0);
                }
                else if (depth == 0 && (code == 12 || code == 22 || code == 32))
                {
                    value = Numbers.Exact(newText[(code - 12) / 10] + 0.0);
                }
                else if (depth == 0 && block && (code == 15 || code == 25 || code == 35))
                {
                    value = Numbers.Exact(newPlace[(code - 15) / 10] + 0.0);
                }
                else if (depth == 0 && block && (code == 16 || code == 26 || code == 36))
                {
                    value = Scaled(value, factor);
                }
                else if (depth == 0 && block && code == 47)
                {
                    // The block's matrix, row by row: its scales (the diagonal) follow the factor, its translation is left as it
                    // was, as AutoCAD 2021 writes it.
                    if (matrixAt % 5 == 0 && matrixAt < 15)
                    {
                        value = Scaled(value, factor);
                    }

                    matrixAt++;
                }
                else if (depth == 1 && (code == 10 || code == 20 || code == 30) && leaderAt >= 0)
                {
                    value = Numbers.Exact(newLanding[leaderAt][(code - 10) / 10] + 0.0);
                }
                else if (depth == 1 && code == 40 && !doglegDone)
                {
                    doglegDone = true;
                    value = Scaled(value, factor);
                }

                result.Add(new KeyValuePair<int, string>(code, value));
            }

            return result;
        }

        /// <summary>
        /// How an annotative multileader of the space is drawn at a scale: false where it is not shown there and ANNOALLVISIBLE
        /// is off; true otherwise, with <paramref name="context"/> the groups of its context data at that scale where it holds a
        /// context of it other than the one its record carries, else null (the record is drawn as it is).
        /// </summary>
        internal bool TryContextDataAt(
            DxfAnnotationScale at,
            bool allVisible,
            out List<KeyValuePair<int, string>> context
        )
        {
            context = null;

            if (
                !(this is DxfMLeader)
                || at == null
                || (_item.CopiedContexts == null && _item.File.ExtensionDictionaryOf(_item) == null)
                || Annotative != DxfAnnotativeStates.True
            )
            {
                return true;
            }

            foreach (ScaleContext held in ScaleContexts())
            {
                if (string.Equals(held.Scale, at.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    if (!held.IsDefault)
                    {
                        context = ContextData(FindContext(held.Key));
                    }

                    return true;
                }
            }

            return allVisible;
        }

        private static string Scaled(string value, double factor) =>
            Numbers.TryRead(value, out double number) ? Numbers.Exact(number * factor + 0.0) : value;
    }
}
