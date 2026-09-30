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

        /// <summary>
        /// Kept for the life of the window, so that each run takes out what the last one drew.
        /// </summary>
        private readonly PlaneSection planeSection = new PlaneSection();

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

        /// <summary>
        /// Cuts the selected parts and IFC objects by a plane through three picked points and draws the sections:
        /// <see cref="PlaneSection"/>.
        /// </summary>
        private void sectionButton_Click(object sender, EventArgs e)
        {
            planeSection.Run();
        }

        /// <summary>
        /// Takes the sections drawn out of the model as the window closes, leaving the model as it was found.
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (planeSection.RemoveDrawn() > 0)
                {
                    model.CommitChanges();
                }
            }
            catch (Exception)
            {
                // Tekla Structures went first, and took the model with it.
            }

            base.OnFormClosed(e);
        }
    }
}
