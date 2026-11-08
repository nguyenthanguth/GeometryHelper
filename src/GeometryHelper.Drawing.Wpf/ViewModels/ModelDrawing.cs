using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>
    /// A model drawing open in the presenter: the drawing, the document showing it, the source it was made from, the preset
    /// it was made with, and what the file list and Properties say of it.
    /// </summary>
    /// <remarks>
    /// Its number, name and revision are those of its title block: a drawing of the beam B1 made with the number B-101 and the
    /// name "Beam B1" is listed as B-101, "Beam B1 · rev A", on its A2. Its dot is amber while the model has changed since it was
    /// made, blue while it has hand edits or changes not saved, and green otherwise.
    /// </remarks>
    public sealed class ModelDrawing : ObservableObject
    {
        private Drawing _drawing;
        private CadDocument _document;
        private bool _isOutOfDate;

        internal ModelDrawing(string key, Drawing drawing, CadDocument document, IDrawingSourceProvider provider, string presetName, IEnumerable<string> wholeAssemblies)
        {
            Key = key;
            _drawing = drawing;
            _document = document;
            Provider = provider;
            PresetName = presetName ?? "Custom";
            WholeAssemblies = new SortedSet<string>(wholeAssemblies ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            LastMade = DateTime.Now;
        }

        /// <summary>Gets the key the file list knows the drawing by.</summary>
        public string Key { get; }

        /// <summary>Gets the drawing.</summary>
        public Drawing Drawing
        {
            get => _drawing;
            internal set => Set(ref _drawing, value);
        }

        /// <summary>Gets the presenter's document showing the drawing.</summary>
        public CadDocument Document
        {
            get => _document;
            internal set => Set(ref _document, value);
        }

        /// <summary>Gets the source the drawing was last made from.</summary>
        public IDrawingSourceProvider Provider { get; internal set; }

        /// <summary>Gets the name of the preset it was made with, "Changed" once its settings were edited.</summary>
        public string PresetName { get; internal set; }

        /// <summary>Gets when it was last made or updated, as this machine's clock gave it.</summary>
        public DateTime LastMade { get; internal set; }

        /// <summary>Gets the assemblies drawn whole: a fitting added to one of them in the model is drawn on update too.</summary>
        public IReadOnlyCollection<string> WholeAssemblies { get; }

        /// <summary>Gets whether the model has changed since the drawing was made.</summary>
        public bool IsOutOfDate
        {
            get => _isOutOfDate;
            internal set => Set(ref _isOutOfDate, value);
        }

        /// <summary>Gets the drawing number: the title block's, or the drawing's id where it has none.</summary>
        public string Number => NonEmpty(TitleBlock.DrawingNumber, Drawing.Id);

        /// <summary>Gets the name: the title block's title, or the main object's name and mark.</summary>
        public string Name => NonEmpty(TitleBlock.Title, Drawing.Id);

        /// <summary>Gets the revision; empty for none.</summary>
        public string Revision => TitleBlock.Revision;

        /// <summary>Gets the sheet's size, as A2.</summary>
        public string SheetSize => Drawing.Sheet?.SizeName ?? Drawing.Settings.Sheet.Size.ToString();

        /// <summary>Gets whether the drawing has hand edits or changes not saved.</summary>
        public bool IsEdited => Document.IsModified || Drawing.Edits.Count > 0;

        /// <summary>Gets its dot in the file list: out of date first, then edited, else up to date.</summary>
        public CadFileState State => IsOutOfDate ? CadFileState.OutOfDate : IsEdited ? CadFileState.Edited : CadFileState.UpToDate;

        /// <summary>Gets its state in words, as Properties shows it: "Up to date", "Edited · 2 hand edits", "Out of date".</summary>
        public string StatusText
        {
            get
            {
                if (IsOutOfDate)
                {
                    return "Out of date";
                }

                int edits = Drawing.Edits.Count;
                return edits > 0 ? "Edited · " + edits.ToString(CultureInfo.InvariantCulture) + (edits == 1 ? " hand edit" : " hand edits")
                    : Document.IsModified ? "Edited · not saved" : "Up to date";
            }
        }

        /// <summary>Gets the title block as the sheet has it filled, or as the settings give it for a drawing opened and not made again.</summary>
        internal TitleBlock TitleBlock => Drawing.Sheet?.TitleBlock ?? Drawing.Settings.Sheet.TitleBlock;

        /// <summary>Raises the change of everything the list and Properties read.</summary>
        internal void Refresh()
        {
            Raise(nameof(Number));
            Raise(nameof(Name));
            Raise(nameof(Revision));
            Raise(nameof(SheetSize));
            Raise(nameof(State));
            Raise(nameof(StatusText));
        }

        private static string NonEmpty(string value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;

        /// <inheritdoc/>
        public override string ToString() => Number + " " + Name;
    }
}
