using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// DWGPROPS, as AutoCAD 2021's Drawing Properties dialog: its Summary texts and custom properties written to the drawing
    /// (the header of a drawing of R2004 or later, the DWGPROPS record of an older one) as one undo step, its General and
    /// Statistics tabs read from the file and the header, and the properties kept through a save as DXF and as DWG.
    /// </summary>
    public class DrawingPropertiesTests : IDisposable
    {
        private readonly string _folder = Path.Combine(
            Path.GetTempPath(),
            "gk",
            "props-" + Guid.NewGuid().ToString("N")
        );

        public DrawingPropertiesTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
                // A file still held is left for the system to clear.
            }
        }

        /// <summary>Runs DWGPROPS with a window that answers as the test says; the request the window was given is handed back.</summary>
        private static CadDrawingPropertiesRequest Props(
            CadDocument document,
            Action<CadDrawingPropertiesRequest> answer
        )
        {
            CadCommandSession session = Session(document);
            CadDrawingPropertiesRequest shown = null;
            session.UiRequested += (sender, e) =>
            {
                if (e.Name == "DWGPROPS")
                {
                    shown = (CadDrawingPropertiesRequest)e.Payload;
                    answer(shown);
                    e.Handled = true;
                }
            };
            Run(session, "DWGPROPS");
            Assert.False(session.IsRunning);
            return shown;
        }

        [Fact]
        public void Dwgprops_WithNoWindow_SaysTheWindowShowsIt()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);

            Run(session, "DWGPROPS");

            Assert.Contains("DWGPROPS is shown by the window.", session.History);
            Assert.Null(document.NextUndo);
        }

        [Fact]
        public void OkWritesTheSummaryAndCustomProperties_AsOneUndoStep_AndUndoTakesThemBack()
        {
            CadDocument document = Blank();

            Props(document, request =>
            {
                request.Title = "Plate B-12";
                request.Subject = "Stiffener";
                request.Author = "J. Doe";
                request.Keywords = "plate, steel";
                request.Comments = "Rev A";
                request.HyperlinkBase = "https://example.com/plans";
                request.CustomProperties.Add(new KeyValuePair<string, string>("Part number", "B-12"));
                request.CustomProperties.Add(new KeyValuePair<string, string>("Weight", "42.5"));
                request.Action = CadDialogAction.Ok;
            });

            DxfDatabaseSummaryInfo info = document.Database.SummaryInfo;
            Assert.Equal("Plate B-12", info.Title);
            Assert.Equal("Stiffener", info.Subject);
            Assert.Equal("J. Doe", info.Author);
            Assert.Equal("plate, steel", info.Keywords);
            Assert.Equal("Rev A", info.Comments);
            Assert.Equal("https://example.com/plans", info.HyperlinkBase);
            Assert.Equal(
                new[] { "Part number=B-12", "Weight=42.5" },
                info.CustomProperties.Select(p => p.Key + "=" + p.Value)
            );
            Assert.Equal("DWGPROPS", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(string.Empty, document.Database.SummaryInfo.Title);
            Assert.Empty(document.Database.SummaryInfo.CustomProperties);
            Assert.True(document.Redo());
            Assert.Equal("Plate B-12", document.Database.SummaryInfo.Title);
        }

        [Fact]
        public void Cancel_WritesNothing()
        {
            CadDocument document = Blank();

            Props(document, request =>
            {
                request.Title = "Never written";
                request.Action = CadDialogAction.Cancel;
            });

            Assert.Equal(string.Empty, document.Database.SummaryInfo.Title);
            Assert.Null(document.NextUndo);
        }

        [Fact]
        public void APropertiesDialogChangedInNothing_IsNoUndoStep()
        {
            CadDocument document = Blank();

            Props(document, request => request.Action = CadDialogAction.Ok);

            Assert.Null(document.NextUndo);
        }

        [Fact]
        public void ACustomPropertyNamedTwice_LettersOfEitherCaseTheSame_IsRefusedAndNothingIsWritten()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadDrawingPropertiesRequest shown = null;
            session.UiRequested += (sender, e) =>
            {
                shown = (CadDrawingPropertiesRequest)e.Payload;
                shown.CustomProperties.Add(new KeyValuePair<string, string>("Part", "1"));
                shown.CustomProperties.Add(new KeyValuePair<string, string>("PART", "2"));
                shown.Action = CadDialogAction.Ok;
                e.Handled = true;
            };

            Run(session, "DWGPROPS");

            Assert.False(shown.TryBuild(out DxfDatabaseSummaryInfo info, out string problem));
            Assert.Null(info);
            Assert.Contains("PART", problem);
            Assert.Empty(document.Database.SummaryInfo.CustomProperties);
            Assert.Contains(problem, session.History);
        }

        [Fact]
        public void ARowWithNoNameAndNoValue_IsLeftOut_ButAValueWithNoNameIsRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            CadDrawingPropertiesRequest shown = null;
            session.UiRequested += (sender, e) =>
            {
                shown = (CadDrawingPropertiesRequest)e.Payload;
                e.Handled = true;
            };
            Run(session, "DWGPROPS");

            shown.CustomProperties.Add(new KeyValuePair<string, string>(string.Empty, string.Empty));
            Assert.True(shown.TryBuild(out DxfDatabaseSummaryInfo info, out _));
            Assert.Empty(info.CustomProperties);

            shown.CustomProperties.Add(new KeyValuePair<string, string>(" ", "orphan"));
            Assert.False(shown.TryBuild(out _, out string problem));
            Assert.Contains("name", problem);
        }

        [Fact]
        public void ALineBreakInAText_IsRefused()
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.UiRequested += (sender, e) =>
            {
                var request = (CadDrawingPropertiesRequest)e.Payload;
                request.Title = "two\nlines";
                Assert.False(request.TryBuild(out _, out string problem));
                Assert.Contains("line break", problem);
                e.Handled = true;
            };

            Run(session, "DWGPROPS");

            Assert.Equal(string.Empty, document.Database.SummaryInfo.Title);
        }

        [Fact]
        public void TheDialogStartsFromTheDrawingsProperties_AndKeepsWhoSavedItLastAndItsRevision()
        {
            DxfDatabase database = new DxfDatabase("AC1027");
            database.SummaryInfo = new DxfDatabaseSummaryInfoBuilder
            {
                Title = "Old title",
                RevisionNumber = "7",
            }.ToDatabaseSummaryInfo();
            database.SetHeader("$LASTSAVEDBY", new DxfTypedValue(1, "A. Smith"));
            CadDocument document = CadDocument.From(database, "plan.dxf");

            CadDrawingPropertiesRequest request = Props(document, shown =>
            {
                Assert.Equal("Old title", shown.Title);
                shown.Title = "New title";
                shown.Action = CadDialogAction.Ok;
            });

            Assert.Equal("New title", document.Database.SummaryInfo.Title);
            Assert.Equal("7", document.Database.SummaryInfo.RevisionNumber);
            Assert.Equal("Last saved by", request.Statistics[2].Key);
            Assert.Equal("A. Smith", request.Statistics[2].Value);
            Assert.Equal("7", request.Statistics[3].Value);
        }

        [Fact]
        public void TheStatisticsTab_ReadsTheDatesAndTheEditingTimeFromTheHeader()
        {
            DxfDatabase database = new DxfDatabase("AC1027");

            // 1 January 2000 at 18:00 is the Julian day 2451545.75; an hour and a half of editing is 0.0625 of a day.
            database.SetHeader("$TDCREATE", new DxfTypedValue(40, "2451545.75"));
            database.SetHeader("$TDUPDATE", new DxfTypedValue(40, "2451546.0"));
            database.SetHeader("$TDINDWG", new DxfTypedValue(40, "0.0625"));
            CadDocument document = CadDocument.From(database, "plan.dxf");

            CadDrawingPropertiesRequest request = Props(document, shown => shown.Action = CadDialogAction.Cancel);

            Assert.Equal(
                new[] { "Created", "Modified", "Last saved by", "Revision number", "Total editing time" },
                request.Statistics.Select(s => s.Key)
            );
            Assert.Equal(
                CadDrawingPropertiesRequest.Date(new DateTime(2000, 1, 1, 18, 0, 0)),
                request.Statistics[0].Value
            );
            Assert.Equal(
                CadDrawingPropertiesRequest.Date(new DateTime(2000, 1, 2, 0, 0, 0)),
                request.Statistics[1].Value
            );
            Assert.Equal("1 hour, 30 minutes", request.Statistics[4].Value);
        }

        [Theory]
        [InlineData(0.0, "0 minutes")]
        [InlineData(1.0, "1 minute")]
        [InlineData(45.0, "45 minutes")]
        [InlineData(60.0, "1 hour")]
        [InlineData(61.0, "1 hour, 1 minute")]
        [InlineData(180.0, "3 hours")]
        [InlineData(125.9, "2 hours, 5 minutes")]
        public void TotalEditingTime_IsWrittenAsAutoCadWritesIt(double minutes, string expected)
        {
            Assert.Equal(expected, CadDrawingPropertiesRequest.EditingTime(TimeSpan.FromMinutes(minutes)));
        }

        [Fact]
        public void TheGeneralTab_ReadsTheFile_AndSaysOnlyTheTypeWhereThereIsNone()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            string path = Path.Combine(_folder, "plan.dxf");
            database.Save(path);
            CadDocument document = CadDocument.Open(path);

            CadDrawingPropertiesRequest request = Props(document, shown => shown.Action = CadDialogAction.Cancel);

            Assert.Equal("plan.dxf", request.FileName);
            Assert.Equal("plan.dxf Properties", request.WindowTitle);
            Assert.Equal(
                new[] { "Type", "Location", "Size", "Created", "Modified", "Accessed" },
                request.General.Select(g => g.Key)
            );
            Assert.Equal("AutoCAD DXF", request.General[0].Value);
            Assert.Equal(_folder, request.General[1].Value);
            long bytes = new FileInfo(path).Length;
            Assert.Equal(CadDrawingPropertiesRequest.Size(bytes), request.General[2].Value);
            Assert.False(request.IsReadOnly);

            CadDrawingPropertiesRequest unsaved = Props(Blank(), shown => shown.Action = CadDialogAction.Cancel);
            Assert.Equal(new[] { "Type" }, unsaved.General.Select(g => g.Key));
        }

        [Fact]
        public void ASizeIsWrittenInKilobytesAndMegabytes_WithTheBytesInBrackets()
        {
            string kilobytes = CadDrawingPropertiesRequest.Size(12800);
            string megabytes = CadDrawingPropertiesRequest.Size(3L * 1024L * 1024L);

            Assert.StartsWith(
                12.5.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) + "KB (",
                kilobytes
            );
            Assert.EndsWith(" bytes)", kilobytes);
            Assert.StartsWith(
                3.0.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture) + "MB (",
                megabytes
            );
        }

        [Fact]
        public void SavingStampsTheModifiedDate_WhichTheStatisticsTabThenShows()
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.SetHeader("$TDUPDATE", new DxfTypedValue(40, "2451545.75"));
            CadDocument document = CadDocument.From(database, "plan.dxf");
            DateTime before = DateTime.Now.AddMinutes(-1);

            document.SaveAs(Path.Combine(_folder, "saved.dxf"));

            DateTime stamped = document.Database.Tdupdate;
            Assert.InRange(stamped, before, DateTime.Now.AddMinutes(1));
            DxfDatabase again = DxfDatabase.Parse(File.ReadAllText(Path.Combine(_folder, "saved.dxf")));
            Assert.InRange(again.Tdupdate, before, DateTime.Now.AddMinutes(1));
        }

        [Theory]
        [InlineData("kept.dxf")]
        [InlineData("kept.dwg")]
        public void ThePropertiesSurviveASaveAsDxfAndASaveAsDwg(string name)
        {
            CadDocument document = CadDocument.From(CadTemplate.CreateMetric(), "plan.dxf");
            Props(document, request =>
            {
                request.Title = "Plate B-12";
                request.Author = "J. Doe";
                request.Comments = "Rev A";
                request.HyperlinkBase = "C:\\plans";
                request.CustomProperties.Add(new KeyValuePair<string, string>("Part number", "B-12"));
                request.CustomProperties.Add(new KeyValuePair<string, string>("Weight", "42.5"));
                request.Action = CadDialogAction.Ok;
            });

            string path = Path.Combine(_folder, name);
            document.SaveAs(path);
            DxfDatabaseSummaryInfo read = CadDocument.Open(path).Database.SummaryInfo;

            Assert.Equal("Plate B-12", read.Title);
            Assert.Equal("J. Doe", read.Author);
            Assert.Equal("Rev A", read.Comments);
            Assert.Equal("C:\\plans", read.HyperlinkBase);
            Assert.Equal(
                new[] { "Part number=B-12", "Weight=42.5" },
                read.CustomProperties.Select(p => p.Key + "=" + p.Value)
            );
        }

        [Fact]
        public void ADrawingOfR2000KeepsThePropertiesInItsDwgpropsRecord()
        {
            DxfDatabase database = new DxfDatabase("AC1015");
            CadDocument document = CadDocument.From(database, "old.dxf");

            Props(document, request =>
            {
                request.Title = "Old plate";
                request.CustomProperties.Add(new KeyValuePair<string, string>("Part", "A-1"));
                request.Action = CadDialogAction.Ok;
            });

            Assert.Equal("Old plate", document.Database.SummaryInfo.Title);
            DxfDatabase again = DxfDatabase.Parse(document.Database.ToText());
            Assert.Equal("Old plate", again.SummaryInfo.Title);
            Assert.Equal("Part", again.SummaryInfo.CustomProperties.Single().Key);
        }
    }
}
