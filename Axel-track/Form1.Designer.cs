namespace AxelTrackNS
{
    partial class FormAxelTrack
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormAxelTrack));
            this.label_MessageCommunication = new System.Windows.Forms.Label();
            this.label_ErrorMessage = new System.Windows.Forms.Label();
            this.buttonMoveTo = new System.Windows.Forms.Button();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label4 = new System.Windows.Forms.Label();
            this.textBoxAcceleration = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textBoxVelocity = new System.Windows.Forms.TextBox();
            this.label_GroupStatusDescription = new System.Windows.Forms.Label();
            this.labelPosition = new System.Windows.Forms.Label();
            this.labelStatus = new System.Windows.Forms.Label();
            this.textBoxTarget = new System.Windows.Forms.TextBox();
            this.textBoxPosition = new System.Windows.Forms.TextBox();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.groupBoxShuttle = new System.Windows.Forms.GroupBox();
            this.label_progressText = new System.Windows.Forms.Label();
            this.textBoxSweepCount = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.progressBar1 = new System.Windows.Forms.ProgressBar();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.buttonStopShuttle = new System.Windows.Forms.Button();
            this.textBoxShuttleAcceleration = new System.Windows.Forms.TextBox();
            this.buttonStartShuttle = new System.Windows.Forms.Button();
            this.textBoxShuttleRange = new System.Windows.Forms.TextBox();
            this.tabControl = new System.Windows.Forms.TabControl();
            this.tabSimple = new System.Windows.Forms.TabPage();
            this.buttonAbort = new System.Windows.Forms.Button();
            this.buttonInitiate = new System.Windows.Forms.Button();
            this.tabComplicated = new System.Windows.Forms.TabPage();
            this.labelGroup = new System.Windows.Forms.Label();
            this.TextBox_Group = new System.Windows.Forms.TextBox();
            this.checkBoxLog = new System.Windows.Forms.CheckBox();
            this.buttonKill = new System.Windows.Forms.Button();
            this.buttonHome = new System.Windows.Forms.Button();
            this.buttonInitialize = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.buttonDisconnect = new System.Windows.Forms.Button();
            this.buttonConnect = new System.Windows.Forms.Button();
            this.labelIpPort = new System.Windows.Forms.Label();
            this.labelIpAddress = new System.Windows.Forms.Label();
            this.textBox_IPPort = new System.Windows.Forms.TextBox();
            this.textBox_IPAddress = new System.Windows.Forms.TextBox();
            this.picTrain = new System.Windows.Forms.PictureBox();
            this.groupBox1.SuspendLayout();
            this.groupBoxShuttle.SuspendLayout();
            this.tabControl.SuspendLayout();
            this.tabSimple.SuspendLayout();
            this.tabComplicated.SuspendLayout();
            this.groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picTrain)).BeginInit();
            this.SuspendLayout();
            // 
            // label_MessageCommunication
            // 
            this.label_MessageCommunication.AutoSize = true;
            this.label_MessageCommunication.Location = new System.Drawing.Point(11, 285);
            this.label_MessageCommunication.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label_MessageCommunication.Name = "label_MessageCommunication";
            this.label_MessageCommunication.Size = new System.Drawing.Size(0, 13);
            this.label_MessageCommunication.TabIndex = 1;
            // 
            // label_ErrorMessage
            // 
            this.label_ErrorMessage.AutoSize = true;
            this.label_ErrorMessage.ForeColor = System.Drawing.Color.Red;
            this.label_ErrorMessage.Location = new System.Drawing.Point(135, 366);
            this.label_ErrorMessage.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label_ErrorMessage.Name = "label_ErrorMessage";
            this.label_ErrorMessage.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.label_ErrorMessage.Size = new System.Drawing.Size(0, 13);
            this.label_ErrorMessage.TabIndex = 1;
            // 
            // buttonMoveTo
            // 
            this.buttonMoveTo.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(224)))), ((int)(((byte)(192)))));
            this.buttonMoveTo.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonMoveTo.Location = new System.Drawing.Point(106, 108);
            this.buttonMoveTo.Name = "buttonMoveTo";
            this.buttonMoveTo.Size = new System.Drawing.Size(75, 32);
            this.buttonMoveTo.TabIndex = 5;
            this.buttonMoveTo.Text = "Move to";
            this.buttonMoveTo.UseVisualStyleBackColor = false;
            this.buttonMoveTo.Click += new System.EventHandler(this.buttonMoveTo_Click);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.textBoxAcceleration);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.textBoxVelocity);
            this.groupBox1.Controls.Add(this.label_GroupStatusDescription);
            this.groupBox1.Controls.Add(this.buttonMoveTo);
            this.groupBox1.Controls.Add(this.labelPosition);
            this.groupBox1.Controls.Add(this.labelStatus);
            this.groupBox1.Controls.Add(this.textBoxTarget);
            this.groupBox1.Controls.Add(this.textBoxPosition);
            this.groupBox1.Controls.Add(this.textBoxStatus);
            this.groupBox1.Location = new System.Drawing.Point(229, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(300, 180);
            this.groupBox1.TabIndex = 6;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "XPS status ";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(189, 54);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(102, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Acceleration (estim.)";
            // 
            // textBoxAcceleration
            // 
            this.textBoxAcceleration.BackColor = System.Drawing.Color.LightGoldenrodYellow;
            this.textBoxAcceleration.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxAcceleration.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxAcceleration.Location = new System.Drawing.Point(199, 71);
            this.textBoxAcceleration.Name = "textBoxAcceleration";
            this.textBoxAcceleration.ReadOnly = true;
            this.textBoxAcceleration.Size = new System.Drawing.Size(75, 21);
            this.textBoxAcceleration.TabIndex = 10;
            this.textBoxAcceleration.Text = "0";
            this.textBoxAcceleration.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(100, 54);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(44, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Velocity";
            // 
            // textBoxVelocity
            // 
            this.textBoxVelocity.BackColor = System.Drawing.Color.AliceBlue;
            this.textBoxVelocity.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxVelocity.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxVelocity.Location = new System.Drawing.Point(106, 71);
            this.textBoxVelocity.Name = "textBoxVelocity";
            this.textBoxVelocity.ReadOnly = true;
            this.textBoxVelocity.Size = new System.Drawing.Size(75, 21);
            this.textBoxVelocity.TabIndex = 8;
            this.textBoxVelocity.Text = "0";
            this.textBoxVelocity.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label_GroupStatusDescription
            // 
            this.label_GroupStatusDescription.AutoSize = true;
            this.label_GroupStatusDescription.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_GroupStatusDescription.ForeColor = System.Drawing.Color.DimGray;
            this.label_GroupStatusDescription.Location = new System.Drawing.Point(15, 153);
            this.label_GroupStatusDescription.Name = "label_GroupStatusDescription";
            this.label_GroupStatusDescription.Size = new System.Drawing.Size(37, 13);
            this.label_GroupStatusDescription.TabIndex = 7;
            this.label_GroupStatusDescription.Text = "Status";
            // 
            // labelPosition
            // 
            this.labelPosition.AutoSize = true;
            this.labelPosition.Location = new System.Drawing.Point(10, 54);
            this.labelPosition.Name = "labelPosition";
            this.labelPosition.Size = new System.Drawing.Size(44, 13);
            this.labelPosition.TabIndex = 4;
            this.labelPosition.Text = "Position";
            // 
            // labelStatus
            // 
            this.labelStatus.AutoSize = true;
            this.labelStatus.Location = new System.Drawing.Point(141, 22);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(37, 13);
            this.labelStatus.TabIndex = 4;
            this.labelStatus.Text = "Status";
            // 
            // textBoxTarget
            // 
            this.textBoxTarget.BackColor = System.Drawing.Color.White;
            this.textBoxTarget.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxTarget.Location = new System.Drawing.Point(200, 115);
            this.textBoxTarget.Name = "textBoxTarget";
            this.textBoxTarget.Size = new System.Drawing.Size(75, 24);
            this.textBoxTarget.TabIndex = 3;
            this.textBoxTarget.Text = "-100";
            this.textBoxTarget.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // textBoxPosition
            // 
            this.textBoxPosition.BackColor = System.Drawing.Color.LemonChiffon;
            this.textBoxPosition.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxPosition.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxPosition.Location = new System.Drawing.Point(12, 71);
            this.textBoxPosition.Name = "textBoxPosition";
            this.textBoxPosition.ReadOnly = true;
            this.textBoxPosition.Size = new System.Drawing.Size(75, 21);
            this.textBoxPosition.TabIndex = 3;
            this.textBoxPosition.Text = "0";
            this.textBoxPosition.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // textBoxStatus
            // 
            this.textBoxStatus.BackColor = System.Drawing.SystemColors.Control;
            this.textBoxStatus.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxStatus.Location = new System.Drawing.Point(191, 20);
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            this.textBoxStatus.Size = new System.Drawing.Size(75, 20);
            this.textBoxStatus.TabIndex = 3;
            this.textBoxStatus.Text = "0";
            this.textBoxStatus.TextAlign = System.Windows.Forms.HorizontalAlignment.Center;
            // 
            // groupBoxShuttle
            // 
            this.groupBoxShuttle.Controls.Add(this.label_progressText);
            this.groupBoxShuttle.Controls.Add(this.textBoxSweepCount);
            this.groupBoxShuttle.Controls.Add(this.label6);
            this.groupBoxShuttle.Controls.Add(this.progressBar1);
            this.groupBoxShuttle.Controls.Add(this.label2);
            this.groupBoxShuttle.Controls.Add(this.label1);
            this.groupBoxShuttle.Controls.Add(this.buttonStopShuttle);
            this.groupBoxShuttle.Controls.Add(this.textBoxShuttleAcceleration);
            this.groupBoxShuttle.Controls.Add(this.buttonStartShuttle);
            this.groupBoxShuttle.Controls.Add(this.textBoxShuttleRange);
            this.groupBoxShuttle.Location = new System.Drawing.Point(229, 203);
            this.groupBoxShuttle.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxShuttle.Name = "groupBoxShuttle";
            this.groupBoxShuttle.Padding = new System.Windows.Forms.Padding(2);
            this.groupBoxShuttle.Size = new System.Drawing.Size(300, 151);
            this.groupBoxShuttle.TabIndex = 8;
            this.groupBoxShuttle.TabStop = false;
            this.groupBoxShuttle.Text = " Jogging ";
            // 
            // label_progressText
            // 
            this.label_progressText.AutoSize = true;
            this.label_progressText.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label_progressText.Location = new System.Drawing.Point(170, 105);
            this.label_progressText.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label_progressText.Name = "label_progressText";
            this.label_progressText.Size = new System.Drawing.Size(20, 17);
            this.label_progressText.TabIndex = 25;
            this.label_progressText.Text = "[ ]";
            // 
            // textBoxSweepCount
            // 
            this.textBoxSweepCount.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxSweepCount.Location = new System.Drawing.Point(129, 105);
            this.textBoxSweepCount.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxSweepCount.Name = "textBoxSweepCount";
            this.textBoxSweepCount.Size = new System.Drawing.Size(37, 24);
            this.textBoxSweepCount.TabIndex = 24;
            this.textBoxSweepCount.Text = "3";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(33, 108);
            this.label6.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(94, 15);
            this.label6.TabIndex = 23;
            this.label6.Text = "numb. of cycles:";
            // 
            // progressBar1
            // 
            this.progressBar1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar1.Location = new System.Drawing.Point(0, 137);
            this.progressBar1.Margin = new System.Windows.Forms.Padding(2);
            this.progressBar1.Name = "progressBar1";
            this.progressBar1.Size = new System.Drawing.Size(300, 14);
            this.progressBar1.TabIndex = 19;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.Color.Maroon;
            this.label2.Location = new System.Drawing.Point(122, 60);
            this.label2.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 13);
            this.label2.TabIndex = 18;
            this.label2.Text = "Acceleration";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.Color.Blue;
            this.label1.Location = new System.Drawing.Point(122, 15);
            this.label1.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(64, 13);
            this.label1.TabIndex = 17;
            this.label1.Text = "Range [mm]";
            // 
            // buttonStopShuttle
            // 
            this.buttonStopShuttle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.buttonStopShuttle.Enabled = false;
            this.buttonStopShuttle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonStopShuttle.Location = new System.Drawing.Point(200, 42);
            this.buttonStopShuttle.Name = "buttonStopShuttle";
            this.buttonStopShuttle.Size = new System.Drawing.Size(94, 37);
            this.buttonStopShuttle.TabIndex = 16;
            this.buttonStopShuttle.Text = "Stop Shuttle";
            this.buttonStopShuttle.UseVisualStyleBackColor = false;
            this.buttonStopShuttle.Click += new System.EventHandler(this.buttonStopShuttle_Click);
            // 
            // textBoxShuttleAcceleration
            // 
            this.textBoxShuttleAcceleration.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxShuttleAcceleration.ForeColor = System.Drawing.Color.Maroon;
            this.textBoxShuttleAcceleration.Location = new System.Drawing.Point(115, 75);
            this.textBoxShuttleAcceleration.Name = "textBoxShuttleAcceleration";
            this.textBoxShuttleAcceleration.Size = new System.Drawing.Size(75, 24);
            this.textBoxShuttleAcceleration.TabIndex = 15;
            this.textBoxShuttleAcceleration.Text = "500";
            this.textBoxShuttleAcceleration.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // buttonStartShuttle
            // 
            this.buttonStartShuttle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(192)))), ((int)(((byte)(255)))), ((int)(((byte)(192)))));
            this.buttonStartShuttle.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonStartShuttle.Location = new System.Drawing.Point(10, 42);
            this.buttonStartShuttle.Name = "buttonStartShuttle";
            this.buttonStartShuttle.Size = new System.Drawing.Size(95, 37);
            this.buttonStartShuttle.TabIndex = 14;
            this.buttonStartShuttle.Text = "Start Shuttle";
            this.buttonStartShuttle.UseVisualStyleBackColor = false;
            this.buttonStartShuttle.Click += new System.EventHandler(this.buttonStartShuttle_Click);
            // 
            // textBoxShuttleRange
            // 
            this.textBoxShuttleRange.BackColor = System.Drawing.Color.White;
            this.textBoxShuttleRange.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxShuttleRange.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(192)))));
            this.textBoxShuttleRange.Location = new System.Drawing.Point(115, 32);
            this.textBoxShuttleRange.Name = "textBoxShuttleRange";
            this.textBoxShuttleRange.Size = new System.Drawing.Size(75, 24);
            this.textBoxShuttleRange.TabIndex = 13;
            this.textBoxShuttleRange.Text = "300";
            this.textBoxShuttleRange.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // tabControl
            // 
            this.tabControl.Controls.Add(this.tabSimple);
            this.tabControl.Controls.Add(this.tabComplicated);
            this.tabControl.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl.Location = new System.Drawing.Point(7, 12);
            this.tabControl.Margin = new System.Windows.Forms.Padding(2);
            this.tabControl.Name = "tabControl";
            this.tabControl.SelectedIndex = 0;
            this.tabControl.Size = new System.Drawing.Size(212, 264);
            this.tabControl.TabIndex = 9;
            // 
            // tabSimple
            // 
            this.tabSimple.Controls.Add(this.buttonAbort);
            this.tabSimple.Controls.Add(this.buttonInitiate);
            this.tabSimple.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabSimple.Location = new System.Drawing.Point(4, 25);
            this.tabSimple.Margin = new System.Windows.Forms.Padding(2);
            this.tabSimple.Name = "tabSimple";
            this.tabSimple.Padding = new System.Windows.Forms.Padding(2);
            this.tabSimple.Size = new System.Drawing.Size(204, 235);
            this.tabSimple.TabIndex = 0;
            this.tabSimple.Text = " Simple ";
            this.tabSimple.UseVisualStyleBackColor = true;
            // 
            // buttonAbort
            // 
            this.buttonAbort.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonAbort.ForeColor = System.Drawing.Color.Gray;
            this.buttonAbort.Location = new System.Drawing.Point(28, 159);
            this.buttonAbort.Margin = new System.Windows.Forms.Padding(2);
            this.buttonAbort.Name = "buttonAbort";
            this.buttonAbort.Size = new System.Drawing.Size(154, 48);
            this.buttonAbort.TabIndex = 1;
            this.buttonAbort.Text = "Abort";
            this.buttonAbort.UseVisualStyleBackColor = true;
            this.buttonAbort.Visible = false;
            this.buttonAbort.Click += new System.EventHandler(this.buttonAbort_Click);
            // 
            // buttonInitiate
            // 
            this.buttonInitiate.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonInitiate.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(192)))));
            this.buttonInitiate.Location = new System.Drawing.Point(28, 50);
            this.buttonInitiate.Margin = new System.Windows.Forms.Padding(2);
            this.buttonInitiate.Name = "buttonInitiate";
            this.buttonInitiate.Size = new System.Drawing.Size(154, 50);
            this.buttonInitiate.TabIndex = 0;
            this.buttonInitiate.Text = "Get ready";
            this.buttonInitiate.UseVisualStyleBackColor = true;
            this.buttonInitiate.Click += new System.EventHandler(this.buttonInitiate_Click);
            // 
            // tabComplicated
            // 
            this.tabComplicated.Controls.Add(this.labelGroup);
            this.tabComplicated.Controls.Add(this.TextBox_Group);
            this.tabComplicated.Controls.Add(this.checkBoxLog);
            this.tabComplicated.Controls.Add(this.buttonKill);
            this.tabComplicated.Controls.Add(this.buttonHome);
            this.tabComplicated.Controls.Add(this.buttonInitialize);
            this.tabComplicated.Controls.Add(this.groupBox2);
            this.tabComplicated.Location = new System.Drawing.Point(4, 25);
            this.tabComplicated.Margin = new System.Windows.Forms.Padding(2);
            this.tabComplicated.Name = "tabComplicated";
            this.tabComplicated.Padding = new System.Windows.Forms.Padding(2);
            this.tabComplicated.Size = new System.Drawing.Size(204, 235);
            this.tabComplicated.TabIndex = 1;
            this.tabComplicated.Text = "Complicated";
            this.tabComplicated.UseVisualStyleBackColor = true;
            // 
            // labelGroup
            // 
            this.labelGroup.AutoSize = true;
            this.labelGroup.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelGroup.Location = new System.Drawing.Point(5, 132);
            this.labelGroup.Name = "labelGroup";
            this.labelGroup.Size = new System.Drawing.Size(97, 15);
            this.labelGroup.TabIndex = 14;
            this.labelGroup.Text = "Positioner name";
            // 
            // TextBox_Group
            // 
            this.TextBox_Group.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.TextBox_Group.Location = new System.Drawing.Point(103, 129);
            this.TextBox_Group.Name = "TextBox_Group";
            this.TextBox_Group.Size = new System.Drawing.Size(91, 21);
            this.TextBox_Group.TabIndex = 13;
            this.TextBox_Group.Text = "Group1.Pos";
            // 
            // checkBoxLog
            // 
            this.checkBoxLog.AutoSize = true;
            this.checkBoxLog.Checked = true;
            this.checkBoxLog.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBoxLog.Location = new System.Drawing.Point(107, 196);
            this.checkBoxLog.Name = "checkBoxLog";
            this.checkBoxLog.Size = new System.Drawing.Size(87, 21);
            this.checkBoxLog.TabIndex = 12;
            this.checkBoxLog.Text = "Save Log";
            this.checkBoxLog.UseVisualStyleBackColor = true;
            // 
            // buttonKill
            // 
            this.buttonKill.BackColor = System.Drawing.SystemColors.Control;
            this.buttonKill.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonKill.Location = new System.Drawing.Point(109, 159);
            this.buttonKill.Name = "buttonKill";
            this.buttonKill.Size = new System.Drawing.Size(75, 23);
            this.buttonKill.TabIndex = 9;
            this.buttonKill.Text = "Kill";
            this.buttonKill.UseVisualStyleBackColor = false;
            this.buttonKill.Click += new System.EventHandler(this.buttonKill_Click);
            // 
            // buttonHome
            // 
            this.buttonHome.BackColor = System.Drawing.SystemColors.Control;
            this.buttonHome.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonHome.Location = new System.Drawing.Point(14, 196);
            this.buttonHome.Name = "buttonHome";
            this.buttonHome.Size = new System.Drawing.Size(75, 23);
            this.buttonHome.TabIndex = 10;
            this.buttonHome.Text = "Home";
            this.buttonHome.UseVisualStyleBackColor = false;
            this.buttonHome.Click += new System.EventHandler(this.buttonHome_Click);
            // 
            // buttonInitialize
            // 
            this.buttonInitialize.BackColor = System.Drawing.SystemColors.Control;
            this.buttonInitialize.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonInitialize.Location = new System.Drawing.Point(14, 159);
            this.buttonInitialize.Name = "buttonInitialize";
            this.buttonInitialize.Size = new System.Drawing.Size(75, 23);
            this.buttonInitialize.TabIndex = 11;
            this.buttonInitialize.Text = "Initialize";
            this.buttonInitialize.UseVisualStyleBackColor = false;
            this.buttonInitialize.Click += new System.EventHandler(this.buttonInitialize_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.buttonDisconnect);
            this.groupBox2.Controls.Add(this.buttonConnect);
            this.groupBox2.Controls.Add(this.labelIpPort);
            this.groupBox2.Controls.Add(this.labelIpAddress);
            this.groupBox2.Controls.Add(this.textBox_IPPort);
            this.groupBox2.Controls.Add(this.textBox_IPAddress);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(3, 5);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(197, 115);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "TCP IP";
            // 
            // buttonDisconnect
            // 
            this.buttonDisconnect.Location = new System.Drawing.Point(105, 76);
            this.buttonDisconnect.Margin = new System.Windows.Forms.Padding(2);
            this.buttonDisconnect.Name = "buttonDisconnect";
            this.buttonDisconnect.Size = new System.Drawing.Size(75, 23);
            this.buttonDisconnect.TabIndex = 2;
            this.buttonDisconnect.Text = "Disconnect";
            this.buttonDisconnect.UseVisualStyleBackColor = true;
            this.buttonDisconnect.Click += new System.EventHandler(this.buttonDisconnect_Click);
            // 
            // buttonConnect
            // 
            this.buttonConnect.Location = new System.Drawing.Point(11, 76);
            this.buttonConnect.Margin = new System.Windows.Forms.Padding(2);
            this.buttonConnect.Name = "buttonConnect";
            this.buttonConnect.Size = new System.Drawing.Size(75, 23);
            this.buttonConnect.TabIndex = 2;
            this.buttonConnect.Text = "Connect";
            this.buttonConnect.UseVisualStyleBackColor = true;
            this.buttonConnect.Click += new System.EventHandler(this.ConnectButton);
            // 
            // labelIpPort
            // 
            this.labelIpPort.AutoSize = true;
            this.labelIpPort.Location = new System.Drawing.Point(20, 49);
            this.labelIpPort.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelIpPort.Name = "labelIpPort";
            this.labelIpPort.Size = new System.Drawing.Size(43, 15);
            this.labelIpPort.TabIndex = 1;
            this.labelIpPort.Text = "IP Port";
            // 
            // labelIpAddress
            // 
            this.labelIpAddress.AutoSize = true;
            this.labelIpAddress.Location = new System.Drawing.Point(20, 23);
            this.labelIpAddress.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelIpAddress.Name = "labelIpAddress";
            this.labelIpAddress.Size = new System.Drawing.Size(65, 15);
            this.labelIpAddress.TabIndex = 1;
            this.labelIpAddress.Text = "IP address";
            // 
            // textBox_IPPort
            // 
            this.textBox_IPPort.Location = new System.Drawing.Point(90, 46);
            this.textBox_IPPort.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_IPPort.Name = "textBox_IPPort";
            this.textBox_IPPort.Size = new System.Drawing.Size(39, 21);
            this.textBox_IPPort.TabIndex = 0;
            this.textBox_IPPort.Text = "5001";
            // 
            // textBox_IPAddress
            // 
            this.textBox_IPAddress.Location = new System.Drawing.Point(90, 21);
            this.textBox_IPAddress.Margin = new System.Windows.Forms.Padding(2);
            this.textBox_IPAddress.Name = "textBox_IPAddress";
            this.textBox_IPAddress.Size = new System.Drawing.Size(92, 21);
            this.textBox_IPAddress.TabIndex = 0;
            this.textBox_IPAddress.Text = "192.168.0.254";
            // 
            // picTrain
            // 
            this.picTrain.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.picTrain.Image = ((System.Drawing.Image)(resources.GetObject("picTrain.Image")));
            this.picTrain.ImageLocation = "";
            this.picTrain.InitialImage = ((System.Drawing.Image)(resources.GetObject("picTrain.InitialImage")));
            this.picTrain.Location = new System.Drawing.Point(7, 308);
            this.picTrain.Margin = new System.Windows.Forms.Padding(2);
            this.picTrain.Name = "picTrain";
            this.picTrain.Size = new System.Drawing.Size(119, 67);
            this.picTrain.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.picTrain.TabIndex = 10;
            this.picTrain.TabStop = false;
            this.picTrain.Click += new System.EventHandler(this.picTrain_Click);
            // 
            // FormAxelTrack
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(542, 394);
            this.Controls.Add(this.picTrain);
            this.Controls.Add(this.tabControl);
            this.Controls.Add(this.groupBoxShuttle);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label_MessageCommunication);
            this.Controls.Add(this.label_ErrorMessage);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "FormAxelTrack";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Axel Track v1.2";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBoxShuttle.ResumeLayout(false);
            this.groupBoxShuttle.PerformLayout();
            this.tabControl.ResumeLayout(false);
            this.tabSimple.ResumeLayout(false);
            this.tabComplicated.ResumeLayout(false);
            this.tabComplicated.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picTrain)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label_MessageCommunication;
        private System.Windows.Forms.Label label_ErrorMessage;
        private System.Windows.Forms.Button buttonMoveTo;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelPosition;
        private System.Windows.Forms.Label labelStatus;
        private System.Windows.Forms.TextBox textBoxTarget;
        private System.Windows.Forms.TextBox textBoxPosition;
        private System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBoxAcceleration;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBoxVelocity;
        private System.Windows.Forms.Label label_GroupStatusDescription;
        private System.Windows.Forms.GroupBox groupBoxShuttle;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonStopShuttle;
        private System.Windows.Forms.TextBox textBoxShuttleAcceleration;
        private System.Windows.Forms.Button buttonStartShuttle;
        private System.Windows.Forms.TextBox textBoxShuttleRange;
        private System.Windows.Forms.TabControl tabControl;
        private System.Windows.Forms.TabPage tabSimple;
        private System.Windows.Forms.Button buttonAbort;
        private System.Windows.Forms.Button buttonInitiate;
        private System.Windows.Forms.TabPage tabComplicated;
        private System.Windows.Forms.Button buttonKill;
        private System.Windows.Forms.Button buttonHome;
        private System.Windows.Forms.Button buttonInitialize;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Button buttonDisconnect;
        private System.Windows.Forms.Button buttonConnect;
        private System.Windows.Forms.Label labelIpPort;
        private System.Windows.Forms.Label labelIpAddress;
        private System.Windows.Forms.TextBox textBox_IPPort;
        private System.Windows.Forms.TextBox textBox_IPAddress;
        private System.Windows.Forms.CheckBox checkBoxLog;
        private System.Windows.Forms.TextBox textBoxSweepCount;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label labelGroup;
        private System.Windows.Forms.TextBox TextBox_Group;
        private System.Windows.Forms.Label label_progressText;
        private System.Windows.Forms.PictureBox picTrain;
    }
}

