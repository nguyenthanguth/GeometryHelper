using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Cad;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

[assembly: CadPluginApi("1.1")]

namespace GeometryHelper.Cad.SampleUiPlugin
{
    /// <summary>
    /// The sample plugin that adds to the window: a ribbon tab "Circles", a contextual tab "Circle" shown while only circles
    /// are selected, a panel on the Plugins tab, a "Circle tools" group in Properties, a DRAWINGS button and row menu, a canvas
    /// menu, and the object snap "Circle top" (CTOP). One class does it all and also holds the command NUMCIRCLES2, so the
    /// commands and the window additions share the one object.
    /// </summary>
    public sealed class CircleUi : ICadPluginApplication
    {
        private const string IconResource = "GeometryHelper.Cad.SampleUiPlugin.circle16.png";

        /// <summary>The path of the plus-in-a-circle icon of the large button, on a square of 24.</summary>
        private const string NumberIcon = "M3 12 A9 9 0 1 0 21 12 A9 9 0 1 0 3 12 Z M8 10 L12 7 V17";

        private bool _mark;
        private bool _batch;
        private DxfColor _markColor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);
        private string _markLayer = "Mark";

        /// <summary>Adds the tabs, panels, Properties group, DRAWINGS and canvas items and the snap to the host.</summary>
        /// <param name="host">The host.</param>
        public void Initialize(CadPluginHost host)
        {
            var number = new CadRibbonPanel("Number");
            number.Items.Add(
                new CadRibbonButton("Number circles", "NUMCIRCLES2")
                {
                    Size = CadRibbonItemSize.Large,
                    Icon = CadPluginIcon.FromPathData(NumberIcon),
                    ToolTip = "Number circles",
                    Description = "Writes a number at the centre of each circle selected.",
                }
            );
            CadPluginIcon small = CadPluginIcon.FromResource(typeof(CircleUi).Assembly, IconResource);
            number.Items.Add(new CadRibbonButton("Count", Count) { Icon = small, CanRun = c => c.Document != null });
            number.Items.Add(new CadRibbonButton("Clear marks", ClearMarks) { Icon = small });
            number.Items.Add(
                new CadRibbonSplitButton(
                    "Sizes",
                    new[]
                    {
                        new CadRibbonButton("Diameter 10", c => SetDiameter(c, 10.0)) { CanRun = OnlyCircles },
                        new CadRibbonButton("Diameter 20", c => SetDiameter(c, 20.0)) { CanRun = OnlyCircles },
                        new CadRibbonButton("Diameter 50", c => SetDiameter(c, 50.0)) { CanRun = OnlyCircles },
                    }
                )
                {
                    Icon = small,
                }
            );
            var tab = new CadRibbonTab("Circles", "Circles");
            tab.Panels.Add(number);
            host.AddRibbonTab(tab);

            var mark = new CadRibbonPanel("Mark");
            mark.Items.Add(
                new CadRibbonButton("Mark", c => _mark = !_mark)
                {
                    Size = CadRibbonItemSize.Large,
                    Icon = small,
                    ToolTip = "Mark the circles selected",
                }
            );
            var contextual = new CadRibbonTab("Circle", "Circle") { ShowWhen = OnlyCircles };
            contextual.Panels.Add(mark);
            host.AddRibbonTab(contextual);

            var tools = new CadRibbonPanel("Circle tools");
            tools.Items.Add(
                new CadRibbonButton("Count", Count) { Icon = small, ToolTip = "Count the circles of the drawing" }
            );
            host.AddRibbonPanel(tools);

            host.AddProperties(Describe);
            host.AddDrawingsButton(
                new CadPluginButton(CadPluginIcon.FromPathData("M4 12 H20 M12 4 V20"), "Count the circles", Count)
                {
                    CanRun = c => c.Document != null,
                }
            );
            host.AddDrawingsMenu(request =>
                new[]
                {
                    new CadPluginMenuItem(
                        "Circle report",
                        new[]
                        {
                            new CadPluginMenuItem("Count circles", Count, request.Document != null),
                            new CadPluginMenuItem("Write report", Count, false),
                        }
                    ),
                    new CadPluginMenuItem("Include in batch", c => _batch = !_batch, true, _batch),
                }
            );
            host.AddCanvasMenu(context =>
                OnlyCircles(context)
                    ? new[]
                    {
                        new CadPluginMenuItem("Number these", c => c.RunCommand("NUMCIRCLES2")),
                        new CadPluginMenuItem(
                            "Circle tools",
                            new[]
                            {
                                new CadPluginMenuItem("Mark", c => _mark = true, true, _mark),
                                new CadPluginMenuItem("Clear marks", ClearMarks, _mark),
                            }
                        ),
                    }
                    : null
            );

            var top = new CadCustomSnap("Circle top", "CTOP", TopOfCircle)
            {
                Glyph = new[]
                {
                    new CadMarkerStroke(
                        new[] { new GeoPoint2(-4.5, 4.5), new GeoPoint2(4.5, 4.5), new GeoPoint2(0.0, -4.5) },
                        true,
                        filled: true
                    ),
                },
            };
            top.EntityTypes.Add(typeof(DxfCircle));
            host.AddObjectSnap(top);
        }

        /// <summary>Lets go of what the sample holds: the mark and the batch tick.</summary>
        public void Terminate()
        {
            _mark = false;
            _batch = false;
        }

        /// <summary>
        /// Numbers the circles selected from 1, a text at the centre of each, on the layer Mark, in one undo step: three
        /// circles are numbered 1, 2 and 3.
        /// </summary>
        /// <param name="c">The command's context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("NUMCIRCLES2", Description = "Number the selected circles from 1", UsesSelection = true)]
        public IEnumerable<CadPrompt> NumberCircles(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var circles = new List<DxfCircle>();

            foreach (string handle in c.Selected)
            {
                if (c.Document.FindEntity(handle) is DxfCircle circle)
                {
                    circles.Add(circle);
                }
            }

            if (circles.Count == 0)
            {
                c.Write("No circle was selected.");
                yield break;
            }

            string layer = _markLayer;
            bool newLayer = !c.Database.LayerTable.Has(layer);
            CadEditRecord[] planned = newLayer
                ? new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layer", null, layer) }
                : null;
            CadEditResult result = c.Apply(planned, s =>
            {
                if (newLayer)
                {
                    s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = layer, ColorIndex = 7 });
                }

                int number = 1;

                foreach (DxfCircle circle in circles)
                {
                    var text = new DxfDBText();
                    text.SetDatabaseDefaults(c.Database);
                    text.Layer = layer;
                    text.Height = 2.5;
                    text.TextString = number.ToString(CultureInfo.InvariantCulture);
                    text.Justify = DxfAttachmentPoint.MiddleCenter;
                    text.AlignmentPoint = circle.Center;
                    s.Append(c.Space, text);
                    number++;
                }
            });

            if (result.Succeeded)
            {
                c.Write(string.Format(CultureInfo.InvariantCulture, "Numbered {0} circles.", circles.Count));
            }
        }

        /// <summary>Whether a drawing is open and every object selected is a circle, at least one.</summary>
        private static bool OnlyCircles(CadPluginContext context)
        {
            if (context.Document == null || context.Selected.Count == 0)
            {
                return false;
            }

            foreach (string handle in context.Selected)
            {
                if (!(context.Document.FindEntity(handle) is DxfCircle))
                {
                    return false;
                }
            }

            return true;
        }

        private static void Count(CadPluginContext context)
        {
            int count = 0;

            foreach (DxfEntity entity in context.Document.CurrentSpace.Block)
            {
                if (entity is DxfCircle)
                {
                    count++;
                }
            }

            context.Write(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} circles in {1}.",
                    count,
                    context.Document.CurrentSpace.Name
                )
            );
        }

        private void ClearMarks(CadPluginContext context)
        {
            _mark = false;
            context.Write("Marks cleared.");
        }

        private static void SetDiameter(CadPluginContext context, double diameter)
        {
            CadEditResult result = CadPropertyEditor.Set(
                context.Document,
                context.Selected,
                "Diameter",
                diameter.ToString("R", CultureInfo.InvariantCulture)
            );

            if (!result.Succeeded)
            {
                context.Write(result.Message);
            }
        }

        /// <summary>The Properties provider: adds the "Circle tools" group where every object shown is a circle.</summary>
        private CadSelectionInfo Describe(CadPropertyRequest request)
        {
            if (request.Document == null || request.Shown == null || request.Handles.Count == 0)
            {
                return null;
            }

            var circles = new List<DxfCircle>();

            foreach (string handle in request.Handles)
            {
                if (!(request.Document.FindEntity(handle) is DxfCircle circle))
                {
                    return null;
                }

                circles.Add(circle);
            }

            CadDocument document = request.Document;
            IReadOnlyList<string> handles = request.Handles;
            var rows = new List<CadPropertyRow>
            {
                new CadPropertyRow("Number of circles", circles.Count.ToString(CultureInfo.InvariantCulture), true),
                CadPropertyRow.Length("Diameter", circles[0].Diameter, document, d =>
                {
                    CadEditResult result = CadPropertyEditor.Set(
                        document,
                        handles,
                        "Diameter",
                        d.ToString("R", CultureInfo.InvariantCulture)
                    );
                    return result.Succeeded ? null : result.Message;
                }),
                CadPropertyRow.Check("Mark", _mark, text =>
                {
                    _mark = text == CadPropertyRow.YesText;
                    return null;
                }),
                CadPropertyRow.Color("Mark color", _markColor, color =>
                {
                    _markColor = color;
                    return null;
                }),
                CadPropertyRow.Layer("Mark layer", _markLayer, layer =>
                {
                    if (!document.Database.LayerTable.Has(layer))
                    {
                        return "Circle tools: the drawing has no layer " + layer + ".";
                    }

                    _markLayer = layer;
                    return null;
                }),
            };
            var groups = new List<CadPropertyGroup>(request.Shown.Groups)
            {
                new CadPropertyGroup("Circle tools", rows),
            };
            return new CadSelectionInfo(request.Shown.Label, groups);
        }

        /// <summary>The point at the top of a circle, 90 degrees round from its X axis: the centre and a radius up.</summary>
        private static IEnumerable<GeoPoint2> TopOfCircle(CadSnapRequest request)
        {
            if (request.Entity is DxfCircle circle)
            {
                yield return new GeoPoint2(circle.Center.X, circle.Center.Y + circle.Radius);
            }
        }
    }
}
