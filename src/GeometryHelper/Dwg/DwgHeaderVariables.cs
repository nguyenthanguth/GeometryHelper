using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The header variables of a DWG file: the values of its header section read in the order the file keeps them, each
    /// under the name of its system variable, with the handles of the tables and dictionaries the file is entered through.
    /// A flag is kept as an int, a number as a double, a point as an array of doubles, a text as its bytes (in the drawing's
    /// code page before R2007, UTF-8 from R2007), a handle as a ulong and a whole number of 64 bits as a long.
    /// </summary>
    internal sealed class DwgHeaderVariables
    {
        /// <summary>
        /// The header from R2000 on, one entry per value: where the value came or went with a version, the first version
        /// that has it (<c>1018+</c>) or the first that has it no more (<c>-1018</c>); its bit code; its name. A name
        /// starting with a question mark is a value the specification does not name; one starting with an underscore is a
        /// handle the reader itself uses. The writer writes the header by the same list.
        /// </summary>
        internal const string Layout =
            "1027+ BLL REQUIREDVERSIONS;"
            + "BD ?1;BD ?2;BD ?3;BD ?4;TV ?5;TV ?6;TV ?7;TV ?8;BL ?9;BL ?10;-1018 H _VPORTHEADER;"
            + "B DIMASO;B DIMSHO;B PLINEGEN;B ORTHOMODE;B REGENMODE;B FILLMODE;B QTEXTMODE;B PSLTSCALE;B LIMCHECK;1018+ B ?11;"
            + "B USRTIMER;B SKPOLY;B ANGDIR;B SPLFRAME;B MIRRTEXT;B WORLDVIEW;B TILEMODE;B PLIMCHECK;B VISRETAIN;B DISPSILH;"
            + "B PELLIPSE;BS PROXYGRAPHICS;BS TREEDEPTH;BS LUNITS;BS LUPREC;BS AUNITS;BS AUPREC;BS ATTMODE;BS PDMODE;"
            + "1018+ BL ?12;1018+ BL ?13;1018+ BL ?14;"
            + "BS USERI1;BS USERI2;BS USERI3;BS USERI4;BS USERI5;BS SPLINESEGS;BS SURFU;BS SURFV;BS SURFTYPE;BS SURFTAB1;"
            + "BS SURFTAB2;BS SPLINETYPE;BS SHADEDGE;BS SHADEDIF;BS UNITMODE;BS MAXACTVP;BS ISOLINES;BS CMLJUST;BS TEXTQLTY;"
            + "BD LTSCALE;BD TEXTSIZE;BD TRACEWID;BD SKETCHINC;BD FILLETRAD;BD THICKNESS;BD ANGBASE;BD PDSIZE;BD PLINEWID;"
            + "BD USERR1;BD USERR2;BD USERR3;BD USERR4;BD USERR5;BD CHAMFERA;BD CHAMFERB;BD CHAMFERC;BD CHAMFERD;BD FACETRES;"
            + "BD CMLSCALE;BD CELTSCALE;TV MENU;DT TDUCREATE;DT TDUUPDATE;1018+ BL ?15;1018+ BL ?16;1018+ BL ?17;"
            + "DT TDINDWG;DT TDUSRTIMER;CMC CECOLOR;HS HANDSEED;"
            + "H CLAYER;H TEXTSTYLE;H CELTYPE;1021+ H CMATERIAL;H DIMSTYLE;H CMLSTYLE;BD PSVPSCALE;"
            + "3BD PINSBASE;3BD PEXTMIN;3BD PEXTMAX;2RD PLIMMIN;2RD PLIMMAX;BD PELEVATION;3BD PUCSORG;3BD PUCSXDIR;3BD PUCSYDIR;"
            + "H PUCSNAME;H PUCSORTHOREF;BS PUCSORTHOVIEW;H PUCSBASE;3BD PUCSORGTOP;3BD PUCSORGBOTTOM;3BD PUCSORGLEFT;"
            + "3BD PUCSORGRIGHT;3BD PUCSORGFRONT;3BD PUCSORGBACK;"
            + "3BD INSBASE;3BD EXTMIN;3BD EXTMAX;2RD LIMMIN;2RD LIMMAX;BD ELEVATION;3BD UCSORG;3BD UCSXDIR;3BD UCSYDIR;"
            + "H UCSNAME;H UCSORTHOREF;BS UCSORTHOVIEW;H UCSBASE;3BD UCSORGTOP;3BD UCSORGBOTTOM;3BD UCSORGLEFT;"
            + "3BD UCSORGRIGHT;3BD UCSORGFRONT;3BD UCSORGBACK;"
            + "TV DIMPOST;TV DIMAPOST;BD DIMSCALE;BD DIMASZ;BD DIMEXO;BD DIMDLI;BD DIMEXE;BD DIMRND;BD DIMDLE;BD DIMTP;BD DIMTM;"
            + "1021+ BD DIMFXL;1021+ BD DIMJOGANG;1021+ BS DIMTFILL;1021+ CMC DIMTFILLCLR;"
            + "B DIMTOL;B DIMLIM;B DIMTIH;B DIMTOH;B DIMSE1;B DIMSE2;BS DIMTAD;BS DIMZIN;BS DIMAZIN;1021+ BS DIMARCSYM;"
            + "BD DIMTXT;BD DIMCEN;BD DIMTSZ;BD DIMALTF;BD DIMLFAC;BD DIMTVP;BD DIMTFAC;BD DIMGAP;BD DIMALTRND;"
            + "B DIMALT;BS DIMALTD;B DIMTOFL;B DIMSAH;B DIMTIX;B DIMSOXD;CMC DIMCLRD;CMC DIMCLRE;CMC DIMCLRT;"
            + "BS DIMADEC;BS DIMDEC;BS DIMTDEC;BS DIMALTU;BS DIMALTTD;BS DIMAUNIT;BS DIMFRAC;BS DIMLUNIT;BS DIMDSEP;BS DIMTMOVE;"
            + "BS DIMJUST;B DIMSD1;B DIMSD2;BS DIMTOLJ;BS DIMTZIN;BS DIMALTZ;BS DIMALTTZ;B DIMUPT;BS DIMATFIT;1021+ B DIMFXLON;"
            + "1024+ B DIMTXTDIRECTION;1024+ BD ?18;1024+ TV ?19;1024+ BD ?20;1024+ TV ?21;"
            + "H DIMTXSTY;H DIMLDRBLK;H DIMBLK;H DIMBLK1;H DIMBLK2;1021+ H DIMLTYPE;1021+ H DIMLTEX1;1021+ H DIMLTEX2;"
            + "BS DIMLWD;BS DIMLWE;"
            + "H _BLOCKS;H _LAYERS;H _STYLES;H _LTYPES;H _VIEWS;H _UCSS;H _VPORTS;H _APPIDS;H _DIMSTYLES;-1018 H _VPORTENTITIES;"
            + "H _GROUPS;H _MLINESTYLES;H _NAMEDOBJECTS;BS TSTACKALIGN;BS TSTACKSIZE;TV HYPERLINKBASE;TV STYLESHEET;"
            + "H _LAYOUTS;H _PLOTSETTINGS;H _PLOTSTYLES;1018+ H _MATERIALS;1018+ H _COLORS;1021+ H _VISUALSTYLES;1027+ H ?22;"
            + "BL _FLAGS;BS INSUNITS;BS CEPSNTYPE;H?CEPSNTYPE=3 CPSNID;TV FINGERPRINTGUID;TV VERSIONGUID;"
            + "1018+ RC SORTENTS;1018+ RC INDEXCTL;1018+ RC HIDETEXT;1018+ RC XCLIPFRAME;1018+ RC DIMASSOC;1018+ RC HALOGAP;"
            + "1018+ BS OBSCOLOR;1018+ BS INTERSECTIONCOLOR;1018+ RC OBSLTYPE;1018+ RC INTERSECTIONDISPLAY;1018+ TV PROJECTNAME;"
            + "H _PAPERSPACE;H _MODELSPACE;H _BYLAYER;H _BYBLOCK;H _CONTINUOUS;"
            + "1021+ B CAMERADISPLAY;1021+ BL ?23;1021+ BL ?24;1021+ BD ?25;1021+ BD STEPSPERSEC;1021+ BD STEPSIZE;"
            + "1021+ BD 3DDWFPREC;1021+ BD LENSLENGTH;1021+ BD CAMERAHEIGHT;1021+ RC SOLIDHIST;1021+ RC SHOWHIST;"
            + "1021+ BD PSOLWIDTH;1021+ BD PSOLHEIGHT;1021+ BD LOFTANG1;1021+ BD LOFTANG2;1021+ BD LOFTMAG1;1021+ BD LOFTMAG2;"
            + "1021+ BS LOFTPARAM;1021+ RC LOFTNORMALS;1021+ BD LATITUDE;1021+ BD LONGITUDE;1021+ BD NORTHDIRECTION;"
            + "1021+ BL TIMEZONE;1021+ RC LIGHTGLYPHDISPLAY;1021+ RC TILEMODELIGHTSYNCH;1021+ RC DWFFRAME;1021+ RC DGNFRAME;"
            + "1021+ B REALWORLDSCALE;1021+ CMC INTERFERECOLOR;1021+ H INTERFEREOBJVS;1021+ H INTERFEREVPVS;1021+ H DRAGVS;"
            + "1021+ RC CSHADOW;1021+ BD SHADOWPLANELOCATION";

        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        private DwgHeaderVariables() { }

        /// <summary>
        /// Reads the header variables from the data of their section. From R2007 the data start with their size in bits;
        /// the texts are UTF-16 in a string stream at their end and the handles follow them, as in an object. Throws
        /// <see cref="FormatException"/> where the data end before the last of them.
        /// </summary>
        public static DwgHeaderVariables Read(DwgContainer container)
        {
            int version = DwgContainer.NumberOf(container.Version);
            var header = new DwgHeaderVariables();
            var data = new DwgBitReader(container.Header);
            DwgBitReader strings = null;
            DwgBitReader handles = data;

            if (version >= 1021)
            {
                long bits = (uint)data.RawLong();

                if (bits <= 32 || bits > data.End)
                {
                    throw new FormatException("The DWG header variables name more data than they hold.");
                }

                long end = DwgBitReader.StringStream(container.Header, 32, bits - 1, out strings);
                handles = new DwgBitReader(container.Header, bits, data.End);
                data = new DwgBitReader(container.Header, 32, end);
            }

            foreach (string item in Layout.Split(';'))
            {
                string entry = item;
                string guard = entry.Substring(0, entry.IndexOf(' '));

                if (guard[0] == '-' || guard[guard.Length - 1] == '+')
                {
                    entry = entry.Substring(guard.Length + 1);
                    bool from = guard[guard.Length - 1] == '+';
                    int bound = int.Parse(
                        from ? guard.Substring(0, guard.Length - 1) : guard.Substring(1),
                        CultureInfo.InvariantCulture
                    );

                    if (from ? version < bound : version >= bound)
                    {
                        continue;
                    }
                }

                int split = entry.IndexOf(' ');
                string code = entry.Substring(0, split);
                string name = entry.Substring(split + 1);
                int condition = code.IndexOf('?');

                if (condition > 0)
                {
                    string[] parts = code.Substring(condition + 1).Split('=');
                    code = code.Substring(0, condition);

                    if (header.Int(parts[0]) != int.Parse(parts[1], CultureInfo.InvariantCulture))
                    {
                        continue;
                    }
                }

                header._values[name] = Value(data, strings, handles, code, version);
            }

            int flags = header.Int("_FLAGS");
            header._values["CELWEIGHT"] = DwgObjectReader.LineWeight(flags & 0x1F);
            header._values["ENDCAPS"] = (flags >> 5) & 3;
            header._values["JOINSTYLE"] = (flags >> 7) & 3;
            header._values["LWDISPLAY"] = (flags & 0x200) == 0 ? 1 : 0;
            header._values["XEDIT"] = (flags & 0x400) == 0 ? 1 : 0;
            header._values["EXTNAMES"] = (flags & 0x800) != 0 ? 1 : 0;
            header._values["PSTYLEMODE"] = (flags & 0x2000) != 0 ? 1 : 0;
            header._values["OLESTARTUP"] = (flags & 0x4000) != 0 ? 1 : 0;
            return header;
        }

        /// <summary>Whether the header has a value under <paramref name="name"/>.</summary>
        public bool Has(string name) => _values.ContainsKey(name);

        /// <summary>The value under <paramref name="name"/> as it was read; null where there is none.</summary>
        public object Get(string name) => _values.TryGetValue(name, out object value) ? value : null;

        /// <summary>The flag or count under <paramref name="name"/>; 0 where there is none.</summary>
        public int Int(string name) => _values.TryGetValue(name, out object value) && value is int number ? number : 0;

        /// <summary>The handle under <paramref name="name"/>; 0 where there is none.</summary>
        public ulong Handle(string name) =>
            _values.TryGetValue(name, out object value) && value is ulong handle ? handle : 0;

        private static object Value(
            DwgBitReader data,
            DwgBitReader strings,
            DwgBitReader handles,
            string code,
            int version
        )
        {
            switch (code)
            {
                case "B":
                    return data.Bit() ? 1 : 0;
                case "BS":
                    return data.BitShort();
                case "BL":
                    return data.BitLong();
                case "BLL":
                    return data.BitLongLong();
                case "RC":
                    return (int)data.RawChar();
                case "BD":
                    return data.BitDouble();
                case "TV":
                    return Text(data, strings, version);
                case "H":
                    return handles.Handle();
                case "HS":
                    return data.Handle();
                case "CMC":
                    int index = data.BitShort();

                    if (version >= 1018)
                    {
                        // From R2004 a colour also has its RGB value and the names of a colour of a book; the header writes the index.
                        int rgb = data.BitLong();
                        int names = data.RawChar();
                        index =
                            ((rgb >> 24) & 0xFF) == 0xC3 ? rgb & 0xFF
                            : ((rgb >> 24) & 0xFF) == 0xC0 ? 256
                            : ((rgb >> 24) & 0xFF) == 0xC1 ? 0
                            : index;

                        if ((names & 1) != 0)
                        {
                            Text(data, strings, version);
                        }

                        if ((names & 2) != 0)
                        {
                            Text(data, strings, version);
                        }
                    }

                    return index;
                case "3BD":
                    return data.Point3();
                case "2RD":
                    return data.RawPoint2();
                case "DT":
                    // A Julian day and the milliseconds into it.
                    int day = data.BitLong();
                    return day + (data.BitLong() / 86400000.0);
                default:
                    throw new InvalidOperationException("The header list has the unknown bit code " + code + ".");
            }
        }

        private static byte[] Text(DwgBitReader data, DwgBitReader strings, int version)
        {
            if (version < 1021)
            {
                return data.Text();
            }

            return strings == null ? new byte[0] : strings.TextUnicode();
        }
    }
}
