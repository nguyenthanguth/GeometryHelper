using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace GeometryHelper.Cad
{
    /// <summary>Which object types the Select Objects page of the Data Extraction wizard lists.</summary>
    public enum CadExtractionDisplay
    {
        /// <summary>Blocks and the other objects.</summary>
        All = 0,

        /// <summary>"Display blocks only".</summary>
        BlocksOnly = 1,

        /// <summary>"Display non-blocks only".</summary>
        NonBlocksOnly = 2,
    }

    /// <summary>The conditions of a filter on the Refine Data page, in the words of its list.</summary>
    public enum CadFilterCondition
    {
        /// <summary>No filter.</summary>
        None = 0,

        /// <summary>Greater than.</summary>
        Greater = 1,

        /// <summary>Greater than or equal to.</summary>
        GreaterOrEqual = 2,

        /// <summary>Less than.</summary>
        Less = 3,

        /// <summary>Less than or equal to.</summary>
        LessOrEqual = 4,

        /// <summary>Equal to.</summary>
        Equal = 5,

        /// <summary>Not equal to.</summary>
        NotEqual = 6,

        /// <summary>Between two values, both included.</summary>
        Between = 7,

        /// <summary>Outside two values.</summary>
        Outside = 8,

        /// <summary>Contains the words.</summary>
        Contains = 9,

        /// <summary>Begins with the words.</summary>
        BeginsWith = 10,
    }

    /// <summary>A sort of the extracted rows: a column, up or down.</summary>
    public sealed class CadExtractionSort
    {
        /// <summary>Makes a sort.</summary>
        /// <param name="column">The column's key: <c>Count</c>, <c>Name</c> or a property's key.</param>
        /// <param name="descending">Whether the largest comes first.</param>
        public CadExtractionSort(string column, bool descending)
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
            Descending = descending;
        }

        /// <summary>Gets the column's key.</summary>
        public string Column { get; }

        /// <summary>Gets whether the sort is downwards.</summary>
        public bool Descending { get; }
    }

    /// <summary>A filter of the extracted rows: a column, a condition and the value or two values it compares with.</summary>
    public sealed class CadExtractionFilter
    {
        /// <summary>Makes a filter.</summary>
        /// <param name="column">The column's key.</param>
        /// <param name="condition">The condition.</param>
        /// <param name="value">The value; the lower one of Between and Outside.</param>
        /// <param name="second">The upper value of Between and Outside.</param>
        public CadExtractionFilter(string column, CadFilterCondition condition, string value, string second = "")
        {
            Column = column ?? throw new ArgumentNullException(nameof(column));
            Condition = condition;
            Value = value ?? string.Empty;
            Second = second ?? string.Empty;
        }

        /// <summary>Gets the column's key.</summary>
        public string Column { get; }

        /// <summary>Gets the condition.</summary>
        public CadFilterCondition Condition { get; }

        /// <summary>Gets the value.</summary>
        public string Value { get; }

        /// <summary>Gets the second value of Between and Outside.</summary>
        public string Second { get; }
    }

    /// <summary>
    /// The settings of a data extraction, as the Data Extraction wizard collects them and a .dxe file keeps them: the data
    /// source (the current drawing, with the objects selected or all of them, and other drawing files and folders), the
    /// object types and properties chosen, how the rows are refined (identical rows combined, the Count and Name columns,
    /// sorts, filters, columns renamed) and where the data goes (a table in the drawing, a file of .xlsx, .csv or .txt).
    /// </summary>
    /// <remarks>
    /// The file is XML of the presenter's own, <c>GeometryHelperDataExtraction</c>, not the .dxe AutoCAD writes, which is a
    /// serialised .NET object of AutoCAD's own assemblies that only AutoCAD reads; a .dxe of AutoCAD is not read here and the
    /// presenter's is not read by AutoCAD. The data link of a table made from a data extraction names the file by its
    /// connection string, as AutoCAD's names its own.
    /// </remarks>
    public sealed class CadExtractionSettings
    {
        /// <summary>Gets or sets whether the current drawing is a source of the extraction.</summary>
        public bool IncludeCurrentDrawing { get; set; } = true;

        /// <summary>Gets the drawing files that are sources besides the current drawing, as full paths.</summary>
        public List<string> Files { get; } = new List<string>();

        /// <summary>Gets the folders whose drawings are sources (DWG and DXF files), as full paths.</summary>
        public List<string> Folders { get; } = new List<string>();

        /// <summary>Gets or sets whether the drawings of the folders in <see cref="Folders"/> are searched in their subfolders too.</summary>
        public bool IncludeSubfolders { get; set; }

        /// <summary>
        /// Gets the handles of the objects of the current drawing that are extracted, as "Select objects in the current drawing"
        /// takes them; null for every object of the drawing.
        /// </summary>
        public List<string> SelectedHandles { get; set; }

        /// <summary>Gets or sets whether objects inside blocks are extracted too ("Extract objects from blocks"); true as the wizard opens.</summary>
        public bool ExtractFromBlocks { get; set; } = true;

        /// <summary>Gets or sets whether objects inside external references are extracted ("Extract objects from xrefs"). Stored; an xref's own objects are not read.</summary>
        public bool ExtractFromXrefs { get; set; } = true;

        /// <summary>Gets or sets whether external references count in block counts ("Include xrefs in block counts").</summary>
        public bool IncludeXrefsInCounts { get; set; } = true;

        /// <summary>Gets or sets whether only the objects of model space are extracted ("Objects in model space") rather than every object of the drawing.</summary>
        public bool ModelSpaceOnly { get; set; }

        /// <summary>Gets or sets which object types the wizard lists.</summary>
        public CadExtractionDisplay Display { get; set; }

        /// <summary>Gets or sets whether blocks without attributes are left out of the list ("Display blocks with attributes only").</summary>
        public bool BlocksWithAttributesOnly { get; set; }

        /// <summary>Gets or sets whether only the types the drawing holds are listed ("Display objects currently in-use only").</summary>
        public bool UsedOnly { get; set; }

        /// <summary>Gets the object types extracted, as the page lists them (Line, Circle, a block's name); null for every type.</summary>
        public List<string> SelectedTypes { get; set; }

        /// <summary>Gets the categories left out of the property list (Category filter): Attribute, Drawing, General, Geometry, Misc, Dynamic Block.</summary>
        public List<string> UnselectedCategories { get; } = new List<string>();

        /// <summary>Gets the properties extracted, by key (<c>General/Layer</c>), in the order of the columns; null for every property.</summary>
        public List<string> SelectedProperties { get; set; }

        /// <summary>Gets the names the columns are renamed to, by the column's key (<c>Count</c>, <c>Name</c> or a property's key).</summary>
        public Dictionary<string, string> ColumnNames { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Gets or sets whether rows alike are made one with the number of them in Count ("Combine identical rows"); true as the wizard opens.</summary>
        public bool CombineIdenticalRows { get; set; } = true;

        /// <summary>Gets or sets whether the Count column is shown; true as the wizard opens.</summary>
        public bool ShowCount { get; set; } = true;

        /// <summary>Gets or sets whether the Name column (the object's type or block name) is shown; true as the wizard opens.</summary>
        public bool ShowName { get; set; } = true;

        /// <summary>Gets or sets the order of the columns, by key; null for Count, Name and then the properties as chosen.</summary>
        public List<string> ColumnOrder { get; set; }

        /// <summary>Gets the sorts, the first the main one.</summary>
        public List<CadExtractionSort> Sorts { get; } = new List<CadExtractionSort>();

        /// <summary>Gets the filters, all of which a row meets to be kept.</summary>
        public List<CadExtractionFilter> Filters { get; } = new List<CadExtractionFilter>();

        /// <summary>Gets or sets whether the extraction is inserted as a table in the drawing; true as the wizard opens.</summary>
        public bool InsertTable { get; set; } = true;

        /// <summary>Gets or sets whether the extraction is written to a file.</summary>
        public bool OutputToFile { get; set; }

        /// <summary>Gets or sets the output file, .xlsx, .csv or .txt (tab delimited).</summary>
        public string OutputFile { get; set; } = string.Empty;

        /// <summary>Gets or sets the table style of the inserted table; empty for the current one.</summary>
        public string TableStyle { get; set; } = string.Empty;

        /// <summary>Gets or sets the title written in the first row of the table; empty for a title row of no words.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Gets or sets whether a column renamed is headed with its new name and the property's name under it ("Use property names as additional column headers").</summary>
        public bool UsePropertyNamesAsHeaders { get; set; }

        /// <summary>Gives the settings of the same values, a deep copy.</summary>
        /// <returns>The copy.</returns>
        public CadExtractionSettings Clone() => FromXml(ToXml().ToString());

        /// <summary>Writes the settings as the presenter's .dxe file.</summary>
        /// <param name="path">The file; one there is written over.</param>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
        public void Save(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            File.WriteAllText(path, ToXml().ToString(), new UTF8Encoding(false));
        }

        /// <summary>Reads a .dxe file of the presenter's.</summary>
        /// <param name="path">The file.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="InvalidDataException">Thrown when the file is no .dxe of the presenter's, as one AutoCAD wrote is not.</exception>
        public static CadExtractionSettings Load(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            return FromXml(File.ReadAllText(path, Encoding.UTF8));
        }

        /// <summary>Reads the settings from their XML.</summary>
        /// <param name="xml">The text.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the text is no data extraction file of the presenter's.</exception>
        public static CadExtractionSettings FromXml(string xml)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            XElement root;

            try
            {
                root = XDocument.Parse(xml).Root;
            }
            catch (XmlException exception)
            {
                throw new InvalidDataException("The file is not a data extraction file.", exception);
            }

            if (root == null || root.Name.LocalName != "GeometryHelperDataExtraction")
            {
                throw new InvalidDataException(
                    "The file is not a data extraction file of this program: a .dxe that AutoCAD wrote is not read here."
                );
            }

            var settings = new CadExtractionSettings();
            XElement source = root.Element("Source");

            if (source != null)
            {
                settings.IncludeCurrentDrawing = Flag(source, "currentDrawing", true);
                settings.ExtractFromBlocks = Flag(source, "extractFromBlocks", true);
                settings.ExtractFromXrefs = Flag(source, "extractFromXrefs", true);
                settings.IncludeXrefsInCounts = Flag(source, "includeXrefsInCounts", true);
                settings.ModelSpaceOnly = Flag(source, "modelSpaceOnly", false);
                settings.IncludeSubfolders = Flag(source, "includeSubfolders", false);
                settings.Files.AddRange(
                    source
                        .Elements("File")
                        .Select(e => (string)e.Attribute("path"))
                        .Where(p => !string.IsNullOrEmpty(p))
                );
                settings.Folders.AddRange(
                    source
                        .Elements("Folder")
                        .Select(e => (string)e.Attribute("path"))
                        .Where(p => !string.IsNullOrEmpty(p))
                );

                if (Flag(source, "selected", false))
                {
                    settings.SelectedHandles = source
                        .Elements("Object")
                        .Select(e => (string)e.Attribute("handle"))
                        .Where(h => !string.IsNullOrEmpty(h))
                        .ToList();
                }
            }

            XElement objects = root.Element("Objects");

            if (objects != null)
            {
                settings.Display = Enum.TryParse((string)objects.Attribute("display"), out CadExtractionDisplay display)
                    ? display
                    : CadExtractionDisplay.All;
                settings.BlocksWithAttributesOnly = Flag(objects, "attributesOnly", false);
                settings.UsedOnly = Flag(objects, "usedOnly", false);

                if (!Flag(objects, "allTypes", true))
                {
                    settings.SelectedTypes = objects
                        .Elements("Type")
                        .Select(e => (string)e.Attribute("name"))
                        .Where(n => n != null)
                        .ToList();
                }
            }

            XElement properties = root.Element("Properties");

            if (properties != null)
            {
                settings.UnselectedCategories.AddRange(
                    properties
                        .Elements("UnselectedCategory")
                        .Select(e => (string)e.Attribute("name"))
                        .Where(n => n != null)
                );

                if (!Flag(properties, "all", true))
                {
                    settings.SelectedProperties = properties
                        .Elements("Property")
                        .Select(e => (string)e.Attribute("key"))
                        .Where(k => k != null)
                        .ToList();
                }

                foreach (XElement named in properties.Elements("Property").Concat(properties.Elements("Column")))
                {
                    string key = (string)named.Attribute("key");
                    string alias = (string)named.Attribute("alias");

                    if (key != null && !string.IsNullOrEmpty(alias))
                    {
                        settings.ColumnNames[key] = alias;
                    }
                }
            }

            XElement refine = root.Element("Refine");

            if (refine != null)
            {
                settings.CombineIdenticalRows = Flag(refine, "combine", true);
                settings.ShowCount = Flag(refine, "showCount", true);
                settings.ShowName = Flag(refine, "showName", true);
                settings.UsePropertyNamesAsHeaders = Flag(refine, "propertyHeaders", false);

                if (refine.Element("Order") != null)
                {
                    settings.ColumnOrder = refine
                        .Element("Order")
                        .Elements("Column")
                        .Select(e => (string)e.Attribute("key"))
                        .Where(k => k != null)
                        .ToList();
                }

                foreach (XElement sort in refine.Elements("Sort"))
                {
                    settings.Sorts.Add(
                        new CadExtractionSort(
                            (string)sort.Attribute("column") ?? string.Empty,
                            Flag(sort, "descending", false)
                        )
                    );
                }

                foreach (XElement filter in refine.Elements("Filter"))
                {
                    if (Enum.TryParse((string)filter.Attribute("condition"), out CadFilterCondition condition))
                    {
                        settings.Filters.Add(
                            new CadExtractionFilter(
                                (string)filter.Attribute("column") ?? string.Empty,
                                condition,
                                (string)filter.Attribute("value"),
                                (string)filter.Attribute("second")
                            )
                        );
                    }
                }
            }

            XElement output = root.Element("Output");

            if (output != null)
            {
                settings.InsertTable = Flag(output, "insertTable", true);
                settings.OutputToFile = Flag(output, "toFile", false);
                settings.OutputFile = (string)output.Attribute("file") ?? string.Empty;
                settings.TableStyle = (string)output.Attribute("tableStyle") ?? string.Empty;
                settings.Title = (string)output.Attribute("title") ?? string.Empty;
            }

            return settings;
        }

        /// <summary>The settings as XML, the document of a .dxe file.</summary>
        /// <returns>The document.</returns>
        public XDocument ToXml()
        {
            var source = new XElement(
                "Source",
                new XAttribute("currentDrawing", Text(IncludeCurrentDrawing)),
                new XAttribute("extractFromBlocks", Text(ExtractFromBlocks)),
                new XAttribute("extractFromXrefs", Text(ExtractFromXrefs)),
                new XAttribute("includeXrefsInCounts", Text(IncludeXrefsInCounts)),
                new XAttribute("modelSpaceOnly", Text(ModelSpaceOnly)),
                new XAttribute("includeSubfolders", Text(IncludeSubfolders)),
                new XAttribute("selected", Text(SelectedHandles != null)),
                Files.Select(f => new XElement("File", new XAttribute("path", f))),
                Folders.Select(f => new XElement("Folder", new XAttribute("path", f))),
                (SelectedHandles ?? new List<string>()).Select(h => new XElement("Object", new XAttribute("handle", h)))
            );
            var objects = new XElement(
                "Objects",
                new XAttribute("display", Display.ToString()),
                new XAttribute("attributesOnly", Text(BlocksWithAttributesOnly)),
                new XAttribute("usedOnly", Text(UsedOnly)),
                new XAttribute("allTypes", Text(SelectedTypes == null)),
                (SelectedTypes ?? new List<string>()).Select(t => new XElement("Type", new XAttribute("name", t)))
            );
            var properties = new XElement(
                "Properties",
                new XAttribute("all", Text(SelectedProperties == null)),
                UnselectedCategories.Select(c => new XElement("UnselectedCategory", new XAttribute("name", c))),
                (SelectedProperties ?? new List<string>()).Select(k =>
                    ColumnNames.TryGetValue(k, out string alias)
                        ? new XElement("Property", new XAttribute("key", k), new XAttribute("alias", alias))
                        : new XElement("Property", new XAttribute("key", k))
                ),
                ColumnNames
                    .Where(n => SelectedProperties == null || !SelectedProperties.Contains(n.Key))
                    .OrderBy(n => n.Key, StringComparer.Ordinal)
                    .Select(n => new XElement("Column", new XAttribute("key", n.Key), new XAttribute("alias", n.Value)))
            );
            var refine = new XElement(
                "Refine",
                new XAttribute("combine", Text(CombineIdenticalRows)),
                new XAttribute("showCount", Text(ShowCount)),
                new XAttribute("showName", Text(ShowName)),
                new XAttribute("propertyHeaders", Text(UsePropertyNamesAsHeaders)),
                ColumnOrder == null
                    ? null
                    : new XElement("Order", ColumnOrder.Select(k => new XElement("Column", new XAttribute("key", k)))),
                Sorts.Select(s => new XElement(
                    "Sort",
                    new XAttribute("column", s.Column),
                    new XAttribute("descending", Text(s.Descending))
                )),
                Filters.Select(f => new XElement(
                    "Filter",
                    new XAttribute("column", f.Column),
                    new XAttribute("condition", f.Condition.ToString()),
                    new XAttribute("value", f.Value),
                    new XAttribute("second", f.Second)
                ))
            );
            var output = new XElement(
                "Output",
                new XAttribute("insertTable", Text(InsertTable)),
                new XAttribute("toFile", Text(OutputToFile)),
                new XAttribute("file", OutputFile ?? string.Empty),
                new XAttribute("tableStyle", TableStyle ?? string.Empty),
                new XAttribute("title", Title ?? string.Empty)
            );
            return new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(
                    "GeometryHelperDataExtraction",
                    new XAttribute("version", "1"),
                    source,
                    objects,
                    properties,
                    refine,
                    output
                )
            );
        }

        private static string Text(bool value) => value ? "true" : "false";

        private static bool Flag(XElement element, string name, bool otherwise)
        {
            string text = (string)element.Attribute(name);
            return text == null
                ? otherwise
                : string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1";
        }
    }
}
