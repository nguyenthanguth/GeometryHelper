using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// UNION, SUBTRACT and INTERSECT of regions, as AutoCAD 2021 makes them (it makes them of solids too, which the presenter
    /// does not read): the first region chosen is given the answer in place and the others are erased.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>One region taking part: its handle, its entity and its shape.</summary>
        private sealed class RegionPart
        {
            internal RegionPart(string handle, DxfUnknownEntity entity, DxfRegionShape shape)
            {
                Handle = handle;
                Entity = entity;
                Shape = shape;
            }

            internal string Handle { get; }

            internal DxfUnknownEntity Entity { get; }

            internal DxfRegionShape Shape { get; }
        }

        private const string TooFew = "At least 2 solids, surfaces, or coplanar regions must be selected.";

        /// <summary>
        /// UNION: "Select objects:", then every region chosen as one: the first, a REGION still, takes the shape of the lot (two
        /// regions far apart are one region of two faces), the others are erased, and the result keeps the first's handle, layer,
        /// linetype and lineweight but is ByLayer in colour, as AutoCAD 2021 leaves it. Objects that are not regions are passed over,
        /// and of regions in several planes those of the plane most of them share are combined, the others left as they are; fewer
        /// than two regions say "At least 2 solids, surfaces, or coplanar regions must be selected." Nothing chosen ends it quietly.
        /// </summary>
        private static IEnumerable<CadPrompt> Union(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            List<RegionPart> parts = Coplanar(c, RegionParts(c, c.Selected));

            if (parts.Count < 2)
            {
                c.Write(TooFew);
                yield break;
            }

            Combine(c, parts, new List<RegionPart>(), tolerance =>
                DxfRegionShape.Unite(parts.Select(p => p.Shape), tolerance)
            );
        }

        /// <summary>
        /// INTERSECT: "Select objects:", then what every region chosen has in common, given to the first and the others erased,
        /// as UNION does; where they have nothing in common AutoCAD says "Null region created - deleted" and all are erased.
        /// </summary>
        private static IEnumerable<CadPrompt> Intersect(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            List<RegionPart> parts = Coplanar(c, RegionParts(c, c.Selected));

            if (parts.Count < 2)
            {
                c.Write(TooFew);
                yield break;
            }

            Combine(c, parts, new List<RegionPart>(), tolerance =>
                DxfRegionShape.Intersect(parts.Select(p => p.Shape), tolerance)
            );
        }

        /// <summary>
        /// SUBTRACT: "Select solids, surfaces, and regions to subtract from ..", "Select objects:" until Enter, then "Select solids,
        /// surfaces, and regions to subtract .." and the same: the regions of the second choice are taken from the union of
        /// the first, which is given to the first of them while the others and the regions taken away are erased. Where nothing
        /// is left AutoCAD says "Null region created - deleted" and erases all. A choice with no region says "No solids,
        /// surfaces, or regions selected."; fewer than two coplanar regions in all, "At least two solids, surfaces, or coplanar
        /// regions must be selected."
        /// </summary>
        private static IEnumerable<CadPrompt> Subtract(CadCommandContext c)
        {
            c.Write("Select solids, surfaces, and regions to subtract from ..");

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            List<RegionPart> sources = RegionParts(c, c.Selected);

            if (sources.Count == 0)
            {
                c.Write("No solids, surfaces, or regions selected.");
                yield break;
            }

            c.Write("Select solids, surfaces, and regions to subtract ..");

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            List<RegionPart> tools = RegionParts(c, c.Selected);

            if (tools.Count == 0)
            {
                c.Write("No solids, surfaces, or regions selected.");
                yield break;
            }

            // Those out of the plane of the first source take no part; what takes none of the two is no subtraction.
            sources = Coplanar(c, sources);
            tools = tools.Where(t => t.Shape.IsCoplanarWith(sources[0].Shape, c.Session.Tolerance)).ToList();

            if (tools.Count == 0)
            {
                c.Write("At least two solids, surfaces, or coplanar regions must be selected.");
                yield break;
            }

            Combine(c, sources, tools, tolerance =>
                DxfRegionShape.Subtract(sources.Select(p => p.Shape), tools.Select(p => p.Shape), tolerance)
            );
        }

        /// <summary>The regions among some objects, in the order given, each with the shape its ACIS data holds; one whose data is not readable flat faces is left out.</summary>
        private static List<RegionPart> RegionParts(CadCommandContext c, IEnumerable<string> handles)
        {
            var parts = new List<RegionPart>();

            foreach (string handle in handles)
            {
                if (
                    c.Document.FindEntity(handle) is DxfUnknownEntity region
                    && region.DxfName == "REGION"
                    && region.GetRegionShape() is DxfRegionShape shape
                )
                {
                    parts.Add(new RegionPart(handle, region, shape));
                }
            }

            return parts;
        }

        /// <summary>
        /// The regions that lie in one plane, the plane most of them share (the earlier where two share as many): AutoCAD 2021
        /// combines the regions of one plane and leaves the others where they are, an elevated circle chosen with a box and a
        /// circle on the XY plane staying as it was.
        /// </summary>
        private static List<RegionPart> Coplanar(CadCommandContext c, List<RegionPart> parts)
        {
            var groups = new List<List<RegionPart>>();

            foreach (RegionPart part in parts)
            {
                List<RegionPart> group = groups.Find(g => g[0].Shape.IsCoplanarWith(part.Shape, c.Session.Tolerance));

                if (group == null)
                {
                    groups.Add(group = new List<RegionPart>());
                }

                group.Add(part);
            }

            List<RegionPart> most = null;

            foreach (List<RegionPart> group in groups)
            {
                if (most == null || group.Count > most.Count)
                {
                    most = group;
                }
            }

            return most ?? parts;
        }

        /// <summary>
        /// Makes the answer of a boolean the first source's shape and erases every other region that took part, as one edit;
        /// where nothing is left, says so and erases them all.
        /// </summary>
        private static void Combine(
            CadCommandContext c,
            List<RegionPart> sources,
            List<RegionPart> tools,
            Func<Tolerance, DxfRegionShape> make
        )
        {
            DxfRegionShape answer;

            try
            {
                answer = make(c.Session.Tolerance);
            }
            catch (ArgumentException e)
            {
                c.Write(e.Message);
                return;
            }

            RegionPart first = sources[0];
            var planned = new List<CadEditRecord>();
            var gone = new List<string>();

            if (answer != null)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, first.Handle, c.Command.Name));
            }
            else
            {
                c.Write("Null region created - deleted");
            }

            // Every other region that took part is erased, the first too where nothing is left of it.
            foreach (RegionPart part in sources.Concat(tools))
            {
                if ((answer == null || part.Handle != first.Handle) && !gone.Contains(part.Handle))
                {
                    gone.Add(part.Handle);
                    planned.Add(new CadEditRecord(CadEditRecordKind.Erased, part.Handle));
                }
            }

            c.Apply(planned, s =>
            {
                if (answer != null)
                {
                    var region = (DxfUnknownEntity)s.Document.FindEntity(first.Handle);
                    region.SetRegionShape(answer);

                    // AutoCAD 2021 leaves the first region ByLayer in colour, its true colour gone, the rest of its look kept.
                    region.ColorIndex = 256;
                    region.TrueColor = null;
                }

                foreach (string handle in gone)
                {
                    if (!s.IsHandled(handle))
                    {
                        s.Document.FindEntity(handle).Erase();
                    }
                }
            });
        }
    }
}
