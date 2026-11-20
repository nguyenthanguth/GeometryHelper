using System;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        /// <summary>
        /// <c>rtos</c>, <c>angtos</c>, <c>distof</c> and <c>angtof</c>, which write and read lengths and angles as the drawing's
        /// units say: the mode and the precision left out are the drawing's LUNITS and LUPREC (AUNITS and AUPREC for angles),
        /// and <c>rtos</c> and <c>angtos</c> suppress zeros as its DIMZIN says.
        /// </summary>
        partial void RegisterUnits()
        {
            Define("RTOS", 1, 3, a =>
                Units(() =>
                    LispUnits.Rtos(
                        ToDouble(Number(a[0])),
                        a.Length > 1 ? Int(a[1]) : Setting("LUNITS", 2),
                        a.Length > 2 ? Int(a[2]) : Setting("LUPREC", 4),
                        Setting("DIMZIN", 0)
                    )
                )
            );
            Define("ANGTOS", 1, 3, a =>
                Units(() =>
                    LispUnits.Angtos(
                        ToDouble(Number(a[0])),
                        a.Length > 1 ? Int(a[1]) : Setting("AUNITS", 0),
                        a.Length > 2 ? Int(a[2]) : Setting("AUPREC", 0),
                        Setting("DIMZIN", 0)
                    )
                )
            );
            Define("DISTOF", 1, 2, a =>
                Units(() => LispUnits.Distof(Str(a[0]), a.Length > 1 ? Int(a[1]) : Setting("LUNITS", 2)))
            );
            Define("ANGTOF", 1, 2, a =>
                Units(() => LispUnits.Angtof(Str(a[0]), a.Length > 1 ? Int(a[1]) : Setting("AUNITS", 0)))
            );
        }

        /// <summary>The drawing's whole-number setting <paramref name="name"/>, or what a blank drawing has where <see cref="Variable"/> gives none.</summary>
        private int Setting(string name, int fallback)
        {
            object value = Variable?.Invoke(name);
            return value is int whole ? whole
                : value is double real ? (int)real
                : fallback;
        }

        /// <summary>
        /// Runs a units function, turning its refusal of a mode or a precision into <c>bad argument value: rtos mode: 6</c>.
        /// </summary>
        private static object Units(Func<object> work)
        {
            try
            {
                return work();
            }
            catch (ArgumentOutOfRangeException refused)
            {
                string message = refused.Message;
                int end = message.IndexOfAny(new[] { '\r', '\n' });
                throw new LispException("bad argument value: " + (end < 0 ? message : message.Substring(0, end)));
            }
        }
    }
}
