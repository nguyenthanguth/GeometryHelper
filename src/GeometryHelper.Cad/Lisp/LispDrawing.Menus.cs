using System;
using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>menucmd</c> and <c>getcname</c>, the two functions of the menus that need the session: the first evaluates a DIESEL
    /// expression (<c>(menucmd "M=$(getvar,date)")</c>) with the session's system variables and gives nil for every other menu
    /// string, as the presenter has no menu to point at; the second gives the English name of a command from its local one and
    /// back, as the presenter's commands are named in English: <c>(getcname "LINE")</c> is <c>"_LINE"</c> and
    /// <c>(getcname "_LINE")</c> is <c>"LINE"</c>.
    /// </summary>
    internal static partial class LispDrawing
    {
        /// <summary>Defines the functions of this file; called with the others of the entity functions.</summary>
        private static void RegisterMenuFunctions(LispInterpreter lisp, CadCommandSession session)
        {
            lisp.Define("MENUCMD", 1, 1, a => MenuCommand(session, LispInterpreter.Str(a[0])));
            lisp.Define("GETCNAME", 1, 1, a => CommandName(session, LispInterpreter.Str(a[0])));
        }

        private static object MenuCommand(CadCommandSession session, string text)
        {
            if (!text.StartsWith("M=", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var diesel = new CadDiesel(
                name =>
                {
                    string shown = session.Registry.FindVariable(name)?.GetValue(session);
                    return shown != null && shown.Length >= 2 && shown[0] == '"' && shown[shown.Length - 1] == '"'
                        ? shown.Substring(1, shown.Length - 2)
                        : shown ?? string.Empty;
                },
                DateTime.Now,
                CultureInfo.CurrentCulture
            );
            return diesel.Evaluate(text.Substring(2));
        }

        private static object CommandName(CadCommandSession session, string typed)
        {
            string name = typed.Trim();
            bool english = name.StartsWith("_", StringComparison.Ordinal);
            name = english ? name.Substring(1) : name;
            name = name.StartsWith(".", StringComparison.Ordinal) ? name.Substring(1) : name;
            CadCommand command = session.Registry.FindByName(name);

            if (command == null)
            {
                return null;
            }

            return english ? command.Name : "_" + command.Name;
        }
    }
}
