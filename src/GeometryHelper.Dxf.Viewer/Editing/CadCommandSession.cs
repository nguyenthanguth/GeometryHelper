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

    /// <summary>A request of a command to the window: open Properties, the Layer manager, Global settings, save, or show a drawing RECOVER opened.</summary>
    public sealed class CadUiRequestEventArgs : EventArgs
    {
        internal CadUiRequestEventArgs(string name, CadDocument document = null, object payload = null)
        {
            Name = name;
            Document = document;
            Payload = payload;
        }

        /// <summary>Gets the plot PLOT, PREVIEW, PAGESETUP or -PLOT hands the window, the <see cref="Payload"/> of those requests; null for every other request.</summary>
        public CadPlotJob Plot => Payload as CadPlotJob;

        /// <summary>
        /// Gets the image PNGOUT, JPGOUT or BMPOUT hands the window, the <see cref="Payload"/> of those requests and of
        /// IMAGEOUT; null for every other request.
        /// </summary>
        public CadImageJob Image => Payload as CadImageJob;

        /// <summary>Gets the file EXPORT or -EXPORT asks for, the <see cref="Payload"/> of those requests; null for every other request.</summary>
        public CadExportJob Export => Payload as CadExportJob;

        /// <summary>
        /// Gets what is asked: PROPERTIES, LAYER, OPTIONS, QSAVE, RECOVER, PLOT, PREVIEW, PAGESETUP, -PLOT to write a plot to a
        /// file, PNGOUT, JPGOUT or BMPOUT for the file of an image, IMAGEOUT to draw the image and write it, EXPORT or -EXPORT
        /// for the file of an export, or MATCHPROP for its Property Settings dialog, which sets the session's
        /// <see cref="CadCommandSession.MatchProperties"/>.
        /// </summary>
        public string Name { get; }

        /// <summary>Gets the document to show, for RECOVER the drawing recovered; null for the other requests.</summary>
        public CadDocument Document { get; }

        /// <summary>
        /// Gets what the command hands its dialog, which the window fills and closes with an action: a
        /// <see cref="CadBlockDefinitionRequest"/> for BLOCK, <see cref="CadInsertRequest"/> for INSERT,
        /// <see cref="CadWriteBlockRequest"/> for WBLOCK, <see cref="CadAttributeDefinitionRequest"/> for ATTDEF and
        /// <see cref="CadAttributeEditRequest"/> for EATTEDIT, <see cref="CadAttributeManagerRequest"/> for BATTMAN,
        /// <see cref="CadAttributeValuesRequest"/> for ATTDIA (the Edit Attributes dialog of INSERT),
        /// <see cref="CadPurgeRequest"/> for PURGE and <see cref="CadGroupManagerRequest"/> for CLASSICGROUP; for REDEFINE,
        /// which INSERT asks when the block chosen from another drawing has a namesake in this one, the name of the block, "Yes"
        /// in <see cref="Answer"/> redefining it; for PURGEITEM, which PURGE asks before each object it purges, the question,
        /// "Yes" or "No" in <see cref="Answer"/>, null stopping the purge; null for the other requests.
        /// </summary>
        public object Payload { get; }

        /// <summary>Gets or sets whether the window did what was asked; a LAYER not handled runs its prompts on the command line.</summary>
        public bool Handled { get; set; }

        /// <summary>
        /// Gets or sets what the window's dialog answered, for a command that goes on with it: for PASTESPEC the paste chosen,
        /// PASTECLIP, PASTEBLOCK, PASTEORIG or TEXT; for REDEFINE Yes or No; null where it was cancelled or asks nothing.
        /// </summary>
        public string Answer { get; set; }
    }

    /// <summary>
    /// The command line of one document, as AutoCAD's: a command started by its name or alias, its prompts answered by points
    /// picked on the canvas, text typed, objects selected, Enter and Esc, with the history of what was asked and answered. It
    /// knows no window: the canvas and the command line call <see cref="Feed(GeoPoint2)"/>, <see cref="Feed(string)"/>,
    /// <see cref="FeedSelection(IEnumerable{string}, bool)"/>, <see cref="Enter"/> and <see cref="Escape"/>, and a test calls them the same way.
    /// </summary>
    /// <remarks>
    /// Every edit a command makes is one undo step: the session opens a group of edits on the command's first edit and closes
    /// it when the command ends. Esc ends a command keeping what it made, as AutoCAD keeps the segments a LINE drew; MOVE,
    /// COPY and the other commands that edit at their end change nothing when Esc ends them first. A command that throws has
    /// its edits taken back.
    /// </remarks>
    public sealed partial class CadCommandSession
    {
        private const string Unknown = "Unknown command \"{0}\".  Press F1 for help.";

        private readonly List<string> _history = new List<string>();
        private readonly SortedSet<string> _gathered = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<KeyValuePair<string, GeoPoint2>> _clicks = new List<KeyValuePair<string, GeoPoint2>>();
        private readonly SortedSet<string> _whole = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        // The objects a window or crossing box took, each with the box and whether it crossed, and those another option took
        // whole all the same: -ATTEDIT takes only the attributes a window took.
        private readonly List<Tuple<string, DisplayBox, bool>> _windows = new List<Tuple<string, DisplayBox, bool>>();
        private readonly SortedSet<string> _unwindowed = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private IEnumerator<CadPrompt> _body;
        private CadCommandContext _context;

        // The snap of the point being fed by Feed(CadSnapResult), which the point answer carries; null otherwise.
        private CadSnapResult _snapFed;
        private CadCommand _gripCommand;
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

        /// <summary>The view of the canvas in the units of the space worked in: through the viewport worked in, in model units; else <see cref="View"/>.</summary>
        internal CadView WorkView =>
            View != null && Document.ActiveViewport is DxfViewport viewport
                ? CadViewports.ModelView(View, viewport)
                : View;

        /// <summary>
        /// Gets or sets how near a picked point must come to an object to pick it, in drawing units: the pick box in pixels
        /// divided by the view's pixels per unit. 3 until set.
        /// </summary>
        public double PickAperture { get; set; } = 3.0;

        /// <summary>Gets or sets the tolerance of the commands' checks, as whether two points given are one; <see cref="GeometryHelper.Tolerance.Default"/> until set.</summary>
        public Tolerance Tolerance { get; set; } = Tolerance.Default;

        /// <summary>
        /// Gets or sets the clipboard COPYCLIP, CUTCLIP and COPYBASE put objects on and the PASTE commands take them from: one in
        /// memory of this session's own until set; the window sets the Windows clipboard, so that objects go between its drawings
        /// and other copies of the app.
        /// </summary>
        public ICadClipboard Clipboard { get; set; } = new CadMemoryClipboard();

        /// <summary>
        /// Gets or sets the properties MATCHPROP copies, as the check boxes of its Property Settings dialog: every one until
        /// changed, as in AutoCAD. With Layer alone left checked, MATCHPROP moves the objects to the source's layer and keeps
        /// their colour.
        /// </summary>
        public CadMatchProperties MatchProperties { get; set; } = CadMatchProperties.All;

        /// <summary>
        /// Gets what the Block Attribute Manager of BATTMAN keeps from one run to the next, as its Settings dialog sets it:
        /// the columns of its grid, tags given twice drawn in red, and whether Apply brings the INSERTs in line at once.
        /// </summary>
        public CadAttributeManagerSettings AttributeManager { get; } = new CadAttributeManagerSettings();

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

        /// <summary>
        /// Gets the user coordinate system the prompts work in, the document's <see cref="CadUcsManager.Working"/>: a point typed
        /// as <c>x,y</c>, <c>@dx,dy</c> or <c>@distance&lt;angle</c> is in it, one typed after a <c>*</c> in the world's, and an
        /// angle given is measured from its X axis. Points given to the commands are world points all the same: in a system
        /// turned a quarter turn, <c>@100&lt;0</c> from (0, 0) is the world point (0, 100).
        /// </summary>
        public CadUcs Ucs => Document.Ucs.Working;

        /// <summary>Starts a command by its name or alias, ending any command running first as Esc would.</summary>
        /// <param name="typed">The name or alias: MOVE, M.</param>
        /// <returns>False, with "Unknown command" in the history, where the registry knows no such command; false, with "Not
        /// allowed in the Block Editor.", for a command that leaves the block while the Block Editor is open (BEDIT, XREF,
        /// LAYOUT, PLOT and the others).</returns>
        public bool Start(string typed)
        {
            if (IsRunning)
            {
                Escape();
            }

            string name = (typed ?? string.Empty).Trim();
            Write("Command: " + name);
            CadCommand command = Registry.Find(name);

            // A system variable's name typed alone asks its new value, as SETVAR does, where no command has the name.
            if (command == null && Registry.FindVariable(name) is CadSystemVariable variable)
            {
                command = new CadCommand(variable.Name, c => CadCommands.TypedVariable(c, variable));
            }

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

            // The Block Editor refuses what leaves the block or works on another space, as AutoCAD's.
            if (Document.BlockEditor != null && CadCommands.LeavesBlockEditor(command.Name))
            {
                Write(CadBlockEditor.NotAllowed);
                Changed();
                return false;
            }

            var preSelected = new List<string>();

            // With PICKFIRST 0 a command asks for its objects whatever was selected before it, as AutoCAD's.
            if (command.UsesSelection && Document.PickFirst != 0)
            {
                foreach (
                    string handle in new SortedSet<string>(Document.Selection.Handles, StringComparer.OrdinalIgnoreCase)
                )
                {
                    if (Document.FindEntity(handle) != null)
                    {
                        preSelected.Add(handle);
                    }
                }
            }

            LastCommandName = command.Name;
            Run(command, preSelected);
            return true;
        }

        /// <summary>
        /// Starts a grip edit, as a click on a grip starts AutoCAD's with no command running: the grips given are hot, the first
        /// the one clicked, and the command line asks "** STRETCH **" and "Specify stretch point or [Base point/Copy/Undo/eXit]:".
        /// Enter or Space goes on to MOVE, ROTATE, SCALE and MIRROR and back to STRETCH; STRETCH, MOVE, ROTATE, SCALE and MIRROR,
        /// or ST, MO, RO, SC and MI, typed go to that mode. STRETCH moves the hot grips together by the way from the base point,
        /// the first hot grip until Base point gives another; the other modes move, turn, scale or mirror the objects selected.
        /// Copy leaves the originals and asks again until eXit, Enter or Esc, as does a first point picked with Ctrl held
        /// (<see cref="Feed(GeoPoint2, bool, bool)"/>). The whole grip edit is one undo step; the command run last stays the one
        /// Enter runs again.
        /// </summary>
        /// <param name="hotGrips">The hot grips, each the handle of an entity and the index of its grip as <see cref="CadGrips.Of"/> gives it.</param>
        /// <returns>False, starting nothing, where a command is running or no grip given is one of an entity of the drawing.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the grips are null.</exception>
        public bool StartGripEdit(IEnumerable<KeyValuePair<string, int>> hotGrips)
        {
            if (hotGrips == null)
            {
                throw new ArgumentNullException(nameof(hotGrips));
            }

            if (IsRunning)
            {
                return false;
            }

            var hot = new List<KeyValuePair<string, int>>();

            foreach (KeyValuePair<string, int> grip in hotGrips)
            {
                DxfEntity entity = Document.FindEntity(grip.Key);

                if (entity != null && grip.Value >= 0 && grip.Value < CadGrips.Of(entity).Count && !hot.Contains(grip))
                {
                    hot.Add(grip);
                }
            }

            if (hot.Count == 0)
            {
                return false;
            }

            _gripCommand = new CadCommand("GRIP", c => CadCommands.GripEdit(c, hot));
            Run(_gripCommand, Array.Empty<string>());
            return true;
        }

        /// <summary>Gets whether the command running is a grip edit (<see cref="StartGripEdit"/>), whose hot grips the canvas keeps showing.</summary>
        public bool IsGripEditing => Current != null && ReferenceEquals(Current, _gripCommand);

        /// <summary>Runs a command's body from its first prompt.</summary>
        private void Run(CadCommand command, IReadOnlyList<string> preSelected)
        {
            Current = command;
            _context = new CadCommandContext(this, command, preSelected);
            ClearGathering();

            try
            {
                _body = command.Body(_context).GetEnumerator();
            }
            catch (Exception exception)
            {
                Fail(exception);
                return;
            }

            Advance();
        }

        /// <summary>
        /// Answers the prompt with a point picked on the canvas (already snapped). At "Select objects:" it picks the object
        /// nearest the point within <see cref="PickAperture"/>.
        /// </summary>
        /// <param name="point">The point, in drawing units.</param>
        /// <returns>Whether the prompt took it.</returns>
        public bool Feed(GeoPoint2 point) => Feed(point, false);

        /// <summary>
        /// Answers the prompt with a point picked on the canvas, as <see cref="Feed(GeoPoint2)"/> does, saying whether Shift was
        /// held: at a prompt for one object the answer carries it (<see cref="CadInput.Shift"/>), as TRIM extends an object
        /// picked with Shift held, and EXTEND trims one; at "Select objects:" the object picked is taken out of the objects
        /// gathered, as AutoCAD takes it out. At any other prompt Shift changes nothing.
        /// </summary>
        /// <param name="point">The point, in drawing units.</param>
        /// <param name="shift">Whether Shift was held as the point was picked.</param>
        /// <returns>Whether the prompt took it.</returns>
        public bool Feed(GeoPoint2 point, bool shift) => Feed(point, shift, false);

        /// <summary>
        /// Answers the prompt with a point picked on the canvas, as <see cref="Feed(GeoPoint2, bool)"/> does, saying whether Ctrl
        /// was held too: at a prompt for a point, a distance or an angle the answer carries it (<see cref="CadInput.Control"/>), as
        /// ARC draws an arc whose end is picked with Ctrl held clockwise rather than counter-clockwise, and a grip edit takes its
        /// first point picked with Ctrl held as Copy.
        /// </summary>
        /// <param name="point">The point, in drawing units.</param>
        /// <param name="shift">Whether Shift was held as the point was picked.</param>
        /// <param name="control">Whether Ctrl was held as the point was picked.</param>
        /// <returns>Whether the prompt took it.</returns>
        public bool Feed(GeoPoint2 point, bool shift, bool control)
        {
            if (Prompt == null)
            {
                return false;
            }

            if (_option != null)
            {
                return OptionPoint(point, Prompt.Text);
            }

            if (_multiple != null && Prompt.Kind == CadPromptKind.Selection)
            {
                // Multiple: the points are kept, and the objects at them found at Enter.
                Write(Prompt.Text);
                _multiple.Add(point);
                Changed();
                return true;
            }

            switch (Prompt.Kind)
            {
                case CadPromptKind.Point:
                case CadPromptKind.Distance:
                case CadPromptKind.Angle:
                    return AnswerPoint(point, Prompt.Text, control);
                case CadPromptKind.Selection:
                case CadPromptKind.Entity:
                    IReadOnlyList<string> picked = Document
                        .GetPickIndex(Document.WorkingSpace)
                        .Pick(point.X, point.Y, PickAperture);

                    if (picked.Count == 0)
                    {
                        Write(Prompt.Text);
                        Write(Prompt.Kind == CadPromptKind.Entity ? "Nothing selected." : "0 found");
                        Changed();
                        return false;
                    }

                    if (Prompt.Kind == CadPromptKind.Entity && Document.FindEntity(picked[0]) != null)
                    {
                        // The point the object was picked at goes with it, as UCS OBject takes the end of a line nearer the pick.
                        return Answer(
                            new CadInput(CadInputKind.Selection, point, 0.0, null, new[] { picked[0] })
                            {
                                Shift = shift,
                                IsPickedAt = true,
                            },
                            Prompt.Text,
                            null
                        );
                    }

                    return TakeSelection(new[] { picked[0] }, point, shift, null, false);
                default:
                    return Invalid("Point is not valid here.");
            }
        }

        /// <summary>
        /// Answers the prompt with a point an object snap gave on the canvas, as <see cref="Feed(GeoPoint2, bool, bool)"/> answers
        /// with its point, keeping which snap and which object gave it: a dimension made with DIMASSOC 2 ties the point to the
        /// object, as AutoCAD ties a point picked with ENDpoint, MIDpoint, CENter, QUAdrant, NODe or NEArest. The end of a line
        /// snapped with ENDpoint at the first prompt of DIMLINEAR ties the dimension's first point to that end.
        /// </summary>
        /// <param name="snapped">The snap's result, as <see cref="ObjectSnap.Snap"/> gives it.</param>
        /// <param name="control">Whether Ctrl was held as the point was picked.</param>
        /// <returns>Whether the prompt took it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the result is null.</exception>
        public bool Feed(CadSnapResult snapped, bool control)
        {
            _snapFed = snapped ?? throw new ArgumentNullException(nameof(snapped));

            try
            {
                return Feed(snapped.Point, false, control);
            }
            finally
            {
                _snapFed = null;
            }
        }

        /// <summary>
        /// Takes text typed on the command line and Enter: with no command running, the name of one to start (or Enter alone
        /// running the last again); else the answer to the prompt, a keyword by its capital letters, a number, a point as
        /// <c>x,y</c>, <c>@dx,dy</c>, <c>@distance&lt;angle</c> or <c>distance&lt;angle</c>, a number alone at a point prompt
        /// being a distance along the rubber band towards the cursor. At "Select objects:" the options of AutoCAD 2021: ALL
        /// every object of the space, Window, Crossing and BOX by two corners, Fence, WPolygon and CPolygon by points until
        /// Enter, Last the object made last, Previous the objects the command before took, Group by name, Remove and Add to
        /// take out and put in, Undo the last change, Multiple points until Enter, SIngle to end with the first objects taken,
        /// AUto; anything else is "*Invalid selection*" with the options listed.
        /// A point modifier, FROM, M2P, TT, TK or a point filter, starts at a prompt for a point (<see cref="StartPointModifier"/>).
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

            if (_option != null)
            {
                return OptionText(text.Trim());
            }

            string typed = Prompt.Kind == CadPromptKind.String ? text : text.Trim();
            string echo = _refeeding ? null : Prompt.Text + " " + typed;

            if (Prompt.Kind != CadPromptKind.String)
            {
                string keyword = Prompt.MatchKeyword(typed);

                if (keyword != null)
                {
                    return Answer(new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null), echo, null);
                }
            }

            if (StartPointModifier(typed))
            {
                return true;
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
                    if (Prompt.Kind == CadPromptKind.Selection)
                    {
                        string option = Options.MatchKeyword(typed);

                        if (option != null)
                        {
                            return SelectOption(option, echo);
                        }
                    }

                    if (TryPoint(typed, out GeoPoint2 pickedAt))
                    {
                        return Feed(pickedAt);
                    }

                    return Prompt.Kind == CadPromptKind.Selection
                        ? InvalidSelection(echo)
                        : Invalid("*Invalid selection*", echo);
                case CadPromptKind.Point:
                    if (TryNeeded(typed, out GeoPoint2 needed))
                    {
                        return Answer(new CadInput(CadInputKind.Point, needed, 0.0, typed, null), echo, needed);
                    }

                    if (TryPoint(typed, out GeoPoint2 point))
                    {
                        return Answer(new CadInput(CadInputKind.Point, point, 0.0, typed, null), echo, point);
                    }

                    if (Prompt.BasePoint != null && TryLength(typed, out double along))
                    {
                        // Direct distance entry: the distance along the rubber band, from the base point towards the cursor.
                        GeoPoint2 from = Prompt.BasePoint.Value;
                        GeoVector2 toward = Cursor.HasValue
                            ? (IsTrackingOn() ? Square(from, Cursor.Value) : Cursor.Value) - from
                            : new GeoVector2(1.0, 0.0);
                        double length = toward.Length;
                        GeoVector2 unit = length > 0.0 ? toward / length : new GeoVector2(1.0, 0.0);
                        GeoPoint2 reached = from + unit * along;
                        return Answer(new CadInput(CadInputKind.Point, reached, 0.0, typed, null), echo, reached);
                    }

                    if (Prompt.AcceptsText)
                    {
                        return Answer(new CadInput(CadInputKind.Text, default, 0.0, typed, null), echo, null);
                    }

                    if (_modifiers.Count > 0)
                    {
                        return MissModifier(typed, echo);
                    }

                    return Invalid(
                        Prompt.Keywords.Count > 0 ? "Point or option keyword required." : "Invalid point.",
                        echo
                    );
                case CadPromptKind.Distance:
                    if (TryLength(typed, out double distance))
                    {
                        return Answer(new CadInput(CadInputKind.Number, default, distance, typed, null), echo, null);
                    }

                    if (Prompt.BasePoint != null && TryPoint(typed, out GeoPoint2 far))
                    {
                        return Answer(
                            new CadInput(CadInputKind.Number, far, (far - Prompt.BasePoint.Value).Length, typed, null),
                            echo,
                            far
                        );
                    }

                    return Invalid("Requires numeric distance, second point, or option keyword.", echo);
                case CadPromptKind.Angle:
                    if (TryAngle(typed, Prompt, out double radians))
                    {
                        return Answer(new CadInput(CadInputKind.Number, default, radians, typed, null), echo, null);
                    }

                    if (Prompt.BasePoint != null && TryPoint(typed, out GeoPoint2 toward2))
                    {
                        GeoVector2 way = toward2 - Prompt.BasePoint.Value;
                        return Answer(
                            new CadInput(CadInputKind.Number, toward2, PickedAngle(Prompt, way), typed, null),
                            echo,
                            toward2
                        );
                    }

                    return Invalid("Requires valid numeric angle or second point.", echo);
                default:
                    return Invalid("Invalid input.", echo);
            }
        }

        /// <summary>
        /// Answers "Select objects:" with objects picked by a click, a window or a crossing box, or with Shift held takes them
        /// out of those gathered: "1 found, 1 removed, 2 total". At "Remove objects:" they are taken out either way. At a prompt
        /// for one object, the first.
        /// </summary>
        /// <param name="handles">The handles.</param>
        /// <param name="remove">Whether they are taken out, as a pick or a box with Shift held takes them out.</param>
        /// <returns>Whether the prompt took them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public bool FeedSelection(IEnumerable<string> handles, bool remove = false) =>
            TakeSelection(handles, null, remove, null, false);

        /// <summary>
        /// Answers "Select objects:" with the objects a window or crossing box dragged on the canvas took, as
        /// <see cref="FeedSelection(IEnumerable{string}, bool)"/> does, the box kept with them: -ATTEDIT then takes only the
        /// attributes of an INSERT that lie inside the window, or that the crossing box touches, as AutoCAD's does.
        /// </summary>
        /// <param name="handles">The handles the box took.</param>
        /// <param name="window">The box, in the coordinates of the space.</param>
        /// <param name="crossing">Whether the box was a crossing one (dragged right to left), which takes what it touches.</param>
        /// <param name="remove">Whether they are taken out, as a box with Shift held takes them out.</param>
        /// <returns>Whether the prompt took them.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the handles are null.</exception>
        public bool FeedSelection(IEnumerable<string> handles, DisplayBox window, bool crossing, bool remove = false) =>
            TakeSelection(handles, null, remove, window, crossing);

        /// <summary>Answers "Select objects:" with objects, a click's point with them where one picked them, or a window's box, or takes them out.</summary>
        private bool TakeSelection(
            IEnumerable<string> handles,
            GeoPoint2? clicked,
            bool remove,
            DisplayBox? window,
            bool crossing
        )
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

                return Answer(
                    new CadInput(CadInputKind.Selection, default, 0.0, null, new[] { found[0] }),
                    Prompt.Text,
                    null
                );
            }

            if (Prompt.Kind != CadPromptKind.Selection)
            {
                return false;
            }

            Write(Prompt.Text);
            var gathered = new List<string>(Document.WithGroups(found));

            // With PICKADD 0 what is picked last replaces those gathered, and Shift adds to them, as AutoCAD's.
            if (Document.PickAdd == 0 && !_removing)
            {
                if (!remove)
                {
                    ClearGathering();
                }

                remove = false;
            }

            if (remove || _removing)
            {
                return Gather(gathered, true);
            }

            // The object a click picked is kept with the click's point, as -ATTEDIT takes the one attribute clicked; any
            // other way of selecting takes an object whole.
            foreach (string handle in gathered)
            {
                if (clicked.HasValue && string.Equals(handle, found[0], StringComparison.OrdinalIgnoreCase))
                {
                    _clicks.Add(new KeyValuePair<string, GeoPoint2>(handle, clicked.Value));
                }
                else
                {
                    _whole.Add(handle);

                    if (window.HasValue)
                    {
                        _windows.Add(Tuple.Create(handle, window.Value, crossing));
                    }
                    else
                    {
                        _unwindowed.Add(handle);
                    }
                }
            }

            return Gather(gathered, false);
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

            if (_option != null)
            {
                OptionEnter();
                return;
            }

            if (Prompt.Kind == CadPromptKind.Selection && _multiple != null)
            {
                MultipleEnter();
                return;
            }

            if (Prompt.Kind == CadPromptKind.Selection)
            {
                EndSelection();
                return;
            }

            string keyword = Prompt.MatchKeyword(Prompt.Default);
            Answer(
                keyword != null ? new CadInput(CadInputKind.Keyword, default, 0.0, keyword, null) : CadInput.Nothing,
                Prompt.Text,
                null
            );
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
            if (Quiet)
            {
                return;
            }

            _history.Add(line ?? string.Empty);
        }

        /// <summary>Whether lines written are dropped, as they are while a preview runs the command's code at the cursor.</summary>
        internal bool Quiet { get; set; }

        /// <summary>
        /// Ctrl+Shift+A: switches the picking of groups whole (<see cref="CadDocument.PickStyle"/>) off where it is on and on
        /// where it is off, writing "&lt;Group off&gt;" or "&lt;Group on&gt;" as AutoCAD does; a command running goes on.
        /// </summary>
        public void TogglePickStyle()
        {
            bool on = (Document.PickStyle & 1) == 0;
            Document.PickStyle = on ? Document.PickStyle | 1 : Document.PickStyle & ~1;
            Write(on ? "<Group on>" : "<Group off>");
            Changed();
        }

        /// <summary>
        /// Switches selection cycling (<see cref="CadDocument.SelectionCycling"/>) off, or on again as 2, writing
        /// "&lt;Selection Cycling off&gt;" or "&lt;Selection Cycling on&gt;" as AutoCAD does.
        /// </summary>
        public void ToggleSelectionCycling()
        {
            bool on = Document.SelectionCycling == 0;
            Document.SelectionCycling = on ? 2 : 0;
            Write(on ? "<Selection Cycling on>" : "<Selection Cycling off>");
            Changed();
        }

        /// <summary>The views ZOOM Previous goes back to: a list for each space and for each viewport of a layout.</summary>
        internal CadViewHistory Views { get; } = new CadViewHistory();

        /// <summary>The system variable SETVAR was given last, which it offers next.</summary>
        internal string LastVariable { get; set; }

        /// <summary>The block INSERT placed last, which -INSERT offers next, as AutoCAD's INSNAME.</summary>
        internal string LastBlock { get; set; }

        /// <summary>The layer new dimensions are put on, as AutoCAD's DIMLAYER that DIM's LAyer sets; null for the current layer.</summary>
        internal string DimLayer { get; set; }

        /// <summary>
        /// Gets the values the commands remember from one run to the next, by name, as TEXT the height last typed
        /// ("text.height"); a host sets a default here, as Global settings sets the text height of TEXT and MTEXT, their width
        /// factor ("text.widthFactor") and the height of the text of the dimensions DIM* makes ("dim.text.height").
        /// </summary>
        public Dictionary<string, double> Remembered { get; } =
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether a command asks the window for a file by its file dialog, as AutoCAD's FILEDIA 1, the default; FILEDIA 0
        /// typed makes PNGOUT, JPGOUT, BMPOUT and the window's SAVEAS ask "Enter file name:" or "Save drawing as" on the
        /// command line instead, as AutoCAD does in a script.
        /// </summary>
        public bool FileDialogs => !(Remembered.TryGetValue("filedia", out double value) && value == 0.0);

        /// <summary>
        /// Gets the way the last segment drawn ran at its end, a line or an arc of LINE, ARC or a continued arc, as a unit
        /// vector: what ARC's Enter continues along, and what the window counts polar angles from under POLARMODE 1; null
        /// where none has been drawn. After a line from (0, 0) to (30, 40) it is (0.6, 0.8).
        /// </summary>
        public GeoVector2? LastSegment =>
            Remembered.TryGetValue("draw.last", out double drawn)
            && drawn != 0.0
            && Remembered.TryGetValue("draw.last.dx", out double dx)
            && Remembered.TryGetValue("draw.last.dy", out double dy)
                ? new GeoVector2(dx, dy)
                : (GeoVector2?)null;

        internal void EnsureGroup(string name)
        {
            if (!_grouped)
            {
                Document.BeginGroup(name);
                _grouped = true;
            }
        }

        /// <summary>Closes the command's undo step opened so far, so that the edits it makes next are a step of their own, as TEXTEDIT makes one step of each text.</summary>
        internal void CloseGroup()
        {
            if (_grouped)
            {
                Document.EndGroup();
                _grouped = false;
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

        internal bool Request(string name, CadDocument document = null, object payload = null) =>
            Ask(name, document, payload).Handled;

        /// <summary>Asks the window for something and gives back what it did and answered.</summary>
        internal CadUiRequestEventArgs Ask(string name, CadDocument document = null, object payload = null)
        {
            var args = new CadUiRequestEventArgs(name, document, payload);
            UiRequested?.Invoke(this, args);
            return args;
        }

        /// <summary>Forgets the objects "Select objects:" gathered and the clicks that picked them.</summary>
        private void ClearGathering()
        {
            _gathered.Clear();
            _clicks.Clear();
            _whole.Clear();
            _windows.Clear();
            _unwindowed.Clear();
        }

        /// <summary>The end of "Select objects:": the objects gathered answer it, kept for Previous.</summary>
        private void EndSelection()
        {
            var handles = new List<string>(_gathered);
            List<KeyValuePair<string, GeoPoint2>> clicks = _clicks.FindAll(p => !_whole.Contains(p.Key));
            List<Tuple<string, DisplayBox, bool>> windows = _windows.FindAll(w =>
                _gathered.Contains(w.Item1) && !_unwindowed.Contains(w.Item1)
            );
            string echo = Prompt.Text;
            ClearGathering();
            RememberSelection(handles);
            ResetOptions();
            Answer(
                new CadInput(CadInputKind.Selection, default, 0.0, null, handles)
                {
                    Clicks = clicks,
                    Windows = windows,
                },
                echo,
                null
            );
        }

        /// <summary>
        /// Adds objects to those gathered, "2 found (1 duplicate), 3 total", or takes them out, "1 found, 1 removed, 2 total";
        /// the change kept for Undo. With SIngle, the first objects added end the selection.
        /// </summary>
        private bool Gather(List<string> handles, bool remove)
        {
            int before = _gathered.Count;
            int duplicates = 0;
            var changed = new List<string>();

            foreach (string handle in handles)
            {
                if (remove ? _gathered.Remove(handle) : _gathered.Add(handle))
                {
                    changed.Add(handle);
                }
                else if (!remove)
                {
                    duplicates++;
                }
            }

            if (remove)
            {
                foreach (string handle in changed)
                {
                    _whole.Remove(handle);
                    _unwindowed.Remove(handle);
                    _clicks.RemoveAll(p => string.Equals(p.Key, handle, StringComparison.OrdinalIgnoreCase));
                    _windows.RemoveAll(w => string.Equals(w.Item1, handle, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (changed.Count > 0)
            {
                _steps.Add(
                    new KeyValuePair<List<string>, List<string>>(
                        remove ? new List<string>() : changed,
                        remove ? changed : new List<string>()
                    )
                );
            }

            string found = handles.Count.ToString(CultureInfo.InvariantCulture) + " found";

            if (remove)
            {
                found +=
                    ", "
                    + changed.Count.ToString(CultureInfo.InvariantCulture)
                    + " removed, "
                    + _gathered.Count.ToString(CultureInfo.InvariantCulture)
                    + " total";
            }
            else
            {
                if (duplicates > 0)
                {
                    found += " (" + duplicates.ToString(CultureInfo.InvariantCulture) + " duplicate)";
                }

                if (before > 0 || duplicates > 0)
                {
                    found += ", " + _gathered.Count.ToString(CultureInfo.InvariantCulture) + " total";
                }
            }

            Write(found);

            if (_single && !remove && handles.Count > 0 && Prompt != null && Prompt.Kind == CadPromptKind.Selection)
            {
                EndSelection();
                return true;
            }

            Changed();
            return handles.Count > 0;
        }

        /// <summary>
        /// Answers a prompt for a point, a distance or an angle with a point: a distance or an angle measured from the prompt's
        /// base point. With no echo the history is left as it is, as for the point a modifier (FROM, M2P) gives.
        /// </summary>
        private bool AnswerPoint(GeoPoint2 point, string echo, bool control = false)
        {
            switch (Prompt.Kind)
            {
                case CadPromptKind.Point:
                    return Answer(
                        new CadInput(CadInputKind.Point, point, 0.0, null, null) { Control = control, Snap = _snapFed },
                        echo,
                        point
                    );
                case CadPromptKind.Distance:
                    if (Prompt.BasePoint == null)
                    {
                        return Invalid("Requires numeric distance or option keyword.");
                    }

                    return Answer(
                        new CadInput(CadInputKind.Number, point, (point - Prompt.BasePoint.Value).Length, null, null)
                        {
                            Control = control,
                        },
                        echo,
                        point
                    );
                default:
                    if (Prompt.BasePoint == null)
                    {
                        return Invalid("Requires valid numeric angle or second point.");
                    }

                    GeoVector2 way = point - Prompt.BasePoint.Value;
                    return Answer(
                        new CadInput(CadInputKind.Number, point, PickedAngle(Prompt, way), null, null)
                        {
                            Control = control,
                        },
                        echo,
                        point
                    );
            }
        }

        private bool Answer(CadInput input, string echo, GeoPoint2? point)
        {
            // The points a prompt that follows the cursor takes are not written, as AutoCAD writes none of the path.
            if (echo != null && !(Prompt.Tracks && input.IsPoint))
            {
                Write(echo);
            }

            if (point.HasValue)
            {
                LastPoint = point.Value;
            }

            // The prompt of a point modifier (Base point:, First point of mid:) takes the answer in place of the command.
            if (_modifiers.Count > 0)
            {
                return TakeModifierInput(input);
            }

            _trackPoints.Clear();
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

            Prompt = _body.Current
                ?? throw new InvalidOperationException("The command " + Current.Name + " asked a null prompt.");

            if (Prompt.Kind == CadPromptKind.Selection)
            {
                ResetOptions();
            }

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
                GeometryHelperLog.Warn(
                    "CadCommandSession: the command " + Current?.Name + " failed to end.",
                    exception
                );
            }

            bool cleared = outcome == CadCommandOutcome.Completed && _context != null && _context.ClearsSelection;
            _body = null;
            _context = null;
            Current = null;
            Prompt = null;
            _modifiers.Clear();
            _trackPoints.Clear();
            ClearGathering();
            ResetOptions();
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
                    Write(
                        "Script stopped at line "
                            + (i + 1).ToString(CultureInfo.InvariantCulture)
                            + ": "
                            + lines[i].Trim()
                    );
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

            // A * before the numbers gives them in world coordinates, as AutoCAD's *10,20 and @*5,0; else they are the current UCS's.
            bool world = text.StartsWith("*", StringComparison.Ordinal);
            text = world ? text.Substring(1).Trim() : text;
            CadUcs ucs = world ? CadUcs.World : Ucs;
            int polar = text.IndexOf('<');
            GeoVector2 offset;

            if (polar > 0)
            {
                if (
                    !TryLength(text.Substring(0, polar), out double length)
                    || !TryAngle(text.Substring(polar + 1), null, out double angle)
                )
                {
                    return false;
                }

                offset = new GeoVector2(length * Math.Cos(angle), length * Math.Sin(angle));
            }
            else
            {
                string[] parts = text.Split(',');

                if (
                    parts.Length < 2
                    || parts.Length > 3
                    || !TryLength(parts[0], out double x)
                    || !TryLength(parts[1], out double y)
                    || (parts.Length == 3 && !TryLength(parts[2], out _))
                )
                {
                    return false;
                }

                offset = new GeoVector2(x, y);
            }

            point = relative ? LastPoint + ucs.ToWorld(offset) : ucs.ToWorld(GeoPoint2.Origin + offset);
            return true;
        }

        private static bool TryNumber(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        /// <summary>A length typed, read in the drawing's LUNITS: feet and inches in engineering and architectural units, as AutoCAD reads them.</summary>
        private bool TryLength(string text, out double value) =>
            CadUnits.TryLength(text, Document.Database.Lunits, out value);

        /// <summary>
        /// An angle typed, read in the drawing's AUNITS: grads or radians where they are its units, and any angle with its own
        /// mark. It is counted as the drawing counts angles, from ANGBASE and clockwise with ANGDIR 1, as AutoCAD counts
        /// @100&lt;30: at a prompt for an amount turned (<see cref="CadPrompt.AsksTurn"/>) ANGDIR alone turns it, and at one
        /// taken as typed neither does. A &lt; before it, as in @100&lt;&lt;30, is AutoCAD's override: decimal degrees from east,
        /// counter-clockwise; two, as in @100&lt;&lt;&lt;30, the drawing's units from east, counter-clockwise.
        /// </summary>
        /// <param name="text">The text after the &lt; of a polar point, or the answer to a prompt for an angle.</param>
        /// <param name="prompt">The prompt for an angle answered; null for the angle of a polar point.</param>
        /// <param name="radians">The angle, in radians, counter-clockwise in the UCS.</param>
        private bool TryAngle(string text, CadPrompt prompt, out double radians)
        {
            string typed = text.Trim();
            DxfDatabase database = Document.Database;

            // A polar point has one < before its angle already; a prompt for an angle takes << and <<< whole.
            int marks = prompt == null ? 1 : 0;

            while (marks < 3 && typed.StartsWith("<", StringComparison.Ordinal))
            {
                typed = typed.Substring(1).TrimStart();
                marks++;
            }

            if (marks > 1)
            {
                return CadUnits.TryAngle(typed, marks == 2 ? 0 : database.Aunits, out radians);
            }

            if (!CadUnits.TryAngle(typed, database.Aunits, out radians))
            {
                return false;
            }

            if (prompt == null || !prompt.AsksPlainAngle)
            {
                radians = CadUnits.ToDirection(
                    radians,
                    prompt != null && prompt.AsksTurn ? 0.0 : database.Angbase,
                    database.Angdir
                );
            }

            return true;
        }

        /// <summary>
        /// The angle a point picked answers a prompt for an angle with: its direction from the base point, counter-clockwise
        /// from the X axis of the UCS, and for an amount turned (<see cref="CadPrompt.AsksTurn"/>) from ANGBASE, as AutoCAD's
        /// ROTATE turns nothing where the point is picked along the base direction.
        /// </summary>
        /// <param name="prompt">The prompt answered.</param>
        /// <param name="way">The way from the base point to the point picked, in world coordinates.</param>
        /// <returns>The angle, in radians.</returns>
        internal double PickedAngle(CadPrompt prompt, GeoVector2 way)
        {
            double direction = Ucs.AngleToUcs(Math.Atan2(way.Y, way.X));
            return prompt != null && prompt.AsksTurn ? direction - Document.Database.Angbase : direction;
        }

        /// <summary>
        /// Counts a direction as the drawing counts angles, for a readout or a default in angle brackets: from ANGBASE, clockwise
        /// with ANGDIR 1, from nought up to a whole turn, as AutoCAD's DIST writes 90 for a line drawn east in a drawing whose
        /// angles are counted from north, clockwise.
        /// </summary>
        /// <param name="direction">The direction, in radians counter-clockwise from the X axis of the UCS.</param>
        /// <returns>The angle counted, in radians.</returns>
        public double CountAngle(double direction) =>
            CadUnits.FromDirection(direction, Document.Database.Angbase, Document.Database.Angdir);
    }
}
