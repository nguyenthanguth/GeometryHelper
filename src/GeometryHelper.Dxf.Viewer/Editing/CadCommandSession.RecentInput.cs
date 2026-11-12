using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The answers typed at the prompts, which the Recent Input menu of the canvas offers again, as AutoCAD 2021's with its
    /// INPUTHISTORYMODE 15: those of every command of the session, kept apart by what the prompt asked.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // How many answers of each kind are kept.
        private const int RecentInputCount = 10;

        // The answers typed, the newest first, for each kind of prompt that keeps them.
        private readonly Dictionary<CadPromptKind, List<string>> _recentInput =
            new Dictionary<CadPromptKind, List<string>>();

        /// <summary>
        /// Gets the answers typed at prompts of the kind the prompt waiting asks, the newest first and each once, ten at most:
        /// points typed at a prompt for a point (as <c>100,0</c>, <c>@0,50</c> or a distance along the rubber band), numbers
        /// at one for a distance, an angle or a whole number. A point picked on the canvas and an option keyword are not
        /// kept. After LINE's 0,0 then 100,0, the next point offers 100,0 and 0,0; empty where the prompt asks for something
        /// else or no command runs.
        /// </summary>
        public IReadOnlyList<string> RecentInput =>
            Prompt != null && _recentInput.TryGetValue(Prompt.Kind, out List<string> typed)
                ? typed
                : (IReadOnlyList<string>)Array.Empty<string>();

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

            string text = input.Text.Trim();
            typed.RemoveAll(t => string.Equals(t, text, StringComparison.OrdinalIgnoreCase));
            typed.Insert(0, text);

            if (typed.Count > RecentInputCount)
            {
                typed.RemoveAt(typed.Count - 1);
            }
        }
    }
}
