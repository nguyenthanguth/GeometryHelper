using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GeometryHelper;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;
using Tekla.Structures.Model;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Reads the objects of Tekla IFC reference models into <see cref="GeoSolid3"/> bodies, in the current work plane.
    /// <para>
    /// A reference object carries the GlobalId of its IFC product in the <c>EXTERNAL.GUID</c> report property, and
    /// that GlobalId is what GeometryHelper.IfcConvert looks products up by. Objects are grouped by the reference
    /// model they belong to rather than by file: one IFC file inserted twice is placed twice, while it is parsed
    /// once. Placement, scale and the default options are those of <see cref="ReferenceModelConvert"/>.
    /// </para>
    /// <para>
    /// An object with no GlobalId, one its file does not hold, one that cannot be converted, and every object of a
    /// reference model that is not IFC or cannot be read, are left out rather than thrown on, and written to
    /// <see cref="GeometryHelperLog"/>. An object given twice is returned once, and the rest keep the order they were
    /// given in.
    /// </para>
    /// </summary>
    public static class ReferenceModelObjectConvert
    {
        /// <summary>
        /// Gets the IFC GlobalId of a reference object, from its <c>EXTERNAL.GUID</c> report property.
        /// </summary>
        /// <param name="referenceObject">The reference object.</param>
        /// <returns>The GlobalId, or null when the object has none (an object of a DWG reference model, for one).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceObject"/> is null.</exception>
        public static string GetIfcGuid(this ReferenceModelObject referenceObject)
        {
            if (referenceObject == null)
            {
                throw new ArgumentNullException(nameof(referenceObject));
            }

            string guid = string.Empty;

            return referenceObject.GetReportProperty("EXTERNAL.GUID", ref guid) && !string.IsNullOrWhiteSpace(guid)
                ? guid.Trim()
                : null;
        }

        /// <summary>
        /// Converts one reference object into bodies in the current work plane.
        /// </summary>
        /// <param name="referenceObject">The reference object.</param>
        /// <param name="options">The conversion settings; see <see cref="ReferenceModelConvert.CreateIfcConvertOptions"/>.</param>
        /// <returns>The bodies of the object's IFC product; empty when it cannot be converted.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceObject"/> is null.</exception>
        public static GeoSolid3[] ToGeoSolids(this ReferenceModelObject referenceObject, IfcConvertOptions options = null)
        {
            if (referenceObject == null)
            {
                throw new ArgumentNullException(nameof(referenceObject));
            }

            return ToGeoSolids(new[] { referenceObject }, options);
        }

        /// <summary>
        /// Converts reference objects, of any number of reference models and IFC files, into bodies in the current
        /// work plane.
        /// </summary>
        /// <param name="referenceObjects">The reference objects, for example those selected in the model.</param>
        /// <param name="options">The conversion settings; see <see cref="ReferenceModelConvert.CreateIfcConvertOptions"/>.</param>
        /// <returns>The bodies that could be read. An IFC product can have several; see <see cref="ToIfcGeometries"/> to tell whose they are.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceObjects"/> is null.</exception>
        public static GeoSolid3[] ToGeoSolids(this IEnumerable<ReferenceModelObject> referenceObjects, IfcConvertOptions options = null)
        {
            return ToIfcGeometries(referenceObjects, options).SelectMany(g => g.Solids).ToArray();
        }

        /// <summary>
        /// Converts reference objects into the whole geometry of their IFC products, in the current work plane:
        /// bodies and open surfaces, with the GlobalId, name, type and conversion warnings of each product.
        /// </summary>
        /// <param name="referenceObjects">The reference objects, for example those selected in the model.</param>
        /// <param name="options">The conversion settings; see <see cref="ReferenceModelConvert.CreateIfcConvertOptions"/>.</param>
        /// <returns>
        /// One entry per product that could be read, in the order the objects were given; an object given twice comes
        /// back once. A product with no body of its own comes back empty (<see cref="IfcProductGeometry.IsEmpty"/>)
        /// rather than being left out.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="referenceObjects"/> is null.</exception>
        /// <remarks>
        /// <see cref="IfcProductGeometry.GlobalId"/> leads back to the Tekla object through
        /// <c>ReferenceModel.GetReferenceModelObjectByExternalGuid</c>, and <see cref="IfcProductGeometry.Placement"/>
        /// is the product's own frame, carried into the current work plane as well.
        /// </remarks>
        public static IReadOnlyList<IfcProductGeometry> ToIfcGeometries(this IEnumerable<ReferenceModelObject> referenceObjects, IfcConvertOptions options = null)
        {
            if (referenceObjects == null)
            {
                throw new ArgumentNullException(nameof(referenceObjects));
            }

            // Tekla first: the reference model and GlobalId of every object, in the order given.
            List<(int ReferenceModelId, string Guid)> requests = new List<(int ReferenceModelId, string Guid)>();
            List<ReferenceModel> referenceModels = new List<ReferenceModel>();
            int objectCount = 0;
            int withoutReferenceModel = 0;
            int withoutGuid = 0;

            foreach (ReferenceModelObject referenceObject in referenceObjects)
            {
                if (referenceObject == null)
                {
                    continue;
                }

                objectCount++;

                try
                {
                    ReferenceModel referenceModel = referenceObject.GetReferenceModel();
                    if (referenceModel == null)
                    {
                        withoutReferenceModel++;
                        continue;
                    }

                    string guid = referenceObject.GetIfcGuid();
                    if (guid == null)
                    {
                        withoutGuid++;
                        continue;
                    }

                    requests.Add((referenceModel.Identifier.ID, guid));
                    referenceModels.Add(referenceModel);
                }
                catch (Exception exception)
                {
                    // An object Tekla cannot describe is left out.
                    GeometryHelperLog.Warn($"Reference object {referenceObject.Identifier.ID} could not be read from Tekla; it is left out.", exception);
                }
            }

            // Counted rather than written one by one: a selection in a DWG reference model can hold thousands of these.
            if (withoutReferenceModel > 0)
            {
                GeometryHelperLog.Debug($"{withoutReferenceModel} reference object(s) have no reference model; they are left out.");
            }

            if (withoutGuid > 0)
            {
                GeometryHelperLog.Debug($"{withoutGuid} reference object(s) have no IFC GlobalId (EXTERNAL.GUID), as the objects of a reference model that is not IFC do; they are left out.");
            }

            Dictionary<int, ReferenceModelConvert.Frame> frames = ReferenceModelConvert.ReadFrames(referenceModels, options, false);

            // Then the IFC files, which need nothing more from Tekla.
            List<IfcProductGeometry> geometries = ConvertRequests(requests, frames);
            GeometryHelperLog.Info($"ReferenceModelObjectConvert: {geometries.Count} IFC products from {objectCount} reference objects.");
            return geometries;
        }

        /// <summary>
        /// Converts the product with each GlobalId in the file of its reference model and carries it into the work
        /// plane. Requests whose reference model has no frame, whose file cannot be read, or whose product is missing
        /// or fails, are left out; a request repeated for the same reference model is served once.
        /// </summary>
        internal static List<IfcProductGeometry> ConvertRequests(
            IEnumerable<(int ReferenceModelId, string Guid)> requests,
            IReadOnlyDictionary<int, ReferenceModelConvert.Frame> frames)
        {
            // Each object once, in the order given, its file opened when first asked for.
            var work = new List<(string Guid, ReferenceModelConvert.Frame Frame, IfcStoreCache Cache)>();
            HashSet<(int, string)> done = new HashSet<(int, string)>();
            Dictionary<int, IfcStoreCache> caches = new Dictionary<int, IfcStoreCache>();

            foreach ((int referenceModelId, string guid) in requests)
            {
                if (guid == null
                    || !frames.TryGetValue(referenceModelId, out ReferenceModelConvert.Frame frame)
                    || !done.Add((referenceModelId, guid)))
                {
                    continue;
                }

                if (!caches.TryGetValue(referenceModelId, out IfcStoreCache cache))
                {
                    cache = ReferenceModelConvert.TryOpen(frame.IfcFilePath);
                    caches[referenceModelId] = cache;
                }

                if (cache != null)
                {
                    work.Add((guid, frame, cache));
                }
            }

            // The objects do not depend on one another, so those of a file read into memory are converted and carried
            // into the work plane on every core at once, and gathered back in the order given. A file xBIM keeps on
            // disk is read one object at a time. Each is converted under the tolerance in force here, a scope the
            // caller opened included, which the other threads would not see otherwise.
            var converted = new IfcProductGeometry[work.Count];
            Tolerance tolerance = Tolerance.Global;

            Parallel.For(0, work.Count, i =>
            {
                if (work[i].Cache.IsInMemory)
                {
                    using (Tolerance.Use(tolerance))
                    {
                        converted[i] = ConvertOne(work[i].Guid, work[i].Frame, work[i].Cache);
                    }
                }
            });

            for (int i = 0; i < work.Count; i++)
            {
                if (!work[i].Cache.IsInMemory)
                {
                    converted[i] = ConvertOne(work[i].Guid, work[i].Frame, work[i].Cache);
                }
            }

            return converted.Where(geometry => geometry != null).ToList();
        }

        /// <summary>
        /// Converts the product with one GlobalId and carries it into the work plane; null, and logged, when the file
        /// does not hold it or it fails.
        /// </summary>
        private static IfcProductGeometry ConvertOne(string guid, ReferenceModelConvert.Frame frame, IfcStoreCache cache)
        {
            IfcProductGeometry geometry;
            try
            {
                geometry = cache.GetGeometry(guid, frame.Options);
            }
            catch (Exception exception)
            {
                GeometryHelperLog.Warn($"{guid} in '{frame.IfcFilePath}' could not be converted; it is left out.", exception);
                return null;
            }

            if (geometry == null)
            {
                GeometryHelperLog.Debug($"{guid} is not in '{Path.GetFileName(frame.IfcFilePath)}'; it is left out.");
                return null;
            }

            return ReferenceModelConvert.TransformGeometry(geometry, frame.IfcToWorkPlane, frame.Options.Tolerance);
        }
    }
}
