using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of external references and images, in AutoCAD 2021's words: XATTACH and IMAGEATTACH (the attach dialogs,
    /// their typed forms with no window), XREF, EXTERNALREFERENCES and IMAGE (the palette), -XREF and -IMAGE, IMAGECLIP,
    /// IMAGEFRAME and TRANSPARENCY. Each change of the drawing is one undo step; unloading and reloading change what is shown,
    /// not the drawing, and are not.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] XrefOptions = { "?", "Bind", "Detach", "Path", "pathType", "Unload", "Reload", "Overlay", "Attach" };
        private static readonly string[] ImageOptions = { "?", "Detach", "Path", "Reload", "Unload", "Attach" };
        private static readonly string[] XrefPointKeywords = { "Scale", "X", "Y", "Z", "Rotate", "PScale", "PX", "PY", "PZ", "PRotate" };

        internal static void AddXrefsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("XATTACH", c => Attach(c, CadAttachKind.Xref)));
            registry.Add(new CadCommand("IMAGEATTACH", c => Attach(c, CadAttachKind.Image)));
            registry.Add(new CadCommand("XREF", XrefPalette));
            registry.Add(new CadCommand("EXTERNALREFERENCES", XrefPalette));
            registry.Add(new CadCommand("IMAGE", XrefPalette));
            registry.Add(new CadCommand("-XREF", XrefTyped));
            registry.Add(new CadCommand("-IMAGE", ImageTyped));
            registry.Add(new CadCommand("IMAGECLIP", ImageClip));
            registry.Add(new CadCommand("TRANSPARENCY", Transparency));
        }

        /// <summary>XREF (XR), EXTERNALREFERENCES (ER) and IMAGE (IM): the window's External References palette.</summary>
        private static IEnumerable<CadPrompt> XrefPalette(CadCommandContext c)
        {
            if (!c.Session.Request("XREF", c.Document))
            {
                c.Write("The External References palette is shown by the window.");
            }

            yield break;
        }

        /// <summary>
        /// XATTACH (XA) and IMAGEATTACH (IAT): the window's attach dialog; OK attaches the file chosen, asking on the command
        /// line what is to be specified on screen. With no window, the file's name typed and every placing asked.
        /// </summary>
        private static IEnumerable<CadPrompt> Attach(CadCommandContext c, CadAttachKind kind)
        {
            var request = new CadAttachRequest(c.Document, kind);

            if (!c.Session.Request(kind == CadAttachKind.Xref ? "XATTACH" : "IMAGEATTACH", c.Document, request))
            {
                foreach (CadPrompt prompt in kind == CadAttachKind.Xref ? AttachTyped(c, false) : ImageAttachTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                yield break;
            }

            foreach (CadPrompt prompt in kind == CadAttachKind.Xref ? PlaceXref(c, request) : PlaceImage(c, request, false))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -XREF (-XR): "Enter an option [?/Bind/Detach/Path/pathType/Unload/Reload/Overlay/Attach] &lt;Attach&gt;:", each option
        /// asking for the names of the external references it acts on, with wildcards and commas, as AutoCAD's does. Bind is
        /// refused: a DXF external reference is shown, never written into the drawing.
        /// </summary>
        private static IEnumerable<CadPrompt> XrefTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter an option", XrefOptions, "Attach");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "Attach" || option == "Overlay")
            {
                foreach (CadPrompt prompt in AttachTyped(c, option == "Overlay"))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (option == "Bind")
            {
                c.Write("Bind is not offered: a DXF external reference is shown, never written into the drawing.");
                yield break;
            }

            string ask = option == "?" ? "Enter xref name(s) to list" : option == "Path" ? "Edit xref name(s) to edit path" : option == "pathType" ? "Enter xref name(s) to edit path type" : "Enter xref name(s) to " + option.ToLowerInvariant();
            yield return CadPrompt.ForString(ask, option == "?" ? "*" : null);
            string names = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : option == "?" ? "*" : null;

            if (names == null)
            {
                yield break;
            }

            List<DxfBlockTableRecord> blocks = XrefsNamed(c.Database, names);

            switch (option)
            {
                case "?":
                    ListXrefs(c, blocks);
                    break;
                case "Detach":
                    DetachXrefs(c, blocks);
                    break;
                case "Unload":
                    foreach (DxfBlockTableRecord block in blocks)
                    {
                        c.Write("Xref \"" + block.Name + "\": " + block.PathName);
                        c.Document.Xrefs.SetUnloaded(block.Name, true);
                        c.Write("\"" + block.Name + "\" is unloaded.");
                    }

                    Redisplay(c, blocks.Count);
                    break;
                case "Reload":
                    foreach (DxfBlockTableRecord block in blocks)
                    {
                        ReloadXref(c, block);
                    }

                    Redisplay(c, blocks.Count);
                    break;
                case "Path":
                    foreach (DxfBlockTableRecord block in blocks)
                    {
                        foreach (CadPrompt prompt in EditXrefPath(c, block.Name))
                        {
                            yield return prompt;
                        }
                    }

                    break;
                case "pathType":
                    yield return CadPrompt.ForKeyword("Enter new path type", new[] { "Full", "Relative", "None" });

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        SetXrefPathType(c, blocks, (CadPathType)Enum.Parse(typeof(CadPathType), c.Input.Text));
                    }

                    break;
            }
        }

        /// <summary>The external references of a drawing whose names match a list of names with wildcards, parted by commas, in ordinal order.</summary>
        private static List<DxfBlockTableRecord> XrefsNamed(DxfDatabase database, string names)
        {
            string[] patterns = names.Split(',').Select(p => p.Trim().Trim('"')).Where(p => p.Length > 0).ToArray();
            var found = new List<DxfBlockTableRecord>();

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (block.IsFromExternalReference && !block.IsLayout && patterns.Any(p => CadWildcard.IsMatch(block.Name, p, true)))
                {
                    found.Add(block);
                }
            }

            found.Sort((a, b) =>
            {
                int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
            });
            return found;
        }

        /// <summary>-XREF ?: the table AutoCAD 2021 prints, the name quoted, its type and its path as saved, and the count.</summary>
        private static void ListXrefs(CadCommandContext c, List<DxfBlockTableRecord> blocks)
        {
            c.Write("Xref name".PadRight(34) + "Xref Type".PadRight(13) + "Path");
            c.Write(new string('-', 22).PadRight(34) + new string('-', 9).PadRight(13) + new string('-', 10));

            foreach (DxfBlockTableRecord block in blocks)
            {
                c.Write(("\"" + block.Name + "\"").PadRight(34) + (block.IsFromOverlayReference ? "Overlay" : "Attach").PadRight(13) + block.PathName);
            }

            c.Write("Total Xref(s): " + blocks.Count.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>-XREF Detach: each external reference detached as AutoCAD's DetachXref detaches it, in one undo step; its INSERTs in the spaces asked through the owner gate.</summary>
        private static void DetachXrefs(CadCommandContext c, List<DxfBlockTableRecord> blocks)
        {
            if (blocks.Count == 0)
            {
                c.Write("No matching xref names found.");
                return;
            }

            var planned = new List<CadEditRecord>();
            var names = new List<string>();

            foreach (DxfBlockTableRecord block in blocks)
            {
                names.Add(block.Name);

                foreach (DxfBlockReference insert in block.GetBlockReferences(true))
                {
                    if (c.Document.FindEntity(insert.Handle) != null)
                    {
                        planned.Add(new CadEditRecord(CadEditRecordKind.Erased, insert.Handle));
                    }
                }
            }

            planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Detach xref", string.Join(",", names), null));
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string name in names)
                {
                    s.Database.DetachXref(s.Database.BlockTable[name]);
                }
            });

            if (result.Succeeded)
            {
                foreach (string name in names)
                {
                    c.Document.Xrefs.SetUnloaded(name, false);
                }
            }
        }

        /// <summary>-XREF Reload: the file found again and read anew, in AutoCAD 2021's words.</summary>
        private static void ReloadXref(CadCommandContext c, DxfBlockTableRecord block)
        {
            c.Write("Reload Xref \"" + block.Name + "\": " + block.PathName);
            CadXrefManager xrefs = c.Document.Xrefs;
            xrefs.SetUnloaded(block.Name, false);
            string found = xrefs.Locate(block.PathName);

            if (found == null)
            {
                c.Write("\"" + block.PathName + "\" cannot be found.");
                return;
            }

            xrefs.Forget(found);
            c.Write(xrefs.Read(found).Database == null ? "Error: Unable to load " + found + "." : "\"" + block.Name + "\" loaded.");
        }

        /// <summary>Draws every space again after an external reference or image was unloaded or reloaded, which changes no record.</summary>
        private static void Redisplay(CadCommandContext c, int count)
        {
            if (count > 0)
            {
                c.Document.Regen();
            }
        }

        /// <summary>-XREF Path: the old path shown and a new one asked until one names a file that is found, or Enter keeps it.</summary>
        private static IEnumerable<CadPrompt> EditXrefPath(CadCommandContext c, string name)
        {
            while (true)
            {
                DxfBlockTableRecord block = c.Database.BlockTable[name];
                c.Write("xref name: \"" + name + "\"");
                c.Write("Old path: " + block.PathName);
                yield return CadPrompt.ForString("Enter new path");
                string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                if (typed.Length == 0)
                {
                    c.Write("Path unchanged.");
                    yield break;
                }

                if (c.Document.Xrefs.Locate(typed) == null)
                {
                    c.Write("\"" + typed + "\": Can't find file in search path.");
                    continue;
                }

                string old = block.PathName;
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Xref path", old, typed) }, s => s.Database.BlockTable[name].PathName = typed);
                yield break;
            }
        }

        /// <summary>-XREF pathType: each path found written anew as Full, Relative or None, in one undo step; one not found is not changed.</summary>
        private static void SetXrefPathType(CadCommandContext c, List<DxfBlockTableRecord> blocks, CadPathType type)
        {
            var paths = new List<KeyValuePair<string, string>>();
            int unchanged = 0;

            foreach (DxfBlockTableRecord block in blocks)
            {
                string found = c.Document.Xrefs.Locate(block.PathName);
                string path = found == null ? null : CadXrefManager.PathFor(found, type, c.Document.DrawingFolder);

                if (path == null || string.Equals(path, block.PathName, StringComparison.Ordinal))
                {
                    unchanged++;
                    continue;
                }

                paths.Add(new KeyValuePair<string, string>(block.Name, path));
            }

            if (paths.Count > 0)
            {
                CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Xref path type", null, type.ToString()) }, s =>
                {
                    foreach (KeyValuePair<string, string> pair in paths)
                    {
                        s.Database.BlockTable[pair.Key].PathName = pair.Value;
                    }
                });

                if (result.Succeeded)
                {
                    c.Write(paths.Count.ToString(CultureInfo.InvariantCulture) + " path(s) changed.");
                }
            }

            if (unchanged > 0)
            {
                c.Write(unchanged.ToString(CultureInfo.InvariantCulture) + " path(s) not changed.");
            }
        }

        /// <summary>-XREF Attach and Overlay, and XATTACH with no window: the file's name typed, then every placing asked.</summary>
        private static IEnumerable<CadPrompt> AttachTyped(CadCommandContext c, bool overlay)
        {
            yield return CadPrompt.ForString(overlay ? "Enter name of file to overlay" : "Enter name of file to attach");
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

            if (typed.Length == 0)
            {
                yield break;
            }

            var request = new CadAttachRequest(c.Document, CadAttachKind.Xref) { IsOverlay = overlay, SpecifyScaleOnScreen = true, SpecifyRotationOnScreen = true, UniformScale = false };
            string name = NameBefore(ref typed);
            request.FileName = FileOf(c, typed, ".dxf");

            if (name != null)
            {
                request.Name = name;
            }

            foreach (CadPrompt prompt in PlaceXref(c, request))
            {
                yield return prompt;
            }
        }

        /// <summary>The name of <c>name=path</c>, the path left; null where none is given.</summary>
        private static string NameBefore(ref string typed)
        {
            int equals = typed.IndexOf('=');

            if (equals <= 0)
            {
                return null;
            }

            string name = typed.Substring(0, equals).Trim();
            typed = typed.Substring(equals + 1).Trim().Trim('"');
            return name;
        }

        /// <summary>The full path of a file typed: as typed where it names one, else found where an external reference is found; the extension given where it has none.</summary>
        private static string FileOf(CadCommandContext c, string typed, string extension)
        {
            try
            {
                if (!Path.HasExtension(typed))
                {
                    typed += extension;
                }

                if (Path.IsPathRooted(typed))
                {
                    return typed;
                }

                string found = c.Document.Xrefs.Locate(typed);

                if (found != null)
                {
                    return found;
                }

                string folder = c.Document.DrawingFolder;
                return folder == null ? Path.GetFullPath(typed) : Path.Combine(folder, typed);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is NotSupportedException || exception is PathTooLongException)
            {
                return typed;
            }
        }

        /// <summary>
        /// Attaches or overlays the drawing a request names and places its INSERT, in one undo step: the reference reused
        /// where the drawing has one of the name and file, refused where the name is a block's or another file's.
        /// </summary>
        private static IEnumerable<CadPrompt> PlaceXref(CadCommandContext c, CadAttachRequest request)
        {
            if (request.Error != null)
            {
                c.Write(request.Error);
                yield break;
            }

            string name = request.Name;

            if (!IsBlockName(name) || name.IndexOf('|') >= 0)
            {
                c.Write("Invalid xref name \"" + name + "\".");
                yield break;
            }

            if (c.Database.BlockTable.Has(name))
            {
                DxfBlockTableRecord existing = c.Database.BlockTable[name];

                if (!existing.IsFromExternalReference)
                {
                    c.Write("\"" + name + "\" is the name of a block of the drawing: the external reference is not attached.");
                    yield break;
                }

                if (!string.Equals(c.Document.Xrefs.Locate(existing.PathName), request.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("Xref \"" + name + "\" names another file: give this one a name of its own, as name=path.");
                    yield break;
                }

                c.Write("Xref \"" + name + "\" has already been defined.");
                name = existing.Name;
            }
            else
            {
                c.Write((request.IsOverlay ? "Overlay" : "Attach") + " Xref \"" + name + "\": " + request.SavedPath);
                c.Write("\"" + name + "\" loaded.");
            }

            var placing = new XrefPlacing { Point = request.Point, Scale = request.Scale, Rotation = request.Rotation };

            foreach (CadPrompt prompt in AskPlacing(c, placing, request.SpecifyPointOnScreen, request.SpecifyScaleOnScreen, request.SpecifyRotationOnScreen, request.UniformScale))
            {
                yield return prompt;
            }

            if (!placing.Given)
            {
                yield break;
            }

            double factor = request.UnitFactor;
            double angle = c.Session.Ucs.AngleToWorld(placing.Rotation);
            GeoPoint3 point = placing.Point;
            GeoVector3 scale = placing.Scale;
            string saved = request.SavedPath;
            bool overlay = request.IsOverlay;
            CadSpace space = c.Space;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, overlay ? "Overlay xref" : "Attach xref", null, name) }, s =>
            {
                DxfBlockTableRecord block = s.Database.BlockTable.Has(name) ? s.Database.BlockTable[name] : overlay ? s.Database.OverlayXref(saved, name) : s.Database.AttachXref(saved, name);
                var insert = new DxfBlockReference(point, block);
                s.Append(space, Current(c, insert));
                insert.ScaleFactors = new GeoVector3(scale.X * factor, scale.Y * factor, scale.Z * factor);
                insert.Rotation = angle;
            });
        }

        /// <summary>Where an external reference is placed, given or asked, and whether it was.</summary>
        private sealed class XrefPlacing
        {
            public GeoPoint3 Point { get; set; }

            public GeoVector3 Scale { get; set; }

            public double Rotation { get; set; }

            public bool Given { get; set; }
        }

        /// <summary>
        /// Asks what is to be specified on screen, as AutoCAD's -XREF Attach asks it: "Specify insertion point or
        /// [Scale/X/Y/Z/Rotate/PScale/PX/PY/PZ/PRotate]:" (a P option sets the same as its plain one), "Enter X scale factor,
        /// specify opposite corner, or [Corner/XYZ] &lt;1&gt;:" and "Enter Y scale factor &lt;use X scale factor&gt;:", or
        /// "Specify scale factor for XYZ axes &lt;1&gt;:" where it is scaled alike along every axis, and "Specify rotation angle
        /// &lt;0&gt;:".
        /// </summary>
        private static IEnumerable<CadPrompt> AskPlacing(CadCommandContext c, XrefPlacing placing, bool askPoint, bool askScale, bool askRotation, bool uniform)
        {
            bool scaleGiven = false;
            bool rotationGiven = false;

            while (askPoint)
            {
                yield return CadPrompt.ForPoint("Specify insertion point", null, XrefPointKeywords);

                if (c.Input.IsPoint)
                {
                    placing.Point = P3(c.Input.Point);
                    break;
                }

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                string option = c.Input.Text.StartsWith("P", StringComparison.Ordinal) && c.Input.Text.Length > 1 ? c.Input.Text.Substring(1) : c.Input.Text;

                if (option == "Rotate")
                {
                    yield return CadPrompt.ForAngle("Specify rotation angle", null, null, Number(0.0)).Turning();

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        placing.Rotation = c.Input.Number;
                        rotationGiven = true;
                    }

                    continue;
                }

                string axis = option == "Scale" ? null : option;
                yield return CadPrompt.ForDistance(axis == null ? "Specify scale factor for XYZ axes" : "Specify " + axis + " scale factor", null, null, Length(1.0));

                if (c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0)
                {
                    double f = c.Input.Number;
                    GeoVector3 now = placing.Scale;
                    placing.Scale = axis == null ? new GeoVector3(f, f, f) : axis == "X" ? new GeoVector3(f, now.Y, now.Z) : axis == "Y" ? new GeoVector3(now.X, f, now.Z) : new GeoVector3(now.X, now.Y, f);
                    scaleGiven = true;
                }
            }

            if (askScale && !scaleGiven)
            {
                if (uniform)
                {
                    yield return CadPrompt.ForDistance("Specify scale factor for XYZ axes", null, null, Length(1.0));
                    double f = c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0 ? c.Input.Number : 1.0;
                    placing.Scale = new GeoVector3(f, f, f);
                }
                else
                {
                    var at = new GeoPoint2(placing.Point.X, placing.Point.Y);
                    yield return CadPrompt.ForPoint("Enter X scale factor, specify opposite corner,", null, new[] { "Corner", "XYZ" }, "1", true);
                    double? x = null;
                    double? y = null;
                    double? z = null;

                    if (c.Input.Is("Corner"))
                    {
                        yield return CadPrompt.ForPoint("Specify opposite corner", at);
                    }
                    else if (c.Input.Is("XYZ"))
                    {
                        yield return CadPrompt.ForDistance("Specify X scale factor", null, null, Length(1.0));
                        x = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                        yield return CadPrompt.ForDistance("Specify Y scale factor", null, null, "use X scale factor");
                        y = c.Input.Kind == CadInputKind.Number ? c.Input.Number : x;
                        yield return CadPrompt.ForDistance("Specify Z scale factor", null, null, "use X scale factor");
                        z = c.Input.Kind == CadInputKind.Number ? c.Input.Number : x;
                    }

                    if (c.Input.IsPoint)
                    {
                        GeoVector2 corner = c.Input.Point - at;
                        x = corner.X;
                        y = corner.Y;
                    }
                    else if (x == null && c.Input.Kind == CadInputKind.Text)
                    {
                        if (!double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed) || typed == 0.0 || double.IsInfinity(typed) || double.IsNaN(typed))
                        {
                            c.Write("Requires a nonzero number, a point or an option keyword.");
                            yield break;
                        }

                        x = typed;
                    }

                    x = x ?? 1.0;

                    if (y == null)
                    {
                        yield return CadPrompt.ForDistance("Enter Y scale factor", null, null, "use X scale factor");
                        y = c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0 ? c.Input.Number : x;
                    }

                    if (x.Value == 0.0 || y.Value == 0.0)
                    {
                        c.Write("Value must be nonzero.");
                        yield break;
                    }

                    placing.Scale = new GeoVector3(x.Value, y.Value, z ?? x.Value);
                }
            }

            if (askRotation && !rotationGiven)
            {
                yield return CadPrompt.ForAngle("Specify rotation angle", new GeoPoint2(placing.Point.X, placing.Point.Y), null, Number(0.0)).Turning();

                if (c.Input.Kind == CadInputKind.Number)
                {
                    placing.Rotation = c.Input.Number;
                }
            }

            placing.Given = true;
        }

        /// <summary>
        /// -IMAGE (-IM): "Enter image option [?/Detach/Path/Reload/Unload/Attach] &lt;Attach&gt;:", each option asking for a list
        /// of image names with wildcards and commas, in AutoCAD 2021's words.
        /// </summary>
        private static IEnumerable<CadPrompt> ImageTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter image option", ImageOptions, "Attach");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "Attach")
            {
                foreach (CadPrompt prompt in ImageAttachTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            string ask = option == "?" ? "Images to list" : option == "Path" ? "Enter list of images for path modification" : "Enter list of images to " + option.ToLowerInvariant();
            yield return CadPrompt.ForString(ask, option == "?" ? "*" : null);
            string names = c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : option == "?" ? "*" : null;

            if (names == null)
            {
                yield break;
            }

            List<KeyValuePair<string, DxfRasterImageDef>> images = ImagesNamed(c.Database, names);

            switch (option)
            {
                case "?":
                    ListImages(c, images);
                    break;
                case "Detach":
                    DetachImages(c, images);
                    break;
                case "Unload":
                    foreach (KeyValuePair<string, DxfRasterImageDef> image in images)
                    {
                        c.Document.Xrefs.SetImageLoaded(image.Key, false);
                    }

                    Redisplay(c, images.Count);
                    break;
                case "Reload":
                    foreach (KeyValuePair<string, DxfRasterImageDef> image in images)
                    {
                        c.Write("  Reloading...");
                        c.Document.Xrefs.SetImageLoaded(image.Key, true);
                        string found = c.Document.Xrefs.Locate(image.Value.SourceFileName);
                        c.Write("Reload image " + image.Key + ": " + (found ?? image.Value.SourceFileName));
                        c.Write(found == null ? image.Key + ": Image file not found." : image.Key + " loaded and relinked.");
                    }

                    Redisplay(c, images.Count);
                    break;
                case "Path":
                    foreach (KeyValuePair<string, DxfRasterImageDef> image in images)
                    {
                        c.Write("Image name: " + image.Key);
                        c.Write("Old path:   " + image.Value.SourceFileName);
                        yield return CadPrompt.ForString("Enter New path");
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

                        if (typed.Length > 0 && !string.Equals(typed, image.Value.SourceFileName, StringComparison.Ordinal))
                        {
                            string key = image.Key;
                            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Image path", image.Value.SourceFileName, typed) }, s =>
                                ((DxfRasterImageDef)CadXrefManager.Images(s.Database).GetAt(key)).SourceFileName = typed);
                        }
                    }

                    break;
            }
        }

        /// <summary>The image definitions of a drawing whose names match a list of names with wildcards, in ordinal order.</summary>
        private static List<KeyValuePair<string, DxfRasterImageDef>> ImagesNamed(DxfDatabase database, string names)
        {
            string[] patterns = names.Split(',').Select(p => p.Trim().Trim('"')).Where(p => p.Length > 0).ToArray();
            var found = new List<KeyValuePair<string, DxfRasterImageDef>>();
            DxfDBDictionary dictionary = CadXrefManager.Images(database);

            if (dictionary != null)
            {
                foreach (DxfDBDictionaryEntry entry in dictionary)
                {
                    if (entry.Value is DxfRasterImageDef definition && patterns.Any(p => CadWildcard.IsMatch(entry.Key, p, true)))
                    {
                        found.Add(new KeyValuePair<string, DxfRasterImageDef>(entry.Key, definition));
                    }
                }
            }

            found.Sort((a, b) =>
            {
                int byName = string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase);
                return byName != 0 ? byName : string.CompareOrdinal(a.Key, b.Key);
            });
            return found;
        }

        /// <summary>-IMAGE ?: the table AutoCAD 2021 prints, each definition with its images and path, and the counts.</summary>
        private static void ListImages(CadCommandContext c, List<KeyValuePair<string, DxfRasterImageDef>> images)
        {
            c.Write("Image Name".PadRight(33) + "Instances  Path");
            c.Write(new string('-', 16).PadRight(33) + new string('-', 9) + "  " + new string('-', 10));
            int instances = 0;

            foreach (KeyValuePair<string, DxfRasterImageDef> image in images)
            {
                int count = image.Value.GetEntityCount(out _);
                instances += count;
                c.Write(image.Key.PadRight(33) + count.ToString(CultureInfo.InvariantCulture).PadLeft(9) + "  " + image.Value.SourceFileName);
            }

            c.Write("Total image definitions: " + images.Count.ToString(CultureInfo.InvariantCulture));
            c.Write("Total image instances: " + instances.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>-IMAGE Detach: each definition erased with the images that show it, as AutoCAD's detach, in one undo step; the images in the spaces asked through the owner gate.</summary>
        private static void DetachImages(CadCommandContext c, List<KeyValuePair<string, DxfRasterImageDef>> images)
        {
            if (images.Count == 0)
            {
                return;
            }

            var planned = new List<CadEditRecord>();
            var handles = new HashSet<string>(images.Select(i => i.Value.Handle), StringComparer.OrdinalIgnoreCase);

            foreach (CadSpace space in c.Document.Spaces)
            {
                foreach (DxfEntity entity in space.Block)
                {
                    if (entity is DxfRasterImage image && !(entity is DxfWipeout) && image.ImageDef != null && handles.Contains(image.ImageDef.Handle))
                    {
                        planned.Add(new CadEditRecord(CadEditRecordKind.Erased, entity.Handle));
                    }
                }
            }

            var names = images.Select(i => i.Key).ToList();
            planned.Add(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Detach image", string.Join(",", names), null));
            c.Apply(planned, s =>
            {
                DxfDBDictionary dictionary = CadXrefManager.Images(s.Database);

                foreach (string name in names)
                {
                    dictionary.GetAt(name).Erase();
                }
            });
        }

        /// <summary>-IMAGE Attach and IMAGEATTACH with no window: "Enter image file name to attach:", as name=path to name it, then every placing asked.</summary>
        private static IEnumerable<CadPrompt> ImageAttachTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter image file name to attach");
            string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : string.Empty;

            if (typed.Length == 0)
            {
                yield break;
            }

            var request = new CadAttachRequest(c.Document, CadAttachKind.Image) { SpecifyScaleOnScreen = true, SpecifyRotationOnScreen = true };
            string name = NameBefore(ref typed);
            request.FileName = FileOf(c, typed, ".png");

            if (name != null)
            {
                request.Name = name;
            }

            foreach (CadPrompt prompt in PlaceImage(c, request, true))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// Attaches the picture a request names and places its IMAGE, in one undo step, as AutoCAD 2021's -IMAGE Attach asks:
        /// "Specify insertion point &lt;0,0&gt;:", "Base image size: Width: 10.583333, Height: 7.9375, Millimeters", "Specify
        /// scale factor &lt;1&gt;:" and "Specify rotation angle &lt;0&gt;:". A definition of the name and path is reused; one of the
        /// name and another path is refused, as AutoCAD refuses it.
        /// </summary>
        private static IEnumerable<CadPrompt> PlaceImage(CadCommandContext c, CadAttachRequest request, bool typed)
        {
            if (request.Error != null)
            {
                c.Write(request.Error);
                yield break;
            }

            string name = request.Name;
            DxfDBDictionary images = CadXrefManager.Images(c.Database);

            if (images != null && images.Contains(name))
            {
                var existing = images.GetAt(name) as DxfRasterImageDef;

                if (existing == null || !string.Equals(c.Document.Xrefs.Locate(existing.SourceFileName), request.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    c.Write("Warning: The image name " + name + " is already in use. Use the syntax <imagename>=<filename>.");
                    yield break;
                }
            }

            GeoPoint3 point = request.Point;
            double scale = request.Scale.X;
            double rotation = request.Rotation;

            if (request.SpecifyPointOnScreen)
            {
                yield return CadPrompt.ForPoint("Specify insertion point", null, null, typed ? "0,0" : null);

                if (c.Input.IsPoint)
                {
                    point = P3(c.Input.Point);
                }
                else if (!c.Input.IsNone || !typed)
                {
                    yield break;
                }
            }

            if (request.SpecifyScaleOnScreen)
            {
                c.Write("Base image size: Width: " + Plain(request.BaseWidth) + ", Height: " + Plain(request.BaseHeight) + ", " + UnitName(c.Database.Insunits));
                yield return CadPrompt.ForDistance("Specify scale factor", new GeoPoint2(point.X, point.Y), null, "1");

                if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                {
                    scale = c.Input.Number;
                }
            }

            if (request.SpecifyRotationOnScreen)
            {
                yield return CadPrompt.ForAngle("Specify rotation angle", new GeoPoint2(point.X, point.Y), null, Number(0.0)).Turning();

                if (c.Input.Kind == CadInputKind.Number)
                {
                    rotation = c.Input.Number;
                }
            }

            double angle = c.Session.Ucs.AngleToWorld(rotation);
            double width = request.BaseWidth * scale;
            double height = request.BaseHeight * scale;
            var u = new GeoVector3(width * Math.Cos(angle), width * Math.Sin(angle), 0.0);
            var v = new GeoVector3(-height * Math.Sin(angle), height * Math.Cos(angle), 0.0);
            string saved = request.SavedPath;
            var pixels = new GeoVector2(request.PixelWidth, request.PixelHeight);
            CadSpace space = c.Space;
            c.Apply(null, s =>
            {
                DxfDBDictionary dictionary = CadXrefManager.Images(s.Database) ?? DxfRasterImageDef.CreateImageDictionary(s.Database);
                DxfRasterImageDef definition;

                if (dictionary.Contains(name))
                {
                    definition = (DxfRasterImageDef)dictionary.GetAt(name);
                }
                else
                {
                    definition = new DxfRasterImageDef(saved, pixels);
                    dictionary.SetAt(name, definition);
                }

                var image = new DxfRasterImage(definition);
                image.SetOrientation(point, u, v);
                s.Append(space, Current(c, image));
            });
        }

        /// <summary>A number as AutoCAD prints a size: up to six places, no zeros after the last figure; 1, 0.75, 10.583333.</summary>
        private static string Plain(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        /// <summary>
        /// IMAGECLIP (ICL): "Select image to clip:", then "Enter image clipping option [ON/OFF/Delete/New boundary] &lt;New&gt;:":
        /// ON and OFF switch the clip, Delete takes the boundary away, New boundary asks a rectangle, a polygon or a polyline,
        /// Invert clip switching between hiding what lies outside and what lies inside, as AutoCAD 2021 asks.
        /// </summary>
        private static IEnumerable<CadPrompt> ImageClip(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select image to clip");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];

            if (!(c.Document.FindEntity(handle) is DxfRasterImage picked) || picked is DxfWipeout)
            {
                c.Write("Object selected is not an image.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter image clipping option", new[] { "ON", "OFF", "Delete", "New boundary" }, "New");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;

            if (option == "ON" || option == "OFF")
            {
                bool on = option == "ON";
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Clipping", null, on ? "On" : "Off") }, s => ((DxfRasterImage)s.Document.FindEntity(handle)).IsClipped = on);
                yield break;
            }

            if (option == "Delete")
            {
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Clip boundary", null, "Whole image") }, s => ((DxfRasterImage)s.Document.FindEntity(handle)).SetClipBoundaryToWholeImage());
                yield break;
            }

            if (Clipped(picked))
            {
                yield return CadPrompt.ForKeyword("Delete old boundary?", new[] { "No", "Yes" }, "Yes");

                if (c.Input.Is("No"))
                {
                    yield break;
                }
            }

            bool inverted = false;
            List<GeoPoint2> corners = null;
            DxfClipBoundaryType type = DxfClipBoundaryType.Rectangle;

            while (corners == null)
            {
                c.Write(inverted ? "Inside mode - Objects inside boundary will be hidden." : "Outside mode - Objects outside boundary will be hidden.");
                yield return CadPrompt.ForKeyword("Specify clipping boundary or select invert option", new[] { "Select polyline", "Polygonal", "Rectangular", "Invert clip" }, "Rectangular");

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("Invert clip"))
                {
                    inverted = !inverted;
                    continue;
                }

                if (c.Input.Is("Rectangular"))
                {
                    yield return CadPrompt.ForPoint("Specify first corner point");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify opposite corner point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    GeoPoint2 second = c.Input.Point;
                    corners = new List<GeoPoint2> { first, new GeoPoint2(first.X, second.Y), second, new GeoPoint2(second.X, first.Y), first };
                    continue;
                }

                if (c.Input.Is("Polygonal"))
                {
                    var points = new List<GeoPoint2>();
                    yield return CadPrompt.ForPoint("Specify first point");

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    points.Add(c.Input.Point);

                    while (true)
                    {
                        yield return CadPrompt.ForPoint("Specify next point", points[points.Count - 1], points.Count >= 3 ? new[] { "Close", "Undo" } : new[] { "Undo" });

                        if (c.Input.IsPoint)
                        {
                            points.Add(c.Input.Point);
                            continue;
                        }

                        if (c.Input.Is("Undo"))
                        {
                            if (points.Count > 1)
                            {
                                points.RemoveAt(points.Count - 1);
                            }

                            continue;
                        }

                        break;
                    }

                    if (points.Count < 3)
                    {
                        yield break;
                    }

                    points.Add(points[0]);
                    corners = points;
                    type = DxfClipBoundaryType.Poly;
                    continue;
                }

                yield return CadPrompt.ForEntity("Select polyline");

                if (c.Input.Kind != CadInputKind.Selection || !(c.Document.FindEntity(c.Input.Handles[0]) is DxfPolyline outline) || !outline.Closed || HasArcs(outline))
                {
                    c.Write("The boundary is a closed polyline of straight segments.");
                    yield break;
                }

                corners = new List<GeoPoint2>();

                for (int i = 0; i < outline.NumberOfVertices; i++)
                {
                    GeoPoint3 at = outline.GetPoint3dAt(i);
                    corners.Add(new GeoPoint2(at.X, at.Y));
                }

                corners.Add(corners[0]);
                type = DxfClipBoundaryType.Poly;
            }

            if (inverted && string.CompareOrdinal(c.Database.Version ?? string.Empty, "AC1024") < 0)
            {
                c.Write("An inverted clip needs a drawing of R2010 or later.");
                yield break;
            }

            List<GeoPoint2> world = corners;
            DxfClipBoundaryType kind = type;
            bool invert = inverted;
            Tolerance tolerance = c.Session.Tolerance;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Clip boundary", null, kind == DxfClipBoundaryType.Rectangle ? "Rectangular" : "Polygonal") }, s =>
            {
                var image = (DxfRasterImage)s.Document.FindEntity(handle);
                GeoTransform3 toPixels = image.PixelToModelTransform.Inverse(tolerance);
                var pixels = new List<GeoPoint2>();

                foreach (GeoPoint2 corner in world)
                {
                    GeoPoint3 pixel = toPixels.Transform(new GeoPoint3(corner.X, corner.Y, 0.0));
                    pixels.Add(new GeoPoint2(pixel.X, pixel.Y));
                }

                pixels[pixels.Count - 1] = pixels[0];
                image.SetClipBoundary(kind, pixels);
                image.IsClipped = true;

                if (invert || image.IsClipInverted)
                {
                    image.IsClipInverted = invert;
                }
            });
        }

        /// <summary>Whether an image is clipped by a boundary of its own rather than its whole picture.</summary>
        private static bool Clipped(DxfRasterImage image)
        {
            IReadOnlyList<GeoPoint2> boundary = image.GetClipBoundary();
            int across = (int)image.ImageWidth;
            int up = (int)image.ImageHeight;
            return image.IsClipped && !(image.ClipBoundaryType == DxfClipBoundaryType.Rectangle && boundary.Count == 5
                && boundary.Min(p => p.X) == -0.5 && boundary.Min(p => p.Y) == -0.5 && boundary.Max(p => p.X) == across - 0.5 && boundary.Max(p => p.Y) == up - 0.5);
        }

        private static bool HasArcs(DxfPolyline polyline)
        {
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                if (polyline.GetBulgeAt(i) != 0.0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>TRANSPARENCY: "Select image(s):", then "Enter transparency mode [ON/OFF] &lt;OFF&gt;:", the transparent pixels of a picture letting what is under it show while ON.</summary>
        private static IEnumerable<CadPrompt> Transparency(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select image(s)");
            var images = new List<string>();

            foreach (string handle in c.Input.Handles ?? Array.Empty<string>())
            {
                if (c.Document.FindEntity(handle) is DxfRasterImage image && !(image is DxfWipeout))
                {
                    images.Add(handle);
                }
            }

            if (images.Count == 0)
            {
                yield break;
            }

            bool now = ((DxfRasterImage)c.Document.FindEntity(images[0])).ImageTransparency;
            yield return CadPrompt.ForKeyword("Enter transparency mode", new[] { "ON", "OFF" }, now ? "ON" : "OFF");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            bool on = c.Input.Is("ON");
            c.Apply(images.Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, "Transparency", null, on ? "On" : "Off")).ToList(), s =>
            {
                foreach (string handle in images)
                {
                    ((DxfRasterImage)s.Document.FindEntity(handle)).ImageTransparency = on;
                }
            });
        }
    }
}
