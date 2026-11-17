using System;

namespace GeometryHelper.Cad
{
    public sealed partial class CadDocument
    {
        /// <summary>
        /// Gets whether the words of a field are drawn on a grey ground that is not plotted, as AutoCAD's FIELDDISPLAY 1 does;
        /// true until FIELDDISPLAY is set to 0.
        /// </summary>
        public bool FieldDisplay { get; internal set; } = true;

        /// <summary>
        /// Evaluates the fields of the drawing for a reason, as FIELDEVAL allows: the fields of a drawing just opened (open), about
        /// to be saved (save), regenerated (regen) or plotted (plot). A field that cannot be evaluated keeps what it showed.
        /// </summary>
        /// <param name="reason">One of the bits of FIELDEVAL: 1 open, 2 save, 4 plot, 8 eTransmit, 16 regen.</param>
        /// <returns>How many texts changed.</returns>
        internal int UpdateFields(int reason)
        {
            try
            {
                return CadFields.UpdateAll(this, reason);
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                GeometryHelperLog.Warn("CadDocument: the fields were not updated.", exception);
                return 0;
            }
        }
    }
}
