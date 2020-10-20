/*******************************************************************************
*
* Example program:
*   GlobalDigitalPortIO_USB
*
* Description:
*   This example shows how to load a digital port input/output task from the Measurement & 
*   Automation Explorer (MAX) and use it to read/write the lowest 8 bits from/to the digital port.
*   This example should also work with E-Series and M-Series devices.
*
* Instructions for running:
*   1.  Create an on demand digital port I/O NI-DAQmx global task in MAX. For help, refer to 
*       "Creating Tasks and Channels" in the Measurement & Automation Explorer Help. 
*       To access this help, select Start>>All Programs>>National Instruments>>
*       Measurement & Automation. In MAX, select Help>>MAX Help.
*
*       Note: If you prefer, you can import an on demand digital port I/O task and a simulated USB
*       device into MAX from the GlobalDigitalPort[Input/Output]_USB.nce file, which is located in the 
*       example directory. Refer to "Using the Configuration Import Wizard" in the 
*       Measurement & Automation Explorer Help for more information.
*
*   2.  Run the application, select the task from the drop-down list, and then toggle to switches
*       to write values to the port or click the Read button to read values from the port
*
* Steps:
*  Write
*   1.  Load the task from MAX.
*   2.  Create a DigitalSingleChannelWriter and call WriteSingleSamplePort to write the data
*       to the digital port.
*  Read
*   1.  Load the task from MAX.
*   2.  Create a DigitalSingleChannelReader and call ReadSingleSamplePortInt32 to read the data
*       from the digital port.
*******************************************************************************/


using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using NationalInstruments.DAQmx;
using NationalInstruments.UI;
using NationalInstruments.UI.WindowsForms;

namespace NationalInstruments.Examples.GlobalDigitalPortIO_USB
{
    /// <summary>
    /// Summary description for Form1.
    /// </summary>
    public class MainForm : System.Windows.Forms.Form
    {
        private NationalInstruments.UI.WindowsForms.Switch line7Switch;
        private NationalInstruments.UI.WindowsForms.Led line7Led;
        private System.Windows.Forms.GroupBox writeGroupBox;
        private NationalInstruments.UI.WindowsForms.Switch line6Switch;
        private NationalInstruments.UI.WindowsForms.Switch line5Switch;
        private NationalInstruments.UI.WindowsForms.Switch line4Switch;
        private NationalInstruments.UI.WindowsForms.Switch line3Switch;
        private NationalInstruments.UI.WindowsForms.Switch line2Switch;
        private NationalInstruments.UI.WindowsForms.Switch line1Switch;
        private NationalInstruments.UI.WindowsForms.Switch line0Switch;
        private System.Windows.Forms.GroupBox readGroupBox;
        private NationalInstruments.UI.WindowsForms.Led line6Led;
        private NationalInstruments.UI.WindowsForms.Led line5Led;
        private NationalInstruments.UI.WindowsForms.Led line4Led;
        private NationalInstruments.UI.WindowsForms.Led line3Led;
        private NationalInstruments.UI.WindowsForms.Led line2Led;
        private NationalInstruments.UI.WindowsForms.Led line1Led;
        private NationalInstruments.UI.WindowsForms.Led line0Led;
        private System.Windows.Forms.Button readButton;
        private Label writeLabel;
        private ComboBox writeComboBox;
        private Label readLabel;
        private ComboBox readComboBox;
        private Label infoLabel;
        private Label ledLine7Label;
        private Label ledLine0Label;
        private Label switchLine7Label;
        private Label switchLine0Label;
        private bool resettingSwitches;
        private System.Windows.Forms.Label readIndoLabel;

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;

        public MainForm()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            readButton.Enabled = false;

            // Add valid digital input and output tasks to the combo boxes
            foreach (string s in DaqSystem.Local.Tasks)
            {
                try
                {
                    using (Task t = DaqSystem.Local.LoadTask(s))
                    {
                        t.Control(TaskAction.Verify);

                        if (t.DOChannels.Count > 0 &&
                            t.Timing.SampleTimingType == SampleTimingType.OnDemand)
                        {
                            writeComboBox.Items.Add(s);
                            readComboBox.Items.Add(s);
                        }

                        if (t.DIChannels.Count > 0 &&
                            t.Timing.SampleTimingType == SampleTimingType.OnDemand)
                        {
                            readComboBox.Items.Add(s);
                        }
                    }
                }
                catch (DaqException)
                {
                    // Ignore invalid tasks
                }
            }

            // By default select the first item in the combo boxes
            if (writeComboBox.Items.Count > 0)
            {
                writeComboBox.SelectedIndex = 0;
            }

            if (readComboBox.Items.Count > 0)
            {
                readComboBox.SelectedIndex = 0;
                readButton.Enabled = true;
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                if (components != null) 
                {
                    components.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.line7Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line7Led = new NationalInstruments.UI.WindowsForms.Led();
            this.writeGroupBox = new System.Windows.Forms.GroupBox();
            this.switchLine7Label = new System.Windows.Forms.Label();
            this.switchLine0Label = new System.Windows.Forms.Label();
            this.infoLabel = new System.Windows.Forms.Label();
            this.writeLabel = new System.Windows.Forms.Label();
            this.writeComboBox = new System.Windows.Forms.ComboBox();
            this.line6Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line5Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line4Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line3Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line2Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line1Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.line0Switch = new NationalInstruments.UI.WindowsForms.Switch();
            this.readGroupBox = new System.Windows.Forms.GroupBox();
            this.ledLine7Label = new System.Windows.Forms.Label();
            this.ledLine0Label = new System.Windows.Forms.Label();
            this.readIndoLabel = new System.Windows.Forms.Label();
            this.readLabel = new System.Windows.Forms.Label();
            this.readComboBox = new System.Windows.Forms.ComboBox();
            this.readButton = new System.Windows.Forms.Button();
            this.line6Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line5Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line4Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line3Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line2Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line1Led = new NationalInstruments.UI.WindowsForms.Led();
            this.line0Led = new NationalInstruments.UI.WindowsForms.Led();
            ((System.ComponentModel.ISupportInitialize)(this.line7Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line7Led)).BeginInit();
            this.writeGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.line6Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line5Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line4Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line3Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line2Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line1Switch)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line0Switch)).BeginInit();
            this.readGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.line6Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line5Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line4Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line3Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line2Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line1Led)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.line0Led)).BeginInit();
            this.SuspendLayout();
            // 
            // line7Switch
            // 
            this.line7Switch.Location = new System.Drawing.Point(16, 73);
            this.line7Switch.Name = "line7Switch";
            this.line7Switch.Size = new System.Drawing.Size(40, 70);
            this.line7Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line7Switch.TabIndex = 0;
            this.line7Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line7Led
            // 
            this.line7Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line7Led.Location = new System.Drawing.Point(16, 94);
            this.line7Led.Name = "line7Led";
            this.line7Led.Size = new System.Drawing.Size(35, 35);
            this.line7Led.TabIndex = 1;
            // 
            // writeGroupBox
            // 
            this.writeGroupBox.Controls.Add(this.switchLine7Label);
            this.writeGroupBox.Controls.Add(this.switchLine0Label);
            this.writeGroupBox.Controls.Add(this.infoLabel);
            this.writeGroupBox.Controls.Add(this.writeLabel);
            this.writeGroupBox.Controls.Add(this.writeComboBox);
            this.writeGroupBox.Controls.Add(this.line7Switch);
            this.writeGroupBox.Controls.Add(this.line6Switch);
            this.writeGroupBox.Controls.Add(this.line5Switch);
            this.writeGroupBox.Controls.Add(this.line4Switch);
            this.writeGroupBox.Controls.Add(this.line3Switch);
            this.writeGroupBox.Controls.Add(this.line2Switch);
            this.writeGroupBox.Controls.Add(this.line1Switch);
            this.writeGroupBox.Controls.Add(this.line0Switch);
            this.writeGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.writeGroupBox.Location = new System.Drawing.Point(40, 16);
            this.writeGroupBox.Name = "writeGroupBox";
            this.writeGroupBox.Size = new System.Drawing.Size(745, 156);
            this.writeGroupBox.TabIndex = 2;
            this.writeGroupBox.TabStop = false;
            this.writeGroupBox.Text = "Digital Write";
            // 
            // switchLine7Label
            // 
            this.switchLine7Label.AutoSize = true;
            this.switchLine7Label.Location = new System.Drawing.Point(30, 136);
            this.switchLine7Label.Name = "switchLine7Label";
            this.switchLine7Label.Size = new System.Drawing.Size(10, 16);
            this.switchLine7Label.TabIndex = 8;
            this.switchLine7Label.Text = "7";
            // 
            // switchLine0Label
            // 
            this.switchLine0Label.AutoSize = true;
            this.switchLine0Label.Location = new System.Drawing.Point(422, 137);
            this.switchLine0Label.Name = "switchLine0Label";
            this.switchLine0Label.Size = new System.Drawing.Size(10, 16);
            this.switchLine0Label.TabIndex = 7;
            this.switchLine0Label.Text = "0";
            // 
            // infoLabel
            // 
            this.infoLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.infoLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.infoLabel.Location = new System.Drawing.Point(475, 32);
            this.infoLabel.Name = "infoLabel";
            this.infoLabel.Size = new System.Drawing.Size(264, 96);
            this.infoLabel.TabIndex = 6;
            this.infoLabel.Text = @"Before running this application, make sure you have a USB device and an on demand digital port output task in MAX. The GlobalDigitalPortOutput_USB.nce file in the example directory contains a simulated USB device and a task that you can import to MAX. Double-click the .nce file to launch MAX and then follow the installation directions.";
            // 
            // writeLabel
            // 
            this.writeLabel.AutoSize = true;
            this.writeLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.writeLabel.Location = new System.Drawing.Point(21, 40);
            this.writeLabel.Name = "writeLabel";
            this.writeLabel.Size = new System.Drawing.Size(61, 16);
            this.writeLabel.TabIndex = 3;
            this.writeLabel.Text = "Write Task:";
            // 
            // writeComboBox
            // 
            this.writeComboBox.Location = new System.Drawing.Point(103, 32);
            this.writeComboBox.Name = "writeComboBox";
            this.writeComboBox.Size = new System.Drawing.Size(172, 21);
            this.writeComboBox.TabIndex = 2;
            this.writeComboBox.SelectedIndexChanged += new System.EventHandler(this.writeComboBox_SelectedIndexChanged);
            // 
            // line6Switch
            // 
            this.line6Switch.Location = new System.Drawing.Point(72, 73);
            this.line6Switch.Name = "line6Switch";
            this.line6Switch.Size = new System.Drawing.Size(40, 70);
            this.line6Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line6Switch.TabIndex = 0;
            this.line6Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line5Switch
            // 
            this.line5Switch.Location = new System.Drawing.Point(128, 73);
            this.line5Switch.Name = "line5Switch";
            this.line5Switch.Size = new System.Drawing.Size(40, 70);
            this.line5Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line5Switch.TabIndex = 0;
            this.line5Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line4Switch
            // 
            this.line4Switch.Location = new System.Drawing.Point(184, 73);
            this.line4Switch.Name = "line4Switch";
            this.line4Switch.Size = new System.Drawing.Size(40, 70);
            this.line4Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line4Switch.TabIndex = 0;
            this.line4Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line3Switch
            // 
            this.line3Switch.Location = new System.Drawing.Point(240, 73);
            this.line3Switch.Name = "line3Switch";
            this.line3Switch.Size = new System.Drawing.Size(40, 70);
            this.line3Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line3Switch.TabIndex = 0;
            this.line3Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line2Switch
            // 
            this.line2Switch.Location = new System.Drawing.Point(296, 73);
            this.line2Switch.Name = "line2Switch";
            this.line2Switch.Size = new System.Drawing.Size(40, 70);
            this.line2Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line2Switch.TabIndex = 0;
            this.line2Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line1Switch
            // 
            this.line1Switch.Location = new System.Drawing.Point(352, 73);
            this.line1Switch.Name = "line1Switch";
            this.line1Switch.Size = new System.Drawing.Size(40, 70);
            this.line1Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line1Switch.TabIndex = 0;
            this.line1Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // line0Switch
            // 
            this.line0Switch.Location = new System.Drawing.Point(408, 73);
            this.line0Switch.Name = "line0Switch";
            this.line0Switch.Size = new System.Drawing.Size(40, 70);
            this.line0Switch.SwitchStyle = NationalInstruments.UI.SwitchStyle.VerticalToggle3D;
            this.line0Switch.TabIndex = 0;
            this.line0Switch.StateChanged += new NationalInstruments.UI.ActionEventHandler(this.lineSwitch_StateChanged);
            // 
            // readGroupBox
            // 
            this.readGroupBox.Controls.Add(this.ledLine7Label);
            this.readGroupBox.Controls.Add(this.ledLine0Label);
            this.readGroupBox.Controls.Add(this.readIndoLabel);
            this.readGroupBox.Controls.Add(this.readLabel);
            this.readGroupBox.Controls.Add(this.readComboBox);
            this.readGroupBox.Controls.Add(this.readButton);
            this.readGroupBox.Controls.Add(this.line7Led);
            this.readGroupBox.Controls.Add(this.line6Led);
            this.readGroupBox.Controls.Add(this.line5Led);
            this.readGroupBox.Controls.Add(this.line4Led);
            this.readGroupBox.Controls.Add(this.line3Led);
            this.readGroupBox.Controls.Add(this.line2Led);
            this.readGroupBox.Controls.Add(this.line1Led);
            this.readGroupBox.Controls.Add(this.line0Led);
            this.readGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.readGroupBox.Location = new System.Drawing.Point(40, 243);
            this.readGroupBox.Name = "readGroupBox";
            this.readGroupBox.Size = new System.Drawing.Size(745, 156);
            this.readGroupBox.TabIndex = 3;
            this.readGroupBox.TabStop = false;
            this.readGroupBox.Text = "Digital Read";
            // 
            // ledLine7Label
            // 
            this.ledLine7Label.AutoSize = true;
            this.ledLine7Label.Location = new System.Drawing.Point(27, 130);
            this.ledLine7Label.Name = "ledLine7Label";
            this.ledLine7Label.Size = new System.Drawing.Size(10, 16);
            this.ledLine7Label.TabIndex = 8;
            this.ledLine7Label.Text = "7";
            // 
            // ledLine0Label
            // 
            this.ledLine0Label.AutoSize = true;
            this.ledLine0Label.Location = new System.Drawing.Point(419, 130);
            this.ledLine0Label.Name = "ledLine0Label";
            this.ledLine0Label.Size = new System.Drawing.Size(10, 16);
            this.ledLine0Label.TabIndex = 7;
            this.ledLine0Label.Text = "0";
            // 
            // readIndoLabel
            // 
            this.readIndoLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.readIndoLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.readIndoLabel.Location = new System.Drawing.Point(475, 33);
            this.readIndoLabel.Name = "readIndoLabel";
            this.readIndoLabel.Size = new System.Drawing.Size(264, 96);
            this.readIndoLabel.TabIndex = 6;
            this.readIndoLabel.Text = @"Before running this application, make sure you have a USB device and an on demand digital port input task in MAX. The GlobalDigitalPortInput_USB.nce file in the example directory contains a simulated USB device and a task that you can import to MAX. Double-click the .nce file to launch MAX and then follow the installation directions.";
            // 
            // readLabel
            // 
            this.readLabel.AutoSize = true;
            this.readLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.readLabel.Location = new System.Drawing.Point(21, 46);
            this.readLabel.Name = "readLabel";
            this.readLabel.Size = new System.Drawing.Size(62, 16);
            this.readLabel.TabIndex = 3;
            this.readLabel.Text = "Read Task:";
            // 
            // readComboBox
            // 
            this.readComboBox.Location = new System.Drawing.Point(103, 38);
            this.readComboBox.Name = "readComboBox";
            this.readComboBox.Size = new System.Drawing.Size(172, 21);
            this.readComboBox.TabIndex = 2;
            this.readComboBox.SelectedIndexChanged += new System.EventHandler(this.readComboBox_SelectedIndexChanged);
            // 
            // readButton
            // 
            this.readButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.readButton.Location = new System.Drawing.Point(317, 38);
            this.readButton.Name = "readButton";
            this.readButton.TabIndex = 2;
            this.readButton.Text = "Read";
            this.readButton.Click += new System.EventHandler(this.readButton_Click);
            // 
            // line6Led
            // 
            this.line6Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line6Led.Location = new System.Drawing.Point(72, 94);
            this.line6Led.Name = "line6Led";
            this.line6Led.Size = new System.Drawing.Size(35, 35);
            this.line6Led.TabIndex = 1;
            // 
            // line5Led
            // 
            this.line5Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line5Led.Location = new System.Drawing.Point(128, 94);
            this.line5Led.Name = "line5Led";
            this.line5Led.Size = new System.Drawing.Size(35, 35);
            this.line5Led.TabIndex = 1;
            // 
            // line4Led
            // 
            this.line4Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line4Led.Location = new System.Drawing.Point(184, 94);
            this.line4Led.Name = "line4Led";
            this.line4Led.Size = new System.Drawing.Size(35, 35);
            this.line4Led.TabIndex = 1;
            // 
            // line3Led
            // 
            this.line3Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line3Led.Location = new System.Drawing.Point(240, 94);
            this.line3Led.Name = "line3Led";
            this.line3Led.Size = new System.Drawing.Size(35, 35);
            this.line3Led.TabIndex = 1;
            // 
            // line2Led
            // 
            this.line2Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line2Led.Location = new System.Drawing.Point(296, 94);
            this.line2Led.Name = "line2Led";
            this.line2Led.Size = new System.Drawing.Size(35, 35);
            this.line2Led.TabIndex = 1;
            // 
            // line1Led
            // 
            this.line1Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line1Led.Location = new System.Drawing.Point(352, 94);
            this.line1Led.Name = "line1Led";
            this.line1Led.Size = new System.Drawing.Size(35, 35);
            this.line1Led.TabIndex = 1;
            // 
            // line0Led
            // 
            this.line0Led.LedStyle = NationalInstruments.UI.LedStyle.Round3D;
            this.line0Led.Location = new System.Drawing.Point(408, 94);
            this.line0Led.Name = "line0Led";
            this.line0Led.Size = new System.Drawing.Size(35, 35);
            this.line0Led.TabIndex = 1;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(822, 445);
            this.Controls.Add(this.readGroupBox);
            this.Controls.Add(this.writeGroupBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainForm";
            this.Text = "Global Digital Port IO - USB";
            ((System.ComponentModel.ISupportInitialize)(this.line7Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line7Led)).EndInit();
            this.writeGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.line6Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line5Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line4Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line3Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line2Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line1Switch)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line0Switch)).EndInit();
            this.readGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.line6Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line5Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line4Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line3Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line2Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line1Led)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.line0Led)).EndInit();
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() 
        {
            Application.Run(new MainForm());
        }

        private void lineSwitch_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (!resettingSwitches)
            {
                try
                {
                    // Get the task name and load from MAX
                    string taskName = writeComboBox.SelectedItem.ToString();

                    using (Task digitalWriteTask = DaqSystem.Local.LoadTask(taskName))
                    {
                        // Get switch values
                        int[] dataArray = new int[8];
                        dataArray[0] = Convert.ToInt32(line0Switch.Value);
                        dataArray[1] = Convert.ToInt32(line1Switch.Value);
                        dataArray[2] = Convert.ToInt32(line2Switch.Value);
                        dataArray[3] = Convert.ToInt32(line3Switch.Value);
                        dataArray[4] = Convert.ToInt32(line4Switch.Value);
                        dataArray[5] = Convert.ToInt32(line5Switch.Value);
                        dataArray[6] = Convert.ToInt32(line6Switch.Value);
                        dataArray[7] = Convert.ToInt32(line7Switch.Value);

                        int dataValue = 0;

                        // Convert switch values (0/1) into a decimal value
                        for (int i = 0; i < 8; i++)
                        {
                            if (dataArray[i] == 1)
                                dataValue += Convert.ToInt32(Math.Pow(2, (double)i));
                        }

                        // Write data to the port
                        DigitalSingleChannelWriter writer = new DigitalSingleChannelWriter(digitalWriteTask.Stream);
                        writer.WriteSingleSamplePort(true, dataValue);

                    }
                }
                catch (DaqException ex)
                {
                    MessageBox.Show(ex.Message);
                    ResetSwitches();
                }
            }
        }

        private void readButton_Click(object sender, EventArgs e)
        {
            try
            {
                // Get the task name and load from MAX
                string taskName = readComboBox.SelectedItem.ToString();

                using (Task digitalReadTask = DaqSystem.Local.LoadTask(taskName))
                {
                    // Read data from the port
                    DigitalSingleChannelReader reader = new DigitalSingleChannelReader(digitalReadTask.Stream);
                    int dataValueRead = reader.ReadSingleSamplePortInt32();


                    // Check which bits of the read value are set to 1
                    bool[] dataArray = new bool[8];

                    for (int i = 0; i < 8; i++)
                    {
                        if (((dataValueRead >> i) & 1) == 1)
                            dataArray[i] = true;
                    }

                    // Display set bits
                    line0Led.Value = dataArray[0];
                    line1Led.Value = dataArray[1];
                    line2Led.Value = dataArray[2];
                    line3Led.Value = dataArray[3];
                    line4Led.Value = dataArray[4];
                    line5Led.Value = dataArray[5];
                    line6Led.Value = dataArray[6];
                    line7Led.Value = dataArray[7];
                }
            }
            catch (DaqException ex)
            {
                MessageBox.Show(ex.Message);
                ResetLeds();
            }
        }

        private void writeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetSwitches();
        }

        private void readComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ResetLeds();
        }

        private void ResetSwitches()
        {
            resettingSwitches = true;

            line0Switch.Value = false;
            line1Switch.Value = false;
            line2Switch.Value = false;
            line3Switch.Value = false;
            line4Switch.Value = false;
            line5Switch.Value = false;
            line6Switch.Value = false;
            line7Switch.Value = false;

            resettingSwitches = false;
        }

        private void ResetLeds()
        {
            line0Led.Value = false;
            line1Led.Value = false;
            line2Led.Value = false;
            line3Led.Value = false;
            line4Led.Value = false;
            line5Led.Value = false;
            line6Led.Value = false;
            line7Led.Value = false;
        }
    }
}
