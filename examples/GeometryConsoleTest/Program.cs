using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.Geometry;

namespace GeometryConsoleTest
{
    internal class Program
    {
        // IFC models from the project's TestFiles folder, copied next to the executable (see GeometryConsoleTest.csproj).
        private static readonly string TestFilesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestFiles");

        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // An IFC file or a folder of IFC files can be passed on the command line; otherwise every file in TestFiles is used.
            string source = args.Length > 0 ? args[0] : TestFilesFolder;
            string[] ifcFiles = Directory.Exists(source)
                ? Directory.GetFiles(source, "*.ifc").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray()
                : File.Exists(source) ? new[] { source } : Array.Empty<string>();

            if (ifcFiles.Length == 0)
            {
                Console.WriteLine($"No IFC file found at '{source}'.");
                return 1;
            }

            foreach (string ifcFilePath in ifcFiles)
            {
                RunFile(ifcFilePath);
            }

            return 0;
        }

        private static void RunFile(string ifcFilePath)
        {
            Console.WriteLine(new string('=', 100));
            Console.WriteLine($"File: {Path.GetFileName(ifcFilePath)}");
            Stopwatch total = Stopwatch.StartNew();

            // 1. Open IFC model (Zero xBIM dependency, pure GeometryHelper)
            using (var model = IfcStoreCache.Open(ifcFilePath))
            {
                Console.WriteLine($"Schema: {model.SchemaVersion}, length unit: {model.OriginalLengthUnit}, products: {model.ProductCount}");
                if (model.DuplicateGlobalIds.Count > 0)
                {
                    Console.WriteLine($"Duplicate GlobalIds (only the first product of each is reachable): {string.Join(", ", model.DuplicateGlobalIds)}");
                }

                // Geometry is reported in metres whatever the model's length unit.
                var options = new IfcConvertOptions { TargetUnit = LengthUnit.Meters };

                // 2. Query product catalog (super lightweight metadata, no geometry conversion)
                IReadOnlyList<IfcProductMetadata> catalog = model.GetProductCatalog();
                Console.WriteLine($"Total products in model: {catalog.Count}");
                foreach (IGrouping<string, IfcProductMetadata> group in catalog.GroupBy(p => p.IfcType).OrderByDescending(g => g.Count()))
                {
                    Console.WriteLine($"  {group.Key}: {group.Count()}");
                }

                // ── Loop over every product: geometry, warnings and properties ──────────

                int withSolids = 0;
                int withWarnings = 0;
                double totalVolume = 0.0;
                string firstSolidGuid = null;

                foreach (IfcProductMetadata product in catalog)
                {
                    Console.WriteLine();
                    string tag = string.IsNullOrEmpty(product.Tag) ? string.Empty : $", Tag: {product.Tag}";
                    Console.WriteLine($"{product.IfcType} '{product.Name}' GUID: {product.GlobalId}{tag}");

                    // 3. Complete geometry information (Solids, Surfaces, AABB, Warnings)
                    IfcProductGeometry geom = model.GetGeometry(product.GlobalId, options);
                    if (geom == null)
                    {
                        continue;
                    }

                    if (geom.HasSolids)
                    {
                        withSolids++;
                        totalVolume += geom.TotalVolume;
                        firstSolidGuid = firstSolidGuid ?? product.GlobalId;

                        Console.WriteLine($"  Total Volume: {geom.TotalVolume:F4} m3 in {geom.Solids.Count} solid(s)");
                        Console.WriteLine($"  Bounding Box: Min={geom.BoundingBox.Min}, Max={geom.BoundingBox.Max}");
                        Console.WriteLine($"  Placement Origin: {geom.Placement.Transform(GeoPoint3.Origin)}");

                        // Closed manifold solid bodies
                        foreach (GeoSolid3 s in geom.Solids)
                        {
                            Console.WriteLine($"    Solid Faces: {s.Faces.Count}, Volume: {s.Volume:F4} m3");
                        }
                    }

                    // Open surface models or thin sheets (if any)
                    foreach (GeoFace3 surface in geom.OpenSurfaces)
                    {
                        Console.WriteLine($"  Surface Area: {surface.Area:F4} m2");
                    }

                    // Problems met while converting (items skipped, solids not closed, openings not cut...)
                    if (geom.HasWarnings)
                    {
                        withWarnings++;
                        foreach (string warning in geom.Warnings)
                        {
                            Console.WriteLine($"  ! {warning}");
                        }
                    }

                    // 4. Get ALL property sets and quantity sets of the product (no xBIM required)
                    IReadOnlyDictionary<string, IfcPropertySet> props = model.GetProperties(product.GlobalId);
                    foreach (IfcPropertySet pset in props.Values)
                    {
                        Console.WriteLine($"  [{pset.Name}] — {pset.Properties.Count} properties");
                        foreach (IfcPropertyValue value in pset.Properties.Values)
                        {
                            Console.WriteLine($"    {value}");
                        }
                    }

                    // 5. Get a single property value directly, e.g. IsExternal from Pset_WallCommon for an IfcWall
                    if (product.IfcType.StartsWith("Ifc", StringComparison.Ordinal))
                    {
                        string commonPset = "Pset_" + product.IfcType.Substring(3) + "Common";
                        IfcPropertyValue isExternal = model.GetProperty(product.GlobalId, commonPset, "IsExternal");
                        if (isExternal != null)
                        {
                            Console.WriteLine($"  IsExternal ({commonPset}) = {isExternal.Value} ({isExternal.TypeName})");
                        }
                    }
                }

                // ── Queries across the model ─────────────────────────────────────────────

                // 6. Retrieve all solids by IFC type name (e.g. IfcWall, IfcBeam, IfcColumn), openings cut
                var optionsWithVoids = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, ApplyVoids = true };
                IReadOnlyList<GeoSolid3> wallSolids = model.GetSolidsByType("IfcWall", optionsWithVoids);
                Console.WriteLine();
                Console.WriteLine($"Converted {wallSolids.Count} wall solid(s) with openings cut: {wallSolids.Sum(s => s.Volume):F4} m3");

                if (firstSolidGuid != null)
                {
                    // 7. Geometry cache: the first call for a set of options converts, the second is served from cache
                    var optsLocal = new IfcConvertOptions { TargetUnit = LengthUnit.Meters, CoordinateSpace = CoordinateSpace.Local };
                    Stopwatch sw = Stopwatch.StartNew();
                    GeoSolid3 localFirst = model.GetSolid(firstSolidGuid, optsLocal);    // First call: converts
                    sw.Stop();
                    Console.WriteLine($"\nFirst GetSolid ({firstSolidGuid}, Local): {sw.ElapsedMilliseconds}ms");

                    sw.Restart();
                    GeoSolid3 localCached = model.GetSolid(firstSolidGuid, optsLocal);   // Second call: from cache
                    sw.Stop();
                    Console.WriteLine($"Second GetSolid (cached): {sw.ElapsedMilliseconds}ms");
                    Console.WriteLine($"Same object reference: {ReferenceEquals(localFirst, localCached)}");

                    // 8. CoordinateSpace: Local keeps the product's own frame, Global applies its placement
                    GeoSolid3 globalSolid = model.GetSolid(firstSolidGuid, options);
                    Console.WriteLine($"Local centroid: {localFirst?.Centroid}");
                    Console.WriteLine($"Global centroid: {globalSolid?.Centroid}");
                }

                Console.WriteLine();
                Console.WriteLine($"Summary: {catalog.Count} products, {withSolids} with solids ({totalVolume:F4} m3), " +
                    $"{withWarnings} with warnings, {total.Elapsed.TotalSeconds:F1}s");
            }
        }
    }
}
