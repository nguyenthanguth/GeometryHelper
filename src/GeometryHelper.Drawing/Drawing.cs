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
    /// <see cref="Open"/>. <see cref="Update"/> makes it again from the model as it is now: it takes out only the entities it
    /// wrote, writes them anew, applies the hand edits that still apply and reports those that no longer do; what was drawn
    /// by hand on it stays. A beam drawn 3 000 long and updated to 3 500 has its overall dimension read 3500, a mark moved 40
    /// to the right still 40 to the right of where the drawing puts it, and a line drawn by hand on the sheet still there.
    /// </para>
    /// <para>
    /// <see cref="OpenInPresenter"/> shows the drawing in the 2D presenter with its owner registered: a projected line there
    /// cannot be moved, a mark or a dimension can, and the edit is recorded here as a hand edit, one undo step with it.
    /// </para>
    /// </remarks>
    public sealed class Drawing
    {
        private readonly DrawingSettings _settings;
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
        /// <returns>The drawing: its elements, edits and plugin runs as saved, its views empty until <see cref="Update"/>.</returns>
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
        /// Makes the drawing again from the model as it is now: views, dimensions, marks and sheet, with the settings it has.
        /// Only the entities it wrote are taken out and written anew; the hand edits that still apply are applied again, those
        /// that no longer do are taken out and reported; the plugin runs are kept and reported as to be run again.
        /// </summary>
        /// <param name="newSource">The objects as they are now.</param>
        /// <returns>What changed and what became of the hand edits.</returns>
        /// <remarks>
        /// Shown in the presenter, the update is one undo step there. Undoing it brings back the DXF and the hand edits as they
        /// were; <see cref="Views"/> keeps the views of the update until the drawing is updated again.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the source is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the presenter refuses or fails the update, as while a
        /// command of it runs; the drawing is then left as it was.</exception>
        public UpdateReport Update(DrawingSource newSource)
        {
            if (newSource == null)
            {
                throw new ArgumentNullException(nameof(newSource));
            }

            DrawingContent content = DrawingBuilder.Make(newSource, _settings, Tolerance);
            DrawingBuilder.Elements(content, _settings, out List<DrawingElement> views, out List<DrawingElement> annotations);
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
            DrawingSource oldSource = _source;
            DrawingContent oldContent = _content;
            List<DrawingElement> oldViews = _views, oldAnnotations = _annotations;
            Dictionary<string, DrawingElement> oldElements = _elements;
            List<DrawingEdit> oldEdits = _edits;
            List<string> oldWarnings = _warnings;
            double oldScale = _scale;
            _source = newSource;
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
                    _source = oldSource;
                    _content = oldContent;
                    _views = oldViews;
                    _annotations = oldAnnotations;
                    _elements = oldElements;
                    _edits = oldEdits;
                    _warnings = oldWarnings;
                    _scale = oldScale;
                    throw new InvalidOperationException("The presenter did not take the update: " + result.Message);
                }
            }

            GeometryHelperLog.Info("Drawing.Update: " + Id + ": " + report + ".");
            return report;
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

            if (records != null)
            {
                Read(records);
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
