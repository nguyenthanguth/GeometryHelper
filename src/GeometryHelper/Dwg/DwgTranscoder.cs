using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly List<DwgObject> _objects;
        private readonly Dictionary<ulong, DwgObject> _byHandle;
        private readonly DwgHandleSet _written;
        private readonly Dictionary<ulong, int[]> _viewportNumbers = new Dictionary<ulong, int[]>();
        private DwgDxfWriter _dxf;
        private readonly ProgressSteps _progress;
        private readonly CancellationToken _stop;
        private readonly bool _doubleByte;
        private readonly int _version;

        /// <summary>The bytes of a text before R2007 read one to a letter, for the name of a file.</summary>
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>The folders the files of shapes are looked for in, in their order; null or empty for none.</summary>
        private IList<string> _supportFolders;

        /// <summary>The names of the shapes of each file of shapes looked for, by the file as its style gives it; null where it was not found.</summary>
        private readonly Dictionary<string, Dictionary<int, string>> _shapeFiles = new Dictionary<
            string,
            Dictionary<int, string>
        >(StringComparer.OrdinalIgnoreCase);

        private DwgTranscoder(DwgContainer container, ProgressSteps progress, CancellationToken stop)
        {
            _container = container;

            // Made to hold every object of the file, so that neither is grown, and its arrays made and let go of, as they fill.
            _objects = new List<DwgObject>(container.ObjectMap.Count);
            _byHandle = new Dictionary<ulong, DwgObject>(container.ObjectMap.Count);
            _written = new DwgHandleSet(container.ObjectMap.Count);

            // A DXF is some six times its DWG (1.2 to 11.9 in 182 drawings, 6 in the two of 300 000 entities): seven times the file is made at once, so that its array is not grown in most.
            _dxf = new DwgDxfWriter((int)Math.Min(container.Objects.LongLength * 7, 512L << 20));
            _progress = progress;
            _stop = stop;
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
            progress?.Stage(0.05, 0.6);
            int broken = ReadObjects(progress, out string firstFault);

            Broken = broken;
            NumberPaperSpaces();
            NumberViewports();
            FillLeaderBoxes();
            AddCellStyleMaps();
            LeaveOutIncompleteGraphs();

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

        /// <summary>
        /// How many threads decode the objects of a file: 0 to let the size of the file and the machine decide (one thread
        /// for a file of fewer than <see cref="ParallelFrom"/> objects, else as many as the machine has up to
        /// <see cref="MostThreads"/>), 1 for the calling thread alone, or the number asked for whatever the file's size. The
        /// DXF made is the same bytes for each; it is set by the tests and the benchmarks.
        /// </summary>
        internal static int DecodeThreads { get; set; }

        /// <summary>The fewest objects a file holds for its objects to be decoded on several threads.</summary>
        private const int ParallelFrom = 4096;

        /// <summary>The most threads that decode the objects of a file.</summary>
        private const int MostThreads = 8;

        /// <summary>How many objects a thread takes at a time.</summary>
        private const int ChunkSize = 512;

        private static int ThreadsFor(int objects)
        {
            if (DecodeThreads > 0)
            {
                return DecodeThreads;
            }

            return objects < ParallelFrom ? 1 : Math.Min(Environment.ProcessorCount, MostThreads);
        }

        /// <summary>
        /// A decoder refuses broken bytes with a <see cref="FormatException"/>; the geometry it hands decoded values to may
        /// refuse values no drawing has (a spline whose plane cannot be worked out) in its own way. Either is one broken
        /// object, left out and counted.
        /// </summary>
        private static bool IsBrokenObject(Exception fault) =>
            fault is FormatException
            || fault is InvalidOperationException
            || fault is ArgumentException
            || fault is ArithmeticException
            || fault is IndexOutOfRangeException;

        private static string FaultOf(ulong handle, Exception fault) =>
            "the object " + handle.ToString("X", CultureInfo.InvariantCulture) + ": " + fault.Message;

        /// <summary>
        /// Decodes the objects of the file into <see cref="_objects"/> and <see cref="_byHandle"/> in the order of the object
        /// map, and gives the number left out as broken and the first of their faults, as the map's order puts it.
        /// </summary>
        private int ReadObjects(ProgressSteps progress, out string firstFault)
        {
            List<KeyValuePair<ulong, long>> map = _container.ObjectMap;

            // The offsets by handle, made the first time a table asks for its style, so that a drawing of no table pays nothing.
            var offsets = new Lazy<Dictionary<ulong, long>>(
                () =>
                {
                    var made = new Dictionary<ulong, long>();

                    foreach (KeyValuePair<ulong, long> entry in map)
                    {
                        made[entry.Key] = entry.Value;
                    }

                    return made;
                },
                LazyThreadSafetyMode.ExecutionAndPublication
            );

            // A reader holds the object it is reading, so each thread has its own.
            DwgObjectReader NewReader() =>
                new DwgObjectReader(_container.Objects, _classes, _container.Version)
                {
                    OffsetOf = handle => offsets.Value.TryGetValue(handle, out long at) ? at : (long?)null,
                };

            int threads = ThreadsFor(map.Count);

            if (threads > 1)
            {
                return ReadObjectsOnThreads(NewReader, threads, progress, out firstFault);
            }

            DwgObjectReader reader = NewReader();
            int broken = 0;
            firstFault = null;
            int read = 0;

            foreach (KeyValuePair<ulong, long> entry in map)
            {
                _stop.ThrowIfCancellationRequested();
                progress?.Report(read++, map.Count);

                try
                {
                    DwgObject one = reader.Read(entry.Key, entry.Value);
                    _objects.Add(one);
                    _byHandle[one.Handle] = one;
                }
                catch (Exception fault) when (IsBrokenObject(fault))
                {
                    broken++;
                    firstFault = firstFault ?? FaultOf(entry.Key, fault);
                }
            }

            return broken;
        }

        /// <summary>
        /// Decodes the objects as <see cref="ReadObjects"/> does, in chunks of the map taken by <paramref name="threads"/>
        /// threads, each with a reader of its own, the calling thread one of them. What the one thread would give comes out
        /// the same: the objects are put in the order of the map after they are decoded, the first broken object told is the
        /// first of the map and not the first to be met, a failure that is not one of a broken object is the one of the
        /// lowest object, and a token stopped is answered by the threads leaving off at the next object.
        /// </summary>
        private int ReadObjectsOnThreads(
            Func<DwgObjectReader> newReader,
            int threads,
            ProgressSteps progress,
            out string firstFault
        )
        {
            List<KeyValuePair<ulong, long>> map = _container.ObjectMap;
            int count = map.Count;
            var decoded = new DwgObject[count];
            var gate = new object();
            int broken = 0;
            int firstBroken = int.MaxValue;
            string firstText = null;
            int failedAt = int.MaxValue;
            Exception failure = null;
            bool stopped = false;
            int done = 0;

            Parallel.For(
                0,
                (count + ChunkSize - 1) / ChunkSize,
                new ParallelOptions { MaxDegreeOfParallelism = threads },
                newReader,
                (chunk, state, reader) =>
                {
                    int from = chunk * ChunkSize;
                    int to = Math.Min(from + ChunkSize, count);

                    // Beyond a failure no object is of any use: the failure thrown is the lowest one.
                    if (from > Volatile.Read(ref failedAt))
                    {
                        return reader;
                    }

                    for (int i = from; i < to; i++)
                    {
                        if (_stop.IsCancellationRequested)
                        {
                            Volatile.Write(ref stopped, true);
                            state.Stop();
                            return reader;
                        }

                        try
                        {
                            decoded[i] = reader.Read(map[i].Key, map[i].Value);
                        }
                        catch (Exception fault) when (IsBrokenObject(fault))
                        {
                            lock (gate)
                            {
                                broken++;

                                if (i < firstBroken)
                                {
                                    firstBroken = i;
                                    firstText = FaultOf(map[i].Key, fault);
                                }
                            }
                        }
                        catch (Exception fault)
                        {
                            lock (gate)
                            {
                                if (i < failedAt)
                                {
                                    Volatile.Write(ref failedAt, i);
                                    failure = fault;
                                }
                            }

                            break;
                        }
                    }

                    // Told by whichever thread ends a chunk, one at a time and each chunk adding its own: with the calling thread
                    // alone telling, a file of 6 030 objects (12 chunks) went from 0.045 to 0.54 in one step when the other
                    // threads took the chunks before it ended its own. Nothing is told once a stop is asked for.
                    lock (gate)
                    {
                        done += to - from;

                        if (progress != null && !_stop.IsCancellationRequested)
                        {
                            progress.Report(done, count);
                        }
                    }

                    return reader;
                },
                reader => { }
            );

            if (Volatile.Read(ref stopped))
            {
                _stop.ThrowIfCancellationRequested();
            }

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }

            foreach (DwgObject one in decoded)
            {
                if (one != null)
                {
                    _objects.Add(one);
                    _byHandle[one.Handle] = one;
                }
            }

            firstFault = firstText;
            return broken;
        }

        /// <summary>The number of objects left out because they were broken.</summary>
        private int Broken { get; }

        /// <summary>
        /// The DXF of the DWG in <paramref name="file"/>, as bytes in the drawing's code page. Throws
        /// <see cref="FormatException"/> for a file that is not a DWG or is broken where nothing can be read past it, and
        /// <see cref="NotSupportedException"/> for a DWG of a version that is not read.
        /// </summary>
        /// <remarks>
        /// The files of shapes are looked for in <paramref name="supportFolders"/>, in their order. How far it has got is
        /// told to <paramref name="progress"/> (null for none): its parts unpacked to 0.05, its objects read to 0.6, and the
        /// DXF of them written to 1. A token that is stopped is looked at before each page of a section is unpacked, before
        /// each object is read and before each entity and object is written, and ends the work with an
        /// <see cref="OperationCanceledException"/>.
        /// </remarks>
        public static byte[] ToDxf(
            byte[] file,
            IList<string> supportFolders = null,
            ProgressSteps progress = null,
            CancellationToken stop = default
        )
        {
            progress?.Stage(0.0, 0.05);
            return ToDxf(DwgContainer.Read(file, stop), supportFolders, progress, stop);
        }

        /// <summary>
        /// The DXF of the DWG in <paramref name="file"/> as <see cref="ToDxf(byte[], IList{string}, ProgressSteps, CancellationToken)"/>
        /// makes it, in the array it was written in and not in a copy of it cut to its length, for a reader of it that is given
        /// the length (<see cref="Dxf.DxfDatabase"/>): a DXF of 46 MB is not copied, and its copy is not left to be collected.
        /// The bytes from <paramref name="length"/> on are not DXF.
        /// </summary>
        public static byte[] ToDxfBuffer(
            byte[] file,
            out int length,
            IList<string> supportFolders = null,
            ProgressSteps progress = null,
            CancellationToken stop = default
        )
        {
            progress?.Stage(0.0, 0.05);
            return Transcode(DwgContainer.Read(file, stop), supportFolders, progress, stop).Take(out length);
        }

        /// <summary>
        /// The DXF of the DWG whose parts <paramref name="container"/> holds, as
        /// <see cref="ToDxf(byte[], IList{string}, ProgressSteps, CancellationToken)"/>.
        /// </summary>
        public static byte[] ToDxf(
            DwgContainer container,
            IList<string> supportFolders = null,
            ProgressSteps progress = null,
            CancellationToken stop = default
        ) => Transcode(container, supportFolders, progress, stop).ToArray();

        private static DwgDxfWriter Transcode(
            DwgContainer container,
            IList<string> supportFolders,
            ProgressSteps progress,
            CancellationToken stop
        )
        {
            var transcoder = new DwgTranscoder(container, progress, stop) { _supportFolders = supportFolders };
            progress?.Stage(0.6, 1.0);
            transcoder.WriteHeader();
            transcoder.WriteClasses();
            transcoder.WriteTables();
            transcoder.WriteBlocks();
            transcoder.WriteObjects();
            transcoder.WriteDataStore();
            transcoder.WriteThumbnail();
            transcoder._dxf.Write(0, "EOF");
            transcoder.LogSummary();
            progress?.Report(1.0);
            return transcoder._dxf;
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
        /// Leaves out what a dynamic block keeps whole or not at all: an evaluation graph that names an expression not read
        /// (a constraint parameter whose dependency is not read is no expression), a network that names an action not read, a
        /// parameter that points to a variable or dependency not read, and each object of the network (variable, dependency, group,
        /// body) whose owner is not read. A graph that names an expression the drawing lacks is no graph, and AutoCAD drops its
        /// nodes; one that leans on a network with a hole in it would lose its constraints and be refused. The block stays an
        /// ordinary one.
        /// </summary>
        private void LeaveOutIncompleteGraphs()
        {
            bool left = true;

            while (left)
            {
                left = false;

                foreach (DwgObject one in _objects)
                {
                    if (!one.Decoded || !IsLeftOut(one))
                    {
                        continue;
                    }

                    one.Decoded = false;
                    left = true;

                    // The expressions a graph owns go with it.
                    if (one.Name == "ACAD_EVALUATION_GRAPH")
                    {
                        foreach (DwgObject owned in _objects)
                        {
                            if (owned.Owner == one.Handle)
                            {
                                owned.Decoded = false;
                            }
                        }
                    }
                }
            }
        }

        private bool IsKept(ulong handle) => handle == 0 || (Find(handle)?.Decoded ?? false);

        /// <summary>Whether the object of a dynamic block's graph or network has to be left out for what it needs and lacks.</summary>
        private bool IsLeftOut(DwgObject one)
        {
            string after = null;

            switch (one.Name)
            {
                case "ACAD_EVALUATION_GRAPH":
                    return !PointersAreKept(one, 360, null);
                case "ACDBASSOCNETWORK":
                    return !PointersAreKept(one, 360, "AcDbAssocNetwork") || !IsKept(one.Owner);
                case "BLOCKHORIZONTALCONSTRAINTPARAMETER":
                case "BLOCKVERTICALCONSTRAINTPARAMETER":
                case "BLOCKRADIALCONSTRAINTPARAMETER":
                    after = "AcDbBlockConstraintParameter";
                    break;
                case "BLOCKUSERPARAMETER":
                    after = "AcDbBlockUserParameter";
                    break;
                case "ACDBASSOCVARIABLE":
                case "ACDBASSOC2DCONSTRAINTGROUP":
                case "ACDBASSOCDEPENDENCY":
                case "ACDBASSOCGEOMDEPENDENCY":
                case "ACDBASSOCVALUEDEPENDENCY":
                case "ASSOCDIMDEPENDENCYBODY":
                case "BLOCKPARAMDEPENDENCYBODY":
                    return !IsKept(one.Owner);
                default:
                    return false;
            }

            // A parameter points, past its marker, to the dependency or variable it leans on.
            return !PointersAreKept(one, 330, after, true);
        }

        /// <summary>
        /// Whether the objects the groups of <paramref name="code"/> name are all read: those past the group that is the marker
        /// <paramref name="after"/> (none for the whole body), the first only where <paramref name="first"/> is set.
        /// </summary>
        private bool PointersAreKept(DwgObject one, int code, string after, bool first = false)
        {
            bool past = after == null;

            foreach (DwgGroup group in one.Body)
            {
                if (group.Code == 100)
                {
                    past |= group.Value is string marker && marker == after;
                }
                else if (past && group.Code == code && group.Value is ulong handle)
                {
                    if (!IsKept(handle))
                    {
                        return false;
                    }

                    if (first)
                    {
                        return true;
                    }
                }
            }

            return true;
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

        [MethodImpl(DwgJit.Hot)]
        private DwgObject Find(ulong handle) =>
            handle != 0 && _byHandle.TryGetValue(handle, out DwgObject found) ? found : null;

        /// <summary>The name of the record at <paramref name="handle"/> as the bytes of the file; null where there is none.</summary>
        [MethodImpl(DwgJit.Hot)]
        private byte[] NameOf(ulong handle) => Find(handle)?.RecordName;

        /// <summary>
        /// The name of a shape in the file of shapes of its text style (group 3), that file looked for in the support folders
        /// in their order, by its name alone and with .shx where it has no extension, or where its style gives it whole;
        /// null where no folder holds a compiled file of shapes of that name, or it names no shape of that number.
        /// </summary>
        private string ShapeName(DwgShapeName shape)
        {
            byte[] file = null;

            foreach (DwgGroup group in Find(shape.Style)?.BodyIfAny ?? NoGroups)
            {
                if (group.Code == 3 && group.Value is byte[] bytes)
                {
                    file = bytes;
                }
            }

            if (file == null || file.Length == 0 || _supportFolders == null || _supportFolders.Count == 0)
            {
                return null;
            }

            string named = (_version >= 1021 ? Encoding.UTF8 : Latin1).GetString(file);

            // The entities are written on several threads, and the files of shapes are read once each, by whichever asks first.
            lock (_shapeFiles)
            {
                if (!_shapeFiles.TryGetValue(named, out Dictionary<int, string> names))
                {
                    names = ShapeFile(named);
                    _shapeFiles.Add(named, names);
                }

                return names != null && names.TryGetValue(shape.Number, out string name) ? name : null;
            }
        }

        /// <summary>The names of the shapes of the file of shapes <paramref name="named"/>, as <see cref="ShapeName"/> looks for it; null where it is not found.</summary>
        private Dictionary<int, string> ShapeFile(string named)
        {
            try
            {
                string name = System.IO.Path.GetFileName(named);

                if (!System.IO.Path.HasExtension(name))
                {
                    name += ".shx";
                }

                var paths = new List<string>();

                if (System.IO.Path.IsPathRooted(named))
                {
                    paths.Add(named);
                }

                foreach (string folder in _supportFolders)
                {
                    if (!string.IsNullOrEmpty(folder))
                    {
                        paths.Add(System.IO.Path.Combine(folder, name));
                    }
                }

                foreach (string path in paths)
                {
                    Dictionary<int, string> names = System.IO.File.Exists(path)
                        ? Dxf.DxfSupportFiles.ShapeNames(System.IO.File.ReadAllBytes(path))
                        : null;

                    if (names != null)
                    {
                        return names;
                    }
                }
            }
            catch (Exception fault)
                when (fault is System.IO.IOException
                    || fault is UnauthorizedAccessException
                    || fault is ArgumentException
                    || fault is NotSupportedException
                )
            {
                GeometryHelperLog.Warn(
                    "DwgReader: the file of shapes " + named + " could not be read: " + fault.Message
                );
            }

            return null;
        }

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

        [MethodImpl(DwgJit.Hot)]
        private void Write(DwgGroup group)
        {
            // A number held in the group itself, which is most of them, is written as it is, with no object made of it.
            if (group.IsReal)
            {
                _dxf.Write(group.Code, group.RealValue);
                return;
            }

            if (group.IsWhole)
            {
                _dxf.Write(group.Code, (long)group.WholeValue);
                return;
            }

            WriteOther(group);
        }

        [MethodImpl(DwgJit.Hot)]
        private void WriteOther(DwgGroup group)
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
            else if (group.Value is DwgShapeName shape)
            {
                string named = ShapeName(shape);

                if (named != null)
                {
                    _dxf.Write(2, named);
                }
                else
                {
                    _dxf.Write(70, shape.Number);
                }
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

        [MethodImpl(DwgJit.Hot)]
        private void Write(List<DwgGroup> groups)
        {
            // Null for an object that has none of them: no list is made for it.
            if (groups == null)
            {
                return;
            }

            foreach (DwgGroup group in groups)
            {
                Write(group);
            }
        }

        /// <summary>
        /// The name a header variable gets where its handle names no record: AutoCAD 2021 refuses a DXF whose current layer,
        /// text style or linetype has no name ("Invalid or incomplete DXF input"), so those are the ones every drawing has.
        /// </summary>
        private static byte[] NameWhereNoneIsNamed(string variable)
        {
            switch (variable)
            {
                case "CLAYER":
                    return Encoding.ASCII.GetBytes("0");
                case "TEXTSTYLE":
                    return Encoding.ASCII.GetBytes("Standard");
                case "CELTYPE":
                    return Encoding.ASCII.GetBytes("ByLayer");
                default:
                    return new byte[0];
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
                    _dxf.Write(code, NameOf(handle) ?? NameWhereNoneIsNamed(name));
                }
                else if (value is double angle && code == 50)
                {
                    _dxf.Write(code, angle * 180.0 / Math.PI);
                }
                else
                {
                    _dxf.WriteValue(code, value);
                }

                // The properties of a drawing of R2004 or later follow the code page, as AutoCAD writes them.
                if (name == "DWGCODEPAGE")
                {
                    WriteSummaryInfo();
                }
            }

            _dxf.Write(0, "ENDSEC");
        }

        /// <summary>
        /// The properties the section <c>AcDb:SummaryInfo</c> holds, as the variables of the header AutoCAD writes them in:
        /// <c>$TITLE</c>, <c>$SUBJECT</c>, <c>$AUTHOR</c>, <c>$KEYWORDS</c>, <c>$COMMENTS</c>, <c>$LASTSAVEDBY</c> and
        /// <c>$REVISIONNUMBER</c>, each where it is not empty, then a <c>$CUSTOMPROPERTYTAG</c> and a <c>$CUSTOMPROPERTY</c> for
        /// each custom property. The base of the hyperlinks is the header's own <c>$HYPERLINKBASE</c>.
        /// </summary>
        private void WriteSummaryInfo()
        {
            DwgSummaryInfo.Fields fields = DwgSummaryInfo.Read(_container.SummaryInfo, _version >= 1021);

            if (fields == null)
            {
                return;
            }

            string[] names =
            {
                "$TITLE",
                "$SUBJECT",
                "$AUTHOR",
                "$KEYWORDS",
                "$COMMENTS",
                "$LASTSAVEDBY",
                "$REVISIONNUMBER",
            };

            for (int i = 0; i < names.Length; i++)
            {
                if (fields.Texts[i].Length > 0)
                {
                    _dxf.Write(9, names[i]);
                    _dxf.Write(1, fields.Texts[i]);
                }
            }

            foreach (KeyValuePair<byte[], byte[]> property in fields.Custom)
            {
                _dxf.Write(9, "$CUSTOMPROPERTYTAG");
                _dxf.Write(1, property.Key);
                _dxf.Write(9, "$CUSTOMPROPERTY");
                _dxf.Write(1, property.Value);
            }
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
        [MethodImpl(DwgJit.Hot)]
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

            if (one.HasReactors)
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

        [MethodImpl(DwgJit.Hot)]
        private void WriteEntity(DwgObject entity, ulong owner, bool paper, bool nested)
        {
            if (!_written.Add(entity.Handle))
            {
                return;
            }

            _stop.ThrowIfCancellationRequested();
            _progress?.Report(_written.Count, _objects.Count);

            if (!entity.Decoded)
            {
                return;
            }

            Emit(entity, owner, paper);

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

        /// <summary>
        /// Writes the record of one entity, or, while the entities of a section are being planned, puts it in the plan to be
        /// written with the others on several threads.
        /// </summary>
        [MethodImpl(DwgJit.Hot)]
        private void Emit(DwgObject entity, ulong owner, bool paper)
        {
            RenderPlan plan = _plan;

            if (plan == null)
            {
                WriteRecord(entity, owner, paper);
                return;
            }

            Plan(plan, entity, owner, paper);
        }

        /// <summary>The record of an entity as DXF has it, without those it owns: its head, common groups, own groups and extended data.</summary>
        [MethodImpl(DwgJit.Hot)]
        private void WriteRecord(DwgObject entity, ulong owner, bool paper)
        {
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
                    ? entity.CommonIfAny?.FindAll(group => group.Code != 347)
                    : entity.CommonIfAny
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
                Write(entity.BodyIfAny);
            }

            Write(entity.XDataIfAny);
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

            // BodyIfAny and not Body: the entities are written on several threads, and Body makes the list of an object that has none.
            foreach (DwgGroup group in vertex.BodyIfAny ?? NoGroups)
            {
                if (own || (group.Code != 40 && group.Code != 41))
                {
                    Write(group);
                }
            }
        }

        private static readonly List<DwgGroup> NoGroups = new List<DwgGroup>();

        private static double Width(DwgObject one, int code)
        {
            foreach (DwgGroup group in one.BodyIfAny ?? NoGroups)
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
        [MethodImpl(DwgJit.Hot)]
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
        [MethodImpl(DwgJit.Hot)]
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

            // A set, for a drawing of dimensions has a block of its own for each: 20 000 of them were 200 million comparisons in a list.
            var listed = new HashSet<DwgObject>();

            foreach (ulong handle in control.Entries)
            {
                DwgObject block = Find(handle);

                if (block != null && block.BlockBody != null && listed.Add(block))
                {
                    blocks.Add(block);
                }
            }

            int threads = ThreadsFor(_objects.Count);

            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "BLOCKS");
            BeginPlan(threads);

            try
            {
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
            }
            catch
            {
                AbandonPlan();
                throw;
            }

            EndPlan();
            _dxf.Write(0, "ENDSEC");
            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "ENTITIES");
            BeginPlan(threads);

            try
            {
                foreach (DwgObject block in blocks)
                {
                    if (block.Handle == model || block.Handle == activePaper)
                    {
                        WriteOwned(block, block.Handle, block.Handle == activePaper, false);
                    }
                }
            }
            catch
            {
                AbandonPlan();
                throw;
            }

            EndPlan();
            _dxf.Write(0, "ENDSEC");
        }

        /// <summary>An entity put in the plan of a section: the entity, the owner it is written under and whether it is in paper space.</summary>
        private struct PlannedEntity
        {
            public DwgObject Entity;
            public ulong Owner;
            public bool Paper;
        }

        /// <summary>A run of the plan that is full or ended: its entities, how many there are, and its place among the runs.</summary>
        private struct PlannedRun
        {
            public int Index;
            public PlannedEntity[] Entities;
            public int Count;
        }

        /// <summary>How many entities a thread writes at a time, into a buffer of its own that is put after the one before it.</summary>
        private const int RenderChunk = 1024;

        /// <summary>
        /// The entities of a section written on several threads. The walk over the blocks, on the calling thread, decides
        /// which entities are written, in what order and under which owner, a handful of table lookups an entity, and hands
        /// them on in runs of <see cref="RenderChunk"/> as it goes; the other threads write the records of a run into a
        /// buffer of its own, and the buffers are put in the DXF in the order of the runs, so the bytes are those of one
        /// thread writing the entities as the walk met them. The runs and the buffers are used again as they are done, so
        /// that a section of 300 000 entities makes some ten of each and not three hundred, where a collector of a drawing's
        /// 300 000 objects would take the added memory for a reason to collect.
        /// </summary>
        private sealed class RenderPlan
        {
            public readonly BlockingCollection<PlannedRun> Runs = new BlockingCollection<PlannedRun>();
            public readonly object Gate = new object();
            public readonly Stack<DwgDxfWriter> SpareBuffers = new Stack<DwgDxfWriter>();
            public readonly Stack<PlannedEntity[]> SpareRuns = new Stack<PlannedEntity[]>();
            public readonly Dictionary<int, DwgDxfWriter> Finished = new Dictionary<int, DwgDxfWriter>();

            // Of the walk, on the calling thread alone.
            public PlannedEntity[] Run;
            public int Fill;
            public int Handed;

            // Under the gate.
            public int Next;
            public int FailedAt = int.MaxValue;
            public Exception Failure;
            public Exception Unexpected;
            public bool Stopped;
            public bool Abandoned;
            public bool Closed;
            public int Active;
        }

        /// <summary>The section being written on threads; null where its entities are written as the walk meets them.</summary>
        private RenderPlan _plan;

        /// <summary>
        /// Starts the plan of a section whose entities <paramref name="threads"/> threads are to write, the calling thread
        /// one of them, or none where there is one thread: the entities are then written as the walk meets them.
        /// </summary>
        private void BeginPlan(int threads)
        {
            if (threads <= 1)
            {
                _plan = null;
                return;
            }

            RenderPlan plan = _plan = new RenderPlan();

            for (int i = 1; i < threads; i++)
            {
                Task.Run(() => WorkOnRuns(plan));
            }
        }

        /// <summary>
        /// A thread of the pool writing the runs of a plan, unless the plan is closed by the time the pool gives it a thread:
        /// a section is not kept waiting for a thread that has not begun.
        /// </summary>
        private void WorkOnRuns(RenderPlan plan)
        {
            lock (plan.Gate)
            {
                if (plan.Closed)
                {
                    return;
                }

                plan.Active++;
            }

            try
            {
                WriteRuns(plan);
            }
            catch (Exception fault)
            {
                lock (plan.Gate)
                {
                    plan.Unexpected = plan.Unexpected ?? fault;
                }
            }
            finally
            {
                lock (plan.Gate)
                {
                    plan.Active--;
                    Monitor.PulseAll(plan.Gate);
                }
            }
        }

        /// <summary>Closes a plan to the threads of the pool that have not begun, and waits for those that have to leave it.</summary>
        private static void Close(RenderPlan plan)
        {
            lock (plan.Gate)
            {
                plan.Closed = true;

                while (plan.Active > 0)
                {
                    Monitor.Wait(plan.Gate);
                }
            }

            plan.Runs.Dispose();
        }

        /// <summary>Puts an entity in the plan, to be written with the others on the threads of the section.</summary>
        [MethodImpl(DwgJit.Hot)]
        private void Plan(RenderPlan plan, DwgObject entity, ulong owner, bool paper)
        {
            if (plan.Run == null)
            {
                lock (plan.Gate)
                {
                    plan.Run = plan.SpareRuns.Count > 0 ? plan.SpareRuns.Pop() : null;
                }

                plan.Run = plan.Run ?? new PlannedEntity[RenderChunk];
                plan.Fill = 0;
            }

            plan.Run[plan.Fill++] = new PlannedEntity
            {
                Entity = entity,
                Owner = owner,
                Paper = paper,
            };

            if (plan.Fill == RenderChunk)
            {
                HandOn(plan);
            }
        }

        private static void HandOn(RenderPlan plan)
        {
            plan.Runs.Add(
                new PlannedRun
                {
                    Index = plan.Handed++,
                    Entities = plan.Run,
                    Count = plan.Fill,
                }
            );
            plan.Run = null;
            plan.Fill = 0;
        }

        /// <summary>
        /// Ends the plan of a section: the rest of its entities are handed on and the calling thread writes with the others
        /// until all are written, the DXF holding them in the order of the plan. A failure thrown is that of the first run to
        /// fail, and a token stopped is answered at the next entity.
        /// </summary>
        private void EndPlan()
        {
            RenderPlan plan = _plan;
            _plan = null;

            if (plan == null)
            {
                return;
            }

            try
            {
                if (plan.Fill > 0)
                {
                    HandOn(plan);
                }
            }
            finally
            {
                plan.Runs.CompleteAdding();
            }

            try
            {
                WriteRuns(plan);
            }
            finally
            {
                Close(plan);
            }

            if (plan.Stopped)
            {
                _stop.ThrowIfCancellationRequested();
            }

            Exception failure = plan.Failure ?? plan.Unexpected;

            if (failure != null)
            {
                ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }

        /// <summary>
        /// Ends the plan of a section the walk could not finish, a token stopped or a record that cannot be made: the threads
        /// are let go, and what they were given is not written.
        /// </summary>
        private void AbandonPlan()
        {
            RenderPlan plan = _plan;
            _plan = null;

            if (plan == null)
            {
                return;
            }

            lock (plan.Gate)
            {
                plan.Abandoned = true;
            }

            // What a thread fails with is of no use: the walk failed first, and its failure is the one thrown.
            plan.Runs.CompleteAdding();
            Close(plan);
        }

        /// <summary>
        /// Writes the runs of a plan as they are handed on, until the plan is complete, on the thread that calls it, with a
        /// copy of this transcoder whose DXF is the buffer it writes the run in; each buffer is put in the DXF once those
        /// before it are, and used again.
        /// </summary>
        private void WriteRuns(RenderPlan plan)
        {
            DwgTranscoder worker = (DwgTranscoder)MemberwiseClone();

            foreach (PlannedRun run in plan.Runs.GetConsumingEnumerable())
            {
                DwgDxfWriter buffer;
                bool skip;

                lock (plan.Gate)
                {
                    // Beyond a failure no run is of any use: the failure thrown is the lowest one.
                    skip = plan.Abandoned || plan.Stopped || run.Index > plan.FailedAt;
                    buffer = plan.SpareBuffers.Count > 0 ? plan.SpareBuffers.Pop() : null;
                }

                if (skip)
                {
                    GiveBack(plan, run, buffer);
                    continue;
                }

                buffer = buffer ?? new DwgDxfWriter(RenderChunk * 160);
                worker._dxf = buffer;

                try
                {
                    for (int i = 0; i < run.Count; i++)
                    {
                        if (_stop.IsCancellationRequested)
                        {
                            lock (plan.Gate)
                            {
                                plan.Stopped = true;
                            }

                            break;
                        }

                        worker.WriteRecord(run.Entities[i].Entity, run.Entities[i].Owner, run.Entities[i].Paper);
                    }
                }
                catch (Exception fault)
                {
                    lock (plan.Gate)
                    {
                        if (run.Index < plan.FailedAt)
                        {
                            plan.FailedAt = run.Index;
                            plan.Failure = fault;
                        }
                    }

                    GiveBack(plan, run, buffer);
                    continue;
                }

                lock (plan.Gate)
                {
                    plan.Finished[run.Index] = buffer;

                    while (plan.Finished.TryGetValue(plan.Next, out DwgDxfWriter done))
                    {
                        _dxf.Append(done);
                        done.Clear();
                        plan.SpareBuffers.Push(done);
                        plan.Finished.Remove(plan.Next++);
                    }

                    Array.Clear(run.Entities, 0, run.Count);
                    plan.SpareRuns.Push(run.Entities);
                }
            }
        }

        private static void GiveBack(RenderPlan plan, PlannedRun run, DwgDxfWriter buffer)
        {
            lock (plan.Gate)
            {
                if (buffer != null)
                {
                    buffer.Clear();
                    plan.SpareBuffers.Push(buffer);
                }

                Array.Clear(run.Entities, 0, run.Count);
                plan.SpareRuns.Push(run.Entities);
            }
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
        internal static readonly string[] AcisSchemas =
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

        /// <summary>
        /// The section THUMBNAILIMAGE, last, as AutoCAD writes it: the size of the picture of the file's preview (90) and its
        /// bytes in pieces of 127 under 310. None where the file has no picture: AutoCAD keeps a header of zeros alone in the
        /// preview of a drawing it has drawn none of.
        /// </summary>
        private void WriteThumbnail()
        {
            byte[] image = DwgPreview.Image(_container.Preview);

            if (image == null)
            {
                return;
            }

            _dxf.Write(0, "SECTION");
            _dxf.Write(2, "THUMBNAILIMAGE");
            WriteBinary(90, image);
            _dxf.Write(0, "ENDSEC");
        }

        [MethodImpl(DwgJit.Hot)]
        private void WriteObject(DwgObject one)
        {
            if (one.IsEntity || one.IsControl || one.IsTableRecord || !one.Decoded || !_written.Add(one.Handle))
            {
                return;
            }

            _stop.ThrowIfCancellationRequested();
            _progress?.Report(_written.Count, _objects.Count);

            WriteHead(one, one.Name, one.Owner);
            Write(one.BodyIfAny);
            Write(one.XDataIfAny);
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
