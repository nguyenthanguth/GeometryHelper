using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The dialogs of PLOTSTAMP, IMAGEADJUST, ATTEXT and XBIND, over the typed commands; with no window each asks on the
    /// command line as -PLOTSTAMP, -IMAGEADJUST, -ATTEXT and -XBIND do.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>PLOTSTAMP: the window's Plot Stamp dialog over a copy of the session's stamp, taken on OK; with no window, -PLOTSTAMP's questions.</summary>
        private static IEnumerable<CadPrompt> PlotStampDialog(CadCommandContext c)
        {
            var request = new CadPlotStampRequest(c.Session.PlotStamp);

            if (!c.Session.Request("PLOTSTAMP", c.Document, request))
            {
                foreach (CadPrompt prompt in PlotStamp(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.IsAccepted)
            {
                c.Session.PlotStamp.CopyFrom(request.Stamp);
            }
        }

        /// <summary>
        /// IMAGEADJUST once the images are selected: the window's Image Adjust dialog, starting at the image's own values where
        /// one is selected and at 50, 50 and 0 where several are; OK gives every image not on a locked layer the three values,
        /// one undo step. With no window, -IMAGEADJUST's option and value.
        /// </summary>
        private static IEnumerable<CadPrompt> ImageAdjustDialog(CadCommandContext c, List<string> handles)
        {
            var first = (DxfRasterImage)c.Document.FindEntity(handles[0]);
            bool one = handles.Count == 1;
            string saved = first.Path;
            string found = string.IsNullOrEmpty(saved) ? null : c.Document.Xrefs.Locate(saved);
            var request = new CadImageAdjustRequest(
                handles.Count,
                one ? first.Brightness : 50,
                one ? first.Contrast : 50,
                one ? first.Fade : 0,
                found
            );

            if (!c.Session.Request("IMAGEADJUST", c.Document, request))
            {
                foreach (CadPrompt prompt in ImageAdjustOptions(c, handles))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (!request.IsAccepted)
            {
                yield break;
            }

            var values = new[]
            {
                new KeyValuePair<string, int>("Brightness", Clamp(request.Brightness)),
                new KeyValuePair<string, int>("Contrast", Clamp(request.Contrast)),
                new KeyValuePair<string, int>("Fade", Clamp(request.Fade)),
            };
            var planned = new List<CadEditRecord>();
            var changed = new List<string>();
            int locked = 0;

            foreach (string handle in handles)
            {
                var image = (DxfRasterImage)c.Document.FindEntity(handle);

                if (CadLayerEditor.IsLocked(c.Database, image.Layer))
                {
                    locked++;
                    continue;
                }

                foreach (KeyValuePair<string, int> value in values)
                {
                    int was = ImageLevel(image, value.Key);

                    if (was != value.Value)
                    {
                        planned.Add(
                            new CadEditRecord(
                                CadEditRecordKind.PropertyChanged,
                                handle,
                                value.Key,
                                was.ToString(CultureInfo.InvariantCulture),
                                value.Value.ToString(CultureInfo.InvariantCulture)
                            )
                        );

                        if (!changed.Contains(handle))
                        {
                            changed.Add(handle);
                        }
                    }
                }
            }

            if (locked > 0)
            {
                c.Write("The image is on a locked layer.");
            }

            if (changed.Count == 0)
            {
                yield break;
            }

            c.Apply(planned, s =>
            {
                foreach (string handle in changed)
                {
                    var image = (DxfRasterImage)s.Document.FindEntity(handle);

                    foreach (KeyValuePair<string, int> value in values)
                    {
                        SetImageLevel(image, value.Key, (byte)value.Value);
                    }
                }
            });
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));

        /// <summary>
        /// ATTEXT: the window's Attribute Extraction dialog, shown again after its Select Objects button has had the objects
        /// picked; OK reads the template of a CDF or SDF extract and writes the extract as -ATTEXT does, a template or output
        /// file refused saying why. With no window, -ATTEXT's questions.
        /// </summary>
        private static IEnumerable<CadPrompt> AttributeExtractDialog(CadCommandContext c)
        {
            string title = Path.GetFileNameWithoutExtension(c.Document.Title ?? "Drawing1");
            string remembered = LastTemplates.TryGetValue(c.Session, out StrongBox<string> box) ? box.Value : null;
            var request = new CadAttextRequest
            {
                TemplateFile = remembered ?? string.Empty,
                OutputFile = ExtractPath(c.Document, title, ".txt"),
            };

            while (true)
            {
                request.IsSelecting = false;

                if (!c.Session.Request("ATTEXT", c.Document, request))
                {
                    foreach (CadPrompt prompt in AttributeExtract(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (!request.IsSelecting)
                {
                    break;
                }

                yield return CadPrompt.ForSelection("Select objects");

                if (c.Input.Kind == CadInputKind.Selection)
                {
                    request.Selected = c.Input.Handles ?? Array.Empty<string>();
                }
            }

            if (!request.IsAccepted)
            {
                yield break;
            }

            bool dxf = request.Format == CadAttextFormat.Dxf;
            string output = (request.OutputFile ?? string.Empty).Trim().Trim('"');

            if (output.Length == 0)
            {
                c.Write("Enter an output file.");
                yield break;
            }

            string extractPath = ExtractPath(c.Document, output, dxf ? ".dxx" : ".txt");
            AttextTemplate template = null;

            if (!dxf)
            {
                string typed = (request.TemplateFile ?? string.Empty).Trim().Trim('"');
                string templatePath = typed.Length == 0 ? null : ExtractPath(c.Document, typed, ".txt");

                if (templatePath == null || !File.Exists(templatePath))
                {
                    c.Write(
                        "\""
                            + (templatePath == null ? string.Empty : Path.GetFileName(templatePath))
                            + "\": Can't find file."
                    );
                    yield break;
                }

                if (
                    string.Equals(
                        Path.GetFullPath(extractPath),
                        Path.GetFullPath(templatePath),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    c.Write("The template and extract files cannot be the same.");
                    yield break;
                }

                LastTemplates.GetOrCreateValue(c.Session).Value = templatePath;

                try
                {
                    template = ReadTemplate(c, File.ReadAllText(templatePath));
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    c.Write("Cannot read \"" + templatePath + "\": " + exception.Message);
                    yield break;
                }

                if (template == null)
                {
                    yield break;
                }
            }

            WriteAttributeExtract(
                c,
                dxf,
                request.Format == CadAttextFormat.Sdf,
                request.Selected,
                template,
                extractPath
            );
        }

        // The kinds of the Xbind dialog's tree, in its order, and the kind of -XBIND each is.
        private static readonly string[][] XbindTreeKinds =
        {
            new[] { "Block", "BLOCK" },
            new[] { "Dimstyle", "DIMSTYLE" },
            new[] { "Layer", "LAYER" },
            new[] { "Linetype", "LTYPE" },
            new[] { "Textstyle", "STYLE" },
        };

        /// <summary>
        /// XBIND: the window's Xbind dialog, the external references with the definitions they brought; OK binds those added to
        /// Definitions to Bind as -XBIND binds them, each kind one edit of the command's undo step, and says how many of each.
        /// With no window, -XBIND's questions.
        /// </summary>
        private static IEnumerable<CadPrompt> XBindDialog(CadCommandContext c)
        {
            var request = new CadXbindRequest(XbindReferences(c));

            if (!c.Session.Request("XBIND", c.Document, request))
            {
                foreach (CadPrompt prompt in XBind(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (!request.IsAccepted)
            {
                yield break;
            }

            foreach (string[] kind in XbindTreeKinds)
            {
                string[] patterns = request
                    .ToBind.Where(p => p.Key == kind[0])
                    .Select(p => EscapedWildcard(p.Value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (patterns.Length == 0)
                {
                    continue;
                }

                int bound = kind[1] == "BLOCK" ? BindBlocks(c, patterns) : BindSymbols(c, kind[1], patterns);
                string noun = XbindKinds.First(k => k[1] == kind[1])[2];
                c.Write(
                    bound == 0
                        ? "No matching " + noun + " names found."
                        : bound.ToString(CultureInfo.InvariantCulture) + " " + noun + "(s) bound."
                );
            }
        }

        /// <summary>A name as a wildcard that matches it alone, each character but a letter or a digit escaped by a backquote.</summary>
        private static string EscapedWildcard(string name) =>
            string.Concat(name.Select(ch => char.IsLetterOrDigit(ch) ? ch.ToString() : "`" + ch));

        /// <summary>The external references of the drawing with the definitions each brought, as the Xbind dialog lists them.</summary>
        private static IReadOnlyList<CadXbindReference> XbindReferences(CadCommandContext c)
        {
            var references = new List<CadXbindReference>();
            CadXrefManager xrefs = c.Document.Xrefs;

            foreach (
                DxfBlockTableRecord xref in c
                    .Database.BlockTable.Where(b => b.IsFromExternalReference && !b.IsLayout)
                    .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(b => b.Name, StringComparer.Ordinal)
            )
            {
                string prefix = xref.Name + "|";
                var definitions = new List<KeyValuePair<string, IReadOnlyList<string>>>();

                foreach (string[] kind in XbindTreeKinds)
                {
                    List<string> names;

                    if (kind[1] == "BLOCK")
                    {
                        string found = xrefs.Locate(xref.PathName, c.Document.DrawingFolder);
                        DxfDatabase source = found == null ? null : xrefs.Read(found).Database;
                        names =
                            source == null
                                ? new List<string>()
                                : source
                                    .BlockTable.Where(b =>
                                        !b.IsLayout
                                        && !b.IsAnonymous
                                        && !b.Name.StartsWith("*", StringComparison.Ordinal)
                                        && !b.IsFromExternalReference
                                        && !b.IsDependent
                                    )
                                    .Select(b => prefix + b.Name)
                                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(n => n, StringComparer.Ordinal)
                                    .ToList();
                    }
                    else
                    {
                        names = DependentNames(c, kind[1])
                            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }

                    if (names.Count > 0)
                    {
                        definitions.Add(new KeyValuePair<string, IReadOnlyList<string>>(kind[0], names));
                    }
                }

                references.Add(new CadXbindReference(xref.Name, definitions));
            }

            return references;
        }
    }
}
