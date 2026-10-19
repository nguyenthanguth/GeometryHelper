using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The modifier keys held with a gesture.
    /// </summary>
    [Flags]
    public enum GestureModifiers
    {
        /// <summary>
        /// No modifier.
        /// </summary>
        None = 0,

        /// <summary>
        /// The Ctrl key.
        /// </summary>
        Ctrl = 1,

        /// <summary>
        /// The Alt key.
        /// </summary>
        Alt = 2,

        /// <summary>
        /// The Shift key.
        /// </summary>
        Shift = 4,
    }

    /// <summary>
    /// What kind of input a gesture is.
    /// </summary>
    public enum GestureKind
    {
        /// <summary>
        /// A key pressed.
        /// </summary>
        Key,

        /// <summary>
        /// The mouse wheel turned.
        /// </summary>
        Wheel,

        /// <summary>
        /// A mouse button pressed and released without moving.
        /// </summary>
        Click,

        /// <summary>
        /// A mouse button clicked twice.
        /// </summary>
        DoubleClick,

        /// <summary>
        /// A mouse button held while the mouse moves.
        /// </summary>
        Drag,
    }

    /// <summary>
    /// A mouse button.
    /// </summary>
    public enum GestureButton
    {
        /// <summary>
        /// No button: a key or the wheel.
        /// </summary>
        None,

        /// <summary>
        /// The left button.
        /// </summary>
        Left,

        /// <summary>
        /// The middle button, the wheel pressed.
        /// </summary>
        Middle,

        /// <summary>
        /// The right button.
        /// </summary>
        Right,
    }

    /// <summary>
    /// One input a keymap binds to an action: a key, the wheel, or a mouse button's click, double click or drag, with
    /// modifiers. It is written as text, <c>Ctrl+Shift+H</c>, <c>Wheel</c>, <c>Ctrl+MiddleDrag</c>, <c>DoubleClick</c>, and
    /// two gestures are the same when their canonical text is.
    /// </summary>
    public sealed class Gesture : IEquatable<Gesture>
    {
        private static readonly HashSet<string> _keys = BuildKeys();

        private Gesture(GestureModifiers modifiers, GestureKind kind, GestureButton button, string key)
        {
            Modifiers = modifiers;
            Kind = kind;
            Button = button;
            Key = key;
        }

        /// <summary>
        /// Gets the modifier keys held.
        /// </summary>
        public GestureModifiers Modifiers { get; }

        /// <summary>
        /// Gets the kind of input.
        /// </summary>
        public GestureKind Kind { get; }

        /// <summary>
        /// Gets the mouse button of a click, double click or drag; none for a key or the wheel.
        /// </summary>
        public GestureButton Button { get; }

        /// <summary>
        /// Gets the key of a key gesture, as written: <c>A</c> to <c>Z</c>, <c>0</c> to <c>9</c>, <c>F1</c> to <c>F24</c>,
        /// <c>Esc</c>, <c>Enter</c>, <c>Space</c>, <c>Tab</c>, <c>Delete</c>, <c>Backspace</c>, <c>Insert</c>, <c>Home</c>,
        /// <c>End</c>, <c>PageUp</c>, <c>PageDown</c>, <c>Left</c>, <c>Right</c>, <c>Up</c> and <c>Down</c>; null for the others.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Reads a gesture from its text.
        /// </summary>
        /// <param name="text">The text, <c>Ctrl+Shift+H</c> for example; the case does not matter.</param>
        /// <returns>The gesture.</returns>
        /// <exception cref="FormatException">Thrown when the text is not a gesture; the message says what is wrong with it.</exception>
        public static Gesture Parse(string text)
        {
            if (!TryParse(text, out Gesture gesture, out string problem))
            {
                throw new FormatException(problem);
            }

            return gesture;
        }

        /// <summary>
        /// Reads a gesture from its text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="gesture">The gesture when the method returns true.</param>
        /// <param name="problem">What is wrong with the text when the method returns false.</param>
        /// <returns>true when the text is a gesture; otherwise, false.</returns>
        public static bool TryParse(string text, out Gesture gesture, out string problem)
        {
            gesture = null;
            problem = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                problem = "A gesture cannot be empty.";
                return false;
            }

            string[] parts = text.Split('+').Select(part => part.Trim()).ToArray();
            GestureModifiers modifiers = GestureModifiers.None;

            for (int i = 0; i < parts.Length - 1; i++)
            {
                GestureModifiers one;

                switch (parts[i].ToLowerInvariant())
                {
                    case "ctrl": one = GestureModifiers.Ctrl; break;
                    case "alt": one = GestureModifiers.Alt; break;
                    case "shift": one = GestureModifiers.Shift; break;
                    default:
                        problem = "'" + parts[i] + "' in '" + text + "' is not a modifier; use Ctrl, Alt or Shift.";
                        return false;
                }

                if ((modifiers & one) != 0)
                {
                    problem = "'" + text + "' names " + parts[i] + " twice.";
                    return false;
                }

                modifiers |= one;
            }

            string last = parts[parts.Length - 1];
            string lower = last.ToLowerInvariant();

            if (lower == "wheel")
            {
                gesture = new Gesture(modifiers, GestureKind.Wheel, GestureButton.None, null);
                return true;
            }

            foreach (GestureButton button in new[] { GestureButton.Left, GestureButton.Middle, GestureButton.Right })
            {
                string prefix = button.ToString().ToLowerInvariant();

                foreach (GestureKind kind in new[] { GestureKind.Click, GestureKind.DoubleClick, GestureKind.Drag })
                {
                    string suffix = kind.ToString().ToLowerInvariant();

                    // The left button is the default, so 'Click' means 'LeftClick'.
                    if (lower == prefix + suffix || (button == GestureButton.Left && lower == suffix))
                    {
                        gesture = new Gesture(modifiers, kind, button, null);
                        return true;
                    }
                }
            }

            string key = CanonicalKey(last);

            if (key == null)
            {
                problem = "'" + last + "' in '" + text + "' is not a key, the wheel, or a mouse gesture.";
                return false;
            }

            gesture = new Gesture(modifiers, GestureKind.Key, GestureButton.None, key);
            return true;
        }

        /// <summary>
        /// Writes the gesture in the one form two equal gestures share: modifiers in the order Ctrl, Alt, Shift, then the
        /// key, the wheel, or the mouse gesture, a left button written as <c>Click</c>, <c>DoubleClick</c> and <c>LeftDrag</c>.
        /// </summary>
        public override string ToString()
        {
            var text = new StringBuilder();

            if ((Modifiers & GestureModifiers.Ctrl) != 0)
            {
                text.Append("Ctrl+");
            }

            if ((Modifiers & GestureModifiers.Alt) != 0)
            {
                text.Append("Alt+");
            }

            if ((Modifiers & GestureModifiers.Shift) != 0)
            {
                text.Append("Shift+");
            }

            switch (Kind)
            {
                case GestureKind.Key:
                    text.Append(Key);
                    break;

                case GestureKind.Wheel:
                    text.Append("Wheel");
                    break;

                case GestureKind.Click:
                    text.Append(Button == GestureButton.Left ? string.Empty : Button.ToString()).Append("Click");
                    break;

                case GestureKind.DoubleClick:
                    text.Append(Button == GestureButton.Left ? string.Empty : Button.ToString()).Append("DoubleClick");
                    break;

                default:
                    text.Append(Button).Append("Drag");
                    break;
            }

            return text.ToString();
        }

        /// <summary>
        /// Determines whether another gesture is the same input.
        /// </summary>
        public bool Equals(Gesture other) => other != null && string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);

        /// <summary>
        /// Determines whether an object is the same input.
        /// </summary>
        public override bool Equals(object obj) => Equals(obj as Gesture);

        /// <summary>
        /// Gets a hash code of the canonical text.
        /// </summary>
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(ToString());

        private static string CanonicalKey(string text)
        {
            foreach (string key in _keys)
            {
                if (string.Equals(key, text, StringComparison.OrdinalIgnoreCase))
                {
                    return key;
                }
            }

            return string.Equals(text, "Escape", StringComparison.OrdinalIgnoreCase) ? "Esc" : null;
        }

        private static HashSet<string> BuildKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal)
            {
                "Esc", "Enter", "Space", "Tab", "Delete", "Backspace", "Insert", "Home", "End",
                "PageUp", "PageDown", "Left", "Right", "Up", "Down",
            };

            for (char c = 'A'; c <= 'Z'; c++)
            {
                keys.Add(c.ToString());
            }

            for (char c = '0'; c <= '9'; c++)
            {
                keys.Add(c.ToString());
            }

            for (int f = 1; f <= 24; f++)
            {
                keys.Add("F" + f);
            }

            return keys;
        }
    }
}
