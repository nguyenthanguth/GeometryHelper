using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The user coordinate systems of a drawing open in the presenter, as AutoCAD keeps them: the current system of model
    /// space and that of paper space, up to ten earlier ones of each for UCS Previous, the named systems of the drawing's UCS
    /// table, and how the UCS icon is shown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// They are read from the drawing when first asked for: the current systems from the header (<c>$UCSORG</c>,
    /// <c>$UCSXDIR</c>, <c>$UCSYDIR</c>, <c>$UCSNAME</c> and the <c>$PUCS</c> ones of paper space), the named ones from the UCS
    /// table. As in AutoCAD, a change of system is not a step of the drawing's undo: U and REDO leave it as it is, and UCS
    /// Previous goes back through the earlier ones. The drawing is given them when it is saved, the table and the header
    /// written as AutoCAD writes them; one with nothing changed is saved with its own bytes.
    /// </para>
    /// <para>
    /// All the layouts share the one system of paper space the header keeps; AutoCAD keeps one in each layout as well.
    /// </para>
    /// </remarks>
    public sealed class CadUcsManager
    {
        private const int PreviousLimit = 10;

        private readonly CadDocument _document;
        private readonly SortedDictionary<string, CadUcs> _named = new SortedDictionary<string, CadUcs>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly SortedSet<string> _unread = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<CadUcs> _modelPrevious = new List<CadUcs>();
        private readonly List<CadUcs> _paperPrevious = new List<CadUcs>();

        // Whether each was changed since the drawing was opened, which a save writes; and whether any was since the last save.
        private bool _modelChanged;
        private bool _paperChanged;
        private bool _tableChanged;
        private bool _modified;

        internal CadUcsManager(CadDocument document)
        {
            _document = document;
            DxfDatabase database = document.Database;

            foreach (DxfUcsTableRecord record in database.UcsTable)
            {
                CadUcs read = Read(record.Origin, record.XAxis, record.YAxis, record.Name);

                if (read == null || _named.ContainsKey(record.Name))
                {
                    _unread.Add(record.Name);
                }
                else
                {
                    _named.Add(record.Name, read);
                }
            }

            Model = Read(database.Ucsorg, database.Ucsxdir, database.Ucsydir, Listed(database.Ucsname)) ?? CadUcs.World;
            Paper = Read(database.Pucsorg, database.Pucsxdir, database.Pucsydir, Listed(database.Pucsname))
                ?? CadUcs.World;
        }

        /// <summary>Raised when a current system, a named one or the icon changes.</summary>
        public event EventHandler Changed;

        /// <summary>Gets the current system of model space.</summary>
        public CadUcs Model { get; private set; }

        /// <summary>Gets the current system of paper space, which every layout shares.</summary>
        public CadUcs Paper { get; private set; }

        /// <summary>Gets the current system of the space the canvas shows.</summary>
        public CadUcs Current => CurrentOf(_document.WorkingSpace);

        /// <summary>
        /// Gets the system points are given in for the space the canvas shows: the current one, or the world where the current
        /// one is not parallel to the view (<see cref="CadUcs.IsPlanar"/>), as a system turned out of the plan read from a file.
        /// </summary>
        public CadUcs Working => WorkingOf(_document.WorkingSpace);

        /// <summary>Gets the named systems, in ordinal order of their names, letters of either case the same.</summary>
        public IReadOnlyList<CadUcs> Named => new List<CadUcs>(_named.Values);

        /// <summary>Gets or sets whether the UCS icon is shown (UCSICON ON and OFF); on until set, as AutoCAD's.</summary>
        public bool IconOn
        {
            get => _iconOn;
            set => Change(ref _iconOn, value);
        }

        private bool _iconOn = true;

        /// <summary>Gets or sets whether the UCS icon is drawn at the origin of the system where the origin is in view (UCSICON ORigin), else at the lower left corner (Noorigin); at the origin until set.</summary>
        public bool IconAtOrigin
        {
            get => _iconAtOrigin;
            set => Change(ref _iconAtOrigin, value);
        }

        private bool _iconAtOrigin = true;

        /// <summary>Gets whether a system has changed since the drawing was opened or last saved.</summary>
        public bool IsModified => _modified;

        /// <summary>Gets the current system of a space: <see cref="Model"/> for model space, <see cref="Paper"/> for a layout.</summary>
        /// <param name="space">The space.</param>
        /// <returns>The system.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space is null.</exception>
        public CadUcs CurrentOf(CadSpace space)
        {
            if (space == null)
            {
                throw new ArgumentNullException(nameof(space));
            }

            return space.IsModel ? Model : Paper;
        }

        /// <summary>Gets the system points are given in for a space, as <see cref="Working"/> says.</summary>
        /// <param name="space">The space.</param>
        /// <returns>The system.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the space is null.</exception>
        public CadUcs WorkingOf(CadSpace space)
        {
            CadUcs current = CurrentOf(space);
            return current.IsPlanar ? current : CadUcs.World;
        }

        /// <summary>Gets the named system of a name, letters of either case the same; null for none.</summary>
        /// <param name="name">The name.</param>
        /// <returns>The system.</returns>
        public CadUcs Find(string name) =>
            name != null && _named.TryGetValue(name.Trim(), out CadUcs found) ? found : null;

        /// <summary>
        /// Makes a system current in the space the canvas shows, the one it replaces kept for <see cref="Previous"/>, ten at
        /// most. A system of the same origin and axes as the current one, to the tolerance of <see cref="Tolerance.Default"/>,
        /// changes nothing.
        /// </summary>
        /// <param name="ucs">The system.</param>
        /// <exception cref="ArgumentNullException">Thrown when the system is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the system is not parallel to the view.</exception>
        public void SetCurrent(CadUcs ucs)
        {
            if (ucs == null)
            {
                throw new ArgumentNullException(nameof(ucs));
            }

            if (!ucs.IsPlanar)
            {
                throw new ArgumentException(
                    "The UCS " + ucs + " is not parallel to the view, which shows the drawing from above.",
                    nameof(ucs)
                );
            }

            bool model = _document.WorkingSpace.IsModel;
            CadUcs now = model ? Model : Paper;

            if (now.IsSameAs(ucs, Tolerance.Default) && string.Equals(now.Name, ucs.Name, StringComparison.Ordinal))
            {
                return;
            }

            List<CadUcs> previous = model ? _modelPrevious : _paperPrevious;
            previous.Add(now);

            if (previous.Count > PreviousLimit)
            {
                previous.RemoveAt(0);
            }

            Put(model, ucs);
        }

        /// <summary>Makes the system current before the last change current again in the space the canvas shows, as UCS Previous.</summary>
        /// <returns>False where there is none.</returns>
        public bool Previous()
        {
            bool model = _document.WorkingSpace.IsModel;
            List<CadUcs> previous = model ? _modelPrevious : _paperPrevious;

            if (previous.Count == 0)
            {
                return false;
            }

            // A system whose name was deleted or given to another since comes back with none.
            CadUcs back = previous[previous.Count - 1];
            previous.RemoveAt(previous.Count - 1);
            CadUcs named = back.Name == null ? null : Find(back.Name);
            Put(
                model,
                named != null && named.IsSameAs(back, Tolerance.Default) ? named
                    : back.Name == null ? back
                    : back.WithName(null)
            );
            return true;
        }

        /// <summary>Saves the current system of the space the canvas shows under a name, replacing a named system of that name, and names the current system so, as UCS Save.</summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the name is one AutoCAD does not take, or that of a system of the file the presenter could not read.</exception>
        public void Save(string name)
        {
            string checkedName = CheckName(name);
            CadUcs saved = Current.WithName(checkedName);
            _named.Remove(checkedName);
            _named.Add(checkedName, saved);
            _tableChanged = true;
            _modified = true;
            Put(_document.WorkingSpace.IsModel, saved);
        }

        /// <summary>Makes a named system current in the space the canvas shows, as UCS Restore.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>False, nothing changed, where there is no system of the name or it is not parallel to the view.</returns>
        public bool Restore(string name)
        {
            CadUcs found = Find(name);

            if (found == null || !found.IsPlanar)
            {
                return false;
            }

            SetCurrent(found);
            return true;
        }

        /// <summary>Deletes a named system, as UCS Delete; a current system of that name is left with none, as AutoCAD's *NO NAME*.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>False where there is no system of the name.</returns>
        public bool Delete(string name)
        {
            CadUcs found = Find(name);

            if (found == null)
            {
                return false;
            }

            _named.Remove(found.Name);
            _tableChanged = true;
            _modified = true;
            NameCurrent(found.Name, null);
            Raise();
            return true;
        }

        /// <summary>Renames a named system, as the UCS dialog does; a current system of that name takes the new one.</summary>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>False where there is no system of the name.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the new name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the new name is one AutoCAD does not take, or another system's.</exception>
        public bool Rename(string name, string newName)
        {
            CadUcs found = Find(name);

            if (found == null)
            {
                return false;
            }

            string checkedName = CheckName(newName);

            if (
                !string.Equals(checkedName, found.Name, StringComparison.OrdinalIgnoreCase)
                && _named.ContainsKey(checkedName)
            )
            {
                throw new ArgumentException(
                    "There is a UCS named " + checkedName + " already, letters of either case the same.",
                    nameof(newName)
                );
            }

            _named.Remove(found.Name);
            _named.Add(checkedName, found.WithName(checkedName));
            _tableChanged = true;
            _modified = true;
            NameCurrent(found.Name, checkedName);
            Raise();
            return true;
        }

        /// <summary>Writes the systems into the drawing as AutoCAD writes them, where one has changed: the UCS table, then the header.</summary>
        internal void WriteTo(DxfDatabase database)
        {
            if (!(_modelChanged || _paperChanged || _tableChanged))
            {
                return;
            }

            DxfUcsTable table = database.UcsTable;

            foreach (DxfUcsTableRecord record in new List<DxfUcsTableRecord>(table))
            {
                if (!_named.ContainsKey(record.Name) && !_unread.Contains(record.Name))
                {
                    try
                    {
                        record.Erase();
                    }
                    catch (InvalidOperationException exception)
                    {
                        GeometryHelperLog.Warn(
                            "CadUcsManager: the UCS " + record.Name + " was kept in the drawing.",
                            exception
                        );
                    }
                }
            }

            foreach (CadUcs ucs in _named.Values)
            {
                if (table.Has(ucs.Name))
                {
                    DxfUcsTableRecord record = table[ucs.Name];
                    record.Name = ucs.Name;
                    record.Origin = ucs.Origin;
                    record.XAxis = ucs.XAxis;
                    record.YAxis = ucs.YAxis;
                }
                else
                {
                    table.Add(
                        new DxfUcsTableRecord
                        {
                            Name = ucs.Name,
                            Origin = ucs.Origin,
                            XAxis = ucs.XAxis,
                            YAxis = ucs.YAxis,
                        }
                    );
                }
            }

            if (_modelChanged)
            {
                database.Ucs = new GeoCoordinateSystem3(Model.Origin, Model.XAxis, Model.YAxis);
                database.Ucsname = Model.Name != null && table.Has(Model.Name) ? Model.Name : null;
            }

            if (_paperChanged)
            {
                database.Pucs = new GeoCoordinateSystem3(Paper.Origin, Paper.XAxis, Paper.YAxis);
                database.Pucsname = Paper.Name != null && table.Has(Paper.Name) ? Paper.Name : null;
            }
        }

        /// <summary>
        /// The drawing was saved with the systems written: nothing is changed since. What was changed before is written again at
        /// each save, as an undo reads back a drawing kept before it was.
        /// </summary>
        internal void Saved() => _modified = false;

        /// <summary>A system read from the drawing; null, with a warning, where its axes do not make one.</summary>
        private static CadUcs Read(GeoPoint3 origin, GeoVector3 xAxis, GeoVector3 yAxis, string name)
        {
            try
            {
                return new CadUcs(origin, xAxis, yAxis, name);
            }
            catch (ArgumentException exception)
            {
                GeometryHelperLog.Warn(
                    "CadUcsManager: the UCS "
                        + (name ?? "of the header")
                        + " has no axes that make one, and is left as the drawing has it.",
                    exception
                );
                return null;
            }
        }

        /// <summary>The name of a current system where the table lists it, else none.</summary>
        private string Listed(string name) => name != null && _named.ContainsKey(name) ? _named[name].Name : null;

        /// <summary>A name AutoCAD takes for a named system, not one of a system the presenter could not read.</summary>
        private string CheckName(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string trimmed = name.Trim();

            if (
                trimmed.Length == 0
                || trimmed.Length > 255
                || trimmed.IndexOfAny("<>/\\\":;?*|=`".ToCharArray()) >= 0
                || string.Equals(trimmed, "*WORLD*", StringComparison.OrdinalIgnoreCase)
            )
            {
                throw new ArgumentException(
                    "A UCS name is up to 255 letters with none of < > / \\ \" : ; ? * | = `: '"
                        + name
                        + "' is not one.",
                    nameof(name)
                );
            }

            if (_unread.Contains(trimmed))
            {
                throw new ArgumentException(
                    "The drawing has a UCS named "
                        + trimmed
                        + " the presenter could not read, which it keeps as it is.",
                    nameof(name)
                );
            }

            return trimmed;
        }

        /// <summary>The current systems named by an old name take the new one, or none where it is null.</summary>
        private void NameCurrent(string old, string name)
        {
            if (string.Equals(Model.Name, old, StringComparison.OrdinalIgnoreCase))
            {
                Model = Model.WithName(name);
                _modelChanged = true;
                _modified = true;
            }

            if (string.Equals(Paper.Name, old, StringComparison.OrdinalIgnoreCase))
            {
                Paper = Paper.WithName(name);
                _paperChanged = true;
                _modified = true;
            }
        }

        private void Put(bool model, CadUcs ucs)
        {
            if (model)
            {
                Model = ucs;
                _modelChanged = true;
                _modified = true;
            }
            else
            {
                Paper = ucs;
                _paperChanged = true;
                _modified = true;
            }

            Raise();
        }

        private void Change(ref bool field, bool value)
        {
            if (field != value)
            {
                field = value;
                Raise();
            }
        }

        private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
