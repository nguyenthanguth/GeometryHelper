using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>How an object of the model changed, as a row of Update from model shows it.</summary>
    public enum ModelChangeKind
    {
        /// <summary>Drawn before and now, changed.</summary>
        Changed,

        /// <summary>New in the model.</summary>
        Added,

        /// <summary>Gone from the model.</summary>
        Removed,
    }

    /// <summary>A row of the model changes: the object, how it changed and a detail, "Length 3 000 → 3 500".</summary>
    public sealed class ModelChangeRow
    {
        internal ModelChangeRow(string objectText, ModelChangeKind change, string detail)
        {
            Object = objectText;
            Change = change;
            Detail = detail;
        }

        /// <summary>Gets the object: its mark and name, "B1 · Beam".</summary>
        public string Object { get; }

        /// <summary>Gets how it changed.</summary>
        public ModelChangeKind Change { get; }

        /// <summary>Gets the detail.</summary>
        public string Detail { get; }

        /// <inheritdoc/>
        public override string ToString() => Object + " " + Change + " " + Detail;
    }

    /// <summary>A row of the hand edits: the edit in words and what becomes of it, kept or no longer applying and why.</summary>
    public sealed class HandEditRow
    {
        internal HandEditRow(string edit, bool kept, string status)
        {
            Edit = edit;
            IsKept = kept;
            Status = status;
        }

        /// <summary>Gets the edit in words: "Mark P1 moved 40, 0".</summary>
        public string Edit { get; }

        /// <summary>Gets whether it is kept.</summary>
        public bool IsKept { get; }

        /// <summary>Gets what becomes of it: "Kept", or "No longer applies: the object B1-plate-2 is no longer in the model".</summary>
        public string Status { get; }

        /// <inheritdoc/>
        public override string ToString() => Edit + ": " + Status;
    }

    /// <summary>
    /// The Update from model dialog, as the design draws it: the source to update from, what changed in the model, which hand
    /// edits are kept and which no longer apply and why, then Update, one undo step.
    /// </summary>
    /// <remarks>
    /// The changes are found before anything is done: a copy of the drawing is updated off the UI thread and its report read.
    /// The beam B1 made 3 500 long with a stiffener added shows "B1 · Beam, Changed, Length 3 000 → 3 500" and "S1 ·
    /// Stiffener, Added"; a mark moved by hand shows Kept. Update then updates the drawing itself; undo puts it back.
    /// </remarks>
    public sealed class UpdateFromModelViewModel : DialogViewModel
    {
        private readonly DrawingWorkspace _workspace;
        private IDrawingSourceProvider _provider;
        private DrawingSource _newSource;
        private UpdateReport _preview;
        private IReadOnlyList<ModelChangeRow> _changes = Array.Empty<ModelChangeRow>();
        private IReadOnlyList<HandEditRow> _edits = Array.Empty<HandEditRow>();
        private string _summary = string.Empty;
        private string _plugins = string.Empty;
        private int _previews;

        /// <summary>Makes the dialog's view model for a model drawing; the changes are found by <see cref="PreviewAsync"/>.</summary>
        /// <param name="workspace">The model drawing of the presenter.</param>
        /// <param name="drawing">The drawing updated.</param>
        /// <exception cref="ArgumentNullException">Thrown when either is null.</exception>
        public UpdateFromModelViewModel(DrawingWorkspace workspace, ModelDrawing drawing)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            Sources = workspace.Providers.Where(p => p.IsAvailable).ToList();
            _provider = Sources.Contains(drawing.Provider) ? drawing.Provider
                : Sources.FirstOrDefault(p => string.Equals(p.Name, drawing.SourceName, StringComparison.Ordinal)) ?? Sources.FirstOrDefault();
            UpdateCommand = new DelegateCommand(_ => _ = UpdateAsync(), _ => !IsBusy && _newSource != null);
            CancelCommand = new DelegateCommand(_ => Close(false));
        }

        /// <summary>Gets the drawing updated.</summary>
        public ModelDrawing Drawing { get; }

        /// <summary>Gets the header's text: "B-101 · Beam B1".</summary>
        public string Title => Drawing.Number + " · " + Drawing.Name;

        /// <summary>Gets the sources that can give objects now.</summary>
        public IReadOnlyList<IDrawingSourceProvider> Sources { get; }

        /// <summary>Gets or sets the source to update from; setting another finds the changes again.</summary>
        public IDrawingSourceProvider Source
        {
            get => _provider;
            set
            {
                if (value != null && Set(ref _provider, value))
                {
                    _ = PreviewAsync();
                }
            }
        }

        /// <summary>Gets the line on top: "The model changed since 2026-11-08 10:42: 2 changed, 1 added, 0 removed. ..."</summary>
        public string Summary
        {
            get => _summary;
            private set => Set(ref _summary, value);
        }

        /// <summary>Gets whether the model changed at all.</summary>
        public bool HasChanges => _preview != null && _preview.AddedObjects.Count + _preview.RemovedObjects.Count + _preview.ChangedObjects.Count > 0;

        /// <summary>Gets the objects of the model changed, added and taken away, each list in the order of the ids.</summary>
        public IReadOnlyList<ModelChangeRow> Changes
        {
            get => _changes;
            private set => Set(ref _changes, value);
        }

        /// <summary>Gets the hand edits, in the order they were made, each with what becomes of it.</summary>
        public IReadOnlyList<HandEditRow> Edits
        {
            get => _edits;
            private set
            {
                if (Set(ref _edits, value))
                {
                    Raise(nameof(HasEdits));
                }
            }
        }

        /// <summary>Gets whether the drawing has hand edits.</summary>
        public bool HasEdits => _edits.Count > 0;

        /// <summary>Gets the line about the plugin runs kept in the drawing.</summary>
        public string PluginText
        {
            get => _plugins;
            private set => Set(ref _plugins, value);
        }

        /// <summary>Gets the report of the update; null until <see cref="UpdateAsync"/> has run.</summary>
        public UpdateReport Report { get; private set; }

        /// <summary>Gets the command of Update.</summary>
        public ICommand UpdateCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>
        /// Finds what an update from the source chosen would change, without changing the drawing: a copy of it, saved and
        /// opened again, is updated off the UI thread and its report read.
        /// </summary>
        /// <returns>The report of the copy; null where the source gave nothing or the work was stopped.</returns>
        public async Task<UpdateReport> PreviewAsync()
        {
            int preview = ++_previews;
            IDrawingSourceProvider provider = _provider;
            DrawingSource old = Drawing.Drawing.Source;

            if (provider == null)
            {
                Summary = old == null
                    ? "The model " + Drawing.Number + " was made from is not at hand: the drawing was opened from its file, and no source can give objects now."
                    : "No source can give objects now.";
                return null;
            }

            string copy = Path.Combine(Path.GetTempPath(), "gk", "drawing", "update-preview", Guid.NewGuid().ToString("N") + ".dxf");
            Directory.CreateDirectory(Path.GetDirectoryName(copy));
            Drawing.Drawing.Save(copy);
            Tolerance tolerance = _workspace.Tolerance;
            DrawingSource newSource;
            UpdateReport report;

            try
            {
                var result = await RunAsync("Finding what changed in " + provider.Name + "…", () =>
                {
                    DrawingSource source = DrawingWorkspace.SourceFor(Drawing, provider.CreateObjects());

                    // A drawing opened from its file does not know its source: one that has none of its objects is not it.
                    if (old == null && source.Objects.Count == 0)
                    {
                        return new KeyValuePair<DrawingSource, UpdateReport>(null, null);
                    }

                    var scratch = global::GeometryHelper.Drawing.Drawing.Open(copy, old, tolerance);
                    return new KeyValuePair<DrawingSource, UpdateReport>(source, scratch.Update(source));
                });
                newSource = result.Key;
                report = result.Value;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                if (preview == _previews)
                {
                    Summary = "The changes could not be found: " + ex.Message;
                    _newSource = null;
                    ((DelegateCommand)UpdateCommand).RaiseCanExecuteChanged();
                }

                return null;
            }
            finally
            {
                TryDelete(copy);
            }

            if (preview != _previews)
            {
                return report;
            }

            if (newSource == null)
            {
                Summary = "None of the objects " + Drawing.Number + " draws is in " + provider.Name + ": choose the source the drawing was made from.";
                _newSource = null;
                ((DelegateCommand)UpdateCommand).RaiseCanExecuteChanged();
                return null;
            }

            // A drawing opened from its file is made again from the source its objects were found in, by Drawing settings too.
            if (old == null)
            {
                Drawing.SourceName = provider.Name;
            }

            _newSource = newSource;
            _preview = report;
            Show(old, newSource, report);
            ((DelegateCommand)UpdateCommand).RaiseCanExecuteChanged();
            return report;
        }

        /// <summary>
        /// Updates the drawing from the source chosen: made off the UI thread (<see cref="global::GeometryHelper.Drawing.Drawing.Prepare"/>),
        /// then written as one undo step of the presenter (<see cref="global::GeometryHelper.Drawing.Drawing.Apply"/>).
        /// </summary>
        /// <returns>The report; null where nothing was updated.</returns>
        public async Task<UpdateReport> UpdateAsync()
        {
            if (_newSource == null)
            {
                await PreviewAsync();
            }

            if (_newSource == null)
            {
                return null;
            }

            UpdateReport report;

            try
            {
                // The slow part off the UI thread; the writing, one undo step of the document, on it.
                DrawingSource source = _newSource;
                global::GeometryHelper.Drawing.Drawing drawing = Drawing.Drawing;
                _workspace.Begin(Drawing);

                try
                {
                    PreparedUpdate prepared = await RunAsync("Making " + Drawing.Number + " again from " + _provider.Name + "…", () => drawing.Prepare(source, null));
                    _workspace.ThrowIfClosed(Drawing);
                    report = _workspace.WriteChange(Drawing, _provider, Drawing.PresetName, () => drawing.Apply(prepared));
                }
                finally
                {
                    _workspace.End(Drawing);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _workspace.ShowMessage(MessageViewModel.Failure("Drawing not updated", Drawing.Number + " was left as it was.", ex));
                return null;
            }

            Report = report;
            Drawing.IsOutOfDate = false;
            _workspace.Changed(Drawing);
            _workspace.Say(Drawing.Number + " updated: " + DrawingWorkspace.Count(report.EditsKept.Count, "hand edit") + " kept, "
                + report.EditsDropped.Count.ToString(CultureInfo.InvariantCulture) + " no longer " + (report.EditsDropped.Count == 1 ? "applies" : "apply") + ".");

            foreach (DroppedEdit dropped in report.EditsDropped)
            {
                _workspace.Say("  No longer applies: " + Describe(dropped.Edit) + ": " + dropped.Reason + ".");
            }

            Close(true);
            return report;
        }

        /// <inheritdoc/>
        protected override void OnBusyChanged() => ((DelegateCommand)UpdateCommand)?.RaiseCanExecuteChanged();

        private void Show(DrawingSource old, DrawingSource now, UpdateReport report)
        {
            var rows = new List<ModelChangeRow>();

            foreach (string id in report.ChangedObjects)
            {
                if (old != null && old.TryGetObject(id, out DrawingObject before) && now.TryGetObject(id, out DrawingObject after))
                {
                    rows.Add(new ModelChangeRow(Name(after), ModelChangeKind.Changed, SourceComparer.Describe(before, after, _workspace.Tolerance)));
                }
            }

            foreach (string id in report.AddedObjects)
            {
                if (now.TryGetObject(id, out DrawingObject added))
                {
                    rows.Add(new ModelChangeRow(Name(added), ModelChangeKind.Added, "New " + DrawingProperties.KindName(added.Kind) + (added.Profile.Length == 0 ? string.Empty : " " + added.Profile)
                        + (added.ParentId == null ? string.Empty : " in " + added.ParentId)));
                }
            }

            foreach (string id in report.RemovedObjects)
            {
                rows.Add(new ModelChangeRow(old != null && old.TryGetObject(id, out DrawingObject removed) ? Name(removed) : id, ModelChangeKind.Removed, "No longer in the model"));
            }

            Changes = rows;
            var edits = new List<HandEditRow>();
            var dropped = new Dictionary<DrawingEdit, string>();

            foreach (DroppedEdit one in report.EditsDropped)
            {
                if (!dropped.ContainsKey(one.Edit))
                {
                    dropped.Add(one.Edit, one.Reason);
                }
            }

            foreach (DrawingEdit edit in Drawing.Drawing.Edits)
            {
                edits.Add(dropped.TryGetValue(edit, out string reason)
                    ? new HandEditRow(Describe(edit), false, "No longer applies: " + reason)
                    : new HandEditRow(Describe(edit), true, "Kept"));
            }

            Edits = edits;
            string since = Drawing.LastMade.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Summary = old == null
                ? "The drawing was opened from its file, which does not keep the model it was made from: Update makes it again from " + _provider.Name
                  + ", " + report.AddedObjects.Count.ToString(CultureInfo.InvariantCulture) + " added, " + report.RemovedObjects.Count.ToString(CultureInfo.InvariantCulture)
                  + " removed; hand edits that still apply are kept."
                : HasChanges
                ? "The model changed since " + since + ": " + report.ChangedObjects.Count.ToString(CultureInfo.InvariantCulture) + " changed, "
                  + report.AddedObjects.Count.ToString(CultureInfo.InvariantCulture) + " added, " + report.RemovedObjects.Count.ToString(CultureInfo.InvariantCulture)
                  + " removed. The drawing will be made again; hand edits that still apply are kept."
                : "The model has not changed since " + since + ". Update makes the drawing again all the same; hand edits are kept.";
            // The preview is made with no plugins, so every run the drawing keeps is among those to run again.
            int runs = report.PluginRunsToRunAgain.Count + report.PluginRunsRun.Count + report.PluginRunsDropped.Count;
            PluginText = runs == 0
                ? "No plugin is set to run on update."
                : DrawingWorkspace.Count(runs, "plugin run") + " kept in the drawing, run again with the drawing plugins loaded; a run whose objects have gone is dropped.";
            Raise(nameof(HasChanges));
        }

        /// <summary>A hand edit in words, as the design writes them: "Mark P1 moved 40, 0", "Dimension 3000: text set to …".</summary>
        private string Describe(DrawingEdit edit)
        {
            DrawingElement element = Drawing.Drawing.Elements.FirstOrDefault(e => string.Equals(e.Id, edit.ObjectId, StringComparison.Ordinal));
            string what = element == null ? edit.ObjectId : element.Kind + " " + (Value(element, "Text") ?? Value(element, "Name") ?? element.Id);

            switch (edit.Kind)
            {
                case CadEditKind.Move:
                    return what + " moved " + (edit.NewValue ?? string.Empty).Replace(",", ", ");
                case CadEditKind.Erase:
                    return what + " erased";
                case CadEditKind.Property:
                    return what + ": " + (edit.Property ?? "value") + " set to \"" + (edit.NewValue ?? string.Empty) + "\"";
                default:
                    return what + " " + edit.Kind.ToString().ToLowerInvariant();
            }
        }

        private static string Value(DrawingElement element, string name) => element.Values.Where(v => v.Key == name).Select(v => v.Value).FirstOrDefault(v => !string.IsNullOrEmpty(v));

        private static string Name(DrawingObject item)
            => (item.Mark.Length > 0 ? item.Mark + " · " : string.Empty) + (item.Name.Length > 0 ? item.Name.Substring(0, 1) + item.Name.Substring(1).ToLowerInvariant() : item.Id);

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                GeometryHelperLog.Debug("UpdateFromModelViewModel: the copy " + path + " was left: " + ex.Message);
            }
        }
    }
}
