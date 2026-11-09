using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMREASSOCIATE and DIMDISASSOCIATE (-DIMDISASSOCIATE at the command line): a dimension tied to new points of objects, or
    /// untied, as AutoCAD 2021's, each one step of undo.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddDimensionAssociationTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("DIMREASSOCIATE", DimReassociate));
            registry.Add(new CadCommand("DIMDISASSOCIATE", DimDisassociate));
            registry.Add(new CadCommand("-DIMDISASSOCIATE", DimDisassociate));
            registry.AddVariable(CadSystemVariable.Whole("DIMASSOC", true, 0, 2, s => s.Document.Database.DimAssoc, (c, v) => c.Database.DimAssoc = v, "$DIMASSOC"));
        }

        /// <summary>
        /// DIMREASSOCIATE: the dimensions selected, then for each its points asked again, as AutoCAD asks them: a linear or an
        /// aligned one "Specify first extension line origin or [Select object] &lt;next&gt;:" and "Specify second extension line
        /// origin &lt;next&gt;:", Select object tying both to the ends of a line; a radius or a diameter "Select arc or circle
        /// &lt;next&gt;:"; an angle of an arc "Select arc &lt;next&gt;:". A point picked with an object snap ties that point
        /// there and the dimension moves to it; Enter keeps the point as it is tied, and a point given with no snap of an
        /// object keeps it too. Other dimensions are passed over.
        /// </summary>
        private static IEnumerable<CadPrompt> DimReassociate(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var dimensions = new List<string>();

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity is DxfRotatedDimension || entity is DxfAlignedDimension || entity is DxfRadialDimension || entity is DxfDiametricDimension || entity is DxfPoint3AngularDimension)
                {
                    dimensions.Add(handle);
                }
            }

            foreach (string handle in dimensions)
            {
                var dimension = (DxfDimension)c.Document.FindEntity(handle);

                if (dimension == null)
                {
                    continue;
                }

                var references = new Dictionary<int, DxfOsnapPointRef>();

                foreach (DxfOsnapPointRef reference in dimension.DimAssoc?.PointReferences ?? new DxfOsnapPointRef[0])
                {
                    references[reference.PointIndex] = reference;
                }

                if (dimension is DxfRotatedDimension || dimension is DxfAlignedDimension)
                {
                    yield return CadPrompt.ForPoint("Specify first extension line origin", null, new[] { "Select object" }, "next");

                    if (c.Input.Is("Select object"))
                    {
                        yield return CadPrompt.ForEntity("Select object");

                        if (c.Input.Kind == CadInputKind.Selection && c.Document.FindEntity(c.Input.Handles[0]) is DxfLine line)
                        {
                            DxfOsnapPointRef[] ends = CadDimAssociation.LineEnds(line);
                            references[0] = ends[0];
                            references[1] = ends[1];
                        }
                        else
                        {
                            c.Write("Object selected is not a line.");
                        }
                    }
                    else
                    {
                        Take(c, references, 0);
                        yield return CadPrompt.ForPoint("Specify second extension line origin", null, null, "next");
                        Take(c, references, 1);
                    }
                }
                else if (dimension is DxfPoint3AngularDimension)
                {
                    yield return CadPrompt.ForEntity("Select arc", null, "next");

                    if (c.Input.Kind == CadInputKind.Selection && c.Document.FindEntity(c.Input.Handles[0]) is DxfArc arc)
                    {
                        foreach (DxfOsnapPointRef reference in CadDimAssociation.ArcEnds(arc))
                        {
                            references[reference.PointIndex] = reference;
                        }
                    }
                }
                else
                {
                    yield return CadPrompt.ForEntity("Select arc or circle", null, "next");

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                        if (picked is DxfCircle || picked is DxfArc)
                        {
                            references[1] = CadDimAssociation.CenterOf(picked)[0];
                        }
                        else
                        {
                            c.Write("Object selected is not a circle or arc.");
                        }
                    }
                }

                if (references.Count == 0)
                {
                    continue;
                }

                var list = new List<DxfOsnapPointRef>(references.Values);
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, "Associative", null, "Yes") }, s =>
                {
                    CadDimAssociation.Reassociate(s.Document, (DxfDimension)s.Document.FindEntity(handle), list);
                });
            }
        }

        /// <summary>Takes the point just given for a point of the dimension: tied where it was snapped to an object followed.</summary>
        private static void Take(CadCommandContext c, Dictionary<int, DxfOsnapPointRef> references, int index)
        {
            if (!c.Input.IsPoint)
            {
                return;
            }

            DxfOsnapPointRef reference = CadDimAssociation.FromSnap(c.Document, index, c.Input.Snap);

            if (reference == null)
            {
                c.Write("The point is on no object to tie it to.");
                return;
            }

            references[index] = reference;
        }

        /// <summary>
        /// DIMDISASSOCIATE: the associative dimensions selected untied from their objects, keeping their points, as AutoCAD's,
        /// which then says how many: "2 disassociated."; the others passed over.
        /// </summary>
        private static IEnumerable<CadPrompt> DimDisassociate(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var tied = new List<string>();

            foreach (string handle in c.Selected)
            {
                if (c.Document.FindEntity(handle) is DxfDimension dimension && dimension.DimAssoc != null)
                {
                    tied.Add(handle);
                }
            }

            if (tied.Count > 0)
            {
                var planned = new List<CadEditRecord>();

                foreach (string handle in tied)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Associative", "Yes", "No"));
                }

                c.Apply(planned, s =>
                {
                    foreach (string handle in tied)
                    {
                        ((DxfDimension)s.Document.FindEntity(handle)).ReleaseDimAssoc();
                    }
                });
            }

            c.Write(tied.Count.ToString(CultureInfo.InvariantCulture) + " disassociated.");
        }
    }
}
