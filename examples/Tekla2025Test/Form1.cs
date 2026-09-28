using System;
using System.Windows.Forms;
using Tekla.Structures.Model;

namespace Tekla2025Test
{
    /// <summary>
    /// Tests run live on the model open in Tekla Structures, one button each. Each test lives in a file of its own,
    /// and its button only starts it.
    /// </summary>
    public partial class Form1 : Form
    {
        private readonly Model model;

        public Form1()
        {
            InitializeComponent();

            model = new Model();

            this.Text = model.GetConnectionStatus().ToString();
        }

        /// <summary>
        /// Checks the selected reinforcement against the selected IFC objects: <see cref="RebarIfcClashCheck"/>.
        /// </summary>
        private void clashCheckButton_Click(object sender, EventArgs e)
        {
            new RebarIfcClashCheck().Run();
        }

        /// <summary>
        /// Checks the selected reinforcement against the selected IFC objects by the bars' centre lines, with no body
        /// built for any bar: <see cref="RebarIfcClashBarCheck"/>.
        /// </summary>
        private void clashBarCheckButton_Click(object sender, EventArgs e)
        {
            new RebarIfcClashBarCheck().Run();
        }
    }
}
