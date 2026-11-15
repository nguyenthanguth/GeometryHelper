using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <content>
    /// REGION, 3DSOLID and BODY. From R2013 the entity carries the GUID of its ACIS data, which the data store of the file holds
    /// (<see cref="DwgDataStoreWriter"/>); in R2004, R2007 and R2010 the entity holds the SAB itself.
    /// </content>
    internal sealed partial class DwgObjectWriter
    {
        private Dictionary<ulong, byte[]> _acis;

        /// <summary>
        /// Whether an ACIS entity is written. From R2013 it names the GUID of its data (group 2) and the section ACDSDATA holds
        /// that data under its handle. In R2004, R2007 and R2010 it is written where <see cref="DwgDrawing.AcisData"/> holds
        /// the SAB of its version (20800, 21200 or 21500, as <see cref="DwgDrawing.SatBodiesToSab"/> makes it of the SAT a DXF
        /// of those versions gives). The SAT text of R2000 is not written.
        /// </summary>
        private bool HasAcisData(DwgRecord record)
        {
            if (!R2004Plus)
            {
                return false;
            }

            if (_acis == null)
            {
                _acis = new Dictionary<ulong, byte[]>();

                foreach (KeyValuePair<ulong, byte[]> data in _drawing.AcisData)
                {
                    _acis[data.Key] = data.Value;
                }
            }

            if (!_acis.TryGetValue(record.Handle, out byte[] sab))
            {
                return false;
            }

            return R2013Plus
                ? Guid.TryParse(record.Of("AcDbModelerGeometry").Text(2).Trim(), out Guid _)
                : sab.Length > 19 && BitConverter.ToInt32(sab, 15) == SabVersion;
        }

        /// <summary>The version of the SAB a file of R2004 (20800), R2007 (21200) or R2010 (21500) holds.</summary>
        private int SabVersion =>
            _version >= 1024 ? 21500
            : _version >= 1021 ? 21200
            : 20800;

        /// <summary>
        /// A REGION, 3DSOLID or BODY of R2013 and later (<paramref name="solid"/> for a 3DSOLID): a wireframe of no wires
        /// nor silhouettes, as AutoCAD writes it, no materials of faces, whether it is modified (290), the GUID of its data
        /// in the store, and of a 3DSOLID the handle of its history.
        /// </summary>
        private void WriteModelerGeometry(bool solid)
        {
            if (!R2013Plus)
            {
                WriteModelerGeometryInFile(solid);
                return;
            }

            DwgGroups geometry = _record.Of("AcDbModelerGeometry");
            byte[] guid = Guid.Parse(geometry.Text(2).Trim()).ToByteArray();
            // The wireframe as AutoCAD writes it for a new solid: there, with no point, no wires and no silhouettes.
            WriteEmptyWireframe();
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

        /// <summary>
        /// A REGION, 3DSOLID or BODY of R2004, R2007 and R2010 (<paramref name="solid"/> for a 3DSOLID): the SAB in the entity
        /// as its bytes, after the flags that say it is there (not empty, and version 2); then the wireframe AutoCAD writes
        /// for a new solid, and from R2007 the count of materials of faces, 0, and of a 3DSOLID the handle of its history.
        /// </summary>
        private void WriteModelerGeometryInFile(bool solid)
        {
            _data.Bit(false);
            _data.Bit(false);
            _data.BitShort(2);
            _data.Bytes(_acis[_record.Handle]);
            WriteEmptyWireframe();

            if (R2007Plus)
            {
                _data.BitLong(0);
            }

            if (solid && R2007Plus)
            {
                H(3, _record.Of("AcDb3dSolid").Handle(350));
            }
        }

        /// <summary>The wireframe of a new solid: there, with no point, no wires and no silhouettes.</summary>
        private void WriteEmptyWireframe()
        {
            _data.Bit(true);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(false);
            _data.BitLong(0);
            _data.Bit(true);
        }
    }
}
