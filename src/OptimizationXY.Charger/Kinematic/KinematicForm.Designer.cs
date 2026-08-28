using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;

namespace OptimizationXY.Charger.Kinematic;

partial class KinematicForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panelResults = new Panel();
            textBoxB2 = new TextBox();
            textBoxA2 = new TextBox();
            textBoxB1 = new TextBox();
            textBoxA1 = new TextBox();
            labelB2 = new Label();
            labelA2 = new Label();
            labelB1 = new Label();
            labelA1 = new Label();
            labelTailY = new Label();
            labelTailX = new Label();
            labelHeadY = new Label();
            labelHeadX = new Label();
            buttonCalculate = new Button();
            richTextBoxDiagnostics = new RichTextBox();
            pictureBoxKinematicGraph = new PictureBox();
            textBoxHeadX = new TextBox();
            textBoxTailX = new TextBox();
            textBoxTailY = new TextBox();
            textBoxHeadY = new TextBox();
            panelResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxKinematicGraph).BeginInit();
            SuspendLayout();
            // 
            // panelResults
            // 
            panelResults.BackColor = Color.LightGray;
            panelResults.Controls.Add(textBoxB2);
            panelResults.Controls.Add(textBoxA2);
            panelResults.Controls.Add(textBoxB1);
            panelResults.Controls.Add(textBoxA1);
            panelResults.Controls.Add(labelB2);
            panelResults.Controls.Add(labelA2);
            panelResults.Controls.Add(labelB1);
            panelResults.Controls.Add(labelA1);
            panelResults.Location = new Point(12, 12);
            panelResults.Name = "panelResults";
            panelResults.Size = new Size(304, 150);
            panelResults.TabIndex = 0;
            // 
            // textBoxB2
            // 
            textBoxB2.Location = new Point(156, 92);
            textBoxB2.Name = "textBoxB2";
            textBoxB2.ReadOnly = true;
            textBoxB2.Size = new Size(100, 23);
            textBoxB2.TabIndex = 11;
            // 
            // textBoxA2
            // 
            textBoxA2.Location = new Point(30, 92);
            textBoxA2.Name = "textBoxA2";
            textBoxA2.ReadOnly = true;
            textBoxA2.Size = new Size(100, 23);
            textBoxA2.TabIndex = 10;
            // 
            // textBoxB1
            // 
            textBoxB1.Location = new Point(156, 45);
            textBoxB1.Name = "textBoxB1";
            textBoxB1.ReadOnly = true;
            textBoxB1.Size = new Size(100, 23);
            textBoxB1.TabIndex = 9;
            // 
            // textBoxA1
            // 
            textBoxA1.Location = new Point(30, 45);
            textBoxA1.Name = "textBoxA1";
            textBoxA1.ReadOnly = true;
            textBoxA1.Size = new Size(100, 23);
            textBoxA1.TabIndex = 8;
            // 
            // labelB2
            // 
            labelB2.AutoSize = true;
            labelB2.Location = new Point(156, 74);
            labelB2.Name = "labelB2";
            labelB2.Size = new Size(20, 15);
            labelB2.TabIndex = 3;
            labelB2.Text = "B2";
            // 
            // labelA2
            // 
            labelA2.AutoSize = true;
            labelA2.Location = new Point(30, 74);
            labelA2.Name = "labelA2";
            labelA2.Size = new Size(21, 15);
            labelA2.TabIndex = 2;
            labelA2.Text = "A2";
            // 
            // labelB1
            // 
            labelB1.AutoSize = true;
            labelB1.Location = new Point(156, 27);
            labelB1.Name = "labelB1";
            labelB1.Size = new Size(20, 15);
            labelB1.TabIndex = 1;
            labelB1.Text = "B1";
            // 
            // labelA1
            // 
            labelA1.AutoSize = true;
            labelA1.Location = new Point(30, 27);
            labelA1.Name = "labelA1";
            labelA1.Size = new Size(21, 15);
            labelA1.TabIndex = 0;
            labelA1.Text = "A1";
            // 
            // labelTailY
            // 
            labelTailY.AutoSize = true;
            labelTailY.Location = new Point(168, 242);
            labelTailY.Margin = new Padding(3, 3, 3, 0);
            labelTailY.Name = "labelTailY";
            labelTailY.Size = new Size(35, 15);
            labelTailY.TabIndex = 12;
            labelTailY.Text = "Tail Y";
            // 
            // labelTailX
            // 
            labelTailX.AutoSize = true;
            labelTailX.Location = new Point(42, 242);
            labelTailX.Margin = new Padding(3, 3, 3, 0);
            labelTailX.Name = "labelTailX";
            labelTailX.Size = new Size(35, 15);
            labelTailX.TabIndex = 11;
            labelTailX.Text = "Tail X";
            // 
            // labelHeadY
            // 
            labelHeadY.AutoSize = true;
            labelHeadY.Location = new Point(168, 195);
            labelHeadY.Margin = new Padding(3, 30, 3, 0);
            labelHeadY.Name = "labelHeadY";
            labelHeadY.Size = new Size(45, 15);
            labelHeadY.TabIndex = 10;
            labelHeadY.Text = "Head Y";
            // 
            // labelHeadX
            // 
            labelHeadX.AutoSize = true;
            labelHeadX.Location = new Point(42, 195);
            labelHeadX.Margin = new Padding(30, 30, 3, 0);
            labelHeadX.Name = "labelHeadX";
            labelHeadX.Size = new Size(45, 15);
            labelHeadX.TabIndex = 9;
            labelHeadX.Text = "Head X";
            // 
            // buttonCalculate
            // 
            buttonCalculate.Location = new Point(42, 316);
            buttonCalculate.Margin = new Padding(3, 30, 3, 3);
            buttonCalculate.Name = "buttonCalculate";
            buttonCalculate.Size = new Size(246, 55);
            buttonCalculate.TabIndex = 17;
            buttonCalculate.Text = "Calculate";
            buttonCalculate.UseVisualStyleBackColor = true;
            buttonCalculate.Click += buttonCalculate_Click;
            // 
            // richTextBoxDiagnostics
            // 
            richTextBoxDiagnostics.Location = new Point(12, 377);
            richTextBoxDiagnostics.Name = "richTextBoxDiagnostics";
            richTextBoxDiagnostics.ReadOnly = true;
            richTextBoxDiagnostics.Size = new Size(313, 286);
            richTextBoxDiagnostics.TabIndex = 18;
            richTextBoxDiagnostics.Text = "";
            richTextBoxDiagnostics.WordWrap = false;
            // 
            // pictureBoxKinematicGraph
            // 
            pictureBoxKinematicGraph.BorderStyle = BorderStyle.FixedSingle;
            pictureBoxKinematicGraph.Location = new Point(348, 23);
            pictureBoxKinematicGraph.Name = "pictureBoxKinematicGraph";
            pictureBoxKinematicGraph.Size = new Size(748, 640);
            pictureBoxKinematicGraph.TabIndex = 19;
            pictureBoxKinematicGraph.TabStop = false;
            // 
            // textBoxHeadX
            // 
            textBoxHeadX.Location = new Point(42, 212);
            textBoxHeadX.Name = "textBoxHeadX";
            textBoxHeadX.Size = new Size(100, 23);
            textBoxHeadX.TabIndex = 20;
            textBoxHeadX.TextChanged += InputTextBox_TextChanged;
            // 
            // textBoxTailX
            // 
            textBoxTailX.Location = new Point(42, 260);
            textBoxTailX.Name = "textBoxTailX";
            textBoxTailX.Size = new Size(100, 23);
            textBoxTailX.TabIndex = 22;
            textBoxTailX.TextChanged += InputTextBox_TextChanged;
            // 
            // textBoxTailY
            // 
            textBoxTailY.Location = new Point(168, 260);
            textBoxTailY.Name = "textBoxTailY";
            textBoxTailY.Size = new Size(100, 23);
            textBoxTailY.TabIndex = 23;
            textBoxTailY.TextChanged += InputTextBox_TextChanged;
            // 
            // textBoxHeadY
            // 
            textBoxHeadY.Location = new Point(168, 212);
            textBoxHeadY.Name = "textBoxHeadY";
            textBoxHeadY.Size = new Size(100, 23);
            textBoxHeadY.TabIndex = 21;
            textBoxHeadY.TextChanged += InputTextBox_TextChanged;
            // 
            // KinematicForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1108, 675);
            Controls.Add(textBoxTailY);
            Controls.Add(textBoxTailX);
            Controls.Add(textBoxHeadY);
            Controls.Add(textBoxHeadX);
            Controls.Add(pictureBoxKinematicGraph);
            Controls.Add(richTextBoxDiagnostics);
            Controls.Add(buttonCalculate);
            Controls.Add(labelTailY);
            Controls.Add(labelTailX);
            Controls.Add(labelHeadY);
            Controls.Add(labelHeadX);
            Controls.Add(panelResults);
            Name = "KinematicForm";
            Text = "Form1";
            panelResults.ResumeLayout(false);
            panelResults.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBoxKinematicGraph).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }







        #endregion

        private Panel panelResults;
        private Label labelB2;
        private Label labelA2;
        private Label labelB1;
        private Label labelA1;
        private Label labelTailY;
        private Label labelTailX;
        private Label labelHeadY;
        private Label labelHeadX;
        private Button buttonCalculate;
        private RichTextBox richTextBoxDiagnostics;
        private PictureBox pictureBoxKinematicGraph;
        private TextBox textBoxHeadX;
        private TextBox textBoxTailX;
        private TextBox textBoxTailY;
        private TextBox textBoxHeadY;
        private TextBox textBoxB2;
        private TextBox textBoxA2;
        private TextBox textBoxB1;
        private TextBox textBoxA1;
    }
