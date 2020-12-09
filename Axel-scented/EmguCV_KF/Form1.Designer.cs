namespace Kalman_Filter
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
            this.MouseTrackingArea = new System.Windows.Forms.PictureBox();
            this.MouseLive_LBL = new System.Windows.Forms.Label();
            this.Start_BTN = new System.Windows.Forms.Button();
            this.MouseTimed_LBL = new System.Windows.Forms.Label();
            this.MousePredicted_LBL = new System.Windows.Forms.Label();
            this.MouseCorrected_LBL = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.MouseTrackingArea)).BeginInit();
            this.SuspendLayout();
            // 
            // MouseTrackingArea
            // 
            this.MouseTrackingArea.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.MouseTrackingArea.BackColor = System.Drawing.Color.White;
            this.MouseTrackingArea.Location = new System.Drawing.Point(12, 49);
            this.MouseTrackingArea.Name = "MouseTrackingArea";
            this.MouseTrackingArea.Size = new System.Drawing.Size(611, 411);
            this.MouseTrackingArea.TabIndex = 0;
            this.MouseTrackingArea.TabStop = false;
            this.MouseTrackingArea.MouseMove += new System.Windows.Forms.MouseEventHandler(this.MouseTrackingArea_MouseMove);
            // 
            // MouseLive_LBL
            // 
            this.MouseLive_LBL.AutoSize = true;
            this.MouseLive_LBL.Location = new System.Drawing.Point(115, 9);
            this.MouseLive_LBL.Name = "MouseLive_LBL";
            this.MouseLive_LBL.Size = new System.Drawing.Size(105, 13);
            this.MouseLive_LBL.TabIndex = 1;
            this.MouseLive_LBL.Text = "Mouse Position Live:";
            // 
            // Start_BTN
            // 
            this.Start_BTN.Location = new System.Drawing.Point(13, 12);
            this.Start_BTN.Name = "Start_BTN";
            this.Start_BTN.Size = new System.Drawing.Size(75, 23);
            this.Start_BTN.TabIndex = 2;
            this.Start_BTN.Text = "Start";
            this.Start_BTN.UseVisualStyleBackColor = true;
            this.Start_BTN.Click += new System.EventHandler(this.Start_BTN_Click);
            // 
            // MouseTimed_LBL
            // 
            this.MouseTimed_LBL.AutoSize = true;
            this.MouseTimed_LBL.Location = new System.Drawing.Point(115, 29);
            this.MouseTimed_LBL.Name = "MouseTimed_LBL";
            this.MouseTimed_LBL.Size = new System.Drawing.Size(114, 13);
            this.MouseTimed_LBL.TabIndex = 3;
            this.MouseTimed_LBL.Text = "Mouse Position Timed:";
            // 
            // MousePredicted_LBL
            // 
            this.MousePredicted_LBL.AutoSize = true;
            this.MousePredicted_LBL.Location = new System.Drawing.Point(348, 29);
            this.MousePredicted_LBL.Name = "MousePredicted_LBL";
            this.MousePredicted_LBL.Size = new System.Drawing.Size(130, 13);
            this.MousePredicted_LBL.TabIndex = 4;
            this.MousePredicted_LBL.Text = "Mouse Position Predicted:";
            // 
            // MouseCorrected_LBL
            // 
            this.MouseCorrected_LBL.AutoSize = true;
            this.MouseCorrected_LBL.Location = new System.Drawing.Point(348, 9);
            this.MouseCorrected_LBL.Name = "MouseCorrected_LBL";
            this.MouseCorrected_LBL.Size = new System.Drawing.Size(131, 13);
            this.MouseCorrected_LBL.TabIndex = 5;
            this.MouseCorrected_LBL.Text = "Mouse Position Corrected:";
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(635, 472);
            this.Controls.Add(this.MouseCorrected_LBL);
            this.Controls.Add(this.MousePredicted_LBL);
            this.Controls.Add(this.MouseTimed_LBL);
            this.Controls.Add(this.Start_BTN);
            this.Controls.Add(this.MouseLive_LBL);
            this.Controls.Add(this.MouseTrackingArea);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.MouseTrackingArea)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox MouseTrackingArea;
        private System.Windows.Forms.Label MouseLive_LBL;
        private System.Windows.Forms.Button Start_BTN;
        private System.Windows.Forms.Label MouseTimed_LBL;
        private System.Windows.Forms.Label MousePredicted_LBL;
        private System.Windows.Forms.Label MouseCorrected_LBL;
    }
}

