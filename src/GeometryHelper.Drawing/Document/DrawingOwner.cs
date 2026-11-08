using System;
using System.Collections.Generic;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// The owner of a model drawing's entities in the 2D presenter (D24): it says what may be done to each, and takes the
    /// edits of them as hand edits of the drawing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule follows what the entity draws, found by the id in its tag (<see cref="DrawingEntityTags"/>): a projected
    /// line, a section's hatch, a symbol (a centre line, a weld symbol, a section, detail or level mark), the frame, the
    /// title block and the part list are <see cref="CadEditRule.ReadOnly"/>, since they are the model and the sheet; a mark
    /// and a view (its viewport or its title) are <see cref="CadEditRule.MoveOnly"/>; a dimension is
    /// <see cref="CadEditRule.Edit"/>, and keeps a move, a text override and its erasing.
    /// </para>
    /// <para>
    /// An edit taken is one undo step of the presenter (<see cref="CadDocument.ApplyEdit"/>): its entities changed and the
    /// hand edit recorded in the drawing document in the DXF at once, so that undoing the step takes the record back with
    /// it. A mark P1 dragged 40 up is the hand edit <c>V-FRONT/M-P-1 Move 0,40</c>; its text and the start of its leader
    /// stand 40 above where the drawing puts them, after an update too, and its arrow still ends on its part.
    /// </para>
    /// </remarks>
    public sealed class DrawingOwner : ICadEntityOwner
    {
        private readonly Drawing _drawing;

        /// <summary>Makes the owner of a drawing's entities.</summary>
        /// <param name="drawing">The drawing.</param>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public DrawingOwner(Drawing drawing)
        {
            _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        }

        /// <summary>Gets the owner's name, the one the drawing's tags carry: GeometryHelper.Drawing.</summary>
        public string Name => DrawingEntityTags.OwnerName;

        /// <summary>
        /// Says what the presenter may do to an entity of the drawing: a dimension edited, a mark or a view moved, everything
        /// else left as it is.
        /// </summary>
        /// <param name="entity">An entity tagged as the drawing's.</param>
        /// <returns>The rule.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public CadEditRule GetEditRule(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (!DrawingEntityTags.TryRead(entity, out string id, out _) || !_drawing.TryGetElement(id, out DrawingElement element))
            {
                return CadEditRule.ReadOnly;
            }

            switch (element.Kind)
            {
                case "Dimension":
                    return CadEditRule.Edit;
                case "Mark":
                case "View":
                    return CadEditRule.MoveOnly;
                default:
                    return CadEditRule.ReadOnly;
            }
        }

        /// <summary>
        /// Takes an edit of one of the drawing's entities: a mark or a view moved, a dimension moved, its text overridden or
        /// erased. The entities change and the hand edit is recorded in the drawing, one undo step of the document; any other
        /// edit is refused and nothing changes.
        /// </summary>
        /// <param name="document">The document the entity is in, the one <see cref="Drawing.OpenInPresenter"/> gave.</param>
        /// <param name="edit">The edit.</param>
        /// <returns>Whether the edit was taken.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the edit is null.</exception>
        public bool TryApply(CadDocument document, CadEdit edit)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            DxfEntity entity = document.FindEntity(edit.Handle);

            if (entity == null || !DrawingEntityTags.TryRead(entity, out string id, out _) || !_drawing.TryGetElement(id, out DrawingElement element))
            {
                return false;
            }

            var record = new DrawingEdit(id, edit.Kind, edit.Property, edit.OldValue, edit.NewValue);

            if (Drawing.WhyNotKind(record, element) != null)
            {
                return false;
            }

            CadEditRecord planned;

            switch (edit.Kind)
            {
                case CadEditKind.Move:
                    planned = new CadEditRecord(CadEditRecordKind.Transformed, edit.Handle, "Move", null, edit.NewValue);
                    break;
                case CadEditKind.Erase:
                    planned = new CadEditRecord(CadEditRecordKind.Erased, edit.Handle);
                    break;
                default:
                    planned = new CadEditRecord(CadEditRecordKind.PropertyChanged, edit.Handle, edit.Property, edit.OldValue, edit.NewValue);
                    break;
            }

            _drawing.AddEdit(record);
            CadEditResult result = document.ApplyEdit(element.Kind.ToUpperInvariant() + " " + edit.Kind.ToString().ToUpperInvariant(), new[] { planned }, scope =>
            {
                Apply(scope, element, record);
                DrawingDocumentStore.Write(scope.Database, _drawing.Records());
            });

            if (!result.Succeeded)
            {
                _drawing.RemoveEdit(record);
                return false;
            }

            GeometryHelperLog.Info("DrawingOwner: " + record + " recorded.");
            return true;
        }

        /// <summary>Changes the entities of an element as an edit says.</summary>
        private void Apply(CadEditScope scope, DrawingElement element, DrawingEdit record)
        {
            DxfDatabase database = scope.Database;
            DrawingSettings settings = _drawing.SettingsInUse;
            bool layout = settings.ExportMode == DrawingExportMode.LayoutAndViewports;

            switch (element.Kind)
            {
                case "Dimension":
                    foreach (DxfEntity entity in Tagged(database.ModelSpace, element.Id, null))
                    {
                        if (entity is DxfDimension dimension)
                        {
                            DrawingDxfWriter.ApplyDimensionEdit(database, dimension, record, ValueOf(element, "Text") ?? string.Empty);
                        }
                    }

                    return;

                case "View":
                    DrawingDxfWriter.TryReadMove(record.NewValue, out GeoVector2 move);
                    DxfBlockTableRecord sheetSpace = layout ? DrawingDxfWriter.SheetLayout(database).BlockTableRecord : database.ModelSpace;
                    var shift = GeoTransform3.Translation(new GeoVector3(move.X, move.Y, 0.0));

                    foreach (DxfEntity entity in Tagged(sheetSpace, null, element.Id))
                    {
                        entity.TransformBy(shift);
                    }

                    return;

                case "Mark":
                    DrawingMark mark = DrawingMark.FromElement(element);

                    if (mark == null || !_drawing.TryGetElement(element.ViewId ?? string.Empty, out DrawingElement view) || !ViewPlace(view, out GeoPoint2 sheetOrigin, out GeoPoint2 modelOrigin, out double scale))
                    {
                        throw new InvalidOperationException("The mark " + element.Id + " cannot be drawn again: its values or its view do not read.");
                    }

                    foreach (DxfEntity entity in Tagged(database.ModelSpace, element.Id, null))
                    {
                        entity.Erase();
                    }

                    SortedDictionary<string, List<DrawingEdit>> byId = DrawingDxfWriter.ById(_drawing.Edits);
                    DrawingDxfWriter.Placement place = layout
                        ? new DrawingDxfWriter.Placement(modelOrigin, 1.0)
                        : new DrawingDxfWriter.Placement(DrawingDxfWriter.Moved(sheetOrigin, byId, view.Id), scale);
                    DrawingDxfWriter.WriteMark(database, view.Id, element.Id, mark, place, byId);

                    foreach (DxfEntity entity in Tagged(database.ModelSpace, element.Id, null))
                    {
                        scope.Added(scope.Document.ModelSpace, entity);
                    }

                    return;
            }
        }

        /// <summary>The live entities of a space tagged with an element's id, or with a view's id as their view; in their order.</summary>
        private static List<DxfEntity> Tagged(DxfBlockTableRecord space, string id, string viewId)
        {
            var found = new List<DxfEntity>();

            foreach (DxfEntity entity in space)
            {
                if (entity.IsErased || !DrawingEntityTags.TryRead(entity, out string objectId, out string inView))
                {
                    continue;
                }

                if ((id != null && string.Equals(objectId, id, StringComparison.Ordinal)) || (viewId != null && string.Equals(inView, viewId, StringComparison.Ordinal)))
                {
                    found.Add(entity);
                }
            }

            return found;
        }

        /// <summary>Where a view stands, read from its element: its origin on the sheet and in model space, and its scale.</summary>
        private static bool ViewPlace(DrawingElement view, out GeoPoint2 sheetOrigin, out GeoPoint2 modelOrigin, out double scale)
        {
            sheetOrigin = new GeoPoint2(0.0, 0.0);
            modelOrigin = new GeoPoint2(0.0, 0.0);
            scale = 0.0;
            bool sheet = false, model = false;

            foreach (KeyValuePair<string, string> value in view.Values)
            {
                switch (value.Key)
                {
                    case "SheetOrigin":
                        sheet = TryPoint(value.Value, out sheetOrigin);
                        break;
                    case "ModelOrigin":
                        model = TryPoint(value.Value, out modelOrigin);
                        break;
                    case "Scale":
                        double.TryParse(value.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out scale);
                        break;
                }
            }

            return sheet && model && scale > 0.0;
        }

        private static string ValueOf(DrawingElement element, string name)
        {
            foreach (KeyValuePair<string, string> value in element.Values)
            {
                if (value.Key == name)
                {
                    return value.Value;
                }
            }

            return null;
        }

        private static bool TryPoint(string written, out GeoPoint2 point)
        {
            bool read = DrawingDxfWriter.TryReadMove(written, out GeoVector2 v);
            point = new GeoPoint2(v.X, v.Y);
            return read;
        }
    }
}
