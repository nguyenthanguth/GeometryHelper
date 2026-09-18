using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.IfcConvert.Core.Internal;
using Xbim.Common.Geometry;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.Interfaces;

namespace GeometryHelper.IfcConvert.Converters.Internal
{
    /// <summary>
    /// Subtracts openings (<c>IfcRelVoidsElement</c>) from host bodies on the exact B-rep geometry in the
    /// xBIM/OpenCascade engine, before any tessellation.
    /// <para>
    /// Cutting after conversion, on <c>GeoSolid3</c> meshes, is unreliable once either body is curved:
    /// a round opening arrives as hundreds of triangles and the mesh boolean returns an open body with
    /// the wrong volume. OpenCascade cuts the exact cylinder instead, as xBIM itself does.
    /// </para>
    /// </summary>
    internal static class VoidConvert
    {
        /// <summary>
        /// Builds the cutting solids of all openings of a host element, in the host's local frame and model units.
        /// </summary>
        /// <param name="element">The host element.</param>
        /// <param name="hostPlacement">The host's global placement matrix (model units).</param>
        /// <param name="cutters">The opening solids when the method returns true; the caller disposes them.</param>
        /// <returns>false when any opening cannot be expressed as engine solids (e.g. mapped representations).</returns>
        public static bool TryCollectCutters(IIfcElement element, XbimMatrix3D hostPlacement, out List<IXbimSolid> cutters)
        {
            cutters = new List<IXbimSolid>();

            XbimMatrix3D globalToHost = hostPlacement;
            globalToHost.Invert();

            try
            {
                foreach (IIfcRelVoidsElement rel in element.HasOpenings)
                {
                    if (!(rel?.RelatedOpeningElement is IIfcProduct opening) ||
                        opening.Representation == null || opening.ObjectPlacement == null)
                    {
                        continue;
                    }

                    // Row-vector convention: host-local = local * openingPlacement * inverse(hostPlacement).
                    XbimMatrix3D openingToHost = XbimMatrix3D.Multiply(opening.ObjectPlacement.ToMatrix3D(), globalToHost);

                    foreach (IIfcRepresentation representation in ProductConvert.SelectBodyRepresentations(opening.Representation.Representations))
                    {
                        foreach (IIfcRepresentationItem item in representation.Items)
                        {
                            if (!(item is IIfcGeometricRepresentationItem geometricItem))
                            {
                                DisposeAll(cutters);
                                cutters = null;
                                return false;
                            }

                            using (IXbimGeometryObject local = IfcEngineContext.CurrentEngine.Create(geometricItem))
                            {
                                if (local == null)
                                {
                                    continue;
                                }

                                IXbimGeometryObject placed = local.Transform(openingToHost);
                                cutters.AddRange(EnumerateSolids(placed));
                            }
                        }
                    }
                }
            }
            catch
            {
                DisposeAll(cutters);
                cutters = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cuts every cutter from a host geometry object.
        /// </summary>
        /// <param name="host">The host geometry, in the same frame as the cutters.</param>
        /// <param name="cutters">The opening solids.</param>
        /// <param name="precision">Boolean precision in model units.</param>
        /// <param name="result">The remaining solids when the method returns true.</param>
        /// <returns>false when the engine fails, so the caller can fall back to another strategy.</returns>
        public static bool TryCut(IXbimGeometryObject host, IReadOnlyList<IXbimSolid> cutters, double precision, out List<IXbimSolid> result)
        {
            result = EnumerateSolids(host).ToList();

            try
            {
                foreach (IXbimSolid cutter in cutters)
                {
                    List<IXbimSolid> next = new List<IXbimSolid>(result.Count);
                    foreach (IXbimSolid solid in result)
                    {
                        next.AddRange(solid.Cut(cutter, precision).Where(s => s != null && s.IsValid));
                    }

                    result = next;
                }

                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Lists the solids contained in any engine geometry object.
        /// </summary>
        public static IEnumerable<IXbimSolid> EnumerateSolids(IXbimGeometryObject geometry)
        {
            switch (geometry)
            {
                case IXbimSolid solid:
                    return new[] { solid };
                case IXbimSolidSet solidSet:
                    return solidSet.ToList();
                case IXbimGeometryObjectSet objectSet:
                    return objectSet.Solids.ToList();
                default:
                    return Enumerable.Empty<IXbimSolid>();
            }
        }

        public static void DisposeAll(IEnumerable<IXbimSolid> solids)
        {
            if (solids == null)
            {
                return;
            }

            foreach (IXbimSolid solid in solids)
            {
                try { solid?.Dispose(); } catch { }
            }
        }
    }
}
