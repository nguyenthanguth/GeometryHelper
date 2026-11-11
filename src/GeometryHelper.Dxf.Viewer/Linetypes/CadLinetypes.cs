using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What came of loading linetypes: the edit, and the names loaded, already there and refused, each with why.</summary>
    public sealed class CadLinetypeLoad
    {
        internal CadLinetypeLoad(
            CadEditResult result,
            IReadOnlyList<string> loaded,
            IReadOnlyList<string> present,
            IReadOnlyList<string> refused
        )
        {
            Result = result;
            Loaded = loaded;
            Present = present;
            Refused = refused;
        }

        /// <summary>Gets the edit: one undo step, or none where nothing was loaded.</summary>
        public CadEditResult Result { get; }

        /// <summary>Gets the names loaded, in the order asked.</summary>
        public IReadOnlyList<string> Loaded { get; }

        /// <summary>Gets the names the drawing had already, passed over.</summary>
        public IReadOnlyList<string> Present { get; }

        /// <summary>Gets a line for each linetype refused, saying why: a text style the drawing has not, a shape not found.</summary>
        public IReadOnlyList<string> Refused { get; }
    }

    /// <summary>
    /// The linetypes of a document loaded, tested and drawn, as LINETYPE and its dialogs need them. A linetype of a file on disk
    /// is loaded by the library's own loader (<see cref="DxfDatabase.LoadLineTypeFile"/>), which reads it as AutoCAD does, text
    /// and shapes alike; one of the presenter's own set is written from its definition (<see cref="Add"/>).
    /// </summary>
    public static class CadLinetypes
    {
        /// <summary>
        /// Loads linetypes into a document as one undo step, named as given: each of a file on disk through the library's
        /// loader, each of <see cref="CadLinetypeFile.BuiltInName"/> from its definition; one the drawing has already passed
        /// over, one the drawing cannot take (its text style or shape missing) refused alone, the others loaded. DASHED of
        /// the presenter's own set loaded into a new drawing is an LTYPE of a dash 6 and a gap 1.5, 7.5 long. A linetype of
        /// <see cref="CadLinetypeFile.BuiltIn"/> itself is written from its definition whatever the path, so that the Load
        /// dialog's list, the presenter's own set followed by a file's, loads in one step.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="linetypes">The linetypes, as read from the file.</param>
        /// <param name="path">The file they were read from: a full path, or <see cref="CadLinetypeFile.BuiltInName"/>.</param>
        /// <param name="stepName">The name of the undo step, as LINETYPE.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document, the linetypes or the path is null.</exception>
        public static CadLinetypeLoad Load(
            CadDocument document,
            IEnumerable<CadLinetypeDefinition> linetypes,
            string path,
            string stepName = "LINETYPE"
        )
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (linetypes == null)
            {
                throw new ArgumentNullException(nameof(linetypes));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            bool own = string.Equals(path, CadLinetypeFile.BuiltInName, StringComparison.OrdinalIgnoreCase);
            var wanted = new List<CadLinetypeDefinition>();
            var present = new List<string>();
            var refused = new List<string>();

            foreach (CadLinetypeDefinition linetype in linetypes)
            {
                if (
                    linetype == null
                    || wanted.Exists(w => string.Equals(w.Name, linetype.Name, StringComparison.OrdinalIgnoreCase))
                )
                {
                    continue;
                }

                if (document.Database.LinetypeTable.Has(linetype.Name))
                {
                    present.Add(linetype.Name);
                }
                else if (linetype.Fault != null)
                {
                    refused.Add(linetype.Fault);
                }
                else
                {
                    wanted.Add(linetype);
                }
            }

            var loaded = new List<string>();

            if (wanted.Count == 0)
            {
                return new CadLinetypeLoad(CadEditResult.Done(Array.Empty<CadEditRecord>()), loaded, present, refused);
            }

            CadEditResult result = document.ApplyEdit(stepName ?? "LINETYPE", null, s =>
            {
                foreach (CadLinetypeDefinition linetype in wanted)
                {
                    try
                    {
                        if (own || CadLinetypeFile.BuiltIn.Contains(linetype))
                        {
                            Add(s.Database, linetype);
                        }
                        else
                        {
                            s.Database.LoadLineTypeFile(linetype.Name, path);
                        }

                        loaded.Add(linetype.Name);
                        s.Record(
                            new CadEditRecord(
                                CadEditRecordKind.TableChanged,
                                null,
                                "Load linetype",
                                null,
                                linetype.Name
                            )
                        );
                    }
                    catch (ArgumentException e)
                    {
                        // The library adds nothing of a linetype it refuses, a style of shapes included.
                        refused.Add(e.Message);
                    }
                }
            });

            if (!result.Succeeded)
            {
                loaded.Clear();
            }

            return new CadLinetypeLoad(result, loaded, present, refused);
        }

        /// <summary>
        /// Writes a linetype of the presenter's own as an LTYPE record of a drawing, as AutoCAD writes one: its name, its
        /// description, 72 the alignment A, the count of its elements and its length, then each element's 49 and 74, and for a
        /// text its 75 of nought, the handle of its text style (340), its scale, rotation and offset (46, 50, 44, 45) and the
        /// text (9). GAS_LINE's gap of 2.4 carrying GAS in STANDARD is <c>49 -2.4, 74 2, 75 0, 340</c> Standard's handle
        /// <c>, 46 1.2, 50 0, 44 -1.2, 45 -0.6, 9 GAS</c>.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="linetype">The linetype.</param>
        /// <returns>The record added.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the linetype is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the linetype could not be read, the drawing has one of its name, a
        /// text names a style the drawing has not, or an element carries a shape, which is loaded from its file alone.</exception>
        public static DxfLinetypeTableRecord Add(DxfDatabase database, CadLinetypeDefinition linetype)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (linetype == null)
            {
                throw new ArgumentNullException(nameof(linetype));
            }

            if (linetype.Fault != null)
            {
                throw new ArgumentException(linetype.Fault, nameof(linetype));
            }

            if (database.LinetypeTable.Has(linetype.Name))
            {
                throw new ArgumentException(
                    "The drawing has a linetype named " + linetype.Name + " already.",
                    nameof(linetype)
                );
            }

            var record = new DxfLinetypeTableRecord
            {
                Name = linetype.Name,
                AsciiDescription = linetype.Description,
                NumDashes = linetype.Elements.Count,
            };

            for (int i = 0; i < linetype.Elements.Count; i++)
            {
                CadLinetypeElement element = linetype.Elements[i];
                record.SetDashLengthAt(i, element.Length);

                if (element.Kind == CadLinetypeElementKind.Shape)
                {
                    throw new ArgumentException(
                        "The linetype "
                            + linetype.Name
                            + " draws the shape "
                            + element.Value
                            + ", which is loaded from its .lin file beside "
                            + element.Style
                            + ".",
                        nameof(linetype)
                    );
                }

                if (element.Kind != CadLinetypeElementKind.Text)
                {
                    continue;
                }

                DxfTextStyleTableRecord style = database.TextStyleTable.FirstOrDefault(t =>
                    !t.IsShapeFile && string.Equals(t.Name, element.Style, StringComparison.OrdinalIgnoreCase)
                );

                if (style == null)
                {
                    throw new ArgumentException(
                        "The linetype "
                            + linetype.Name
                            + " writes its text in the text style "
                            + element.Style
                            + ", which the drawing has not.",
                        nameof(linetype)
                    );
                }

                record.SetTextAt(i, element.Value);
                record.SetShapeStyleAt(i, style);
                record.SetShapeScaleAt(i, element.Scale);
                record.SetShapeRotationAt(i, element.Rotation * Math.PI / 180.0);
                record.SetShapeOffsetAt(i, new GeoVector2(element.X, element.Y));
                record.SetShapeIsUcsOrientedAt(i, element.RotationKind == CadLinetypeRotation.Absolute);
            }

            database.LinetypeTable.Add(record);
            return record;
        }

        /// <summary>
        /// Gets whether something of the drawing draws with a linetype, so that it may not be deleted: ByLayer, ByBlock and
        /// Continuous always, as AutoCAD keeps them, and any other an entity, a layer, a style or the header names, as the
        /// library's purge finds it.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The linetype's name.</param>
        /// <returns>True where it is kept; false for one nothing uses, or one the drawing has not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the name is null.</exception>
        public static bool IsInUse(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (IsKept(name))
            {
                return true;
            }

            DxfLinetypeTableRecord record = database.LinetypeTable.FirstOrDefault(l =>
                string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
            );
            return record != null && database.Purge(new DxfDBObject[] { record }).Count == 0;
        }

        /// <summary>Gets whether a linetype is one AutoCAD keeps in every drawing: ByLayer, ByBlock or Continuous.</summary>
        /// <param name="name">The name.</param>
        /// <returns>True for one of the three.</returns>
        public static bool IsKept(string name) =>
            string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Continuous", StringComparison.OrdinalIgnoreCase);

        /// <summary>Gets the lengths of a linetype of the drawing, as its LTYPE holds them; none for Continuous or one the drawing has not.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The linetype's name.</param>
        /// <returns>The lengths: above nought a dash, below a gap, nought a dot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<double> DashesOf(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfLinetypeTableRecord record =
                name == null
                    ? null
                    : database.LinetypeTable.FirstOrDefault(l =>
                        string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)
                    );

            if (record == null)
            {
                return Array.Empty<double>();
            }

            var dashes = new double[record.NumDashes];

            for (int i = 0; i < dashes.Length; i++)
            {
                dashes[i] = record.DashLengthAt(i);
            }

            return dashes;
        }

        /// <summary>Gets the names of the drawing's linetypes, ByLayer and ByBlock left out, in the order of their names: Continuous, DASHED, HIDDEN.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<string> Names(DxfDatabase database) =>
            (database ?? throw new ArgumentNullException(nameof(database)))
                .LinetypeTable.Select(l => l.Name)
                .Where(n =>
                    !string.Equals(n, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n, "ByBlock", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
    }
}
