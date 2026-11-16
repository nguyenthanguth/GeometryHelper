using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The detail and section view styles and the manager of the persistent names of subentities, from the groups their DXF
    /// gives, into the order the file keeps (the reader's <c>ReadDetailViewStyle</c> and the like give the other way).
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        /// <summary>A colour of a view style's part from its DXF groups: 62, then 420 for a true colour; 257 is none.</summary>
        private struct ViewColour
        {
            public int Index;
            public int Rgb;

            public static ViewColour Of(Cursor groups, int otherwise = 256)
            {
                var colour = new ViewColour { Index = groups.Int(62, otherwise), Rgb = -1 };

                if (groups.At(420))
                {
                    colour.Rgb = groups.Int(420);
                }

                return colour;
            }
        }

        private void Write(ViewColour colour)
        {
            if (R2004Plus && colour.Index == 257 && colour.Rgb < 0)
            {
                _data.BitShort(0);
                _data.BitLong(unchecked((int)0xC8000000));
                _data.RawChar(0);
                return;
            }

            WriteColour(colour.Index, colour.Rgb);
        }

        /// <summary>
        /// The part every model documentation view style starts with: its version, description, whether it is modified for
        /// recompute and, from R2018, its display name and its flags (a file before it keeps those in a record of the style's
        /// extension dictionary, which is written as any other; a style of that form written as R2018 takes them from it).
        /// </summary>
        private void WriteModelDocViewStyle()
        {
            var style = new Cursor(_record.Of("AcDbModelDocViewStyle"));
            _data.BitShort(style.Int(70));
            T(style.Text(3));
            _data.Bit(style.Int(290) != 0);

            if (R2018Plus)
            {
                DwgRecord kept = style.At(300) ? null : NameRecordOfViewStyle(_record);
                string name = style.Text(300);
                int flags = style.Int(90);

                if (kept != null)
                {
                    List<KeyValuePair<int, string>> groups = kept.Body;

                    for (int i = 0; i + 1 < groups.Count; i++)
                    {
                        if (groups[i].Key == 102 && groups[i].Value.Trim() == "DISPLAYNAME" && groups[i + 1].Key == 1)
                        {
                            name = groups[i + 1].Value;
                        }
                        else if (groups[i].Key == 102 && groups[i].Value.Trim() == "FLAGS" && groups[i + 1].Key == 90)
                        {
                            flags = DwgRecord.Int(groups[i + 1].Value);
                        }
                    }
                }

                T(name);
                _data.BitLong(flags);
            }
        }

        /// <summary>
        /// The record of the extension dictionary of a detail or section view style of R2013 and before that holds its display
        /// name and its flags (<c>DISPLAYNAME</c> and <c>FLAGS</c> under <c>ACAD_XREC_ROUNDTRIP</c>); null for a style of
        /// another form or with none.
        /// </summary>
        private DwgRecord NameRecordOfViewStyle(DwgRecord style)
        {
            if ((style.Type != "ACDBDETAILVIEWSTYLE" && style.Type != "ACDBSECTIONVIEWSTYLE") || style.XDictionary == 0)
            {
                return null;
            }

            DwgRecord dictionary = _drawing.Find(style.XDictionary);

            if (dictionary == null || dictionary.Type != "DICTIONARY")
            {
                return null;
            }

            DwgGroups entries = dictionary.Of("AcDbDictionary");
            int at = entries.Each(3).FindIndex(name => name.Trim() == "ACAD_XREC_ROUNDTRIP");
            DwgRecord record = at < 0 ? null : _drawing.Find(DwgRecord.Hex(entries.Each(360)[at]));
            return
                record != null
                && record.Type == "XRECORD"
                && record.Body.Exists(g => g.Key == 102 && g.Value.Trim() == "DISPLAYNAME")
                ? record
                : null;
        }

        /// <summary>
        /// Whether a record is what AutoCAD takes into a detail or section view style when it saves it as R2018: the extension
        /// dictionary of a style of R2013 and before and its record of the display name and the flags.
        /// </summary>
        public bool IsRoundTripOfViewStyle(DwgRecord record)
        {
            if (record.Type != "XRECORD" && record.Type != "DICTIONARY")
            {
                return false;
            }

            DwgRecord owner = _drawing.Find(record.Owner);

            if (record.Type == "XRECORD")
            {
                return owner != null && IsRoundTripOfViewStyle(owner);
            }

            return record.Type == "DICTIONARY"
                && owner != null
                && owner.XDictionary == record.Handle
                && NameRecordOfViewStyle(owner) != null;
        }

        private void WriteDetailViewStyle()
        {
            WriteModelDocViewStyle();
            var style = new Cursor(_record.Of("AcDbDetailViewStyle"));
            int version = style.Int(70);
            style.Int(71);
            int flags = style.Int(90, 3);
            style.Int(71);
            ulong identifierStyle = style.Handle(340);
            ViewColour identifierColour = ViewColour.Of(style);
            double identifierHeight = style.Double(40, 0.24);
            ulong arrow = style.Handle(340);
            ViewColour arrowColour = ViewColour.Of(style);
            double arrowSize = style.Double(40, 0.24);
            string excluded = style.Text(300);
            double identifierOffset = style.Double(40, 0.36);
            int placement = style.Int(280, 3);
            style.Int(71);
            ulong boundaryType = style.Handle(340);
            int boundaryWeight = style.Int(90, 25);
            ViewColour boundaryColour = ViewColour.Of(style);
            style.Int(71);
            ulong labelStyle = style.Handle(340);
            ViewColour labelColour = ViewColour.Of(style);
            double labelHeight = style.Double(40, 0.24);
            int attachment = style.Int(90, 1);
            double labelOffset = style.Double(40, 0.75);
            int alignment = style.Int(90, 1);
            string pattern = style.Text(300);
            style.Int(71);
            ulong borderType = style.Handle(340);
            int borderWeight = style.Int(90, 25);
            ViewColour borderColour = ViewColour.Of(style);
            ulong connectionType = style.Handle(340);
            int connectionWeight = style.Int(90, 25);
            ViewColour connectionColour = ViewColour.Of(style);
            int edge = style.Int(280);

            _data.BitShort(version);
            _data.BitLong(flags);
            Write(identifierColour);
            _data.BitDouble(identifierHeight);
            T(excluded);
            _data.BitDouble(identifierOffset);
            _data.RawChar(placement);
            Write(arrowColour);
            _data.BitDouble(arrowSize);
            _data.BitLong(boundaryWeight);
            Write(boundaryColour);
            Write(labelColour);
            _data.BitDouble(labelHeight);
            _data.BitLong(attachment);
            _data.BitDouble(labelOffset);
            _data.BitLong(alignment);
            T(pattern);
            _data.BitLong(borderWeight);
            Write(borderColour);
            _data.BitLong(connectionWeight);
            Write(connectionColour);
            _data.RawChar(edge);
            H(5, identifierStyle);
            H(5, arrow);
            H(5, boundaryType);
            H(5, labelStyle);
            H(5, borderType);
            H(5, connectionType);
        }

        private void WriteSectionViewStyle()
        {
            WriteModelDocViewStyle();
            var style = new Cursor(_record.Of("AcDbSectionViewStyle"));
            int version = style.Int(70);
            style.Int(71);
            int flags = style.Int(90, 78);
            style.Int(71);
            ulong identifierStyle = style.Handle(340);
            ViewColour identifierColour = ViewColour.Of(style);
            double identifierHeight = style.Double(40, 0.24);
            ulong arrowStart = style.Handle(340);
            ulong arrowEnd = style.Handle(340);
            ViewColour arrowColour = ViewColour.Of(style);
            double arrowSize = style.Double(40, 0.24);
            string excluded = style.Text(300);
            double extension = style.Double(40, 0.48);
            int identifierPosition = style.Int(90, 3);
            double identifierOffset = style.Double(40, 0.18);
            int arrowPosition = style.Int(90, 1);
            style.Int(71);
            ulong planeType = style.Handle(340);
            int planeWeight = style.Int(90, 25);
            ViewColour planeColour = ViewColour.Of(style);
            ulong bendType = style.Handle(340);
            int bendWeight = style.Int(90, 50);
            ViewColour bendColour = ViewColour.Of(style);
            double bendLength = style.Double(40, 0.24);
            double overshoot = style.Double(40);
            double endLength = style.Double(40, 0.24);
            style.Int(71);
            ulong labelStyle = style.Handle(340);
            ViewColour labelColour = ViewColour.Of(style);
            double labelHeight = style.Double(40, 0.24);
            int attachment = style.Int(90, 1);
            double labelOffset = style.Double(40, 0.75);
            int alignment = style.Int(90, 1);
            string pattern = style.Text(300);
            style.Int(71);
            ViewColour hatchColour = ViewColour.Of(style);
            ViewColour hatchBackground = ViewColour.Of(style, R2004Plus ? 256 : 257);
            string hatchPattern = style.Text(300);
            double hatchScale = style.Double(40, 1.0);
            int hatchTransparency = style.Int(90);
            bool first = style.Int(290) != 0;
            bool second = style.Int(290) != 0;
            int count = style.Int(90);
            var angles = new List<double>();

            while (angles.Count < count && style.At(40))
            {
                angles.Add(style.Double(40));
            }

            _data.BitShort(version);
            _data.BitLong(flags);
            Write(identifierColour);
            _data.BitDouble(identifierHeight);
            Write(arrowColour);
            _data.BitDouble(arrowSize);
            T(excluded);
            _data.BitDouble(extension);
            _data.BitLong(planeWeight);
            Write(planeColour);
            _data.BitLong(bendWeight);
            Write(bendColour);
            _data.BitDouble(bendLength);
            _data.BitDouble(endLength);
            Write(labelColour);
            _data.BitDouble(labelHeight);
            _data.BitLong(attachment);
            _data.BitDouble(labelOffset);
            _data.BitLong(alignment);
            T(pattern);
            Write(hatchColour);
            Write(hatchBackground);
            T(hatchPattern);
            _data.BitDouble(hatchScale);
            _data.BitLong(hatchTransparency);
            _data.Bit(first);
            _data.Bit(second);
            _data.BitLong(identifierPosition);
            _data.BitDouble(identifierOffset);
            _data.BitLong(arrowPosition);
            _data.BitDouble(overshoot);
            _data.BitLong(angles.Count);

            foreach (double angle in angles)
            {
                _data.BitDouble(angle);
            }

            H(5, identifierStyle);
            H(5, arrowStart);
            H(5, arrowEnd);
            H(5, planeType);
            H(5, bendType);
            H(5, labelStyle);
        }

        /// <summary>
        /// An ACDBASSOCPERSSUBENTMANAGER: its whole numbers (90) in their order, then its yes or no (290), as its DXF gives
        /// them; of version 2 the DXF gives no identifiers, and none are written.
        /// </summary>
        private void WriteAssocPersSubentManager()
        {
            DwgGroups manager = _record.Of("AcDbAssocPersSubentManager");

            foreach (string value in manager.Each(90))
            {
                _data.BitLong(DwgRecord.Int(value));
            }

            _data.Bit(manager.Int(290) != 0);
        }
    }
}
