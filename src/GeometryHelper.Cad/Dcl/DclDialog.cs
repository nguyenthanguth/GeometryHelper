using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>
    /// A dialog that <c>new_dialog</c> made: its tiles (a copy of the definition, holding the values), where it is shown, its
    /// focus and how it ended. A host shows it and reports what the user does; the AutoLISP functions change it. The rules for
    /// what each kind of tile holds (D318, D339) are here, so that the functions, the script host and the window agree.
    /// </summary>
    internal sealed class DclDialog
    {
        internal DclDialog(DclTile root, int id, string name, DclDialog parent)
        {
            Root = root;
            Id = id;
            Name = name;
            Parent = parent;
        }

        /// <summary>Gets the dialog's top tile.</summary>
        internal DclTile Root { get; }

        /// <summary>Gets the id <c>load_dialog</c> gave the file the dialog came from.</summary>
        internal int Id { get; }

        /// <summary>Gets the dialog's name in its file.</summary>
        internal string Name { get; }

        /// <summary>Gets the dialog this one was opened over; null for none.</summary>
        internal DclDialog Parent { get; }

        /// <summary>Gets or sets the X of the screen point <c>new_dialog</c> gave, in pixels; null to centre the dialog.</summary>
        internal int? ScreenX { get; set; }

        /// <summary>Gets or sets the Y of the screen point <c>new_dialog</c> gave, in pixels; null to centre the dialog.</summary>
        internal int? ScreenY { get; set; }

        /// <summary>Gets or sets the left of the dialog where the host shows it, in screen pixels; <c>done_dialog</c> gives it.</summary>
        internal int PositionX { get; set; }

        /// <summary>Gets or sets the top of the dialog where the host shows it, in screen pixels; <c>done_dialog</c> gives it.</summary>
        internal int PositionY { get; set; }

        /// <summary>Gets or sets the key of the tile with the keyboard focus: <c>initial_focus</c>, then <c>mode_tile</c> 2.</summary>
        internal string FocusKey { get; set; }

        /// <summary>Gets or sets the key of the edit box whose text <c>mode_tile</c> 3 selected; null for none.</summary>
        internal string SelectTextKey { get; set; }

        /// <summary>Gets or sets whether the dialog has ended.</summary>
        internal bool IsDone { get; set; }

        /// <summary>Gets or sets the status it ended with, the value of <c>start_dialog</c>.</summary>
        internal int Status { get; set; }

        /// <summary>Gets or sets whether <c>start_dialog</c> is showing the dialog: <c>done_dialog</c> does nothing before.</summary>
        internal bool IsStarted { get; set; }

        /// <summary>Gets or sets whether <c>term_dialog</c> ended the dialog, whose <c>start_dialog</c> then gives -1 and runs nothing more.</summary>
        internal bool IsTerminated { get; set; }

        /// <summary>Gets or sets the action <c>new_dialog</c> gave for the tiles that have none; null for none.</summary>
        internal string DefaultAction { get; set; }

        /// <summary>
        /// Gets or sets the key of the edit box the user typed in and has not left: a box whose text changed gets its action with
        /// <c>$reason</c> 2 when the focus leaves it, and at the latest when the dialog closes (D340). A host sets it where the
        /// text changes and clears it when it reports the leaving; <c>start_dialog</c> reports it at the close when still set.
        /// </summary>
        internal string PendingEditKey { get; set; }

        /// <summary>Gets or sets whether the line about <c>slide_image</c> was written for this dialog (D320).</summary>
        internal bool SlideNoticeWritten { get; set; }

        /// <summary>Finds the first tile with the key; null for none.</summary>
        internal DclTile Find(string key) => Root.Find(key);

        /// <summary>
        /// Gives the tiles their first values: the <c>list</c> of a list box or popup split at each new line, the <c>value</c> of
        /// each tile (a toggle and a radio button <c>"0"</c> without one, a slider its <c>min_value</c>, a popup with items the
        /// first of them, a text its label), <c>is_enabled</c>, and the focus <c>initial_focus</c> names.
        /// </summary>
        internal void Initialise()
        {
            foreach (DclTile tile in Root.Descendants())
            {
                tile.IsEnabled = !string.Equals(tile.Get("is_enabled"), "false", StringComparison.OrdinalIgnoreCase);

                if (tile.Kind == "list_box" || tile.Kind == "popup_list")
                {
                    string list = tile.Get("list");

                    if (!string.IsNullOrEmpty(list))
                    {
                        tile.Items.AddRange(list.Split('\n'));
                    }
                }

                tile.Value = InitialValue(tile);
            }

            foreach (DclTile cluster in Root.Descendants().Where(tile => tile.Kind == "radio_cluster"))
            {
                string chosen = cluster.Get("value");

                if (!string.IsNullOrEmpty(chosen))
                {
                    SetCluster(cluster, chosen);
                }
            }

            FocusKey = Root.Get("initial_focus");
        }

        private static string InitialValue(DclTile tile)
        {
            switch (tile.Kind)
            {
                case "edit_box":
                    return tile.Get("value") ?? string.Empty;
                case "toggle":
                case "radio_button":
                    return tile.Get("value") ?? "0";
                case "list_box":
                    return tile.Get("value") ?? string.Empty;
                case "popup_list":
                    return tile.Get("value") ?? (tile.Items.Count > 0 ? "0" : string.Empty);
                case "slider":
                    return Clamp(tile, tile.Get("value") ?? tile.Get("min_value") ?? "0");
                case "text":
                    return tile.Get("value") ?? tile.Get("label") ?? string.Empty;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Gives what <c>get_tile</c> gives for a tile: its text, <c>"0"</c> or <c>"1"</c>, the indices chosen in a list (several
        /// separated by single spaces, ascending), the key of the chosen button of a radio cluster; nil (null) for a button, an
        /// image, an image button, a spacer and the clusters, which hold no value.
        /// </summary>
        /// <param name="tile">A tile of this dialog.</param>
        /// <returns>The value, or null.</returns>
        internal string GetValue(DclTile tile)
        {
            switch (tile.Kind)
            {
                case "radio_cluster":
                    DclTile chosen = tile.Descendants()
                        .FirstOrDefault(button => button.Kind == "radio_button" && button.Value == "1");
                    return chosen?.Key ?? string.Empty;
                case "edit_box":
                case "toggle":
                case "radio_button":
                case "list_box":
                case "popup_list":
                case "slider":
                case "text":
                    return tile.Value ?? string.Empty;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Sets a tile's value as <c>set_tile</c> does (D318, D339): an edit box and a text take the string, a toggle too
        /// whatever it is, a radio button set to <c>"1"</c> clears the others of its cluster, a cluster set to a button's key
        /// chooses that button, a multiple-selection list adds the indices to those chosen (out of range ignored, <c>""</c>
        /// clears), a single one takes the lowest index, a popup the index, a slider the number within its limits; a button and the
        /// like keep nothing.
        /// </summary>
        /// <param name="tile">A tile of this dialog.</param>
        /// <param name="value">The string given.</param>
        internal void SetValue(DclTile tile, string value)
        {
            switch (tile.Kind)
            {
                case "edit_box":
                case "toggle":
                case "text":
                    tile.Value = value;
                    break;
                case "radio_button":
                    if (value == "1")
                    {
                        Choose(tile);
                    }
                    else
                    {
                        tile.Value = "0";
                    }

                    break;
                case "radio_cluster":
                    SetCluster(tile, value);
                    break;
                case "list_box":
                    SetList(tile, value);
                    break;
                case "popup_list":
                    int[] chosen = Indices(value, tile.Items.Count).ToArray();

                    if (chosen.Length > 0)
                    {
                        tile.Value = chosen[0].ToString(CultureInfo.InvariantCulture);
                    }

                    break;
                case "slider":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int _))
                    {
                        tile.Value = Clamp(tile, value);
                    }

                    break;
            }
        }

        /// <summary>Puts into a tile what the user did there, as a host reports it: the value after the act.</summary>
        /// <param name="tile">The tile used.</param>
        /// <param name="value">Its value after the act.</param>
        internal void ApplyEvent(DclTile tile, string value)
        {
            switch (tile.Kind)
            {
                case "radio_button" when value == "1":
                    Choose(tile);
                    break;
                case "edit_box":
                case "toggle":
                case "radio_button":
                case "list_box":
                case "popup_list":
                case "slider":
                case "text":
                    tile.Value = value;
                    break;
            }
        }

        /// <summary>
        /// Tidies a list box or popup after <c>end_list</c>: the indices that no longer exist are dropped; a replaced item
        /// (operation 1) loses its choice; a popup that has items and nothing chosen shows the first, unless an item was replaced.
        /// </summary>
        /// <param name="tile">The list box or popup.</param>
        /// <param name="operation">The operation <c>start_list</c> was given: 1 replace, 2 append, 3 new list.</param>
        /// <param name="index">The index of the replaced item.</param>
        internal void ListEnded(DclTile tile, int operation, int index)
        {
            if (tile.Kind != "list_box" && tile.Kind != "popup_list")
            {
                return;
            }

            IEnumerable<int> chosen = Indices(tile.Value, tile.Items.Count);

            if (operation == 1)
            {
                chosen = chosen.Where(item => item != index);
            }

            tile.Value = string.Join(" ", chosen.Select(item => item.ToString(CultureInfo.InvariantCulture)));

            if (tile.Kind == "popup_list" && operation != 1 && tile.Value.Length == 0 && tile.Items.Count > 0)
            {
                tile.Value = "0";
            }
        }

        /// <summary>Tells whether a tile ends the dialog with its accept: the key <c>accept</c>, or <c>is_default</c>.</summary>
        /// <param name="tile">A tile.</param>
        /// <returns>True for the OK of the dialog.</returns>
        internal static bool IsAccept(DclTile tile) =>
            string.Equals(tile.Key, "accept", StringComparison.Ordinal) || tile.IsTrue("is_default");

        /// <summary>Tells whether a tile cancels the dialog: the key <c>cancel</c>, or <c>is_cancel</c>.</summary>
        /// <param name="tile">A tile.</param>
        /// <returns>True for the Cancel of the dialog.</returns>
        internal static bool IsCancel(DclTile tile) =>
            string.Equals(tile.Key, "cancel", StringComparison.Ordinal) || tile.IsTrue("is_cancel");

        /// <summary>Tells whether the dialog has a tile that can end it, one for which <see cref="IsAccept"/> or <see cref="IsCancel"/> holds.</summary>
        /// <returns>True when it has one.</returns>
        internal bool HasExitTile() => Root.Descendants().Any(tile => IsAccept(tile) || IsCancel(tile));

        /// <summary>Gives the tile Enter presses: the first with <c>is_default</c>, else the first with the key <c>accept</c>; null for none.</summary>
        /// <returns>The tile.</returns>
        internal DclTile DefaultTile() =>
            Root.Descendants().FirstOrDefault(tile => tile.IsTrue("is_default"))
            ?? Root.Descendants().FirstOrDefault(tile => string.Equals(tile.Key, "accept", StringComparison.Ordinal));

        /// <summary>Gives the tile Esc presses: the first with <c>is_cancel</c>, else the first with the key <c>cancel</c>; null for none.</summary>
        /// <returns>The tile.</returns>
        internal DclTile CancelTile() =>
            Root.Descendants().FirstOrDefault(tile => tile.IsTrue("is_cancel"))
            ?? Root.Descendants().FirstOrDefault(tile => string.Equals(tile.Key, "cancel", StringComparison.Ordinal));

        /// <summary>Gives the radio cluster a radio button is in: the nearest tile above it; null where it is in none.</summary>
        /// <param name="button">A radio button.</param>
        /// <returns>The cluster.</returns>
        internal static DclTile ClusterOf(DclTile button)
        {
            for (DclTile above = button.Parent; above != null; above = above.Parent)
            {
                if (above.Kind == "radio_cluster")
                {
                    return above;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads a string of indices as <c>get_tile</c> of a list writes them: whole numbers separated by white space. Gives
        /// those from 0 to below <paramref name="count"/>, each once, ascending; the rest is dropped.
        /// </summary>
        /// <param name="text">The string.</param>
        /// <param name="count">The number of items.</param>
        /// <returns>The valid indices, ascending.</returns>
        internal static IEnumerable<int> Indices(string text, int count)
        {
            var found = new SortedSet<int>();

            foreach (string word in (text ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (
                    int.TryParse(word, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    && index >= 0
                    && index < count
                )
                {
                    found.Add(index);
                }
            }

            return found;
        }

        private void Choose(DclTile button)
        {
            DclTile cluster = ClusterOf(button);

            if (cluster != null)
            {
                foreach (DclTile other in cluster.Descendants().Where(tile => tile.Kind == "radio_button"))
                {
                    other.Value = "0";
                }
            }

            button.Value = "1";
        }

        private void SetCluster(DclTile cluster, string key)
        {
            DclTile[] buttons = cluster.Descendants().Where(tile => tile.Kind == "radio_button").ToArray();
            DclTile named = key.Length == 0 ? null : buttons.FirstOrDefault(tile => tile.Key == key);

            if (named != null)
            {
                Choose(named);
            }
            else if (key.Length == 0)
            {
                foreach (DclTile button in buttons)
                {
                    button.Value = "0";
                }
            }
        }

        private static void SetList(DclTile list, string value)
        {
            int[] given = Indices(value, list.Items.Count).ToArray();

            if (list.IsTrue("multiple_select"))
            {
                IEnumerable<int> kept =
                    value.Trim().Length == 0 ? given : Indices(list.Value, list.Items.Count).Union(given);
                list.Value = string.Join(
                    " ",
                    kept.OrderBy(item => item).Select(item => item.ToString(CultureInfo.InvariantCulture))
                );
            }
            else if (value.Trim().Length == 0)
            {
                list.Value = string.Empty;
            }
            else if (given.Length > 0)
            {
                list.Value = given[0].ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string Clamp(DclTile slider, string text)
        {
            int minimum = slider.GetInteger("min_value") ?? 0;
            int maximum = slider.GetInteger("max_value") ?? 10000;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                number = minimum;
            }

            return Math.Max(minimum, Math.Min(Math.Max(minimum, maximum), number))
                .ToString(CultureInfo.InvariantCulture);
        }
    }
}
