using System;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterSpecialForms()
        {
            _quoteFunction = DefineSpecial("QUOTE", Quote);
            DefineSpecial("FUNCTION", Function);
            DefineSpecial("LAMBDA", Lambda);
            DefineSpecial("DEFUN", args => Defun(args, asList: false));
            DefineSpecial("DEFUN-Q", args => Defun(args, asList: true));
            DefineSpecial("SETQ", Setq);
            DefineSpecial("IF", If);
            DefineSpecial("COND", Cond);
            DefineSpecial("WHILE", While);
            DefineSpecial("REPEAT", Repeat);
            DefineSpecial("FOREACH", Foreach);
            DefineSpecial("AND", And);
            DefineSpecial("OR", Or);
            DefineSpecial("PROGN", Progn);
        }

        private static LispException Syntax() => new LispException("syntax error");

        private static int Length(object list)
        {
            int count = 0;

            for (object cell = list; cell is LispCons c; cell = c.Cdr)
            {
                count++;
            }

            return count;
        }

        private object Quote(object args)
        {
            if (!(args is LispCons c) || c.Cdr != null)
            {
                throw Syntax();
            }

            return c.Car;
        }

        /// <summary>
        /// <c>(function x)</c> is <c>x</c>, except that a <c>lambda</c> is made into its function, and AutoCAD hands that
        /// back wrapped as the list <c>(quote #&lt;SUBR @... -lambda-&gt;)</c>, so <c>(function (lambda (x) x))</c> prints
        /// that way. The wrapper is accepted wherever a function is.
        /// </summary>
        private object Function(object args)
        {
            if (!(args is LispCons c) || c.Cdr != null)
            {
                throw Syntax();
            }

            if (c.Car is LispCons form && ReferenceEquals(form.Car, _lambda))
            {
                return new LispCons(
                    _quoteLower,
                    new LispCons(MakeLambda("-lambda-", form.Cdr, allowEmptyBody: true), null)
                );
            }

            return c.Car;
        }

        private object Lambda(object args) => MakeLambda("-lambda-", args, allowEmptyBody: true);

        /// <summary>
        /// <c>defun</c> binds the symbol to a function; <c>defun-q</c> binds it to the list <c>((args) body...)</c>, which
        /// AutoCAD can still call. Either one makes <c>c:name</c> a command. Returns the symbol, which prints as its name.
        /// </summary>
        private object Defun(object args, bool asList)
        {
            if (!(args is LispCons c) || !(c.Car is LispSymbol name) || !(c.Cdr is LispCons definition))
            {
                throw Syntax();
            }

            LispLambda made = MakeLambda(name.Name, definition, allowEmptyBody: false);
            object value = asList ? definition : made;
            name.Value = value;
            _defined[name] = value;
            return name;
        }

        private object Setq(object args)
        {
            if (args == null)
            {
                throw Syntax();
            }

            // The usual (setq a value) does without the walk that checks a longer list.
            if (
                args is LispCons first
                && first.Car is LispSymbol single
                && first.Cdr is LispCons second
                && second.Cdr == null
            )
            {
                return single.Value = Eval(second.Car);
            }

            for (object cell = args; cell != null; )
            {
                if (!(cell is LispCons pair) || !(pair.Car is LispSymbol) || !(pair.Cdr is LispCons value))
                {
                    throw Syntax();
                }

                cell = value.Cdr;
            }

            object result = null;

            for (object cell = args; cell is LispCons pair; )
            {
                var value = (LispCons)pair.Cdr;
                result = ((LispSymbol)pair.Car).Value = Eval(value.Car);
                cell = value.Cdr;
            }

            return result;
        }

        private object If(object args)
        {
            int count = Length(args);

            if (count < 2 || count > 3)
            {
                throw Syntax();
            }

            var test = (LispCons)args;
            var then = (LispCons)test.Cdr;

            if (Eval(test.Car) != null)
            {
                return Eval(then.Car);
            }

            return then.Cdr is LispCons otherwise ? Eval(otherwise.Car) : null;
        }

        private object Cond(object args)
        {
            for (object cell = args; cell != null; )
            {
                if (!(cell is LispCons clauses) || !(clauses.Car is LispCons clause))
                {
                    throw Syntax();
                }

                object test = Eval(clause.Car);

                if (test != null)
                {
                    if (clause.Cdr == null)
                    {
                        return test;
                    }

                    return Body(clause.Cdr);
                }

                cell = clauses.Cdr;
            }

            return null;
        }

        private object Body(object forms)
        {
            object result = null;

            for (object cell = forms; cell is LispCons c; cell = c.Cdr)
            {
                result = Eval(c.Car);
            }

            return result;
        }

        /// <summary>A <c>while</c> gives the value of the last form of its body, nil when the body never ran.</summary>
        private object While(object args)
        {
            if (!(args is LispCons c))
            {
                throw Syntax();
            }

            object result = null;

            while (Eval(c.Car) != null)
            {
                result = Body(c.Cdr);
            }

            return result;
        }

        private object Repeat(object args)
        {
            if (!(args is LispCons c))
            {
                throw Syntax();
            }

            int count = Int(Eval(c.Car));
            object result = null;

            for (int i = 0; i < count; i++)
            {
                result = Body(c.Cdr);
            }

            return result;
        }

        /// <summary>
        /// <c>(foreach x list body...)</c>: the variable takes each item in turn and has its old value back after the loop.
        /// Gives the value of the last form of the body.
        /// </summary>
        private object Foreach(object args)
        {
            if (!(args is LispCons c) || !(c.Car is LispSymbol variable) || !(c.Cdr is LispCons rest))
            {
                throw Syntax();
            }

            object list = Eval(rest.Car);
            var symbols = new[] { variable };
            var saved = new[] { variable.Value };
            object result = null;

            try
            {
                object cell = list;

                while (cell != null)
                {
                    if (!(cell is LispCons item))
                    {
                        throw TypeError("consp", cell);
                    }

                    variable.Value = item.Car;
                    result = Body(rest.Cdr);
                    cell = item.Cdr;
                }
            }
            catch (LispException error) when (error.Record(symbols, saved))
            {
                throw;
            }

            variable.Value = saved[0];
            return result;
        }

        /// <summary>Gives T when no form is nil, nil at the first that is, and not the value of the last, as AutoCAD does.</summary>
        private object And(object args)
        {
            for (object cell = args; cell is LispCons c; cell = c.Cdr)
            {
                if (Eval(c.Car) == null)
                {
                    return null;
                }
            }

            return _true;
        }

        /// <summary>Gives T at the first form that is not nil, nil when there is none.</summary>
        private object Or(object args)
        {
            for (object cell = args; cell is LispCons c; cell = c.Cdr)
            {
                if (Eval(c.Car) != null)
                {
                    return _true;
                }
            }

            return null;
        }

        private object Progn(object args) => Body(args);
    }
}
