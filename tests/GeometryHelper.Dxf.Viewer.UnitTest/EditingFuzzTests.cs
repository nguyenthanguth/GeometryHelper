using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingFuzzTests
    {
        private readonly ITestOutputHelper _output;

        public EditingFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static readonly string[] Commands =
        {
            "MOVE",
            "COPY",
            "ERASE",
            "ROTATE",
            "SCALE",
            "MIRROR",
            "EXPLODE",
            "MATCHPROP",
            "LAYER",
            "UNDO",
            "U",
            "REDO",
            "LINE",
            "PLINE",
            "CIRCLE",
            "ARC",
            "RECTANG",
            "TEXT",
            "MTEXT",
            "MLEADER",
            "DIMLINEAR",
            "DIMALIGNED",
            "DIMRADIUS",
            "DIMDIAMETER",
            "DIMANGULAR",
            "ZOOM",
        };

        private static readonly string[] Texts =
        {
            "A",
            "B-12",
            "Walls",
            "0",
            "Steel",
            "<> mm",
            "Doors, Walls",
            "x",
            "2x",
            "0.5",
            "30",
        };

        private static readonly string[] Labels =
        {
            "Color",
            "Layer",
            "Linetype",
            "Linetype scale",
            "Lineweight",
            "Start X",
            "End Y",
            "Radius",
            "Diameter",
            "Start angle",
            "End angle",
            "Contents",
            "Height",
            "Position X",
        };

        private static readonly string[] Values =
        {
            "Red",
            "ByLayer",
            "8",
            "Steel",
            "0",
            "Continuous",
            "2",
            "0.35 mm",
            "-1",
            "45",
            "120.5",
            "NOTE 2",
            "Nope",
        };

        /// <summary>A drawing of R2013, with its styles and layouts, holding a line, a circle, an arc, a polyline and a text.</summary>
        private static string RichText()
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(200, 0, 0), new GeoVector3(0, 0, 1), 10));
            database.ModelSpace.AppendEntity(new DxfArc(new GeoPoint3(300, 0, 0), 20, 0, Math.PI / 2.0));
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(0, 100), 0.5, 0, 0);
            polyline.AddVertexAt(1, new GeoPoint2(100, 100), 0, 0, 0);
            polyline.AddVertexAt(2, new GeoPoint2(100, 150), 0, 0, 0);
            database.ModelSpace.AppendEntity(polyline);
            database.ModelSpace.AppendEntity(
                new DxfDBText
                {
                    Position = new GeoPoint3(0, 200, 0),
                    Height = 5,
                    TextString = "NOTE",
                }
            );
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "Steel", ColorIndex = 1 });
            return database.ToText();
        }

        [Fact]
        public void TwoThousandRandomCommandSequences_NeverFail_AlwaysReopen_AndUndoBackToTheStart()
        {
            string plan = PlanDatabase().ToText();
            string rich = RichText();
            var failures = new List<string>();
            int steps = 0;
            var outcomes = new SortedDictionary<string, int>(StringComparer.Ordinal);

            for (int seed = 0; seed < 2000; seed++)
            {
                var random = new Random(seed);
                CadDocument document = CadDocument.From(DxfDatabase.Parse(seed % 2 == 0 ? plan : rich), "fuzz.dxf");
                string start = document.Database.ToText();
                CadCommandSession session = Session(document);
                session.PickAperture = 2.0;
                session.View = new CadView(800, 600);
                int actions = random.Next(1, 6);

                for (int a = 0; a < actions; a++)
                {
                    int pick = random.Next(10);

                    if (pick == 0)
                    {
                        Grip(document, random);
                    }
                    else if (pick == 1)
                    {
                        CadPropertyEditor.Set(
                            document,
                            SomeHandles(document, random, 2),
                            Labels[random.Next(Labels.Length)],
                            Values[random.Next(Values.Length)]
                        );
                    }
                    else
                    {
                        Command(session, random);
                        string key = session.LastCommandName + " " + (session.LastOutcome?.ToString() ?? "None");
                        outcomes[key] = outcomes.TryGetValue(key, out int seen) ? seen + 1 : 1;

                        if (session.LastOutcome == CadCommandOutcome.Failed)
                        {
                            failures.Add(
                                "seed "
                                    + seed
                                    + ": "
                                    + string.Join(" | ", session.History.Skip(Math.Max(0, session.History.Count - 6)))
                            );
                        }
                    }
                }

                string text = document.Database.ToText();

                if (!string.Equals(text, DxfDatabase.Parse(text).ToText(), StringComparison.Ordinal))
                {
                    failures.Add("seed " + seed + ": the drawing does not read back as it was written.");
                }

                int undone = 0;

                while (document.Undo())
                {
                    undone++;
                }

                steps += undone;

                if (!string.Equals(start, document.Database.ToText(), StringComparison.Ordinal))
                {
                    failures.Add("seed " + seed + ": undoing " + undone + " steps did not give the start back.");
                }
            }

            _output.WriteLine(
                steps
                    + " undo steps made and undone; commands "
                    + string.Join(", ", outcomes.Select(p => p.Key + " " + p.Value))
            );
            Assert.Empty(failures);
        }

        private static void Command(CadCommandSession session, Random random)
        {
            session.Start(Commands[random.Next(Commands.Length)]);

            for (int i = 0; i < 16 && session.IsRunning; i++)
            {
                CadPrompt prompt = session.Prompt;
                int roll = random.Next(100);

                if (roll < 4)
                {
                    session.Escape();
                    return;
                }

                if (roll < 14)
                {
                    session.Enter();
                    continue;
                }

                if (roll < 26 && prompt.Keywords.Count > 0)
                {
                    session.Feed(CadPrompt.Shortcut(prompt.Keywords[random.Next(prompt.Keywords.Count)]));
                    continue;
                }

                switch (prompt.Kind)
                {
                    case CadPromptKind.Selection:
                        if (random.Next(5) == 0)
                        {
                            session.Feed("ALL");
                        }
                        else
                        {
                            session.FeedSelection(SomeHandles(session.Document, random, 3));
                        }

                        session.Enter();
                        break;
                    case CadPromptKind.Entity:
                        if (random.Next(2) == 0)
                        {
                            session.FeedSelection(SomeHandles(session.Document, random, 1));
                        }
                        else
                        {
                            session.Feed(Point(random));
                        }

                        break;
                    case CadPromptKind.Distance:
                    case CadPromptKind.Angle:
                        if (random.Next(2) == 0)
                        {
                            session.Feed(
                                (random.NextDouble() * 120.0 - 10.0).ToString("0.###", CultureInfo.InvariantCulture)
                            );
                        }
                        else
                        {
                            session.Feed(Point(random));
                        }

                        break;
                    case CadPromptKind.Integer:
                        session.Feed(random.Next(0, 4).ToString(CultureInfo.InvariantCulture));
                        break;
                    case CadPromptKind.String:
                        session.Feed(Texts[random.Next(Texts.Length)]);
                        break;
                    case CadPromptKind.Keyword:
                        session.Feed(
                            prompt.Keywords.Count > 0
                                ? CadPrompt.Shortcut(prompt.Keywords[random.Next(prompt.Keywords.Count)])
                                : "x"
                        );
                        break;
                    default:
                        if (random.Next(6) == 0)
                        {
                            session.Feed(
                                "@"
                                    + random.Next(-50, 50).ToString(CultureInfo.InvariantCulture)
                                    + ","
                                    + random.Next(-50, 50).ToString(CultureInfo.InvariantCulture)
                            );
                        }
                        else
                        {
                            session.Feed(Point(random));
                        }

                        break;
                }
            }

            session.Escape();
        }

        private static void Grip(CadDocument document, Random random)
        {
            IReadOnlyCollection<string> handles = SomeHandles(document, random, 1);

            if (handles.Count == 0)
            {
                return;
            }

            string handle = handles.First();
            int count = CadGrips.Of(document.FindEntity(handle)).Count;

            if (count > 0)
            {
                document.GripDrag(handle, random.Next(count), Point(random));
            }
        }

        private static GeoPoint2 Point(Random random)
        {
            // Mostly on a grid of 10, where the plan's own points lie, so that picks and degenerate shapes come up.
            double x = random.Next(-5, 60) * 10.0 + (random.Next(4) == 0 ? random.NextDouble() : 0.0);
            double y = random.Next(-6, 25) * 10.0 + (random.Next(4) == 0 ? random.NextDouble() : 0.0);
            return new GeoPoint2(x, y);
        }

        private static IReadOnlyCollection<string> SomeHandles(CadDocument document, Random random, int most)
        {
            var handles = new List<string>();

            foreach (DxfEntity entity in document.CurrentSpace.Block)
            {
                if (!string.IsNullOrEmpty(entity.Handle))
                {
                    handles.Add(entity.Handle);
                }
            }

            handles.Sort(StringComparer.Ordinal);
            var picked = new List<string>();
            int count = handles.Count == 0 ? 0 : random.Next(1, most + 1);

            for (int i = 0; i < count; i++)
            {
                picked.Add(handles[random.Next(handles.Count)]);
            }

            return picked;
        }
    }
}
