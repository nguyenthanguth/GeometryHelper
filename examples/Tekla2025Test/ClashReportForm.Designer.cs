namespace Tekla2025Test
{
    partial class ClashReportForm
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle6 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle7 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.summaryTextBox = new System.Windows.Forms.TextBox();
            this.clashGrid = new System.Windows.Forms.DataGridView();
            this.numberColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.kindColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rebarColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ifcTypeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ifcNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ifcTagColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.globalIdColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.volumeColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.depthColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.lengthInsideColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.contactColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.distanceColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.locationColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.errorColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.showAllButton = new System.Windows.Forms.Button();
            this.removeButton = new System.Windows.Forms.Button();
            this.statusLabel = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.clashGrid)).BeginInit();
            this.buttonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.summaryTextBox);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.clashGrid);
            this.splitContainer.Size = new System.Drawing.Size(1084, 526);
            this.splitContainer.SplitterDistance = 170;
            this.splitContainer.TabIndex = 0;
            // 
            // summaryTextBox
            // 
            this.summaryTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.summaryTextBox.Font = new System.Drawing.Font("Consolas", 9F);
            this.summaryTextBox.Location = new System.Drawing.Point(0, 0);
            this.summaryTextBox.Multiline = true;
            this.summaryTextBox.Name = "summaryTextBox";
            this.summaryTextBox.ReadOnly = true;
            this.summaryTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.summaryTextBox.Size = new System.Drawing.Size(1084, 170);
            this.summaryTextBox.TabIndex = 0;
            this.summaryTextBox.WordWrap = false;
            // 
            // clashGrid
            // 
            this.clashGrid.AllowUserToAddRows = false;
            this.clashGrid.AllowUserToDeleteRows = false;
            this.clashGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.clashGrid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.numberColumn,
            this.kindColumn,
            this.rebarColumn,
            this.ifcTypeColumn,
            this.ifcNameColumn,
            this.ifcTagColumn,
            this.globalIdColumn,
            this.volumeColumn,
            this.depthColumn,
            this.lengthInsideColumn,
            this.contactColumn,
            this.distanceColumn,
            this.locationColumn,
            this.errorColumn});
            this.clashGrid.Dock = System.Windows.Forms.DockStyle.Fill;
            this.clashGrid.Location = new System.Drawing.Point(0, 0);
            this.clashGrid.Name = "clashGrid";
            this.clashGrid.ReadOnly = true;
            this.clashGrid.RowHeadersVisible = false;
            this.clashGrid.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.clashGrid.Size = new System.Drawing.Size(1084, 352);
            this.clashGrid.TabIndex = 0;
            this.clashGrid.CellDoubleClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.clashGrid_CellDoubleClick);
            // 
            // numberColumn
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.numberColumn.DefaultCellStyle = dataGridViewCellStyle1;
            this.numberColumn.HeaderText = "#";
            this.numberColumn.Name = "numberColumn";
            this.numberColumn.ReadOnly = true;
            // 
            // kindColumn
            // 
            this.kindColumn.HeaderText = "Kind";
            this.kindColumn.Name = "kindColumn";
            this.kindColumn.ReadOnly = true;
            // 
            // rebarColumn
            // 
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            this.rebarColumn.DefaultCellStyle = dataGridViewCellStyle2;
            this.rebarColumn.HeaderText = "Rebar ID";
            this.rebarColumn.Name = "rebarColumn";
            this.rebarColumn.ReadOnly = true;
            // 
            // ifcTypeColumn
            // 
            this.ifcTypeColumn.HeaderText = "IFC type";
            this.ifcTypeColumn.Name = "ifcTypeColumn";
            this.ifcTypeColumn.ReadOnly = true;
            // 
            // ifcNameColumn
            // 
            this.ifcNameColumn.HeaderText = "IFC name";
            this.ifcNameColumn.Name = "ifcNameColumn";
            this.ifcNameColumn.ReadOnly = true;
            // 
            // ifcTagColumn
            // 
            this.ifcTagColumn.HeaderText = "IFC tag";
            this.ifcTagColumn.Name = "ifcTagColumn";
            this.ifcTagColumn.ReadOnly = true;
            // 
            // globalIdColumn
            // 
            this.globalIdColumn.HeaderText = "GlobalId";
            this.globalIdColumn.Name = "globalIdColumn";
            this.globalIdColumn.ReadOnly = true;
            // 
            // volumeColumn
            // 
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle3.Format = "0";
            this.volumeColumn.DefaultCellStyle = dataGridViewCellStyle3;
            this.volumeColumn.HeaderText = "Volume (mm3)";
            this.volumeColumn.Name = "volumeColumn";
            this.volumeColumn.ReadOnly = true;
            // 
            // depthColumn
            // 
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle4.Format = "0.##";
            this.depthColumn.DefaultCellStyle = dataGridViewCellStyle4;
            this.depthColumn.HeaderText = "Depth (mm)";
            this.depthColumn.Name = "depthColumn";
            this.depthColumn.ReadOnly = true;
            // 
            // lengthInsideColumn
            // 
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle5.Format = "0.#";
            this.lengthInsideColumn.DefaultCellStyle = dataGridViewCellStyle5;
            this.lengthInsideColumn.HeaderText = "Length inside (mm)";
            this.lengthInsideColumn.Name = "lengthInsideColumn";
            this.lengthInsideColumn.ReadOnly = true;
            // 
            // contactColumn
            // 
            dataGridViewCellStyle6.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle6.Format = "0";
            this.contactColumn.DefaultCellStyle = dataGridViewCellStyle6;
            this.contactColumn.HeaderText = "Contact (mm2)";
            this.contactColumn.Name = "contactColumn";
            this.contactColumn.ReadOnly = true;
            // 
            // distanceColumn
            // 
            dataGridViewCellStyle7.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleRight;
            dataGridViewCellStyle7.Format = "0.##";
            this.distanceColumn.DefaultCellStyle = dataGridViewCellStyle7;
            this.distanceColumn.HeaderText = "Gap (mm)";
            this.distanceColumn.Name = "distanceColumn";
            this.distanceColumn.ReadOnly = true;
            // 
            // locationColumn
            // 
            this.locationColumn.HeaderText = "Location (x, y, z)";
            this.locationColumn.Name = "locationColumn";
            this.locationColumn.ReadOnly = true;
            // 
            // errorColumn
            // 
            this.errorColumn.HeaderText = "Error";
            this.errorColumn.Name = "errorColumn";
            this.errorColumn.ReadOnly = true;
            // 
            // buttonPanel
            // 
            this.buttonPanel.AutoSize = true;
            this.buttonPanel.Controls.Add(this.showAllButton);
            this.buttonPanel.Controls.Add(this.removeButton);
            this.buttonPanel.Controls.Add(this.statusLabel);
            this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.buttonPanel.Location = new System.Drawing.Point(0, 526);
            this.buttonPanel.Name = "buttonPanel";
            this.buttonPanel.Padding = new System.Windows.Forms.Padding(3);
            this.buttonPanel.Size = new System.Drawing.Size(1084, 35);
            this.buttonPanel.TabIndex = 1;
            //
            // showAllButton
            //
            this.showAllButton.AutoSize = true;
            this.showAllButton.Location = new System.Drawing.Point(6, 6);
            this.showAllButton.Name = "showAllButton";
            this.showAllButton.Size = new System.Drawing.Size(75, 23);
            this.showAllButton.TabIndex = 0;
            this.showAllButton.Text = "Show all";
            this.showAllButton.UseVisualStyleBackColor = true;
            this.showAllButton.Click += new System.EventHandler(this.showAllButton_Click);
            //
            // removeButton
            //
            this.removeButton.AutoSize = true;
            this.removeButton.Location = new System.Drawing.Point(87, 6);
            this.removeButton.Name = "removeButton";
            this.removeButton.Size = new System.Drawing.Size(105, 23);
            this.removeButton.TabIndex = 1;
            this.removeButton.Text = "Remove from model";
            this.removeButton.UseVisualStyleBackColor = true;
            this.removeButton.Click += new System.EventHandler(this.removeButton_Click);
            // 
            // statusLabel
            // 
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(207, 11);
            this.statusLabel.Margin = new System.Windows.Forms.Padding(12, 8, 3, 0);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 13);
            this.statusLabel.TabIndex = 2;
            // 
            // ClashReportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1084, 561);
            this.Controls.Add(this.splitContainer);
            this.Controls.Add(this.buttonPanel);
            this.MinimumSize = new System.Drawing.Size(640, 360);
            this.Name = "ClashReportForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Clashes";
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel1.PerformLayout();
            this.splitContainer.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.clashGrid)).EndInit();
            this.buttonPanel.ResumeLayout(false);
            this.buttonPanel.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer;
        private System.Windows.Forms.TextBox summaryTextBox;
        private System.Windows.Forms.DataGridView clashGrid;
        private System.Windows.Forms.DataGridViewTextBoxColumn numberColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn kindColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn rebarColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ifcTypeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ifcNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ifcTagColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn globalIdColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn volumeColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn depthColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn lengthInsideColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn contactColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn distanceColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn locationColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn errorColumn;
        private System.Windows.Forms.FlowLayoutPanel buttonPanel;
        private System.Windows.Forms.Button showAllButton;
        private System.Windows.Forms.Button removeButton;
        private System.Windows.Forms.Label statusLabel;
    }
}
