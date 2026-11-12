using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// REGION, 3DSOLID and BODY of R2013 and later: the entity carries the GUID of its ACIS data, which the data store of the
    /// file holds (<see cref="DwgDataStoreWriter"/>).
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        private HashSet<ulong> _acis;

        /// <summary>
        /// Whether an ACIS entity is written: from R2013, where it names the GUID of its data (group 2) and the section
        /// ACDSDATA holds that data under its handle. An entity of SAT text, as DXF before R2013 gives it, is not.
        /// </summary>
        private bool HasAcisData(DwgRecord record)
        {
            if (!R2013Plus)
            {
                return false;
            }

            if (_acis == null)
            {
                _acis = new HashSet<ulong>();

                foreach (KeyValuePair<ulong, byte[]> data in _drawing.AcisData)
                {
                    _acis.Add(data.Key);
                }
            }

            return _acis.Contains(record.Handle)
                && Guid.TryParse(record.Of("AcDbModelerGeometry").Text(2).Trim(), out Guid _);
        }

        /// <summary>
        /// A REGION, 3DSOLID or BODY of R2013 and later (<paramref name="solid"/> for a 3DSOLID): a wireframe of no wires
        /// nor silhouettes, as AutoCAD writes it, no materials of faces, whether it is modified (290), the GUID of its data
        /// in the store, and of a 3DSOLID the handle of its history.
        /// </summary>
        private void WriteModelerGeometry(bool solid)
        {
            DwgGroups geometry = _record.Of("AcDbModelerGeometry");
            byte[] guid = Guid.Parse(geometry.Text(2).Trim()).ToByteArray();
            // The wireframe as AutoCAD writes it for a new solid: there, with no point, no wires and no silhouettes.
            _data.Bit(true);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(true);
            _data.BitLong(0);
            _data.Bit(geometry.Int(290) != 0);
            _data.BitLong(BitConverter.ToInt32(guid, 0));
            _data.BitShort(BitConverter.ToInt16(guid, 4));
            _data.BitShort(BitConverter.ToInt16(guid, 6));
            _data.Bytes(guid, 8, 8);
            _data.BitLong(0);

            if (solid)
            {
                H(3, _record.Of("AcDb3dSolid").Handle(350));
            }
        }
    }
}
