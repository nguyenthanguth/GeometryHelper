using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the Object Grouping list: the group's name and whether it is selectable, Yes or No.</summary>
    public sealed class GroupRowViewModel
    {
        internal GroupRowViewModel(CadGroupRow row)
        {
            Row = row;
        }

        /// <summary>Gets the group of the request.</summary>
        public CadGroupRow Row { get; }

        /// <summary>Gets the name.</summary>
        public string Name => Row.Name;

        /// <summary>Gets Yes where a pick takes the group whole, else No.</summary>
        public string Selectable => Row.Selectable ? "Yes" : "No";
    }

    /// <summary>
    /// AutoCAD's Object Grouping dialog, which CLASSICGROUP shows, over the request its command hands the window: the list of
    /// groups, Group Identification (the name, the description, Find Name &lt;, Highlight &lt; and Include Unnamed), Create
    /// Group (New &lt;, Selectable, Unnamed) and Change Group (Remove &lt;, Add &lt;, Rename, Re-Order…, Description,
    /// Explode, Selectable). A button ending in &lt; closes the dialog for the command line to ask, then it comes back.
    /// </summary>
    public sealed class GroupManagerViewModel : ObservableObject
    {
        private readonly ObservableCollection<GroupRowViewModel> _groups = new ObservableCollection<GroupRowViewModel>();
        private GroupRowViewModel _selected;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of CLASSICGROUP.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public GroupManagerViewModel(CadGroupManagerRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Refresh();
        }

        /// <summary>Gets the request.</summary>
        public CadGroupManagerRequest Request { get; }

        /// <summary>Gets or sets how the window shows Order Group; true where Highlight closed it, so that the dialog closes too.</summary>
        public Func<OrderGroupViewModel, bool> ShowOrder { get; set; }

        /// <summary>Gets the groups listed: the named ones, and the unnamed with Include Unnamed.</summary>
        public ObservableCollection<GroupRowViewModel> Groups => _groups;

        /// <summary>Gets or sets the group chosen, whose name and description fill the boxes.</summary>
        public GroupRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Request.Selected = value?.Name;

                    if (value != null)
                    {
                        Request.GroupName = value.Row.IsUnnamed ? string.Empty : value.Name;
                        Request.Description = value.Row.Description ?? string.Empty;
                        Raise(nameof(GroupName));
                        Raise(nameof(Description));
                    }

                    Raise(nameof(HasSelected));
                }
            }
        }

        /// <summary>Gets whether a group is chosen, which the buttons of Change Group need.</summary>
        public bool HasSelected => _selected != null;

        /// <summary>Gets or sets the Group Name typed.</summary>
        public string GroupName { get => Request.GroupName; set { Request.GroupName = value ?? string.Empty; Raise(nameof(GroupName)); } }

        /// <summary>Gets or sets the Description typed.</summary>
        public string Description { get => Request.Description; set { Request.Description = value ?? string.Empty; Raise(nameof(Description)); } }

        /// <summary>Gets or sets whether unnamed groups are listed.</summary>
        public bool IncludeUnnamed
        {
            get => Request.IncludeUnnamed;
            set
            {
                Request.IncludeUnnamed = value;
                Raise(nameof(IncludeUnnamed));
                Refresh();
            }
        }

        /// <summary>Gets or sets whether a group New makes is selectable.</summary>
        public bool NewSelectable { get => Request.NewSelectable; set { Request.NewSelectable = value; Raise(nameof(NewSelectable)); } }

        /// <summary>Gets or sets whether a group New makes is unnamed.</summary>
        public bool NewUnnamed { get => Request.NewUnnamed; set { Request.NewUnnamed = value; Raise(nameof(NewUnnamed)); } }

        /// <summary>Gets what the last change said where it was refused, as "A group with this name already exists."; empty where none was.</summary>
        public string Message => Request.Message ?? string.Empty;

        /// <summary>A button ending in &lt;: the dialog closed with its words, for the command line to ask.</summary>
        /// <param name="words">Find Name, Highlight, New, Remove or Add.</param>
        public void Pick(string words)
        {
            Request.Pick = words;
            Request.Action = CadDialogAction.SelectObjects;
        }

        /// <summary>Rename: the group chosen given the Group Name typed.</summary>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename() => Changed(Request.Rename());

        /// <summary>Description: the group chosen given the Description typed.</summary>
        /// <returns>Whether it was changed.</returns>
        public bool ChangeDescription() => Changed(Request.ChangeDescription());

        /// <summary>Explode: the group chosen taken apart.</summary>
        /// <returns>Whether it was.</returns>
        public bool Explode() => Changed(Request.Explode());

        /// <summary>Selectable: whether the group chosen is taken whole, switched.</summary>
        /// <returns>Whether it was switched.</returns>
        public bool ToggleSelectable() => Changed(Request.ToggleSelectable());

        /// <summary>
        /// Re-Order…: Order Group over the group chosen. Highlight there closes it and this dialog, for the command line to
        /// show the objects asked for.
        /// </summary>
        /// <returns>Whether the dialog is to close for Highlight.</returns>
        public bool Reorder()
        {
            if (_selected == null || ShowOrder == null)
            {
                return false;
            }

            var order = new OrderGroupViewModel(Request);
            bool highlight = ShowOrder(order);
            Refresh();

            if (highlight)
            {
                Pick("Order Highlight");
            }

            return highlight;
        }

        /// <summary>Closes the dialog with an action: OK keeps every change, Cancel takes them back.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action) => Request.Action = action;

        private bool Changed(bool done)
        {
            Refresh();
            Raise(nameof(Message));
            return done;
        }

        /// <summary>The list made again from the request, the group of <see cref="CadGroupManagerRequest.Selected"/> chosen.</summary>
        private void Refresh()
        {
            string chosen = Request.Selected;
            _groups.Clear();

            foreach (CadGroupRow row in Request.Groups.Where(g => Request.IncludeUnnamed || !g.IsUnnamed))
            {
                _groups.Add(new GroupRowViewModel(row));
            }

            _selected = _groups.FirstOrDefault(g => string.Equals(g.Name, chosen, StringComparison.OrdinalIgnoreCase));
            Request.Selected = _selected?.Name;
            Raise(nameof(Selected));
            Raise(nameof(HasSelected));
            Raise(nameof(GroupName));
            Raise(nameof(Description));
            Raise(nameof(Message));
        }
    }

    /// <summary>
    /// AutoCAD's Order Group dialog of Object Grouping, over the group chosen in its Group Name list: its description, Remove
    /// from position, Enter new position and Number of objects, each label giving the range the group's size allows; Re-Order,
    /// Highlight and Reverse Order. The group of four objects A, B, C, D with 1 to 3 and 1 object reads A, C, D, B. For a
    /// group of twelve with 3 to 0 the labels read (0-11), (0-11) and (1-9).
    /// </summary>
    public sealed class OrderGroupViewModel : ObservableObject
    {
        private string _from = "0";
        private string _to = "0";
        private string _count = "1";
        private string _message = string.Empty;

        /// <summary>Makes the dialog over the group chosen of a request.</summary>
        /// <param name="request">The request of CLASSICGROUP.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public OrderGroupViewModel(CadGroupManagerRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets the request.</summary>
        public CadGroupManagerRequest Request { get; }

        /// <summary>Gets the name of the group.</summary>
        public string Group => Request.Selected ?? string.Empty;

        /// <summary>Gets its description.</summary>
        public string Description => Row?.Description ?? string.Empty;

        /// <summary>Gets "Positions 0 to 3": the positions of its objects.</summary>
        public string Positions => Row == null || Row.Count == 0 ? "No objects" : "Positions 0 to " + (Row.Count - 1).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the groups of the Group Name list, as Object Grouping lists them: the named ones, and the unnamed with Include Unnamed.</summary>
        public IReadOnlyList<string> Groups => Request.Groups.Where(g => Request.IncludeUnnamed || !g.IsUnnamed || string.Equals(g.Name, Request.Selected, StringComparison.OrdinalIgnoreCase)).Select(g => g.Name).ToList();

        /// <summary>Gets or sets the group chosen in the Group Name list, which the boxes and the buttons work on; Object Grouping shows it chosen after.</summary>
        public string SelectedGroup
        {
            get => Request.Selected;
            set
            {
                if (value == null || string.Equals(value, Request.Selected, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Request.Selected = value;
                Raise(nameof(SelectedGroup));
                Raise(nameof(Group));
                Raise(nameof(Description));
                Raise(nameof(Positions));
                RaiseRanges();
            }
        }

        /// <summary>Gets the last position of the group, 11 for twelve objects; -1 for a group with none.</summary>
        public int LastPosition => (Row?.Count ?? 0) - 1;

        /// <summary>
        /// Gets the most objects Re-Order can move from the positions typed: the group's size less the larger of the two, 9 for
        /// twelve objects moved from 3 to 0; a position that does not read counts as 0.
        /// </summary>
        public int MostObjects => Math.Max(0, (Row?.Count ?? 0) - Math.Max(Position(_from), Position(_to)));

        /// <summary>Gets the label of Remove from position with its range: "Remove from position (0-11):".</summary>
        public string RemoveFromLabel => "Remove from position (" + Range(0, LastPosition) + "):";

        /// <summary>Gets the label of Enter new position with its range: "Enter new position number for the object (0-11):".</summary>
        public string NewPositionLabel => "Enter new position number for the object (" + Range(0, LastPosition) + "):";

        /// <summary>Gets the label of Number of objects with its range: "Number of objects (1-9):".</summary>
        public string CountLabel => "Number of objects (" + Range(1, MostObjects) + "):";

        /// <summary>Gets the note under the buttons: "STAIR_CORE has 12 objects, numbered 0 to 11 in the order they were added." and what Highlight does.</summary>
        public string Summary
        {
            get
            {
                int size = Row?.Count ?? 0;
                return Group + " has " + size.ToString(CultureInfo.InvariantCulture) + (size == 1 ? " object" : " objects")
                    + (size == 0 ? "." : ", numbered 0 to " + (size - 1).ToString(CultureInfo.InvariantCulture) + " in the order they were added.")
                    + " Highlight hides the dialog and steps through them on the canvas (Next, Previous, eXit); Enter brings it back.";
            }
        }

        /// <summary>Gets or sets Remove from position, as typed.</summary>
        public string RemoveFrom
        {
            get => _from;
            set
            {
                if (Set(ref _from, value ?? string.Empty))
                {
                    RaiseRanges();
                }
            }
        }

        /// <summary>Gets or sets Enter new position, as typed.</summary>
        public string NewPosition
        {
            get => _to;
            set
            {
                if (Set(ref _to, value ?? string.Empty))
                {
                    RaiseRanges();
                }
            }
        }

        /// <summary>Gets or sets Number of objects, as typed.</summary>
        public string Count { get => _count; set => Set(ref _count, value ?? string.Empty); }

        /// <summary>Gets what the last button said where it was refused.</summary>
        public string Message { get => _message; private set => Set(ref _message, value ?? string.Empty); }

        private CadGroupRow Row => Request.Groups.FirstOrDefault(g => string.Equals(g.Name, Request.Selected, StringComparison.OrdinalIgnoreCase));

        /// <summary>Re-Order: the objects moved as the boxes say.</summary>
        /// <returns>Whether they were moved.</returns>
        public bool Reorder()
        {
            if (!Numbers(out int from, out int to, out int count))
            {
                return false;
            }

            bool done = Request.Reorder(from, to, count);
            Message = Request.Message ?? string.Empty;
            return done;
        }

        /// <summary>Reverse Order: the objects of the group put in the opposite order.</summary>
        /// <returns>Whether they were.</returns>
        public bool Reverse()
        {
            bool done = Request.Reverse();
            Message = Request.Message ?? string.Empty;
            return done;
        }

        /// <summary>
        /// Highlight: the dialogs close and the command line steps through the members of the group on the canvas one at a
        /// time, "Object 0 of 11 [Next/Previous/eXit]:"; eXit or Enter brings Object Grouping back.
        /// </summary>
        /// <returns>Whether the group has members to step through, so that the dialogs close.</returns>
        public bool Highlight()
        {
            if (LastPosition < 0)
            {
                Message = "The group has no objects.";
                return false;
            }

            return true;
        }

        /// <summary>A position as typed, 0 where it does not read or is below 0.</summary>
        private static int Position(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? Math.Max(0, value) : 0;

        private static string Range(int low, int high) => low.ToString(CultureInfo.InvariantCulture) + "-" + Math.Max(low, high).ToString(CultureInfo.InvariantCulture);

        private void RaiseRanges()
        {
            Raise(nameof(LastPosition));
            Raise(nameof(MostObjects));
            Raise(nameof(RemoveFromLabel));
            Raise(nameof(NewPositionLabel));
            Raise(nameof(CountLabel));
            Raise(nameof(Summary));
        }

        private bool Numbers(out int from, out int to, out int count)
        {
            bool read = int.TryParse(_from, NumberStyles.Integer, CultureInfo.InvariantCulture, out from)
                & int.TryParse(_to, NumberStyles.Integer, CultureInfo.InvariantCulture, out to)
                & int.TryParse(_count, NumberStyles.Integer, CultureInfo.InvariantCulture, out count);

            if (!read)
            {
                Message = "Requires a whole number.";
            }

            return read;
        }
    }
}
