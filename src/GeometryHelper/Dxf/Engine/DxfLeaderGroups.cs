using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The groups of the own part of a HATCH, a LEADER or a MULTILEADER as a run of codes and texts: read as the reader reads a
    /// number, written as <see cref="DxfWriter"/> writes one, every group the change is not about keeping its text.
    /// </summary>
    internal static class DxfLeaderGroups
    {
        /// <summary>A group of a code and a text.</summary>
        internal static KeyValuePair<int, string> G(int code, string value) => new KeyValuePair<int, string>(code, value);

        /// <summary>A group of a code and a number, written as the writer writes one.</summary>
        internal static KeyValuePair<int, string> G(int code, double value) => new KeyValuePair<int, string>(code, Real(value));

        /// <summary>A group of a code and a whole number.</summary>
        internal static KeyValuePair<int, string> G(int code, int value) => new KeyValuePair<int, string>(code, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>A number as the writer writes it, a negative nought as nought.</summary>
        internal static string Real(double value) => Numbers.Exact(value + 0.0);

        /// <summary>The number of a text, as the reader reads one; the fallback where it is none, or not finite.</summary>
        internal static double Num(string text, double fallback) => Numbers.TryRead(text, out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>The whole number of a text; the fallback where it is none.</summary>
        internal static int Whole(string text, int fallback)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        /// <summary>The text of the first group of a code in a run, from an index; null where there is none.</summary>
        internal static string Text(IReadOnlyList<KeyValuePair<int, string>> run, int code, int from = 0)
        {
            for (int i = from; i < run.Count; i++)
            {
                if (run[i].Key == code)
                {
                    return run[i].Value;
                }
            }

            return null;
        }

        /// <summary>The own groups of a record as a run: from after its subclass marker, the marker itself left out.</summary>
        internal static List<KeyValuePair<int, string>> Run(IReadOnlyList<DxfTypedValue> own)
        {
            var run = new List<KeyValuePair<int, string>>(own.Count);

            for (int i = own.Count > 0 && own[0].TypeCode == 100 ? 1 : 0; i < own.Count; i++)
            {
                run.Add(new KeyValuePair<int, string>(own[i].TypeCode, own[i].Value));
            }

            return run;
        }

        /// <summary>The codes a run and the own groups it replaces hold, the subclass marker aside: those a rewriting of the run replaces.</summary>
        internal static HashSet<int> Codes(IReadOnlyList<DxfTypedValue> own, IReadOnlyList<KeyValuePair<int, string>> run)
        {
            var codes = new HashSet<int>();

            for (int i = own.Count > 0 && own[0].TypeCode == 100 ? 1 : 0; i < own.Count; i++)
            {
                codes.Add(own[i].TypeCode);
            }

            foreach (KeyValuePair<int, string> group in run)
            {
                codes.Add(group.Key);
            }

            return codes;
        }
    }
}
