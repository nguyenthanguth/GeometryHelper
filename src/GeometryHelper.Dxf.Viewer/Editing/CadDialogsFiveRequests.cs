using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What PLOTSTAMP gives its Plot Stamp dialog: a copy of the session's stamp to edit, which the session's stamp takes
    /// when the dialog closes with OK (<see cref="CadPlotStamp.CopyFrom"/>).
    /// </summary>
    public sealed class CadPlotStampRequest
    {
        internal CadPlotStampRequest(CadPlotStamp stamp)
        {
            Stamp = stamp.Clone();
        }

        /// <summary>Gets the copy of the stamp the dialog edits.</summary>
        public CadPlotStamp Stamp { get; }

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }

    /// <summary>
    /// What IMAGEADJUST gives its Image Adjust dialog: the images selected, the brightness, contrast and fade it starts at
    /// (the image's own where one is selected, AutoCAD's 50, 50 and 0 where several are) and the picture of the first, for
    /// the preview; the values set, each 0 to 100, are given to the images when it closes with OK, as one undo step.
    /// </summary>
    public sealed class CadImageAdjustRequest
    {
        internal CadImageAdjustRequest(int count, int brightness, int contrast, int fade, string imageFile)
        {
            Count = count;
            Brightness = brightness;
            Contrast = contrast;
            Fade = fade;
            ImageFile = imageFile;
        }

        /// <summary>Gets how many images are adjusted.</summary>
        public int Count { get; }

        /// <summary>Gets or sets the brightness, 0 to 100.</summary>
        public int Brightness { get; set; }

        /// <summary>Gets or sets the contrast, 0 to 100.</summary>
        public int Contrast { get; set; }

        /// <summary>Gets or sets the fade, 0 to 100.</summary>
        public int Fade { get; set; }

        /// <summary>Gets the file of the first image's picture where it is found; null where it is not.</summary>
        public string ImageFile { get; }

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }

    /// <summary>The file format of an attribute extract, as the Attribute Extraction dialog's File Format names it.</summary>
    public enum CadAttextFormat
    {
        /// <summary>Comma Delimited File (CDF), as its template says.</summary>
        Cdf,

        /// <summary>Space Delimited File (SDF), as its template says.</summary>
        Sdf,

        /// <summary>DXF Format Extract File (DXX), with no template.</summary>
        Dxf,
    }

    /// <summary>
    /// What ATTEXT gives its Attribute Extraction dialog: the file format, the objects selected (none for every block of the
    /// drawing), the template and output files. Select Objects closes the dialog with <see cref="IsSelecting"/> set, ATTEXT
    /// then asks "Select objects:" and shows it again with the objects found; OK writes the extract as -ATTEXT does.
    /// </summary>
    public sealed class CadAttextRequest
    {
        /// <summary>Gets or sets the file format; CDF to start with.</summary>
        public CadAttextFormat Format { get; set; }

        /// <summary>Gets or sets the template file, which a CDF or SDF extract needs.</summary>
        public string TemplateFile { get; set; } = string.Empty;

        /// <summary>Gets or sets the output file; the drawing's name with .txt, or .dxx for DXF, to start with.</summary>
        public string OutputFile { get; set; } = string.Empty;

        /// <summary>Gets or sets the handles of the objects selected; null for the whole drawing.</summary>
        public IReadOnlyList<string> Selected { get; set; }

        /// <summary>Gets or sets whether the dialog closed for its Select Objects button.</summary>
        public bool IsSelecting { get; set; }

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }

    /// <summary>
    /// What XBIND gives its Xbind dialog: each external reference with the definitions it brought, by kind (Block, Dimstyle,
    /// Layer, Linetype, Textstyle), and the definitions to bind, which -XBIND's binding binds when it closes with OK.
    /// </summary>
    public sealed class CadXbindRequest
    {
        internal CadXbindRequest(IReadOnlyList<CadXbindReference> references)
        {
            References = references;
        }

        /// <summary>Gets the external references, sorted by name, with their definitions.</summary>
        public IReadOnlyList<CadXbindReference> References { get; }

        /// <summary>Gets the definitions to bind, each its kind and its name, as xref|WALL.</summary>
        public IList<KeyValuePair<string, string>> ToBind { get; } = new List<KeyValuePair<string, string>>();

        /// <summary>Gets or sets whether the dialog closed with OK.</summary>
        public bool IsAccepted { get; set; }
    }

    /// <summary>An external reference of the Xbind dialog's tree: its name and the definitions of each kind it brought.</summary>
    public sealed class CadXbindReference
    {
        internal CadXbindReference(string name, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> definitions)
        {
            Name = name;
            Definitions = definitions;
        }

        /// <summary>Gets the name of the external reference.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the definitions by kind, in the tree's order: Block, Dimstyle, Layer, Linetype and Textstyle, a kind with none
        /// left out, each sorted by name.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> Definitions { get; }
    }
}
