using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A scale of the Annotation Object Scale list: its name and, where not every object selected is shown at it, how many are.</summary>
    public sealed class ObjectScaleRowViewModel
    {
        internal ObjectScaleRowViewModel(CadScaleListRow scale, string shared)
        {
            Scale = scale;
            Shared = shared;
        }

        /// <summary>Gets the scale.</summary>
        public CadScaleListRow Scale { get; }

        /// <summary>Gets the name, as 1:50.</summary>
        public string Name => Scale.Name;

        /// <summary>Gets "on 1 of 3" where only some of the objects selected are shown at the scale; empty where all are.</summary>
        public string Shared { get; }
    }

    /// <summary>
    /// AutoCAD 2021's Annotation Object Scale dialog over OBJECTSCALE's request: the Object Scale List with the paper and
    /// drawing units of the scale picked, Add… and Delete, and "List all scales for selected objects" or "List scales common
    /// to all selected objects only". Delete is greyed where an object would be left with no scale, as AutoCAD keeps one; OK
    /// gives the request the scales added and deleted (<see cref="Accept"/>).
    /// </summary>
    public sealed class ObjectScaleViewModel : ObservableObject
    {
        private readonly CadObjectScaleRequest _request;
        private readonly List<string> _added = new List<string>();
        private readonly List<string> _deleted = new List<string>();
        private IReadOnlyList<ObjectScaleRowViewModel> _scales = Array.Empty<ObjectScaleRowViewModel>();
        private ObjectScaleRowViewModel _picked;
        private bool _commonOnly;

        /// <summary>Makes the dialog over OBJECTSCALE's request.</summary>
        /// <param name="request">The request.</param>
        public ObjectScaleViewModel(CadObjectScaleRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            Refresh();
        }

        /// <summary>Gets how many annotative objects are selected.</summary>
        public int ObjectCount => _request.ObjectScales.Count;

        /// <summary>Gets the line of the footer: "3 annotative objects selected".</summary>
        public string Caption =>
            ObjectCount.ToString(CultureInfo.InvariantCulture)
            + (ObjectCount == 1 ? " annotative object selected" : " annotative objects selected");

        /// <summary>Gets or sets whether only the scales common to all the objects are listed; all their scales where false.</summary>
        public bool IsCommonOnly
        {
            get => _commonOnly;
            set
            {
                if (Set(ref _commonOnly, value))
                {
                    Raise(nameof(IsAllListed));
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets whether every scale of the objects is listed: the other choice of <see cref="IsCommonOnly"/>.</summary>
        public bool IsAllListed
        {
            get => !_commonOnly;
            set => IsCommonOnly = !value;
        }

        /// <summary>Gets the Object Scale List, in the order of the drawing's scale list.</summary>
        public IReadOnlyList<ObjectScaleRowViewModel> Scales => _scales;

        /// <summary>Gets or sets the scale picked in the list.</summary>
        public ObjectScaleRowViewModel Picked
        {
            get => _picked;
            set
            {
                if (Set(ref _picked, value))
                {
                    Raise(nameof(PaperUnits));
                    Raise(nameof(DrawingUnits));
                    Raise(nameof(CanDelete));
                }
            }
        }

        /// <summary>Gets the paper units of the scale picked, as 1; empty with none.</summary>
        public string PaperUnits => _picked == null ? string.Empty : Number(_picked.Scale.PaperUnits);

        /// <summary>Gets the drawing units of the scale picked, as 100; empty with none.</summary>
        public string DrawingUnits => _picked == null ? string.Empty : Number(_picked.Scale.DrawingUnits);

        /// <summary>Gets whether Delete can take the scale picked: no object is left with no scale.</summary>
        public bool CanDelete =>
            _picked != null
            && !Current().Any(set => set.Count == 1 && set.Contains(_picked.Name, StringComparer.OrdinalIgnoreCase));

        /// <summary>Gets what Add… offers: the drawing's scales the list does not show, in the order of the drawing's list.</summary>
        public IReadOnlyList<CadScaleListRow> AddChoices =>
            _request
                .DrawingScales.Where(s =>
                    !_scales.Any(r => string.Equals(r.Name, s.Name, StringComparison.OrdinalIgnoreCase))
                )
                .ToList();

        /// <summary>Takes the scale picked from every object shown at it; nothing where <see cref="CanDelete"/> is false.</summary>
        public void Delete()
        {
            if (!CanDelete)
            {
                return;
            }

            string name = _picked.Name;
            _added.RemoveAll(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

            if (_request.ObjectScales.Any(set => set.Contains(name, StringComparer.OrdinalIgnoreCase)))
            {
                _deleted.Add(name);
            }

            Refresh();
        }

        /// <summary>Adds scales to every object, as the OK of Add Scales to Object; a name the drawing has not is passed over.</summary>
        /// <param name="names">The names of the scales.</param>
        public void Add(IEnumerable<string> names)
        {
            foreach (string name in names ?? Array.Empty<string>())
            {
                CadScaleListRow scale = _request.DrawingScales.FirstOrDefault(s =>
                    string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)
                );

                if (scale == null)
                {
                    continue;
                }

                _deleted.RemoveAll(n => string.Equals(n, scale.Name, StringComparison.OrdinalIgnoreCase));

                if (
                    !_request.ObjectScales.All(set => set.Contains(scale.Name, StringComparer.OrdinalIgnoreCase))
                    && !_added.Contains(scale.Name, StringComparer.OrdinalIgnoreCase)
                )
                {
                    _added.Add(scale.Name);
                }
            }

            Refresh();
        }

        /// <summary>Gives the request the scales added and deleted, as OK does.</summary>
        public void Accept()
        {
            _request.ToAdd.Clear();
            _request.ToDelete.Clear();

            foreach (string name in _added)
            {
                _request.ToAdd.Add(name);
            }

            foreach (string name in _deleted)
            {
                _request.ToDelete.Add(name);
            }
        }

        /// <summary>The scales of each object with the changes made so far.</summary>
        private List<List<string>> Current() =>
            _request
                .ObjectScales.Select(set =>
                    set.Where(n => !_deleted.Contains(n, StringComparer.OrdinalIgnoreCase))
                        .Concat(_added)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
                )
                .ToList();

        private void Refresh()
        {
            List<List<string>> current = Current();
            int count = current.Count;
            var rows = new List<ObjectScaleRowViewModel>();

            foreach (CadScaleListRow scale in _request.DrawingScales)
            {
                int shown = current.Count(set => set.Contains(scale.Name, StringComparer.OrdinalIgnoreCase));

                if (shown == 0 || _commonOnly && shown < count)
                {
                    continue;
                }

                rows.Add(
                    new ObjectScaleRowViewModel(
                        scale,
                        shown == count
                            ? string.Empty
                            : "on "
                                + shown.ToString(CultureInfo.InvariantCulture)
                                + " of "
                                + count.ToString(CultureInfo.InvariantCulture)
                    )
                );
            }

            string was = _picked?.Name;
            _scales = rows;
            Raise(nameof(Scales));
            Raise(nameof(AddChoices));
            _picked = null;
            Picked = rows.FirstOrDefault(r => string.Equals(r.Name, was, StringComparison.OrdinalIgnoreCase))
                ?? rows.FirstOrDefault();
            Raise(nameof(CanDelete));
        }

        private static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    /// <summary>A field of User Defined Fields, its words edited in place.</summary>
    public sealed class UserFieldRowViewModel : ObservableObject
    {
        private string _text;

        internal UserFieldRowViewModel(string text)
        {
            _text = text ?? string.Empty;
        }

        /// <summary>Gets or sets the words of the field.</summary>
        public string Text
        {
            get => _text;
            set => Set(ref _text, value ?? string.Empty);
        }
    }

    /// <summary>
    /// AutoCAD 2021's User Defined Fields, from Add / Edit… of Plot Stamp: the user's fields, Add putting "New field" at the end
    /// and picking it, Edit and the Field text box changing the one picked, Delete taking it out. OK gives Plot Stamp the
    /// fields with words (<see cref="Fields"/>), each once.
    /// </summary>
    public sealed class UserDefinedFieldsViewModel : ObservableObject
    {
        private UserFieldRowViewModel _picked;

        /// <summary>Makes the dialog over the user's fields.</summary>
        /// <param name="fields">The fields, in their order.</param>
        public UserDefinedFieldsViewModel(IEnumerable<string> fields)
        {
            foreach (string field in fields ?? Array.Empty<string>())
            {
                Rows.Add(new UserFieldRowViewModel(field));
            }

            _picked = Rows.FirstOrDefault();
        }

        /// <summary>Gets the rows of the list.</summary>
        public System.Collections.ObjectModel.ObservableCollection<UserFieldRowViewModel> Rows { get; } =
            new System.Collections.ObjectModel.ObservableCollection<UserFieldRowViewModel>();

        /// <summary>Gets or sets the row picked.</summary>
        public UserFieldRowViewModel Picked
        {
            get => _picked;
            set
            {
                if (Set(ref _picked, value))
                {
                    Raise(nameof(HasPicked));
                }
            }
        }

        /// <summary>Gets whether a row is picked, so that Edit, Delete and the Field text box are enabled.</summary>
        public bool HasPicked => _picked != null;

        /// <summary>Gets the fields as OK keeps them: those with words, each once, in their order.</summary>
        public IReadOnlyList<string> Fields =>
            Rows.Select(r => r.Text.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        /// <summary>Add: "New field" put at the end and picked.</summary>
        public void Add()
        {
            var row = new UserFieldRowViewModel("New field");
            Rows.Add(row);
            Picked = row;
        }

        /// <summary>Delete: the row picked taken out, the next one picked.</summary>
        public void Delete()
        {
            if (_picked == null)
            {
                return;
            }

            int at = Rows.IndexOf(_picked);
            Rows.RemoveAt(at);
            Picked = Rows.Count == 0 ? null : Rows[Math.Min(at, Rows.Count - 1)];
        }
    }

    /// <summary>A scale Add Scales to Object offers, picked or not.</summary>
    public sealed class ObjectScaleChoiceViewModel : ObservableObject
    {
        private bool _isPicked;

        internal ObjectScaleChoiceViewModel(string name)
        {
            Name = name;
        }

        /// <summary>Gets the name, as 1:20.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the scale is picked.</summary>
        public bool IsPicked
        {
            get => _isPicked;
            set => Set(ref _isPicked, value);
        }
    }

    /// <summary>AutoCAD's Add Scales to Object, from Add… of Annotation Object Scale: the scales offered, several picked at once.</summary>
    public sealed class ObjectScaleAddViewModel
    {
        /// <summary>Makes the picker over the scales offered.</summary>
        /// <param name="choices">The scales, in the order of the drawing's list.</param>
        public ObjectScaleAddViewModel(IEnumerable<CadScaleListRow> choices)
        {
            Scales = (choices ?? Array.Empty<CadScaleListRow>())
                .Select(s => new ObjectScaleChoiceViewModel(s.Name))
                .ToList();
        }

        /// <summary>Gets the scales offered.</summary>
        public IReadOnlyList<ObjectScaleChoiceViewModel> Scales { get; }

        /// <summary>Gets the names of the scales picked, in the order of the list.</summary>
        public IReadOnlyList<string> PickedNames => Scales.Where(s => s.IsPicked).Select(s => s.Name).ToList();
    }
}
