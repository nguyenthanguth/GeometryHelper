using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.SolidGeometry.Geometry;
using Tekla.Structures.Model;
using TSG = Tekla.Structures.Geometry3d;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Reads the IFC file behind a Tekla reference model into <see cref="GeoSolid3"/> bodies, or into the whole
    /// <see cref="IfcProductGeometry"/> of each product, placed where Tekla shows the reference model.
    /// <para>
    /// Tekla draws an IFC reference model by taking the coordinates of the file, scaling them by the reference
    /// model's <see cref="ReferenceModel.Scale"/> and carrying them to where the model was inserted. The same steps
    /// are taken here: GeometryHelper.IfcConvert reads the file in millimetres at that scale, and the frame the
    /// reference model reports through <c>GetCoordinateSystem()</c> places the result.
    /// </para>
    /// <para>
    /// Bodies come back in the current work plane, like every other coordinate the Tekla API hands out
    /// (<c>Part.GetSolid()</c> included), so they line up with what the caller already works with. Reading the
    /// frames needs the work plane switched to global for a moment; it is restored before any IFC file is read.
    /// </para>
    /// <para>
    /// A reference model that is not IFC (DWG, SKP, ...), a file that cannot be read and a product that cannot be
    /// converted are left out rather than thrown on, because one bad file should not cost the others; what was left
    /// out, and why, is written to <see cref="GeometryHelperLog"/>. Each IFC file is parsed once and then served from
    /// <see cref="IfcStoreCache"/> for the life of the process.
    /// </para>
    /// </summary>
    public static class ReferenceModelConvert
    {
        private static readonly string[] IfcExtensions = { ".ifc", ".ifczip", ".ifcxml" };

        /// <summary>
        /// Gets the full path of the IFC file a reference model currently shows, that is its active revision.
        /// </summary>
        /// <param name="referenceModel">The reference model.</param>
        /// <returns>The full path, or null when the reference model shows no IFC file or its path cannot be resolved.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        /// <remarks>
        /// Tekla gives the path relative to the model folder, while the global cache of GeometryHelper.IfcConvert
        /// (<see cref="IfcStoreCache.GetOrCreate(string)"/>) takes full paths only.
        /// </remarks>
        public static string GetIfcFilePath(this ReferenceModel referenceModel)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            return ResolveIfcFilePath(ConnectedModel().GetInfo().ModelPath, referenceModel.ActiveFilePath);
        }

        /// <summary>
        /// Gets the transformation that carries the IFC geometry of a reference model into Tekla global coordinates.
        /// </summary>
        /// <param name="referenceModel">The reference model.</param>
        /// <returns>The transformation from the coordinates of the IFC file to global coordinates.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        /// <remarks>
        /// It places geometry read with the options of <see cref="CreateIfcConvertOptions"/>, which are already in
        /// millimetres and at the reference model's scale; the transformation itself only moves and turns.
        /// </remarks>
        public static GeoTransform3 GetIfcToGlobal(this ReferenceModel referenceModel)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            TSG.CoordinateSystem frame = ReadFrameInGlobal(referenceModel, ConnectedModel().GetWorkPlaneHandler());
            return GeoTransform3.FromCoordinateSystem(frame.ToGeoCoordinateSystem3());
        }

        /// <summary>
        /// Gets the transformation that carries the IFC geometry of a reference model into the current work plane.
        /// </summary>
        /// <param name="referenceModel">The reference model.</param>
        /// <returns>The transformation from the coordinates of the IFC file to the current work plane.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        /// <remarks>
        /// It places geometry read with the options of <see cref="CreateIfcConvertOptions"/>. The work plane is the
        /// one current when the method is called; it is changed for a moment and then restored.
        /// </remarks>
        public static GeoTransform3 GetIfcToWorkPlane(this ReferenceModel referenceModel)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            WorkPlaneHandler workPlaneHandler = ConnectedModel().GetWorkPlaneHandler();
            TSG.Matrix globalToWorkPlane = workPlaneHandler.GetCurrentTransformationPlane().TransformationMatrixToLocal;

            return ComposeIfcToWorkPlane(ReadFrameInGlobal(referenceModel, workPlaneHandler), globalToWorkPlane);
        }

        /// <summary>
        /// Creates the options for reading the IFC file of a reference model: millimetres, the reference model's
        /// scale and global IFC coordinates, with every other setting taken from <paramref name="options"/>.
        /// </summary>
        /// <param name="referenceModel">The reference model whose scale is used.</param>
        /// <param name="options">
        /// The settings to start from, which are copied and left unchanged. Null returns the parts of an assembly
        /// (<see cref="IfcConvertOptions.IncludeAggregatedParts"/>), as Tekla selects them, and leaves openings uncut
        /// (<see cref="IfcConvertOptions.ApplyVoids"/>) for speed; pass options with <c>ApplyVoids = true</c> for bolt
        /// holes and other openings cut as Tekla shows them.
        /// </param>
        /// <returns>New options for this reference model.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        public static IfcConvertOptions CreateIfcConvertOptions(this ReferenceModel referenceModel, IfcConvertOptions options = null)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            return CreateOptions(referenceModel.Scale, options, false);
        }

        /// <summary>
        /// Converts every physical product of the IFC file of a reference model, in the current work plane.
        /// </summary>
        /// <param name="referenceModel">The reference model.</param>
        /// <param name="options">The conversion settings; see <see cref="CreateIfcConvertOptions"/>.</param>
        /// <returns>The bodies that could be read; empty when the file cannot be read or is not IFC.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        public static GeoSolid3[] ToGeoSolids(this ReferenceModel referenceModel, IfcConvertOptions options = null)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            return ToGeoSolids(new[] { referenceModel }, options);
        }

        /// <summary>
        /// Converts every physical product of the IFC files of several reference models, in the current work plane.
        /// </summary>
        /// <param name="referenceModels">The reference models. Null entries and repeats are skipped.</param>
        /// <param name="options">The conversion settings; see <see cref="CreateIfcConvertOptions"/>.</param>
        /// <returns>
        /// The bodies that could be read, reference model by reference model. See
        /// <see cref="ToIfcGeometries(IEnumerable{ReferenceModel}, IfcConvertOptions)"/> to tell whose they are.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModels"/> is null.</exception>
        /// <remarks>
        /// <see cref="IfcConvertOptions.IncludeAggregatedParts"/> is always off here: the whole file is walked, parts
        /// included, so an assembly returning its parts as well would count each of them twice. A file that cannot be
        /// read, or fails part-way, is left out as a whole rather than returned incomplete.
        /// </remarks>
        public static GeoSolid3[] ToGeoSolids(this IEnumerable<ReferenceModel> referenceModels, IfcConvertOptions options = null)
        {
            return ToIfcGeometries(referenceModels, options).SelectMany(p => p.Solids).ToArray();
        }

        /// <summary>
        /// Converts every physical product of the IFC file of a reference model into its whole geometry, in the
        /// current work plane: bodies and open surfaces, with the GlobalId, name, type and conversion warnings of each
        /// product.
        /// </summary>
        /// <param name="referenceModel">The reference model.</param>
        /// <param name="options">The conversion settings; see <see cref="CreateIfcConvertOptions"/>.</param>
        /// <returns>One entry per physical product; empty when the file cannot be read or is not IFC.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModel"/> is null.</exception>
        public static IReadOnlyList<IfcProductGeometry> ToIfcGeometries(this ReferenceModel referenceModel, IfcConvertOptions options = null)
        {
            if (referenceModel == null)
            {
                throw new ArgumentNullException(nameof(referenceModel));
            }

            return ToIfcGeometries(new[] { referenceModel }, options);
        }

        /// <summary>
        /// Converts every physical product of the IFC files of several reference models into its whole geometry, in
        /// the current work plane: bodies and open surfaces, with the GlobalId, name, type and conversion warnings of each
        /// product.
        /// </summary>
        /// <param name="referenceModels">The reference models. Null entries and repeats are skipped.</param>
        /// <param name="options">The conversion settings; see <see cref="CreateIfcConvertOptions"/>.</param>
        /// <returns>
        /// One entry per physical product that could be read, reference model by reference model in the order given. A
        /// reference model that is not IFC, or whose file cannot be read or fails part-way, adds none.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceModels"/> is null.</exception>
        /// <remarks>
        /// <see cref="IfcConvertOptions.IncludeAggregatedParts"/> is always off here: the whole file is walked, parts
        /// included, so an assembly returning its parts as well would count each of them twice. The same IFC file
        /// inserted twice is parsed once and placed twice, so its products come back twice with the same GlobalId.
        /// <c>ReferenceModel.GetReferenceModelObjectByExternalGuid</c> with the GlobalId leads to the Tekla object.
        /// </remarks>
        public static IReadOnlyList<IfcProductGeometry> ToIfcGeometries(this IEnumerable<ReferenceModel> referenceModels, IfcConvertOptions options = null)
        {
            if (referenceModels == null)
            {
                throw new ArgumentNullException(nameof(referenceModels));
            }

            List<ReferenceModel> models = referenceModels
                .Where(m => m != null)
                .GroupBy(m => m.Identifier.ID)
                .Select(g => g.First())
                .ToList();
            Dictionary<int, Frame> frames = ReadFrames(models, options, true);

            List<IfcProductGeometry> products = new List<IfcProductGeometry>();
            int read = 0;
            foreach (ReferenceModel model in models)
            {
                if (!frames.TryGetValue(model.Identifier.ID, out Frame frame))
                {
                    continue;
                }

                IfcStoreCache cache = TryOpen(frame.IfcFilePath);
                List<IfcProductGeometry> geometries = cache == null ? null : ConvertWholeModel(cache, frame.Options, frame.IfcToWorkPlane);

                if (geometries != null)
                {
                    products.AddRange(geometries);
                    read++;
                }
            }

            GeometryHelperLog.Info($"ReferenceModelConvert: {products.Count} IFC products ({products.Sum(p => p.Solids.Count)} bodies) from {read} of {models.Count} reference models.");
            return products;
        }

        /// <summary>
        /// What reading one reference model's IFC file needs, taken from Tekla once: the file, the options and the
        /// placement. Nothing in it refers to Tekla.Structures.Model, so the IFC side can run and be tested without Tekla.
        /// </summary>
        internal sealed class Frame
        {
            internal Frame(string ifcFilePath, IfcConvertOptions options, GeoTransform3 ifcToWorkPlane)
            {
                IfcFilePath = ifcFilePath;
                Options = options;
                IfcToWorkPlane = ifcToWorkPlane;
            }

            internal string IfcFilePath { get; }

            internal IfcConvertOptions Options { get; }

            internal GeoTransform3 IfcToWorkPlane { get; }
        }

        /// <summary>
        /// Reads the frame of every IFC reference model, keyed by its id, switching the work plane to global once
        /// for all of them. Reference models that are not IFC, or that Tekla cannot describe, are left out.
        /// </summary>
        internal static Dictionary<int, Frame> ReadFrames(IEnumerable<ReferenceModel> referenceModels, IfcConvertOptions options, bool wholeModel)
        {
            Dictionary<int, Frame> frames = new Dictionary<int, Frame>();

            List<ReferenceModel> distinct = referenceModels
                .Where(m => m != null)
                .GroupBy(m => m.Identifier.ID)
                .Select(g => g.First())
                .ToList();

            if (distinct.Count == 0)
            {
                return frames;
            }

            Model model = ConnectedModel();
            string modelPath = model.GetInfo().ModelPath;
            WorkPlaneHandler workPlaneHandler = model.GetWorkPlaneHandler();
            TransformationPlane currentPlane = workPlaneHandler.GetCurrentTransformationPlane();
            TSG.Matrix globalToWorkPlane = currentPlane.TransformationMatrixToLocal;

            try
            {
                // A reference model reports its frame in the work plane current when it is read, so read in global.
                workPlaneHandler.SetCurrentTransformationPlane(new TransformationPlane());

                foreach (ReferenceModel referenceModel in distinct)
                {
                    try
                    {
                        // Selecting again refreshes what the object holds in the plane now current.
                        if (!referenceModel.Select())
                        {
                            GeometryHelperLog.Warn($"Reference model {referenceModel.Identifier.ID} could not be read from Tekla; it is left out.");
                            continue;
                        }

                        string ifcFilePath = ResolveIfcFilePath(modelPath, referenceModel.ActiveFilePath);
                        if (ifcFilePath == null)
                        {
                            GeometryHelperLog.Debug($"Reference model {referenceModel.Identifier.ID} ('{referenceModel.ActiveFilePath}') is not an IFC file; it is left out.");
                            continue;
                        }

                        frames[referenceModel.Identifier.ID] = new Frame(
                            ifcFilePath,
                            CreateOptions(referenceModel.Scale, options, wholeModel),
                            ComposeIfcToWorkPlane(referenceModel.GetCoordinateSystem(), globalToWorkPlane));
                    }
                    catch (Exception exception)
                    {
                        // A reference model Tekla cannot describe is left out; the others are still read.
                        GeometryHelperLog.Warn($"Reference model {referenceModel.Identifier.ID} could not be read from Tekla; it is left out.", exception);
                    }
                }
            }
            finally
            {
                workPlaneHandler.SetCurrentTransformationPlane(currentPlane);
            }

            return frames;
        }

        /// <summary>
        /// Resolves the file of a reference model against the model folder. Null when there is no file, when the
        /// path cannot be resolved, or when the file is not IFC (<c>.ifc</c>, <c>.ifczip</c>, <c>.ifcxml</c>).
        /// </summary>
        internal static string ResolveIfcFilePath(string modelPath, string activeFilePath)
        {
            if (string.IsNullOrWhiteSpace(activeFilePath))
            {
                return null;
            }

            try
            {
                string path = activeFilePath.Trim();

                if (!Path.IsPathRooted(path))
                {
                    // A relative path with no model folder would resolve against whatever folder is current.
                    if (string.IsNullOrWhiteSpace(modelPath))
                    {
                        return null;
                    }

                    path = Path.Combine(modelPath, path);
                }

                path = Path.GetFullPath(path);

                return IfcExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) ? path : null;
            }
            catch (Exception exception)
            {
                // Characters or a length Windows does not accept in a path.
                GeometryHelperLog.Warn($"The path '{activeFilePath}' cannot be resolved against the model folder '{modelPath}'.", exception);
                return null;
            }
        }

        /// <summary>
        /// Copies <paramref name="options"/> (or the defaults for Tekla) and sets what belongs to the reference model:
        /// millimetres, its scale and global IFC coordinates. A whole-model read never adds aggregated parts.
        /// </summary>
        internal static IfcConvertOptions CreateOptions(double scale, IfcConvertOptions options, bool wholeModel)
        {
            IfcConvertOptions result = options != null
                ? options.Clone()
                : new IfcConvertOptions { ApplyVoids = false, IncludeAggregatedParts = true };

            result.TargetUnit = LengthUnit.Millimeters;
            result.ScaleFactor = scale > 0.0 && !double.IsInfinity(scale) ? scale : 1.0;
            result.CoordinateSpace = CoordinateSpace.Global;

            if (wholeModel)
            {
                result.IncludeAggregatedParts = false;
            }

            return result;
        }

        /// <summary>
        /// IFC coordinates to the work plane: first where the reference model sits in global coordinates, then from
        /// global into the work plane (<c>TransformationPlane.TransformationMatrixToLocal</c>).
        /// </summary>
        internal static GeoTransform3 ComposeIfcToWorkPlane(TSG.CoordinateSystem ifcFrameInGlobal, TSG.Matrix globalToWorkPlane)
        {
            GeoTransform3 ifcToGlobal = GeoTransform3.FromCoordinateSystem(ifcFrameInGlobal.ToGeoCoordinateSystem3());
            return globalToWorkPlane.ToGeoTransform3() * ifcToGlobal;
        }

        /// <summary>
        /// Returns a copy of <paramref name="geometry"/> carried by <paramref name="transform"/> through
        /// <see cref="IfcProductGeometry.TransformBy"/>, which rebuilds the faces with the tolerance the file was
        /// converted with, so that a thin face the conversion kept does not cost its whole body. What the
        /// transformation itself collapses is left out, added to the product's warnings and logged. Null, logged, when
        /// the product cannot be carried at all. Cached geometry is never changed, since the copy is made of new objects.
        /// </summary>
        internal static IfcProductGeometry TransformGeometry(IfcProductGeometry geometry, GeoTransform3 transform, Tolerance tolerance)
        {
            IfcProductGeometry moved;
            try
            {
                moved = geometry.TransformBy(transform, tolerance);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn($"{geometry.GlobalId} ({geometry.IfcType}) could not be carried into the work plane; it is left out.", exception);
                return null;
            }

            // TransformBy appends what it left out to the warnings the product already had.
            foreach (string warning in moved.Warnings.Skip(geometry.Warnings.Count))
            {
                GeometryHelperLog.Warn($"{geometry.GlobalId} ({geometry.IfcType}) carried into the work plane: {warning}");
            }

            return moved;
        }

        /// <summary>
        /// Converts every physical product of an opened file and carries it by <paramref name="transform"/>. Null
        /// when the file fails part-way, so that it is left out whole rather than returned incomplete.
        /// </summary>
        internal static List<IfcProductGeometry> ConvertWholeModel(IfcStoreCache cache, IfcConvertOptions options, GeoTransform3 transform)
        {
            List<IfcProductGeometry> geometries = new List<IfcProductGeometry>();

            try
            {
                foreach (IfcProductGeometry geometry in cache.EnumerateGeometries(options))
                {
                    IfcProductGeometry moved = geometry == null ? null : TransformGeometry(geometry, transform, options.Tolerance);
                    if (moved != null)
                    {
                        geometries.Add(moved);
                    }
                }
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn($"Reading every product of '{cache.FilePath}' failed part-way; the file is left out.", exception);
                return null;
            }

            return geometries;
        }

        /// <summary>
        /// Opens an IFC file through the global cache, or returns null when it cannot be read.
        /// </summary>
        internal static IfcStoreCache TryOpen(string ifcFilePath)
        {
            try
            {
                return IfcStoreCache.GetOrCreate(ifcFilePath);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn($"The IFC file '{ifcFilePath}' cannot be read; it is left out.", exception);
                return null;
            }
        }

        private static Model ConnectedModel()
        {
            Model model = new Model();

            if (!model.GetConnectionStatus())
            {
                throw new InvalidOperationException("Tekla Structures is not running, or this process cannot connect to it.");
            }

            return model;
        }

        private static TSG.CoordinateSystem ReadFrameInGlobal(ReferenceModel referenceModel, WorkPlaneHandler workPlaneHandler)
        {
            TransformationPlane currentPlane = workPlaneHandler.GetCurrentTransformationPlane();

            try
            {
                workPlaneHandler.SetCurrentTransformationPlane(new TransformationPlane());
                referenceModel.Select();
                return referenceModel.GetCoordinateSystem();
            }
            finally
            {
                workPlaneHandler.SetCurrentTransformationPlane(currentPlane);
            }
        }
    }
}
