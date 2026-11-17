using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// Objects selected before a command (PICKFIRST 1), taken by it as AutoCAD 2021's console takes them, probed one command at
    /// a time: with a selection made first, LAYISO and the rest of the commands that start with "Select objects" use it, say
    /// "N found" where AutoCAD says it, filter what they cannot work on, and clear the grips when they end; the commands whose
    /// first question is for one object (LAYOFF, FILLET, OFFSET) and those AutoCAD asks anyway (REGION, UNION, MASSPROP) still ask.
    /// </summary>
    public sealed class PreSelectionTests
    {
        /// <summary>
        /// The plan (A1 a line on Steel, A2 a line, A3 a circle, A4 an arc, A5 a polyline, A6 the text NOTE, A7 an INSERT of BOX,
        /// A8 a line on the locked layer) with a spline, a hatch, an MTEXT and a rotated dimension added.
        /// </summary>
        private static CadDocument Mixed()
        {
            DxfDatabase db = PlanDatabase();
            db.ModelSpace.AppendEntity(
                new DxfSpline(
                    3,
                    false,
                    false,
                    false,
                    new[]
                    {
                        new GeoPoint3(0, 400, 0),
                        new GeoPoint3(50, 450, 0),
                        new GeoPoint3(100, 400, 0),
                        new GeoPoint3(150, 450, 0),
                        new GeoPoint3(200, 400, 0),
                    },
                    new[] { 0.0, 0, 0, 0, 0.5, 1, 1, 1, 1 },
                    null,
                    0,
                    0
                )
            );
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
            hatch.PatternScale = 2.0;
            hatch.AppendLoop(
                DxfHatchLoopTypes.External,
                new[]
                {
                    new GeoPoint2(20, 600),
                    new GeoPoint2(40, 600),
                    new GeoPoint2(40, 620),
                    new GeoPoint2(20, 620),
                },
                null
            );
            db.ModelSpace.AppendEntity(hatch);
            var mtext = new DxfMText
            {
                Contents = "mtext line",
                TextHeight = 5.0,
                Width = 80,
                Location = new GeoPoint3(0, 300, 0),
            };
            db.ModelSpace.AppendEntity(mtext);
            var dimension = new DxfRotatedDimension(
                0.0,
                new GeoPoint3(0, -50, 0),
                new GeoPoint3(100, -50, 0),
                new GeoPoint3(50, -70, 0),
                string.Empty,
                null
            );
            var picture = new DxfBlockTableRecord { Name = "*D" };
            db.BlockTable.Add(picture);
            dimension.DimBlock = picture;
            db.ModelSpace.AppendEntity(dimension);
            return CadDocument.From(db, "mixed.dxf");
        }

        /// <summary>The handle a key names: A1 to A8 of the plan, or the first spline, hatch, mtext or dimension of the drawing.</summary>
        private static string Resolve(CadDocument document, string key)
        {
            if (key.StartsWith("A", StringComparison.Ordinal))
            {
                return key;
            }

            foreach (DxfEntity entity in document.Database.ModelSpace)
            {
                if (
                    (key == "SPLINE" && entity is DxfSpline)
                    || (key == "HATCH" && entity is DxfHatch)
                    || (key == "MTEXT" && entity is DxfMText)
                    || (key == "DIM" && entity is DxfDimension)
                )
                {
                    return entity.Handle;
                }
            }

            throw new ArgumentException("No entity for the key " + key);
        }

        private static CadCommandSession Started(
            string command,
            string[] keys,
            out CadDocument document,
            params object[] before
        )
        {
            CadDocument made = Mixed();
            document = made;
            CadCommandSession session = Session(made);
            session.Tolerance = Tolerance.Default;
            Run(session, before);
            made.Selection.Set(keys.Select(k => Resolve(made, k)).ToList());
            Run(session, command);
            return session;
        }

        private static List<string> FoundLines(CadCommandSession session) =>
            session.History.Where(l => l.EndsWith(" found", StringComparison.Ordinal)).ToList();

        /// <summary>
        /// The repro of the maintainer's test: a line selected, then LAYISO. It isolates the layer of the line at once, with
        /// "1 found", and the grips are gone; it used to ask "Select objects on the layer(s) to be isolated:".
        /// </summary>
        [Fact]
        public void ALineSelectedFirst_ThenLayiso_IsolatesItsLayerWithoutAsking()
        {
            CadCommandSession session = Started("LAYISO", new[] { "A1" }, out CadDocument document);

            Assert.False(session.IsRunning);
            Assert.Equal("1 found", Assert.Single(FoundLines(session)));
            Assert.Equal("Layer \"Steel\" has been isolated.", session.History.Last());
            Assert.True(document.Database.LayerTable["0"].IsOff);
            Assert.False(document.Database.LayerTable["Steel"].IsOff);
            Assert.Empty(document.Selection.Handles);
        }

        /// <summary>
        /// Without a selection made first LAYISO asks as it did, and what is picked there is isolated.
        /// </summary>
        [Fact]
        public void WithNothingSelectedFirst_LayisoAsksAsBefore()
        {
            CadCommandSession session = Started("LAYISO", Array.Empty<string>(), out CadDocument document);

            Assert.Equal("Select objects on the layer(s) to be isolated:", session.PromptText);
            Run(session, new[] { "A1" }, Enter);
            Assert.False(session.IsRunning);
            Assert.True(document.Database.LayerTable["0"].IsOff);
        }

        /// <summary>With PICKFIRST 0 a command asks whatever was selected first, LAYISO too.</summary>
        [Fact]
        public void WithPickfirstZero_LayisoAsksWhateverWasSelected()
        {
            CadCommandSession session = Started("LAYISO", new[] { "A1" }, out CadDocument document, "PICKFIRST", "0");
            Assert.Equal("Select objects on the layer(s) to be isolated:", session.PromptText);
            Run(session, Esc);
        }

        /// <summary>
        /// The commands that take a selection made first, each as AutoCAD 2021's console took it (probed with the same objects
        /// selected by SSSETFIRST): the prompt it asks next (null where it ends), and the "N found" it says (null where it says
        /// nothing: ALIGN, DRAWORDER, MIRROR3D, and the commands for one object).
        /// </summary>
        public static IEnumerable<object[]> Takers()
        {
            const string Base = "Specify base point or [Displacement] <Displacement>:";
            yield return new object[] { "ERASE", new[] { "A1", "A3" }, null, "2 found" };
            yield return new object[] { "MOVE", new[] { "A1", "A3" }, Base, "2 found" };
            yield return new object[]
            {
                "COPY",
                new[] { "A1", "A3" },
                "Specify base point or [Displacement/mOde]",
                "2 found",
            };
            yield return new object[] { "ROTATE", new[] { "A1", "A3" }, "Specify base point:", "2 found" };
            yield return new object[] { "SCALE", new[] { "A1", "A3" }, "Specify base point:", "2 found" };
            yield return new object[]
            {
                "MIRROR",
                new[] { "A1", "A3" },
                "Specify first point of mirror line:",
                "2 found",
            };
            yield return new object[] { "MIRROR3D", new[] { "A1" }, "Specify first point of mirror plane", null };
            yield return new object[] { "STRETCH", new[] { "A1", "A3" }, Base, "2 found" };
            yield return new object[] { "ALIGN", new[] { "A1" }, "Specify first source point:", null };
            yield return new object[] { "EXPLODE", new[] { "A5" }, null, "1 found" };
            yield return new object[] { "CHPROP", new[] { "A1" }, "Enter property to change", "1 found" };
            yield return new object[] { "CHANGE", new[] { "A1" }, "Specify change point or [Properties]:", "1 found" };
            yield return new object[] { "DRAWORDER", new[] { "A1", "A3" }, "Enter object ordering option", null };
            yield return new object[] { "GROUP", new[] { "A1", "A3" }, null, "2 found" };
            yield return new object[] { "HIDEOBJECTS", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "ISOLATEOBJECTS", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "LAYISO", new[] { "A1", "A3" }, null, "2 found" };
            yield return new object[] { "LAYCUR", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "REVERSE", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "ARRAYRECT", new[] { "A1" }, "Select grip to edit array", "1 found" };
            yield return new object[] { "ARRAYPOLAR", new[] { "A1" }, "Specify center point of array", "1 found" };
            yield return new object[] { "ARRAYPATH", new[] { "A1" }, "Select path curve:", "1 found" };
            yield return new object[] { "BURST", new[] { "A7" }, null, "1 found" };
            yield return new object[] { "TXTEXP", new[] { "A6" }, null, "1 found" };
            yield return new object[]
            {
                "JUSTIFYTEXT",
                new[] { "A6", "A1" },
                "Enter a justification option",
                "1 found",
            };
            yield return new object[]
            {
                "SCALETEXT",
                new[] { "A6", "MTEXT", "A1" },
                "Enter a base point option for scaling",
                "2 found",
            };
            yield return new object[] { "TXT2MTXT", new[] { "A6" }, null, "1 found" };
            yield return new object[]
            {
                "TEXTALIGN",
                new[] { "A6", "MTEXT" },
                "Select text object to align to",
                "2 found",
            };
            yield return new object[] { "DIMDISASSOCIATE", new[] { "A1", "DIM" }, null, "1 found" };
            yield return new object[]
            {
                "DIMREASSOCIATE",
                new[] { "A1", "DIM" },
                "Specify first extension line origin",
                "1 found",
            };
            yield return new object[] { "JOIN", new[] { "A1", "A2" }, null, "2 found" };
            yield return new object[] { "PEDIT", new[] { "A5" }, "Enter an option [Close/Join/Width", null };
            yield return new object[] { "PEDIT", new[] { "A1" }, "Do you want to turn it into one?", null };
            yield return new object[] { "SPLINEDIT", new[] { "SPLINE" }, "Enter an option [", null };
            yield return new object[] { "SPLINEDIT", new[] { "SPLINE", "A1", "A3" }, "Enter an option [", null };
            yield return new object[] { "HATCHEDIT", new[] { "HATCH" }, "Enter a pattern name or", null };
            yield return new object[] { "-HATCHEDIT", new[] { "HATCH" }, "Enter hatch option", "1 found" };
            yield return new object[] { "HATCHGENERATEBOUNDARY", new[] { "HATCH" }, null, "1 found" };
            yield return new object[] { "HATCHSETORIGIN", new[] { "HATCH" }, "Select new hatch origin", "1 found" };
            yield return new object[] { "XCLIP", new[] { "A7" }, "Enter clipping option", "1 found" };
            yield return new object[] { "OBJECTSCALE", new[] { "A1" }, null, null };
            yield return new object[] { "-OBJECTSCALE", new[] { "A1" }, null, null };
            yield return new object[] { "AIOBJECTSCALEADD", new[] { "A1" }, null, null };
            yield return new object[] { "DELCONSTRAINT", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "CONSTRAINTBAR", new[] { "A1" }, "Enter an option", "1 found" };
            yield return new object[] { "DCDISPLAY", new[] { "A1" }, "Enter an option", "1 found" };
            yield return new object[] { "MLEADERALIGN", new[] { "A1" }, null, "1 found" };
            yield return new object[] { "MLEADERCOLLECT", new[] { "A1" }, "Select multileaders:", "1 found" };
            yield return new object[] { "STLOUT", new[] { "A1" }, "Select solids or watertight meshes:", "1 found" };
            yield return new object[] { "TRANSPARENCY", new[] { "A1" }, "Select image(s):", "0 found" };
            yield return new object[] { "IMAGEADJUST", new[] { "A1" }, "Select image(s):", "0 found" };
            yield return new object[] { "PDFADJUST", new[] { "A1" }, "Select PDF underlay:", "0 found" };
            yield return new object[] { "RESETBLOCK", new[] { "A7" }, null, null };
            yield return new object[] { "SELECTSIMILAR", new[] { "A1" }, null, null };
            yield return new object[] { "TEXTEDIT", new[] { "A6" }, "Edit text:", null };
        }

        [Theory]
        [MemberData(nameof(Takers))]
        public void ACommandThatStartsWithSelectObjects_TakesWhatWasSelectedFirst(
            string command,
            string[] keys,
            string prompt,
            string found
        )
        {
            CadCommandSession session = Started(command, keys, out CadDocument document);

            if (prompt == null)
            {
                Assert.False(session.IsRunning);

                // The grips are gone once the command is done with the objects, as AutoCAD clears them; SELECTSIMILAR leaves
                // what it selected.
                if (command != "SELECTSIMILAR")
                {
                    Assert.Empty(document.Selection.Handles);
                }
            }
            else
            {
                Assert.True(session.IsRunning);
                Assert.StartsWith(prompt, session.PromptText, StringComparison.Ordinal);
                Run(session, Esc);
            }

            Assert.False(session.IsRunning);

            if (found == null)
            {
                Assert.Empty(FoundLines(session));
            }
            else
            {
                Assert.Equal(found, Assert.Single(FoundLines(session)));
            }
        }

        /// <summary>
        /// What AutoCAD 2021's console asks anyway with objects selected first: the commands for one object, and the region
        /// and solid commands, which are not made to take a selection.
        /// </summary>
        public static IEnumerable<object[]> Askers()
        {
            yield return new object[] { "REGION", new[] { "A3" }, "Select objects:" };
            yield return new object[] { "UNION", new[] { "A3" }, "Select objects:" };
            yield return new object[] { "INTERSECT", new[] { "A3" }, "Select objects:" };
            yield return new object[] { "MASSPROP", new[] { "A3" }, "Select objects:" };
            yield return new object[] { "LAYOFF", new[] { "A1" }, "Select an object on the layer to be turned off" };
            yield return new object[] { "LAYFRZ", new[] { "A1" }, "Select an object on the layer to be frozen" };
            yield return new object[] { "LAYLCK", new[] { "A1" }, "Select an object on the layer to be locked" };
            yield return new object[] { "LAYULK", new[] { "A1" }, "Select an object on the layer to be unlocked" };
            yield return new object[] { "FILLET", new[] { "A1" }, "Select first object" };
            yield return new object[] { "OFFSET", new[] { "A1" }, "Specify offset distance" };
            yield return new object[] { "BREAK", new[] { "A1" }, "Select object:" };
            yield return new object[] { "DIVIDE", new[] { "A1" }, "Select object to divide:" };
            yield return new object[] { "MEASURE", new[] { "A1" }, "Select object to measure:" };
            yield return new object[] { "TRIM", new[] { "A1" }, "Select object to trim or shift-select to extend" };
            yield return new object[] { "EXTEND", new[] { "A1" }, "Select object to extend or shift-select to trim" };
            yield return new object[] { "PEDIT", new[] { "A5", "A1" }, "Select polyline or [Multiple]:" };
        }

        [Theory]
        [MemberData(nameof(Askers))]
        public void ACommandAutoCADAsksAnyway_AsksAndLeavesTheSelection(string command, string[] keys, string prompt)
        {
            CadCommandSession session = Started(command, keys, out CadDocument document);

            Assert.True(session.IsRunning);
            Assert.StartsWith(prompt, session.PromptText, StringComparison.Ordinal);
            Assert.Empty(FoundLines(session));
            Assert.Equal(
                keys.Select(k => Resolve(document, k)).OrderBy(h => h, StringComparer.Ordinal),
                document.Selection.Handles.OrderBy(h => h, StringComparer.Ordinal)
            );
            Run(session, Esc);
        }

        /// <summary>In Standard mode TRIM and EXTEND take the objects selected first as the cutting or boundary edges.</summary>
        [Theory]
        [InlineData("TRIM", "Select cutting edges ...", "Select object to trim or shift-select to extend")]
        [InlineData("EXTEND", "Select boundary edges ...", "Select object to extend or shift-select to trim")]
        public void InStandardMode_TrimAndExtendTakeTheSelectionAsEdges(string command, string written, string prompt)
        {
            CadCommandSession session = Started(
                command,
                new[] { "A1", "A3" },
                out CadDocument document,
                "TRIMEXTENDMODE",
                "0"
            );

            Assert.True(session.IsRunning);
            Assert.StartsWith(prompt, session.PromptText, StringComparison.Ordinal);
            Assert.Contains(written, session.History);
            Assert.Equal("2 found", Assert.Single(FoundLines(session)));
            Run(session, Esc);
        }
    }
}
