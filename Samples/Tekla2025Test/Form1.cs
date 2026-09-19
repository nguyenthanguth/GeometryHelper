using GeometryHelper.CommonGeometry;
using GeometryHelper.IfcConvert.Core;
using GeometryHelper.SolidGeometry.Geometry;
using GeometryHelper.TeklaConvert;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using GeometryHelper.CommonGeometry.Enums;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;

namespace Tekla2025Test
{
    public partial class Form1 : Form
    {
        private readonly Model model;
        public Form1()
        {
            InitializeComponent();

            model = new Model();
            this.Text = model.GetConnectionStatus().ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                // The IFC objects selected in the model view (Tekla.Structures.Model has a ModelObjectSelector too, hence the full name).
                var selected = new List<ReferenceModelObject>();
                ModelObjectEnumerator selection = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
                while (selection.MoveNext())
                {
                    if (selection.Current is ReferenceModelObject referenceObject)
                    {
                        selected.Add(referenceObject);
                    }
                    else if (selection.Current is ReferenceModel referenceModel)
                    {
                        Stopwatch stopwatch = Stopwatch.StartNew();

                        var ops = new IfcConvertOptions().AddOnlyNames("*BEAM*");
                        var t1 = referenceModel.ToGeoSolids(ops);
                        var ets = stopwatch.Elapsed;
                        MessageBox.Show(ets.Seconds.ToString(), t1.Length.ToString());
                    }
                }

                // In the current work plane, which is where control polycurves are drawn too: no work plane to switch.
                var options = new IfcConvertOptions().AddOnlyNames("*BEAM*", "*COL*").AddSkipNames("SKIP1, SKIP2", "RIN*");
                GeoSolid3[] solids = selected.ToGeoSolids(options);

                // Every face as control polycurves (GeometryDraw): outer boundaries red, holes white.
                ControlPolycurve[] drawn = solids.DrawToTekla();

                Operation.DisplayPrompt($"{solids.Length} solid(s), {drawn.Length} polycurve(s), from {selected.Count} reference object(s).");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                model.CommitChanges();
            }
        }
    }
}
