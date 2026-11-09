using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>How a command ended.</summary>
    public enum CadCommandOutcome
    {
        /// <summary>It ran to its end.</summary>
        Completed,

        /// <summary>Esc ended it; what it had made before stays, as AutoCAD keeps the lines a LINE drew.</summary>
        Cancelled,

        /// <summary>It failed: its edits were taken back.</summary>
        Failed,
    }

    /// <summary>A request of a command to the window: open Properties, the Layer manager, Global settings, or save.</summary>
    public sealed class CadUiRequestEventArgs : EventArgs
    {
        internal CadUiRequestEventArgs(string name)
        {
            Name = name;
        }

        /// <summary>Gets what is asked: PROPERTIES, LAYER, OPTIONS, QSAVE.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the window did what was asked; a LAYER not handled runs its prompts on the command line.</summary>
        public bool Handled { get; set; }
    }

    /// <summary>
    /// The command line of one document, as AutoCAD's: a command started by its name or alias, its prompts answered by points
    /// picked on the canvas, text typed, objects selected, Enter and Esc, with the history of what was asked and answered. It
    /// knows no window: the canvas and the command line call <see cref="Feed(GeoPoint2)"/>, <see cref="Feed(string)"/>,
    /// <see cref="FeedSelection"/>, <see cref="Enter"/> and <see cref="Escape"/>, and a test calls them the same way.
    /// </summary>
    /// <remarks>
    /// Every edit a command makes is one undo step: the session opens a group of edits on the command's first edit and closes
    /// it when the command ends. Esc ends a command keeping what it made, as AutoCAD keeps the segments a LINE drew; MOVE,
    /// COPY and the other commands that edit at their end change nothing when Esc ends them first. A command that throws has
    /// its edits taken back.
    /// </remarks>
    public sealed class CadCommandSession
    {
        private const string Unknown = "Unknown command \"{0}\".  Press F1 for help.";

        private readonly List<string> _history = new List<string>();
        private readonly SortedSet<string> _gathered = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private IEnumerator<CadPrompt> _body;
        private CadCommandContext _context;
        private bool _grouped;
        private List<string> _script;

        /// <summary>Makes the command line of a document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="registry">The commands it knows; those of <see cref="CadCommandRegistry.CreateDefault"/> where null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadCommandSession(CadDocument document, CadCommandRegistry registry = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Registry = registry ?? CadCommandRegistry.CreateDefault();
        }

        /// <summary>Raised when the prompt changes: a command started, a prompt answered, a command ended.</summary>
        public event EventHandler PromptChanged;

        /// <summary>Raised when a command asks the window for something: PROPERTIES, LAYER, OPTIONS, QSAVE.</summary>
        public event EventHandler<CadUiRequestEventArgs> UiRequested;

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the commands it knows.</summary>
        public CadCommandRegistry Registry { get; }

        /// <summary>Gets or sets the view of the canvas, which ZOOM and PAN change; null where there is none, as in a test.</summary>
        public CadView View { get; set; }

        /// <summary>
        /// Gets or sets how near a picked point must come to an object to pick it, in drawing units: the pick box in pixels
        /// divided by the view's pixels per unit. 3 until set.
        /// </summary>
        public double PickAperture { get; set; } = 3.0;

        /// <summary>Gets or sets the tolerance of the commands' checks, as whether two points given are one; <see cref="GeometryHelper.Tolerance.Default"/> until set.</summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Default;

        /// <summary>Gets or sets where the cursor is, in drawing units, so that a number typed at a point prompt is a distance along the rubber band, as AutoCAD's direct distance entry.</summary>
        public GeoPoint2? Cursor { get; set; }

        /// <summary>Gets the command running, or null.</summary>
        public CadCommand Current { get; private set; }

        /// <summary>Gets whether a command is running.</summary>
        public bool IsRunning => Current != null;

        /// <summary>Gets the prompt waiting for an answer, or null when no command runs.</summary>
        public CadPrompt Prompt { get; private set; }

        /// <summary>Gets the text the command line shows: the prompt's, or empty when no command runs.</summary>
        public string PromptText => Prompt?.Text ?? string.Empty;

        /// <summary>Gets the lines of the history, the oldest first: each command, each prompt with its answer, each message.</summary>
        public IReadOnlyList<string> History => _history;

        /// <summary>Gets the objects a "Select objects:" prompt has gathered so far, in ordinal order, so the canvas can highlight them.</summary>
        public IReadOnlyCollection<string> Gathering => _gathered;

        /// <summary>Gets the name of the command run last, which Enter with no command running runs again.</summary>
        public string LastCommandName { get; private set; }

        /// <summary>Gets how the command run last ended; null before any.</summary>
        public CadCommandOutcome? LastOutcome { get; private set; }

        /// <summary>Gets the last point given to a prompt, which a typed @dx,dy is measured from (AutoCAD's LASTPOINT).</summary>
        public GeoPoint2 LastPoint { get; internal set; } = GeoPoint2.Origin;

        /// <summary>Starts a command by its name or alias, ending any command running first as Esc would.</summary>
        /// <param name="typed">The name or alias: MOVE, M.</param>
        /// <returns>False, with "Unknown command" in the history, where the registry knows no such command.</returns>
        public bool Start(string typed)
        {
            if (IsRunning)
            {
                Escape();
            }

            string name = (typed ?? string.Empty).Trim();
            Write("Command: " + name);
            CadCommand command = Registry.Find(name);

            if (command == null)
            {
                Write(string.Format(CultureInfo.InvariantCulture, Unknown, name));
                Changed();
                return false;
            }

            if (!string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                Write(command.Name);
            }

            var preSelected = new List<string>();

            if (command.UsesSelection)
            {
                foreach (string handle in new SortedSet<string>(Document.Selection.Handles, StringComparer.OrdinalIgnoreCase))
                {
                    if (Document.FindEntity(handle) != null)
                    {
                        preSelected.Add(handle);
                    }
                }
            }

            Current = command;
            LastCommandName = command.Name;
            _context = new CadCommandContext(this, command, preSelected);
            _gathered.Clear();

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

        /// <summary>
        /// Answers the prompt with a point picked on the canvas (already snapped). At "Select objects:" it picks the object
        /// nearest the point within <see cref="PickAperture"/>.
        /// </summary>
        /// <param name="point">The point, in drawing units.</param>
        /// <returns>Whether the prompt took it.</returns>
        public bool Feed(GeoPoint2 point)
        {
            if (Prompt == null)
            {
                return false;
            }

            switch (Prompt.Kind)
            {
                case CadPromptKind.Point:
                    return Answer(new CadInput(CadInputKind.Point, point, 0.0, null, null), Prompt.Text, point);
                case CadPromptKind.Distance:
                    if (Prompt.BasePoint == null)
                    {
                        return Invalid("Requires numeric distance or option keyword.");
                    }

                    return Answer(new CadInput(CadInputKind.Number, point, (point - Prompt.BasePoint.Value).Length, null, null), Prompt.Text, point);
                case CadPromptKind.Angle:
                    if (Prompt.BasePoint == null)
                    {
                        return Invalid("Requires valid numeric angle or second point.");
                    }

                    GeoVector2 way = point - Prompt.BasePoint.Value;
                    return Answer(new CadInput(CadInputKind.Number, point, Math.Atan2(way.Y, way.X), null, null), Prompt.Text, point);
                case CadPromptKind.Selection:
                case CadPromptKind.Entity:
                    IReadOnlyList<string> picked = Document.GetPickIndex(Document.CurrentSpace).Pick(point.X, point.Y, PickAperture);

                    if (picked.Count == 0)
                    {
                        Write(Prompt.Text);
                        Write(Prompt.Kind == CadPromptKind.Entity ? "Nothing selected." : "0 found");
                        Changed();
                        return false;
                    }

                    return FeedSelection(new[] { picked[0] });
                default:
                    return Invalid("Point is not valid here.");
            }
        }

        /// <summary>
        /// Takes text typed on the command line and Enter: with no command running, the name of one to start (or Enter alone
        /// running the last again); else the answer to the prompt, a keyword by its capital letters, a number, a point as
        /// <c>x,y</c>, <c>@dx,dy</c>, <c>@distance&lt;angle</c> or <c>distance&lt;angle</c>, a number alone at a point prompt
        /// being a distance along the rubber band towards the cursor, and ALL at "Select objects:" every object of the space.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>Whether it was taken.</returns>
        public bool Feed(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Enter();
                return true;
            }

            if (Prompt == null)
            {
                return Start(text);
            }

            string typed = Prompt.Kind == CadPromptKind.String ? text : text.Trim();
            string echo = Prompt.Text + " " + typed;

            if (Prompt.Kind != CadPromptKind.String)
            {
                string keyword = Prompt.MatchKeyword(typed);

                if (keyword != null)
                {
                    return Answer(new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null), echo, null);
                }
            }

            switch (Prompt.Kind)
            {
                case CadPromptKind.String:
                    return Answer(new CadInput(CadInputKind.Text, default, 0.0, typed, null), echo, null);
                case CadPromptKind.Integer:
                    if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole))
                    {
                        return Answer(new CadInput(CadInputKind.Number, default, whole, typed, null), echo, null);
                    }

                    return Invalid("Requires an integer value or option keyword.", echo);
                case CadPromptKind.Keyword:
                    if (Prompt.AcceptsText)
                    {
                        return Answer(new CadInput(CadInputKind.Text, default, 0.0, typed, null), echo, null);
                    }

                    return Invalid("Invalid option keyword.", echo);
                case CadPromptKind.Selection:
                case CadPromptKind.Entity:
                    if (Prompt.Kind == CadPromptKind.Selection && string.Equals(typed, "ALL", StringComparison.OrdinalIgnoreCase))
                    {
                        var all = new List<string>();

                        foreach (DxfEntity entity in Document.CurrentSpace.Block)
                        {
                            if (!string.IsNullOrEmpty(entity.Handle) && !(entity is DxfViewport viewport && viewport.Number == 1))
                            {
                                all.Add(entity.Handle);
                            }
                        }

                        Write(echo);
                        return Gather(all);
                    }

                    if (TryPoint(typed, out GeoPoint2 pickedAt))
                    {
                        return Feed(pickedAt);
                    }

                    return Invalid("*Invalid selection*", echo);
                case CadPromptKind.Point:
                    if (TryPoint(typed, out GeoPoint2 point))
                    {
                        return Answer(new CadInput(CadInputKind.Point, point, 0.0, typed, null), echo, point);
                    }

                    if (Prompt.BasePoint != null && TryNumber(typed, out double along))
                    {
                        // Direct distance entry: the distance along the rubber band, from the base point towards the cursor.
                        GeoPoint2 from = Prompt.BasePoint.Value;
                        GeoVector2 toward = Cursor.HasValue ? Cursor.Value - from : new GeoVector2(1.0, 0.0);
                        double length = toward.Length;
                        GeoVector2 unit = length > 0.0 ? toward / length : new GeoVector2(1.0, 0.0);
                        GeoPoint2 reached = from + unit * along;
                        return Answer(new CadInput(CadInputKind.Point, reached, 0.0, typed, null), echo, reached);
                    }

                    if (Prompt.AcceptsText)
                    {
                        return Answer(new CadInput(CadInputKind.Text, default, 0.0, typed, null), echo, null);
                    }

                    return Invalid(Prompt.Keywords.Count > 0 ? "Point or option keyword required." : "Invalid point.", echo);
                case CadPromptKind.Distance:
                    if (TryNumber(typed, out double distance))
                    {
                        return Answer(new CadInput(CadInputKind.Number, default, distance, typed, null), echo, null);
                    }

                    if (Prompt.BasePoint != null && TryPoint(typed, out GeoPoint2 far))
                    {
                        return Answer(new CadInput(CadInputKind.Number, far, (far - Prompt.BasePoint.Value).Length, typed, null), echo, far);
                    }

                    return Invalid("Requires numeric distance, second point, or option keyword.", echo);
                case CadPromptKind.Angle:
                    if (TryNumber(typed, out double degrees))
                    {
                        return Answer(new CadInput(CadInputKind.Number, default, degrees * Math.PI / 180.0, typed, null), echo, null);
                    }

                    if (Prompt.BasePoint != null && TryPoint(typed, out GeoPoint2 toward2))
                    {
                        GeoVector2 way = toward2 - Prompt.BasePoint.Value;
                        return Answer(new CadInput(CadInputKind.Number, toward2, Math.Atan2(way.Y, way.X), typed, null), echo, toward2);
                    }

                    return Invalid("Requires valid numeric angle or second point.", echo);
                default:
                    return Invalid("Invalid input.", echo);
            }
        }

        /// <summary>Answers "Select objects:" with objects picked by a click, a window or a crossing box; at a prompt for one object, the first.</summary>
        /// <param name="handles">The handles.</param>
        /// <returns>Whether the prompt took them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public bool FeedSelection(IEnumerable<string> handles)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (Prompt == null)
            {
                return false;
            }

            var found = new List<string>();

            foreach (string handle in handles)
            {
                if (Document.FindEntity(handle) != null)
                {
                    found.Add(handle);
                }
            }

            if (Prompt.Kind == CadPromptKind.Entity)
            {
                if (found.Count == 0)
                {
                    return Invalid("Nothing selected.");
                }

                return Answer(new CadInput(CadInputKind.Selection, default, 0.0, null, new[] { found[0] }), Prompt.Text, null);
            }

            if (Prompt.Kind != CadPromptKind.Selection)
            {
                return false;
            }

            Write(Prompt.Text);
            return Gather(found);
        }

        /// <summary>
        /// Enter or Space with nothing typed: the prompt's default, the end of a selection, the end of a command that draws on
        /// until Enter (LINE); with no command running, the command run last again.
        /// </summary>
        public void Enter()
        {
            if (Prompt == null)
            {
                if (LastCommandName != null)
                {
                    Start(LastCommandName);
                }

                return;
            }

            if (Prompt.Kind == CadPromptKind.Selection)
            {
                var handles = new List<string>(_gathered);
                _gathered.Clear();
                Answer(new CadInput(CadInputKind.Selection, default, 0.0, null, handles), Prompt.Text, null);
                return;
            }

            string keyword = Prompt.MatchKeyword(Prompt.Default);
            Answer(keyword != null ? new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null) : CadInput.Nothing, Prompt.Text, null);
        }

        /// <summary>Esc: ends the command running, keeping what it made, with "*Cancel*" in the history.</summary>
        public void Escape()
        {
            if (!IsRunning)
            {
                return;
            }

            Write(Prompt != null ? Prompt.Text + " *Cancel*" : "*Cancel*");
            Finish(CadCommandOutcome.Cancelled);
        }

        /// <summary>Writes a line into the history.</summary>
        /// <param name="line">The line.</param>
        public void Write(string line)
        {
            _history.Add(line ?? string.Empty);
        }

        internal List<DisplayBox> Views { get; } = new List<DisplayBox>();

        /// <summary>
        /// Gets the values the commands remember from one run to the next, by name, as TEXT the height last typed
        /// ("text.height"); a host sets a default here, as Global settings sets the text height of TEXT and MTEXT, their width
        /// factor ("text.widthFactor") and the height of the text of the dimensions DIM* makes ("dim.text.height").
        /// </summary>
        public Dictionary<string, double> Remembered { get; } = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        internal void EnsureGroup(string name)
        {
            if (!_grouped)
            {
                Document.BeginGroup(name);
                _grouped = true;
            }
        }

        internal CadEditResult Apply(string name, IReadOnlyList<CadEditRecord> planned, Action<CadEditScope> apply)
        {
            EnsureGroup(name);

            CadEditResult result = Document.ApplyEdit(name, planned, apply);

            if (!result.Succeeded)
            {
                Write(result.Message);
            }

            return result;
        }

        internal bool Request(string name)
        {
            var args = new CadUiRequestEventArgs(name);
            UiRequested?.Invoke(this, args);
            return args.Handled;
        }

        private bool Gather(List<string> handles)
        {
            int before = _gathered.Count;
            int duplicates = 0;

            foreach (string handle in handles)
            {
                if (!_gathered.Add(handle))
                {
                    duplicates++;
                }
            }

            string found = handles.Count.ToString(CultureInfo.InvariantCulture) + " found";

            if (duplicates > 0)
            {
                found += " (" + duplicates.ToString(CultureInfo.InvariantCulture) + " duplicate)";
            }

            if (before > 0 || duplicates > 0)
            {
                found += ", " + _gathered.Count.ToString(CultureInfo.InvariantCulture) + " total";
            }

            Write(found);
            Changed();
            return handles.Count > 0;
        }

        private bool Answer(CadInput input, string echo, GeoPoint2? point)
        {
            Write(echo);

            if (point.HasValue)
            {
                LastPoint = point.Value;
            }

            _context.Input = input;
            Advance();
            return true;
        }

        private bool Invalid(string message, string echo = null)
        {
            if (echo != null)
            {
                Write(echo);
            }

            Write(message);
            Changed();
            return false;
        }

        private void Advance()
        {
            bool more;

            try
            {
                more = _body.MoveNext();
            }
            catch (Exception exception)
            {
                Fail(exception);
                return;
            }

            if (!more)
            {
                Finish(CadCommandOutcome.Completed);
                return;
            }

            Prompt = _body.Current ?? throw new InvalidOperationException("The command " + Current.Name + " asked a null prompt.");
            Changed();
        }

        private void Fail(Exception exception)
        {
            GeometryHelperLog.Warn("CadCommandSession: the command " + Current.Name + " failed.", exception);
            Write("*Error* " + exception.Message);

            if (_grouped)
            {
                Document.CancelGroup();
                _grouped = false;
            }

            Finish(CadCommandOutcome.Failed);
        }

        private void Finish(CadCommandOutcome outcome)
        {
            if (_grouped)
            {
                Document.EndGroup();
                _grouped = false;
            }

            try
            {
                _body?.Dispose();
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn("CadCommandSession: the command " + Current?.Name + " failed to end.", exception);
            }

            bool cleared = outcome == CadCommandOutcome.Completed && _context != null && _context.ClearsSelection;
            _body = null;
            _context = null;
            Current = null;
            Prompt = null;
            _gathered.Clear();
            LastOutcome = outcome;

            if (cleared)
            {
                Document.Selection.Clear();
            }

            Changed();
            List<string> script = _script;
            _script = null;

            if (script != null && outcome == CadCommandOutcome.Completed)
            {
                RunScript(script);
            }
        }

        /// <summary>Keeps the lines of a script for the end of the SCRIPT that read them, which feeds them then.</summary>
        internal void QueueScript(List<string> lines) => _script = lines;

        /// <summary>
        /// Feeds each line of a script as typed and Enter pressed, a blank one as Enter and one that starts with a semicolon
        /// passed over as a comment, stopping at the first refused with the number of its line in the file.
        /// </summary>
        private void RunScript(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].TrimStart().StartsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Feed(lines[i]))
                {
                    Write("Script stopped at line " + (i + 1).ToString(CultureInfo.InvariantCulture) + ": " + lines[i].Trim());
                    return;
                }
            }
        }

        private void Changed() => PromptChanged?.Invoke(this, EventArgs.Empty);

        private bool TryPoint(string typed, out GeoPoint2 point)
        {
            point = default;
            string text = typed.Trim();
            bool relative = text.StartsWith("@", StringComparison.Ordinal);

            if (relative)
            {
                text = text.Substring(1).Trim();

                if (text.Length == 0)
                {
                    point = LastPoint;
                    return true;
                }
            }

            int polar = text.IndexOf('<');
            GeoVector2 offset;

            if (polar > 0)
            {
                if (!TryNumber(text.Substring(0, polar), out double length) || !TryNumber(text.Substring(polar + 1), out double degrees))
                {
                    return false;
                }

                double angle = degrees * Math.PI / 180.0;
                offset = new GeoVector2(length * Math.Cos(angle), length * Math.Sin(angle));
            }
            else
            {
                string[] parts = text.Split(',');

                if (parts.Length < 2 || parts.Length > 3 || !TryNumber(parts[0], out double x) || !TryNumber(parts[1], out double y)
                    || (parts.Length == 3 && !TryNumber(parts[2], out _)))
                {
                    return false;
                }

                offset = new GeoVector2(x, y);
            }

            GeoPoint2 from = relative ? LastPoint : GeoPoint2.Origin;
            point = from + offset;
            return true;
        }

        private static bool TryNumber(string text, out double value)
            => double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
