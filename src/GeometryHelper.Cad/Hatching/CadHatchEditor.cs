using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// What the Hatch Editor tab does to the hatches selected: a value changed in it is one undo step named HATCHEDIT, written
    /// as HATCHEDIT and -HATCHEDIT write it, so that the tab and the commands leave the same hatch.
    /// </summary>
    internal static class CadHatchEditor
    {
        /// <summary>The name of the undo step of an edit made in the tab.</summary>
        internal const string Step = "HATCHEDIT";

        /// <summary>
        /// Changes hatches with an action, one undo step: each hatch found again in the drawing as the edit begins. A hatch the
        /// drawing's owner of it allows no change is refused in the owner's words and nothing is changed.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the hatches.</param>
        /// <param name="change">Changes a hatch it is given.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult Edit(
            CadDocument document,
            IReadOnlyList<string> handles,
            Action<CadEditScope, DxfHatch> change
        )
        {
            var planned = handles
                .Select(h => new CadEditRecord(CadEditRecordKind.PropertyChanged, h, "Hatch"))
                .ToList();
            return document.ApplyEdit(Step, planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (s.Document.FindEntity(handle) is DxfHatch hatch)
                    {
                        change(s, hatch);
                    }
                }
            });
        }

        /// <summary>
        /// Puts a hatch of the drawing in the draw order an index of the draw orders -HATCH offers names (do Not assign, send to
        /// Back, bring to Front, send beHind boundary, bring in front of bounDary): to the back or the front of the space, or behind
        /// or in front of the lowest or topmost of the objects the hatch follows; nowhere where it follows none.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="hatch">The hatch.</param>
        /// <param name="order">The index of the order; 0 changes nothing.</param>
        internal static void Place(CadDocument document, DxfHatch hatch, int order)
        {
            if (order <= 0)
            {
                return;
            }

            DxfBlockTableRecord block = document.WorkingSpace.Block;
            DxfDrawOrderTable table = block.CreateDrawOrderTable();
            var moving = new List<DxfEntity> { hatch };

            if (order == 1)
            {
                table.MoveToBottom(moving);
            }
            else if (order == 2)
            {
                table.MoveToTop(moving);
            }
            else
            {
                DxfEntity target = CadCommands.Extreme(block, hatch.GetAssociatedObjects().ToList(), order == 4);

                if (target != null && order == 3)
                {
                    table.MoveBelow(moving, target);
                }
                else if (target != null)
                {
                    table.MoveAbove(moving, target);
                }
            }
        }

        /// <summary>
        /// Gives the hatches the pattern, gradient, scale, angle, island style and annotation of settings, each hatch keeping
        /// the origin its pattern is laid from and whether it follows its objects; one undo step.
        /// </summary>
        /// <param name="document">The drawing.</param>
        /// <param name="handles">The handles of the hatches.</param>
        /// <param name="settings">The settings, as <see cref="CadCommands.HatchSettingsOf"/> reads them and the tab changed them.</param>
        /// <param name="fault">The fault of a pattern that is in no file, or null.</param>
        /// <returns>What came of it.</returns>
        internal static CadEditResult ApplySettings(
            CadDocument document,
            IReadOnlyList<string> handles,
            CadHatchSettings settings,
            out string fault
        )
        {
            string said = null;
            CadEditResult result = Edit(document, handles, (s, h) =>
            {
                CadHatchSettings own = settings.Clone();
                GeoPoint2 origin = CadHatchOrigin.Of(h);
                own.OriginX = origin.X;
                own.OriginY = origin.Y;
                own.Associative = h.Associative;
                said = CadCommands.ApplyEditedSettings(s, h.Handle, own) ?? said;
            });
            fault = said;
            return result;
        }
    }
}
