using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of Edit Drawing Scales' Scale List: one scale of the list the dialog is editing.</summary>
    public sealed class ScaleRowViewModel : ObservableObject
    {
        private CadScaleListRow _scale;

        internal ScaleRowViewModel(CadScaleListRow scale)
        {
            _scale = scale;
        }

        /// <summary>Gets the scale as it stands in the dialog.</summary>
        public CadScaleListRow Scale
        {
            get => _scale;
            internal set
            {
                if (Set(ref _scale, value))
                {
                    Raise(nameof(Name));
                }
            }
        }

        /// <summary>Gets the name shown, as 1:50.</summary>
        public string Name => _scale.Name;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD 2021's Add Scale and Edit Scale of Edit Drawing Scales: Name appearing in scale list, and Scale properties,
    /// Paper units = Drawing units. Edit Scale opens on the scale chosen; the units of a scale an object points at are greyed,
    /// as AutoCAD's are. OK refuses an empty name, a name another scale of the list has (letters of either case the same) and
    /// units that are not numbers above nought, each in AutoCAD's words. Add Scale given the name of a scale an object
    /// points at refuses it ("1:50 already exists and is a referenced scale. Please enter a different scale name."); given
    /// the name of another scale it asks "1:25 already exists.  Would you like to redefine this scale?" and, on Yes, gives
    /// that scale the units. Edit Scale given a name another scale has says "1:25 already exists."
    /// </summary>
    public sealed class ScaleEditViewModel : ObservableObject
    {
        private readonly IReadOnlyList<string> _others;
        private string _name;
        private string _paper;
        private string _drawing;
        private string _message = string.Empty;

        /// <summary>Makes Add Scale, or Edit Scale over a scale.</summary>
        /// <param name="others">The names of the other scales of the list, which the name may not take.</param>
        /// <param name="scale">The scale edited; null for Add Scale.</param>
        public ScaleEditViewModel(IEnumerable<string> others, CadScaleListRow scale = null)
        {
            _others = (others ?? Enumerable.Empty<string>()).ToList();
            Scale = scale;
            _name = scale?.Name ?? string.Empty;
            _paper = scale == null ? "1" : CadScaleListRow.Number(scale.PaperUnits);
            _drawing = scale == null ? "1" : CadScaleListRow.Number(scale.DrawingUnits);
        }

        /// <summary>Gets the scale edited; null for Add Scale.</summary>
        public CadScaleListRow Scale { get; }

        /// <summary>Gets the title: Add Scale or Edit Scale.</summary>
        public string Title => Scale == null ? "Add Scale" : "Edit Scale";

        /// <summary>Gets or sets the name appearing in the scale list.</summary>
        public string Name
        {
            get => _name;
            set => Set(ref _name, value ?? string.Empty);
        }

        /// <summary>Gets or sets the paper units, as typed.</summary>
        public string PaperUnits
        {
            get => _paper;
            set => Set(ref _paper, value ?? string.Empty);
        }

        /// <summary>Gets or sets the drawing units, as typed.</summary>
        public string DrawingUnits
        {
            get => _drawing;
            set => Set(ref _drawing, value ?? string.Empty);
        }

        /// <summary>Gets whether the units can be changed: not for a scale an object points at, as AutoCAD's Edit Scale greys them.</summary>
        public bool CanChangeUnits => Scale == null || !Scale.IsInUse;

        /// <summary>
        /// Gets or sets how the window asks Add Scale's question over a name another scale has: "1:25 already exists.  Would
        /// you like to redefine this scale?", true for Yes. With none set, a scale is redefined without asking.
        /// </summary>
        public Func<string, bool> ConfirmRedefine { get; set; }

        /// <summary>Gets why OK was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the scale OK made; null until then.</summary>
        public CadScaleListRow Result { get; private set; }

        /// <summary>Gets the scale of the list that Add Scale's OK redefined, answered Yes to; null where it added a scale.</summary>
        public CadScaleListRow Redefined { get; private set; }

        /// <summary>The scales of the list Add Scale may redefine, with whether an object points at them; null for none.</summary>
        internal IReadOnlyList<CadScaleListRow> Existing { get; set; }

        /// <summary>The question Add Scale asks over a name used: "1:25 already exists.  Would you like to redefine this scale?".</summary>
        /// <param name="name">The name of the scale.</param>
        /// <returns>The question.</returns>
        public static string RedefineQuestion(string name) =>
            name + " already exists.  Would you like to redefine this scale?";

        /// <summary>OK: the name and units checked, <see cref="Result"/> made from them.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Ok()
        {
            string name = _name.Trim();
            Redefined = null;

            if (name.Length == 0)
            {
                Message = "No name specified for this scale.  Please enter a name.";
                return false;
            }

            if (name.Any(char.IsControl))
            {
                Message = "Must enter a valid name.";
                return false;
            }

            string used = _others.FirstOrDefault(o => string.Equals(o, name, StringComparison.OrdinalIgnoreCase));
            CadScaleListRow existing =
                Scale == null
                    ? Existing?.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
                    : null;

            if (used != null && existing == null)
            {
                Message = used + " already exists.";
                return false;
            }

            if (existing != null && existing.IsKept)
            {
                Message =
                    existing.Name + " already exists and is a referenced scale. Please enter a different scale name.";
                return false;
            }

            if (!TryUnits(_paper, out double paper) || !TryUnits(_drawing, out double drawing))
            {
                Message = "Invalid scale number.";
                return false;
            }

            if (existing != null)
            {
                if (ConfirmRedefine != null && !ConfirmRedefine(existing.Name))
                {
                    Message = string.Empty;
                    return false;
                }

                Redefined = existing;
                name = existing.Name;
            }

            if (!CanChangeUnits)
            {
                paper = Scale.PaperUnits;
                drawing = Scale.DrawingUnits;
            }

            Message = string.Empty;
            Result =
                Redefined != null ? Redefined.With(name, paper, drawing)
                : Scale == null ? new CadScaleListRow(name, paper, drawing)
                : Scale.With(name, paper, drawing);
            return true;
        }

        private static bool TryUnits(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && value > 0.0
            && !double.IsInfinity(value);
    }

    /// <summary>
    /// AutoCAD 2021's Edit Drawing Scales of SCALELISTEDIT over the request its command hands the window: the drawing's scale
    /// list, Add…, Edit… (a double click too), Move Up, Move Down, Delete and Reset, and the scale chosen with its units. Delete
    /// is greyed for a scale the drawing keeps (the unit scale, the current annotation scale, one in use); Reset asks first,
    /// then deletes every scale the drawing does not keep and adds the default ones it lacks. OK writes every change as one
    /// undo step; Cancel drops them all.
    /// </summary>
    public sealed class ScaleListEditViewModel : ObservableObject
    {
        /// <summary>The heading of Reset's question, as the task dialog of AutoCAD 2021's Edit Drawing Scales has it.</summary>
        public const string ResetTitle = "Scale List - Reset";

        /// <summary>The line of Reset's question, as AutoCAD 2021's task dialog has it.</summary>
        public const string ResetQuestion =
            "Resetting will restore a default list of scales for the drawing. Which default scale list do you want to restore?";

        /// <summary>The detail of Reset's question, which AutoCAD 2021's task dialog shows under Show details.</summary>
        public const string ResetDetail =
            "Unreferenced scales will be purged from the drawing and the default scales will be added. Any changes you made to the scale list will be lost.";

        private ScaleRowViewModel _selected;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of SCALELISTEDIT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public ScaleListEditViewModel(CadScaleListRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Scales = new ObservableCollection<ScaleRowViewModel>(request.Scales.Select(s => new ScaleRowViewModel(s)));
            _selected = Scales.FirstOrDefault();
        }

        /// <summary>Gets the request.</summary>
        public CadScaleListRequest Request { get; }

        /// <summary>Gets or sets how the window shows Add Scale or Edit Scale: true for OK.</summary>
        public Func<ScaleEditViewModel, bool> AskScale { get; set; }

        /// <summary>Gets or sets how the window asks Reset's question: true for Yes. With none set, Reset resets.</summary>
        public Func<bool> ConfirmReset { get; set; }

        /// <summary>
        /// Gets the label of the button of Reset's question that restores the default list: "Metric scales", or "Imperial
        /// scales" where the default list is the imperial one, as AutoCAD 2021's task dialog words its buttons.
        /// </summary>
        public string ResetChoice =>
            Request.Defaults.Any(s => s.Name.IndexOf('\'') >= 0) ? "Imperial scales" : "Metric scales";

        /// <summary>Gets the scales, in the order of the list.</summary>
        public ObservableCollection<ScaleRowViewModel> Scales { get; }

        /// <summary>Gets or sets the scale chosen.</summary>
        public ScaleRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    RaiseChosen();
                }
            }
        }

        /// <summary>Gets the legend of the box under the list: "Selected scale: 1:50".</summary>
        public string SelectedTitle => "Selected scale: " + (_selected?.Name ?? string.Empty);

        /// <summary>Gets the paper units of the scale chosen.</summary>
        public string PaperUnits =>
            _selected == null ? string.Empty : CadScaleListRow.Number(_selected.Scale.PaperUnits);

        /// <summary>Gets the drawing units of the scale chosen.</summary>
        public string DrawingUnits =>
            _selected == null ? string.Empty : CadScaleListRow.Number(_selected.Scale.DrawingUnits);

        /// <summary>Gets the line of the scale chosen: "1 paper unit = 50 drawing units".</summary>
        public string Sentence => _selected?.Scale.Sentence() ?? string.Empty;

        /// <summary>Gets the line at the foot: "18 scales".</summary>
        public string Summary => MainViewModel.Count(Scales.Count) + (Scales.Count == 1 ? " scale" : " scales");

        /// <summary>Gets whether Edit… is live: a scale chosen.</summary>
        public bool CanEdit => _selected != null;

        /// <summary>Gets whether Move Up is live: a scale chosen that is not the first.</summary>
        public bool CanMoveUp => _selected != null && Scales.IndexOf(_selected) > 0;

        /// <summary>Gets whether Move Down is live: a scale chosen that is not the last.</summary>
        public bool CanMoveDown => _selected != null && Scales.IndexOf(_selected) < Scales.Count - 1;

        /// <summary>Gets whether Delete is live: a scale chosen that the drawing does not keep.</summary>
        public bool CanDelete => _selected != null && !_selected.Scale.IsKept;

        /// <summary>
        /// Add…: Add Scale shown, its scale added at the end of the list and chosen; or, where its name is another scale's and
        /// the question to redefine it was answered Yes, that scale given the units and chosen.
        /// </summary>
        /// <returns>Whether a scale was added.</returns>
        public bool Add()
        {
            var dialog = new ScaleEditViewModel(Scales.Select(s => s.Name))
            {
                Existing = Scales.Select(s => s.Scale).ToList(),
            };

            if (AskScale == null || !AskScale(dialog) || dialog.Result == null)
            {
                return false;
            }

            if (dialog.Redefined != null)
            {
                ScaleRowViewModel redefined = Scales.First(s => s.Scale == dialog.Redefined);
                redefined.Scale = dialog.Result;
                Selected = redefined;
                RaiseChosen();
                return true;
            }

            var row = new ScaleRowViewModel(dialog.Result);
            Scales.Add(row);
            Selected = row;
            Raise(nameof(Summary));
            RaiseChosen();
            return true;
        }

        /// <summary>Edit…: Edit Scale shown over the scale chosen, its name and units taken.</summary>
        /// <returns>Whether the scale was changed.</returns>
        public bool Edit()
        {
            if (_selected == null)
            {
                return false;
            }

            ScaleRowViewModel row = _selected;
            var dialog = new ScaleEditViewModel(Scales.Where(s => s != row).Select(s => s.Name), row.Scale);

            if (AskScale == null || !AskScale(dialog) || dialog.Result == null)
            {
                return false;
            }

            row.Scale = dialog.Result;
            RaiseChosen();
            return true;
        }

        /// <summary>Move Up: the scale chosen one place up the list.</summary>
        public void MoveUp() => Move(-1);

        /// <summary>Move Down: the scale chosen one place down the list.</summary>
        public void MoveDown() => Move(1);

        /// <summary>Delete: the scale chosen taken out, the next one chosen; nothing for one the drawing keeps.</summary>
        /// <returns>Whether it was taken out.</returns>
        public bool Delete()
        {
            if (!CanDelete)
            {
                return false;
            }

            int at = Scales.IndexOf(_selected);
            Scales.RemoveAt(at);
            _selected = Scales.Count == 0 ? null : Scales[Math.Min(at, Scales.Count - 1)];
            Raise(nameof(Selected));
            Raise(nameof(Summary));
            RaiseChosen();
            return true;
        }

        /// <summary>
        /// Reset, once its question is answered Yes: every scale the drawing does not keep taken out, and the default scales
        /// the list lacks added after those kept, as AutoCAD's Reset and -SCALELISTEDIT Reset do.
        /// </summary>
        /// <returns>Whether the list was reset.</returns>
        public bool Reset()
        {
            if (ConfirmReset != null && !ConfirmReset())
            {
                return false;
            }

            string chosen = _selected?.Name;

            foreach (ScaleRowViewModel row in Scales.Where(s => !s.Scale.IsKept).ToList())
            {
                Scales.Remove(row);
            }

            foreach (CadScaleListRow scale in Request.Defaults)
            {
                if (!Scales.Any(s => string.Equals(s.Name, scale.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    Scales.Add(new ScaleRowViewModel(scale));
                }
            }

            _selected = Scales.FirstOrDefault(s => string.Equals(s.Name, chosen, StringComparison.OrdinalIgnoreCase))
                ?? Scales.FirstOrDefault();
            Raise(nameof(Selected));
            Raise(nameof(Summary));
            RaiseChosen();
            return true;
        }

        /// <summary>OK: the list as it stands handed to the command, which writes it as one undo step.</summary>
        public void Ok()
        {
            Request.Scales = Scales.Select(s => s.Scale).ToList();
            Request.Action = CadDialogAction.Ok;
        }

        /// <summary>Cancel: nothing written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void Move(int step)
        {
            int at = _selected == null ? -1 : Scales.IndexOf(_selected);
            int to = at + step;

            if (at < 0 || to < 0 || to >= Scales.Count)
            {
                return;
            }

            Scales.Move(at, to);
            RaiseChosen();
        }

        private void RaiseChosen()
        {
            Raise(nameof(SelectedTitle));
            Raise(nameof(PaperUnits));
            Raise(nameof(DrawingUnits));
            Raise(nameof(Sentence));
            Raise(nameof(CanEdit));
            Raise(nameof(CanMoveUp));
            Raise(nameof(CanMoveDown));
            Raise(nameof(CanDelete));
        }
    }
}
