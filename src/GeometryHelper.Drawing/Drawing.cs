using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// A drawing of model objects (L7): its views with their lines, dimensions and marks, its sheet, and the document that
    /// keeps it, every element with a stable id and its links to the model, the hand edits made to it and the plugin runs.
    /// It lives in one DXF (A19): <see cref="Database"/> holds the sheet and, in its dictionary GH_DRAWING, the document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A drawing is made by <see cref="DrawingBuilder.Build"/>, saved by <see cref="Save"/> as one DXF and opened again by
    /// <see cref="Open"/>. <see cref="Update(DrawingSource)"/> makes it again from the model as it is now: it takes out only the entities it
    /// wrote, writes them anew, applies the hand edits that still apply and reports those that no longer do; what was drawn
    /// by hand on it stays. A beam drawn 3 000 long and updated to 3 500 has its overall dimension read 3500, a mark moved 40
    /// to the right still 40 to the right of where the drawing puts it, and a line drawn by hand on the sheet still there.
    /// </para>
    /// <para>
    /// <see cref="Update(DrawingSource, DrawingSettings)"/> makes it again with other settings, its hand edits and what was
    /// drawn by hand kept the same way; <see cref="Prepare"/> and <see cref="Apply"/> split that in two, so that a window
    /// makes the drawing on a thread of its own and writes it on its own. <see cref="ReDimension"/>, <see cref="ReMark"/>,
    /// <see cref="ReCentreLines"/>, <see cref="AddView"/>, <see cref="AddSection"/> and <see cref="AddDetail"/> change the
    /// settings for one view and make it again: TC2 given the right end view dimensioned and a section B-B cut along a line
    /// at 1 000 keeps both through later updates.
    /// </para>
    /// <para>
    /// <see cref="OpenInPresenter"/> shows the drawing in the 2D presenter with its owner registered: a projected line there
    /// cannot be moved, a mark or a dimension can, and the edit is recorded here as a hand edit, one undo step with it.
    /// </para>
    /// </remarks>
    public sealed class Drawing
    {
        private readonly List<State> _states = new List<State>();
        private DrawingSettings _settings;
        private DrawingSource _source;
        private DrawingContent _content;
        private DxfDatabase _database;
        private CadDocument _presenter;
        private double _scale;
        private List<DrawingElement> _views = new List<DrawingElement>();
        private List<DrawingElement> _annotations = new List<DrawingElement>();
        private Dictionary<string, DrawingElement> _elements = new Dictionary<string, DrawingElement>(StringComparer.Ordinal);
        private List<DrawingEdit> _edits = new List<DrawingEdit>();
        private List<DrawingPluginRun> _runs = new List<DrawingPluginRun>();
        private List<IReadOnlyList<DxfTypedValue>> _unknown = new List<IReadOnlyList<DxfTypedValue>>();
        private List<string> _warnings = new List<string>();

        internal Drawing(string id, DrawingSource source, DrawingSettings settings, Tolerance tolerance, DrawingContent content)
        {
            Id = id;
            _source = source;
            _settings = settings;
            Tolerance = tolerance;
            Take(content);
            _database = new DxfDatabase("AC1027");
            DrawingDxfWriter.WriteContent(_database, content, settings, _edits, tolerance);
            DrawingDocumentStore.Write(_database, Records());
        }

        private Drawing(string id, DrawingSource source, DrawingSettings settings, Tolerance tolerance, DxfDatabase database, DrawingDocumentRecords records)
        {
            Id = id;
            _source = source;
            _settings = settings;
            Tolerance = tolerance;
            _database = database;
            Read(records);

            foreach (DrawingElement view in _views)
            {
                foreach (KeyValuePair<string, string> value in view.Values)
                {
                    if (value.Key == "Scale" && double.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                    {
                        _scale = scale;
                        break;
                    }
                }

                if (_scale > 0.0)
                {
                    break;
                }
            }
        }

        /// <summary>Gets the drawing's id: the id of its main object.</summary>
        public string Id { get; }

        /// <summary>Gets the objects the drawing was last made from; null for a drawing opened with none and not updated since.</summary>
        public DrawingSource Source => _source;

        /// <summary>Gets a copy of the settings the drawing is made with; changing the copy changes nothing of the drawing.</summary>
        public DrawingSettings Settings => _settings.Clone();

        /// <summary>Gets the tolerance the drawing is made within.</summary>
        public Tolerance Tolerance { get; }

        /// <summary>Gets the scale of the views, 20 for 1:20; a detail has its own.</summary>
        public double Scale => _scale;

        /// <summary>
        /// Gets the views, with their lines, dimensions and marks; empty for a drawing opened from a file and not updated since,
        /// whose lines are in its DXF and whose views are in <see cref="Elements"/>.
        /// </summary>
        public IReadOnlyList<DrawingView> Views => _content == null ? (IReadOnlyList<DrawingView>)new DrawingView[0] : _content.Views.AsReadOnly();

        /// <summary>Gets the sheet; null for a drawing opened from a file and not updated since.</summary>
        public DrawingSheet Sheet => _content?.Sheet;

        /// <summary>
        /// Gets the elements of the document: the views, then the settings, the sheet, the title block and the part list,
        /// then each view's dimensions and marks.
        /// </summary>
        public IReadOnlyList<DrawingElement> Elements
        {
            get
            {
                var all = new List<DrawingElement>(_views.Count + _annotations.Count);
                all.AddRange(_views);
                all.AddRange(_annotations);
                return all.AsReadOnly();
            }
        }

        /// <summary>Gets the hand edits, in the order they were made.</summary>
        public IReadOnlyList<DrawingEdit> Edits => _edits.AsReadOnly();

        /// <summary>Gets the plugin runs, in the order they ran.</summary>
        public IReadOnlyList<DrawingPluginRun> PluginRuns => _runs.AsReadOnly();

        /// <summary>Gets what went wrong in making the drawing, in words: views that did not fit the sheet.</summary>
        public IReadOnlyList<string> Warnings => _warnings.AsReadOnly();

        /// <summary>
        /// Gets the DXF the drawing lives in: the presenter's, once <see cref="OpenInPresenter"/> has shown it, since undoing
        /// there reads the drawing anew.
        /// </summary>
        public DxfDatabase Database => _presenter != null ? _presenter.Database : _database;

        /// <summary>What the drawing was last made of; null for a drawing opened and not updated.</summary>
        internal DrawingContent Content => _content;

        /// <summary>The settings themselves, not a copy.</summary>
        internal DrawingSettings SettingsInUse => _settings;

        /// <summary>
        /// Opens a drawing saved as a DXF: its document read back from the DXF, its entities found there by their tags.
        /// </summary>
        /// <param name="path">The path of the DXF.</param>
        /// <param name="source">The objects the drawing was made from, to compare an update with; null where they are not at hand.</param>
        /// <param name="tolerance">The tolerance the drawing is made within when it is updated.</param>
        /// <returns>The drawing: its elements, edits and plugin runs as saved, its views empty until <see cref="Update(DrawingSource)"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the DXF carries no drawing document.</exception>
        public static Drawing Open(string path, DrawingSource source, Tolerance tolerance)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            DxfDatabase database = DxfDatabase.Open(path);
            DrawingDocumentRecords records = DrawingDocumentStore.Read(database);

            if (records == null)
            {
                throw new ArgumentException("The file " + path + " carries no model drawing.", nameof(path));
            }

            DrawingSettings settings = new DrawingSettings();

            foreach (DrawingElement element in records.Annotations)
            {
                if (element.Id == DrawingBuilder.SettingsId)
                {
                    settings = DrawingSettings.FromValues(element.Values);
                    break;
                }
            }

            string id = string.IsNullOrEmpty(records.DrawingId) ? Path.GetFileNameWithoutExtension(path) : records.DrawingId;
            var drawing = new Drawing(id, source, settings, tolerance, database, records);
            GeometryHelperLog.Info("Drawing.Open: " + records + " read from " + path + ".");
            return drawing;
        }

        /// <summary>Saves the drawing as one DXF: its sheet and its document.</summary>
        /// <param name="path">The path of the file.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            Database.Save(path);
            GeometryHelperLog.Info("Drawing.Save: " + Id + " saved to " + path + ".");
        }

        /// <summary>
        /// Shows the drawing in the 2D presenter: a document over the drawing's DXF, the drawing's owner registered with it,
        /// so that an edit of an entity the drawing wrote goes through the drawing (<see cref="DrawingOwner"/>). The drawing
        /// follows the document from then on: <see cref="Database"/> is its, and undoing there takes back the hand edit too.
        /// </summary>
        /// <returns>The document.</returns>
        public CadDocument OpenInPresenter()
        {
            CadDocument document = CadDocument.From(Database, Id + ".dxf");
            document.Owners.Register(new DrawingOwner(this));
            document.Changed += OnPresenterChanged;

            if (_presenter != null)
            {
                _presenter.Changed -= OnPresenterChanged;
            }

            _presenter = document;
            GeometryHelperLog.Info("Drawing.OpenInPresenter: " + Id + " shown with its owner registered.");
            return document;
        }

        /// <summary>
        /// Makes the drawing again from the model as it is now: views, dimensions, marks, symbols and sheet, with the settings
        /// it has. Only the entities it wrote are taken out and written anew; the hand edits that still apply are applied
        /// again, those that no longer do are taken out and reported; the plugin runs are kept and reported as to be run again.
        /// </summary>
        /// <param name="newSource">The objects as they are now.</param>
        /// <returns>What changed and what became of the hand edits.</returns>
        /// <remarks>
        /// Shown in the presenter, the update is one undo step there. Undoing it brings back the DXF, the hand edits, the
        /// settings and the views as they were. The same as <c>Apply(Prepare(newSource, null))</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the source is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the presenter refuses or fails the update, as while a
        /// command of it runs; the drawing is then left as it was.</exception>
        public UpdateReport Update(DrawingSource newSource) => Update(newSource, null);

        /// <summary>
        /// Makes the drawing again from the model as it is now with other settings: other views, scales, sheet, dimensions,
        /// marks or symbols, the hand edits that still apply kept and what was drawn by hand left as it is, as
        /// <see cref="Update(DrawingSource)"/> keeps them.
        /// </summary>
        /// <param name="newSource">The objects as they are now.</param>
        /// <param name="settings">The settings to make it with from now on, copied; null for those it has.</param>
        /// <returns>What changed and what became of the hand edits.</returns>
        /// <remarks>
        /// TC2 drawn on an A3 and made again on an A2 with its top view taken out keeps the mark of its end plate where it was
        /// dragged to, and the line drawn by hand on the sheet; the edits of the top view's dimensions are dropped and said.
        /// Shown in the presenter, it is one undo step there, which takes the settings back with the rest. The same as
        /// <c>Apply(Prepare(newSource, settings))</c>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the source is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the settings allow no scale, or one that is not a finite number
        /// above nought, or ask for a sheet of its own size with no width or height.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the presenter refuses or fails the update; the drawing is then
        /// left as it was.</exception>
        public UpdateReport Update(DrawingSource newSource, DrawingSettings settings) => Apply(Prepare(newSource, settings));

        /// <summary>
        /// Makes the drawing again from a source with settings, without writing it: the slow part of an update, which touches
        /// neither the drawing nor its DXF and may run on a thread of its own while the drawing is not changed.
        /// </summary>
        /// <param name="newSource">The objects as they are now.</param>
        /// <param name="settings">The settings to make it with, copied; null for those the drawing has.</param>
        /// <returns>The update, to be written by <see cref="Apply"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the source is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the settings allow no scale, or one that is not a finite number
        /// above nought, or ask for a sheet of its own size with no width or height.</exception>
        public PreparedUpdate Prepare(DrawingSource newSource, DrawingSettings settings)
        {
            if (newSource == null)
            {
                throw new ArgumentNullException(nameof(newSource));
            }

            DrawingSettings copy = (settings ?? _settings).Clone();
            DrawingContent content = DrawingBuilder.Make(newSource, copy, Tolerance);
            DrawingBuilder.Elements(content, copy, out List<DrawingElement> views, out List<DrawingElement> annotations);
            return new PreparedUpdate(this, newSource, copy, content, views, annotations);
        }

        /// <summary>
        /// Writes an update this drawing prepared into its DXF, as <see cref="Update(DrawingSource, DrawingSettings)"/> does: the
        /// entities it wrote taken out and written anew, the hand edits that still apply applied again, what was drawn by hand
        /// left; shown in the presenter, one undo step there.
        /// </summary>
        /// <param name="update">The update, from <see cref="Prepare"/>.</param>
        /// <returns>What changed and what became of the hand edits.</returns>
        /// <remarks>
        /// The hand edits are judged as they are when the update is applied, so that an edit made while it was prepared is
        /// kept, or reported, as any other. Run it on the thread the presenter runs on.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the update is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the update was prepared by another drawing.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the update has been applied already, or the presenter
        /// refuses or fails it; the drawing is then left as it was.</exception>
        public UpdateReport Apply(PreparedUpdate update)
        {
            if (update == null)
            {
                throw new ArgumentNullException(nameof(update));
            }

            if (!ReferenceEquals(update.Drawing, this))
            {
                throw new ArgumentException("The update was prepared by another drawing.", nameof(update));
            }

            if (update.IsApplied)
            {
                throw new InvalidOperationException("The update has been applied already.");
            }

            DrawingSource newSource = update.Source;
            DrawingContent content = update.Content;
            List<DrawingElement> views = update.Views, annotations = update.Annotations;
            var next = Index(views, annotations);
            var objectIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (DrawingObject item in newSource.Objects)
            {
                objectIds.Add(item.Id);
            }

            var kept = new List<DrawingEdit>();
            var dropped = new List<DroppedEdit>();

            foreach (DrawingEdit edit in _edits)
            {
                string reason = WhyNot(edit, next, objectIds);

                if (reason == null)
                {
                    kept.Add(edit);
                }
                else
                {
                    dropped.Add(new DroppedEdit(edit, reason));
                }
            }

            UpdateReport report = Compare(newSource, next, kept, dropped);

            // The state is taken first, so that the records written are the new ones; it is put back if the presenter fails.
            State before = Current();
            List<DrawingEdit> oldEdits = _edits;
            _source = newSource;
            _settings = update.SettingsInUse;
            _content = content;
            _views = views;
            _annotations = annotations;
            _elements = next;
            _edits = kept;
            _warnings = content.Warnings;
            _scale = content.Scale;

            void Rewrite(DxfDatabase database)
            {
                DrawingEntityTags.RemoveAll(database);
                DrawingDxfWriter.WriteContent(database, content, _settings, kept, Tolerance);
                DrawingDocumentStore.Write(database, Records());
            }

            if (_presenter == null)
            {
                Rewrite(_database);
            }
            else
            {
                CadEditResult result = _presenter.ApplyEdit("UPDATE", null, scope =>
                {
                    Rewrite(scope.Database);
                    scope.Record(new CadEditRecord(CadEditRecordKind.TableChanged, null));
                });

                if (!result.Succeeded)
                {
                    Restore(before);
                    _edits = oldEdits;
                    throw new InvalidOperationException("The presenter did not take the update: " + result.Message);
                }

                // Undoing the step reads the document back from the DXF; these let the views, settings and source come back
                // with it.
                Remember(before);
                Remember(Current());
            }

            update.IsApplied = true;
            GeometryHelperLog.Info("Drawing.Update: " + Id + ": " + report + ".");
            return report;
        }

        /// <summary>
        /// Dimensions one view by the rules of the settings, whatever its kind, and keeps it dimensioned through later updates
        /// (<see cref="ViewSettings.DimensionedViews"/>): the drawing made again from its source, its hand edits kept.
        /// </summary>
        /// <param name="viewId">The id of the view, V-RIGHT.</param>
        /// <returns>What changed: the view's dimensions among <see cref="UpdateReport.AddedElements"/>, and what became of the
        /// hand edits.</returns>
        /// <remarks>
        /// The right end view of TC2 with the assembly preset, which dimensions the front and top views only, gets the overall
        /// height of the end plate and the pitch of its holes. A view dimensioned already is dimensioned again, as an update
        /// dimensions it. Shown in the presenter, it is one undo step there.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no view of the id.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, so that it has no
        /// source to make it from, or the presenter refuses the step.</exception>
        public UpdateReport ReDimension(string viewId) => ForView(viewId, s => AddOnce(s.Views.DimensionedViews, viewId));

        /// <summary>
        /// Marks one view as the settings mark the first, and keeps it marked through later updates
        /// (<see cref="ViewSettings.MarkedViews"/>): the drawing made again from its source, its hand edits kept.
        /// </summary>
        /// <param name="viewId">The id of the view, V-TOP.</param>
        /// <returns>What changed: the view's marks among <see cref="UpdateReport.AddedElements"/>, and what became of the hand
        /// edits.</returns>
        /// <remarks>
        /// The first view along the axes is always marked: marking it again places its marks again, a mark dragged by hand
        /// keeping its move. Shown in the presenter, it is one undo step there.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no view of the id.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, or the presenter
        /// refuses the step.</exception>
        public UpdateReport ReMark(string viewId) => ForView(viewId, s => AddOnce(s.Views.MarkedViews, viewId));

        /// <summary>
        /// Draws the centre lines of one view again from the holes, bolts and tubes it sees: the drawing made again from its
        /// source with the settings it has, its hand edits kept.
        /// </summary>
        /// <param name="viewId">The id of the view, V-FRONT.</param>
        /// <returns>What changed and what became of the hand edits; nothing where the source has not changed.</returns>
        /// <remarks>Every view has its centre lines; shown in the presenter, this is one undo step there.</remarks>
        /// <exception cref="ArgumentNullException">Thrown when the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no view of the id.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, or the presenter
        /// refuses the step.</exception>
        public UpdateReport ReCentreLines(string viewId) => ForView(viewId, s => { });

        /// <summary>
        /// Adds a view along the axes, or the isometric view, and makes the drawing again with it, its hand edits kept.
        /// </summary>
        /// <param name="kind">The view: front, top, bottom, left, right, back or isometric.</param>
        /// <returns>What changed: the view, V-BACK, among <see cref="UpdateReport.AddedElements"/>.</returns>
        /// <remarks>
        /// TC2 with the assembly preset given its back view has V-BACK beside its front view, as the projection method puts
        /// it. A view the drawing has already changes nothing but is made again. Shown in the presenter, one undo step.
        /// </remarks>
        /// <exception cref="ArgumentException">Thrown when the kind is a section, a detail or a free view.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, or the presenter
        /// refuses the step.</exception>
        public UpdateReport AddView(ViewKind kind)
        {
            if (kind == ViewKind.Section || kind == ViewKind.Detail || kind == ViewKind.Free || !Enum.IsDefined(typeof(ViewKind), kind))
            {
                throw new ArgumentException("A view added is one along the axes or the isometric one; a section or a detail is added by its own call.", nameof(kind));
            }

            return Change(s => AddOnce(s.Views.Kinds, kind));
        }

        /// <summary>
        /// Adds a section cut along a line drawn in a view along the axes and makes the drawing again with it, its hand edits
        /// kept: the section looks to the right of the line from its first point to its second and sees as far as a depth
        /// behind the cut (<see cref="SectionViewRequest(string, ViewKind, GeoLine2, double)"/>).
        /// </summary>
        /// <param name="name">The letter of the section, "B" for SECTION B-B; its view is V-SECTION-B.</param>
        /// <param name="viewId">The id of the view the line is drawn in: one along the axes, V-FRONT.</param>
        /// <param name="cutLine">The line, in the view's millimetres at 1:1.</param>
        /// <param name="depth">How far behind the cut the section sees, in model millimetres; infinity to see all.</param>
        /// <returns>What changed: the section view and its mark among <see cref="UpdateReport.AddedElements"/>.</returns>
        /// <remarks>
        /// TC2 cut along the line from (1000, -200) to (1000, 200) in its front view, 100 deep, gets SECTION B-B: the I of the
        /// beam seen looking towards its far end, and the letter B at both ends of the cut in the front view.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name or the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or names a section the drawing has, the drawing has
        /// no view of the id or it is not one along the axes, or the line has no length.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the depth is negative or not a number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, or the presenter
        /// refuses the step.</exception>
        public UpdateReport AddSection(string name, string viewId, GeoLine2 cutLine, double depth)
        {
            ViewKind kind = KindOf(viewId);
            var request = new SectionViewRequest(name, kind, cutLine, depth);
            NewName(name, Names(_settings.Views.Sections, s => s.Name));
            return Change(s => s.Views.Sections.Add(request));
        }

        /// <summary>
        /// Adds a detail of a rectangle of a view along the axes, drawn at a scale of its own, and makes the drawing again
        /// with it, its hand edits kept.
        /// </summary>
        /// <param name="name">The letter of the detail, "C" for DETAIL C; its view is V-DETAIL-C.</param>
        /// <param name="viewId">The id of the view the rectangle is drawn in: one along the axes or the isometric one.</param>
        /// <param name="area">The rectangle, in the view's millimetres at 1:1.</param>
        /// <param name="scale">The scale the detail is drawn at, 5 for 1:5: no smaller than the view's; the nearest of the
        /// drawing's scales no smaller is taken.</param>
        /// <returns>What changed: the detail view and its mark among <see cref="UpdateReport.AddedElements"/>.</returns>
        /// <remarks>
        /// The end of TC2, from (2800, -200) to (3100, 200) in its front view at 1:10, drawn at 1:5, is a detail twice as large
        /// as the view, and stays twice as large where a later update takes another scale for the views.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the name or the id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is empty or names a detail the drawing has, the drawing has
        /// no view of the id or it is a section or a detail, or the rectangle has no area.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought, or draws
        /// the detail smaller than its view.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing was opened and not updated, or the presenter
        /// refuses the step.</exception>
        public UpdateReport AddDetail(string name, string viewId, GeoRectangle2 area, double scale)
        {
            ViewKind kind = KindOf(viewId);

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "A scale has to be a finite number above nought.");
            }

            var request = new DetailViewRequest(name, kind, area, ViewScale(viewId) / scale);
            NewName(name, Names(_settings.Views.Details, d => d.Name));
            return Change(s => s.Views.Details.Add(request));
        }

        /// <summary>The drawing made again from its source with its settings changed.</summary>
        private UpdateReport Change(Action<DrawingSettings> change)
        {
            if (_source == null)
            {
                throw new InvalidOperationException("A drawing opened from a file is changed once it is updated from its source.");
            }

            DrawingSettings settings = _settings.Clone();
            change(settings);
            return Update(_source, settings);
        }

        /// <summary>A change of the settings for one view the drawing has.</summary>
        private UpdateReport ForView(string viewId, Action<DrawingSettings> change)
        {
            KindOf(viewId);
            return Change(change);
        }

        /// <summary>The kind of a view the drawing has, found by its id.</summary>
        private ViewKind KindOf(string viewId)
        {
            if (viewId == null)
            {
                throw new ArgumentNullException(nameof(viewId));
            }

            if (_content == null)
            {
                throw new InvalidOperationException("A drawing opened from a file is changed once it is updated from its source.");
            }

            foreach (DrawingView view in _content.Views)
            {
                if (string.Equals(view.Id, viewId, StringComparison.Ordinal))
                {
                    return view.Kind;
                }
            }

            throw new ArgumentException("The drawing has no view " + viewId + ".", nameof(viewId));
        }

        private double ViewScale(string viewId)
        {
            foreach (DrawingView view in _content.Views)
            {
                if (string.Equals(view.Id, viewId, StringComparison.Ordinal))
                {
                    return view.Scale;
                }
            }

            return _scale;
        }

        private static List<string> Names<T>(IEnumerable<T> items, Func<T, string> name)
        {
            var names = new List<string>();

            foreach (T item in items)
            {
                names.Add(name(item));
            }

            return names;
        }

        private static void NewName(string name, List<string> names)
        {
            if (name != null && names.Exists(n => string.Equals(ViewBuilder.Clean(n), ViewBuilder.Clean(name), StringComparison.Ordinal)))
            {
                throw new ArgumentException("The drawing has a view named " + name + " already.", nameof(name));
            }
        }

        private static void AddOnce<T>(IList<T> list, T item)
        {
            if (!list.Contains(item))
            {
                list.Add(item);
            }
        }

        /// <summary>What the drawing is made of now: its source, settings, views and document elements.</summary>
        private State Current() => new State(_source, _settings, _content, _views, _annotations, _warnings, _scale);

        /// <summary>Puts the drawing back as a state had it; the hand edits are not part of it.</summary>
        private void Restore(State state)
        {
            _source = state.Source;
            _settings = state.Settings;
            _content = state.Content;
            _views = state.Views;
            _annotations = state.Annotations;
            _elements = Index(_views, _annotations);
            _warnings = state.Warnings;
            _scale = state.Scale;
        }

        /// <summary>Keeps a state an undo or a redo may bring back, the last twenty.</summary>
        private void Remember(State state)
        {
            _states.RemoveAll(s => s.Same(state.Views, state.Annotations));
            _states.Add(state);

            if (_states.Count > 20)
            {
                _states.RemoveAt(0);
            }
        }

        /// <summary>The document as records, for the store.</summary>
        internal DrawingDocumentRecords Records() => new DrawingDocumentRecords(Id, _views, _annotations, _edits, _runs, _unknown);

        /// <summary>Finds an element of the document by its id.</summary>
        internal bool TryGetElement(string id, out DrawingElement element) => _elements.TryGetValue(id, out element);

        /// <summary>Adds a hand edit taken through the presenter; the caller writes the records into the DXF with it.</summary>
        internal void AddEdit(DrawingEdit edit) => _edits.Add(edit);

        /// <summary>Takes a hand edit back out where writing it failed.</summary>
        internal void RemoveLastEdit() => _edits.RemoveAt(_edits.Count - 1);

        /// <summary>The views elements by id, as an update and the owner find them.</summary>
        private static Dictionary<string, DrawingElement> Index(List<DrawingElement> views, List<DrawingElement> annotations)
        {
            var index = new Dictionary<string, DrawingElement>(StringComparer.Ordinal);

            foreach (DrawingElement element in views)
            {
                index[element.Id] = element;
            }

            foreach (DrawingElement element in annotations)
            {
                index[element.Id] = element;
            }

            return index;
        }

        private void Take(DrawingContent content)
        {
            _content = content;
            _scale = content.Scale;
            _warnings = content.Warnings;
            DrawingBuilder.Elements(content, _settings, out _views, out _annotations);
            _elements = Index(_views, _annotations);
        }

        private void Read(DrawingDocumentRecords records)
        {
            _views = new List<DrawingElement>(records.Views);
            _annotations = new List<DrawingElement>(records.Annotations);
            _elements = Index(_views, _annotations);
            _edits = new List<DrawingEdit>(records.Edits);
            _runs = new List<DrawingPluginRun>(records.PluginRuns);
            _unknown = new List<IReadOnlyList<DxfTypedValue>>(records.UnknownRecords);
        }

        /// <summary>After an undo or redo in the presenter, the document read back from the DXF it now stands at.</summary>
        private void OnPresenterChanged(object sender, CadDocumentChangedEventArgs e)
        {
            if (!ReferenceEquals(sender, _presenter) || (e.Kind != CadChangeKind.Undo && e.Kind != CadChangeKind.Redo && e.Kind != CadChangeKind.Rollback))
            {
                return;
            }

            DrawingDocumentRecords records = DrawingDocumentStore.Read(_presenter.Database);

            if (records == null)
            {
                return;
            }

            Read(records);

            // An update undone or redone: the views, settings and source it was made of come back with its document.
            State state = _states.Find(s => s.Same(records.Views, records.Annotations));

            if (state != null)
            {
                Restore(state);
                return;
            }

            foreach (DrawingElement element in records.Annotations)
            {
                if (element.Id == DrawingBuilder.SettingsId)
                {
                    _settings = DrawingSettings.FromValues(element.Values);
                    break;
                }
            }
        }

        /// <summary>What a drawing is made of but its hand edits: its source, settings, views and the elements of its document.</summary>
        private sealed class State
        {
            public State(DrawingSource source, DrawingSettings settings, DrawingContent content, List<DrawingElement> views, List<DrawingElement> annotations, List<string> warnings, double scale)
            {
                Source = source;
                Settings = settings;
                Content = content;
                Views = views;
                Annotations = annotations;
                Warnings = warnings;
                Scale = scale;
            }

            public DrawingSource Source { get; }

            public DrawingSettings Settings { get; }

            public DrawingContent Content { get; }

            public List<DrawingElement> Views { get; }

            public List<DrawingElement> Annotations { get; }

            public List<string> Warnings { get; }

            public double Scale { get; }

            /// <summary>Whether the state has these elements, in this order.</summary>
            public bool Same(IReadOnlyList<DrawingElement> views, IReadOnlyList<DrawingElement> annotations)
            {
                if (views.Count != Views.Count || annotations.Count != Annotations.Count)
                {
                    return false;
                }

                for (int i = 0; i < views.Count; i++)
                {
                    if (!views[i].Equals(Views[i]))
                    {
                        return false;
                    }
                }

                for (int i = 0; i < annotations.Count; i++)
                {
                    if (!annotations[i].Equals(Annotations[i]))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Why an edit no longer applies to the elements of an update; null where it does.</summary>
        private string WhyNot(DrawingEdit edit, Dictionary<string, DrawingElement> next, HashSet<string> objectIds)
        {
            if (!next.TryGetValue(edit.ObjectId, out DrawingElement element))
            {
                if (_elements.TryGetValue(edit.ObjectId, out DrawingElement old))
                {
                    var missing = new List<string>();

                    foreach (string objectId in old.Links.ObjectIds)
                    {
                        if (!objectIds.Contains(objectId) && !missing.Contains(objectId))
                        {
                            missing.Add(objectId);
                        }
                    }

                    if (missing.Count == 1)
                    {
                        return "the object " + missing[0] + " is no longer in the model";
                    }

                    if (missing.Count > 1)
                    {
                        return "the objects " + string.Join(", ", missing) + " are no longer in the model";
                    }

                    return "the drawing no longer makes the " + old.Kind.ToLowerInvariant() + " " + edit.ObjectId;
                }

                return "the drawing has nothing of the id " + edit.ObjectId;
            }

            return WhyNotKind(edit, element);
        }

        /// <summary>Why an edit is not one an element of its kind keeps; null where it is.</summary>
        internal static string WhyNotKind(DrawingEdit edit, DrawingElement element)
        {
            switch (element.Kind)
            {
                case "Mark":
                case "View":
                    if (edit.Kind != CadEditKind.Move)
                    {
                        return "a " + element.Kind.ToLowerInvariant() + " is only moved";
                    }

                    break;

                case "Dimension":
                    if (edit.Kind == CadEditKind.Property && edit.Property == "Text override")
                    {
                        return null;
                    }

                    if (edit.Kind == CadEditKind.Erase)
                    {
                        return null;
                    }

                    if (edit.Kind != CadEditKind.Move)
                    {
                        return "a dimension keeps a move, a text override or its erasing, not a " + edit.Kind.ToString().ToLowerInvariant();
                    }

                    break;

                default:
                    return "the " + element.Kind.ToLowerInvariant() + " " + element.Id + " is not edited by hand";
            }

            return DrawingDxfWriter.TryReadMove(edit.NewValue, out _) ? null : "the move " + (edit.NewValue ?? "(none)") + " does not read as dx,dy";
        }

        /// <summary>What an update changes: the objects of the model and the elements of the drawing, each list in the order of the ids.</summary>
        private UpdateReport Compare(DrawingSource newSource, Dictionary<string, DrawingElement> next, List<DrawingEdit> kept, List<DroppedEdit> dropped)
        {
            var before = new SortedSet<string>(StringComparer.Ordinal);
            var after = new SortedSet<string>(StringComparer.Ordinal);

            if (_source != null)
            {
                foreach (DrawingObject item in _source.Objects)
                {
                    before.Add(item.Id);
                }
            }
            else
            {
                foreach (DrawingElement view in _views)
                {
                    foreach (string objectId in view.Links.ObjectIds)
                    {
                        before.Add(objectId);
                    }
                }
            }

            foreach (DrawingObject item in newSource.Objects)
            {
                after.Add(item.Id);
            }

            var addedObjects = new List<string>();
            var removedObjects = new List<string>();
            var changedObjects = new List<string>();

            foreach (string id in after)
            {
                if (!before.Contains(id))
                {
                    addedObjects.Add(id);
                }
                else if (_source != null && _source.TryGetObject(id, out DrawingObject old) && newSource.TryGetObject(id, out DrawingObject now) && !ObjectComparer.Same(old, now))
                {
                    changedObjects.Add(id);
                }
            }

            foreach (string id in before)
            {
                if (!after.Contains(id))
                {
                    removedObjects.Add(id);
                }
            }

            var addedElements = new List<string>();
            var removedElements = new List<string>();
            var changedElements = new List<string>();
            var nextIds = new SortedSet<string>(next.Keys, StringComparer.Ordinal);
            var oldIds = new SortedSet<string>(_elements.Keys, StringComparer.Ordinal);

            foreach (string id in nextIds)
            {
                if (!_elements.TryGetValue(id, out DrawingElement old))
                {
                    addedElements.Add(id);
                }
                else if (!old.Equals(next[id]))
                {
                    changedElements.Add(id);
                }
            }

            foreach (string id in oldIds)
            {
                if (!next.ContainsKey(id))
                {
                    removedElements.Add(id);
                }
            }

            return new UpdateReport(
                addedObjects.AsReadOnly(),
                removedObjects.AsReadOnly(),
                changedObjects.AsReadOnly(),
                addedElements.AsReadOnly(),
                removedElements.AsReadOnly(),
                changedElements.AsReadOnly(),
                kept.AsReadOnly(),
                dropped.AsReadOnly(),
                new List<DrawingPluginRun>(_runs).AsReadOnly());
        }
    }

    /// <summary>Whether two objects of the model are the same: kind, texts, frame and body, every coordinate to the bit.</summary>
    internal static class ObjectComparer
    {
        public static bool Same(DrawingObject a, DrawingObject b)
        {
            if (a.Kind != b.Kind || a.IsMain != b.IsMain
                || !string.Equals(a.Name, b.Name, StringComparison.Ordinal) || !string.Equals(a.Mark, b.Mark, StringComparison.Ordinal)
                || !string.Equals(a.Profile, b.Profile, StringComparison.Ordinal) || !string.Equals(a.Material, b.Material, StringComparison.Ordinal)
                || !string.Equals(a.ParentId, b.ParentId, StringComparison.Ordinal) || a.Attributes.Count != b.Attributes.Count
                || !Same(a.Frame.Origin, b.Frame.Origin) || !Same(a.Frame.XAxis, b.Frame.XAxis) || !Same(a.Frame.YAxis, b.Frame.YAxis))
            {
                return false;
            }

            foreach (KeyValuePair<string, string> attribute in a.Attributes)
            {
                if (!b.Attributes.TryGetValue(attribute.Key, out string value) || !string.Equals(value, attribute.Value, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return Same(a.Solid, b.Solid);
        }

        private static bool Same(GeoSolid3 a, GeoSolid3 b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a.Faces.Count != b.Faces.Count || a.Openings.Count != b.Openings.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Faces.Count; i++)
            {
                GeoFace3 fa = a.Faces[i];
                GeoFace3 fb = b.Faces[i];

                if (!Same(fa.Boundary, fb.Boundary) || fa.Holes.Count != fb.Holes.Count)
                {
                    return false;
                }

                for (int h = 0; h < fa.Holes.Count; h++)
                {
                    if (!Same(fa.Holes[h], fb.Holes[h]))
                    {
                        return false;
                    }
                }
            }

            for (int i = 0; i < a.Openings.Count; i++)
            {
                if (!Same(a.Openings[i], b.Openings[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(GeoPolygon3 a, GeoPolygon3 b)
        {
            if (a.VertexCount != b.VertexCount)
            {
                return false;
            }

            for (int i = 0; i < a.VertexCount; i++)
            {
                if (!Same(a.Vertices[i], b.Vertices[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(GeoPoint3 a, GeoPoint3 b) => Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);

        private static bool Same(GeoVector3 a, GeoVector3 b) => Bits(a.X, b.X) && Bits(a.Y, b.Y) && Bits(a.Z, b.Z);

        private static bool Bits(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
    }
}
