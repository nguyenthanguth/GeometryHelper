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

        /// <summary>An IMAGEDEF: the file of a raster image, its size in pixels, whether it is loaded, its units and the size of a pixel.</summary>
        private void ReadImageDefinition()
        {
            int version = _data.BitLong();
            double[] size = _data.RawPoint2();
            byte[] file = T();
            bool loaded = _data.Bit();
            int units = _data.RawChar();
            double[] pixel = _data.RawPoint2();
            _object.Add(100, "AcDbRasterImageDef");
            _object.Add(90, version);
            _object.Add(1, file);
            _object.AddPoint(10, size);
            _object.AddPoint(11, pixel);
            _object.Add(280, loaded ? 1 : 0);
            _object.Add(281, units);
        }

        /// <summary>An IMAGEDEF_REACTOR: its class version, then its image, its owner, which DXF writes again.</summary>
        private void ReadImageDefinitionReactor()
        {
            _object.Add(100, "AcDbRasterImageDefReactor");
            _object.Add(90, _data.BitLong());
            _object.Add(330, _object.Owner);
        }

        /// <summary>The RASTERVARIABLES of the drawing: its class version, whether frames show, the quality and the unit of images.</summary>
        private void ReadRasterVariables()
        {
            _object.Add(100, "AcDbRasterVariables");
            _object.Add(90, _data.BitLong());
            _object.Add(70, _data.BitShort());
            _object.Add(71, _data.BitShort());
            _object.Add(72, _data.BitShort());
        }

        /// <summary>The WIPEOUTVARIABLES of the drawing: whether the frames of wipeouts show.</summary>
        private void ReadWipeoutVariables()
        {
            _object.Add(100, "AcDbWipeoutVariables");
            _object.Add(70, _data.BitShort());
        }

        /// <summary>
        /// A DBCOLOR, a colour of a book: from R2004 a CMC colour with its name and its book, which DXF writes as 62, 420
        /// and 430; before R2004 an index, which DXF does not write.
        /// </summary>
        private void ReadBookColour()
        {
            _object.Add(100, "AcDbColor");

            if (!R2004Plus)
            {
                _data.BitShort();
                return;
            }

            _object.BookColour = ReadColour();
            AddColour(_object.Body, _object.BookColour, false);
        }

        /// <summary>
        /// A TABLESTYLE before R2010: its description, flow, margins and what it suppresses, then the styles of the data,
        /// title and header rows, each a text style, height, alignment, colours, from R2007 a data format, and six borders.
        /// False from R2010, whose styles are cell styles, not read yet.
        /// </summary>
        private bool ReadTableStyle()
        {
            if (R2010Plus)
            {
                ReadTableStyle2010();
                return true;
            }

            _object.Add(100, "AcDbTableStyle");
            _object.Add(3, T());
            _object.Add(70, _data.BitShort());
            _object.Add(71, _data.BitShort());
            _object.Add(40, _data.BitDouble());
            _object.Add(41, _data.BitDouble());
            _object.Add(280, _data.Bit() ? 1 : 0);
            _object.Add(281, _data.Bit() ? 1 : 0);

            for (int row = 0; row < 3; row++)
            {
                ulong textStyle = H();

                if (textStyle != 0)
                {
                    _object.Add(7, new DwgName(textStyle, "Standard"));
                }

                _object.Add(140, _data.BitDouble());
                _object.Add(170, _data.BitShort());
                _object.Add(62, ReadColour(true).Index);
                _object.Add(63, ReadColour(true).Index);
                _object.Add(283, _data.Bit() ? 1 : 0);
                var borders = new List<DwgGroup>();

                for (int i = 0; i < 6; i++)
                {
                    borders.Add(new DwgGroup(274 + i, _data.BitShort()));
                    borders.Add(new DwgGroup(284 + i, _data.Bit() ? 1 : 0));
                    borders.Add(new DwgGroup(64 + i, ReadColour(true).Index));
                }

                if (R2007Plus)
                {
                    _object.Add(90, _data.BitLong());
                    _object.Add(91, _data.BitLong());
                    _object.Add(1, T());
                }

                _object.Body.AddRange(borders);
            }

            return true;
        }

        /// <summary>
        /// A MATERIAL: its name and description, its ambient, diffuse and specular colours, each a way (0 the object's own,
        /// 1 its own colour), a factor and the colour itself where its own, then a map of each kind, diffuse, specular,
        /// reflection, opacity, bump and refraction, among them the gloss, the opacity and the index of refraction, and from
        /// R2007 the translucence, self-illumination, reflectivity, illumination model, channels and mode. DXF writes what
        /// differs from a new material's values, as AutoCAD's DXF of it does; the settings of R2010 on are in the records
        /// of its extension dictionary.
        /// </summary>
        private void ReadMaterial()
        {
            _object.Add(100, "AcDbMaterial");
            _object.Add(1, T());
            byte[] description = T();

            if (description.Length > 0)
            {
                _object.Add(2, description);
            }

            ReadMaterialColour(70, 40, 90);
            ReadMaterialColour(71, 41, 91);
            ReadMaterialMap(42, 72, 3, 73, 74, 75, 43);
            int specularWay = _data.RawChar();
            double specularFactor = _data.BitDouble();
            int specular = specularWay == 1 ? _data.BitLong() : 0;
            var specularMap = new List<DwgGroup>();
            List<DwgGroup> body = _object.Body;
            _object.Body = specularMap;
            ReadMaterialMap(46, 77, 4, 78, 79, 170, 47);
            _object.Body = body;
            AddIfNot(44, _data.BitDouble(), 0.5);
            AddIfNot(76, specularWay, 0);
            AddIfNot(45, specularFactor, 1.0);

            if (specularWay == 1)
            {
                _object.Add(92, specular);
            }

            _object.Body.AddRange(specularMap);
            ReadMaterialMap(48, 171, 6, 172, 173, 174, 49);
            double opacity = _data.BitDouble();
            AddIfNot(140, opacity, 1.0);
            ReadMaterialMap(141, 175, 7, 176, 177, 178, 142);
            ReadMaterialMap(143, 179, 8, 270, 271, 272, 144);
            AddIfNot(145, _data.BitDouble(), 1.0);
            ReadMaterialMap(146, 273, 9, 274, 275, 276, 147);

            if (R2007Plus)
            {
                AddIfNot(148, _data.BitDouble(), 0.0);
                AddIfNot(149, _data.BitDouble(), 0.0);
                AddIfNot(468, _data.BitDouble(), 0.0);
                AddIfNot(93, _data.BitLong(), 0);
                AddIfNot(94, _data.BitLong(), 127);
                AddIfNot(282, _data.BitLong(), 0);
            }
        }

        /// <summary>A colour of a material: its way (<paramref name="way"/>), its factor, and its colour where it is its own.</summary>
        private void ReadMaterialColour(int way, int factor, int colour)
        {
            int method = _data.RawChar();
            AddIfNot(way, method, 0);
            AddIfNot(factor, _data.BitDouble(), 1.0);

            if (method == 1)
            {
                _object.Add(colour, _data.BitLong());
            }
        }

        /// <summary>
        /// A map of a material: its blend factor, its projection, tiling and automatic transform, its matrix of 16, its
        /// source (0 the scene, 1 a file) and the file, written in the order DXF gives them.
        /// </summary>
        private void ReadMaterialMap(
            int blend,
            int source,
            int file,
            int projection,
            int tiling,
            int transform,
            int matrix
        )
        {
            AddIfNot(blend, _data.BitDouble(), 1.0);
            int way = _data.RawChar();
            int tile = _data.RawChar();
            int auto = _data.RawChar();
            var values = new double[16];

            for (int i = 0; i < 16; i++)
            {
                values[i] = _data.BitDouble();
            }

            int from = _data.RawChar();
            // AutoCAD leaves out the source of a refraction map where it is the scene, of the others where it is a file.
            AddIfNot(source, from, source == 273 ? 0 : 1);

            if (from == 1)
            {
                byte[] name = T();

                if (name.Length > 0)
                {
                    _object.Add(file, name);
                }
            }

            AddIfNot(projection, way, 2);
            AddIfNot(tiling, tile, 1);
            AddIfNot(transform, auto, 1);
            bool identity = true;

            for (int i = 0; i < 16 && identity; i++)
            {
                identity = values[i] == (i % 5 == 0 ? 1.0 : 0.0);
            }

            if (!identity)
            {
                foreach (double value in values)
                {
                    _object.Add(matrix, value);
                }
            }
        }

        /// <summary>
        /// The kinds of the 58 properties of a visual style of R2013 and later, in their order: 9 a whole number (DXF 90),
        /// 4 a real (40), 6 a colour (62 and 420), 2 a yes or no (290), 1 a text (1).
        /// </summary>
        private const string VisualStyleKinds = "9999446996699496499969992949222222222944969962999296491244";

        /// <summary>
        /// A VISUALSTYLE of R2013 and later: its name, type and version, whether it is for AutoCAD's own use, then each of
        /// its 58 properties, a value and how it is applied (176: 0 taken from the style it derives from, 1 set), which DXF
        /// gives after their count (70 58).
        /// </summary>
        private void ReadVisualStyle()
        {
            _object.Add(100, "AcDbVisualStyle");
            _object.Add(2, T());

            if (!R2010Plus)
            {
                ReadVisualStyle2007();
                return;
            }

            _object.Add(70, _data.BitLong());
            int version = _data.BitShort();
            _object.Add(177, R2013Plus ? version + 1 : version);
            _object.Add(291, _data.Bit() ? 1 : 0);

            if (R2013Plus)
            {
                _object.Add(70, VisualStyleKinds.Length);
            }

            int count = R2013Plus ? VisualStyleKinds.Length : VisualStyleCodes2010.Length;

            for (int i = 0; i < count; i++)
            {
                int code = R2013Plus ? 0 : VisualStyleCodes2010[i];

                switch (VisualStyleKinds[i])
                {
                    case '9':
                        _object.Add(R2013Plus ? 90 : code, _data.BitLong());
                        break;
                    case '4':
                        _object.Add(R2013Plus ? 40 : code, _data.BitDouble());
                        break;
                    case '6':
                        AddColour(
                            _object.Body,
                            ReadStyleColour(),
                            false,
                            R2013Plus ? 62 : code,
                            R2013Plus ? 420 : code + 358
                        );
                        break;
                    case '2':
                        _object.Add(R2013Plus ? 290 : code, _data.Bit() ? 1 : 0);
                        break;
                    default:
                        _object.Add(1, T());
                        break;
                }

                _object.Add(176, _data.BitShort());
            }
        }

        /// <summary>The DXF groups of the 28 properties of a visual style of R2010, the first 28 of the later 58.</summary>
        private static readonly int[] VisualStyleCodes2010 =
        {
            71,
            72,
            73,
            90,
            40,
            41,
            63,
            74,
            91,
            64,
            65,
            75,
            175,
            42,
            92,
            66,
            43,
            76,
            77,
            78,
            67,
            79,
            170,
            171,
            290,
            93,
            44,
            173,
        };

        /// <summary>
        /// A VISUALSTYLE of R2004 and R2007, each property in a place of its own, put into the order of DXF: the face's
        /// lighting, its colour (62 always 5, which the file does not hold) and its mono colour, the edges, the display.
        /// </summary>
        private void ReadVisualStyle2007()
        {
            _object.Add(70, _data.BitLong());
            _object.Add(71, _data.BitLong());
            _object.Add(72, _data.BitLong());
            _object.Add(73, _data.BitLong());
            double opacity = _data.BitDouble();
            double specular = _data.BitDouble();
            DwgColour mono = ReadStyleColour();
            _object.Add(90, _data.BitLong());
            _object.Add(40, opacity);
            _object.Add(41, specular);
            _object.Add(62, 5);
            AddColour(_object.Body, mono, false, 63, 421);
            _object.Add(74, _data.BitLong());
            _object.Add(91, _data.BitLong());
            AddColour(_object.Body, ReadStyleColour(), false, 64, 422);
            AddColour(_object.Body, ReadStyleColour(), false, 65, 423);
            _object.Add(75, _data.BitLong());
            int at = _object.Body.Count;
            _object.Add(42, _data.BitDouble());
            _object.Add(92, _data.BitLong());
            AddColour(_object.Body, ReadStyleColour(), false, 66, 424);
            _object.Add(43, _data.BitDouble());
            _object.Add(76, _data.BitLong());
            _object.Add(77, _data.BitLong());
            _object.Add(78, _data.BitLong());
            AddColour(_object.Body, ReadStyleColour(), false, 67, 425);
            _object.Add(79, _data.BitLong());
            _object.Add(170, _data.RawChar());
            _object.Add(171, _data.BitLong());
            _object.Add(290, _data.Bit() ? 1 : 0);
            _object.Add(174, _data.BitLong());
            _object.Body.Insert(at, new DwgGroup(175, _data.BitLong()));
            _object.Add(93, _data.BitLong());
            _object.Add(44, (double)_data.BitLong());
            _object.Add(173, _data.BitLong());
            double last = R2007Plus ? _data.BitDouble() : 0.0;
            _object.Add(291, _data.Bit() ? 1 : 0);
            _object.Add(45, last);
        }

        /// <summary>A colour of a visual style: a CMC colour, no colour (0xC8) given as 257.</summary>
        private DwgColour ReadStyleColour()
        {
            DwgColour colour = ReadColour();

            if (((colour.Raw >> 24) & 0xFF) == 0xC8)
            {
                colour.Index = 257;
            }

            return colour;
        }

        private void AddIfNot(int code, double value, double usual)
        {
            if (value != usual)
            {
                _object.Add(code, value);
            }
        }

        private void AddIfNot(int code, int value, int usual)
        {
            if (value != usual)
            {
                _object.Add(code, value);
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
            int dxf =
                ((flags & 1) != 0 ? 2 : 0)
                | ((flags & 2) != 0 ? 1 : 0)
                | (flags & 16)
                | ((flags & 32) != 0 ? 64 : 0)
                | ((flags & 64) != 0 ? 32 : 0)
                | (flags & 256)
                | ((flags & 512) != 0 ? 1024 : 0)
                | ((flags & 1024) != 0 ? 512 : 0);
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
                    // Before R2018 the linetype is its place among the linetypes of the table, 32767 ByLayer, 32766 ByBlock.
                    int linetype = _data.BitShort();

                    if (linetype == 32766 || linetype == 32767)
                    {
                        _object.Add(6, linetype == 32766 ? "BYBLOCK" : "BYLAYER");
                    }
                    else
                    {
                        _object.Add(6, new DwgLinetypeIndex(linetype));
                    }
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
                else if (
                    (code >= 0 && code <= 9)
                    || (code >= 100 && code <= 105)
                    || (code >= 300 && code <= 309)
                    || (code >= 410 && code <= 419)
                    || (code >= 430 && code <= 439)
                    || (code >= 470 && code <= 479)
                    || (code >= 1000 && code <= 1003)
                )
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
                else if (
                    (code >= 40 && code <= 59)
                    || (code >= 110 && code <= 149)
                    || (code >= 210 && code <= 239)
                    || (code >= 460 && code <= 469)
                    || (code >= 1010 && code <= 1059)
                )
                {
                    _object.Add(code, values.RawDouble());
                }
                else if (
                    (code >= 60 && code <= 79)
                    || (code >= 170 && code <= 179)
                    || (code >= 270 && code <= 279)
                    || (code >= 370 && code <= 389)
                    || (code >= 400 && code <= 409)
                    || (code >= 1060 && code <= 1070)
                )
                {
                    _object.Add(code, values.RawShort());
                }
                else if (
                    (code >= 90 && code <= 99)
                    || (code >= 420 && code <= 429)
                    || (code >= 440 && code <= 459)
                    || code == 1071
                )
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
                    throw new FormatException(
                        "The XRECORD has a group of the unknown code "
                            + code.ToString(CultureInfo.InvariantCulture)
                            + "."
                    );
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
