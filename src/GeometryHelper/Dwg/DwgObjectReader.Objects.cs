using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <content>The objects that are neither entities nor tables: dictionaries, records of free data, layouts and the like.</content>
    internal sealed partial class DwgObjectReader
    {
        private void ReadDictionary(bool withDefault)
        {
            int count = Count(_data.BitLong());
            int cloning = _data.BitShort();
            bool hardOwner = _data.RawChar() != 0;
            _object.Add(100, "AcDbDictionary");

            if (hardOwner)
            {
                _object.Add(280, 1);
            }

            _object.Add(281, cloning);

            for (int i = 0; i < count; i++)
            {
                _object.Add(3, T());
                _object.Add(hardOwner ? 360 : 350, H());
            }

            if (withDefault)
            {
                _object.Add(100, "AcDbDictionaryWithDefault");
                _object.Add(340, H());
            }
        }

        private void ReadDictionaryVariable()
        {
            int schema = _data.RawChar();
            _object.Add(100, "DictionaryVariables");
            _object.Add(280, schema);
            _object.Add(1, T());
        }

        private void ReadScale()
        {
            _object.Add(100, "AcDbScale");
            _object.Add(70, _data.BitShort());
            _object.Add(300, T());
            _object.Add(140, _data.BitDouble());
            _object.Add(141, _data.BitDouble());
            _object.Add(290, _data.Bit() ? 1 : 0);
        }

        private void ReadGroup()
        {
            _object.Add(100, "AcDbGroup");
            _object.Add(300, T());
            _object.Add(70, _data.BitShort());
            _object.Add(71, _data.BitShort());
            int count = Count(_data.BitLong());

            for (int i = 0; i < count; i++)
            {
                _object.Add(340, H());
            }
        }

        private void ReadMLineStyle()
        {
            _object.RecordName = T();
            byte[] description = T();
            int flags = _data.BitShort();
            DwgColour colour = ReadColour();
            double start = _data.BitDouble();
            double end = _data.BitDouble();
            int lines = _data.RawChar();
            int dxf = ((flags & 1) != 0 ? 2 : 0) | ((flags & 2) != 0 ? 1 : 0) | (flags & 16) | ((flags & 32) != 0 ? 64 : 0)
                | ((flags & 64) != 0 ? 32 : 0) | (flags & 256) | ((flags & 512) != 0 ? 1024 : 0) | ((flags & 1024) != 0 ? 512 : 0);
            _object.Add(100, "AcDbMlineStyle");
            _object.Add(2, _object.RecordName);
            _object.Add(70, dxf);
            _object.Add(3, description);
            AddColour(_object.Body, colour, false);
            _object.Add(51, Degrees(start));
            _object.Add(52, Degrees(end));
            _object.Add(71, lines);

            for (int i = 0; i < lines; i++)
            {
                _object.Add(49, _data.BitDouble());
                AddColour(_object.Body, ReadColour(), false);

                if (R2018Plus)
                {
                    // From R2018 an element names its linetype by handle; DXF writes ByLayer and ByBlock in capitals here.
                    _object.Add(6, new DwgName(H(), "BYLAYER") { Capitals = true });
                }
                else
                {
                    int linetype = _data.BitShort();
                    _object.Add(6, linetype == 32766 ? "BYBLOCK" : "BYLAYER");
                }
            }
        }

        /// <summary>
        /// The data of an XRECORD: groups one after another, each its code as a short and a value whose form the code
        /// decides, the way a binary DXF file writes them.
        /// </summary>
        private void ReadXRecord()
        {
            int size = Count(_data.BitLong());
            long end = _data.Position + (size * 8L);

            if (end > _data.End)
            {
                throw new FormatException("The data of the XRECORD run past its end.");
            }

            var values = new DwgBitReader(_file, _data.Position, end);
            _data.Position = end;
            int cloning = _data.Remaining >= 2 ? _data.BitShort() : 0;
            _object.Add(100, "AcDbXrecord");
            _object.Add(280, cloning);

            while (values.Remaining >= 16)
            {
                int code = values.RawShort();

                if ((code >= 310 && code <= 319) || code == 1004)
                {
                    _object.Add(code, Hex(values.Bytes(values.RawChar())));
                }
                else if ((code >= 0 && code <= 9) || (code >= 100 && code <= 105) || (code >= 300 && code <= 309) || (code >= 410 && code <= 419)
                    || (code >= 430 && code <= 439) || (code >= 470 && code <= 479) || (code >= 1000 && code <= 1003))
                {
                    if (R2007Plus)
                    {
                        _object.Add(code, values.TextUnicodeRaw());
                        continue;
                    }

                    int length = (ushort)values.RawShort();
                    values.RawChar();
                    _object.Add(code, values.Bytes(length));
                }
                else if (code >= 10 && code <= 39)
                {
                    _object.AddPoint(code, values.RawPoint3());
                }
                else if ((code >= 40 && code <= 59) || (code >= 110 && code <= 149) || (code >= 210 && code <= 239) || (code >= 460 && code <= 469)
                    || (code >= 1010 && code <= 1059))
                {
                    _object.Add(code, values.RawDouble());
                }
                else if ((code >= 60 && code <= 79) || (code >= 170 && code <= 179) || (code >= 270 && code <= 279) || (code >= 370 && code <= 389)
                    || (code >= 400 && code <= 409) || (code >= 1060 && code <= 1070))
                {
                    _object.Add(code, values.RawShort());
                }
                else if ((code >= 90 && code <= 99) || (code >= 420 && code <= 429) || (code >= 440 && code <= 459) || code == 1071)
                {
                    _object.Add(code, values.RawLong());
                }
                else if (code >= 160 && code <= 169)
                {
                    _object.Add(code, BitConverter.ToInt64(values.Bytes(8), 0));
                }
                else if (code >= 280 && code <= 299)
                {
                    _object.Add(code, values.RawChar());
                }
                else if ((code >= 320 && code <= 369) || (code >= 390 && code <= 399) || (code >= 1005 && code <= 1009))
                {
                    _object.Add(code, BitConverter.ToUInt64(values.Bytes(8), 0));
                }
                else
                {
                    throw new FormatException("The XRECORD has a group of the unknown code " + code.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }

        private void ReadLayout()
        {
            byte[] pageSetup = T();
            byte[] printer = T();
            int plotFlags = _data.BitShort();
            var margins = new double[4];

            for (int i = 0; i < 4; i++)
            {
                margins[i] = _data.BitDouble();
            }

            double paperWidth = _data.BitDouble();
            double paperHeight = _data.BitDouble();
            byte[] paperSize = T();
            double originX = _data.BitDouble();
            double originY = _data.BitDouble();
            int paperUnits = _data.BitShort();
            int rotation = _data.BitShort();
            int plotType = _data.BitShort();
            var window = new double[4];

            for (int i = 0; i < 4; i++)
            {
                window[i] = _data.BitDouble();
            }

            // Before R2004 the view to plot is named here; from R2004 its handle leads to it.
            object plotView = R2004Plus ? null : T();
            double numerator = _data.BitDouble();
            double denominator = _data.BitDouble();
            byte[] styleSheet = T();
            int scaleType = _data.BitShort();
            double scaleFactor = _data.BitDouble();
            double imageX = _data.BitDouble();
            double imageY = _data.BitDouble();
            int shadeMode = 0;
            int shadeResolution = 0;
            int shadeDpi = 0;

            if (R2004Plus)
            {
                shadeMode = _data.BitShort();
                shadeResolution = _data.BitShort();
                shadeDpi = _data.BitShort();
                plotView = new DwgName(H());
            }

            if (R2007Plus)
            {
                H();
            }

            _object.RecordName = T();
            int tabOrder = _data.BitLong();
            int flags = _data.BitShort();
            double[] ucsOrigin = _data.Point3();
            double[] limitsMin = _data.RawPoint2();
            double[] limitsMax = _data.RawPoint2();
            double[] insertionBase = _data.Point3();
            double[] xAxis = _data.Point3();
            double[] yAxis = _data.Point3();
            double elevation = _data.BitDouble();
            int ortho = _data.BitShort();
            double[] extentsMin = _data.Point3();
            double[] extentsMax = _data.Point3();
            int viewports = R2004Plus ? Count(_data.BitLong()) : 0;
            ulong block = H();
            ulong viewport = H();

            if (R2004Plus)
            {
                // The base and the named UCS, which DXF does not write here, then the viewports of the layout in their order.
                H();
                H();
                _object.Viewports = new List<ulong>(Math.Min(viewports, 1024));

                for (int i = 0; i < viewports; i++)
                {
                    _object.Viewports.Add(H());
                }

                // A layout no viewport was made current in keeps none as the last active; DXF names the first, the one
                // showing the paper.
                if (viewport == 0 && viewports > 0)
                {
                    viewport = _object.Viewports[0];
                }
            }
            _object.Add(100, "AcDbPlotSettings");
            _object.Add(1, pageSetup);
            _object.Add(2, printer);
            _object.Add(4, paperSize);
            _object.Add(6, plotView);

            for (int i = 0; i < 4; i++)
            {
                _object.Add(40 + i, margins[i]);
            }

            _object.Add(44, paperWidth);
            _object.Add(45, paperHeight);
            _object.Add(46, originX);
            _object.Add(47, originY);
            _object.Add(48, window[0]);
            _object.Add(49, window[1]);
            _object.Add(140, window[2]);
            _object.Add(141, window[3]);
            _object.Add(142, numerator);
            _object.Add(143, denominator);
            _object.Add(70, plotFlags);
            _object.Add(72, paperUnits);
            _object.Add(73, rotation);
            _object.Add(74, plotType);
            _object.Add(7, styleSheet);
            _object.Add(75, scaleType);
            _object.Add(147, scaleFactor);

            if (R2004Plus)
            {
                _object.Add(76, shadeMode);
                _object.Add(77, shadeResolution);
                _object.Add(78, shadeDpi);
            }

            _object.Add(148, imageX);
            _object.Add(149, imageY);
            _object.Add(100, "AcDbLayout");
            _object.Add(1, _object.RecordName);
            _object.Add(70, flags);
            _object.Add(71, tabOrder);
            _object.AddPoint(10, limitsMin);
            _object.AddPoint(11, limitsMax);
            _object.AddPoint(12, insertionBase);
            _object.AddPoint(14, extentsMin);
            _object.AddPoint(15, extentsMax);
            _object.Add(146, elevation);
            _object.AddPoint(13, ucsOrigin);
            _object.AddPoint(16, xAxis);
            _object.AddPoint(17, yAxis);
            _object.Add(76, ortho);
            _object.Add(330, block);

            if (viewport != 0)
            {
                _object.Add(331, viewport);
            }
        }
    }
}
