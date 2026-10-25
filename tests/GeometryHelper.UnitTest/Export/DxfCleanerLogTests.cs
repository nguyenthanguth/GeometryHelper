using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Enums;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfModel;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The pointers at what was erased that no list holds, a table cell's at an attribute definition (331) and an XRECORD's
    /// data at an entity (330), kept where they stand as the saving says, and named in the log once, at the first saving that
    /// finds them, not again at each saving after.
    /// </summary>
    public class DxfCleanerLogTests
    {
        [Fact]
        public void APointerLeftAtWhatWasErased_IsNamedInTheLogOnce_WhateverIsSavedAfter()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfDimension followed = db.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "4D");
            db.NamedObjectsDictionary.SetAt("GH_LINK", new DxfXrecord { Data = new List<DxfTypedValue> { new DxfTypedValue(1, "follows"), new DxfTypedValue(330, "4D") } });
            List<string> written = Logged(() =>
            {
                db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single().Erase();
                followed.Erase();
                db.ToText();
                db.ToText();

                // Something else erased: the saving cleans again, and finds the same pointers.
                db.ModelSpace.OfType<DxfDimension>().Single(d => d.Handle == "61").Erase();
                db.ToText();
            });

            string[] left = written.Where(w => w.Contains("pointers at what was removed are left")).ToArray();
            Assert.Single(left);
            Assert.Contains("ACAD_TABLE F6 (331) at 4C", left[0], StringComparison.Ordinal);
            Assert.Contains("XRECORD ", left[0], StringComparison.Ordinal);
            Assert.Contains("(330) at 4D", left[0], StringComparison.Ordinal);

            // The pointers stay, as the saving says.
            string saved = db.ToText();
            Assert.Contains("\n331\n4C\n", Records(saved).Single(r => HandleOf(r) == "F6"), StringComparison.Ordinal);
        }

        private static List<string> Logged(Action action)
        {
            var written = new List<string>();
            Action<GeometryHelperLogLevel, string, Exception> before = GeometryHelperLog.Writer;

            try
            {
                GeometryHelperLog.Enable = true;
                GeometryHelperLog.Writer = (level, message, exception) => written.Add(message);
                action();
            }
            finally
            {
                GeometryHelperLog.Writer = before;
            }

            return written;
        }
    }
}
