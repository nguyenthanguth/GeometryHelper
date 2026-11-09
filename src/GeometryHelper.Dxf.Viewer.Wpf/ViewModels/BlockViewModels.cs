using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Media;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A block of a drawing as a tile of the Blocks palette: its name and its picture, drawn by the canvas's own renderer
    /// when first shown.
    /// </summary>
    public sealed class BlockTileViewModel : ObservableObject
    {
        private readonly Func<string, ImageSource> _draw;
        private ImageSource _picture;
        private bool _drawn;
        private bool _isSelected;

        internal BlockTileViewModel(string name, string description, Func<string, ImageSource> draw)
        {
            Name = name;
            Description = description ?? string.Empty;
            _draw = draw;
        }

        /// <summary>Gets the name of the block.</summary>
        public string Name { get; }

        /// <summary>Gets what the tile is captioned: the name, or "(whole drawing)" for the tile that inserts a whole file.</summary>
        public string DisplayName => Name.Length == 0 ? "(whole drawing)" : Name;

        /// <summary>Gets the description of the block, its tooltip.</summary>
        public string Description { get; }

        /// <summary>Gets the picture of the block, white on black, drawn when first asked; null where it could not be drawn.</summary>
        public ImageSource Picture
        {
            get
            {
                if (!_drawn)
                {
                    _drawn = true;
                    _picture = _draw?.Invoke(Name);
                }

                return _picture;
            }
        }

        /// <summary>Gets or sets whether the tile is the one chosen, framed in the accent.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }
    }

    /// <summary>The pictures of the blocks of a drawing, each drawn from a copy of the drawing so that the drawing itself is not touched.</summary>
    internal sealed class BlockPictures
    {
        private readonly DxfDatabase _source;
        private readonly CadFonts _fonts;
        private readonly int _width;
        private readonly int _height;
        private DxfDatabase _copy;

        internal BlockPictures(DxfDatabase source, CadFonts fonts, int width, int height)
        {
            _source = source;
            _fonts = fonts;
            _width = width;
            _height = height;
        }

        internal ImageSource Draw(string name)
        {
            try
            {
                _copy = _copy ?? DxfDatabase.Parse(_source.ToText());
                return CadStylePreview.Block(_copy, name, _fonts, _width, _height);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is FormatException)
            {
                GeometryHelperLog.Debug("BlockPictures: the block " + name + " was not drawn: " + exception.Message);
                return null;
            }
        }
    }

    /// <summary>Numbers as the block dialogs show and read them: invariant, to two places shown, any typed.</summary>
    internal static class BlockNumbers
    {
        internal static string Text(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

        internal static string Plain(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        internal static bool TryRead(string text, out double value)
            => double.TryParse((text ?? string.Empty).Replace(" ", string.Empty), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);

        internal static readonly DxfUnits[] Units =
        {
            DxfUnits.Undefined, DxfUnits.Inches, DxfUnits.Feet, DxfUnits.Millimeters, DxfUnits.Centimeters, DxfUnits.Decimeters, DxfUnits.Meters,
            DxfUnits.Kilometers, DxfUnits.Yards, DxfUnits.Microns,
        };

        /// <summary>A unit as the dialogs name it, AutoCAD's names in British spelling: Millimetres, Unitless.</summary>
        internal static string UnitName(DxfUnits unit)
        {
            switch (unit)
            {
                case DxfUnits.Undefined: return "Unitless";
                case DxfUnits.Millimeters: return "Millimetres";
                case DxfUnits.Centimeters: return "Centimetres";
                case DxfUnits.Decimeters: return "Decimetres";
                case DxfUnits.Meters: return "Metres";
                case DxfUnits.Kilometers: return "Kilometres";
                default: return unit.ToString();
            }
        }

        internal static DxfUnits Unit(string name) => Units.FirstOrDefault(u => UnitName(u) == name);
    }

    /// <summary>
    /// AutoCAD's Block Definition dialog, which BLOCK (B) shows, over the request its command hands the window: the name (a
    /// block of the drawing chosen to define it again), the base point, the objects and what becomes of them, the behaviour,
    /// the unit and the description. A drawing with three objects selected opens on "3 objects selected" and Convert to block.
    /// </summary>
    public sealed class BlockDefinitionViewModel : ObservableObject
    {
        private readonly BlockPictures _pictures;
        private ImageSource _selection;
        private bool _selectionDrawn;
        private string _name;
        private string _x;
        private string _y;
        private string _z;
        private string _unit;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of BLOCK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public BlockDefinitionViewModel(CadBlockDefinitionRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Names = CadBlocks.Named(request.Document.Database).Select(b => b.Name).ToList();
            _pictures = new BlockPictures(request.Document.Database, request.Document.Fonts, 44, 44);
            _name = request.Name ?? string.Empty;
            _x = BlockNumbers.Text(request.BasePoint.X);
            _y = BlockNumbers.Text(request.BasePoint.Y);
            _z = BlockNumbers.Text(request.BasePoint.Z);
            _unit = BlockNumbers.UnitName(request.Units);
        }

        /// <summary>Gets the request.</summary>
        public CadBlockDefinitionRequest Request { get; }

        /// <summary>Gets the names of the blocks of the drawing, which the name box offers.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>Gets or sets the name of the block.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(Exists));
                    Raise(nameof(Preview));
                    Raise(nameof(CanOk));
                }
            }
        }

        /// <summary>Gets whether a block of the name is in the drawing already, which OK asks to define again.</summary>
        public bool Exists => Names.Any(n => string.Equals(n, (_name ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets the picture of the block of the name, where the drawing has one; for a new one, of the objects selected, as the
        /// block made of them would look; null where there are none, or they are asked after OK.
        /// </summary>
        public ImageSource Preview => Exists ? _pictures.Draw(Names.First(n => string.Equals(n, _name.Trim(), StringComparison.OrdinalIgnoreCase))) : Selection();

        /// <summary>Gets or sets X of the base point, as typed.</summary>
        public string BaseX
        {
            get => _x;
            set => SetNumber(ref _x, value);
        }

        /// <summary>Gets or sets Y of the base point, as typed.</summary>
        public string BaseY
        {
            get => _y;
            set => SetNumber(ref _y, value);
        }

        /// <summary>Gets or sets Z of the base point, as typed.</summary>
        public string BaseZ
        {
            get => _z;
            set => SetNumber(ref _z, value);
        }

        /// <summary>Gets or sets whether the base point is asked after OK.</summary>
        public bool SpecifyBasePointOnScreen
        {
            get => Request.SpecifyBasePointOnScreen;
            set
            {
                Request.SpecifyBasePointOnScreen = value;
                Raise(nameof(SpecifyBasePointOnScreen));
                Raise(nameof(IsBasePointTyped));
            }
        }

        /// <summary>Gets whether the base point is typed here, the boxes enabled.</summary>
        public bool IsBasePointTyped => !Request.SpecifyBasePointOnScreen;

        /// <summary>Gets or sets whether the objects are asked after OK.</summary>
        public bool SpecifyObjectsOnScreen
        {
            get => Request.SpecifyObjectsOnScreen;
            set
            {
                Request.SpecifyObjectsOnScreen = value;
                Raise(nameof(SpecifyObjectsOnScreen));
                Raise(nameof(ObjectsText));
                Raise(nameof(HasObjects));
                Raise(nameof(Preview));
            }
        }

        /// <summary>Gets or sets whether the objects stay as they are.</summary>
        public bool IsRetain
        {
            get => Request.ObjectAction == CadBlockObjectAction.Retain;
            set => SetAction(value, CadBlockObjectAction.Retain);
        }

        /// <summary>Gets or sets whether the objects become an INSERT of the block.</summary>
        public bool IsConvert
        {
            get => Request.ObjectAction == CadBlockObjectAction.ConvertToBlock;
            set => SetAction(value, CadBlockObjectAction.ConvertToBlock);
        }

        /// <summary>Gets or sets whether the objects are erased.</summary>
        public bool IsDelete
        {
            get => Request.ObjectAction == CadBlockObjectAction.Delete;
            set => SetAction(value, CadBlockObjectAction.Delete);
        }

        /// <summary>Gets what the objects are: "3 objects selected", "No objects selected", or that they are asked after OK.</summary>
        public string ObjectsText
        {
            get
            {
                if (Request.SpecifyObjectsOnScreen)
                {
                    return "Objects asked after OK";
                }

                int count = Request.Objects?.Count ?? 0;
                return count == 0 ? "No objects selected" : count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " object selected" : " objects selected");
            }
        }

        /// <summary>Gets whether objects are given, the dot green; amber where none are.</summary>
        public bool HasObjects => Request.SpecifyObjectsOnScreen || (Request.Objects?.Count ?? 0) > 0;

        /// <summary>Gets or sets whether the INSERTs of the block scale it alike along every axis.</summary>
        public bool ScaleUniformly
        {
            get => Request.ScaleUniformly;
            set
            {
                Request.ScaleUniformly = value;
                Raise(nameof(ScaleUniformly));
            }
        }

        /// <summary>Gets or sets whether the INSERTs of the block may be exploded.</summary>
        public bool AllowExploding
        {
            get => Request.AllowExploding;
            set
            {
                Request.AllowExploding = value;
                Raise(nameof(AllowExploding));
            }
        }

        /// <summary>Gets the units the Block unit box offers: Unitless, Inches, Feet, Millimetres and the rest.</summary>
        public IReadOnlyList<string> Units { get; } = BlockNumbers.Units.Select(BlockNumbers.UnitName).ToList();

        /// <summary>Gets or sets the unit of the block, by its name.</summary>
        public string Unit
        {
            get => _unit;
            set => Set(ref _unit, value);
        }

        /// <summary>Gets or sets the description.</summary>
        public string Description
        {
            get => Request.Description;
            set
            {
                Request.Description = (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
                Raise(nameof(Description));
            }
        }

        /// <summary>Gets whether OK may be pressed: a name AutoCAD takes and the base point's numbers read.</summary>
        public bool CanOk => CadBlocks.IsName((_name ?? string.Empty).Trim()) && (Request.SpecifyBasePointOnScreen || (BlockNumbers.TryRead(_x, out _) && BlockNumbers.TryRead(_y, out _) && BlockNumbers.TryRead(_z, out _)));

        /// <summary>Closes the dialog with an action, the values written into the request.</summary>
        /// <param name="action">OK, Cancel, Pick point or Select objects.</param>
        /// <param name="redefine">Whether a block of the name already defined is to be defined again.</param>
        public void Close(CadDialogAction action, bool redefine = false)
        {
            Request.Name = (_name ?? string.Empty).Trim();

            if (BlockNumbers.TryRead(_x, out double x) && BlockNumbers.TryRead(_y, out double y) && BlockNumbers.TryRead(_z, out double z))
            {
                Request.BasePoint = new GeoPoint3(x, y, z);
            }

            Request.Units = BlockNumbers.Unit(_unit);
            Request.Redefine = redefine;
            Request.Action = action;
        }

        private void SetAction(bool value, CadBlockObjectAction action)
        {
            if (!value)
            {
                return;
            }

            Request.ObjectAction = action;
            Raise(nameof(IsRetain));
            Raise(nameof(IsConvert));
            Raise(nameof(IsDelete));
        }

        /// <summary>
        /// The picture of the objects selected, drawn once: in a copy of the drawing, a block of a name it has not made of
        /// copies of them about the base point, drawn as the Blocks palette draws a block. The drawing itself is not touched.
        /// </summary>
        private ImageSource Selection()
        {
            if (Request.SpecifyObjectsOnScreen || _selectionDrawn)
            {
                return Request.SpecifyObjectsOnScreen ? null : _selection;
            }

            _selectionDrawn = true;
            IReadOnlyList<string> objects = Request.Objects ?? Array.Empty<string>();

            if (objects.Count == 0)
            {
                return null;
            }

            try
            {
                DxfDatabase copy = DxfDatabase.Parse(Request.Document.Database.ToText());
                var entities = new List<DxfEntity>();

                foreach (string handle in objects)
                {
                    if (copy.TryGetObject(handle, out DxfDBObject found) && found is DxfEntity entity && !entity.IsErased)
                    {
                        entities.Add(entity);
                    }
                }

                if (entities.Count == 0)
                {
                    return null;
                }

                string name = "PREVIEW";

                for (int i = 1; copy.BlockTable.Has(name); i++)
                {
                    name = "PREVIEW" + i.ToString(CultureInfo.InvariantCulture);
                }

                var record = new DxfBlockTableRecord { Name = name, Origin = Request.BasePoint };
                copy.BlockTable.Add(record);
                copy.WblockCloneObjects(entities, record);
                _selection = CadStylePreview.Block(copy, name, Request.Document.Fonts, 44, 44);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is FormatException)
            {
                GeometryHelperLog.Debug("BlockDefinitionViewModel: the objects selected were not drawn: " + exception.Message);
                _selection = null;
            }

            return _selection;
        }

        private void SetNumber(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (Set(ref field, value ?? string.Empty, name))
            {
                Raise(nameof(CanOk));
            }
        }
    }

    /// <summary>
    /// AutoCAD's Blocks palette, which INSERT (I) shows, over the request its command hands the window: the blocks of the
    /// drawing, those inserted lately (Recent), or those of another drawing's file (Other drawing), each a tile drawn by the
    /// canvas's own renderer, filtered by the words typed; and the insertion options. Clicking a tile chooses it and closes
    /// the palette with OK.
    /// </summary>
    public sealed class BlocksPaletteViewModel : ObservableObject
    {
        private readonly IReadOnlyList<string> _recent;
        private readonly List<BlockTileViewModel> _current;
        private List<BlockTileViewModel> _other = new List<BlockTileViewModel>();
        private string _tab = "Current drawing";
        private string _filter = string.Empty;
        private string _otherFile;
        private string _scale = "1";
        private string _rotation = "0";

        /// <summary>Makes the palette over a request.</summary>
        /// <param name="request">The request of INSERT.</param>
        /// <param name="recent">The names of the blocks inserted lately, the latest first; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public BlocksPaletteViewModel(CadInsertRequest request, IReadOnlyList<string> recent)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _recent = recent ?? Array.Empty<string>();
            var pictures = new BlockPictures(request.Document.Database, request.Document.Fonts, 72, 56);
            _current = CadBlocks.Named(request.Document.Database).Select(b => new BlockTileViewModel(b.Name, b.Comments, pictures.Draw)).ToList();
            Tiles = new ObservableCollection<BlockTileViewModel>();
            Refill();
        }

        /// <summary>Gets the request.</summary>
        public CadInsertRequest Request { get; }

        /// <summary>Gets the tabs: Current drawing, Recent and Other drawing.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { "Current drawing", "Recent", "Other drawing" };

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Set(ref _tab, value ?? Tabs[0]))
                {
                    Raise(nameof(IsOtherDrawing));
                    Refill();
                }
            }
        }

        /// <summary>Gets whether the Other drawing tab is shown, with its Browse… button.</summary>
        public bool IsOtherDrawing => _tab == "Other drawing";

        /// <summary>Gets or sets the words the tiles are filtered by, letters of either case alike.</summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (Set(ref _filter, value ?? string.Empty))
                {
                    Refill();
                }
            }
        }

        /// <summary>Gets the tiles shown.</summary>
        public ObservableCollection<BlockTileViewModel> Tiles { get; }

        /// <summary>Gets the line above the tiles: the drawing's name and how many blocks it has, "plan-level2.dxf · 8 blocks".</summary>
        public string SourceText
        {
            get
            {
                string file = IsOtherDrawing ? (_otherFile == null ? "No drawing chosen: Browse… picks one" : Path.GetFileName(_otherFile)) : Path.GetFileName(Request.Document.FileName ?? string.Empty);
                int count = IsOtherDrawing ? _other.Count : _tab == "Recent" ? Tiles.Count : _current.Count;
                return file + " · " + count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " block" : " blocks");
            }
        }

        /// <summary>Gets the DXF file of the Other drawing tab; null before one is chosen.</summary>
        public string OtherFile => _otherFile;

        /// <summary>Lists the blocks of another drawing's file on the Other drawing tab, the whole file first as a block of its name.</summary>
        /// <param name="path">The path of the DXF file.</param>
        /// <returns>False, nothing changed, where the file cannot be read.</returns>
        public bool OpenOther(string path)
        {
            DxfDatabase other;

            try
            {
                other = DxfDatabase.Open(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException || exception is FormatException || exception is InvalidDataException)
            {
                GeometryHelperLog.Warn("BlocksPaletteViewModel: the drawing " + path + " was not read.", exception);
                return false;
            }

            _otherFile = path;
            var pictures = new BlockPictures(other, Request.Document.Fonts, 72, 56);
            _other = new List<BlockTileViewModel> { new BlockTileViewModel(string.Empty, "The whole drawing, as one block of its name", n => null) };
            _other.AddRange(CadBlocks.Named(other).Select(b => new BlockTileViewModel(b.Name, b.Comments, pictures.Draw)));
            Tab = "Other drawing";
            Refill();
            return true;
        }

        /// <summary>Gets or sets whether the insertion point is asked on the canvas.</summary>
        public bool SpecifyPointOnScreen
        {
            get => Request.SpecifyPointOnScreen;
            set
            {
                Request.SpecifyPointOnScreen = value;
                Raise(nameof(SpecifyPointOnScreen));
            }
        }

        /// <summary>Gets or sets whether the scale is asked on the command line.</summary>
        public bool SpecifyScaleOnScreen
        {
            get => Request.SpecifyScaleOnScreen;
            set
            {
                Request.SpecifyScaleOnScreen = value;
                Raise(nameof(SpecifyScaleOnScreen));
            }
        }

        /// <summary>Gets or sets the uniform scale, as typed.</summary>
        public string Scale
        {
            get => _scale;
            set => Set(ref _scale, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the rotation is asked on the command line.</summary>
        public bool SpecifyRotationOnScreen
        {
            get => Request.SpecifyRotationOnScreen;
            set
            {
                Request.SpecifyRotationOnScreen = value;
                Raise(nameof(SpecifyRotationOnScreen));
            }
        }

        /// <summary>Gets or sets the rotation in degrees, as typed.</summary>
        public string Rotation
        {
            get => _rotation;
            set => Set(ref _rotation, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the block is placed again and again until Enter.</summary>
        public bool Repeat
        {
            get => Request.Repeat;
            set
            {
                Request.Repeat = value;
                Raise(nameof(Repeat));
            }
        }

        /// <summary>Gets or sets whether the INSERT is exploded once placed.</summary>
        public bool Explode
        {
            get => Request.Explode;
            set
            {
                Request.Explode = value;
                Raise(nameof(Explode));
            }
        }

        /// <summary>Chooses a tile: the request given its block, the scale and rotation typed, and OK; false where a number does not read or is nought.</summary>
        /// <param name="tile">The tile.</param>
        /// <returns>Whether the palette closes.</returns>
        public bool Choose(BlockTileViewModel tile)
        {
            if (tile == null || !BlockNumbers.TryRead(_scale, out double scale) || scale == 0.0 || !BlockNumbers.TryRead(_rotation, out double degrees))
            {
                return false;
            }

            foreach (BlockTileViewModel other in Tiles)
            {
                other.IsSelected = other == tile;
            }

            Request.Name = tile.Name;
            Request.FileName = IsOtherDrawing ? _otherFile : null;
            Request.Scale = new GeoVector3(scale, scale, scale);
            Request.Rotation = degrees * Math.PI / 180.0;
            Request.Action = CadDialogAction.Ok;
            return true;
        }

        private void Refill()
        {
            IEnumerable<BlockTileViewModel> source;

            if (IsOtherDrawing)
            {
                source = _other;
            }
            else if (_tab == "Recent")
            {
                source = _recent.Select(n => _current.FirstOrDefault(t => string.Equals(t.Name, n, StringComparison.OrdinalIgnoreCase))).Where(t => t != null);
            }
            else
            {
                source = _current;
            }

            Tiles.Clear();

            foreach (BlockTileViewModel tile in source)
            {
                if (_filter.Length == 0 || (tile.Name.Length == 0 ? "whole drawing" : tile.Name).IndexOf(_filter, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Tiles.Add(tile);
                }
            }

            Raise(nameof(SourceText));
        }
    }

    /// <summary>
    /// AutoCAD's Write Block dialog, which WBLOCK (W) shows, over the request its command hands the window: the source (a
    /// block, the whole drawing or objects), the base point and objects, and the file written with its unit.
    /// </summary>
    public sealed class WriteBlockViewModel : ObservableObject
    {
        private string _x;
        private string _y;
        private string _z;
        private string _unit;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of WBLOCK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public WriteBlockViewModel(CadWriteBlockRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Blocks = CadBlocks.Named(request.Document.Database).Select(b => b.Name).ToList();
            _x = BlockNumbers.Text(request.BasePoint.X);
            _y = BlockNumbers.Text(request.BasePoint.Y);
            _z = BlockNumbers.Text(request.BasePoint.Z);
            _unit = BlockNumbers.UnitName(request.Units);

            if (string.IsNullOrEmpty(request.BlockName) && Blocks.Count > 0)
            {
                request.BlockName = Blocks[0];
            }

            if (string.IsNullOrEmpty(request.FileName))
            {
                string folder = Path.GetDirectoryName(request.Document.FileName ?? string.Empty);
                request.FileName = Path.Combine(string.IsNullOrEmpty(folder) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : folder, "new block.dxf");
            }
        }

        /// <summary>Gets the request.</summary>
        public CadWriteBlockRequest Request { get; }

        /// <summary>Gets the blocks of the drawing the Block box offers.</summary>
        public IReadOnlyList<string> Blocks { get; }

        /// <summary>Gets or sets whether a block is written.</summary>
        public bool IsBlock
        {
            get => Request.Source == CadWriteBlockSource.Block;
            set => SetSource(value, CadWriteBlockSource.Block);
        }

        /// <summary>Gets or sets whether the whole drawing is written.</summary>
        public bool IsEntireDrawing
        {
            get => Request.Source == CadWriteBlockSource.EntireDrawing;
            set => SetSource(value, CadWriteBlockSource.EntireDrawing);
        }

        /// <summary>Gets or sets whether objects are written, the base point and objects then enabled.</summary>
        public bool IsObjects
        {
            get => Request.Source == CadWriteBlockSource.Objects;
            set => SetSource(value, CadWriteBlockSource.Objects);
        }

        /// <summary>Gets or sets the block written.</summary>
        public string BlockName
        {
            get => Request.BlockName;
            set
            {
                Request.BlockName = value ?? string.Empty;
                Raise(nameof(BlockName));
            }
        }

        /// <summary>Gets or sets X of the base point, as typed.</summary>
        public string BaseX
        {
            get => _x;
            set => SetText(ref _x, value);
        }

        /// <summary>Gets or sets Y of the base point, as typed.</summary>
        public string BaseY
        {
            get => _y;
            set => SetText(ref _y, value);
        }

        /// <summary>Gets or sets Z of the base point, as typed.</summary>
        public string BaseZ
        {
            get => _z;
            set => SetText(ref _z, value);
        }

        /// <summary>Gets or sets whether the objects stay as they are.</summary>
        public bool IsRetain
        {
            get => Request.ObjectAction == CadBlockObjectAction.Retain;
            set => SetAction(value, CadBlockObjectAction.Retain);
        }

        /// <summary>Gets or sets whether the objects become an INSERT of a block of the file's name.</summary>
        public bool IsConvert
        {
            get => Request.ObjectAction == CadBlockObjectAction.ConvertToBlock;
            set => SetAction(value, CadBlockObjectAction.ConvertToBlock);
        }

        /// <summary>Gets or sets whether the objects are erased from the drawing.</summary>
        public bool IsDelete
        {
            get => Request.ObjectAction == CadBlockObjectAction.Delete;
            set => SetAction(value, CadBlockObjectAction.Delete);
        }

        /// <summary>Gets what the objects are: "3 objects selected" or "No objects selected".</summary>
        public string ObjectsText
        {
            get
            {
                int count = Request.Objects?.Count ?? 0;
                return count == 0 ? "No objects selected" : count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " object selected" : " objects selected");
            }
        }

        /// <summary>Gets whether objects are given, the dot green; amber where none are.</summary>
        public bool HasObjects => (Request.Objects?.Count ?? 0) > 0;

        /// <summary>Gets or sets the path of the file written.</summary>
        public string FileName
        {
            get => Request.FileName;
            set
            {
                Request.FileName = value ?? string.Empty;
                Raise(nameof(FileName));
                Raise(nameof(CanOk));
            }
        }

        /// <summary>Gets the units the Insert units box offers.</summary>
        public IReadOnlyList<string> Units { get; } = BlockNumbers.Units.Select(BlockNumbers.UnitName).ToList();

        /// <summary>Gets or sets the unit of the new drawing, by its name.</summary>
        public string Unit
        {
            get => _unit;
            set => Set(ref _unit, value);
        }

        /// <summary>Gets whether OK may be pressed: a file named, objects given where they are written, the base point read.</summary>
        public bool CanOk => !string.IsNullOrWhiteSpace(Request.FileName)
            && (Request.Source != CadWriteBlockSource.Objects || (HasObjects && BlockNumbers.TryRead(_x, out _) && BlockNumbers.TryRead(_y, out _) && BlockNumbers.TryRead(_z, out _)))
            && (Request.Source != CadWriteBlockSource.Block || !string.IsNullOrEmpty(Request.BlockName));

        /// <summary>Closes the dialog with an action, the values written into the request.</summary>
        /// <param name="action">OK, Cancel, Pick point or Select objects.</param>
        public void Close(CadDialogAction action)
        {
            if (BlockNumbers.TryRead(_x, out double x) && BlockNumbers.TryRead(_y, out double y) && BlockNumbers.TryRead(_z, out double z))
            {
                Request.BasePoint = new GeoPoint3(x, y, z);
            }

            Request.Units = BlockNumbers.Unit(_unit);
            Request.Action = action;
        }

        private void SetSource(bool value, CadWriteBlockSource source)
        {
            if (!value)
            {
                return;
            }

            Request.Source = source;
            Raise(nameof(IsBlock));
            Raise(nameof(IsEntireDrawing));
            Raise(nameof(IsObjects));
            Raise(nameof(CanOk));
        }

        private void SetAction(bool value, CadBlockObjectAction action)
        {
            if (!value)
            {
                return;
            }

            Request.ObjectAction = action;
            Raise(nameof(IsRetain));
            Raise(nameof(IsConvert));
            Raise(nameof(IsDelete));
        }

        private void SetText(ref string field, string value, [System.Runtime.CompilerServices.CallerMemberName] string name = null)
        {
            if (Set(ref field, value ?? string.Empty, name))
            {
                Raise(nameof(CanOk));
            }
        }
    }

    /// <summary>
    /// AutoCAD's Attribute Definition dialog, which ATTDEF (ATT) shows, over the request its command hands the window: the
    /// modes, the tag, prompt and default, the insertion point and the text settings.
    /// </summary>
    public sealed class AttributeDefinitionViewModel : ObservableObject
    {
        private string _height;
        private string _rotation;
        private string _x = "0.00";
        private string _y = "0.00";
        private string _z = "0.00";

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of ATTDEF.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public AttributeDefinitionViewModel(CadAttributeDefinitionRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _height = BlockNumbers.Plain(request.Height);
            _rotation = BlockNumbers.Plain(request.Rotation * 180.0 / Math.PI);
            Styles = request.Document.Database.TextStyleTable.Select(s => s.Name).Where(n => n.Length > 0).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();
        }

        /// <summary>Gets the request.</summary>
        public CadAttributeDefinitionRequest Request { get; }

        /// <summary>Gets or sets Invisible.</summary>
        public bool Invisible { get => Request.Invisible; set { Request.Invisible = value; Raise(nameof(Invisible)); } }

        /// <summary>Gets or sets Constant.</summary>
        public bool Constant { get => Request.Constant; set { Request.Constant = value; Raise(nameof(Constant)); } }

        /// <summary>Gets or sets Verify.</summary>
        public bool Verify { get => Request.Verify; set { Request.Verify = value; Raise(nameof(Verify)); } }

        /// <summary>Gets or sets Preset.</summary>
        public bool Preset { get => Request.Preset; set { Request.Preset = value; Raise(nameof(Preset)); } }

        /// <summary>Gets or sets Lock position.</summary>
        public bool LockPosition { get => Request.LockPosition; set { Request.LockPosition = value; Raise(nameof(LockPosition)); } }

        /// <summary>Gets or sets Multiple lines.</summary>
        public bool MultipleLines { get => Request.MultipleLines; set { Request.MultipleLines = value; Raise(nameof(MultipleLines)); } }

        /// <summary>Gets or sets the tag.</summary>
        public string Tag { get => Request.Tag; set { Request.Tag = value ?? string.Empty; Raise(nameof(Tag)); Raise(nameof(CanOk)); Raise(nameof(Note)); } }

        /// <summary>Gets or sets the prompt.</summary>
        public string Prompt { get => Request.Prompt; set { Request.Prompt = value ?? string.Empty; Raise(nameof(Prompt)); Raise(nameof(Note)); } }

        /// <summary>Gets or sets the default value.</summary>
        public string Default { get => Request.Default; set { Request.Default = value ?? string.Empty; Raise(nameof(Default)); Raise(nameof(Note)); } }

        /// <summary>Gets or sets whether the insertion point is asked after OK, the boxes then disabled.</summary>
        public bool SpecifyOnScreen
        {
            get => Request.SpecifyOnScreen;
            set
            {
                Request.SpecifyOnScreen = value;
                Raise(nameof(SpecifyOnScreen));
                Raise(nameof(IsPointTyped));
            }
        }

        /// <summary>Gets whether the insertion point is typed here.</summary>
        public bool IsPointTyped => !Request.SpecifyOnScreen;

        /// <summary>Gets or sets X of the insertion point, as typed.</summary>
        public string X { get => _x; set { Set(ref _x, value ?? string.Empty); Raise(nameof(CanOk)); } }

        /// <summary>Gets or sets Y of the insertion point, as typed.</summary>
        public string Y { get => _y; set { Set(ref _y, value ?? string.Empty); Raise(nameof(CanOk)); } }

        /// <summary>Gets or sets Z of the insertion point, as typed.</summary>
        public string Z { get => _z; set { Set(ref _z, value ?? string.Empty); Raise(nameof(CanOk)); } }

        /// <summary>Gets the justifications the box offers, by AutoCAD's names: Left, Center, Right, Middle, Top left and the rest.</summary>
        public IReadOnlyList<string> Justifications { get; } = JustificationNames.All;

        /// <summary>Gets or sets the justification, by its name.</summary>
        public string Justification
        {
            get => JustificationNames.Of(Request.Justification);
            set
            {
                Request.Justification = JustificationNames.From(value);
                Raise(nameof(Justification));
            }
        }

        /// <summary>Gets the text styles of the drawing.</summary>
        public IReadOnlyList<string> Styles { get; }

        /// <summary>Gets or sets the text style; Standard where none is given.</summary>
        public string TextStyle
        {
            get => Request.TextStyle ?? "Standard";
            set
            {
                Request.TextStyle = value;
                Raise(nameof(TextStyle));
            }
        }

        /// <summary>Gets or sets the text height, as typed.</summary>
        public string Height { get => _height; set { Set(ref _height, value ?? string.Empty); Raise(nameof(CanOk)); } }

        /// <summary>Gets or sets the rotation in degrees, as typed.</summary>
        public string Rotation { get => _rotation; set { Set(ref _rotation, value ?? string.Empty); Raise(nameof(CanOk)); } }

        /// <summary>Gets what OK then does, as the board's note says.</summary>
        public string Note => "OK asks for the start point on the canvas. The tag " + (string.IsNullOrEmpty(Request.Tag) ? "TAG" : Request.Tag.ToUpperInvariant())
            + " shows on the canvas until the definition goes into a block with BLOCK; INSERT then asks \"" + (string.IsNullOrEmpty(Request.Prompt) ? Request.Tag : Request.Prompt) + " <" + Request.Default + ">:\".";

        /// <summary>Gets whether OK may be pressed: a tag of one word, a height above nought and the numbers read.</summary>
        public bool CanOk => CadBlocks.IsTag((Request.Tag ?? string.Empty).Trim()) && BlockNumbers.TryRead(_height, out double h) && h > 0.0 && BlockNumbers.TryRead(_rotation, out _)
            && (Request.SpecifyOnScreen || (BlockNumbers.TryRead(_x, out _) && BlockNumbers.TryRead(_y, out _) && BlockNumbers.TryRead(_z, out _)));

        /// <summary>Closes the dialog with an action, the values written into the request.</summary>
        /// <param name="action">OK, Cancel, or the height or rotation to pick.</param>
        public void Close(CadDialogAction action)
        {
            if (BlockNumbers.TryRead(_height, out double h) && h > 0.0)
            {
                Request.Height = h;
            }

            if (BlockNumbers.TryRead(_rotation, out double degrees))
            {
                Request.Rotation = degrees * Math.PI / 180.0;
            }

            if (BlockNumbers.TryRead(_x, out double x) && BlockNumbers.TryRead(_y, out double y) && BlockNumbers.TryRead(_z, out double z))
            {
                Request.Point = new GeoPoint3(x, y, z);
            }

            Request.Action = action;
        }
    }

    /// <summary>The justifications of a text as the dialogs name them, AutoCAD's words.</summary>
    internal static class JustificationNames
    {
        private static readonly KeyValuePair<string, DxfAttachmentPoint>[] Names =
        {
            new KeyValuePair<string, DxfAttachmentPoint>("Left", DxfAttachmentPoint.BaseLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("Center", DxfAttachmentPoint.BaseCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("Right", DxfAttachmentPoint.BaseRight),
            new KeyValuePair<string, DxfAttachmentPoint>("Middle", DxfAttachmentPoint.BaseMid),
            new KeyValuePair<string, DxfAttachmentPoint>("Top left", DxfAttachmentPoint.TopLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("Top center", DxfAttachmentPoint.TopCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("Top right", DxfAttachmentPoint.TopRight),
            new KeyValuePair<string, DxfAttachmentPoint>("Middle left", DxfAttachmentPoint.MiddleLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("Middle center", DxfAttachmentPoint.MiddleCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("Middle right", DxfAttachmentPoint.MiddleRight),
            new KeyValuePair<string, DxfAttachmentPoint>("Bottom left", DxfAttachmentPoint.BottomLeft),
            new KeyValuePair<string, DxfAttachmentPoint>("Bottom center", DxfAttachmentPoint.BottomCenter),
            new KeyValuePair<string, DxfAttachmentPoint>("Bottom right", DxfAttachmentPoint.BottomRight),
        };

        internal static IReadOnlyList<string> All { get; } = Names.Select(n => n.Key).ToList();

        internal static string Of(DxfAttachmentPoint point) => Names.FirstOrDefault(n => n.Value == point).Key ?? point.ToString();

        internal static DxfAttachmentPoint From(string name) => Names.FirstOrDefault(n => n.Key == name).Key == null ? DxfAttachmentPoint.BaseLeft : Names.First(n => n.Key == name).Value;
    }

    /// <summary>One attribute of the Enhanced Attribute Editor's list: its tag, prompt and value, and its text options and properties as typed.</summary>
    public sealed class AttributeRowViewModel : ObservableObject
    {
        private string _height;
        private string _rotation;
        private string _width;
        private string _oblique;

        internal AttributeRowViewModel(CadAttributeRow row)
        {
            Row = row;
            _height = BlockNumbers.Plain(row.Height);
            _rotation = BlockNumbers.Plain(row.Rotation * 180.0 / Math.PI);
            _width = BlockNumbers.Plain(row.WidthFactor);
            _oblique = BlockNumbers.Plain(row.Oblique * 180.0 / Math.PI);
        }

        /// <summary>Gets the row of the request.</summary>
        public CadAttributeRow Row { get; }

        /// <summary>Gets the tag.</summary>
        public string Tag => Row.Tag;

        /// <summary>Gets the prompt.</summary>
        public string Prompt => Row.Prompt;

        /// <summary>Gets or sets the value.</summary>
        public string Value { get => Row.Value; set { Row.Value = value ?? string.Empty; Raise(nameof(Value)); } }

        /// <summary>Gets or sets the text style.</summary>
        public string TextStyle { get => Row.TextStyle; set { Row.TextStyle = value; Raise(nameof(TextStyle)); } }

        /// <summary>Gets or sets the justification, by its name.</summary>
        public string Justification { get => JustificationNames.Of(Row.Justification); set { Row.Justification = JustificationNames.From(value); Raise(nameof(Justification)); } }

        /// <summary>Gets or sets the height, as typed.</summary>
        public string Height { get => _height; set { if (Set(ref _height, value ?? string.Empty) && BlockNumbers.TryRead(_height, out double h)) { Row.Height = h; } } }

        /// <summary>Gets or sets the rotation in degrees, as typed.</summary>
        public string Rotation { get => _rotation; set { if (Set(ref _rotation, value ?? string.Empty) && BlockNumbers.TryRead(_rotation, out double d)) { Row.Rotation = d * Math.PI / 180.0; } } }

        /// <summary>Gets or sets whether the text is written backwards.</summary>
        public bool Backwards { get => Row.Backwards; set { Row.Backwards = value; Raise(nameof(Backwards)); } }

        /// <summary>Gets or sets whether the text is upside down.</summary>
        public bool UpsideDown { get => Row.UpsideDown; set { Row.UpsideDown = value; Raise(nameof(UpsideDown)); } }

        /// <summary>Gets or sets the width factor, as typed.</summary>
        public string WidthFactor { get => _width; set { if (Set(ref _width, value ?? string.Empty) && BlockNumbers.TryRead(_width, out double w)) { Row.WidthFactor = w; } } }

        /// <summary>Gets or sets the oblique angle in degrees, as typed.</summary>
        public string Oblique { get => _oblique; set { if (Set(ref _oblique, value ?? string.Empty) && BlockNumbers.TryRead(_oblique, out double d)) { Row.Oblique = d * Math.PI / 180.0; } } }

        /// <summary>Gets or sets the layer.</summary>
        public string Layer { get => Row.Layer; set { Row.Layer = value; Raise(nameof(Layer)); } }

        /// <summary>Gets or sets the linetype.</summary>
        public string Linetype { get => Row.Linetype; set { Row.Linetype = value; Raise(nameof(Linetype)); } }

        /// <summary>Gets or sets the colour by its name: ByLayer, ByBlock, Red to White, or Color n.</summary>
        public string Color { get => AttributeEditorViewModel.ColorName(Row.ColorIndex); set { Row.ColorIndex = AttributeEditorViewModel.ColorIndex(value, Row.ColorIndex); Raise(nameof(Color)); } }

        /// <summary>Gets or sets the lineweight by its name: ByLayer, ByBlock, Default, or millimetres.</summary>
        public string LineWeight { get => AttributeEditorViewModel.WeightName(Row.LineWeight); set { Row.LineWeight = AttributeEditorViewModel.Weight(value, Row.LineWeight); Raise(nameof(LineWeight)); } }
    }

    /// <summary>
    /// AutoCAD's Enhanced Attribute Editor, which EATTEDIT shows and a double click on an INSERT with attributes opens, over
    /// the request its command hands the window: the block and tag, the tabs Attribute (the list of tag, prompt and value,
    /// and the value of the one chosen), Text Options and Properties. What is typed shows on the canvas at once; Apply keeps
    /// it with the editor open, OK keeps it and closes, Cancel takes back what Apply did not keep, as AutoCAD's.
    /// </summary>
    public sealed class AttributeEditorViewModel : ObservableObject
    {
        private static readonly string[] ColorNames = { "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };
        private string _tab = "Attribute";
        private AttributeRowViewModel _selected;

        /// <summary>Makes the editor over a request.</summary>
        /// <param name="request">The request of EATTEDIT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public AttributeEditorViewModel(CadAttributeEditRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Rows = request.Rows.Select(r => new AttributeRowViewModel(r)).ToList();
            _selected = Rows.FirstOrDefault();

            // Whatever is typed into a row shows on the canvas at once; Cancel takes back what Apply did not keep.
            foreach (AttributeRowViewModel row in Rows)
            {
                row.PropertyChanged += (sender, e) => request.Preview();
            }

            DxfDatabase database = request.Document.Database;
            Styles = database.TextStyleTable.Select(s => s.Name).Where(n => n.Length > 0).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();
            Layers = database.LayerTable.Select(l => l.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();
            Linetypes = database.LinetypeTable.Select(l => l.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();
        }

        /// <summary>Gets the request.</summary>
        public CadAttributeEditRequest Request { get; }

        /// <summary>Gets the name of the block of the INSERT.</summary>
        public string Block => Request.Block;

        /// <summary>Gets the attributes.</summary>
        public IReadOnlyList<AttributeRowViewModel> Rows { get; }

        /// <summary>Gets or sets the attribute chosen, which the value and the other tabs show.</summary>
        public AttributeRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(Tag));
                }
            }
        }

        /// <summary>Gets the tag of the attribute chosen.</summary>
        public string Tag => _selected?.Tag ?? string.Empty;

        /// <summary>Gets the tabs.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { "Attribute", "Text Options", "Properties" };

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Set(ref _tab, value ?? "Attribute"))
                {
                    Raise(nameof(IsAttributeTab));
                    Raise(nameof(IsTextTab));
                    Raise(nameof(IsPropertiesTab));
                }
            }
        }

        /// <summary>Gets whether the Attribute tab is shown.</summary>
        public bool IsAttributeTab => _tab == "Attribute";

        /// <summary>Gets whether the Text Options tab is shown.</summary>
        public bool IsTextTab => _tab == "Text Options";

        /// <summary>Gets whether the Properties tab is shown.</summary>
        public bool IsPropertiesTab => _tab == "Properties";

        /// <summary>Gets the text styles of the drawing.</summary>
        public IReadOnlyList<string> Styles { get; }

        /// <summary>Gets the justifications.</summary>
        public IReadOnlyList<string> Justifications { get; } = JustificationNames.All;

        /// <summary>Gets the layers of the drawing.</summary>
        public IReadOnlyList<string> Layers { get; }

        /// <summary>Gets the linetypes of the drawing.</summary>
        public IReadOnlyList<string> Linetypes { get; }

        /// <summary>Gets the colours offered: ByLayer, ByBlock and the seven named.</summary>
        public IReadOnlyList<string> Colors { get; } = new[] { "ByLayer" }.Concat(ColorNames).ToList();

        /// <summary>Gets the lineweights offered: ByLayer, ByBlock, Default and AutoCAD's widths in millimetres.</summary>
        public IReadOnlyList<string> LineWeights { get; } = new[] { "ByLayer", "ByBlock", "Default" }.Concat(new[] { 0, 5, 9, 13, 15, 18, 20, 25, 30, 35, 40, 50, 53, 60, 70, 80, 90, 100, 106, 120, 140, 158, 200, 211 }.Select(w => (w / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm")).ToList();

        /// <summary>Closes the editor with an action: OK keeps what was typed, Cancel takes back what Apply did not keep.</summary>
        /// <param name="action">OK, Cancel, Apply or Select block.</param>
        public void Close(CadDialogAction action) => Request.Action = action;

        /// <summary>Apply: what was typed is kept, the editor staying open, so that a Cancel after it takes back only what is typed after it.</summary>
        public void Apply() => Request.Apply();

        internal static string ColorName(int index) => index == 256 ? "ByLayer" : index >= 0 && index <= 7 ? ColorNames[index] : "Color " + index.ToString(CultureInfo.InvariantCulture);

        internal static int ColorIndex(string name, int fallback)
        {
            if (name == "ByLayer")
            {
                return 256;
            }

            int at = Array.IndexOf(ColorNames, name);

            if (at >= 0)
            {
                return at;
            }

            return name != null && name.StartsWith("Color ", StringComparison.Ordinal) && int.TryParse(name.Substring(6), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : fallback;
        }

        internal static string WeightName(int weight) => weight == -1 ? "ByLayer" : weight == -2 ? "ByBlock" : weight == -3 ? "Default" : (weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm";

        internal static int Weight(string name, int fallback)
        {
            switch (name)
            {
                case "ByLayer": return -1;
                case "ByBlock": return -2;
                case "Default": return -3;
            }

            return name != null && BlockNumbers.TryRead(name.Replace(" mm", string.Empty), out double mm) ? (int)Math.Round(mm * 100.0) : fallback;
        }
    }
}
