using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The parameters of a drawing as AutoCAD 2021's Parameters Manager works with them: the names of the dimensional
    /// constraints (d1, rad1, ang1) and the user parameters, each with an expression and the value it comes to. A parameter's
    /// expression may name others (<c>d1=2*d2</c>, <c>up1=d1*2+3</c>); changing one moves the objects its constraints hold, as
    /// AutoCAD 2021 does, and each change is one undo step. The palette and -PARAMETERS both work through these.
    /// </summary>
    public static class CadParameters
    {
        /// <summary>The parameters of the working space, in the order the drawing keeps them.</summary>
        /// <param name="document">The document.</param>
        /// <returns>The rows; empty where the space has none.</returns>
        public static IReadOnlyList<CadParameterRow> Rows(CadDocument document)
        {
            var rows = new List<CadParameterRow>();

            if (document == null || !CadConstraints.HoldsAny(document.Database))
            {
                return rows;
            }

            CadConstraints.Work work = CadConstraints.Open(document, document.WorkingSpace);

            foreach (CadParameter parameter in work.Set.Parameters)
            {
                bool angle = work.Set.Dimensional.Exists(d =>
                    d.Kind == CadDimKind.Angular
                    && string.Equals(d.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                );
                rows.Add(
                    new CadParameterRow(parameter.Name, parameter.Expression, parameter.Value, parameter.IsUser, angle)
                );
            }

            return rows;
        }

        /// <summary>Gives a parameter a new expression: the objects its constraints hold move to meet the value it comes to.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="expression">The expression: a number, or one that names other parameters.</param>
        /// <returns>What came of it; refused, with the reason, where the expression cannot be worked out or the constraints cannot be met.</returns>
        public static CadEditResult Edit(CadDocument document, string name, string expression)
        {
            CadConstraints.Work work = Begin(document, out CadEditResult refusal);

            if (work == null)
            {
                return refusal;
            }

            CadParameter parameter = work.Set.Parameter(name);

            if (parameter == null)
            {
                return CadEditResult.Refuse("There is no parameter " + name + ".");
            }

            string old = parameter.Expression;
            double oldValue = parameter.Value;
            parameter.Expression = expression ?? string.Empty;

            if (!CadConstraints.Evaluate(work.Set, out string error))
            {
                return CadEditResult.Refuse(error);
            }

            return Finish(document, work, parameter, old, oldValue);
        }

        /// <summary>Makes a user parameter.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name: letters, digits and underscores, not starting with a digit.</param>
        /// <param name="expression">The expression.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult New(CadDocument document, string name, string expression)
        {
            CadConstraints.Work work = Begin(document, out CadEditResult refusal);

            if (work == null)
            {
                return refusal;
            }

            if (!CadConstraintExpression.IsName(name))
            {
                return CadEditResult.Refuse("\"" + name + "\" is not a name a parameter can have.");
            }

            if (work.Set.Parameter(name) != null)
            {
                return CadEditResult.Refuse("There is a parameter " + name + " already.");
            }

            var parameter = new CadParameter(name, expression ?? string.Empty, 0.0, true);
            work.Set.Parameters.Add(parameter);

            if (!CadConstraints.Evaluate(work.Set, out string error))
            {
                return CadEditResult.Refuse(error);
            }

            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.SettingChanged,
                    null,
                    "Parameter " + name,
                    null,
                    parameter.Expression
                ),
            };
            return document.ApplyEdit("PARAMETERS", planned, s => CadConstraints.Write(work));
        }

        /// <summary>Renames a parameter, in the expressions that name it and the words of its dimensions too.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name of the parameter.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Rename(CadDocument document, string name, string newName)
        {
            CadConstraints.Work work = Begin(document, out CadEditResult refusal);

            if (work == null)
            {
                return refusal;
            }

            CadParameter parameter = work.Set.Parameter(name);

            if (parameter == null)
            {
                return CadEditResult.Refuse("There is no parameter " + name + ".");
            }

            if (!CadConstraintExpression.IsName(newName))
            {
                return CadEditResult.Refuse("\"" + newName + "\" is not a name a parameter can have.");
            }

            if (
                work.Set.Parameter(newName) != null
                && !string.Equals(name, newName, StringComparison.OrdinalIgnoreCase)
            )
            {
                return CadEditResult.Refuse("There is a parameter " + newName + " already.");
            }

            string was = parameter.Name;
            parameter.Name = newName;

            foreach (CadParameter other in work.Set.Parameters)
            {
                other.Expression = Renamed(other.Expression, was, newName);
            }

            foreach (CadDimConstraint dim in work.Set.Dimensional)
            {
                if (string.Equals(dim.Name, was, StringComparison.OrdinalIgnoreCase))
                {
                    dim.Name = newName;
                }
            }

            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Parameter " + was, was, newName),
            };
            return document.ApplyEdit("PARAMETERS", planned, s =>
            {
                CadConstraints.RefreshDimensions(work);
                CadConstraints.Write(work);
            });
        }

        /// <summary>Deletes a user parameter that no expression and no constraint uses.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The name.</param>
        /// <returns>What came of it.</returns>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            CadConstraints.Work work = Begin(document, out CadEditResult refusal);

            if (work == null)
            {
                return refusal;
            }

            CadParameter parameter = work.Set.Parameter(name);

            if (parameter == null)
            {
                return CadEditResult.Refuse("There is no parameter " + name + ".");
            }

            if (!parameter.IsUser)
            {
                return CadEditResult.Refuse(
                    "The parameter " + name + " belongs to a dimensional constraint; delete the constraint."
                );
            }

            foreach (CadParameter other in work.Set.Parameters)
            {
                if (
                    !ReferenceEquals(other, parameter)
                    && CadConstraintExpression
                        .NamesIn(other.Expression)
                        .Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                )
                {
                    return CadEditResult.Refuse(
                        "The parameter " + name + " is used by the expression of " + other.Name + "."
                    );
                }
            }

            work.Set.Parameters.Remove(parameter);
            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(
                    CadEditRecordKind.SettingChanged,
                    null,
                    "Parameter " + name,
                    parameter.Expression,
                    null
                ),
            };
            return document.ApplyEdit("PARAMETERS", planned, s => CadConstraints.Write(work));
        }

        /// <summary>
        /// The name of the parameter a dimension shows as a dimensional constraint (d1, rad1), as the Edit Constraint item of a
        /// dimension's menu needs it; null where the dimension is no constraint of the working space.
        /// </summary>
        internal static string ConstraintNameOf(CadDocument document, string dimensionHandle)
        {
            if (document == null || dimensionHandle == null || !CadConstraints.HoldsAny(document.Database))
            {
                return null;
            }

            CadConstraints.Work work = CadConstraints.Open(document, document.WorkingSpace);
            return work
                .Set.Dimensional.Find(d =>
                    string.Equals(d.DimensionHandle, dimensionHandle, StringComparison.OrdinalIgnoreCase)
                )
                ?.Name;
        }

        /// <summary>
        /// Gives the dimensions that show dimensional constraints the words of a name format (0 the name, 1 the value, 2 the
        /// name and the value, as CONSTRAINTNAMEFORMAT counts), one undo step; a dimension that is no constraint is left as it
        /// is. Refused where none of them is a constraint.
        /// </summary>
        internal static CadEditResult SetNameFormat(
            CadDocument document,
            IReadOnlyList<string> dimensionHandles,
            int format
        )
        {
            CadConstraints.Work work = Begin(document, out CadEditResult refusal);

            if (work == null)
            {
                return refusal;
            }

            var chosen = work
                .Set.Dimensional.Where(d =>
                    d.DimensionHandle != null
                    && d.NameFormat != format
                    && dimensionHandles.Contains(d.DimensionHandle, StringComparer.OrdinalIgnoreCase)
                )
                .ToList();

            if (chosen.Count == 0)
            {
                return CadEditResult.Refuse("No dimensional constraint to change.");
            }

            var planned = chosen
                .Select(d => new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    d.DimensionHandle,
                    CadConstraints.OwnProperty
                ))
                .ToList();
            return document.ApplyEdit("DIMCONSTRAINT", planned, s =>
            {
                foreach (CadDimConstraint constraint in chosen)
                {
                    constraint.NameFormat = format;

                    if (work.Entity(constraint.DimensionHandle) is DxfDimension dimension)
                    {
                        CadCommands.RefreshConstraintDimension(work, constraint, dimension);
                    }
                }

                CadConstraints.Write(work);
            });
        }

        /// <summary>The expression with each use of a name written as another.</summary>
        private static string Renamed(string expression, string from, string to)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return expression;
            }

            var text = new System.Text.StringBuilder();
            int i = 0;

            while (i < expression.Length)
            {
                if (char.IsLetter(expression[i]) || expression[i] == '_')
                {
                    int start = i;

                    while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                    {
                        i++;
                    }

                    string word = expression.Substring(start, i - start);
                    text.Append(string.Equals(word, from, StringComparison.OrdinalIgnoreCase) ? to : word);
                }
                else
                {
                    text.Append(expression[i]);
                    i++;
                }
            }

            return text.ToString();
        }

        private static CadConstraints.Work Begin(CadDocument document, out CadEditResult refusal)
        {
            refusal = null;

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!CadConstraints.CanHold(document.Database))
            {
                refusal = CadEditResult.Refuse(CadConstraints.CannotHold);
                return null;
            }

            CadConstraints.Work work = CadConstraints.Open(document, document.WorkingSpace);

            if (work.Set.Refusal != null)
            {
                refusal = CadEditResult.Refuse(work.Set.Refusal);
                return null;
            }

            return work;
        }

        /// <summary>Solves the constraints at the values the parameters come to now and writes the result.</summary>
        private static CadEditResult Finish(
            CadDocument document,
            CadConstraints.Work work,
            CadParameter changed,
            string oldExpression,
            double oldValue
        )
        {
            string anchor = null;

            foreach (CadDimConstraint dim in work.Set.Dimensional)
            {
                if (anchor == null && dim.Refs.Count > 0)
                {
                    anchor = dim.Refs[0].Handle;
                }
            }

            CadSolveResult result = work.Set.Dimensional.Count == 0 ? null : CadConstraints.Solve(work, null, anchor);

            if (result != null && !result.Succeeded)
            {
                return CadEditResult.Refuse(result.Message ?? CadConstraints.CannotSatisfy);
            }

            var planned = new List<CadEditRecord>();

            if (result != null)
            {
                planned.AddRange(CadConstraints.Moves(work, result));
            }

            planned.Add(
                new CadEditRecord(
                    CadEditRecordKind.SettingChanged,
                    null,
                    "Parameter " + changed.Name,
                    oldExpression,
                    changed.Expression
                )
            );
            return document.ApplyEdit("PARAMETERS", planned, s => CadConstraints.Commit(work, result));
        }
    }
}
