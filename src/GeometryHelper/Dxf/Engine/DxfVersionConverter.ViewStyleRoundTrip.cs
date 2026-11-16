using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    internal sealed partial class DxfVersionConverter
    {
        #region The round trip of a view style

        /// <summary>
        /// The display name (300) and the flags (90) that a detail or section view style of R2018 holds among its own groups are
        /// kept by a file before it in the style's extension dictionary, as <c>ACAD_XREC_ROUNDTRIP</c>, an xrecord of
        /// <c>DISPLAYNAME</c> (1) and <c>FLAGS</c> (90): a style going down gets the dictionary and its xrecord, as AutoCAD 2021
        /// writes them, a style going up takes them back into its own groups. A style with an extension dictionary of another
        /// kind, or one whose record is not that, is left as it is.
        /// </summary>
        private void ViewStyleRoundTrip(Record style)
        {
            if (!Subclass(style.Groups, "AcDbModelDocViewStyle", out int start, out int end))
            {
                return;
            }

            if (_target < 1032)
            {
                KeepViewStyleName(style, start, end);
            }
            else
            {
                TakeViewStyleName(style, end);
            }
        }

        private void KeepViewStyleName(Record style, int start, int end)
        {
            if (style.Groups.Exists(g => g.Code == 102 && g.Value == "{ACAD_XDICTIONARY"))
            {
                return;
            }

            int name = style.Groups.FindIndex(start, end - start, g => g.Code == 300);
            int flags = style.Groups.FindIndex(start, end - start, g => g.Code == 90);
            string handle = HandleOf(style);
            string dictionary = handle == null ? null : NewHandle();
            string xrecord = dictionary == null ? null : NewHandle();

            if (xrecord == null)
            {
                return;
            }

            var dictionaryRecord = new Record();
            dictionaryRecord.Groups.AddRange(
                new[]
                {
                    new Group(0, "DICTIONARY"),
                    new Group(5, dictionary),
                    new Group(330, handle),
                    new Group(100, "AcDbDictionary"),
                    Whole(280, 1),
                    Whole(281, 1),
                    new Group(3, "ACAD_XREC_ROUNDTRIP"),
                    new Group(360, xrecord),
                }
            );
            var xrecordRecord = new Record();
            xrecordRecord.Groups.AddRange(
                new[]
                {
                    new Group(0, "XRECORD"),
                    new Group(5, xrecord),
                    new Group(102, "{ACAD_REACTORS"),
                    new Group(330, dictionary),
                    new Group(102, "}"),
                    new Group(330, dictionary),
                    new Group(100, "AcDbXrecord"),
                    Whole(280, 1),
                    new Group(102, "DISPLAYNAME"),
                    new Group(1, name < 0 ? string.Empty : style.Groups[name].Value),
                    new Group(102, "FLAGS"),
                    Whole(90, flags < 0 ? 0 : WholeValue(style.Groups[flags].Value)),
                }
            );
            _added.Add(dictionaryRecord);
            _added.Add(xrecordRecord);
            style.Groups.InsertRange(
                2,
                new[] { new Group(102, "{ACAD_XDICTIONARY"), new Group(360, dictionary), new Group(102, "}") }
            );
        }

        private void TakeViewStyleName(Record style, int end)
        {
            Record xrecord = RoundTripRecord(style);

            if (xrecord == null)
            {
                return;
            }

            int name = xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "DISPLAYNAME");
            int flags = xrecord.Groups.FindIndex(g => g.Code == 102 && g.Value == "FLAGS");

            if (
                name < 0
                || flags < 0
                || name + 1 >= xrecord.Groups.Count
                || flags + 1 >= xrecord.Groups.Count
                || xrecord.Groups[name + 1].Code != 1
                || xrecord.Groups[flags + 1].Code != 90
            )
            {
                return;
            }

            style.Groups.InsertRange(
                end,
                new[] { new Group(300, xrecord.Groups[name + 1].Value), new Group(90, xrecord.Groups[flags + 1].Value) }
            );
            ForgetDictionary(style);
        }

        #endregion
    }
}
