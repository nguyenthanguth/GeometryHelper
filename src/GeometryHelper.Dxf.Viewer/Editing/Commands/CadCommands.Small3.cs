using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The registry entries of FILL, QTEXT, LIMITS, TIME, STATUS, MULTIPLE, TRACE, SKETCH, MASSPROP, MINSERT and DIMINSPECT
    /// with -DIMINSPECT, and the variables that go with them: SKETCHINC, SKPOLY and SKTOLERANCE of SKETCH, TDCREATE, TDUCREATE,
    /// TDUPDATE, TDUUPDATE, TDINDWG, TDUSRTIMER and USRTIMER of TIME. Each command is in its own file.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddSmall3To(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("FILL", Fill));
            registry.Add(new CadCommand("QTEXT", QuickText));
            registry.Add(new CadCommand("LIMITS", Limits));
            registry.Add(new CadCommand("TIME", Time));
            registry.Add(new CadCommand("STATUS", Status));
            registry.Add(new CadCommand("MULTIPLE", Multiple));
            registry.Add(new CadCommand("TRACE", Trace));
            registry.Add(new CadCommand("SKETCH", Sketch));
            registry.Add(new CadCommand("MASSPROP", MassProperties));
            registry.Add(new CadCommand("MINSERT", MInsert));
            registry.Add(new CadCommand("DIMINSPECT", DimInspect, true));
            registry.Add(new CadCommand("-DIMINSPECT", DimInspectTyped, true));

            // The times of the drawing, as AutoCAD shows them: Julian dates to eight places, each only read.
            registry.AddVariable(TimeVariable("TDCREATE", t => CadDocumentTimes.Julian(t.Created)));
            registry.AddVariable(TimeVariable("TDUCREATE", t => CadDocumentTimes.Julian(t.Created.ToUniversalTime())));
            registry.AddVariable(TimeVariable("TDUPDATE", t => CadDocumentTimes.Julian(t.Updated)));
            registry.AddVariable(TimeVariable("TDUUPDATE", t => CadDocumentTimes.Julian(t.Updated.ToUniversalTime())));
            registry.AddVariable(TimeVariable("TDINDWG", t => Days(t.Editing)));
            registry.AddVariable(TimeVariable("TDUSRTIMER", t => Days(t.Timer)));
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "USRTIMER",
                    false,
                    0,
                    1,
                    s => s.Document.Times.TimerRunning ? 1 : 0,
                    (c, v) => c.Document.Times.SetTimer(v != 0)
                )
            );

            // What SKETCH draws with: the length of a piece, the type, and how closely a spline follows the sketch.
            registry.AddVariable(
                CadSystemVariable.Number(
                    "SKETCHINC",
                    true,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => s.Document.Database.Sketchinc,
                    (c, v) => c.Database.Sketchinc = v,
                    "$SKETCHINC",
                    check: v => v > 0.0 ? null : "Cannot set SKETCHINC to that value."
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "SKPOLY",
                    false,
                    0,
                    2,
                    s => SketchType(s),
                    (c, v) => c.Remember(SketchTypeKey, v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Number(
                    "SKTOLERANCE",
                    false,
                    CadSystemVariable.RealBound.Finite,
                    false,
                    s => Kept(s, SketchToleranceKey, 0.5),
                    (c, v) => c.Remember(SketchToleranceKey, v),
                    check: v => v >= 0.0 && v <= 1.0 ? null : "Cannot set SKTOLERANCE to that value."
                )
            );
        }

        /// <summary>A time of the drawing, to read: a Julian date or a count of days, to eight places.</summary>
        private static CadSystemVariable TimeVariable(string name, Func<CadDocumentTimes, string> show) =>
            CadSystemVariable.ReadOnly(
                name,
                CadVariableKind.Real,
                s => show(s.Document.Times),
                s => new[] { double.Parse(show(s.Document.Times), CultureInfo.InvariantCulture) }
            );

        /// <summary>A time spent in days to eight places, as TDINDWG shows it.</summary>
        private static string Days(TimeSpan span) =>
            span.TotalDays.ToString("0.00000000", CultureInfo.InvariantCulture);

        /// <summary>
        /// Changes a switch of the header as one undo step, recorded as a setting changed, and does nothing where it is as it
        /// was: FILL and QTEXT set $FILLMODE and $QTEXTMODE through it.
        /// </summary>
        private static void SetSwitch(
            CadCommandContext c,
            string property,
            bool before,
            bool after,
            Action<DxfDatabase> set
        )
        {
            if (before == after)
            {
                return;
            }

            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.SettingChanged,
                        null,
                        property,
                        before ? "1" : "0",
                        after ? "1" : "0"
                    ),
                },
                s => set(s.Database)
            );
        }
    }
}
