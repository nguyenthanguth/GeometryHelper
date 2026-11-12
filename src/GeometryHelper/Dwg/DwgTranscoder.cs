using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Turns a DWG file into the DXF file that says the same: the header variables, the classes, the tables, the blocks,
    /// the entities and the objects, each object as the record DXF has for it, in the order AutoCAD writes them. The DXF
    /// reader then makes the document, so a drawing read from a DWG is the drawing read from its DXF.
    /// </summary>
    internal sealed class DwgTranscoder
    {
        /// <summary>
        /// The header variables in the order DXF writes them, each with its first group code and, where it came later
        /// than R2000, the first version whose DXF has it. <c>$TDCREATE</c> and <c>$TDUPDATE</c> are not in a DWG and are
        /// not written; <c>$ACADMAINTVER</c> is group 90 from R2018.
        /// </summary>
        private const string HeaderOrder =
            "ACADVER 1;ACADMAINTVER 70;DWGCODEPAGE 3;REQUIREDVERSIONS 160 1027;INSBASE 10;EXTMIN 10;EXTMAX 10;"
            + "LIMMIN 10;LIMMAX 10;ORTHOMODE 70;REGENMODE 70;FILLMODE 70;QTEXTMODE 70;MIRRTEXT 70;LTSCALE 40;ATTMODE 70;"
            + "TEXTSIZE 40;TRACEWID 40;TEXTSTYLE 7;CLAYER 8;CELTYPE 6;CECOLOR 62;CELTSCALE 40;DISPSILH 70;DIMSCALE 40;"
            + "DIMASZ 40;DIMEXO 40;DIMDLI 40;DIMRND 40;DIMDLE 40;DIMEXE 40;DIMTP 40;DIMTM 40;DIMTXT 40;DIMCEN 40;"
            + "DIMTSZ 40;DIMTOL 70;DIMLIM 70;DIMTIH 70;DIMTOH 70;DIMSE1 70;DIMSE2 70;DIMTAD 70;DIMZIN 70;DIMBLK 1;"
            + "DIMASO 70;DIMSHO 70;DIMPOST 1;DIMAPOST 1;DIMALT 70;DIMALTD 70;DIMALTF 40;DIMLFAC 40;DIMTOFL 70;DIMTVP 40;"
            + "DIMTIX 70;DIMSOXD 70;DIMSAH 70;DIMBLK1 1;DIMBLK2 1;DIMSTYLE 2;DIMCLRD 70;DIMCLRE 70;DIMCLRT 70;DIMTFAC 40;"
            + "DIMGAP 40;DIMJUST 70;DIMSD1 70;DIMSD2 70;DIMTOLJ 70;DIMTZIN 70;DIMALTZ 70;DIMALTTZ 70;DIMUPT 70;DIMDEC 70;"
            + "DIMTDEC 70;DIMALTU 70;DIMALTTD 70;DIMTXSTY 7;DIMAUNIT 70;DIMADEC 70;DIMALTRND 40;DIMAZIN 70;DIMDSEP 70;"
            + "DIMATFIT 70;DIMFRAC 70;DIMLDRBLK 1;DIMLUNIT 70;DIMLWD 70;DIMLWE 70;DIMTMOVE 70;DIMFXL 40 1021;"
            + "DIMFXLON 70 1021;DIMJOGANG 40 1021;DIMTFILL 70 1021;DIMTFILLCLR 70 1021;DIMARCSYM 70 1021;DIMLTYPE 6 1021;"
            + "DIMLTEX1 6 1021;DIMLTEX2 6 1021;DIMTXTDIRECTION 70 1024;LUNITS 70;LUPREC 70;SKETCHINC 40;FILLETRAD 40;"
            + "AUNITS 70;AUPREC 70;MENU 1;ELEVATION 40;PELEVATION 40;THICKNESS 40;LIMCHECK 70;CHAMFERA 40;CHAMFERB 40;"
            + "CHAMFERC 40;CHAMFERD 40;SKPOLY 70;TDUCREATE 40;TDUUPDATE 40;TDINDWG 40;TDUSRTIMER 40;USRTIMER 70;"
            + "ANGBASE 50;ANGDIR 70;PDMODE 70;PDSIZE 40;PLINEWID 40;SPLFRAME 70;SPLINETYPE 70;SPLINESEGS 70;HANDSEED 5;"
            + "SURFTAB1 70;SURFTAB2 70;SURFTYPE 70;SURFU 70;SURFV 70;UCSBASE 2;UCSNAME 2;UCSORG 10;UCSXDIR 10;UCSYDIR 10;"
            + "UCSORTHOREF 2;UCSORTHOVIEW 70;UCSORGTOP 10;UCSORGBOTTOM 10;UCSORGLEFT 10;UCSORGRIGHT 10;UCSORGFRONT 10;"
            + "UCSORGBACK 10;PUCSBASE 2;PUCSNAME 2;PUCSORG 10;PUCSXDIR 10;PUCSYDIR 10;PUCSORTHOREF 2;PUCSORTHOVIEW 70;"
            + "PUCSORGTOP 10;PUCSORGBOTTOM 10;PUCSORGLEFT 10;PUCSORGRIGHT 10;PUCSORGFRONT 10;PUCSORGBACK 10;USERI1 70;"
            + "USERI2 70;USERI3 70;USERI4 70;USERI5 70;USERR1 40;USERR2 40;USERR3 40;USERR4 40;USERR5 40;WORLDVIEW 70;"
            + "SHADEDGE 70;SHADEDIF 70;TILEMODE 70;MAXACTVP 70;PINSBASE 10;PLIMCHECK 70;PEXTMIN 10;PEXTMAX 10;PLIMMIN 10;"
            + "PLIMMAX 10;UNITMODE 70;VISRETAIN 70;PLINEGEN 70;PSLTSCALE 70;TREEDEPTH 70;CMLSTYLE 2;CMLJUST 70;"
            + "CMLSCALE 40;PROXYGRAPHICS 70;MEASUREMENT 70;CELWEIGHT 370;ENDCAPS 280;JOINSTYLE 280;LWDISPLAY 290;"
            + "INSUNITS 70;HYPERLINKBASE 1;STYLESHEET 1;XEDIT 290;CEPSNTYPE 380;PSTYLEMODE 290;FINGERPRINTGUID 2;"
            + "VERSIONGUID 2;EXTNAMES 290;PSVPSCALE 40;OLESTARTUP 290;SORTENTS 280 1018;INDEXCTL 280 1018;"
            + "HIDETEXT 280 1018;XCLIPFRAME 280 1018;HALOGAP 280 1018;OBSCOLOR 70 1018;OBSLTYPE 280 1018;"
            + "INTERSECTIONDISPLAY 280 1018;INTERSECTIONCOLOR 70 1018;DIMASSOC 280 1018;PROJECTNAME 1 1018;"
            + "CAMERADISPLAY 290 1021;LENSLENGTH 40 1021;CAMERAHEIGHT 40 1021;STEPSPERSEC 40 1021;STEPSIZE 40 1021;"
            + "3DDWFPREC 40 1021;PSOLWIDTH 40 1021;PSOLHEIGHT 40 1021;LOFTANG1 40 1021;LOFTANG2 40 1021;LOFTMAG1 40 1021;"
            + "LOFTMAG2 40 1021;LOFTPARAM 70 1021;LOFTNORMALS 280 1021;LATITUDE 40 1021;LONGITUDE 40 1021;"
            + "NORTHDIRECTION 40 1021;TIMEZONE 70 1021;LIGHTGLYPHDISPLAY 280 1021;TILEMODELIGHTSYNCH 280 1021;"
            + "CMATERIAL 347 1021;SOLIDHIST 280 1021;SHOWHIST 280 1021;DWFFRAME 280 1021;DGNFRAME 280 1021;"
            + "REALWORLDSCALE 290 1021;INTERFERECOLOR 62 1021;INTERFEREOBJVS 345 1021;INTERFEREVPVS 346 1021;"
            + "CSHADOW 280 1021;SHADOWPLANELOCATION 40 1021";

        /// <summary>The tables in the order DXF writes them, each with the header's name for the handle of its control object.</summary>
        private static readonly string[][] Tables =
        {
            new[] { "VPORT", "_VPORTS" },
            new[] { "LTYPE", "_LTYPES" },
            new[] { "LAYER", "_LAYERS" },
            new[] { "STYLE", "_STYLES" },
            new[] { "VIEW", "_VIEWS" },
            new[] { "UCS", "_UCSS" },
            new[] { "APPID", "_APPIDS" },
            new[] { "DIMSTYLE", "_DIMSTYLES" },
            new[] { "BLOCK_RECORD", "_BLOCKS" },
        };

        private readonly DwgContainer _container;
        private readonly DwgHeaderVariables _header;
        private readonly List<DwgClass> _classes;
        private readonly List<DwgObject> _objects = new List<DwgObject>();
        private readonly Dictionary<ulong, DwgObject> _byHandle = new Dictionary<ulong, DwgObject>();
        private readonly HashSet<ulong> _written = new HashSet<ulong>();
        private readonly Dictionary<ulong, int[]> _viewportNumbers = new Dictionary<ulong, int[]>();
        private readonly DwgDxfWriter _dxf = new DwgDxfWriter();
        private readonly ProgressSteps _progress;
        private readonly bool _doubleByte;
        private readonly int _version;

        private DwgTranscoder(DwgContainer container, ProgressSteps progress)
        {
            _container = container;
            _progress = progress;
            _version = DwgContainer.NumberOf(_container.Version);
            _header = DwgHeaderVariables.Read(_container);
            _classes = DwgClass.ReadAll(_container.Classes, _container.Version);

            foreach (DwgClass one in _classes)
            {
                // AutoCAD 2021 names the class of a table's data DATATABLE in a DWG of R2000 and ACDBDATATABLE in its DXF.
                if (_version < 1018 && Encoding.ASCII.GetString(one.DxfName) == "DATATABLE")
                {
                    one.DxfName = Encoding.ASCII.GetBytes("ACDBDATATABLE");
                }
            }
            string page = _container.CodePage;

            _doubleByte = page == "ANSI_932" || page == "ANSI_936" || page == "ANSI_949" || page == "ANSI_950";
            // The offsets by handle, made the first time a table asks for its style, so that a drawing of no table pays nothing.
            Dictionary<ulong, long> offsets = null;
            var reader = new DwgObjectReader(_container.Objects, _classes, _container.Version)
            {
                OffsetOf = handle =>
                {
                    if (offsets == null)
                    {
                        offsets = new Dictionary<ulong, long>();

                        foreach (KeyValuePair<ulong, long> entry in _container.ObjectMap)
                        {
                            offsets[entry.Key] = entry.Value;
                        }
                    }

                    return offsets.TryGetValue(handle, out long at) ? at : (long?)null;
                },
            };
            int broken = 0;
            string firstFault = null;
            int read = 0;
            progress?.Stage(0.05, 0.6);

            foreach (KeyValuePair<ulong, long> entry in _container.ObjectMap)
            {
                progress?.Report(read++, _container.ObjectMap.Count);

                try
                {
                    DwgObject one = reader.Read(entry.Key, entry.Value);
                    _objects.Add(one);
                    _byHandle[one.Handle] = one;
                }
                catch (Exception fault)
                    when (fault is FormatException
                        || fault is InvalidOperationException
                        || fault is ArgumentException
                        || fault is ArithmeticException
                        || fault is IndexOutOfRangeException
                    )
                {
                    // A decoder refuses broken bytes with a FormatException; the geometry it hands decoded values to may
                    // refuse values no drawing has (a spline whose plane cannot be worked out) in its own way. Either is
                    // one broken object.
                    broken++;
                    firstFault = firstFault
                        ?? "the object " + entry.Key.ToString("X", CultureInfo.InvariantCulture) + ": " + fault.Message;
                }
            }

            Broken = broken;
            NumberPaperSpaces();
            NumberViewports();
            FillLeaderBoxes();
            AddCellStyleMaps();

            if (broken > 0)
            {
                GeometryHelperLog.Warn(
                    "DwgReader: "
                        + broken.ToString(CultureInfo.InvariantCulture)
                        + " broken objects were left out; the first is "
                        + firstFault
                );
            }
        }

        /// <summary>The number of objects left out because they were broken.</summary>
        private int Broken { get; }

        /// <summary>
        /// The DXF of the DWG in <paramref name="file"/>, as bytes in the drawing's code page. Throws
        /// <see cref="FormatException"/> for a file that is not a DWG or is broken where nothing can be read past it, and
        /// <see cref="NotSupportedException"/> for a DWG of a version that is not read.
        /// </summary>
        public static byte[] ToDxf(byte[] file) => ToDxf(file, null);

        /// <summary>
        /// The DXF of the DWG in <paramref name="file"/>, as <see cref="ToDxf(byte[])"/>, how far it has got told to
        /// <paramref name="progress"/> (null for none): its parts unpacked to 0.05, its objects read to 0.6, and the DXF of
        /// them written to 1.
        /// </summary>
        public static byte[] ToDxf(byte[] file, ProgressSteps progress)
        {
            progress?.Stage(0.0, 0.05);
            return ToDxf(DwgContainer.Read(file), progress);
        }

        /// <summary>The DXF of the DWG whose parts <paramref name="container"/> holds, as <see cref="ToDxf(byte[])"/>.</summary>
        public static byte[] ToDxf(DwgContainer container) => ToDxf(container, null);

        private static byte[] ToDxf(DwgContainer container, ProgressSteps progress)
        {
            var transcoder = new DwgTranscoder(container, progress);
            progress?.Stage(0.6, 1.0);
            transcoder.WriteHeader();
            transcoder.WriteClasses();
            transcoder.WriteTables();
            transcoder.WriteBlocks();
            transcoder.WriteObjects();
            transcoder.WriteDataStore();
            transcoder._dxf.Write(0, "EOF");
            transcoder.LogSummary();
            progress?.Report(1.0);
            return transcoder._dxf.ToArray();
        }

        /// <summary>
        /// Names the blocks a DWG keeps without their number the way DXF does. A DWG calls the block of every layout
        /// <c>*Paper_Space</c>; DXF keeps that name for the layout that is current and numbers the others from
        /// <c>*Paper_Space0</c> in the order of the block table. An anonymous block is kept as <c>*D</c> or <c>*U</c>, and
        /// DXF numbers them from 1 in the same order: <c>*D1</c>, <c>*D2</c>.
        /// </summary>
        private void NumberPaperSpaces()
        {
            DwgObject control = Find(_header.Handle("_BLOCKS"));
            ulong active = _header.Handle("_PAPERSPACE");
            int number = 0;
            int anonymous = 0;
            var seen = new HashSet<ulong>();

            if (control == null)
            {
                return;
            }

            foreach (ulong handle in control.Entries)
            {
                DwgObject block = Find(handle);

                if (block == null || block.BlockBody == null || block.RecordName == null || !seen.Add(handle))
                {
                    continue;
                }

                byte[] old = block.RecordName;
                byte[] name;

                if (Same(old, "*Paper_Space"))
                {
                    if (handle == active)
                    {
                        continue;
                    }

                    name = Encoding.ASCII.GetBytes("*Paper_Space" + number.ToString(CultureInfo.InvariantCulture));
                    number++;
                }
                else if ((block.BlockFlags & 1) != 0 && old.Length == 2 && old[0] == (byte)'*')
                {
                    anonymous++;
                    name = Encoding.ASCII.GetBytes(
                        "*" + (char)old[1] + anonymous.ToString(CultureInfo.InvariantCulture)
                    );
                }
                else
                {
                    continue;
                }

                block.RecordName = name;
                Rename(block.Body, old, name);
                Rename(block.BlockBody, old, name);
            }
        }

        /// <summary>
        /// Gives the viewports of the current layout the number (group 69) and status (68) AutoCAD writes, which a DWG does
        /// not hold. From R2004 a layout lists its viewports, the one showing the paper first, and they are numbered from
        /// 1 in that order. An R2000 layout lists none: the viewport that shows the paper itself (its view centre and height
        /// its own) is number 1 and the others follow in the order of their handles. The status is 0 for a viewport that is
        /// off, else its number from R2004 and -1 in R2000. The viewports of the other layouts are 0 and 0.
        /// </summary>
        private void NumberViewports()
        {
            ulong active = _header.Handle("_PAPERSPACE");
            var order = new List<DwgObject>();

            foreach (DwgObject layout in _objects)
            {
                if (layout.Name == "LAYOUT" && layout.Viewports != null && Owned330(layout) == active)
                {
                    foreach (ulong handle in layout.Viewports)
                    {
                        if (Find(handle) is DwgObject viewport && viewport.Type == 0x22 && !order.Contains(viewport))
                        {
                            order.Add(viewport);
                        }
                    }
                }
            }

            if (_version < 1018)
            {
                foreach (DwgObject one in _objects)
                {
                    if (
                        one.Type == 0x22
                        && one.Decoded
                        && (one.EntityMode == 1 || (one.EntityMode == 0 && one.Owner == active))
                    )
                    {
                        order.Add(one);
                    }
                }

                int paper = order.FindIndex(ShowsThePaper);

                if (paper > 0)
                {
                    DwgObject first = order[paper];
                    order.RemoveAt(paper);
                    order.Insert(0, first);
                }
            }

            for (int i = 0; i < order.Count; i++)
            {
                bool off = (Int(order[i], 90) & 0x20000) != 0;
                _viewportNumbers[order[i].Handle] = new[]
                {
                    i + 1,
                    off ? 0
                    : _version < 1018 ? -1
                    : i + 1,
                };
            }
        }

        /// <summary>
        /// A table style of cell styles of its own keeps them in DXF in a CELLSTYLEMAP under its extension dictionary
        /// (<c>ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP</c>), which a DWG does not hold: the two are added, with the
        /// next handles of the drawing, after the objects read, where the style has no extension dictionary.
        /// </summary>
        private void AddCellStyleMaps()
        {
            var added = new List<DwgObject>();
            ulong next = _header.Handle("HANDSEED");

            foreach (DwgObject style in _objects)
            {
                if (style.CellStyleMap == null)
                {
                    continue;
                }

                if (style.XDictionary != 0)
                {
                    // A DWG AutoCAD made of a DXF may keep the dictionary, empty or naming a CELLSTYLEMAP it does not hold:
                    // the map read of the style takes that one's place, or is put into the dictionary.
                    DwgObject held = Find(style.XDictionary);

                    if (!TakePlaceOfMap(style) && held != null && held.Decoded && held.Name == "DICTIONARY")
                    {
                        DwgObject into = style.CellStyleMap;
                        into.Handle = next++;
                        into.Owner = held.Handle;
                        into.Reactors.Add(held.Handle);
                        held.Add(3, "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP");
                        held.Add(360, into.Handle);
                        added.Add(into);
                    }

                    continue;
                }

                var dictionary = new DwgObject
                {
                    Name = "DICTIONARY",
                    Decoded = true,
                    Handle = next++,
                    Owner = style.Handle,
                };
                DwgObject map = style.CellStyleMap;
                map.Handle = next++;
                map.Owner = dictionary.Handle;
                map.Reactors.Add(dictionary.Handle);
                dictionary.Reactors.Add(style.Handle);
                dictionary.Add(100, "AcDbDictionary");
                dictionary.Add(280, 1);
                dictionary.Add(281, 1);
                dictionary.Add(3, "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP");
                dictionary.Add(360, map.Handle);
                style.XDictionary = dictionary.Handle;
                added.Add(dictionary);
                added.Add(map);
            }

            foreach (DwgObject one in added)
            {
                _objects.Add(one);
                _byHandle[one.Handle] = one;
            }

            if (added.Count > 0)
            {
                _header.Set("HANDSEED", next);
            }
        }

        /// <summary>
        /// Puts the CELLSTYLEMAP read of a table style in place of the one its extension dictionary names, of the same
        /// handle, owner and reactors, where that one is not read or not in the file (AutoCAD's DWG of a DXF names one it
        /// does not hold); false where the dictionary names none.
        /// </summary>
        private bool TakePlaceOfMap(DwgObject style)
        {
            DwgObject dictionary = Find(style.XDictionary);
            List<DwgGroup> entries = dictionary?.Body ?? new List<DwgGroup>();

            for (int i = 0; i + 1 < entries.Count; i++)
            {
                if (
                    entries[i].Code != 3
                    || !(entries[i].Value is byte[] name)
                    || Encoding.ASCII.GetString(name) != "ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP"
                    || !(entries[i + 1].Value is ulong handle)
                )
                {
                    continue;
                }

                DwgObject held = Find(handle);
                DwgObject map = style.CellStyleMap;

                if (held != null && held.Decoded)
                {
                    return true;
                }

                map.Handle = handle;
                map.Owner = held?.Owner ?? dictionary.Handle;
                map.Reactors.AddRange(held?.Reactors ?? new List<ulong> { dictionary.Handle });

                if (held != null)
                {
                    _objects[_objects.IndexOf(held)] = map;
                }
                else
                {
                    _objects.Add(map);
                }

                _byHandle[map.Handle] = map;
                return true;
            }

            return false;
        }

        /// <summary>
        /// From R2010 a LEADER keeps no box of its text, and AutoCAD writes the extents of the MTEXT it annotates (groups 40
        /// and 41), which the reader left as NaN; they are 0 where the annotation is no MTEXT.
        /// </summary>
        private void FillLeaderBoxes()
        {
            foreach (DwgObject leader in _objects)
            {
                if (leader.Type != 0x2D)
                {
                    continue;
                }

                double[] extents = Find(leader.Annotation)?.TextExtents ?? new[] { 0.0, 0.0 };

                for (int i = 0; i < leader.Body.Count; i++)
                {
                    DwgGroup group = leader.Body[i];

                    if ((group.Code == 40 || group.Code == 41) && group.Value is double value && double.IsNaN(value))
                    {
                        leader.Body[i] = new DwgGroup(group.Code, extents[group.Code - 40]);
                    }
                }
            }
        }

        /// <summary>Whether a viewport shows the paper itself: its view centre is its centre and its view height its height.</summary>
        private static bool ShowsThePaper(DwgObject viewport)
        {
            return Number(viewport, 12) == Number(viewport, 10)
                && Number(viewport, 22) == Number(viewport, 20)
                && Number(viewport, 45) == Number(viewport, 41);
        }

        /// <summary>The handle a LAYOUT gives its block under group 330.</summary>
        private static ulong Owned330(DwgObject layout)
        {
            foreach (DwgGroup group in layout.Body)
            {
                if (group.Code == 330 && group.Value is ulong handle)
                {
                    return handle;
                }
            }

            return 0;
        }

        private static double Number(DwgObject one, int code)
        {
            foreach (DwgGroup group in one.Body)
            {
                if (group.Code == code && group.Value is double value)
                {
                    return value;
                }
            }

            return double.NaN;
        }

        private static int Int(DwgObject one, int code)
        {
            foreach (DwgGroup group in one.Body)
            {
                if (group.Code == code && group.Value is int value)
                {
                    return value;
                }
            }

            return 0;
        }

        private static void Rename(List<DwgGroup> groups, byte[] old, byte[] name)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                if (ReferenceEquals(groups[i].Value, old))
                {
                    groups[i] = new DwgGroup(groups[i].Code, name);
                }
            }
        }

        private DwgObject Find(ulong handle) =>
            handle != 0 && _byHandle.TryGetValue(handle, out DwgObject found) ? found : null;

        /// <summary>The name of the record at <paramref name="handle"/> as the bytes of the file; null where there is none.</summary>
        private byte[] NameOf(ulong handle) => Find(handle)?.RecordName;

        private static bool Same(byte[] name, string text)
        {
            if (name.Length != text.Length)
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                if (char.ToUpperInvariant((char)name[i]) != char.ToUpperInvariant(text[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void Write(DwgGroup group)
        {
            if (group.Value is DwgName name)
            {
                byte[] found = NameOf(name.Handle) ?? Encoding.ASCII.GetBytes(name.Fallback);

                if (name.Capitals && (Same(found, "ByLayer") || Same(found, "ByBlock")))
                {
                    found = Encoding.ASCII.GetBytes(Same(found, "ByLayer") ? "BYLAYER" : "BYBLOCK");
                }

                if (name.OmitWhen == null || !Same(found, name.OmitWhen))
                {
                    _dxf.Write(group.Code, found);
                }
            }
            else if (group.Value is DwgBookColour book)
            {
                DwgColour colour = Find(book.Book)?.BookColour;
                _dxf.Write(62, colour?.Index ?? book.Index);

                if (colour != null && colour.Rgb >= 0)
                {
                    _dxf.Write(420, colour.Rgb);
                }

                if (colour?.Name != null && colour.Name.Length > 0)
                {
                    _dxf.WriteValue(
                        430,
                        colour.BookName != null && colour.BookName.Length > 0
                            ? JoinBook(colour.BookName, colour.Name)
                            : colour.Name
                    );
                }
            }
            else if (group.Value is DwgLinetypeIndex linetype)
            {
                // The records of the table follow ByBlock and ByLayer, which the control lists first.
                List<ulong> entries = Find(_header.Handle("_LTYPES"))?.Entries;
                byte[] named =
                    entries != null && linetype.Index >= 0 && linetype.Index + 2 < entries.Count
                        ? NameOf(entries[linetype.Index + 2])
                        : null;
                _dxf.Write(group.Code, named ?? Encoding.ASCII.GetBytes("BYLAYER"));
            }
            else if (group.Value is DwgViewportNumber number)
            {
                _dxf.Write(
                    group.Code,
                    _viewportNumbers.TryGetValue(number.Viewport, out int[] numbers)
                        ? numbers[number.Status ? 1 : 0]
                        : 0
                );
            }
            else if (group.Code == DwgObjectReader.LongText)
            {
                // From R2007 the texts are UTF-8, whose characters of several bytes are kept whole.
                _dxf.WriteChunks(3, 1, (byte[])group.Value, 250, _doubleByte, _version >= 1021);
            }
            else
            {
                _dxf.WriteValue(group.Code, group.Value);
            }
        }

        private void Write(List<DwgGroup> groups)
        {
            foreach (DwgGroup group in groups)
            {
                Write(group);
            }
        }

        private void WriteHeader()
        {
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "HEADER");

            foreach (string entry in HeaderOrder.Split(';'))
            {
                string[] parts = entry.Split(' ');
                string name = parts[0];
                int code = int.Parse(parts[1], CultureInfo.InvariantCulture);
                object value;

                if (parts.Length > 2 && _version < int.Parse(parts[2], CultureInfo.InvariantCulture))
                {
                    continue;
                }

                if (name == "ACADMAINTVER" && _version >= 1032)
                {
                    code = 90;
                }

                if (name == "XCLIPFRAME" && _version < 1024)
                {
                    // Before R2010 the frame of a clipped xref is on or off, a flag of group 290; the value 2 of later
                    // releases (shown, not plotted) is written off, as AutoCAD writes it.
                    _dxf.Write(9, "$XCLIPFRAME");
                    _dxf.Write(290, _header.Int("XCLIPFRAME") == 1 ? 1 : 0);
                    continue;
                }

                switch (name)
                {
                    case "ACADVER":
                        value = _container.Version;
                        break;
                    case "ACADMAINTVER":
                        value = _container.MaintenanceVersion;
                        break;
                    case "DWGCODEPAGE":
                        value = _container.CodePage;
                        break;
                    case "MEASUREMENT":
                        value = _container.Measurement;
                        break;
                    default:
                        value = _header.Get(name);
                        break;
                }

                if (value == null)
                {
                    continue;
                }

                _dxf.Write(9, "$" + name);

                if (value is ulong handle && name != "HANDSEED" && (code < 330 || code > 369))
                {
                    _dxf.Write(code, NameOf(handle) ?? new byte[0]);
                }
                else if (value is double angle && code == 50)
                {
                    _dxf.Write(code, angle * 180.0 / Math.PI);
                }
                else
                {
                    _dxf.WriteValue(code, value);
                }
            }

            _dxf.Write(0, "ENDSEC");
        }

        private void WriteClasses()
        {
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "CLASSES");

            foreach (DwgClass one in _classes)
            {
                _dxf.Write(0, "CLASS");
                _dxf.Write(1, one.DxfName);
                _dxf.Write(2, one.CppName);
                _dxf.Write(3, one.Application);
                _dxf.Write(90, one.ProxyFlags);

                if (one.Count >= 0)
                {
                    _dxf.Write(91, one.Count);
                }

                _dxf.Write(280, one.WasZombie ? 1 : 0);
                _dxf.Write(281, one.IsEntity ? 1 : 0);
            }

            _dxf.Write(0, "ENDSEC");
        }

        /// <summary>The handle, the extension dictionary, the reactors and the owner every record starts with.</summary>
        private void WriteHead(DwgObject one, string name, ulong owner, int handleCode = 5)
        {
            _dxf.Write(0, name);
            _dxf.WriteHandle(handleCode, one.Handle);

            if (one.XDictionary != 0)
            {
                _dxf.Write(102, "{ACAD_XDICTIONARY");
                _dxf.WriteHandle(360, one.XDictionary);
                _dxf.Write(102, "}");
            }

            if (one.Reactors.Count > 0)
            {
                _dxf.Write(102, "{ACAD_REACTORS");

                foreach (ulong reactor in one.Reactors)
                {
                    _dxf.WriteHandle(330, reactor);
                }

                _dxf.Write(102, "}");
            }

            _dxf.WriteHandle(330, owner);
        }

        private void WriteTables()
        {
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "TABLES");

            foreach (string[] table in Tables)
            {
                DwgObject control = Find(_header.Handle(table[1]));

                if (control == null || !control.IsControl)
                {
                    throw new FormatException("The DWG file has no table of the kind " + table[0] + ".");
                }

                var records = new List<DwgObject>();

                foreach (ulong handle in control.Entries)
                {
                    DwgObject record = Find(handle);

                    if (record != null && record.IsTableRecord && record.Name == table[0] && _written.Add(handle))
                    {
                        records.Add(record);
                    }
                }

                _dxf.Write(0, "TABLE");
                _dxf.Write(2, table[0]);
                _dxf.WriteHandle(5, control.Handle);

                if (control.XDictionary != 0)
                {
                    _dxf.Write(102, "{ACAD_XDICTIONARY");
                    _dxf.WriteHandle(360, control.XDictionary);
                    _dxf.Write(102, "}");
                }

                _dxf.WriteHandle(330, 0);
                _dxf.Write(100, "AcDbSymbolTable");
                _dxf.Write(70, control.BlockFlags);

                Write(control.Body);
                Write(control.XData);

                foreach (DwgObject record in records)
                {
                    WriteHead(record, table[0], control.Handle, table[0] == "DIMSTYLE" ? 105 : 5);
                    _dxf.Write(100, "AcDbSymbolTableRecord");
                    Write(record.Body);
                    Write(record.XData);
                }

                _dxf.Write(0, "ENDTAB");
            }

            _dxf.Write(0, "ENDSEC");
        }

        private void WriteEntity(DwgObject entity, ulong owner, bool paper, bool nested)
        {
            if (!_written.Add(entity.Handle))
            {
                return;
            }

            _progress?.Report(_written.Count, _objects.Count);

            if (!entity.Decoded)
            {
                return;
            }

            ulong parent = entity.EntityMode == 0 && entity.Owner != 0 ? entity.Owner : owner;
            WriteHead(entity, entity.Name, parent);
            _dxf.Write(100, "AcDbEntity");

            if (paper)
            {
                _dxf.Write(67, 1);
            }

            _dxf.Write(8, NameOf(entity.Layer) ?? new byte[] { (byte)'0' });

            // AutoCAD 2021 writes no material for a BLOCK or an ENDBLK, though the file may give them one.
            Write(
                entity.Type == 0x04 || entity.Type == 0x05
                    ? entity.Common.FindAll(group => group.Code != 347)
                    : entity.Common
            );

            if (entity.ProxyGraphics != null)
            {
                WriteBinary(_version >= 1024 ? 160 : 92, entity.ProxyGraphics);
            }

            if (entity.Type == 0x04)
            {
                DwgObject header = Find(owner);
                _dxf.Write(100, "AcDbBlockBegin");

                if (header?.BlockBody != null)
                {
                    Write(header.BlockBody);
                }
            }

            if (entity.Type == 0x0A)
            {
                WriteVertex(entity, Find(parent));
            }
            else
            {
                Write(entity.Body);
            }

            Write(entity.XData);

            if (nested || (entity.Owned != null ? entity.Owned.Count == 0 : entity.First == 0))
            {
                return;
            }

            WriteOwned(entity, entity.Handle, paper, true);
            DwgObject end = Find(entity.SeqEnd);

            if (end != null && end.IsEntity)
            {
                WriteEntity(end, entity.Handle, paper, true);
            }
        }

        /// <summary>The name DXF gives a colour of a book: the book, a dollar sign, the colour.</summary>
        private static byte[] JoinBook(byte[] book, byte[] name)
        {
            var joined = new byte[book.Length + 1 + name.Length];
            Array.Copy(book, joined, book.Length);
            joined[book.Length] = (byte)'$';
            Array.Copy(name, 0, joined, book.Length + 1, name.Length);
            return joined;
        }

        /// <summary>A run of bytes as DXF writes it: its length under <paramref name="sizeCode"/>, then pieces of 127 bytes in hex under 310.</summary>
        private void WriteBinary(int sizeCode, byte[] bytes)
        {
            _dxf.Write(sizeCode, bytes.Length);
            var hex = new StringBuilder(254);

            for (int at = 0; at < bytes.Length; at += 127)
            {
                hex.Clear();

                for (int i = at; i < Math.Min(at + 127, bytes.Length); i++)
                {
                    hex.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
                }

                _dxf.Write(310, hex.ToString());
            }
        }

        /// <summary>A vertex of a 2D polyline: DXF leaves its widths out where they are the widths of the polyline.</summary>
        private void WriteVertex(DwgObject vertex, DwgObject polyline)
        {
            double start = Width(vertex, 40);
            double end = Width(vertex, 41);
            bool own = polyline == null || start != Width(polyline, 40) || end != Width(polyline, 41);

            foreach (DwgGroup group in vertex.Body)
            {
                if (own || (group.Code != 40 && group.Code != 41))
                {
                    Write(group);
                }
            }
        }

        private static double Width(DwgObject one, int code)
        {
            foreach (DwgGroup group in one.Body)
            {
                if (group.Code == code && group.Value is double width)
                {
                    return width;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// The entities <paramref name="holder"/> owns, under <paramref name="owner"/>: from R2004 the list it holds, in its
        /// order; before R2004 the chain from its first entity to its last.
        /// </summary>
        private void WriteOwned(DwgObject holder, ulong owner, bool paper, bool nested)
        {
            if (holder.Owned == null)
            {
                WriteChain(holder.First, holder.Last, owner, paper, nested);
                return;
            }

            foreach (ulong handle in holder.Owned)
            {
                DwgObject entity = Find(handle);

                if (entity != null && entity.IsEntity)
                {
                    WriteEntity(entity, owner, paper, nested);
                }
            }
        }

        /// <summary>The entities from <paramref name="first"/> to <paramref name="last"/>, each leading to the next.</summary>
        private void WriteChain(ulong first, ulong last, ulong owner, bool paper, bool nested)
        {
            ulong at = first;

            // An entity is written once, so a chain that turns back on itself ends there.
            while (true)
            {
                DwgObject entity = Find(at);

                if (entity == null || !entity.IsEntity || _written.Contains(at))
                {
                    break;
                }

                WriteEntity(entity, owner, paper, nested);

                if (at == last)
                {
                    return;
                }

                at = entity.Next;
            }

            // The chain broke before its end, at an entity that was left out. The entities it did not reach are still
            // there, and they are written in the order of their handles, which is the order they were made in.
            ulong model = _header.Handle("_MODELSPACE");
            ulong activePaper = _header.Handle("_PAPERSPACE");

            foreach (DwgObject entity in _objects)
            {
                bool owned =
                    entity.EntityMode == 0 ? entity.Owner == owner
                    : entity.EntityMode == 2 ? owner == model
                    : owner == activePaper;

                // A BLOCK, an ENDBLK and a SEQEND are not of the chain: the one who owns them writes them.
                if (
                    entity.IsEntity
                    && owned
                    && entity.Type != 0x04
                    && entity.Type != 0x05
                    && entity.Type != 0x06
                    && !_written.Contains(entity.Handle)
                )
                {
                    WriteEntity(entity, owner, paper, nested);
                }
            }
        }

        private bool IsPaperSpace(DwgObject block)
        {
            return block.RecordName != null
                && block.RecordName.Length >= 12
                && Same(Slice(block.RecordName, 12), "*PAPER_SPACE");
        }

        private static byte[] Slice(byte[] bytes, int count)
        {
            var slice = new byte[count];
            Array.Copy(bytes, slice, count);
            return slice;
        }

        private void WriteBlocks()
        {
            DwgObject control = Find(_header.Handle("_BLOCKS"));
            ulong model = _header.Handle("_MODELSPACE");
            ulong activePaper = _header.Handle("_PAPERSPACE");
            var blocks = new List<DwgObject>();

            foreach (ulong handle in control.Entries)
            {
                DwgObject block = Find(handle);

                if (block != null && block.BlockBody != null && !blocks.Contains(block))
                {
                    blocks.Add(block);
                }
            }

            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "BLOCKS");

            foreach (DwgObject block in blocks)
            {
                bool paper = IsPaperSpace(block);
                DwgObject begin = Find(block.BlockEntity);
                DwgObject end = Find(block.EndBlock);

                if (begin == null || end == null || !begin.IsEntity || !end.IsEntity)
                {
                    continue;
                }

                WriteEntity(begin, block.Handle, paper, true);

                if (block.Handle != model && block.Handle != activePaper)
                {
                    WriteOwned(block, block.Handle, paper, false);
                }

                WriteEntity(end, block.Handle, paper, true);
            }

            _dxf.Write(0, "ENDSEC");
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "ENTITIES");

            foreach (DwgObject block in blocks)
            {
                if (block.Handle == model || block.Handle == activePaper)
                {
                    WriteOwned(block, block.Handle, block.Handle == activePaper, false);
                }
            }

            _dxf.Write(0, "ENDSEC");
        }

        private void WriteObjects()
        {
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "OBJECTS");
            DwgObject root = Find(_header.Handle("_NAMEDOBJECTS"));

            if (root != null)
            {
                WriteObject(root);
            }

            foreach (DwgObject one in _objects)
            {
                WriteObject(one);
            }

            _dxf.Write(0, "ENDSEC");
        }

        /// <summary>
        /// The schemas AutoCAD writes in the section ACDSDATA for the ACIS data of the solids, as pairs of a group and its
        /// value: the schema of the records of ACIS data, an ID and the data, and the four schemas of their attributes.
        /// </summary>
        private static readonly string[] AcisSchemas =
        {
            "0|ACDSSCHEMA|90|0|1|AcDb3DSolid_ASM_Data|2|AcDbDs::ID|280|10|91|8|2|ASM_Data|280|15|91|0|101|ACDSRECORD|95|0|90|1"
                + "|2|AcDbDs::TreatedAsObjectData|280|1|291|1|101|ACDSRECORD|95|0|90|2|2|AcDbDs::Legacy|280|1|291|1"
                + "|101|ACDSRECORD|1|AcDbDs::ID|90|3|2|AcDs:Indexable|280|1|291|1"
                + "|101|ACDSRECORD|1|AcDbDs::ID|90|4|2|AcDbDs::HandleAttribute|280|7|282|1",
            "0|ACDSSCHEMA|90|1|1|AcDbDs::TreatedAsObjectDataSchema|2|AcDbDs::TreatedAsObjectData|280|1|91|0",
            "0|ACDSSCHEMA|90|2|1|AcDbDs::LegacySchema|2|AcDbDs::Legacy|280|1|91|0",
            "0|ACDSSCHEMA|90|3|1|AcDbDs::IndexedPropertySchema|2|AcDs:Indexable|280|1|91|0",
            "0|ACDSSCHEMA|90|4|1|AcDbDs::HandleAttributeSchema|2|AcDbDs::HandleAttribute|280|7|91|1|284|1",
        };

        /// <summary>
        /// From R2013 the section ACDSDATA, where the data store of the file holds ACIS data: the store's version, the
        /// schemas AutoCAD writes for that data, and a record for each entity written, its handle (320) and its SAB bytes
        /// (94 and 310). None where the store holds no record; a store that cannot be read is left out with a Warn line.
        /// </summary>
        private void WriteDataStore()
        {
            if (_container.DataStore == null)
            {
                return;
            }

            DwgDataStore store;

            try
            {
                store = DwgDataStore.Read(_container.DataStore);
            }
            catch (FormatException fault)
            {
                GeometryHelperLog.Warn("DwgReader: the data store of the ACIS data was left out: " + fault.Message);
                return;
            }

            List<KeyValuePair<ulong, byte[]>> records = store.Records.FindAll(r => _written.Contains(r.Key));

            if (records.Count == 0)
            {
                return;
            }

            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "ACDSDATA");
            _dxf.Write(70, store.Version);
            _dxf.Write(71, store.Flags);

            foreach (string schema in AcisSchemas)
            {
                string[] parts = schema.Split('|');

                for (int i = 0; i + 1 < parts.Length; i += 2)
                {
                    _dxf.Write(int.Parse(parts[i], CultureInfo.InvariantCulture), parts[i + 1]);
                }
            }

            foreach (KeyValuePair<ulong, byte[]> record in records)
            {
                _dxf.Write(0, "ACDSRECORD");
                _dxf.Write(90, 0);
                _dxf.Write(2, "AcDbDs::ID");
                _dxf.Write(280, 10);
                _dxf.Write(320, record.Key.ToString("X", CultureInfo.InvariantCulture));
                _dxf.Write(2, "ASM_Data");
                _dxf.Write(280, 15);
                WriteBinary(94, record.Value);
            }

            _dxf.Write(0, "ENDSEC");
        }

        private void WriteObject(DwgObject one)
        {
            if (one.IsEntity || one.IsControl || one.IsTableRecord || !one.Decoded || !_written.Add(one.Handle))
            {
                return;
            }

            _progress?.Report(_written.Count, _objects.Count);

            WriteHead(one, one.Name, one.Owner);
            Write(one.Body);
            Write(one.XData);
        }

        /// <summary>One line for the types that are not read, with the count of each, and one for the whole call.</summary>
        private void LogSummary()
        {
            var skipped = new SortedDictionary<string, int>(StringComparer.Ordinal);
            int written = 0;

            foreach (DwgObject one in _objects)
            {
                if (one.Decoded)
                {
                    written += _written.Contains(one.Handle) ? 1 : 0;
                    continue;
                }

                skipped.TryGetValue(one.Name, out int count);
                skipped[one.Name] = count + 1;
            }

            int skippedCount = 0;

            if (skipped.Count > 0)
            {
                var line = new StringBuilder("DwgReader: types that are not read yet were left out:");

                foreach (KeyValuePair<string, int> pair in skipped)
                {
                    line.Append(' ')
                        .Append(pair.Key)
                        .Append(' ')
                        .Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                    skippedCount += pair.Value;
                }

                GeometryHelperLog.Debug(line.ToString());
            }

            GeometryHelperLog.Info(
                "DwgReader: read a DWG of "
                    + DwgContainer.ReleaseOf(_container.Version)
                    + " with "
                    + _container.ObjectMap.Count.ToString(CultureInfo.InvariantCulture)
                    + " objects: "
                    + written.ToString(CultureInfo.InvariantCulture)
                    + " turned into DXF records, "
                    + skippedCount.ToString(CultureInfo.InvariantCulture)
                    + " of types not read yet, "
                    + Broken.ToString(CultureInfo.InvariantCulture)
                    + " broken."
            );
        }
    }
}
