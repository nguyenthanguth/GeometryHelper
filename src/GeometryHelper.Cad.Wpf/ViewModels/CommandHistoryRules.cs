using System;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>What the history above the command line does with a key pressed while it holds the keyboard.</summary>
    internal enum CommandHistoryKey
    {
        /// <summary>The history keeps the key: Ctrl+C and Ctrl+A, the arrows and page keys that move or extend a selection, Esc with a selection.</summary>
        Stays,

        /// <summary>The key types a character: the character goes to the input, which takes the keyboard.</summary>
        Types,

        /// <summary>The key is the input's (Enter, Space, Esc, Tab, Back, Delete, F keys, shortcuts): the input takes the keyboard and the key.</summary>
        ToInput,
    }

    /// <summary>
    /// The rules of the history area of the command line, as AutoCAD 2021's: how many lines fit, how many a drag of its top
    /// edge gives, and which keys it keeps. Plain numbers and keys, so that they are checked without a window.
    /// </summary>
    internal static class CommandHistoryRules
    {
        /// <summary>
        /// The most lines the history may show: as many as fit in half the height of the drawing area, the canvas and the
        /// command line together, so that the canvas always keeps the other half. A box 800 high with 16 px lines gives 25.
        /// </summary>
        /// <param name="areaHeight">The height of the drawing area in pixels.</param>
        /// <param name="lineHeight">The height of one line in pixels.</param>
        /// <returns>Whole lines, 0 at least.</returns>
        internal static int MostLines(double areaHeight, double lineHeight) =>
            lineHeight > 0.0 && areaHeight > 0.0 ? (int)Math.Floor(0.5 * areaHeight / lineHeight) : 0;

        /// <summary>
        /// The lines shown: the number kept, or as many as fit where the window is too small for it; the number kept is not
        /// changed by that, so that a window made large again shows it again.
        /// </summary>
        /// <param name="kept">The number kept in the settings.</param>
        /// <param name="most">The most that fit, <see cref="MostLines"/>.</param>
        /// <returns>The lines to show, 0 at least.</returns>
        internal static int Shown(int kept, int most) => Math.Max(0, Math.Min(kept, most));

        /// <summary>
        /// The lines a drag of the top edge gives: the height at the start less the way the pointer went down, in whole lines,
        /// to the nearest, held between 0 and the most that fit. Dragging up 40 px from 3 lines of 16 px gives 5 lines.
        /// </summary>
        /// <param name="startHeight">The height of the history when the drag began, in pixels.</param>
        /// <param name="pointerDown">How far down the pointer has gone since the drag began; negative when it went up.</param>
        /// <param name="lineHeight">The height of one line in pixels.</param>
        /// <param name="most">The most that fit, <see cref="MostLines"/>.</param>
        /// <returns>Whole lines from 0 to <paramref name="most"/>.</returns>
        internal static int LinesForDrag(double startHeight, double pointerDown, double lineHeight, int most) =>
            lineHeight > 0.0
                ? Math.Max(
                    0,
                    Math.Min(
                        most,
                        (int)Math.Round((startHeight - pointerDown) / lineHeight, MidpointRounding.AwayFromZero)
                    )
                )
                : 0;

        /// <summary>
        /// What the history does with a key. It keeps Ctrl+C and Ctrl+A, the arrows, Home, End and the page keys (with Shift
        /// they extend the selection) and Esc where a selection is there to let go. A key that types a character, with no
        /// Ctrl or Alt, is a character for the input. Every other key, Enter, Space, Esc with no selection, Tab, Back, Delete,
        /// the F keys and every shortcut, is the input's.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifier keys held.</param>
        /// <param name="hasSelection">Whether text is selected in the history.</param>
        /// <returns>What the history does with it.</returns>
        internal static CommandHistoryKey Classify(Key key, ModifierKeys modifiers, bool hasSelection)
        {
            bool control = (modifiers & ModifierKeys.Control) != 0;
            bool alt = (modifiers & ModifierKeys.Alt) != 0;

            switch (key)
            {
                case Key.LeftShift:
                case Key.RightShift:
                case Key.LeftCtrl:
                case Key.RightCtrl:
                case Key.LeftAlt:
                case Key.RightAlt:
                case Key.System:
                    return CommandHistoryKey.Stays;
                case Key.Escape:
                    return hasSelection && modifiers == ModifierKeys.None
                        ? CommandHistoryKey.Stays
                        : CommandHistoryKey.ToInput;
                case Key.Up:
                case Key.Down:
                case Key.Left:
                case Key.Right:
                case Key.Home:
                case Key.End:
                case Key.PageUp:
                case Key.PageDown:
                    return alt ? CommandHistoryKey.ToInput : CommandHistoryKey.Stays;
                case Key.C:
                case Key.A:
                    if (modifiers == ModifierKeys.Control)
                    {
                        return CommandHistoryKey.Stays;
                    }

                    break;
                case Key.Enter:
                case Key.Space:
                case Key.Tab:
                case Key.Back:
                case Key.Delete:
                    return CommandHistoryKey.ToInput;
            }

            bool typing =
                (key >= Key.A && key <= Key.Z)
                || (key >= Key.D0 && key <= Key.D9)
                || (key >= Key.NumPad0 && key <= Key.Divide)
                || (key >= Key.Oem1 && key <= Key.OemBackslash);
            return typing && !control && !alt ? CommandHistoryKey.Types : CommandHistoryKey.ToInput;
        }
    }
}
