using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MASSPROP for regions: "Select objects:", then the report AutoCAD 2021 writes (<see cref="CadMassProperties"/>): the area
    /// and perimeter, the bounding box, the centroid, the moments of inertia and the product of inertia about the origin, the
    /// radii of gyration, and the principal moments and directions about the centroid, of all the regions selected together. Then
    /// "Write analysis to a file? [Yes/No] &lt;N&gt;:"; Yes asks "Enter file name &lt;plan.mpr&gt;:" and writes the report to that
    /// file as AutoCAD writes its .mpr. "Nothing selected." and "No solids or regions selected." are AutoCAD's words where
    /// nothing, or nothing of a region, was picked. The regions have to lie flat on the XY plane; the 3D solids MASSPROP also
    /// takes are not part of the presenter.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> MassProperties(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                c.Write("Nothing selected.");
                yield break;
            }

            var shapes = new List<DxfRegionShape>();

            foreach (string handle in c.Selected)
            {
                if (
                    c.Document.FindEntity(handle) is DxfUnknownEntity region
                    && region.DxfName == "REGION"
                    && region.GetRegionShape() is DxfRegionShape shape
                )
                {
                    shapes.Add(shape);
                }
            }

            if (shapes.Count == 0)
            {
                c.Write("No solids or regions selected.");
                yield break;
            }

            foreach (DxfRegionShape shape in shapes)
            {
                if (Math.Abs(shape.Normal.Z) < 1.0 - 1E-9)
                {
                    c.Write("MASSPROP takes regions that lie flat on the XY plane; one of them does not.");
                    yield break;
                }
            }

            CadMassProperties found = CadMassProperties.Of(shapes);

            if (found == null)
            {
                c.Write("The regions have no area.");
                yield break;
            }

            foreach (string line in found.Lines())
            {
                c.Write(line);
            }

            yield return CadPrompt.ForKeyword("Write analysis to a file?", new[] { "Yes", "No" }, "N");

            if (!c.Input.Is("Yes"))
            {
                yield break;
            }

            string fallback =
                c.Document.HasFile && Path.IsPathRooted(c.Document.FileName)
                    ? Path.ChangeExtension(c.Document.FileName, ".mpr")
                    : Path.Combine(
                        Environment.CurrentDirectory,
                        Path.GetFileNameWithoutExtension(c.Document.Title) + ".mpr"
                    );
            yield return CadPrompt.ForString("Enter file name", fallback);
            string path =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : fallback;

            try
            {
                File.WriteAllText(path, found.FileText(), new UTF8Encoding(false));
            }
            catch (Exception e)
                when (e is IOException
                    || e is UnauthorizedAccessException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                c.Write("Cannot write the file " + path + ": " + e.Message);
            }
        }
    }
}
