using System;
using System.Windows.Controls;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The % button beside a text box of the attribute dialogs: it asks the Field dialog for a field and puts the field, in
    /// its markers (<c>%&lt;\AcVar Date \f "dd/MM/yyyy"&gt;%</c>), where the caret is, over what is selected, as AutoCAD's Insert
    /// field does in the Default and Value boxes.
    /// </summary>
    internal static class FieldInsertion
    {
        /// <summary>Puts the field the Field dialog ends with into the box; nothing where it was cancelled.</summary>
        /// <param name="box">The text box.</param>
        /// <param name="ask">Shows the Field dialog; the field in its markers, or null.</param>
        internal static void Into(TextBox box, Func<string> ask)
        {
            string code = ask?.Invoke();

            if (code != null)
            {
                box.SelectedText = code;
                box.CaretIndex = box.SelectionStart + box.SelectionLength;
                box.SelectionLength = 0;
            }

            box.Focus();
        }
    }
}
