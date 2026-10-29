using System.Linq;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// What AutoCAD's <c>Database</c> keeps in the extension dictionaries of model space and of the layer table: the
    /// drawing's geographic location, its layer filters and its layer states.
    /// </summary>
    public sealed partial class DxfDatabase
    {
        private DxfLayerStateManager _layerStateManager;

        /// <summary>
        /// Gets the geographic location of the drawing, as AutoCAD's <c>GeoDataObject</c> gives its id: the GEODATA model
        /// space's extension dictionary names ACAD_GEOGRAPHICDATA; null where the drawing holds none, as one never placed on the
        /// earth. The version of the file is not asked: one older than R2010 holding a GEODATA gives it, though
        /// <see cref="DxfGeoData.PostToDb"/> adds none to a file older than R2010.
        /// </summary>
        public DxfGeoData GeoDataObject
        {
            get
            {
                DxfDBDictionary extension = ModelSpace?.ExtensionDictionary;
                return extension != null && extension.Contains("ACAD_GEOGRAPHICDATA") ? extension.GetAt("ACAD_GEOGRAPHICDATA") as DxfGeoData : null;
            }
        }

        /// <summary>
        /// Gets the layer filters of the drawing, as AutoCAD's <c>LayerFilters</c>: the filter All at the root, and nested in
        /// it the filters of the dictionaries ACLYDICTIONARY and ACAD_LAYERFILTERS of the layer table's extension dictionary and
        /// the three AutoCAD makes, as <see cref="DxfLayerFilter"/> says; a new tree on each call, as the drawing has them then.
        /// </summary>
        /// <remarks>AutoCAD sets a tree back to change the filters; here they are read only, as <see cref="DxfLayerFilter"/> says.</remarks>
        public DxfLayerFilterTree LayerFilters
        {
            get
            {
                var root = new DxfLayerFilter("All", null, true);
                DxfDBDictionary extension = LayerTable.ExtensionDictionary;

                // AutoCAD 2005 and later: property filters and groups, each an XRECORD named by its 300.
                if (extension != null && extension.Contains("ACLYDICTIONARY") && extension.GetAt("ACLYDICTIONARY") is DxfDBDictionary kept)
                {
                    foreach (DxfDBDictionaryEntry entry in kept)
                    {
                        string name = entry.Value is DxfXrecord record ? record.Data.Where(g => g.TypeCode == 300).Select(g => g.Value).FirstOrDefault() : null;

                        if (!string.IsNullOrEmpty(name) && !root.Nests(name))
                        {
                            root.Nest(name);
                        }
                    }
                }

                if (extension != null && extension.Contains("ACAD_LAYERFILTERS") && extension.GetAt("ACAD_LAYERFILTERS") is DxfDBDictionary filters)
                {
                    foreach (DxfDBDictionaryEntry entry in filters)
                    {
                        if (!root.Nests(entry.Key))
                        {
                            root.Nest(entry.Key);
                        }
                    }
                }

                foreach (string made in new[] { "All Used Layers", "Unreconciled New Layers", "Viewport Overrides" })
                {
                    root.Nest(made, true);
                }

                return new DxfLayerFilterTree(root);
            }
        }

        /// <summary>
        /// Gets the layer states of the drawing, as AutoCAD's <c>LayerStateManager</c>: the states of the dictionary
        /// ACAD_LAYERSTATES of the layer table's extension dictionary, read and listed as <see cref="DxfLayerStateManager"/>
        /// says; the same manager each time.
        /// </summary>
        public DxfLayerStateManager LayerStateManager => _layerStateManager ?? (_layerStateManager = new DxfLayerStateManager(this));
    }
}
