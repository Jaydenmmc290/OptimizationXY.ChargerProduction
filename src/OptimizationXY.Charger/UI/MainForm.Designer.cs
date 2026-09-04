namespace OptimizationXY.Charger.UI
{
    partial class MainForm
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
            groupBoxOverlay = new GroupBox();
            pictureBoxLogOverlay = new PictureBox();
            panelControls = new Panel();
            lblOptimizationMode = new Label();
            cmbOptimizationMode = new ComboBox();
            BtnStartCapture = new Button();
            BtnStopCapture = new Button();
            BtnProductionSetup = new Button();
            lblDiagnostics = new Label();
            lblMode = new Label();
            lblBlock = new Label();
            groupConcatenated = new GroupBox();
            pictureBoxConcatenated = new PictureBox();
            groupBoxDiagnostics = new GroupBox();
            txtDiagnostics = new TextBox();
            groupBoxProfileMeasurements = new GroupBox();
            lblHeadX = new Label();
            txtHeadX = new TextBox();
            lblHeadY = new Label();
            txtHeadY = new TextBox();
            lblTailX = new Label();
            txtTailX = new TextBox();
            lblTailY = new Label();
            txtTailY = new TextBox();
            lblUsableSheets = new Label();
            txtUsableSheets = new TextBox();
            lblUsableVeneerLength = new Label();
            txtUsableVeneerLength = new TextBox();
            groupBoxOverlay.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogOverlay).BeginInit();
            panelControls.SuspendLayout();
            groupConcatenated.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxConcatenated).BeginInit();
            groupBoxDiagnostics.SuspendLayout();
            groupBoxProfileMeasurements.SuspendLayout();
            SuspendLayout();
            // 
            // groupBoxOverlay
            // 
            groupBoxOverlay.Controls.Add(pictureBoxLogOverlay);
            groupBoxOverlay.Location = new Point(192, 405);
            groupBoxOverlay.Name = "groupBoxOverlay";
            groupBoxOverlay.Size = new Size(254, 262);
            groupBoxOverlay.TabIndex = 35;
            groupBoxOverlay.TabStop = false;
            groupBoxOverlay.Text = "Log Scan Overlay";
            // 
            // pictureBoxLogOverlay
            // 
            pictureBoxLogOverlay.BorderStyle = BorderStyle.FixedSingle;
            pictureBoxLogOverlay.Dock = DockStyle.Fill;
            pictureBoxLogOverlay.Location = new Point(3, 19);
            pictureBoxLogOverlay.Name = "pictureBoxLogOverlay";
            pictureBoxLogOverlay.Size = new Size(248, 240);
            pictureBoxLogOverlay.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxLogOverlay.TabIndex = 3;
            pictureBoxLogOverlay.TabStop = false;
            // 
            // panelControls
            // 
            panelControls.Controls.Add(lblOptimizationMode);
            panelControls.Controls.Add(cmbOptimizationMode);
            panelControls.Controls.Add(BtnStartCapture);
            panelControls.Controls.Add(BtnStopCapture);
            panelControls.Controls.Add(BtnProductionSetup);
            panelControls.Controls.Add(lblDiagnostics);
            panelControls.Controls.Add(lblMode);
            panelControls.Controls.Add(lblBlock);
            panelControls.Location = new Point(15, 278);
            panelControls.Name = "panelControls";
            panelControls.Size = new Size(752, 119);
            panelControls.TabIndex = 39;
            // 
            // lblOptimizationMode
            // 
            lblOptimizationMode.AutoSize = true;
            lblOptimizationMode.Location = new Point(245, 5);
            lblOptimizationMode.Name = "lblOptimizationMode";
            lblOptimizationMode.Size = new Size(110, 15);
            lblOptimizationMode.TabIndex = 12;
            lblOptimizationMode.Text = "Optimization Mode";
            // 
            // cmbOptimizationMode
            // 
            cmbOptimizationMode.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOptimizationMode.FormattingEnabled = true;
            cmbOptimizationMode.Location = new Point(245, 24);
            cmbOptimizationMode.Name = "cmbOptimizationMode";
            cmbOptimizationMode.Size = new Size(300, 23);
            cmbOptimizationMode.TabIndex = 11;
            // 
            // BtnStartCapture
            // 
            BtnStartCapture.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BtnStartCapture.Enabled = false;
            BtnStartCapture.Location = new Point(20, 3);
            BtnStartCapture.Name = "BtnStartCapture";
            BtnStartCapture.Size = new Size(105, 35);
            BtnStartCapture.TabIndex = 9;
            BtnStartCapture.Text = "Start Block";
            BtnStartCapture.UseVisualStyleBackColor = true;
            BtnStartCapture.Click += BtnStartCapture_Click;
            // 
            // BtnStopCapture
            // 
            BtnStopCapture.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            BtnStopCapture.Enabled = false;
            BtnStopCapture.Location = new Point(131, 3);
            BtnStopCapture.Name = "BtnStopCapture";
            BtnStopCapture.Size = new Size(105, 35);
            BtnStopCapture.TabIndex = 10;
            BtnStopCapture.Text = "Stop Block";
            BtnStopCapture.UseVisualStyleBackColor = true;
            BtnStopCapture.Click += BtnStopCapture_Click;
            // 
            // BtnProductionSetup
            // 
            BtnProductionSetup.Location = new Point(618, 3);
            BtnProductionSetup.Name = "BtnProductionSetup";
            BtnProductionSetup.Size = new Size(123, 44);
            BtnProductionSetup.TabIndex = 13;
            BtnProductionSetup.Text = "Setup / Calibration";
            BtnProductionSetup.UseVisualStyleBackColor = true;
            BtnProductionSetup.Click += BtnProductionSetup_Click;
            // 
            // lblDiagnostics
            // 
            lblDiagnostics.AutoSize = true;
            lblDiagnostics.Location = new Point(12, 86);
            lblDiagnostics.Name = "lblDiagnostics";
            lblDiagnostics.Size = new Size(198, 15);
            lblDiagnostics.TabIndex = 8;
            lblDiagnostics.Text = "Head Rx: 0 Drop: 0 | Tail Rx: 0 Drop: 0";
            // 
            // lblMode
            // 
            lblMode.AutoSize = true;
            lblMode.Location = new Point(12, 61);
            lblMode.Name = "lblMode";
            lblMode.Size = new Size(85, 15);
            lblMode.TabIndex = 7;
            lblMode.Text = "Mode: Starting";
            // 
            // lblBlock
            // 
            lblBlock.AutoSize = true;
            lblBlock.Location = new Point(230, 61);
            lblBlock.Name = "lblBlock";
            lblBlock.Size = new Size(71, 15);
            lblBlock.TabIndex = 6;
            lblBlock.Text = "Block: None";
            // 
            // groupConcatenated
            // 
            groupConcatenated.Controls.Add(pictureBoxConcatenated);
            groupConcatenated.Location = new Point(18, 6);
            groupConcatenated.Name = "groupConcatenated";
            groupConcatenated.Size = new Size(749, 260);
            groupConcatenated.TabIndex = 36;
            groupConcatenated.TabStop = false;
            groupConcatenated.Text = "LIVE | HEAD + TAIL CONCATENATED FOOTAGE";
            // 
            // pictureBoxConcatenated
            // 
            pictureBoxConcatenated.BackColor = Color.Black;
            pictureBoxConcatenated.Dock = DockStyle.Fill;
            pictureBoxConcatenated.Location = new Point(3, 19);
            pictureBoxConcatenated.Name = "pictureBoxConcatenated";
            pictureBoxConcatenated.Size = new Size(743, 238);
            pictureBoxConcatenated.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxConcatenated.TabIndex = 0;
            pictureBoxConcatenated.TabStop = false;
            // 
            // groupBoxDiagnostics
            // 
            groupBoxDiagnostics.Controls.Add(txtDiagnostics);
            groupBoxDiagnostics.Location = new Point(454, 405);
            groupBoxDiagnostics.Name = "groupBoxDiagnostics";
            groupBoxDiagnostics.Size = new Size(316, 262);
            groupBoxDiagnostics.TabIndex = 37;
            groupBoxDiagnostics.TabStop = false;
            groupBoxDiagnostics.Text = "Diagnostics";
            // 
            // txtDiagnostics
            // 
            txtDiagnostics.Dock = DockStyle.Fill;
            txtDiagnostics.Location = new Point(3, 19);
            txtDiagnostics.Multiline = true;
            txtDiagnostics.Name = "txtDiagnostics";
            txtDiagnostics.ReadOnly = true;
            txtDiagnostics.ScrollBars = ScrollBars.Vertical;
            txtDiagnostics.Size = new Size(310, 240);
            txtDiagnostics.TabIndex = 0;
            // 
            // groupBoxProfileMeasurements
            // 
            groupBoxProfileMeasurements.Controls.Add(lblHeadX);
            groupBoxProfileMeasurements.Controls.Add(txtHeadX);
            groupBoxProfileMeasurements.Controls.Add(lblHeadY);
            groupBoxProfileMeasurements.Controls.Add(txtHeadY);
            groupBoxProfileMeasurements.Controls.Add(lblTailX);
            groupBoxProfileMeasurements.Controls.Add(txtTailX);
            groupBoxProfileMeasurements.Controls.Add(lblTailY);
            groupBoxProfileMeasurements.Controls.Add(txtTailY);
            groupBoxProfileMeasurements.Controls.Add(lblUsableSheets);
            groupBoxProfileMeasurements.Controls.Add(txtUsableSheets);
            groupBoxProfileMeasurements.Controls.Add(lblUsableVeneerLength);
            groupBoxProfileMeasurements.Controls.Add(txtUsableVeneerLength);
            groupBoxProfileMeasurements.Location = new Point(15, 405);
            groupBoxProfileMeasurements.Name = "groupBoxProfileMeasurements";
            groupBoxProfileMeasurements.Size = new Size(161, 255);
            groupBoxProfileMeasurements.TabIndex = 40;
            groupBoxProfileMeasurements.TabStop = false;
            groupBoxProfileMeasurements.Text = "Spindle Output";
            // 
            // lblHeadX
            // 
            lblHeadX.AutoSize = true;
            lblHeadX.Location = new Point(6, 22);
            lblHeadX.Name = "lblHeadX";
            lblHeadX.Size = new Size(45, 15);
            lblHeadX.TabIndex = 27;
            lblHeadX.Text = "Head X";
            // 
            // txtHeadX
            // 
            txtHeadX.Location = new Point(65, 19);
            txtHeadX.Name = "txtHeadX";
            txtHeadX.ReadOnly = true;
            txtHeadX.Size = new Size(80, 23);
            txtHeadX.TabIndex = 20;
            // 
            // lblHeadY
            // 
            lblHeadY.AutoSize = true;
            lblHeadY.Location = new Point(6, 55);
            lblHeadY.Name = "lblHeadY";
            lblHeadY.Size = new Size(45, 15);
            lblHeadY.TabIndex = 26;
            lblHeadY.Text = "Head Y";
            // 
            // txtHeadY
            // 
            txtHeadY.Location = new Point(65, 52);
            txtHeadY.Name = "txtHeadY";
            txtHeadY.ReadOnly = true;
            txtHeadY.Size = new Size(80, 23);
            txtHeadY.TabIndex = 25;
            // 
            // lblTailX
            // 
            lblTailX.AutoSize = true;
            lblTailX.Location = new Point(6, 84);
            lblTailX.Name = "lblTailX";
            lblTailX.Size = new Size(35, 15);
            lblTailX.TabIndex = 24;
            lblTailX.Text = "Tail X";
            // 
            // txtTailX
            // 
            txtTailX.Location = new Point(65, 81);
            txtTailX.Name = "txtTailX";
            txtTailX.ReadOnly = true;
            txtTailX.Size = new Size(80, 23);
            txtTailX.TabIndex = 23;
            // 
            // lblTailY
            // 
            lblTailY.AutoSize = true;
            lblTailY.Location = new Point(6, 113);
            lblTailY.Name = "lblTailY";
            lblTailY.Size = new Size(35, 15);
            lblTailY.TabIndex = 22;
            lblTailY.Text = "Tail Y";
            // 
            // txtTailY
            // 
            txtTailY.Location = new Point(65, 110);
            txtTailY.Name = "txtTailY";
            txtTailY.ReadOnly = true;
            txtTailY.Size = new Size(80, 23);
            txtTailY.TabIndex = 21;
            // 
            // lblUsableSheets
            // 
            lblUsableSheets.AutoSize = true;
            lblUsableSheets.Location = new Point(6, 202);
            lblUsableSheets.Name = "lblUsableSheets";
            lblUsableSheets.Size = new Size(79, 15);
            lblUsableSheets.TabIndex = 19;
            lblUsableSheets.Text = "Usable Sheets";
            // 
            // txtUsableSheets
            // 
            txtUsableSheets.Location = new Point(7, 220);
            txtUsableSheets.Name = "txtUsableSheets";
            txtUsableSheets.ReadOnly = true;
            txtUsableSheets.Size = new Size(80, 23);
            txtUsableSheets.TabIndex = 18;
            // 
            // lblUsableVeneerLength
            // 
            lblUsableVeneerLength.AutoSize = true;
            lblUsableVeneerLength.Location = new Point(6, 148);
            lblUsableVeneerLength.Name = "lblUsableVeneerLength";
            lblUsableVeneerLength.Size = new Size(153, 15);
            lblUsableVeneerLength.TabIndex = 17;
            lblUsableVeneerLength.Text = "Usable Veneer Length (mm)";
            // 
            // txtUsableVeneerLength
            // 
            txtUsableVeneerLength.Location = new Point(6, 166);
            txtUsableVeneerLength.Name = "txtUsableVeneerLength";
            txtUsableVeneerLength.ReadOnly = true;
            txtUsableVeneerLength.Size = new Size(92, 23);
            txtUsableVeneerLength.TabIndex = 16;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(773, 672);
            Controls.Add(groupBoxProfileMeasurements);
            Controls.Add(panelControls);
            Controls.Add(groupBoxDiagnostics);
            Controls.Add(groupConcatenated);
            Controls.Add(groupBoxOverlay);
            Name = "MainForm";
            Text = "Optimization XY";
            Load += MainForm_Load;
            groupBoxOverlay.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBoxLogOverlay).EndInit();
            panelControls.ResumeLayout(false);
            panelControls.PerformLayout();
            groupConcatenated.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBoxConcatenated).EndInit();
            groupBoxDiagnostics.ResumeLayout(false);
            groupBoxDiagnostics.PerformLayout();
            groupBoxProfileMeasurements.ResumeLayout(false);
            groupBoxProfileMeasurements.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBoxOverlay;
        private PictureBox pictureBoxLogOverlay;
        private GroupBox groupConcatenated;
        private PictureBox pictureBoxConcatenated;
        private GroupBox groupBoxDiagnostics;
        private TextBox txtDiagnostics;
        private Panel panelControls;
        private Label lblDiagnostics;
        private Label lblMode;
        private Label lblBlock;

        private Label lblOptimizationMode;
        private ComboBox cmbOptimizationMode;
        private Button BtnStartCapture;
        private Button BtnStopCapture;
        private Button BtnProductionSetup;
        private GroupBox groupBoxProfileMeasurements;
        private Label lblHeadX;
        private Label lblHeadY;
        private Label lblTailX;
        private Label lblTailY;
        private Label lblUsableSheets;
        private Label lblUsableVeneerLength;
        private TextBox txtHeadX;
        private TextBox txtHeadY;
        private TextBox txtTailX;
        private TextBox txtTailY;
        private TextBox txtUsableSheets;
        private TextBox txtUsableVeneerLength;
    }
}
