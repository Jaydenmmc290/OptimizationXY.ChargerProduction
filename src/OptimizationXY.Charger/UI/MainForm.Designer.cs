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
            lblPointCount = new Label();
            lblMeasuredLengthInches = new Label();
            txtMeasuredLengthInches = new TextBox();
            lblMaxDiameterIn = new Label();
            lblMinDiameterIn = new Label();
            txtPointCount = new TextBox();
            lblAvgDiameterIn = new Label();
            lblAvgDiameterMm = new Label();
            txtAvgDiameterMM = new TextBox();
            txtAvgDiameterInches = new TextBox();
            txtMinDiameterInches = new TextBox();
            txtMaxDiameterInches = new TextBox();
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
            groupBoxOverlay.Location = new Point(15, 287);
            groupBoxOverlay.Name = "groupBoxOverlay";
            groupBoxOverlay.Size = new Size(255, 253);
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
            pictureBoxLogOverlay.Size = new Size(249, 231);
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
            panelControls.Controls.Add(lblDiagnostics);
            panelControls.Controls.Add(lblMode);
            panelControls.Controls.Add(lblBlock);
            panelControls.Location = new Point(276, 287);
            panelControls.Name = "panelControls";
            panelControls.Size = new Size(634, 119);
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
            BtnStartCapture.Location = new Point(12, 3);
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
            BtnStopCapture.Location = new Point(123, 3);
            BtnStopCapture.Name = "BtnStopCapture";
            BtnStopCapture.Size = new Size(105, 35);
            BtnStopCapture.TabIndex = 10;
            BtnStopCapture.Text = "Stop Block";
            BtnStopCapture.UseVisualStyleBackColor = true;
            BtnStopCapture.Click += BtnStopCapture_Click;
            // 
            // BtnProductionSetup
            // 
            BtnProductionSetup.Location = new Point(916, 362);
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
            groupConcatenated.Location = new Point(12, 12);
            groupConcatenated.Name = "groupConcatenated";
            groupConcatenated.Size = new Size(1014, 260);
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
            pictureBoxConcatenated.Size = new Size(1008, 238);
            pictureBoxConcatenated.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBoxConcatenated.TabIndex = 0;
            pictureBoxConcatenated.TabStop = false;
            // 
            // groupBoxDiagnostics
            // 
            groupBoxDiagnostics.Controls.Add(txtDiagnostics);
            groupBoxDiagnostics.Location = new Point(273, 412);
            groupBoxDiagnostics.Name = "groupBoxDiagnostics";
            groupBoxDiagnostics.Size = new Size(329, 147);
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
            txtDiagnostics.Size = new Size(323, 125);
            txtDiagnostics.TabIndex = 0;
            // 
            // groupBoxProfileMeasurements
            // 
            groupBoxProfileMeasurements.Controls.Add(lblPointCount);
            groupBoxProfileMeasurements.Controls.Add(lblMeasuredLengthInches);
            groupBoxProfileMeasurements.Controls.Add(txtMeasuredLengthInches);
            groupBoxProfileMeasurements.Controls.Add(lblMaxDiameterIn);
            groupBoxProfileMeasurements.Controls.Add(lblMinDiameterIn);
            groupBoxProfileMeasurements.Controls.Add(txtPointCount);
            groupBoxProfileMeasurements.Controls.Add(lblAvgDiameterIn);
            groupBoxProfileMeasurements.Controls.Add(lblAvgDiameterMm);
            groupBoxProfileMeasurements.Controls.Add(txtAvgDiameterMM);
            groupBoxProfileMeasurements.Controls.Add(txtAvgDiameterInches);
            groupBoxProfileMeasurements.Controls.Add(txtMinDiameterInches);
            groupBoxProfileMeasurements.Controls.Add(txtMaxDiameterInches);
            groupBoxProfileMeasurements.Location = new Point(608, 412);
            groupBoxProfileMeasurements.Name = "groupBoxProfileMeasurements";
            groupBoxProfileMeasurements.Size = new Size(423, 147);
            groupBoxProfileMeasurements.TabIndex = 40;
            groupBoxProfileMeasurements.TabStop = false;
            groupBoxProfileMeasurements.Text = "Profile Measurements";
            // 
            // lblPointCount
            // 
            lblPointCount.AutoSize = true;
            lblPointCount.Location = new Point(108, 25);
            lblPointCount.Name = "lblPointCount";
            lblPointCount.Size = new Size(81, 15);
            lblPointCount.TabIndex = 27;
            lblPointCount.Text = "Point Count #";
            // 
            // lblMeasuredLengthInches
            // 
            lblMeasuredLengthInches.AutoSize = true;
            lblMeasuredLengthInches.Location = new Point(95, 61);
            lblMeasuredLengthInches.Name = "lblMeasuredLengthInches";
            lblMeasuredLengthInches.Size = new Size(120, 15);
            lblMeasuredLengthInches.TabIndex = 26;
            lblMeasuredLengthInches.Text = "Measured Length (in)";
            // 
            // txtMeasuredLengthInches
            // 
            txtMeasuredLengthInches.Location = new Point(9, 58);
            txtMeasuredLengthInches.Name = "txtMeasuredLengthInches";
            txtMeasuredLengthInches.Size = new Size(80, 23);
            txtMeasuredLengthInches.TabIndex = 25;
            // 
            // lblMaxDiameterIn
            // 
            lblMaxDiameterIn.AutoSize = true;
            lblMaxDiameterIn.Location = new Point(306, 22);
            lblMaxDiameterIn.Name = "lblMaxDiameterIn";
            lblMaxDiameterIn.Size = new Size(101, 15);
            lblMaxDiameterIn.TabIndex = 24;
            lblMaxDiameterIn.Text = "Max Diameter (in)";
            // 
            // lblMinDiameterIn
            // 
            lblMinDiameterIn.AutoSize = true;
            lblMinDiameterIn.Location = new Point(95, 102);
            lblMinDiameterIn.Name = "lblMinDiameterIn";
            lblMinDiameterIn.Size = new Size(100, 15);
            lblMinDiameterIn.TabIndex = 23;
            lblMinDiameterIn.Text = "Min Diameter (in)";
            // 
            // txtPointCount
            // 
            txtPointCount.Location = new Point(9, 22);
            txtPointCount.Name = "txtPointCount";
            txtPointCount.Size = new Size(93, 23);
            txtPointCount.TabIndex = 20;
            // 
            // lblAvgDiameterIn
            // 
            lblAvgDiameterIn.AutoSize = true;
            lblAvgDiameterIn.Location = new Point(306, 61);
            lblAvgDiameterIn.Name = "lblAvgDiameterIn";
            lblAvgDiameterIn.Size = new Size(100, 15);
            lblAvgDiameterIn.TabIndex = 22;
            lblAvgDiameterIn.Text = "Avg Diameter (in)";
            // 
            // lblAvgDiameterMm
            // 
            lblAvgDiameterMm.AutoSize = true;
            lblAvgDiameterMm.Location = new Point(306, 101);
            lblAvgDiameterMm.Name = "lblAvgDiameterMm";
            lblAvgDiameterMm.Size = new Size(112, 15);
            lblAvgDiameterMm.TabIndex = 21;
            lblAvgDiameterMm.Text = "Avg Diameter (mm)";
            // 
            // txtAvgDiameterMM
            // 
            txtAvgDiameterMM.Location = new Point(220, 99);
            txtAvgDiameterMM.Name = "txtAvgDiameterMM";
            txtAvgDiameterMM.Size = new Size(80, 23);
            txtAvgDiameterMM.TabIndex = 16;
            // 
            // txtAvgDiameterInches
            // 
            txtAvgDiameterInches.Location = new Point(220, 58);
            txtAvgDiameterInches.Name = "txtAvgDiameterInches";
            txtAvgDiameterInches.Size = new Size(80, 23);
            txtAvgDiameterInches.TabIndex = 17;
            // 
            // txtMinDiameterInches
            // 
            txtMinDiameterInches.Location = new Point(9, 99);
            txtMinDiameterInches.Name = "txtMinDiameterInches";
            txtMinDiameterInches.Size = new Size(80, 23);
            txtMinDiameterInches.TabIndex = 18;
            // 
            // txtMaxDiameterInches
            // 
            txtMaxDiameterInches.Location = new Point(220, 19);
            txtMaxDiameterInches.Name = "txtMaxDiameterInches";
            txtMaxDiameterInches.Size = new Size(80, 23);
            txtMaxDiameterInches.TabIndex = 19;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1050, 567);
            Controls.Add(groupBoxProfileMeasurements);
            Controls.Add(panelControls);
            Controls.Add(groupBoxDiagnostics);
            Controls.Add(groupConcatenated);
            Controls.Add(BtnProductionSetup);
            Controls.Add(groupBoxOverlay);
            Name = "MainForm";
            Text = "Optimization XY Charger";
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
        private Label lblPointCount;
        private Label lblMeasuredLengthInches;
        private TextBox txtMeasuredLengthInches;
        private Label lblMaxDiameterIn;
        private Label lblMinDiameterIn;
        private TextBox txtPointCount;
        private Label lblAvgDiameterIn;
        private Label lblAvgDiameterMm;
        private TextBox txtAvgDiameterMM;
        private TextBox txtAvgDiameterInches;
        private TextBox txtMinDiameterInches;
        private TextBox txtMaxDiameterInches;
    }
}
