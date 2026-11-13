using System;
using System.Collections.Generic;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    public abstract partial class DxfDBObject
    {
        /// <summary>
        /// The context of a multileader at another scale, laid out as AutoCAD 2021 lays it out (probed on a multileader of an
        /// annotative style, text 0.2 high at 1:50 and 1:100): the scale (40) is 1 over the new scale's, the sizes (41 the text
        /// height, 140 the arrowhead, 145 the landing gap, 43 and 44 the box, 142 and 143 the columns, and the dogleg's length 40
        /// inside LEADER{) multiplied by <paramref name="factor"/>, the old scale's over the new one's; the landing and the
        /// leader's lines stay where they are, the base of the content (10) is the landing moved along the dogleg's direction
        /// by the new length, and the location of the text (12) is the new base moved by the old offset from the old base
        /// multiplied by the factor. A multileader of one leader holding text, whose context is at 1:50: the landing (100, 100)
        /// with a dogleg of 0.36, a gap of 4.5 and a text height of 0.2 has at 1:100 the base (100.72, 100) and the text 9.72 right
        /// of it, the text 0.4 high.
        /// </summary>
        internal static List<KeyValuePair<int, string>> MLeaderLaidOut(
            List<KeyValuePair<int, string>> inner,
            DxfAnnotationScale scale,
            double factor
        )
        {
            int leaders = 0;
            bool text = false;
            int depth = 0;
            GeoVector3 landing = default(GeoVector3);
            GeoVector3 along = new GeoVector3(1.0, 0.0, 0.0);
            double dogleg = 0.0;
            var oldBase = new double[3];
            var oldText = new double[3];
            bool gotLanding = false;
            bool gotAlong = false;
            bool gotDogleg = false;

            foreach (KeyValuePair<int, string> g in inner)
            {
                int code = g.Key;
                string value = g.Value.Trim();
                double number = double.NaN;
                Numbers.TryRead(value, out number);

                if (code == 302 && value == "LEADER{")
                {
                    leaders++;
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
                    text = true;
                }
                else if (depth == 0 && (code == 10 || code == 20 || code == 30))
                {
                    oldBase[(code - 10) / 10] = number;
                }
                else if (depth == 0 && (code == 12 || code == 22 || code == 32))
                {
                    oldText[(code - 12) / 10] = number;
                }
                else if (depth == 1 && (code == 10 || code == 20 || code == 30) && leaders == 1)
                {
                    landing = With(landing, (code - 10) / 10, number);
                    gotLanding = true;
                }
                else if (depth == 1 && (code == 11 || code == 21 || code == 31) && leaders == 1)
                {
                    along = With(gotAlong ? along : new GeoVector3(0.0, 0.0, 0.0), (code - 11) / 10, number);
                    gotAlong = true;
                }
                else if (depth == 1 && code == 40 && !gotDogleg)
                {
                    dogleg = number;
                    gotDogleg = true;
                }
            }

            if (leaders != 1 || !text || !gotLanding || !Guard.IsFinite(dogleg))
            {
                throw new NotSupportedException(
                    "A multileader is shown at a second scale here only when it has one leader and holds text: AutoCAD lays others out again, which the library does not do."
                );
            }

            double length = dogleg * factor;
            var newBase = new[]
            {
                landing.X + along.X * length,
                landing.Y + along.Y * length,
                landing.Z + along.Z * length,
            };
            var newText = new double[3];

            for (int i = 0; i < 3; i++)
            {
                newText[i] = newBase[i] + (oldText[i] - oldBase[i]) * factor;
            }

            var result = new List<KeyValuePair<int, string>>(inner.Count);
            depth = 0;
            bool scaleDone = false;
            bool doglegDone = false;

            foreach (KeyValuePair<int, string> g in inner)
            {
                int code = g.Key;
                string value = g.Value;
                string trimmed = value.Trim();

                if (code == 302 && trimmed == "LEADER{")
                {
                    depth = 1;
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
                else if (depth == 1 && code == 40 && !doglegDone)
                {
                    doglegDone = true;
                    value = Numbers.Exact(length + 0.0);
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

        private static GeoVector3 With(GeoVector3 v, int axis, double number) =>
            axis == 0 ? new GeoVector3(number, v.Y, v.Z)
            : axis == 1 ? new GeoVector3(v.X, number, v.Z)
            : new GeoVector3(v.X, v.Y, number);

        private static string Scaled(string value, double factor) =>
            Numbers.TryRead(value, out double number) ? Numbers.Exact(number * factor + 0.0) : value;
    }
}
