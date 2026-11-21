using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Cad.Lisp;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A host that drives dialogs from a script, for tests and for the harness that compares a routine run here with the same
    /// routine in AutoCAD (D329). The steps are strings of words separated by spaces, a text in double quotes with <c>\"</c>
    /// and <c>\\</c> inside; one step is done at a time, each dialog taking the next steps where the last left off:
    /// <list type="bullet">
    /// <item><description><c>type key "text"</c> types into an edit box, cut at its <c>edit_limit</c> (132 by default). The box's
    /// action runs, with <c>$reason</c> 2, when the next step goes to another tile and at the latest as the dialog closes.</description></item>
    /// <item><description><c>enter [key]</c> is Enter in the edit box or list box <c>key</c>, by default the tile with the focus.</description></item>
    /// <item><description><c>click key [x y]</c> presses a button (at its centre by default), flips a toggle, chooses a radio
    /// button, presses an image button (at 0 0 by default).</description></item>
    /// <item><description><c>dblclick key [index | x y]</c> double-clicks an item of a list box, or an image button.</description></item>
    /// <item><description><c>select key [index ...]</c> chooses in a list box or popup list; none clears.</description></item>
    /// <item><description><c>slide key n [drag]</c> moves a slider, <c>$reason</c> 1, or 3 with <c>drag</c>.</description></item>
    /// <item><description><c>esc</c> and <c>close</c> are Esc and the close box; <c>dump</c> writes every tile that has a key to the log.</description></item>
    /// </list>
    /// A step for a tile that is not there, is disabled or is of another kind is written to the log as <c>refused step</c> and
    /// skipped. When the steps run out while a dialog is shown, the host presses Esc and logs <c>script ended</c>; if the
    /// dialog is still there at the next call, it ends it with 0. The log has <c>open name</c> and the dump that follows it,
    /// <c>dump name key=value ...</c> (the keys in tile order, the values as <c>prin1</c> writes them), and <c>close name status</c>.
    /// </summary>
    internal sealed class DclScriptHost : IDclHost
    {
        private readonly Queue<string> _steps;
        private readonly List<string> _log = new List<string>();
        private readonly Dictionary<DclDialog, Queue<DclEvent>> _held = new Dictionary<DclDialog, Queue<DclEvent>>();
        private readonly HashSet<DclDialog> _ended = new HashSet<DclDialog>();

        /// <summary>Makes a host that does the steps in order.</summary>
        /// <param name="steps">The steps, one string each.</param>
        internal DclScriptHost(IEnumerable<string> steps)
        {
            _steps = new Queue<string>(steps ?? Enumerable.Empty<string>());
        }

        /// <summary>Gets the log: what the host did and saw, one line each, in order.</summary>
        internal IReadOnlyList<string> Log => _log;

        /// <summary>Gets how many steps have not been done.</summary>
        internal int Remaining => _steps.Count;

        /// <inheritdoc/>
        public void Open(DclDialog dialog)
        {
            dialog.PositionX = dialog.ScreenX ?? 0;
            dialog.PositionY = dialog.ScreenY ?? 0;
            _log.Add("open " + dialog.Name);
            _log.Add(Dump(dialog));
        }

        /// <inheritdoc/>
        public DclEvent Next(DclDialog dialog)
        {
            if (_held.TryGetValue(dialog, out Queue<DclEvent> held) && held.Count > 0)
            {
                return held.Dequeue();
            }

            while (_steps.Count > 0)
            {
                string step = _steps.Dequeue();
                DclEvent done = Do(dialog, step);

                if (done != null)
                {
                    return done;
                }
            }

            if (_ended.Add(dialog))
            {
                _log.Add("script ended");
            }
            else
            {
                dialog.IsDone = true;
                dialog.Status = 0;
            }

            return DclEvent.Escape();
        }

        /// <inheritdoc/>
        public void Close(DclDialog dialog)
        {
            _held.Remove(dialog);
            _ended.Remove(dialog);
            _log.Add("close " + dialog.Name + " " + dialog.Status.ToString(CultureInfo.InvariantCulture));
        }

        private DclEvent Do(DclDialog dialog, string step)
        {
            List<string> words = Words(step);

            if (words == null || words.Count == 0)
            {
                return Refuse(step);
            }

            string verb = words[0].ToLowerInvariant();
            DclTile tile = words.Count > 1 ? dialog.Find(words[1]) : null;

            switch (verb)
            {
                case "esc":
                case "close":
                    return words.Count == 1 ? DclEvent.Escape() : Refuse(step);
                case "dump":
                    _log.Add(Dump(dialog));
                    return null;
                case "enter":
                    return Enter(dialog, step, words);
            }

            if (tile == null || !tile.IsEnabled)
            {
                return Refuse(step);
            }

            switch (verb)
            {
                case "type":
                    return Type(dialog, step, words, tile);
                case "click":
                    return Click(dialog, step, words, tile);
                case "dblclick":
                    return DoubleClick(dialog, step, words, tile);
                case "select":
                    return Select(dialog, step, words, tile);
                case "slide":
                    return Slide(dialog, step, words, tile);
                default:
                    return Refuse(step);
            }
        }

        private DclEvent Refuse(string step)
        {
            _log.Add("refused " + step);
            return null;
        }

        private DclEvent Type(DclDialog dialog, string step, List<string> words, DclTile tile)
        {
            if (words.Count != 3 || tile.Kind != "edit_box")
            {
                return Refuse(step);
            }

            string text = words[2];
            int limit = tile.GetInteger("edit_limit") ?? 132;

            if (text.Length > limit)
            {
                text = text.Substring(0, limit);
            }

            // Moving the typing to another box leaves the one before it.
            DclEvent left = Leave(dialog, tile.Key);

            if (!string.Equals(tile.Value, text, StringComparison.Ordinal))
            {
                tile.Value = text;
                dialog.PendingEditKey = tile.Key;
            }

            dialog.FocusKey = tile.Key;
            return left;
        }

        private DclEvent Enter(DclDialog dialog, string step, List<string> words)
        {
            if (words.Count > 2)
            {
                return Refuse(step);
            }

            string key = words.Count == 2 ? words[1] : dialog.FocusKey;
            DclTile tile = key == null ? null : dialog.Find(key);

            if (words.Count == 2 && (tile == null || !tile.IsEnabled))
            {
                return Refuse(step);
            }

            return Report(dialog, tile?.Key, DclEvent.Enter(tile?.Key));
        }

        private DclEvent Click(DclDialog dialog, string step, List<string> words, DclTile tile)
        {
            if (!(words.Count == 2 || (words.Count == 4 && IsPoint(tile))) || !Clickable(tile))
            {
                return Refuse(step);
            }

            int x = 0;
            int y = 0;

            if (words.Count == 4)
            {
                if (!Whole(words[2], out x) || !Whole(words[3], out y))
                {
                    return Refuse(step);
                }
            }
            else if (tile.Kind == "button")
            {
                Centre(dialog, tile, out x, out y);
            }

            string value = "1";

            if (tile.Kind == "toggle")
            {
                value = tile.Value == "1" ? "0" : "1";
            }

            return Report(dialog, tile.Key, DclEvent.Tile(tile.Key, value, 1, x, y));
        }

        private DclEvent DoubleClick(DclDialog dialog, string step, List<string> words, DclTile tile)
        {
            if (tile.Kind == "list_box" && words.Count <= 3)
            {
                string value = dialog.GetValue(tile);

                if (words.Count == 3)
                {
                    if (!Whole(words[2], out int index) || index < 0 || index >= tile.Items.Count)
                    {
                        return Refuse(step);
                    }

                    value = index.ToString(CultureInfo.InvariantCulture);
                }

                return Report(dialog, tile.Key, DclEvent.Tile(tile.Key, value, 4));
            }

            if (tile.Kind == "image_button" && (words.Count == 2 || words.Count == 4))
            {
                int x = 0;
                int y = 0;

                if (words.Count == 4 && (!Whole(words[2], out x) || !Whole(words[3], out y)))
                {
                    return Refuse(step);
                }

                return Report(dialog, tile.Key, DclEvent.Tile(tile.Key, "1", 4, x, y));
            }

            return Refuse(step);
        }

        private DclEvent Select(DclDialog dialog, string step, List<string> words, DclTile tile)
        {
            if (tile.Kind != "list_box" && tile.Kind != "popup_list")
            {
                return Refuse(step);
            }

            var chosen = new SortedSet<int>();

            foreach (string word in words.Skip(2))
            {
                if (!Whole(word, out int index) || index < 0 || index >= tile.Items.Count)
                {
                    return Refuse(step);
                }

                chosen.Add(index);
            }

            if (chosen.Count > 1 && !(tile.Kind == "list_box" && tile.IsTrue("multiple_select")))
            {
                return Refuse(step);
            }

            string value = string.Join(" ", chosen.Select(index => index.ToString(CultureInfo.InvariantCulture)));
            return Report(dialog, tile.Key, DclEvent.Tile(tile.Key, value, 1));
        }

        private DclEvent Slide(DclDialog dialog, string step, List<string> words, DclTile tile)
        {
            bool drag = words.Count == 4 && string.Equals(words[3], "drag", StringComparison.OrdinalIgnoreCase);

            if (tile.Kind != "slider" || !(words.Count == 3 || drag) || !Whole(words[2], out int position))
            {
                return Refuse(step);
            }

            int minimum = tile.GetInteger("min_value") ?? 0;
            int maximum = Math.Max(minimum, tile.GetInteger("max_value") ?? 10000);
            string value = Math.Max(minimum, Math.Min(maximum, position)).ToString(CultureInfo.InvariantCulture);
            return Report(dialog, tile.Key, DclEvent.Tile(tile.Key, value, drag ? 3 : 1));
        }

        /// <summary>
        /// Gives the event of a step: where an edit box is still waiting to report its text and the step goes to another tile,
        /// the box leaves first and the step's own event follows at the next call.
        /// </summary>
        private DclEvent Report(DclDialog dialog, string key, DclEvent own)
        {
            DclEvent left = key == null ? null : Leave(dialog, key);

            if (left == null)
            {
                return own;
            }

            if (!_held.TryGetValue(dialog, out Queue<DclEvent> held))
            {
                held = new Queue<DclEvent>();
                _held[dialog] = held;
            }

            held.Enqueue(own);
            return left;
        }

        // The edit box typed in and not left, when the focus now goes to another tile: its event, reason 2; else null.
        private static DclEvent Leave(DclDialog dialog, string goingTo)
        {
            string pending = dialog.PendingEditKey;
            DclTile box = pending == null ? null : dialog.Find(pending);

            if (box == null || string.Equals(pending, goingTo, StringComparison.Ordinal))
            {
                return null;
            }

            dialog.PendingEditKey = null;
            return DclEvent.Tile(pending, dialog.GetValue(box), 2);
        }

        private static bool Clickable(DclTile tile) =>
            tile.Kind == "button"
            || tile.Kind == "toggle"
            || tile.Kind == "radio_button"
            || tile.Kind == "image_button";

        private static bool IsPoint(DclTile tile) => tile.Kind == "button" || tile.Kind == "image_button";

        private static void Centre(DclDialog dialog, DclTile tile, out int x, out int y)
        {
            x = 0;
            y = 0;

            if (DclLayout.Arrange(dialog.Root, DclMetrics.Default).TryGetValue(tile, out DclRect rect))
            {
                x = (int)Math.Floor(rect.Width / 2);
                y = (int)Math.Floor(rect.Height / 2);
            }
        }

        private static bool Whole(string word, out int number) =>
            int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);

        private static string Dump(DclDialog dialog)
        {
            var line = new StringBuilder("dump ").Append(dialog.Name);

            foreach (DclTile tile in dialog.Root.Descendants().Where(each => each.Key != null))
            {
                string value = dialog.GetValue(tile);
                line.Append(' ').Append(tile.Key).Append('=').Append(LispInterpreter.Print(value, true));
            }

            return line.ToString();
        }

        /// <summary>Splits a step into its words: separated by spaces, a word in double quotes keeping its spaces, with <c>\"</c> and <c>\\</c> inside. Null for a quote left open.</summary>
        private static List<string> Words(string step)
        {
            var words = new List<string>();
            var word = new StringBuilder();
            bool inWord = false;
            bool quoted = false;

            for (int i = 0; i < step.Length; i++)
            {
                char c = step[i];

                if (quoted)
                {
                    if (c == '\\' && i + 1 < step.Length && (step[i + 1] == '"' || step[i + 1] == '\\'))
                    {
                        word.Append(step[++i]);
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        word.Append(c);
                    }
                }
                else if (c == '"')
                {
                    quoted = true;
                    inWord = true;
                }
                else if (c == ' ' || c == '\t')
                {
                    if (inWord)
                    {
                        words.Add(word.ToString());
                        word.Clear();
                        inWord = false;
                    }
                }
                else
                {
                    word.Append(c);
                    inWord = true;
                }
            }

            if (quoted)
            {
                return null;
            }

            if (inWord)
            {
                words.Add(word.ToString());
            }

            return words;
        }
    }
}
