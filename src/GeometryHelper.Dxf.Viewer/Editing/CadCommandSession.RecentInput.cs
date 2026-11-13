using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The answers typed at the prompts, which the Recent Input menu of the canvas offers again, as AutoCAD 2021's with its
    /// INPUTHISTORYMODE 15: those of every command of the session, kept apart by what the prompt asked, the points and numbers
    /// written in the drawing's units and the selection sets named by their count, "Selection set (3 items)".
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // How many answers of each kind are kept.
        private const int RecentInputCount = 10;

        // The answers typed, the newest first, for each kind of prompt that keeps them.
        private readonly Dictionary<CadPromptKind, List<string>> _recentInput =
            new Dictionary<CadPromptKind, List<string>>();

        // The selection sets answered at Select objects, the newest first, each the handles that were taken.
        private readonly List<string[]> _recentSets = new List<string[]>();

        /// <summary>
        /// Gets the answers typed at prompts of the kind the prompt waiting asks, the newest first and each once, ten at most:
        /// points typed at a prompt for a point (as <c>100,0</c>, <c>@0,50</c> or a distance along the rubber band), numbers
        /// at one for a distance, an angle or a whole number, written in the drawing's units (<c>100.0000,0.0000</c> at
        /// LUNITS 2 and LUPREC 4), and at Select objects the sets of objects answered, "Selection set (2 items)". A point
        /// picked on the canvas and an option keyword are not kept. After LINE's 0,0 then 100,0, the next point offers
        /// 100.0000,0.0000 and 0.0000,0.0000; empty where the prompt asks for something else or no command runs.
        /// </summary>
        public IReadOnlyList<string> RecentInput =>
            Prompt == null ? Array.Empty<string>()
            : Prompt.Kind == CadPromptKind.Selection ? _recentSets.Select(Named).ToList()
            : _recentInput.TryGetValue(Prompt.Kind, out List<string> typed) ? typed
            : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// Answers the prompt waiting with an entry of <see cref="RecentInput"/>, as the Recent Input menu does: the text typed
        /// again, or at Select objects the set of objects again, those of it that are still in the drawing.
        /// </summary>
        /// <param name="index">The entry's place in <see cref="RecentInput"/>, from nought.</param>
        /// <returns>Whether the prompt took it; false where there is no such entry.</returns>
        public bool FeedRecentInput(int index)
        {
            IReadOnlyList<string> recent = RecentInput;

            if (index < 0 || index >= recent.Count)
            {
                return false;
            }

            return Prompt.Kind == CadPromptKind.Selection ? FeedSelection(_recentSets[index]) : Feed(recent[index]);
        }

        /// <summary>The name AutoCAD gives a set of objects in Recent Input: "Selection set (3 items)".</summary>
        private static string Named(string[] set) =>
            "Selection set (" + set.Length.ToString(CultureInfo.InvariantCulture) + " items)";

        /// <summary>Keeps a set of objects answered at Select objects, the newest first, one of the same objects moved to the top.</summary>
        private void RememberSet(IReadOnlyList<string> handles)
        {
            if (_refeeding || handles.Count == 0)
            {
                return;
            }

            string[] set = handles.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToArray();
            _recentSets.RemoveAll(s => s.SequenceEqual(set, StringComparer.OrdinalIgnoreCase));
            _recentSets.Insert(0, set);

            if (_recentSets.Count > RecentInputCount)
            {
                _recentSets.RemoveAt(_recentSets.Count - 1);
            }
        }

        /// <summary>Keeps an answer typed at a prompt for a point, a distance, an angle or a whole number, the newest first.</summary>
        private void RememberTyped(CadInput input)
        {
            if (
                Prompt == null
                || _refeeding
                || string.IsNullOrWhiteSpace(input.Text)
                || (input.Kind != CadInputKind.Point && input.Kind != CadInputKind.Number)
            )
            {
                return;
            }

            CadPromptKind kind = Prompt.Kind;

            if (
                kind != CadPromptKind.Point
                && kind != CadPromptKind.Distance
                && kind != CadPromptKind.Angle
                && kind != CadPromptKind.Integer
            )
            {
                return;
            }

            if (!_recentInput.TryGetValue(kind, out List<string> typed))
            {
                typed = new List<string>();
                _recentInput[kind] = typed;
            }

            string text = InDrawingUnits(input.Text.Trim(), kind);
            typed.RemoveAll(t => string.Equals(t, text, StringComparison.OrdinalIgnoreCase));
            typed.Insert(0, text);

            if (typed.Count > RecentInputCount)
            {
                typed.RemoveAt(typed.Count - 1);
            }
        }

        /// <summary>
        /// An answer written in the drawing's units, as Recent Input shows it: each number of a point (<c>100,0</c>,
        /// <c>@0,50</c>, <c>#10,20</c>, <c>@50&lt;45</c>) or of a distance as the drawing writes a length (LUNITS and LUPREC),
        /// the angle after a &lt; or of an angle as it writes an angle (AUNITS and AUPREC), the prefixes kept; a whole number,
        /// and an answer that does not read as numbers of the units, as it was typed.
        /// </summary>
        private string InDrawingUnits(string text, CadPromptKind kind)
        {
            if (kind == CadPromptKind.Integer)
            {
                return text;
            }

            DxfDatabase database = Document.Database;
            int start = 0;

            while (start < text.Length && (text[start] == '@' || text[start] == '#' || text[start] == '*'))
            {
                start++;
            }

            var written = new List<string>();

            foreach (string part in text.Substring(start).Split(','))
            {
                string[] polar = part.Split('<');

                if (polar.Length > 2)
                {
                    return text;
                }

                string first;

                if (kind == CadPromptKind.Angle && polar.Length == 1)
                {
                    if (!CadUnits.TryAngle(part, database.Aunits, out double angle))
                    {
                        return text;
                    }

                    first = CadUnits.Angle(angle, database.Aunits, database.Auprec);
                }
                else
                {
                    if (!CadUnits.TryLength(polar[0], database.Lunits, out double length))
                    {
                        return text;
                    }

                    first = CadUnits.Length(length, database.Lunits, database.Luprec);
                }

                if (polar.Length == 2)
                {
                    if (!CadUnits.TryAngle(polar[1], database.Aunits, out double turned))
                    {
                        return text;
                    }

                    first += "<" + CadUnits.Angle(turned, database.Aunits, database.Auprec);
                }

                written.Add(first);
            }

            return text.Substring(0, start) + string.Join(",", written);
        }
    }
}
