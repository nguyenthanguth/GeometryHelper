using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Batch 5 of acad.pgp's commands, output: EXPORTPDF, EXPORTDWF, EXPORTDWFX, IMPORT and PLOTSTAMP with -PLOTSTAMP, and
    /// the PDFSHX variable (in CadCommands.SystemVariables.cs) that PLOT, -PLOT, EXPORTPDF and -EXPORT read. The PDF is
    /// written by the window's writer, as -PLOT's is; the plot stamp and the SHX text ride on the plot's settings
    /// (<see cref="CadPlotSettings.PlotStamp"/>, <see cref="CadPlotSettings.PdfShx"/>).
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] StampOptions =
        {
            "On",
            "OFF",
            "Fields",
            "User Fields",
            "Log file",
            "LOCation",
            "Text Properties",
            "UNits",
        };

        internal static void AddOutputBatchTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("EXPORTPDF", ExportPdfCommand));
            registry.Add(new CadCommand("EXPORTDWF", c => ExportDwf(c, "DWF")));
            registry.Add(new CadCommand("EXPORTDWFX", c => ExportDwf(c, "DWFx")));
            registry.Add(new CadCommand("IMPORT", Import));
            registry.Add(new CadCommand("PLOTSTAMP", PlotStampDialog));
            registry.Add(new CadCommand("-PLOTSTAMP", PlotStamp));
        }

        /// <summary>
        /// Gives a plot the session's plot stamp, as a copy, where it is on, and the SHX text of PDFSHX 1, so that the PDF
        /// writer reads them from the settings.
        /// </summary>
        private static void WithOutputOptions(CadCommandContext c, CadPlotSettings settings)
        {
            settings.PlotStamp = c.Session.PlotStamp.On ? c.Session.PlotStamp.Clone() : null;
            settings.PdfShx = Kept(c.Session, "pdfshx", 0.0) != 0.0;
        }

        /// <summary>
        /// EXPORTPDF: with the window, its Save As PDF dialog for the file and the options AutoCAD 2021's shows beside it
        /// (<see cref="CadExportJob.PdfOptions"/>): in a layout the current layout or all layouts, in model space the display, the
        /// extents or a window, its corners asked once the dialog is closed where it gave none; the page setup in use, or the
        /// one Page Setup Override set; the plot stamp, which becomes the session's; the objects' hyperlinks; and the
        /// PDF opened in the viewer when written (the request OPENINVIEWER, the file its payload). With FILEDIA 0, or with no
        /// window to show it, the questions of -EXPORT's Pdf on the command line: the plot area, "Detailed plot configuration?
        /// [Yes/No] &lt;No&gt;:" for the paper, the scale and the plot styles, and "Enter file name &lt;file&gt;:". AutoCAD 2021's
        /// console asks nothing of EXPORTPDF, so the questions are -EXPORT's, All layouts among them.
        /// </summary>
        private static IEnumerable<CadPrompt> ExportPdfCommand(CadCommandContext c)
        {
            CadSpace space = c.Space;
            string offered = PlotFileName(c.Document, space);
            CadPlotSettings inUse = CadPlotSettings.Read(c.Document, space);
            inUse.UseDevice(CadPlotSettings.PdfDevice);
            var options = new CadExportPdfOptions(space.IsModel, inUse.Clone(), c.Session.PlotStamp.Clone());
            var job = new CadExportJob("Save As PDF", new[] { CadExportJob.Pdf }, CadExportJob.Pdf, offered)
            {
                PdfOptions = options,
            };

            if (!c.Session.FileDialogs || !c.Session.Request("EXPORTPDF", c.Document, job))
            {
                foreach (CadPrompt prompt in ExportPdf(c, "EXPORTPDF"))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (job.FileName == null)
            {
                yield break;
            }

            DisplayBox window = options.WindowBox;

            if (options.Export == CadExportPdfOptions.Window && window.IsEmpty)
            {
                yield return CadPrompt.ForPoint("Specify first corner");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify opposite corner", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                window = new DisplayBox(
                    Math.Min(first.X, second.X),
                    Math.Min(first.Y, second.Y),
                    Math.Max(first.X, second.X),
                    Math.Max(first.Y, second.Y)
                );
            }

            // The dialog's stamp is the session's from now on, on or off as Include plot stamp left it.
            c.Session.PlotStamp.CopyFrom(options.PlotStamp);
            c.Session.PlotStamp.On = options.IncludePlotStamp;

            void WithDialogOptions(CadPlotSettings settings)
            {
                WithOutputOptions(c, settings);
                settings.PdfHyperlinks = options.IncludeHyperlinks;
            }

            CadPlotSettings overridden = options.OverridePageSetup ? options.PageSetupOverride.Clone() : null;

            if (overridden != null)
            {
                overridden.UseDevice(CadPlotSettings.PdfDevice);
            }

            string written;

            if (options.Export == CadExportPdfOptions.AllLayouts)
            {
                written = ExportedPdfOfLayouts(c, overridden, job.FileName, WithDialogOptions);
            }
            else
            {
                CadPlotSettings settings = overridden ?? inUse;
                settings.Area =
                    options.Export == CadExportPdfOptions.CurrentLayout ? CadPlotArea.Layout
                    : options.Export == CadExportPdfOptions.Extents ? CadPlotArea.Extents
                    : options.Export == CadExportPdfOptions.Window ? CadPlotArea.Window
                    : CadPlotArea.Display;
                settings.Window = settings.Area == CadPlotArea.Window ? window : settings.Window;
                WithDialogOptions(settings);
                written = ExportedPdf(c, space, settings, job.FileName);
            }

            if (written != null && options.OpenInViewer)
            {
                c.Session.Request("OPENINVIEWER", c.Document, written);
            }
        }

        /// <summary>EXPORTDWF and EXPORTDWFX: the presenter has no DWF writer, so they say so and write nothing; EXPORTPDF writes a PDF.</summary>
        private static IEnumerable<CadPrompt> ExportDwf(CadCommandContext c, string format)
        {
            c.Write(format + " files have no writer in the presenter: nothing was exported. EXPORTPDF writes a PDF.");
            yield break;
        }

        /// <summary>
        /// IMPORT: the file by the window's Import File dialog, or with FILEDIA 0 "Enter import file name:" as AutoCAD 2021's
        /// console asks it; a name that is not a file there is answered "Can't find file.". A DWG or DXF drawing is brought in
        /// as a block of the file's name and placed as INSERT places one, its insertion point, scale and rotation asked. Any
        /// other type (WMF, ACIS, 3DS and the rest of AutoCAD's) has no reader here and is refused, nothing imported.
        /// </summary>
        private static IEnumerable<CadPrompt> Import(CadCommandContext c)
        {
            var job = new CadExportJob("Import File", new[] { CadExportJob.Drawing }, CadExportJob.Drawing, null)
            {
                Opens = true,
            };
            string file;

            if (c.Session.FileDialogs && c.Session.Request("IMPORT", c.Document, job))
            {
                if (job.FileName == null)
                {
                    yield break;
                }

                file = job.FileName;
            }
            else
            {
                yield return CadPrompt.ForString("Enter import file name");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    yield break;
                }

                file = c.Input.Text.Trim().Trim('"');
            }

            if (!File.Exists(file))
            {
                c.Write("Can't find file.");
                yield break;
            }

            if (!CadDrawingFile.IsDrawingName(file))
            {
                string extension = Path.GetExtension(file);
                c.Write(
                    (extension.Length == 0 ? "A file of no type" : extension.ToUpperInvariant() + " files")
                        + " cannot be imported by the presenter, which reads a DWG or a DXF drawing: nothing was imported."
                );
                yield break;
            }

            string name = null;

            foreach (
                CadPrompt prompt in BringInFile(c, file, Path.GetFileNameWithoutExtension(file), false, n => name = n)
            )
            {
                yield return prompt;
            }

            if (name == null)
            {
                yield break;
            }

            var placing = new Placing { Scale = new GeoVector3(1.0, 1.0, 1.0) };

            foreach (CadPrompt prompt in Place(c, name, placing, true, true, true, false))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// PLOTSTAMP and -PLOTSTAMP: "Enter an option [On/OFF/Fields/User Fields/Log file/LOCation/Text Properties/UNits]:", as
        /// AutoCAD 2021's console asks it, each option asking its questions in AutoCAD's words and the main question coming back
        /// after it, until Enter. On and OFF switch the stamp; Fields asks "Stamp drawing name? [Yes/No]" for each of the seven
        /// fields; User Fields asks two texts, "." clearing one; Log file asks whether to write the log and its path; LOCation
        /// asks the corner, the orientation, upside down, the offset and what it is measured from; Text Properties the font, the
        /// height and a single line; UNits Inches, Millimeters or Pixels. The settings are kept for the session and stamp every
        /// PDF written while the stamp is on. PLOTSTAMP shows the window's Plot Stamp dialog (CadCommands.DialogsFive.cs) and asks these where there is none.
        /// </summary>
        private static IEnumerable<CadPrompt> PlotStamp(CadCommandContext c)
        {
            CadPlotStamp stamp = c.Session.PlotStamp;

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", StampOptions).Hiding("User", "Text");

                if (c.Input.Is("On"))
                {
                    stamp.On = true;
                }
                else if (c.Input.Is("OFF"))
                {
                    stamp.On = false;
                }
                else if (c.Input.Is("Fields"))
                {
                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp drawing name?", stamp.DrawingName, v =>
                            stamp.DrawingName = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp layout name?", stamp.LayoutName, v =>
                            stamp.LayoutName = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp date and time?", stamp.DateAndTime, v =>
                            stamp.DateAndTime = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp login name?", stamp.LoginName, v =>
                            stamp.LoginName = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp plot device name?", stamp.DeviceName, v =>
                            stamp.DeviceName = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp paper size?", stamp.PaperSize, v =>
                            stamp.PaperSize = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Stamp plot scale?", stamp.PlotScale, v =>
                            stamp.PlotScale = v
                        )
                    )
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("User Fields") || c.Input.Is("User"))
                {
                    yield return CadPrompt.ForString("Enter user field 1", stamp.UserField1);
                    stamp.UserField1 = UserField(c, stamp.UserField1);
                    yield return CadPrompt.ForString("Enter user field 2", stamp.UserField2);
                    stamp.UserField2 = UserField(c, stamp.UserField2);
                }
                else if (c.Input.Is("Log file"))
                {
                    foreach (
                        CadPrompt prompt in StampField(c, "Write plot stamp to log file?", stamp.WriteLog, v =>
                            stamp.WriteLog = v
                        )
                    )
                    {
                        yield return prompt;
                    }

                    if (stamp.WriteLog)
                    {
                        yield return CadPrompt.ForString("Enter log file path", stamp.LogFile);

                        if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
                        {
                            stamp.LogFile = c.Input.Text.Trim().Trim('"');
                        }
                    }
                }
                else if (c.Input.Is("LOCation"))
                {
                    foreach (CadPrompt prompt in StampLocation(c, stamp))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Text Properties") || c.Input.Is("Text"))
                {
                    yield return CadPrompt.ForString("Enter font name", stamp.FontName);

                    if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0)
                    {
                        stamp.FontName = c.Input.Text.Trim();
                    }

                    while (true)
                    {
                        yield return CadPrompt.ForDistance("Enter text height", null, null, Four(stamp.TextHeight));

                        if (c.Input.IsNone)
                        {
                            break;
                        }

                        if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                        {
                            stamp.TextHeight = c.Input.Number;
                            break;
                        }

                        c.Write("Requires a height above nought.");
                    }

                    foreach (
                        CadPrompt prompt in StampField(c, "Place plot stamp on single line?", stamp.SingleLine, v =>
                            stamp.SingleLine = v
                        )
                    )
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("UNits"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter measurement units",
                        new[] { "Inches", "Millimeters", "Pixels" },
                        stamp.Units.ToString()
                    );

                    if (!c.Input.IsNone)
                    {
                        stamp.Units =
                            c.Input.Is("Millimeters") ? CadPlotStampUnits.Millimeters
                            : c.Input.Is("Pixels") ? CadPlotStampUnits.Pixels
                            : CadPlotStampUnits.Inches;
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>LOCation's questions: the corner, the orientation, upside down, the offset and what it is measured from.</summary>
        private static IEnumerable<CadPrompt> StampLocation(CadCommandContext c, CadPlotStamp stamp)
        {
            yield return CadPrompt.ForKeyword(
                "Enter location",
                new[] { "TL", "TR", "BL", "BR" },
                stamp.Location == CadPlotStampCorner.TopLeft ? "TL"
                    : stamp.Location == CadPlotStampCorner.TopRight ? "TR"
                    : stamp.Location == CadPlotStampCorner.BottomRight ? "BR"
                    : "BL"
            );

            if (!c.Input.IsNone)
            {
                stamp.Location =
                    c.Input.Is("TL") ? CadPlotStampCorner.TopLeft
                    : c.Input.Is("TR") ? CadPlotStampCorner.TopRight
                    : c.Input.Is("BR") ? CadPlotStampCorner.BottomRight
                    : CadPlotStampCorner.BottomLeft;
            }

            yield return CadPrompt.ForKeyword(
                "Enter stamp orientation",
                new[] { "Horizontal", "Vertical" },
                stamp.Vertical ? "Vertical" : "Horizontal"
            );
            stamp.Vertical = c.Input.IsNone ? stamp.Vertical : c.Input.Is("Vertical");

            foreach (
                CadPrompt prompt in StampField(c, "Stamp upside-down?", stamp.UpsideDown, v => stamp.UpsideDown = v)
            )
            {
                yield return prompt;
            }

            while (true)
            {
                yield return CadPrompt.ForString(
                    "Specify plot stamp offset",
                    Four(stamp.OffsetX) + "," + Four(stamp.OffsetY)
                );

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
                {
                    break;
                }

                if (TryPair(c.Input.Text, out double x, out double y))
                {
                    stamp.OffsetX = x;
                    stamp.OffsetY = y;
                    break;
                }

                c.Write("Requires an offset as x,y.");
            }

            yield return CadPrompt.ForKeyword(
                "Specify offset relative to",
                new[] { "paper Border", "printable Area" },
                stamp.OffsetFromPaperBorder ? "paper Border" : "printable Area"
            );
            stamp.OffsetFromPaperBorder = c.Input.IsNone ? stamp.OffsetFromPaperBorder : c.Input.Is("paper Border");
        }

        /// <summary>A Yes/No question whose default is what the stamp has, the answer given to <paramref name="set"/>.</summary>
        private static IEnumerable<CadPrompt> StampField(
            CadCommandContext c,
            string message,
            bool current,
            Action<bool> set
        )
        {
            yield return CadPrompt.ForKeyword(message, PlotYesNo, current ? "Yes" : "No");

            if (!c.Input.IsNone)
            {
                set(c.Input.Is("Yes"));
            }
        }

        /// <summary>The text typed for a user field; Enter keeps the field, a point clears it.</summary>
        private static string UserField(CadCommandContext c, string current) =>
            c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0 ? current
            : c.Input.Text.Trim() == "." ? string.Empty
            : c.Input.Text.Trim();

        private static string Four(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
    }
}
