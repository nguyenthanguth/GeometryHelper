using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    public sealed partial class CadCommandSession
    {
        // The block tool whose INSERT is being started, which the request of that INSERT is answered from.
        private CadTool _askedTool;
        private string _askedFile;
        private GeoPoint2? _askedAt;

        /// <summary>
        /// Runs a tool of a tool palette, as clicking it does in AutoCAD. A command tool starts the command of its line, with
        /// the answers the line gives to its first prompts fed in; a hatch tool starts HATCH with the tool's pattern, angle and
        /// scale; a block tool starts INSERT with its block, scale and rotation, asking the insertion point on the command line
        /// (and the scale and rotation, where the tool prompts for them). The tool's layer, colour, linetype and lineweight
        /// are the current ones while the command runs, and the drawing's are put back when it ends or is cancelled. A command
        /// running is ended first, as Esc would.
        /// </summary>
        /// <param name="tool">The tool.</param>
        /// <param name="at">Where the tool is dropped on the drawing, as dragging it from the palette does: a block is put there, scale and rotation as the tool has them, and a hatch fills the area round it; null to ask on the command line. A command tool ignores it.</param>
        /// <returns>False, with the reason written to the history, where the tool cannot run: a text or separator tool, a command tool with no command or one that is unknown, a block tool whose block is not in the drawing and whose drawing cannot be found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the tool is null.</exception>
        public bool StartTool(CadTool tool, GeoPoint2? at = null)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            switch (tool.Kind)
            {
                case CadToolKind.Command:
                    System.Collections.Generic.IReadOnlyList<string> words = CadToolRun.Words(tool.Command);

                    if (words.Count == 0)
                    {
                        Write("The tool " + tool.Name + " has no command.");
                        Changed();
                        return false;
                    }

                    if (!Start(words[0], tool))
                    {
                        return false;
                    }

                    for (int i = 1; i < words.Count && IsRunning; i++)
                    {
                        Feed(words[i]);
                    }

                    return true;
                case CadToolKind.Hatch:
                    if (!Start("HATCH", tool))
                    {
                        return false;
                    }

                    if (at.HasValue && IsRunning)
                    {
                        Feed(at.Value);

                        if (IsRunning)
                        {
                            Enter();
                        }
                    }

                    return true;
                case CadToolKind.Block:
                    string problem = CadToolRun.BlockProblem(Document, tool, out string file);

                    if (problem != null)
                    {
                        Write(problem);
                        Changed();
                        return false;
                    }

                    _askedTool = tool;
                    _askedFile = file;
                    _askedAt = at;

                    try
                    {
                        return Start("INSERT", tool);
                    }
                    finally
                    {
                        _askedTool = null;
                        _askedFile = null;
                        _askedAt = null;
                    }
                default:
                    return false;
            }
        }

        /// <summary>Answers the INSERT request of a block tool being started; false for any other request.</summary>
        private bool AnswersToolInsert(string name, object payload)
        {
            if (_askedTool == null || name != "INSERT" || !(payload is CadInsertRequest request))
            {
                return false;
            }

            CadToolRun.Answer(request, _askedTool, _askedFile, _askedAt);
            return true;
        }
    }
}
