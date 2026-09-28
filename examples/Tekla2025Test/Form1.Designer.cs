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
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(492, 257);
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
    }
}

