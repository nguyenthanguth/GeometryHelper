using System;
using System.Collections.Generic;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// The header variables of a DWG file: the values of its header section read in the order the file keeps them, each
    /// under the name of its system variable, with the handles of the tables and dictionaries the file is entered through.
    /// A flag is kept as an int, a number as a double, a point as an array of doubles, a text as its bytes in the drawing's
    /// code page, and a handle as a ulong.
    /// </summary>
    internal sealed class DwgHeaderVariables
    {
        /// <summary>
        /// The header of R2000, one entry per value: its bit code, a space, the name. A name starting with a question mark
        /// is a value the specification does not name; one starting with an underscore is a handle the reader itself uses.
        /// </summary>
        private const string R2000 =
            "BD ?1;BD ?2;BD ?3;BD ?4;TV ?5;TV ?6;TV ?7;TV ?8;BL ?9;BL ?10;H _VPORTHEADER;" +
            "B DIMASO;B DIMSHO;B PLINEGEN;B ORTHOMODE;B REGENMODE;B FILLMODE;B QTEXTMODE;B PSLTSCALE;B LIMCHECK;B USRTIMER;" +
            "B SKPOLY;B ANGDIR;B SPLFRAME;B MIRRTEXT;B WORLDVIEW;B TILEMODE;B PLIMCHECK;B VISRETAIN;B DISPSILH;B PELLIPSE;" +
            "BS PROXYGRAPHICS;BS TREEDEPTH;BS LUNITS;BS LUPREC;BS AUNITS;BS AUPREC;BS ATTMODE;BS PDMODE;" +
            "BS USERI1;BS USERI2;BS USERI3;BS USERI4;BS USERI5;BS SPLINESEGS;BS SURFU;BS SURFV;BS SURFTYPE;BS SURFTAB1;" +
            "BS SURFTAB2;BS SPLINETYPE;BS SHADEDGE;BS SHADEDIF;BS UNITMODE;BS MAXACTVP;BS ISOLINES;BS CMLJUST;BS TEXTQLTY;" +
            "BD LTSCALE;BD TEXTSIZE;BD TRACEWID;BD SKETCHINC;BD FILLETRAD;BD THICKNESS;BD ANGBASE;BD PDSIZE;BD PLINEWID;" +
            "BD USERR1;BD USERR2;BD USERR3;BD USERR4;BD USERR5;BD CHAMFERA;BD CHAMFERB;BD CHAMFERC;BD CHAMFERD;BD FACETRES;" +
            "BD CMLSCALE;BD CELTSCALE;TV MENU;DT TDUCREATE;DT TDUUPDATE;DT TDINDWG;DT TDUSRTIMER;CMC CECOLOR;H HANDSEED;" +
            "H CLAYER;H TEXTSTYLE;H CELTYPE;H DIMSTYLE;H CMLSTYLE;BD PSVPSCALE;" +
            "3BD PINSBASE;3BD PEXTMIN;3BD PEXTMAX;2RD PLIMMIN;2RD PLIMMAX;BD PELEVATION;3BD PUCSORG;3BD PUCSXDIR;3BD PUCSYDIR;" +
            "H PUCSNAME;H PUCSORTHOREF;BS PUCSORTHOVIEW;H PUCSBASE;3BD PUCSORGTOP;3BD PUCSORGBOTTOM;3BD PUCSORGLEFT;" +
            "3BD PUCSORGRIGHT;3BD PUCSORGFRONT;3BD PUCSORGBACK;" +
            "3BD INSBASE;3BD EXTMIN;3BD EXTMAX;2RD LIMMIN;2RD LIMMAX;BD ELEVATION;3BD UCSORG;3BD UCSXDIR;3BD UCSYDIR;" +
            "H UCSNAME;H UCSORTHOREF;BS UCSORTHOVIEW;H UCSBASE;3BD UCSORGTOP;3BD UCSORGBOTTOM;3BD UCSORGLEFT;" +
            "3BD UCSORGRIGHT;3BD UCSORGFRONT;3BD UCSORGBACK;" +
            "TV DIMPOST;TV DIMAPOST;BD DIMSCALE;BD DIMASZ;BD DIMEXO;BD DIMDLI;BD DIMEXE;BD DIMRND;BD DIMDLE;BD DIMTP;BD DIMTM;" +
            "B DIMTOL;B DIMLIM;B DIMTIH;B DIMTOH;B DIMSE1;B DIMSE2;BS DIMTAD;BS DIMZIN;BS DIMAZIN;" +
            "BD DIMTXT;BD DIMCEN;BD DIMTSZ;BD DIMALTF;BD DIMLFAC;BD DIMTVP;BD DIMTFAC;BD DIMGAP;BD DIMALTRND;" +
            "B DIMALT;BS DIMALTD;B DIMTOFL;B DIMSAH;B DIMTIX;B DIMSOXD;CMC DIMCLRD;CMC DIMCLRE;CMC DIMCLRT;" +
            "BS DIMADEC;BS DIMDEC;BS DIMTDEC;BS DIMALTU;BS DIMALTTD;BS DIMAUNIT;BS DIMFRAC;BS DIMLUNIT;BS DIMDSEP;BS DIMTMOVE;" +
            "BS DIMJUST;B DIMSD1;B DIMSD2;BS DIMTOLJ;BS DIMTZIN;BS DIMALTZ;BS DIMALTTZ;B DIMUPT;BS DIMATFIT;" +
            "H DIMTXSTY;H DIMLDRBLK;H DIMBLK;H DIMBLK1;H DIMBLK2;BS DIMLWD;BS DIMLWE;" +
            "H _BLOCKS;H _LAYERS;H _STYLES;H _LTYPES;H _VIEWS;H _UCSS;H _VPORTS;H _APPIDS;H _DIMSTYLES;H _VPORTENTITIES;" +
            "H _GROUPS;H _MLINESTYLES;H _NAMEDOBJECTS;BS TSTACKALIGN;BS TSTACKSIZE;TV HYPERLINKBASE;TV STYLESHEET;" +
            "H _LAYOUTS;H _PLOTSETTINGS;H _PLOTSTYLES;BL _FLAGS;BS INSUNITS;BS CEPSNTYPE;H?CEPSNTYPE=3 CPSNID;" +
            "TV FINGERPRINTGUID;TV VERSIONGUID;H _PAPERSPACE;H _MODELSPACE;H _BYLAYER;H _BYBLOCK;H _CONTINUOUS";

        private readonly Dictionary<string, object> _values = new Dictionary<string, object>(StringComparer.Ordinal);

        private DwgHeaderVariables()
        {
        }

        /// <summary>
        /// Reads the header variables from the data of their section. Throws <see cref="FormatException"/> where the data
        /// ends before the last of them.
        /// </summary>
        public static DwgHeaderVariables Read(DwgContainer container)
        {
            var header = new DwgHeaderVariables();
            var reader = new DwgBitReader(container.Header);

            foreach (string entry in R2000.Split(';'))
            {
                int space = entry.IndexOf(' ');
                string code = entry.Substring(0, space);
                string name = entry.Substring(space + 1);
                int condition = code.IndexOf('?');

                if (condition > 0)
                {
                    string[] parts = code.Substring(condition + 1).Split('=');
                    code = code.Substring(0, condition);

                    if (header.Int(parts[0]) != int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture))
                    {
                        continue;
                    }
                }

                header._values[name] = Value(reader, code);
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
        public ulong Handle(string name) => _values.TryGetValue(name, out object value) && value is ulong handle ? handle : 0;

        private static object Value(DwgBitReader reader, string code)
        {
            switch (code)
            {
                case "B": return reader.Bit() ? 1 : 0;
                case "BS": return reader.BitShort();
                case "BL": return reader.BitLong();
                case "BD": return reader.BitDouble();
                case "TV": return reader.Text();
                case "H": return reader.Handle();
                case "CMC": return reader.BitShort();
                case "3BD": return reader.Point3();
                case "2RD": return reader.RawPoint2();
                case "DT":
                    // A Julian day and the milliseconds into it.
                    int day = reader.BitLong();
                    return day + (reader.BitLong() / 86400000.0);
                default: throw new InvalidOperationException("The header list has the unknown bit code " + code + ".");
            }
        }
    }
}
