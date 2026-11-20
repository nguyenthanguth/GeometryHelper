using System.Collections.Generic;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// <c>getvar</c> and <c>setvar</c>: the system variables of the presenter (<see cref="CadCommandRegistry.Variables"/>) read
    /// and set as AutoLISP does, through <see cref="CadSystemVariable.ValueForLisp"/> and
    /// <see cref="CadSystemVariable.SetFromLisp"/>. A whole number is an integer, a real or an angle (in radians) a real, a point
    /// a list of three reals (two for the variables AutoCAD keeps as a 2D point). A variable the presenter does not have is nil
    /// for <c>getvar</c> and a rejected setting for <c>setvar</c>.
    /// </summary>
    internal static partial class LispDrawing
    {
        static partial void RegisterVariables(LispInterpreter lisp, CadCommandSession session)
        {
            // A session with no window has no OSMODE: the routines that save it, set it and put it back (ZZ) need one, and the
            // drawing of AutoCAD's blank template has 167. A window adds its own, which replaces this.
            if (session.Registry.FindVariable("OSMODE") == null)
            {
                int modes = 167;
                session.Registry.AddVariable(CadSystemVariable.Integer("OSMODE", 0, 32767, () => modes, v => modes = v));
            }

            lisp.Define("GETVAR", 1, 1, a => GetVar(lisp, session, VariableName(a[0])));
            lisp.Define("SETVAR", 2, 2, a => SetVar(lisp, session, VariableName(a[0]), a[1]));
        }

        /// <summary>The variable of a name, in any case; an alias (LTS for LTSCALE) is no name to AutoLISP.</summary>
        private static CadSystemVariable Named(CadCommandSession session, string name)
        {
            CadSystemVariable variable = session.Registry.FindVariable(name);
            return variable != null && string.Equals(variable.Name, name.Trim(), System.StringComparison.OrdinalIgnoreCase)
                ? variable
                : null;
        }

        /// <summary>The name a string or a symbol gives; AutoCAD 2021's error for anything else (q6: <c>(getvar 5)</c>).</summary>
        private static string VariableName(object value)
        {
            if (value is string text)
            {
                return text;
            }

            if (value is LispSymbol symbol)
            {
                return symbol.Name;
            }

            if (value == null)
            {
                return "NIL";
            }

            throw new LispException("bad argument type: (or stringp symbolp): " + LispInterpreter.Print(value, true));
        }

        private static object GetVar(LispInterpreter lisp, CadCommandSession session, string name) =>
            lisp.OnOwner(() =>
            {
                CadSystemVariable variable = Named(session, name);
                return variable == null ? null : ToLisp(variable.ValueForLisp(session));
            });

        private static object ToLisp(object value)
        {
            if (value is double[] point)
            {
                var items = new List<object>(point.Length);

                foreach (double coordinate in point)
                {
                    items.Add(coordinate);
                }

                return LispInterpreter.ListOf(items);
            }

            return value;
        }

        /// <summary>
        /// Sets a variable through its own rules and gives the value as it was given (q6: <c>(setvar "TEXTSIZE" 3)</c> is 3, and
        /// <c>(getvar "TEXTSIZE")</c> then 3.0). A change of a variable the drawing keeps opens the undo step of the evaluation first.
        /// </summary>
        private static object SetVar(LispInterpreter lisp, CadCommandSession session, string name, object value)
        {
            bool taken = (bool)
                lisp.OnOwner(() =>
                {
                    CadSystemVariable variable = Named(session, name);
                    object neutral = Neutral(value);

                    if (variable == null || neutral == null)
                    {
                        return false;
                    }

                    if (variable.IsInDrawing)
                    {
                        session.OpenLispStep();
                    }

                    return variable.SetFromLisp(session, neutral);
                });

            if (!taken)
            {
                throw new LispException(
                    "AutoCAD variable setting rejected: "
                        + LispInterpreter.Print(name, true)
                        + " "
                        + LispInterpreter.Print(value, true)
                );
            }

            return value;
        }

        /// <summary>The value of a form as the neutral type a variable takes: an int, a double, a string, or a double[] for a list of two or three numbers; null for nil and the rest.</summary>
        private static object Neutral(object value)
        {
            if (value is int || value is double || value is string)
            {
                return value;
            }

            GeometryHelper.Geometry.GeoPoint3? point = PointOf(value);

            if (point == null)
            {
                return null;
            }

            int count = LispInterpreter.ItemsOf(value).Count;
            return count == 2
                ? new[] { point.Value.X, point.Value.Y }
                : new[] { point.Value.X, point.Value.Y, point.Value.Z };
        }
    }
}
