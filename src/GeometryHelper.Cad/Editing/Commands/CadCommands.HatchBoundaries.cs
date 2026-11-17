using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The commands that work on the boundaries of a hatch already made, as AutoCAD 2021 runs them and as the Hatch Editor tab
    /// starts them: HATCHGENERATEBOUNDARY, HATCHSETBOUNDARY and the typed -HATCHEDIT with its Disassociate, Style, Properties,
    /// DRaw order, ADd boundaries, Remove boundaries, recreate Boundary, ASsociate, separate Hatches, Origin, ANnotative, hatch
    /// COlor, LAyer and Transparency.
    /// </summary>
    internal static partial class CadCommands
    {
        internal static void AddHatchBoundariesTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("HATCHGENERATEBOUNDARY", HatchGenerateBoundary, true));
            registry.Add(new CadCommand("HATCHSETBOUNDARY", HatchSetBoundary));
            registry.Add(new CadCommand("-HATCHEDIT", HatchEditTyped, true));
        }

        private static readonly string[] HatchEditOptions =
        {
            "DIsassociate",
            "Style",
            "Properties",
            "DRaw order",
            "ADd boundaries",
            "Remove boundaries",
            "recreate Boundary",
            "ASsociate",
            "separate Hatches",
            "Origin",
            "ANnotative",
            "hatch COlor",
            "LAyer",
            "Transparency",
        };

        /// <summary>
        /// HATCHGENERATEBOUNDARY: "Select hatch objects:" (the hatches selected before it are taken instead), then a closed
        /// polyline on the current layer round each loop of each hatch, a circle for a loop that is one whole circle, as AutoCAD
        /// 2021 makes them (probed: a square hatched round a circle gave a polyline and a CIRCLE, on the current layer, nothing
        /// said); the objects follow nothing and the hatch is left as it is. A loop with an ellipse or a spline edge is passed
        /// over and said. One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchGenerateBoundary(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select hatch objects", e => e is DxfHatch))
            {
                yield return prompt;
            }

            var made = new List<DxfEntity>();
            int passed = 0;

            foreach (string handle in c.Selected)
            {
                foreach (
                    CadGrips.HatchBoundary shape in CadHatchBoundaries.Shapes((DxfHatch)c.Document.FindEntity(handle))
                )
                {
                    if (shape == null)
                    {
                        passed++;
                    }
                    else
                    {
                        DxfEntity circle = CadHatchBoundaries.WholeCircle(shape, c.Session.Tolerance);
                        made.Add(
                            circle != null ? Current(c, circle) : Current(c, CadHatchBoundaries.PolylineOf(shape))
                        );
                    }
                }
            }

            if (made.Count > 0)
            {
                CadEditResult result = c.Apply(null, s =>
                {
                    foreach (DxfEntity entity in made)
                    {
                        s.Append(c.Space, entity);
                    }
                });

                if (!result.Succeeded && result.Message != null)
                {
                    c.Write(result.Message);
                }
            }

            SayPassedLoops(c, passed);
        }

        /// <summary>The words for the loops of a hatch that are no shape here: an ellipse or a spline in them, or a hatch not flat to the plan.</summary>
        private static void SayPassedLoops(CadCommandContext c, int passed)
        {
            if (passed > 0)
            {
                c.Write(
                    Count(passed, "loop", "loops")
                        + " of ellipses, splines or a tilted hatch cannot be made a boundary and "
                        + (passed == 1 ? "was" : "were")
                        + " passed over."
                );
            }
        }

        /// <summary>
        /// HATCHSETBOUNDARY: "Select hatch object:", "Select objects to be used for the new boundary:" and "Erase selected
        /// linework? [Yes/No] &lt;N&gt;:" (probed in AutoCAD 2021's console): the hatch's loops are taken out and made anew of
        /// the closed objects selected, the outer loops and the islands as HATCH finds them, the hatch following them where it
        /// followed its old ones, and the objects erased where the answer is Yes (the hatch then follows nothing). One undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchSetBoundary(CadCommandContext c)
        {
            yield return CadPrompt
                .ForEntity("Select hatch object")
                .Expecting("Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon");
            string handle = c.Input.Handles.FirstOrDefault();

            if (handle == null)
            {
                yield break;
            }

            if (!(c.Document.FindEntity(handle) is DxfHatch hatch))
            {
                c.Write("Selected object is not a hatch.");
                yield break;
            }

            if (CadLayerEditor.IsLocked(c.Database, hatch.Layer))
            {
                c.Write("1 was on a locked layer.");
                yield break;
            }

            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Hatch"),
            };

            if (!c.Document.EditGate.Allows(c.Document, hatch, planned[0], out string reason))
            {
                c.Write(reason ?? "The hatch " + handle + " may not be changed.");
                yield break;
            }

            yield return CadPrompt.ForSelection("Select objects to be used for the new boundary");
            IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.FromObjects(
                c.Document,
                c.Input.Handles.Where(h => !string.Equals(h, handle, StringComparison.OrdinalIgnoreCase)),
                c.Session.Tolerance,
                out int passed
            );

            if (passed > 0)
            {
                c.Write(
                    passed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + (passed == 1 ? " object is" : " objects are")
                        + " not closed and cannot be a hatch boundary."
                );
            }

            if (loops.Count == 0)
            {
                c.Write("Valid hatch boundary not found.");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Erase selected linework?", new[] { "Yes", "No" }, "N");
            bool erase = c.Input.Is("Yes");
            var sources = loops
                .SelectMany(l => l.Sources)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (erase)
            {
                planned.AddRange(sources.Select(h => new CadEditRecord(CadEditRecordKind.Erased, h)));
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                var edited = (DxfHatch)s.Document.FindEntity(handle);
                bool follow = edited.Associative && !erase;
                List<CadBoundaryLoop> kept = CadBoundaryFinder.Kept(loops, edited.HatchStyle).ToList();

                for (int i = edited.NumberOfLoops - 1; i >= 0; i--)
                {
                    edited.RemoveLoopAt(i);
                }

                AppendLoops(edited, kept);

                if (follow)
                {
                    edited.SetAssociatedObjects(FollowedBy(s.Document, kept));
                }

                if (erase)
                {
                    foreach (string source in sources)
                    {
                        s.Document.FindEntity(source).Erase();
                    }
                }
            });

            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>The objects each of the loops is made of, a list for each in order, for a hatch to follow.</summary>
        private static List<IReadOnlyList<DxfEntity>> FollowedBy(
            CadDocument document,
            IEnumerable<CadBoundaryLoop> loops
        ) =>
            loops
                .Select(l =>
                    (IReadOnlyList<DxfEntity>)
                        l.Sources.Select(h => document.FindEntity(h)).Where(e => e != null).ToList()
                )
                .ToList();

        /// <summary>The settings of a hatch as the Hatch Editor shows them: as <see cref="CadHatchSettings.Of"/> reads them, with the origin its pattern is laid from.</summary>
        internal static CadHatchSettings HatchSettingsOf(DxfHatch hatch)
        {
            CadHatchSettings settings = CadHatchSettings.Of(hatch);
            GeoPoint2 origin = CadHatchOrigin.Of(hatch);
            settings.OriginX = origin.X;
            settings.OriginY = origin.Y;
            return settings;
        }

        /// <summary>
        /// -HATCHEDIT: "Select hatch objects:" (the hatches selected before it are taken instead), then "Enter hatch option
        /// [DIsassociate/Style/Properties/DRaw order/ADd boundaries/Remove boundaries/recreate Boundary/ASsociate/separate
        /// Hatches/Origin/ANnotative/hatch COlor/LAyer/Transparency] &lt;Properties&gt;:" (the words AutoCAD 2021 asks), and the
        /// option's own questions; each option one undo step on every hatch selected, Add, Remove and Associate on the first alone.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchEditTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjectsOf("Select hatch objects", e => e is DxfHatch))
            {
                yield return prompt;
            }

            var hatches = new List<DxfHatch>();

            foreach (string handle in c.Selected)
            {
                var hatch = (DxfHatch)c.Document.FindEntity(handle);
                var record = new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Hatch");

                if (c.Document.EditGate.Allows(c.Document, hatch, record, out string reason))
                {
                    hatches.Add(hatch);
                }
                else
                {
                    c.Write(reason ?? "The hatch " + handle + " may not be changed.");
                }
            }

            if (hatches.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter hatch option", HatchEditOptions, "Properties");
            IEnumerable<CadPrompt> option;

            if (c.Input.Is("DIsassociate"))
            {
                option = HatchDisassociate(c, hatches);
            }
            else if (c.Input.Is("Style"))
            {
                option = HatchStyleAsked(c, hatches);
            }
            else if (c.Input.Is("DRaw order"))
            {
                option = HatchEditOrder(c, hatches);
            }
            else if (c.Input.Is("ADd boundaries"))
            {
                option = HatchAddBoundaries(c, hatches[0]);
            }
            else if (c.Input.Is("Remove boundaries"))
            {
                option = HatchTakeBoundaries(c, hatches[0]);
            }
            else if (c.Input.Is("recreate Boundary"))
            {
                option = HatchRecreate(c, hatches);
            }
            else if (c.Input.Is("ASsociate"))
            {
                option = HatchAssociate(c, hatches[0]);
            }
            else if (c.Input.Is("separate Hatches"))
            {
                option = HatchSeparate(c, hatches);
            }
            else if (c.Input.Is("Origin"))
            {
                option = HatchEditOrigin(c, hatches);
            }
            else if (c.Input.Is("ANnotative"))
            {
                option = HatchEditAnnotative(c, hatches);
            }
            else if (c.Input.Is("hatch COlor"))
            {
                option = HatchEditColors(c, hatches);
            }
            else if (c.Input.Is("LAyer"))
            {
                option = HatchEditLayer(c, hatches);
            }
            else if (c.Input.Is("Transparency"))
            {
                option = HatchEditTransparency(c, hatches);
            }
            else if (c.Input.Kind == CadInputKind.None || c.Input.Is("Properties"))
            {
                option = HatchEditProperties(c, hatches);
            }
            else
            {
                yield break;
            }

            foreach (CadPrompt prompt in option)
            {
                yield return prompt;
            }
        }

        /// <summary>One edit of hatches by an option of -HATCHEDIT, one undo step: the change given each, found again in the drawing.</summary>
        private static void HatchEditApply(
            CadCommandContext c,
            List<DxfHatch> hatches,
            Action<CadEditScope, DxfHatch> change
        )
        {
            var planned = hatches
                .Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h.Handle, "Hatch"))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (DxfHatch hatch in hatches)
                {
                    change(s, (DxfHatch)s.Document.FindEntity(hatch.Handle));
                }
            });

            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }

        private static IEnumerable<CadPrompt> HatchDisassociate(CadCommandContext c, List<DxfHatch> hatches)
        {
            HatchEditApply(c, hatches, (s, h) => h.Associative = false);
            c.Write("Hatch boundary associativity removed.");
            yield break;
        }

        private static IEnumerable<CadPrompt> HatchStyleAsked(CadCommandContext c, List<DxfHatch> hatches)
        {
            yield return CadPrompt.ForKeyword(
                "Enter hatching style",
                new[] { "Ignore", "Outer", "Normal" },
                hatches[0].HatchStyle.ToString()
            );

            DxfHatchStyle? style =
                c.Input.Is("Ignore") ? DxfHatchStyle.Ignore
                : c.Input.Is("Outer") ? DxfHatchStyle.Outer
                : c.Input.Is("Normal") ? DxfHatchStyle.Normal
                : (DxfHatchStyle?)null;

            if (style.HasValue)
            {
                HatchEditApply(c, hatches, (s, h) => h.HatchStyle = style.Value);
            }
        }

        /// <summary>Properties: the pattern, its scale and angle, spacing, double or gradient asked as -HATCH asks them, laid on every hatch selected, each keeping its own origin and the rest it has.</summary>
        private static IEnumerable<CadPrompt> HatchEditProperties(CadCommandContext c, List<DxfHatch> hatches)
        {
            CadHatchSettings settings = HatchSettingsOf(hatches[0]);

            foreach (CadPrompt prompt in HatchPropertiesTyped(c, settings))
            {
                yield return prompt;
            }

            ApplyHatchSettingsTo(c, hatches, settings);
        }

        /// <summary>Gives hatches the pattern of settings, each hatch keeping its own origin, association, island style and annotation.</summary>
        private static void ApplyHatchSettingsTo(CadCommandContext c, List<DxfHatch> hatches, CadHatchSettings settings)
        {
            string fault = null;
            HatchEditApply(c, hatches, (s, h) =>
            {
                CadHatchSettings own = settings.Clone();
                GeoPoint2 origin = CadHatchOrigin.Of(h);
                own.OriginX = origin.X;
                own.OriginY = origin.Y;
                own.Associative = h.Associative;
                own.IslandStyle = h.HatchStyle;
                own.Annotative = settings.Annotative;
                fault = ApplyEditedSettings(s, h.Handle, own) ?? fault;
            });

            if (fault != null)
            {
                c.Write(fault);
            }
        }

        private static IEnumerable<CadPrompt> HatchEditAnnotative(CadCommandContext c, List<DxfHatch> hatches)
        {
            CadHatchSettings settings = HatchSettingsOf(hatches[0]);

            foreach (CadPrompt prompt in HatchAnnotative(c, settings))
            {
                yield return prompt;
            }

            ApplyHatchSettingsTo(c, hatches, settings);
        }

        private static IEnumerable<CadPrompt> HatchEditOrder(CadCommandContext c, List<DxfHatch> hatches)
        {
            yield return CadPrompt.ForKeyword("Enter draw order", HatchOrders, HatchOrders[3]);
            int order = Array.FindIndex(HatchOrders, o => c.Input.Is(o));

            if (order > 0)
            {
                HatchEditApply(c, hatches, (s, h) => CadHatchEditor.Place(s.Document, h, order));
            }
        }

        /// <summary>
        /// ADd boundaries: "Specify internal point or [Select objects]:" until Enter, each area found round a point (with its
        /// islands as the hatch's island style keeps them) or taken from closed objects selected, added to the hatch, which
        /// follows their objects as well where it followed those it had.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchAddBoundaries(CadCommandContext c, DxfHatch hatch)
        {
            var picks = new List<IReadOnlyList<CadBoundaryLoop>>();

            foreach (CadPrompt prompt in HatchPicked(c, hatch, picks))
            {
                yield return prompt;
            }

            if (picks.Count == 0)
            {
                yield break;
            }

            HatchEditApply(c, new List<DxfHatch> { hatch }, (s, h) =>
            {
                bool follow = h.Associative;
                var objects = new List<IReadOnlyList<DxfEntity>>();

                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    objects.Add(h.GetAssociatedObjectsAt(i));
                }

                List<CadBoundaryLoop> added = KeptLoops(picks, new CadHatchSettings { IslandStyle = h.HatchStyle });
                AppendLoops(h, added);

                if (follow)
                {
                    objects.AddRange(FollowedBy(s.Document, added));
                    h.SetAssociatedObjects(objects);
                }
            });
        }

        /// <summary>"Specify internal point or [Select objects]:" until Enter: the areas found and the loops of the objects selected, in <paramref name="picks"/>.</summary>
        private static IEnumerable<CadPrompt> HatchPicked(
            CadCommandContext c,
            DxfHatch hatch,
            List<IReadOnlyList<CadBoundaryLoop>> picks
        )
        {
            double gap = c.Document.HatchSettings.GapTolerance;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify internal point", null, new[] { "Select objects" });

                if (c.Input.IsPoint)
                {
                    IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.Find(
                        c.Document,
                        c.Space,
                        c.Input.Point,
                        c.Session.Tolerance,
                        c.Session.WorkView?.VisibleBox,
                        null,
                        gap,
                        GapPixel(c)
                    );

                    if (loops == null)
                    {
                        c.Write("Valid hatch boundary not found.");
                    }
                    else
                    {
                        picks.Add(loops);
                    }
                }
                else if (c.Input.Is("Select objects"))
                {
                    yield return CadPrompt.ForSelection();
                    IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.FromObjects(
                        c.Document,
                        c.Input.Handles.Where(h => !string.Equals(h, hatch.Handle, StringComparison.OrdinalIgnoreCase)),
                        c.Session.Tolerance,
                        out int passed
                    );

                    if (passed > 0)
                    {
                        c.Write(
                            passed.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + (passed == 1 ? " object is" : " objects are")
                                + " not closed and cannot be a hatch boundary."
                        );
                    }

                    if (loops.Count > 0)
                    {
                        picks.Add(loops);
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Remove boundaries: "Select objects:", the loops of the hatch made of the objects selected taken out, and, for a loop
        /// of a hatch that follows no object, the loop of a shape the object makes; an outer loop takes its islands with it.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchTakeBoundaries(CadCommandContext c, DxfHatch hatch)
        {
            yield return CadPrompt.ForSelection();

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            var wanted = new HashSet<string>(c.Input.Handles, StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<CadBoundaryLoop> made = CadBoundaryFinder.FromObjects(
                c.Document,
                c.Input.Handles,
                c.Session.Tolerance,
                out _
            );
            Tolerance tolerance = c.Session.Tolerance;
            bool any = false;

            HatchEditApply(c, new List<DxfHatch> { hatch }, (s, h) =>
            {
                bool follow = h.Associative;
                CadGrips.HatchBoundary[] shapes = CadHatchBoundaries.Shapes(h);
                var objects = new List<IReadOnlyList<DxfEntity>>();
                var drop = new List<bool>();
                bool dropping = false;

                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    IReadOnlyList<DxfEntity> followed = h.GetAssociatedObjectsAt(i);
                    objects.Add(followed);
                    bool hit =
                        followed.Any(e => wanted.Contains(e.Handle))
                        || (followed.Count == 0 && made.Any(l => CadHatchBoundaries.Same(shapes[i], l, tolerance)));
                    bool outer = (h.LoopTypeAt(i) & DxfHatchLoopTypes.External) != 0;
                    dropping = outer ? hit : dropping || hit;
                    drop.Add(dropping);
                }

                any = drop.Contains(true);

                for (int i = drop.Count - 1; i >= 0; i--)
                {
                    if (drop[i])
                    {
                        h.RemoveLoopAt(i);
                    }
                }

                if (follow && any)
                {
                    h.SetAssociatedObjects(objects.Where((o, i) => !drop[i]).ToList());
                }
            });

            if (!any)
            {
                c.Write("No boundary of the hatch is made of the objects selected.");
            }
        }

        /// <summary>
        /// recreate Boundary: "Enter type of boundary object [Region/Polyline] &lt;Polyline&gt;:" and "Reassociate hatch with
        /// new boundary? [Yes/No] &lt;N&gt;:" (as AutoCAD 2021 asks them): a closed polyline, or a region, on the current layer
        /// round each loop of each hatch, the hatch following the polylines where the answer is Yes. A loop that is no shape
        /// here is passed over and said.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchRecreate(CadCommandContext c, List<DxfHatch> hatches)
        {
            bool region = c.Remembered(BoundaryRegionKey, 0.0) != 0.0;
            yield return CadPrompt.ForKeyword(
                "Enter type of boundary object",
                new[] { "Region", "Polyline" },
                region ? "Region" : "Polyline"
            );

            if (c.Input.Is("Region") || c.Input.Is("Polyline"))
            {
                region = c.Input.Is("Region");
                c.Remember(BoundaryRegionKey, region ? 1.0 : 0.0);
            }

            yield return CadPrompt.ForKeyword("Reassociate hatch with new boundary?", new[] { "Yes", "No" }, "N");
            bool follow = c.Input.Is("Yes") && !region;
            int passed = 0;

            if (region)
            {
                var curves = new List<KeyValuePair<List<DxfCurve>, List<List<DxfCurve>>>>();

                foreach (DxfHatch hatch in hatches)
                {
                    foreach (CadGrips.HatchBoundary shape in CadHatchBoundaries.Shapes(hatch))
                    {
                        if (shape == null)
                        {
                            passed++;
                        }
                        else
                        {
                            curves.Add(
                                new KeyValuePair<List<DxfCurve>, List<List<DxfCurve>>>(
                                    new List<DxfCurve> { CadHatchBoundaries.PolylineOf(shape) },
                                    null
                                )
                            );
                        }
                    }
                }

                int made = MakeRegions(c, curves);
                c.Write(made > 0 ? "Created " + Count(made, "region", "regions") + "." : "No region was made.");
                SayPassedLoops(c, passed);
                yield break;
            }

            HatchEditApply(c, hatches, (s, h) =>
            {
                CadGrips.HatchBoundary[] shapes = CadHatchBoundaries.Shapes(h);
                var objects = new List<IReadOnlyList<DxfEntity>>();

                foreach (CadGrips.HatchBoundary shape in shapes)
                {
                    if (shape == null)
                    {
                        passed++;
                        objects.Add(new List<DxfEntity>());
                    }
                    else
                    {
                        objects.Add(
                            new List<DxfEntity> { s.Append(c.Space, Current(c, CadHatchBoundaries.PolylineOf(shape))) }
                        );
                    }
                }

                if (follow && shapes.Length > 0)
                {
                    h.SetAssociatedObjects(objects);
                }
            });
            SayPassedLoops(c, passed);
        }

        /// <summary>
        /// ASsociate: "Specify internal point or [Select objects]:", the hatch made to follow the objects that make the areas
        /// found, each of its loops the objects of the loop of the same corners; "Valid hatch boundary not found." where none
        /// of its loops is one of them.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchAssociate(CadCommandContext c, DxfHatch hatch)
        {
            var picks = new List<IReadOnlyList<CadBoundaryLoop>>();

            foreach (CadPrompt prompt in HatchPicked(c, hatch, picks))
            {
                yield return prompt;
            }

            List<CadBoundaryLoop> found = picks.SelectMany(p => p).ToList();
            Tolerance tolerance = c.Session.Tolerance;
            bool matched = false;

            HatchEditApply(c, new List<DxfHatch> { hatch }, (s, h) =>
            {
                CadGrips.HatchBoundary[] shapes = CadHatchBoundaries.Shapes(h);
                var objects = new List<IReadOnlyList<DxfEntity>>();

                foreach (CadGrips.HatchBoundary shape in shapes)
                {
                    CadBoundaryLoop loop = found.FirstOrDefault(l => CadHatchBoundaries.Same(shape, l, tolerance));
                    matched |= loop != null;
                    objects.Add(loop == null ? new List<DxfEntity>() : FollowedBy(s.Document, new[] { loop })[0]);
                }

                if (matched)
                {
                    h.SetAssociatedObjects(objects);
                }
            });

            if (!matched)
            {
                c.Write("Valid hatch boundary not found.");
            }
        }

        /// <summary>
        /// separate Hatches: a hatch of several areas made into one hatch of each, an area being an outer loop with the islands
        /// after it; each keeps the pattern and what the hatch follows. Nothing is done to a hatch of one area.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchSeparate(CadCommandContext c, List<DxfHatch> hatches)
        {
            HatchEditApply(c, hatches, (s, h) =>
            {
                var first = new List<int>();

                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    if ((h.LoopTypeAt(i) & DxfHatchLoopTypes.External) != 0 || first.Count == 0)
                    {
                        first.Add(i);
                    }
                }

                if (first.Count < 2)
                {
                    return;
                }

                bool follow = h.Associative;
                var objects = new List<IReadOnlyList<DxfEntity>>();

                for (int i = 0; i < h.NumberOfLoops; i++)
                {
                    objects.Add(h.GetAssociatedObjectsAt(i));
                }

                for (int area = first.Count - 1; area >= 0; area--)
                {
                    int from = first[area];
                    int to = area + 1 < first.Count ? first[area + 1] : objects.Count;
                    DxfHatch own = h;

                    if (area > 0)
                    {
                        own = (DxfHatch)h.Clone();
                        s.Append(c.Space, own);
                    }

                    // Every loop but this area's is taken out of the copy, and the area's alone of the hatch left last.
                    for (int i = own.NumberOfLoops - 1; i >= 0; i--)
                    {
                        if (i < from || i >= to)
                        {
                            own.RemoveLoopAt(i);
                        }
                    }

                    if (follow)
                    {
                        own.SetAssociatedObjects(objects.GetRange(from, to - from));
                    }
                }
            });
            yield break;
        }

        /// <summary>
        /// Origin: "[Use current origin/Set new origin/Default to boundary extents]": Set new origin asks "Select point:" and lays
        /// the pattern of every hatch from it, Default to boundary extents asks the corner or the middle of the box round each
        /// hatch's outer loops; a solid fill, a gradient and lines of the user's have no lines to lay.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchEditOrigin(CadCommandContext c, List<DxfHatch> hatches)
        {
            string[] choices = { "Use current origin", "Set new origin", "Default to boundary extents" };
            yield return CadPrompt.ForKeyword("Specify hatch origin", choices, choices[0]);
            GeoPoint2? point = null;
            CadHatchOriginMode mode = CadHatchOriginMode.Current;

            if (c.Input.Is(choices[1]))
            {
                yield return CadPrompt.ForPoint("Select point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                point = c.Input.Point;
            }
            else if (c.Input.Is(choices[2]))
            {
                yield return CadPrompt.ForKeyword("Specify corner", HatchCorners, HatchCorners[0]);
                int corner = Array.FindIndex(HatchCorners, o => c.Input.Is(o));
                mode = (CadHatchOriginMode)(Math.Max(corner, 0) + 1);
            }
            else
            {
                yield break;
            }

            HatchEditApply(c, hatches, (s, h) =>
            {
                GeoPoint2? origin = point;

                if (
                    origin == null
                    && CadHatchBoundaries.Box(
                        CadHatchBoundaries.Shapes(h),
                        out double x0,
                        out double y0,
                        out double x1,
                        out double y1
                    )
                )
                {
                    origin =
                        mode == CadHatchOriginMode.BottomLeft ? new GeoPoint2(x0, y0)
                        : mode == CadHatchOriginMode.BottomRight ? new GeoPoint2(x1, y0)
                        : mode == CadHatchOriginMode.TopRight ? new GeoPoint2(x1, y1)
                        : mode == CadHatchOriginMode.TopLeft ? new GeoPoint2(x0, y1)
                        : new GeoPoint2((x0 + x1) / 2.0, (y0 + y1) / 2.0);
                }

                if (origin.HasValue)
                {
                    CadHatchOrigin.MoveTo(h, origin.Value);
                }
            });
        }

        /// <summary>The colour and background colour of the hatches, asked as -HATCH asks them; the drawing's current colour where a dot is typed, no background for none.</summary>
        private static IEnumerable<CadPrompt> HatchEditColors(CadCommandContext c, List<DxfHatch> hatches)
        {
            DxfColor background = hatches[0].BackgroundColor;
            var settings = new CadHatchSettings
            {
                Color = hatches[0].Color,
                BackgroundColor = background.IsNone ? null : background,
            };
            CadHatchSettings kept = c.Document.HatchSettings;

            try
            {
                foreach (CadPrompt prompt in HatchColors(c, settings))
                {
                    yield return prompt;
                }
            }
            finally
            {
                // -HATCHEDIT's colours are the hatches', not the next hatch's (HPCOLOR).
                c.Document.HatchSettings = kept;
            }

            DxfColor current = Current(c, new DxfHatch()).Color;
            HatchEditApply(c, hatches, (s, h) =>
            {
                h.Color = settings.Color ?? current;
                h.BackgroundColor = settings.BackgroundColor ?? DxfColor.FromColorIndex(DxfColorMethod.None, 0);
            });
        }

        /// <summary>The layer of the hatches, asked as -HATCH asks it; the drawing's current layer where a dot is typed.</summary>
        private static IEnumerable<CadPrompt> HatchEditLayer(CadCommandContext c, List<DxfHatch> hatches)
        {
            var settings = new CadHatchSettings { Layer = hatches[0].Layer };
            CadHatchSettings kept = c.Document.HatchSettings;

            try
            {
                foreach (CadPrompt prompt in HatchLayer(c, settings))
                {
                    yield return prompt;
                }
            }
            finally
            {
                c.Document.HatchSettings = kept;
            }

            string layer = settings.Layer ?? Current(c, new DxfHatch()).Layer;
            HatchEditApply(c, hatches, (s, h) => h.Layer = layer);
        }

        /// <summary>The transparency of the hatches, asked as -HATCH asks it; the drawing's current one where a dot is typed.</summary>
        private static IEnumerable<CadPrompt> HatchEditTransparency(CadCommandContext c, List<DxfHatch> hatches)
        {
            var settings = new CadHatchSettings { Transparency = hatches[0].Transparency };
            CadHatchSettings kept = c.Document.HatchSettings;

            try
            {
                foreach (CadPrompt prompt in HatchTransparency(c, settings))
                {
                    yield return prompt;
                }
            }
            finally
            {
                c.Document.HatchSettings = kept;
            }

            DxfTransparency transparency = settings.Transparency ?? Current(c, new DxfHatch()).Transparency;
            HatchEditApply(c, hatches, (s, h) => h.Transparency = transparency);
        }
    }
}
