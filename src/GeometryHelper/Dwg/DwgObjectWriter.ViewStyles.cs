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

            public static ViewColour Of(Cursor groups)
            {
                var colour = new ViewColour { Index = groups.Int(62, 256), Rgb = -1 };

                if (groups.At(420))
                {
                    colour.Rgb = groups.Int(420);
                }

                return colour;
            }
        }

        private void Write(ViewColour colour)
        {
            if (colour.Index == 257 && colour.Rgb < 0)
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
        /// recompute, its display name and its flags.
        /// </summary>
        private void WriteModelDocViewStyle()
        {
            var style = new Cursor(_record.Of("AcDbModelDocViewStyle"));
            _data.BitShort(style.Int(70));
            T(style.Text(3));
            _data.Bit(style.Int(290) != 0);
            T(style.Text(300));
            _data.BitLong(style.Int(90));
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
            ViewColour hatchBackground = ViewColour.Of(style);
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
