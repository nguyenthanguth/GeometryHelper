namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// The functions of AutoLISP that work on the drawing and ask the user: the get functions, <c>initget</c>, the selection
    /// sets, the entities, the tables and the variables. They exist only in the interpreter of a
    /// <see cref="CadCommandSession"/>, registered when the session makes it, since a bare interpreter has no drawing. Each
    /// family registers itself in a file of its own, through the partial methods below.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>Defines the drawing functions in the interpreter of a session.</summary>
        /// <param name="lisp">The interpreter.</param>
        /// <param name="session">The session whose drawing and prompts the functions use.</param>
        internal static void Register(LispInterpreter lisp, CadCommandSession session)
        {
            RegisterInput(lisp, session);
            RegisterCommand(lisp, session);
            RegisterEntities(lisp, session);
            RegisterTables(lisp, session);
            RegisterSelection(lisp, session);
            RegisterVariables(lisp, session);
            RegisterSession(lisp, session);
            RegisterFiles(lisp, session);
            RegisterPickfirst(lisp, session);
            RegisterCurves(lisp, session);
            RegisterActiveX(lisp, session);
            RegisterDialogs(lisp, session);
            RegisterXdata(lisp, session);
            RegisterDictionaries(lisp, session);
            RegisterGraphics(lisp, session);
            RegisterGrRead(lisp, session);
        }

        static partial void RegisterInput(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterCommand(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterEntities(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterTables(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterSelection(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterVariables(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterSession(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterFiles(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterPickfirst(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterCurves(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterActiveX(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterDialogs(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterXdata(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterDictionaries(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterGraphics(LispInterpreter lisp, CadCommandSession session);

        static partial void RegisterGrRead(LispInterpreter lisp, CadCommandSession session);
    }
}
