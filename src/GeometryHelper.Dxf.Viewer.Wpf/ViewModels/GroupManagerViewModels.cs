using System;
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
                Pick("Highlight");
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
    /// AutoCAD's Order Group dialog of Object Grouping, over the group chosen: its name and description, Remove from position,
    /// Enter new position and Number of objects; Re-Order, Highlight and Reverse Order. The group of four objects A, B, C, D
    /// with 1 to 3 and 1 object reads A, C, D, B.
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

        /// <summary>Gets or sets Remove from position, as typed.</summary>
        public string RemoveFrom { get => _from; set => Set(ref _from, value ?? string.Empty); }

        /// <summary>Gets or sets Enter new position, as typed.</summary>
        public string NewPosition { get => _to; set => Set(ref _to, value ?? string.Empty); }

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

        /// <summary>Highlight: the objects from Remove from position on, Number of objects of them, to be shown on the canvas.</summary>
        /// <returns>Whether the boxes read, so that the dialogs close for the command line to show them.</returns>
        public bool Highlight()
        {
            if (!Numbers(out int from, out int to, out int count))
            {
                return false;
            }

            Request.HighlightFrom = from;
            Request.HighlightCount = count;
            return true;
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
