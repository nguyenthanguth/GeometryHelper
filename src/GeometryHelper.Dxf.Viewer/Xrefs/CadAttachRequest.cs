using System;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What an attach dialog attaches: a DXF as an external reference (XATTACH), or a picture as an image (IMAGEATTACH).</summary>
    public enum CadAttachKind
    {
        /// <summary>A DXF drawing, attached or overlaid as an external reference.</summary>
        Xref,

        /// <summary>A picture file, PNG, JPEG or BMP, attached as an image.</summary>
        Image,
    }

    /// <summary>
    /// The Attach External Reference and Attach Image dialogs of XATTACH and IMAGEATTACH, as data the command and the window
    /// share: the file, its name and path type, and the insertion point, scale and rotation, each given or asked on the command
    /// line after OK. Setting <see cref="FileName"/> reads what the dialog shows of the file: a picture's pixels and size, a
    /// drawing's unit.
    /// </summary>
    public sealed class CadAttachRequest
    {
        private string _fileName;

        /// <summary>Makes the request of a document, as the dialog opens: the point asked, scale 1, no rotation, a relative path.</summary>
        /// <param name="document">The document.</param>
        /// <param name="kind">What is attached.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public CadAttachRequest(CadDocument document, CadAttachKind kind)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Kind = kind;
        }

        /// <summary>Gets the document attached to.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets what is attached.</summary>
        public CadAttachKind Kind { get; }

        /// <summary>
        /// Gets or sets the full path of the file chosen. Set, the name becomes the file's name without its extension, and the
        /// file is read: a picture's pixels and resolution, a drawing's unit; <see cref="Error"/> says why one cannot be attached.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            set
            {
                _fileName = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                Error = null;
                Drawing = null;
                PixelWidth = 0;
                PixelHeight = 0;
                BaseWidth = 0.0;
                BaseHeight = 0.0;
                HasResolution = false;
                UnitFactor = 1.0;
                Units = Document.Database.Insunits;

                if (_fileName == null)
                {
                    Name = string.Empty;
                    return;
                }

                try
                {
                    _fileName = Path.GetFullPath(_fileName);
                    Name = Path.GetFileNameWithoutExtension(_fileName);
                }
                catch (Exception exception)
                    when (exception is ArgumentException
                        || exception is NotSupportedException
                        || exception is PathTooLongException
                    )
                {
                    Error = "Invalid file name.";
                    Name = string.Empty;
                    return;
                }

                if (Kind == CadAttachKind.Image)
                {
                    ReadPicture();
                }
                else
                {
                    ReadDrawing();
                }
            }
        }

        /// <summary>Gets or sets the name: of the external reference's block, or of the image definition; the file's name until set.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets whether the drawing is overlaid rather than attached (Reference Type); not for an image.</summary>
        public bool IsOverlay { get; set; }

        /// <summary>Gets or sets how the path is written; Relative until set, as AutoCAD 2021's dialog opens.</summary>
        public CadPathType PathType { get; set; } = CadPathType.Relative;

        /// <summary>
        /// Gets the path written for the file: as <see cref="PathType"/> says, from the drawing's folder for Relative, the full
        /// path while the drawing has no file yet; empty while no file is chosen.
        /// </summary>
        public string SavedPath =>
            _fileName == null ? string.Empty : CadXrefManager.PathFor(_fileName, PathType, Document.DrawingFolder);

        /// <summary>Gets whether Relative is asked of a drawing not saved yet, which is written with the full path instead.</summary>
        public bool IsRelativeFallingBack => PathType == CadPathType.Relative && Document.DrawingFolder == null;

        /// <summary>Gets or sets whether the insertion point is asked on the command line; true until set.</summary>
        public bool SpecifyPointOnScreen { get; set; } = true;

        /// <summary>Gets or sets the insertion point where it is not asked.</summary>
        public GeoPoint3 Point { get; set; }

        /// <summary>Gets or sets whether the scale is asked on the command line.</summary>
        public bool SpecifyScaleOnScreen { get; set; }

        /// <summary>Gets or sets the scale along X, Y and Z where it is not asked; 1, 1, 1 until set. An image takes X alone.</summary>
        public GeoVector3 Scale { get; set; } = new GeoVector3(1.0, 1.0, 1.0);

        /// <summary>Gets or sets whether the external reference is scaled alike along every axis (Uniform Scale); true until set.</summary>
        public bool UniformScale { get; set; } = true;

        /// <summary>Gets or sets whether the rotation is asked on the command line.</summary>
        public bool SpecifyRotationOnScreen { get; set; }

        /// <summary>Gets or sets the rotation where it is not asked, in radians.</summary>
        public double Rotation { get; set; }

        /// <summary>Gets the pixels across of the picture read; nought before.</summary>
        public int PixelWidth { get; private set; }

        /// <summary>Gets the pixels up of the picture read; nought before.</summary>
        public int PixelHeight { get; private set; }

        /// <summary>
        /// Gets the width of the picture at scale 1 in the drawing's units, as AutoCAD 2021 sizes it: its pixels over its
        /// resolution where the file gives one and the drawing has a unit (40 pixels of 96 dots an inch are 10.5833 mm), else
        /// 1, as AutoCAD sizes a picture of no resolution one unit wide.
        /// </summary>
        public double BaseWidth { get; private set; }

        /// <summary>Gets the height of the picture at scale 1 in the drawing's units, in the measure of its width.</summary>
        public double BaseHeight { get; private set; }

        /// <summary>Gets whether the picture read gives its resolution, which sizes it in the drawing's unit.</summary>
        public bool HasResolution { get; private set; }

        /// <summary>Gets the unit: of the drawing read for an external reference (Block Unit), of this drawing for an image.</summary>
        public DxfUnits Units { get; private set; }

        /// <summary>Gets the factor an external reference is scaled by, from its drawing's unit to this drawing's (Factor); 1 for an image.</summary>
        public double UnitFactor { get; private set; } = 1.0;

        /// <summary>Gets the drawing read for an external reference, to preview it; null for an image or a file that cannot be read.</summary>
        public DxfDatabase Drawing { get; private set; }

        /// <summary>Gets why the file chosen cannot be attached, in AutoCAD's words; null where it can.</summary>
        public string Error { get; private set; }

        /// <summary>Gets or sets what the window's dialog was closed with; Cancel until set.</summary>
        public CadDialogAction Action { get; set; }

        private void ReadDrawing()
        {
            if (!File.Exists(_fileName))
            {
                Error = "\"" + _fileName + "\" cannot be found.";
                return;
            }

            CadXrefContent content = Document.Xrefs.Read(_fileName);

            if (content.Database == null)
            {
                Error = "Error: Unable to load " + _fileName + ".";
                return;
            }

            Drawing = content.Database;
            Units = content.Database.Insunits;
            double from = CadCommands.UnitMillimetres(Units);
            double to = CadCommands.UnitMillimetres(Document.Database.Insunits);
            UnitFactor = from > 0.0 && to > 0.0 ? from / to : 1.0;
        }

        private void ReadPicture()
        {
            if (!CadImageFile.IsPicture(_fileName))
            {
                Error = "Only PNG, JPEG and BMP pictures are attached.";
                return;
            }

            if (!File.Exists(_fileName))
            {
                Error = "Image not found.";
                return;
            }

            if (!CadImageFile.TryRead(_fileName, out int width, out int height, out double mx, out double my))
            {
                Error = "Invalid image file.";
                return;
            }

            PixelWidth = width;
            PixelHeight = height;
            double unit = CadCommands.UnitMillimetres(Document.Database.Insunits);
            HasResolution = mx > 0.0 && my > 0.0 && unit > 0.0;

            if (HasResolution)
            {
                BaseWidth = width * mx / unit;
                BaseHeight = height * my / unit;
            }
            else
            {
                BaseWidth = 1.0;
                BaseHeight = (double)height / width;
            }
        }
    }
}
