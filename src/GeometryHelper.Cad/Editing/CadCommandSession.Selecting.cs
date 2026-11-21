using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>What a selection band drawn on the canvas takes.</summary>
    public enum CadSelectionBandKind
    {
        /// <summary>A window typed W: what lies wholly inside the box.</summary>
        Window,

        /// <summary>A crossing typed C: what the box touches.</summary>
        Crossing,

        /// <summary>A box typed BOX: a window drawn left to right, a crossing right to left.</summary>
        Box,

        /// <summary>A fence typed F: what its segments cross, the line not closed.</summary>
        Fence,

        /// <summary>A window polygon typed WP: what lies wholly inside the polygon.</summary>
        WindowPolygon,

        /// <summary>A crossing polygon typed CP: what the polygon touches.</summary>
        CrossingPolygon,
    }

    /// <summary>
    /// The band an option of "Select objects:" draws on the canvas while it asks its points: the corners or points given so
    /// far, the cursor the next. The canvas draws a box from the first corner to the cursor, a fence through the points to the
    /// cursor, or a polygon through them closed back by the cursor.
    /// </summary>
    public sealed class CadSelectionBand
    {
        internal CadSelectionBand(CadSelectionBandKind kind, IReadOnlyList<GeoPoint2> points)
        {
            Kind = kind;
            Points = points;
        }

        /// <summary>Gets what the band takes.</summary>
        public CadSelectionBandKind Kind { get; }

        /// <summary>Gets the corners or points given so far, the first first.</summary>
        public IReadOnlyList<GeoPoint2> Points { get; }
    }

    /// <summary>The options of "Select objects:", as AutoCAD 2021's.</summary>
    public sealed partial class CadCommandSession
    {
        private const string Expects =
            "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon/Group/Add/Remove/Multiple/Previous/Undo/AUto/SIngle";

        private static readonly CadPrompt Options = CadPrompt.ForKeyword("Select objects", Expects.Split('/'));

        private readonly List<GeoPoint2> _band = new List<GeoPoint2>();

        // The changes of the selection gathered so far, the last last, which Undo takes back one at a time.
        private readonly List<KeyValuePair<List<string>, List<string>>> _steps =
            new List<KeyValuePair<List<string>, List<string>>>();
        private IReadOnlyList<string> _previous = Array.Empty<string>();

        /// <summary>Gets the objects the last selection took, the Previous selection that <c>(ssget "P")</c> gives again.</summary>
        internal IReadOnlyList<string> PreviousSelection => _previous;
        private CadPrompt _asked;
        private string _option;
        private bool _removing;
        private bool _single;
        private List<GeoPoint2> _multiple;

        /// <summary>Forgets the options of a "Select objects:" a command asks anew.</summary>
        private void ResetOptions()
        {
            _band.Clear();
            _steps.Clear();
            _asked = null;
            _option = null;
            _removing = false;
            _single = false;
            _multiple = null;
            _windowWritten = false;
            EndWindowInSelection();
        }

        /// <summary>Keeps a selection a command took, for Previous to give again (AutoCAD's previous selection set).</summary>
        internal void RememberSelection(IReadOnlyList<string> handles)
        {
            if (handles.Count > 0)
            {
                _previous = new List<string>(handles);
            }
        }

        /// <summary>
        /// The objects a window, a crossing or a polygon took, as AutoCAD hands them to the set (accoreconsole, probes p1 and p4 of
        /// the l2fix2 scratch): the newest first, as ALL does (five lines made at y 0, 10, 20, 30 and 40 came as 40, 30, 20, 10, 0),
        /// and, where the session has a view, Window, Crossing and WPolygon only the objects the view shows (a view from y 8.7 to
        /// 31.3 gave the lines at 10, 20 and 30 and not those at 0 and 40; a line across the edge of the view counts, one wholly
        /// outside does not). CPolygon and Fence are not held to the view, and a session with no view, as a script or a test has,
        /// takes everything.
        /// </summary>
        /// <param name="found">The handles the window took.</param>
        /// <param name="inView">Whether the objects must be in the view: true for Window, Crossing and WPolygon.</param>
        internal List<string> TakenByWindow(IEnumerable<string> found, bool inView)
        {
            List<string> ordered = NewestFirst(found);
            CadView view = WorkView;

            if (!inView || view == null || !(view.Width > 1.0) || !(view.Height > 1.0))
            {
                return ordered;
            }

            PickIndex index = Document.GetPickIndex(Document.WorkingSpace);
            var shown = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (int i in index.IndicesIn(view.VisibleBox))
            {
                shown.Add(index.List.Items[i].Handle ?? string.Empty);
            }

            ordered.RemoveAll(h => !shown.Contains(h));
            return ordered;
        }

        /// <summary>An option typed at "Select objects:", by its capital letters or whole.</summary>
        private bool SelectOption(string option, string echo)
        {
            Echo(echo);

            switch (option)
            {
                case "ALL":
                    var all = new List<string>();

                    foreach (DxfEntity entity in Document.WorkingSpace.Block)
                    {
                        if (
                            !string.IsNullOrEmpty(entity.Handle)
                            && !(entity is DxfViewport viewport && viewport.Number == 1)
                        )
                        {
                            all.Add(entity.Handle);
                        }
                    }

                    // The object made last comes first in the set, as AutoCAD counts the set of ALL (runs/hhh, runs/nnn).
                    all = NewestFirst(all);

                    if (!_removing)
                    {
                        ReplaceWithPickAdd0();
                        _whole.UnionWith(all);
                        _unwindowed.UnionWith(all);
                    }

                    return Gather(all, _removing);
                case "Last":
                    string last = LastVisible();

                    if (!_removing && last != null)
                    {
                        ReplaceWithPickAdd0();
                        _unwindowed.Add(last);
                    }

                    return Gather(last == null ? new List<string>() : new List<string> { last }, _removing);
                case "Previous":
                    var previous = new List<string>();

                    foreach (string handle in _previous)
                    {
                        if (
                            Document.FindEntity(handle) != null
                            && ReferenceEquals(Document.FindSpace(handle), Document.WorkingSpace)
                        )
                        {
                            previous.Add(handle);
                        }
                    }

                    if (!_removing)
                    {
                        ReplaceWithPickAdd0();
                        _whole.UnionWith(previous);
                        _unwindowed.UnionWith(previous);
                    }

                    return Gather(previous, _removing);
                case "Remove":
                    _asked = _asked ?? Prompt;
                    _removing = true;
                    Ask(CadPrompt.ForSelection("Remove objects"));
                    return true;
                case "Add":
                    _removing = false;
                    Ask(_asked);
                    _asked = null;
                    return true;
                case "Undo":
                    UndoStep();
                    return true;
                case "SIngle":
                    _single = true;
                    Changed();
                    return true;
                case "Multiple":
                    _multiple = new List<GeoPoint2>();
                    Changed();
                    return true;
                case "AUto":
                    Changed();
                    return true;
                case "Group":
                    return BeginOption(option, CadPrompt.ForString("Enter group name"));
                case "Window":
                case "Crossing":
                case "BOX":
                    return BeginOption(option, CadPrompt.ForPoint("Specify first corner"));
                case "Fence":
                    return BeginOption(option, CadPrompt.ForPoint("Specify first fence point or pick/drag cursor"));
                default:
                    return BeginOption(option, CadPrompt.ForPoint("First polygon point or pick/drag cursor"));
            }
        }

        /// <summary>Starts an option that asks prompts of its own, the command's "Select objects:" kept for when it is done.</summary>
        private bool BeginOption(string option, CadPrompt first)
        {
            _asked = _asked ?? Prompt;
            _option = option;
            _band.Clear();
            Ask(first);
            return true;
        }

        /// <summary>Writes the line a prompt and its answer make, where it was not written already (null).</summary>
        private void Echo(string echo)
        {
            if (echo != null && !(Prompt != null && Prompt.Unwritten && echo == Prompt.Text))
            {
                Write(echo);
            }
        }

        /// <summary>Shows a prompt the session asks itself, in place of the command's.</summary>
        private void Ask(CadPrompt prompt)
        {
            Prompt = prompt ?? Prompt;
            Changed();
        }

        /// <summary>Ends an option: back at the command's "Select objects:", or "Remove objects:" while removing.</summary>
        private void EndOption()
        {
            EndWindowInSelection();
            _option = null;
            _band.Clear();
            Prompt = _removing ? CadPrompt.ForSelection("Remove objects") : _asked ?? Prompt;

            if (!_removing)
            {
                _asked = null;
            }
        }

        /// <summary>A point given to an option's prompt: a corner, a fence point or a polygon point.</summary>
        private bool OptionPoint(GeoPoint2 point, string echo)
        {
            if (_option == "Group")
            {
                return Invalid("Invalid input.", echo);
            }

            Echo(echo);
            LastPoint = point;
            _band.Add(point);

            if (_option == "Window" || _option == "Crossing" || _option == "BOX")
            {
                if (_band.Count == 1)
                {
                    CadSelectionBandKind kind =
                        _option == "Window" ? CadSelectionBandKind.Window
                        : _option == "Crossing" ? CadSelectionBandKind.Crossing
                        : CadSelectionBandKind.Box;
                    Ask(Banded(CadPrompt.ForPoint("Specify opposite corner", point), kind));
                    return true;
                }

                GeoPoint2 first = _band[0];
                var box = new DisplayBox(
                    Math.Min(first.X, point.X),
                    Math.Min(first.Y, point.Y),
                    Math.Max(first.X, point.X),
                    Math.Max(first.Y, point.Y)
                );
                bool crossing = _option == "Crossing" || (_option == "BOX" && point.X < first.X);
                PickIndex index = Document.GetPickIndex(Document.WorkingSpace);
                List<string> found = TakenByWindow(crossing ? index.Crossing(box) : index.Window(box), true);
                EndOption();
                return GatherWhole(found, box, crossing);
            }

            AskNextPoint();
            return true;
        }

        /// <summary>Asks the next point of a fence or polygon, its band through the points given; the first again where none is left.</summary>
        private void AskNextPoint()
        {
            bool fence = _option == "Fence";

            if (_band.Count == 0)
            {
                Ask(
                    CadPrompt.ForPoint(
                        fence
                            ? "Specify first fence point or pick/drag cursor"
                            : "First polygon point or pick/drag cursor"
                    )
                );
                return;
            }

            CadSelectionBandKind kind =
                fence ? CadSelectionBandKind.Fence
                : _option == "WPolygon" ? CadSelectionBandKind.WindowPolygon
                : CadSelectionBandKind.CrossingPolygon;
            CadPrompt next = fence
                ? CadPrompt.ForPoint("Specify next fence point", _band[_band.Count - 1], new[] { "Undo" })
                : CadPrompt.ForPoint("Specify endpoint of line", _band[_band.Count - 1], new[] { "Undo" });
            Ask(Banded(next, kind));
        }

        private CadPrompt Banded(CadPrompt prompt, CadSelectionBandKind kind)
        {
            prompt.Band = new CadSelectionBand(kind, new List<GeoPoint2>(_band));
            return prompt;
        }

        /// <summary>Text typed at an option's prompt: a point, Undo, or a group's name.</summary>
        private bool OptionText(string typed)
        {
            // Text a modifier handed on has its echo written already.
            string echo = _refeeding ? null : Prompt.Echo(typed);

            if (_option == "Group")
            {
                Echo(echo);
                List<string> members = GroupMembers(typed);
                EndOption();

                if (members == null)
                {
                    Write("Group \"" + typed + "\" has not been defined.");
                    Changed();
                    return false;
                }

                return GatherWhole(members);
            }

            if (WindowOption(typed, echo))
            {
                return true;
            }

            if (Prompt.MatchKeyword(typed) == "Undo")
            {
                Echo(echo);
                _band.RemoveAt(_band.Count - 1);
                AskNextPoint();
                return true;
            }

            // A number alone is a distance from the last point (the corner "W 1" gives), as a point prompt takes it.
            if (TryPoint(typed, out GeoPoint2 point) || TryDistancePoint(typed, Prompt.BasePoint, out point))
            {
                // After an object snap override the point is the snap of the object under it, as at any prompt for a point.
                TypedSnapResult snapped = SnapTyped(ref point, echo, out string absent);

                if (snapped == TypedSnapResult.Asking)
                {
                    return true;
                }

                if (snapped == TypedSnapResult.Missing)
                {
                    Echo(echo);
                    Write(absent);
                    return _option == "Window" || _option == "Crossing" || _option == "BOX"
                        ? InvalidWindow(null)
                        : Invalid("Invalid response.");
                }

                return OptionPoint(point, echo);
            }

            // AutoCAD 2021's words: a box says "Invalid window specification." and a fence or a polygon "Invalid response.".
            if (_option == "Window" || _option == "Crossing" || _option == "BOX")
            {
                return InvalidWindow(echo);
            }

            return Invalid("Invalid response.", echo);
        }

        /// <summary>
        /// What a corner of a window, a crossing or a BOX is answered with when it is no point (or Enter): "Invalid window
        /// specification." and the first corner asked again, the opposite corner of the implied window asked as it was.
        /// </summary>
        private bool InvalidWindow(string echo)
        {
            Echo(echo);
            Write("Invalid window specification.");

            if (!ReferenceEquals(Prompt, _windowFirst))
            {
                _band.Clear();
                Ask(CadPrompt.ForPoint("Specify first corner"));
                return false;
            }

            Changed();
            return false;
        }

        /// <summary>
        /// Enter at an option's prompt: a fence of two points or more, or a polygon of three or more, takes what it takes; a
        /// box or a group not finished takes nothing. Back at "Select objects:" either way.
        /// </summary>
        private void OptionEnter()
        {
            if (WindowOption(null, null))
            {
                return;
            }

            Write(Prompt.Text);
            string option = _option;

            if (option == "Window" || option == "Crossing" || option == "BOX")
            {
                InvalidWindow(null);
                return;
            }

            var points = new List<GeoPoint2>(_band);
            EndOption();
            PickIndex index = Document.GetPickIndex(Document.WorkingSpace);

            if (option == "Fence" && points.Count >= 2)
            {
                GatherWhole(new List<string>(index.Fence(points)));
            }
            else if ((option == "WPolygon" || option == "CPolygon") && points.Count >= 3)
            {
                GatherWhole(
                    TakenByWindow(
                        option == "WPolygon" ? index.WindowPolygon(points) : index.CrossingPolygon(points),
                        option == "WPolygon"
                    )
                );
            }
            else
            {
                Changed();
            }
        }

        /// <summary>Enter at "Select objects:" while Multiple gathers points: the object nearest each, one not taken at that point before, then back at "Select objects:".</summary>
        private void MultipleEnter()
        {
            Write(Prompt.Text);
            List<GeoPoint2> points = _multiple;
            _multiple = null;
            PickIndex index = Document.GetPickIndex(Document.WorkingSpace);
            var found = new List<string>();
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (GeoPoint2 point in points)
            {
                foreach (string handle in index.Pick(point.X, point.Y, PickAperture))
                {
                    if (taken.Add(handle))
                    {
                        found.Add(handle);
                        break;
                    }
                }
            }

            GatherWhole(found);
        }

        /// <summary>With PICKADD 0, forgets the objects gathered so far, so that those an option takes next replace them.</summary>
        private void ReplaceWithPickAdd0()
        {
            if (Document.PickAdd == 0)
            {
                ClearGathering();
            }
        }

        /// <summary>Gathers objects an option took whole, or takes them out while removing; a window's box kept with what it took.</summary>
        private bool GatherWhole(List<string> found, DisplayBox? window = null, bool crossing = false)
        {
            if (!_removing)
            {
                ReplaceWithPickAdd0();
                List<string> grouped = new List<string>(Document.WithGroups(found));
                _whole.UnionWith(grouped);

                if (window.HasValue)
                {
                    _windows.AddRange(grouped.Select(h => Tuple.Create(h, window.Value, crossing)));
                }
                else
                {
                    _unwindowed.UnionWith(grouped);
                }

                return Gather(grouped, false);
            }

            return Gather(new List<string>(Document.WithGroups(found)), true);
        }

        /// <summary>The members of a group named, in ordinal order, those in the current space; null where the drawing has no group of that name.</summary>
        private List<string> GroupMembers(string name)
        {
            DxfDBDictionary dictionary = Document.Database.GroupDictionary;

            if (dictionary == null)
            {
                return null;
            }

            foreach (DxfDBDictionaryEntry entry in dictionary)
            {
                if (
                    string.Equals(entry.Key, name.Trim(), StringComparison.OrdinalIgnoreCase)
                    && entry.Value is DxfGroup group
                    && !group.IsErased
                )
                {
                    var members = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (DxfEntity member in group.GetAllEntityIds())
                    {
                        if (
                            member.Handle != null
                            && Document.FindEntity(member.Handle) != null
                            && ReferenceEquals(Document.FindSpace(member.Handle), Document.WorkingSpace)
                        )
                        {
                            members.Add(member.Handle);
                        }
                    }

                    return new List<string>(members);
                }
            }

            return null;
        }

        /// <summary>
        /// The object Last takes: the one made last of those the current space shows, as AutoCAD's Last takes the last visible
        /// object made, by the highest handle; an object on a layer off or frozen is not shown and not taken. Null for none.
        /// </summary>
        private string LastVisible()
        {
            PickIndex index = Document.GetPickIndex(Document.WorkingSpace);
            string last = null;
            ulong highest = 0;

            foreach (DisplayItem2 item in index.List.Items)
            {
                string handle = item.Handle;

                if (
                    string.IsNullOrEmpty(handle)
                    || !ulong.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong value)
                )
                {
                    continue;
                }

                if (last == null || value > highest)
                {
                    if (Document.FindEntity(handle) != null)
                    {
                        last = handle;
                        highest = value;
                    }
                }
            }

            return last;
        }

        /// <summary>Undo at "Select objects:": the last change of the selection taken back.</summary>
        private void UndoStep()
        {
            if (_steps.Count == 0)
            {
                Changed();
                return;
            }

            KeyValuePair<List<string>, List<string>> step = _steps[_steps.Count - 1];
            _steps.RemoveAt(_steps.Count - 1);

            foreach (string handle in step.Key)
            {
                _gathered.Remove(handle);
                _pickOrder.RemoveAll(h => string.Equals(h, handle, StringComparison.OrdinalIgnoreCase));
                _whole.Remove(handle);
                _unwindowed.Remove(handle);
                _clicks.RemoveAll(p => string.Equals(p.Key, handle, StringComparison.OrdinalIgnoreCase));
                _windows.RemoveAll(w => string.Equals(w.Item1, handle, StringComparison.OrdinalIgnoreCase));
            }

            foreach (string handle in step.Value)
            {
                if (_gathered.Add(handle))
                {
                    _pickOrder.Add(handle);
                }

                _whole.Add(handle);
                _unwindowed.Add(handle);
            }

            Changed();
        }

        /// <summary>The invalid answer at "Select objects:", with the options AutoCAD lists.</summary>
        private bool InvalidSelection(string echo)
        {
            Echo(echo);
            Write("*Invalid selection*");
            Write("Expects a point or");
            return Invalid(Expects);
        }

        /// <summary>
        /// The invalid answer at a prompt for one object: "*Invalid selection*" and the words that prompt has, "Expects a single
        /// object." or "Expects a point or" and the options it takes (<see cref="CadPrompt.Expected"/>).
        /// </summary>
        private bool InvalidObject(string echo)
        {
            Echo(echo);
            Write("*Invalid selection*");

            if (Prompt.Expected == null)
            {
                return Invalid("Expects a single object.");
            }

            // The words of a routine's entsel are one line, as AutoCAD 2021 writes them.
            if (ReferenceEquals(Prompt, _lispAskPrompt))
            {
                return Invalid("Expects a point or " + Prompt.Expected);
            }

            Write("Expects a point or");
            return Invalid(Prompt.Expected);
        }
    }
}
