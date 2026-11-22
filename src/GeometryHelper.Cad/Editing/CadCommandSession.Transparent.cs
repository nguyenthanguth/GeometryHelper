using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// PAN, ZOOM, CAL, SETVAR, the settings ORTHO, POLAR, OSNAP, GRID and SNAP, DIST, ID, LAYER, COLOR, LINETYPE, LWEIGHT,
    /// UNITS, QUICKCALC, DSETTINGS, TEXTSCR and the name of any system variable run inside another command, as AutoCAD 2021
    /// runs 'PAN and 'ZOOM: typed with an apostrophe at any prompt
    /// but one for text, they ask their own prompts after "&gt;&gt;", and once they end the command they interrupted writes
    /// "Resuming LINE command." and asks its prompt again, the objects "Select objects:" gathered still gathered. One nests
    /// in the other, 'ZOOM in a transparent PAN, as AutoCAD's does; 'ZOOM at ZOOM's own prompt is refused with ZOOM's words.
    /// A transparent CAL ends by typing its value at the prompt the command interrupted waits at, a point as 11,21,0 or a
    /// distance as 7 (<see cref="CadCommandContext.TransparentResult"/>), as AutoCAD 2021 does.
    /// </summary>
    public sealed partial class CadCommandSession
    {
        // The commands that run inside another, as AutoCAD's transparent PAN, ZOOM, CAL, SETVAR and the settings ORTHO, POLAR,
        // OSNAP, GRID and SNAP, the inquiries DIST and ID, the settings LAYER, COLOR, LINETYPE, LWEIGHT and UNITS, QUICKCALC,
        // DSETTINGS and TEXTSCR; a system variable's name is transparent too (see TryTransparent).
        private static readonly string[] Transparents =
        {
            "PAN",
            "ZOOM",
            "CAL",
            "SETVAR",
            "OSNAP",
            "ORTHO",
            "POLAR",
            "GRID",
            "SNAP",
            "DIST",
            "ID",
            "LAYER",
            "-LAYER",
            "COLOR",
            "-COLOR",
            "LINETYPE",
            "-LINETYPE",
            "LWEIGHT",
            "-LWEIGHT",
            "UNITS",
            "-UNITS",
            "QUICKCALC",
            "DSETTINGS",
            "TEXTSCR",
        };

        // What a transparent command that shows a dialog runs instead: its prompts on the command line, which is what the
        // dialog's name asks for in AutoCAD's console, so that 'LAYER asks "Enter an option [?/Make/Set/...]:".
        private static readonly Dictionary<string, string> TransparentForms = new Dictionary<string, string>
        {
            ["LAYER"] = "-LAYER",
            ["COLOR"] = "-COLOR",
            ["LINETYPE"] = "-LINETYPE",
            ["LWEIGHT"] = "-LWEIGHT",
            ["UNITS"] = "-UNITS",
        };

        // How many commands may wait inside one another, the one running among them, as AutoCAD 2021 allows.
        private const int NestingLimit = 4;

        // The commands a transparent one interrupted, the one it returns to last.
        private readonly Stack<Suspended> _suspended = new Stack<Suspended>();

        /// <summary>Gets whether the command running was started inside another, as 'ZOOM typed at LINE's next point.</summary>
        public bool IsTransparent => _suspended.Count > 0;

        /// <summary>
        /// 'PAN or 'ZOOM typed at a prompt: the command waiting is put aside and the transparent one runs. Another command
        /// typed with an apostrophe is "** That command may not be invoked transparently **", the text then going to the
        /// prompt as typed, as AutoCAD's; a name no command has goes to the prompt alone, unless it is a system variable's,
        /// which asks its new value as SETVAR does ('PDMODE). A fifth command inside four is "Commands may not be nested more
        /// than 4 deep", the text again going to the prompt.
        /// </summary>
        /// <returns>Whether the text was taken here.</returns>
        private bool TryTransparent(string text)
        {
            string typed = text.Trim();

            if (Prompt == null || Prompt.Kind == CadPromptKind.String || typed.Length < 2 || typed[0] != '\'')
            {
                return false;
            }

            // The _ and . that menus and routines put in front of the name (<c>'_.ZOOM</c>) are taken off, as for any typed name.
            string named = WithoutPrefixes(typed.Substring(1));
            CadCommand command = Registry.Find(named);

            // A variable's name is a transparent command of its own: it asks its new value, in SETVAR's words.
            if (command == null && Registry.FindVariable(named) is CadSystemVariable variable)
            {
                command = new CadCommand(variable.Name, c => CadCommands.TypedVariable(c, variable));
            }
            else if (command != null && Array.IndexOf(Transparents, command.Name) < 0)
            {
                Write("** That command may not be invoked transparently **");
                return false;
            }

            if (command == null)
            {
                return false;
            }

            if (_suspended.Count + 1 >= NestingLimit)
            {
                Write("Commands may not be nested more than " + NestingLimit + " deep");
                return false;
            }

            if (TransparentForms.TryGetValue(command.Name, out string form))
            {
                command = Registry.Find(form) ?? command;
            }

            string echo = Prompt.Echo(typed);

            // ZOOM's prompt takes no ZOOM of its own, as AutoCAD's refuses it with these words.
            if (command.Name == "ZOOM" && Current != null && Current.Name == "ZOOM")
            {
                Invalid("Requires a distance, numberX, or option keyword.", echo);
                return true;
            }

            Write(echo);
            _suspended.Push(new Suspended(this));
            Clear();
            Current = command;
            _context = new CadCommandContext(this, command, Array.Empty<string>());
            RaiseCommandStarts(command);

            try
            {
                _body = command.Body(_context).GetEnumerator();
            }
            catch (Exception exception)
            {
                Fail(exception);
                return true;
            }

            Advance();
            return true;
        }

        /// <summary>The name of PAN or ZOOM typed with an apostrophe at the Command prompt without it, ZOOM for 'ZOOM; null for any other text.</summary>
        private string WithoutApostrophe(string name)
        {
            if (name.Length < 2 || name[0] != '\'')
            {
                return null;
            }

            // The _ and . that menus and routines put in front of the name ('_.ZOOM) are taken off.
            string named = WithoutPrefixes(name.Substring(1));
            CadCommand command = Registry.Find(named);

            if (command == null)
            {
                return Registry.FindVariable(named) != null ? named : null;
            }

            return Array.IndexOf(Transparents, command.Name) >= 0 ? named : null;
        }

        /// <summary>
        /// The end of a transparent command: its own undo step closed where it opened one, the command it interrupted given
        /// back as it was, "Resuming LINE command." written. Esc ends the commands it interrupted too.
        /// </summary>
        private void EndTransparent(CadCommandOutcome outcome)
        {
            Suspended outer = _suspended.Pop();
            CadCommand ended = Current;

            // A form of AutoLISP typed at a prompt that waited for the user ends by answering the prompt with its value, as
            // L1's decision 9 says; the command it interrupted does not end with it, nor does it say it is resuming.
            string result = outer.ForLisp ? LispAnswer(_lispValue) : _context?.TransparentResult;
            _lispValue = null;

            if (_grouped && !outer.Grouped)
            {
                CloseGroup();
            }

            try
            {
                _body?.Dispose();
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn(
                    "CadCommandSession: the command " + Current?.Name + " failed to end.",
                    exception
                );
            }

            outer.Restore(this);
            RaiseCommandEnds(ended, outcome);

            if (outcome == CadCommandOutcome.Cancelled && !outer.ForLisp)
            {
                Finish(CadCommandOutcome.Cancelled);
                return;
            }

            if (!outer.ForLisp)
            {
                Write("Resuming " + Current.Name + " command.");
            }

            Changed();

            // The wait of a routine that this one carried ended with it: the routine is told once the command it interrupted is back.
            RunLispCancel();

            // 'CAL's value is typed at the prompt that was waiting.
            if (result != null)
            {
                if (outer.ForLisp)
                {
                    Refeed(result);
                }
                else
                {
                    Feed(result);
                }
            }
        }

        /// <summary>Whether a failing transparent command shares the undo step of the command it interrupted, which it then leaves open.</summary>
        private bool SharesGroup => _suspended.Count > 0 && _suspended.Peek().Grouped;

        /// <summary>The state of a prompt half answered, put aside for a transparent command that starts with none.</summary>
        private void Clear()
        {
            _band.Clear();
            _asked = null;
            _option = null;
            _removing = false;
            _single = false;
            _multiple = null;
            _windowFirst = null;
            _windowInSelection = false;
            _windowWritten = false;
            _secondFor = null;
            _modifiers.Clear();
            _trackPoints.Clear();
            AngleOverride = null;
        }

        /// <summary>A command a transparent one interrupted, with the state of its prompt.</summary>
        private sealed class Suspended
        {
            private readonly CadCommand _command;
            private readonly IEnumerator<CadPrompt> _body;
            private readonly CadCommandContext _context;
            private readonly CadPrompt _prompt;
            private readonly List<GeoPoint2> _band;
            private readonly CadPrompt _asked;
            private readonly string _option;
            private readonly bool _removing;
            private readonly bool _single;
            private readonly List<GeoPoint2> _multiple;
            private readonly CadPrompt _windowFirst;
            private readonly bool _windowInSelection;
            private readonly string _windowAfter;
            private readonly bool _windowWritten;
            private readonly CadPrompt _secondFor;
            private readonly GeoPoint2 _secondFrom;
            private readonly GeoPoint2 _secondLast;
            private readonly List<PointModifier> _modifiers;
            private readonly List<GeoPoint2> _trackPoints;
            private readonly double? _angleOverride;
            private readonly GeoPoint2 _lastPoint;

            internal Suspended(CadCommandSession session)
            {
                _command = session.Current;
                _body = session._body;
                _context = session._context;
                _prompt = session.Prompt;
                _band = new List<GeoPoint2>(session._band);
                _asked = session._asked;
                _option = session._option;
                _removing = session._removing;
                _single = session._single;
                _multiple = session._multiple;
                _windowFirst = session._windowFirst;
                _windowInSelection = session._windowInSelection;
                _windowAfter = session._windowAfter;
                _windowWritten = session._windowWritten;
                _secondFor = session._secondFor;
                _secondFrom = session._secondFrom;
                _secondLast = session._secondLast;
                _modifiers = new List<PointModifier>(session._modifiers);
                _trackPoints = new List<GeoPoint2>(session._trackPoints);
                _angleOverride = session.AngleOverride;
                _lastPoint = session.LastPoint;
                Grouped = session._grouped;
            }

            /// <summary>Gets whether the command had opened its undo step, which the transparent one then shares.</summary>
            internal bool Grouped { get; }

            /// <summary>Gets or sets whether what interrupted the command is a form of AutoLISP that waits for the user, not a transparent command.</summary>
            internal bool ForLisp { get; set; }

            /// <summary>Puts the command and its prompt back, the last point (LASTPOINT) as it was before the transparent command.</summary>
            internal void Restore(CadCommandSession session)
            {
                session.Current = _command;
                session._body = _body;
                session._context = _context;
                session.Prompt = _prompt;
                session._band.Clear();
                session._band.AddRange(_band);
                session._asked = _asked;
                session._option = _option;
                session._removing = _removing;
                session._single = _single;
                session._multiple = _multiple;
                session._windowFirst = _windowFirst;
                session._windowInSelection = _windowInSelection;
                session._windowAfter = _windowAfter;
                session._windowWritten = _windowWritten;
                session._secondFor = _secondFor;
                session._secondFrom = _secondFrom;
                session._secondLast = _secondLast;
                session._modifiers.Clear();
                session._modifiers.AddRange(_modifiers);
                session._trackPoints.Clear();
                session._trackPoints.AddRange(_trackPoints);
                session.AngleOverride = _angleOverride;
                session.LastPoint = _lastPoint;
                session._grouped = Grouped;
            }
        }
    }
}
