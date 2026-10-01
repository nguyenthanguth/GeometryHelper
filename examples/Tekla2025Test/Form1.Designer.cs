namespace Tekla2025Test
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.clashCheckButton = new System.Windows.Forms.Button();
            this.clashBarCheckButton = new System.Windows.Forms.Button();
            this.sectionButton = new System.Windows.Forms.Button();
            this.drawBodiesButton = new System.Windows.Forms.Button();
            this.drawTrianglesButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // clashCheckButton
            // 
            this.clashCheckButton.AutoSize = true;
            this.clashCheckButton.Location = new System.Drawing.Point(167, 104);
            this.clashCheckButton.Name = "clashCheckButton";
            this.clashCheckButton.Size = new System.Drawing.Size(125, 39);
            this.clashCheckButton.TabIndex = 0;
            this.clashCheckButton.Text = "Clash check: selected rebar vs IFC";
            this.clashCheckButton.UseVisualStyleBackColor = true;
            this.clashCheckButton.Click += new System.EventHandler(this.clashCheckButton_Click);
            // 
            // clashBarCheckButton
            // 
            this.clashBarCheckButton.AutoSize = true;
            this.clashBarCheckButton.Location = new System.Drawing.Point(167, 155);
            this.clashBarCheckButton.Name = "clashBarCheckButton";
            this.clashBarCheckButton.Size = new System.Drawing.Size(125, 39);
            this.clashBarCheckButton.TabIndex = 1;
            this.clashBarCheckButton.Text = "Clash check by centre line: selected rebar vs IFC";
            this.clashBarCheckButton.UseVisualStyleBackColor = true;
            this.clashBarCheckButton.Click += new System.EventHandler(this.clashBarCheckButton_Click);
            //
            // sectionButton
            //
            this.sectionButton.AutoSize = true;
            this.sectionButton.Location = new System.Drawing.Point(167, 206);
            this.sectionButton.Name = "sectionButton";
            this.sectionButton.Size = new System.Drawing.Size(125, 39);
            this.sectionButton.TabIndex = 2;
            this.sectionButton.Text = "Section by 3 points: selected parts and IFC";
            this.sectionButton.UseVisualStyleBackColor = true;
            this.sectionButton.Click += new System.EventHandler(this.sectionButton_Click);
            //
            // drawBodiesButton
            //
            this.drawBodiesButton.AutoSize = true;
            this.drawBodiesButton.Location = new System.Drawing.Point(167, 257);
            this.drawBodiesButton.Name = "drawBodiesButton";
            this.drawBodiesButton.Size = new System.Drawing.Size(125, 39);
            this.drawBodiesButton.TabIndex = 3;
            this.drawBodiesButton.Text = "Draw selected as GeoSolid3";
            this.drawBodiesButton.UseVisualStyleBackColor = true;
            this.drawBodiesButton.Click += new System.EventHandler(this.drawBodiesButton_Click);
            //
            // drawTrianglesButton
            //
            this.drawTrianglesButton.AutoSize = true;
            this.drawTrianglesButton.Location = new System.Drawing.Point(167, 308);
            this.drawTrianglesButton.Name = "drawTrianglesButton";
            this.drawTrianglesButton.Size = new System.Drawing.Size(125, 39);
            this.drawTrianglesButton.TabIndex = 4;
            this.drawTrianglesButton.Text = "Draw selected as GeoSolid3 triangles";
            this.drawTrianglesButton.UseVisualStyleBackColor = true;
            this.drawTrianglesButton.Click += new System.EventHandler(this.drawTrianglesButton_Click);
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(492, 410);
            this.Controls.Add(this.drawTrianglesButton);
            this.Controls.Add(this.drawBodiesButton);
            this.Controls.Add(this.sectionButton);
            this.Controls.Add(this.clashBarCheckButton);
            this.Controls.Add(this.clashCheckButton);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button clashCheckButton;
        private System.Windows.Forms.Button clashBarCheckButton;
        private System.Windows.Forms.Button sectionButton;
        private System.Windows.Forms.Button drawBodiesButton;
        private System.Windows.Forms.Button drawTrianglesButton;
    }
}

