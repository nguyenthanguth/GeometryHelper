using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>A reference of the Reference Edit dialog's list: its name, indented by how deep it is nested, and where it comes from.</summary>
    public sealed class ReferenceEditRowViewModel
    {
        internal ReferenceEditRowViewModel(CadRefEditLevel level)
        {
            Level = level;
            Depth = level.Chain.Count - 1;
        }

        /// <summary>Gets the reference.</summary>
        public CadRefEditLevel Level { get; }

        /// <summary>Gets how deep the reference is nested: 0 for the one in the drawing.</summary>
        public int Depth { get; }

        /// <summary>Gets the name of the reference's block.</summary>
        public string Name => Level.Name;

        /// <summary>Gets the left margin of the name, 16 for each level nested.</summary>
        public System.Windows.Thickness Indent => new System.Windows.Thickness(Depth * 16, 0, 0, 0);

        /// <summary>Gets what kind of reference it is: "Xref" or "Block".</summary>
        public string Kind => Level.IsExternalReference ? "Xref" : "Block";
    }

    /// <summary>
    /// AutoCAD 2021's Reference Edit dialog over REFEDIT's request: the references the point reaches, the one to edit with
    /// the path of its file where it is an external reference, whether the objects are all taken or picked by point, and the
    /// settings: display of attribute definitions (only for a block that has some), unique layer and symbol names (only for
    /// an external reference) and the lock on the objects outside the working set. OK writes the request
    /// (<see cref="Accept"/>), which the command then takes.
    /// </summary>
    public sealed class ReferenceEditViewModel : ObservableObject
    {
        private readonly CadRefEditRequest _request;
        private readonly List<ReferenceEditRowViewModel> _rows;
        private ReferenceEditRowViewModel _picked;
        private bool _prompt;
        private bool _attributes;
        private bool _unique;
        private bool _lock;

        /// <summary>Makes the dialog over REFEDIT's request.</summary>
        /// <param name="request">The request.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public ReferenceEditViewModel(CadRefEditRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _rows = request.Levels.Select(l => new ReferenceEditRowViewModel(l)).ToList();
            References = _rows;
            _picked = References[Math.Max(0, Math.Min(References.Count - 1, request.SelectedIndex))];
            _prompt = request.PromptForObjects;
            _attributes = request.DisplayAttributeDefinitions;
            _unique = request.UniqueNames;
            _lock = request.LockObjects;
        }

        /// <summary>Gets the references the dialog lists, the outermost first.</summary>
        public IReadOnlyList<ReferenceEditRowViewModel> References { get; }

        /// <summary>Gets or sets the reference chosen.</summary>
        public ReferenceEditRowViewModel Picked
        {
            get => _picked;
            set
            {
                if (value != null && Set(ref _picked, value))
                {
                    Raise(nameof(PathText));
                    Raise(nameof(HasAttributeDefinitions));
                    Raise(nameof(IsExternal));
                }
            }
        }

        /// <summary>
        /// Gets the path of the reference chosen, as the dialog's Path line shows it: the full path of the file of an external
        /// reference, "not found" where it is gone, and "Block definition" for a block.
        /// </summary>
        public string PathText =>
            !_picked.Level.IsExternalReference ? "Block definition" : _picked.Level.FileName ?? "Xref file not found";

        /// <summary>Gets whether the reference chosen is an external reference, which unique names are for.</summary>
        public bool IsExternal => _picked.Level.IsExternalReference;

        /// <summary>Gets whether the reference chosen has attribute definitions, which displaying them is for.</summary>
        public bool HasAttributeDefinitions => _request.HasAttributeDefinitionsAt(_rows.IndexOf(_picked));

        /// <summary>Gets or sets whether every object of the reference is taken (Automatically select all nested objects); the other choice of <see cref="IsPrompt"/>.</summary>
        public bool IsAutomatic
        {
            get => !_prompt;
            set => IsPrompt = !value;
        }

        /// <summary>Gets or sets whether the objects are asked for by point (Prompt to select nested objects).</summary>
        public bool IsPrompt
        {
            get => _prompt;
            set
            {
                if (Set(ref _prompt, value))
                {
                    Raise(nameof(IsAutomatic));
                }
            }
        }

        /// <summary>Gets or sets whether the attribute definitions of the block are in the working set.</summary>
        public bool DisplayAttributeDefinitions
        {
            get => _attributes;
            set => Set(ref _attributes, value);
        }

        /// <summary>Gets or sets whether the layers of an external reference's objects take names of their own while edited.</summary>
        public bool UniqueNames
        {
            get => _unique;
            set => Set(ref _unique, value);
        }

        /// <summary>Gets or sets whether the objects outside the working set are locked.</summary>
        public bool LockObjects
        {
            get => _lock;
            set => Set(ref _lock, value);
        }

        /// <summary>Writes the choices into the request and marks it accepted, as OK does.</summary>
        public void Accept()
        {
            _request.SelectedIndex = _rows.IndexOf(_picked);
            _request.PromptForObjects = _prompt;
            _request.DisplayAttributeDefinitions = _attributes;
            _request.UniqueNames = _unique;
            _request.LockObjects = _lock;
            _request.IsAccepted = true;
        }
    }
}
