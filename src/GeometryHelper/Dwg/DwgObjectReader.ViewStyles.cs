using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// The detail and section view styles a new drawing holds from AutoCAD 2013 on, in every version AutoCAD saves them, and
    /// the manager of the persistent names of subentities beside the solids. The file keeps each property in an order of its
    /// own; DXF gives them in groups marked 71 0 to 71 4, which these put them back into.
    /// </content>
    internal sealed partial class DwgObjectReader
    {
        /// <summary>
        /// The part every model documentation view style starts with: its version, description and whether it is modified
        /// for recompute; from R2018 its display name and flags, which an older file keeps in a record of its extension
        /// dictionary.
        /// </summary>
        private void ReadModelDocViewStyle()
        {
            _object.Add(100, "AcDbModelDocViewStyle");
            _object.Add(70, _data.BitShort());
            _object.Add(3, T());
            _object.Add(290, _data.Bit() ? 1 : 0);

            if (R2018Plus)
            {
                _object.Add(300, T());
                _object.Add(90, _data.BitLong());
            }
        }

        /// <summary>
        /// The colour of a view style's part: from R2004 its groups (62, and 420 for a true colour); before R2004 DXF gives
        /// none.
        /// </summary>
        private void AddViewColour(DwgColour colour)
        {
            if (R2004Plus)
            {
                AddColour(_object.Body, colour, false);
            }
        }

        /// <summary>
        /// An ACDBDETAILVIEWSTYLE: the identifier and its arrow, the boundary, the view label, the border and the line that
        /// connects them, and the edge of the model.
        /// </summary>
        private void ReadDetailViewStyle()
        {
            ReadModelDocViewStyle();
            int version = _data.BitShort();
            int flags = _data.BitLong();
            DwgColour identifierColour = ReadStyleColour();
            double identifierHeight = _data.BitDouble();
            byte[] excluded = T();
            double identifierOffset = _data.BitDouble();
            int placement = _data.RawChar();
            DwgColour arrowColour = ReadStyleColour();
            double arrowSize = _data.BitDouble();
            int boundaryWeight = _data.BitLong();
            DwgColour boundaryColour = ReadStyleColour();
            DwgColour labelColour = ReadStyleColour();
            double labelHeight = _data.BitDouble();
            int attachment = _data.BitLong();
            double labelOffset = _data.BitDouble();
            int alignment = _data.BitLong();
            byte[] pattern = T();
            int borderWeight = _data.BitLong();
            DwgColour borderColour = ReadStyleColour();
            int connectionWeight = _data.BitLong();
            DwgColour connectionColour = ReadStyleColour();
            int edge = _data.RawChar();

            _object.Add(100, "AcDbDetailViewStyle");
            _object.Add(70, version);
            _object.Add(71, 0);
            _object.Add(90, flags);
            _object.Add(71, 1);
            _object.Add(340, H());
            AddViewColour(identifierColour);
            _object.Add(40, identifierHeight);
            _object.Add(340, H());
            AddViewColour(arrowColour);
            _object.Add(40, arrowSize);
            _object.Add(300, excluded);
            _object.Add(40, identifierOffset);
            _object.Add(280, placement);
            _object.Add(71, 2);
            _object.Add(340, H());
            _object.Add(90, boundaryWeight);
            AddViewColour(boundaryColour);
            _object.Add(71, 3);
            _object.Add(340, H());
            AddViewColour(labelColour);
            _object.Add(40, labelHeight);
            _object.Add(90, attachment);
            _object.Add(40, labelOffset);
            _object.Add(90, alignment);
            _object.Add(300, pattern);
            _object.Add(71, 4);
            _object.Add(340, H());
            _object.Add(90, borderWeight);
            AddViewColour(borderColour);
            _object.Add(340, H());
            _object.Add(90, connectionWeight);
            AddViewColour(connectionColour);
            _object.Add(280, edge);
        }

        /// <summary>
        /// An ACDBSECTIONVIEWSTYLE: the identifier and the arrows, the cutting plane and its bends, the view label and the
        /// hatch, with the angles of the hatch.
        /// </summary>
        private void ReadSectionViewStyle()
        {
            ReadModelDocViewStyle();
            int version = _data.BitShort();
            int flags = _data.BitLong();
            DwgColour identifierColour = ReadStyleColour();
            double identifierHeight = _data.BitDouble();
            DwgColour arrowColour = ReadStyleColour();
            double arrowSize = _data.BitDouble();
            byte[] excluded = T();
            double extension = _data.BitDouble();
            int planeWeight = _data.BitLong();
            DwgColour planeColour = ReadStyleColour();
            int bendWeight = _data.BitLong();
            DwgColour bendColour = ReadStyleColour();
            double bendLength = _data.BitDouble();
            double endLength = _data.BitDouble();
            DwgColour labelColour = ReadStyleColour();
            double labelHeight = _data.BitDouble();
            int attachment = _data.BitLong();
            double labelOffset = _data.BitDouble();
            int alignment = _data.BitLong();
            byte[] pattern = T();
            DwgColour hatchColour = ReadStyleColour();
            DwgColour hatchBackground = ReadStyleColour();
            byte[] hatchPattern = T();
            double hatchScale = _data.BitDouble();
            int hatchTransparency = _data.BitLong();
            bool first = _data.Bit();
            bool second = _data.Bit();
            int identifierPosition = _data.BitLong();
            double identifierOffset = _data.BitDouble();
            int arrowPosition = _data.BitLong();
            double overshoot = _data.BitDouble();
            int count = Count(_data.BitLong());
            var angles = new List<double>(count);

            for (int i = 0; i < count; i++)
            {
                angles.Add(_data.BitDouble());
            }

            _object.Add(100, "AcDbSectionViewStyle");
            _object.Add(70, version);
            _object.Add(71, 0);
            _object.Add(90, flags);
            _object.Add(71, 1);
            _object.Add(340, H());
            AddViewColour(identifierColour);
            _object.Add(40, identifierHeight);
            _object.Add(340, H());
            _object.Add(340, H());
            AddViewColour(arrowColour);
            _object.Add(40, arrowSize);
            _object.Add(300, excluded);
            _object.Add(40, extension);
            _object.Add(90, identifierPosition);
            _object.Add(40, identifierOffset);
            _object.Add(90, arrowPosition);
            _object.Add(71, 2);
            _object.Add(340, H());
            _object.Add(90, planeWeight);
            AddViewColour(planeColour);
            _object.Add(340, H());
            _object.Add(90, bendWeight);
            AddViewColour(bendColour);
            _object.Add(40, bendLength);
            _object.Add(40, overshoot);
            _object.Add(40, endLength);
            _object.Add(71, 3);
            _object.Add(340, H());
            AddViewColour(labelColour);
            _object.Add(40, labelHeight);
            _object.Add(90, attachment);
            _object.Add(40, labelOffset);
            _object.Add(90, alignment);
            _object.Add(300, pattern);
            _object.Add(71, 4);
            AddViewColour(hatchColour);
            AddViewColour(hatchBackground);
            _object.Add(300, hatchPattern);
            _object.Add(40, hatchScale);
            _object.Add(90, hatchTransparency);
            _object.Add(290, first ? 1 : 0);
            _object.Add(290, second ? 1 : 0);
            _object.Add(90, count);

            foreach (double angle in angles)
            {
                _object.Add(40, angle);
            }
        }

        /// <summary>
        /// An ACDBASSOCPERSSUBENTMANAGER, which AutoCAD keeps beside solids: whole numbers, among them a count of the
        /// identifiers given out and those identifiers, then a yes or no. From its version 2 (R2013) DXF gives the count as 0
        /// and leaves the identifiers out, as AutoCAD writes it.
        /// </summary>
        private void ReadAssocPersSubentManager()
        {
            _object.Add(100, "AcDbAssocPersSubentManager");
            int version = _data.BitLong();
            _object.Add(90, version);

            for (int i = 0; i < 5; i++)
            {
                _object.Add(90, _data.BitLong());
            }

            int count = Count(_data.BitLong());
            _object.Add(90, version >= 2 ? 0 : count);

            for (int i = 0; i < count; i++)
            {
                int identifier = _data.BitLong();

                if (version < 2)
                {
                    _object.Add(90, identifier);
                }
            }

            _object.Add(90, _data.BitLong());

            if (version >= 2)
            {
                _object.Add(90, _data.BitLong());
            }

            _object.Add(290, _data.Bit() ? 1 : 0);
        }
    }
}
