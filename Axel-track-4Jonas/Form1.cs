using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.IO.Pipes;
using System.Diagnostics;
using CommandInterfaceXPS; // Newport Assembly .Net access
using NationalInstruments.Analysis;

namespace AxelTrackNS
{

    public partial class FormAxelTrack : Form
    {
        class PipeClient
        {
            public void Send(string SendStr, string PipeName, int TimeOut = 1000)
            {
                try
                {
                    NamedPipeClientStream pipeStream = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);

                    // The connect function will indefinitely wait for the pipe to become available
                    // If that is not acceptable specify a maximum waiting time (in ms)
                    pipeStream.Connect(TimeOut);
                    Debug.WriteLine("[Client] Pipe connection established > " + SendStr);

                    byte[] _buffer = Encoding.UTF8.GetBytes(SendStr);
                    pipeStream.BeginWrite(_buffer, 0, _buffer.Length, AsyncSend, pipeStream);
                }
                catch (TimeoutException oEX)
                {
                    Debug.WriteLine(oEX.Message);
                }
            }

            private void AsyncSend(IAsyncResult iar)
            {
                try
                {
                    // Get the pipe
                    NamedPipeClientStream pipeStream = (NamedPipeClientStream)iar.AsyncState;

                    // End the write
                    pipeStream.EndWrite(iar);
                    pipeStream.Flush();
                    pipeStream.Close();
                    pipeStream.Dispose();
                }
                catch (Exception oEX)
                {
                    Debug.WriteLine(oEX.Message);
                }
            }
        }

        private PipeClient _pipeClient = null;
        private bool PipeConnected = false;

        const int DEFAULT_TIMEOUT = 10000;
        const int POLLING_INTERVALLE_MS = 100; // Milliseconds
        const int NB_POSITIONERS = 1;

        string strAssemblyPath = string.Empty;
        string logINIPath = string.Empty;
        string LogINIFullPath = string.Empty;

        List<string> log = new List<string>();
        bool logFlag = true;

        CommandInterfaceXPS.XPS m_xpsInterface = null;           // Socket #1 (order)
        CommandInterfaceXPS.XPS m_xpsInterfaceForPolling = null; // Socket #2 (polling)

        string m_IPAddress;
        int      m_IPPort;
        bool     m_CommunicationOK;
        string   m_GroupName;
        string   m_PositionerName;
        bool     m_IsPositioner;
        double[] m_TargetPosition = new double[NB_POSITIONERS];
        double[] m_CurrentPosition = new double[NB_POSITIONERS];
        double[] m_CurrentVelocity = new double[NB_POSITIONERS];
        double[] m_CurrentAcceleration = new double[NB_POSITIONERS];
        int m_CurrentGroupStatus;
        int sweep, sweepCount;
        string   m_CurrentGroupStatusDescription;
        string   m_XPSControllerVersion;
        string   m_errorDescription;        

        int            m_PollingInterval;
        bool           m_pollingFlag;
        private Thread m_PollingThread;
        // status
        public delegate void ChangedCurrentGroupStateHandler(int currentGroupStatus, string description);
        private event ChangedCurrentGroupStateHandler m_CurrentGroupStateChanged;
        public event ChangedCurrentGroupStateHandler GroupStatusChanged
        {
            add { m_CurrentGroupStateChanged += value; }
            remove { m_CurrentGroupStateChanged -= value; }
        }
        // position
        public delegate void ChangedCurrentPositionHandler(double[] currentPositions);
        private event ChangedCurrentPositionHandler m_CurrentPositionChanged;
        public event ChangedCurrentPositionHandler PositionChanged
        {
            add { m_CurrentPositionChanged += value; }
            remove { m_CurrentPositionChanged -= value; }
        }
        // velocity
        public delegate void ChangedCurrentVelocityHandler(double[] currentVelocities);
        private event ChangedCurrentVelocityHandler m_CurrentVelocityChanged;
        public event ChangedCurrentVelocityHandler VelocityChanged
        {
            add { m_CurrentVelocityChanged += value; }
            remove { m_CurrentVelocityChanged -= value; }
        }
        // Acceleration
        public delegate void ChangedCurrentAccelerationHandler(double[] currentAccelerations);
        private event ChangedCurrentAccelerationHandler m_CurrentAccelerationChanged;
        public event ChangedCurrentAccelerationHandler AccelerationChanged
        {
            add { m_CurrentAccelerationChanged += value; }
            remove { m_CurrentAccelerationChanged -= value; }
        }
        // errorMessage
        public delegate void ChangedLabelErrorMessageHandler(string currentErrorMessage);
        private event ChangedLabelErrorMessageHandler m_ErrorMessageChanged;
        public event ChangedLabelErrorMessageHandler ErrorMessageChanged
        {
            add { m_ErrorMessageChanged += value; }
            remove { m_ErrorMessageChanged -= value; }
        }
        // ProgressBar
        public delegate void ChangedProgressBarHandler(int currentProgressBar);
        private event ChangedProgressBarHandler m_ProgressBarChanged;
        public event ChangedProgressBarHandler ProgressBarChanged
        {
            add { m_ProgressBarChanged += value; }
            remove { m_ProgressBarChanged -= value; }
        }
        // ProgressText
        public delegate void ChangedProgressTextHandler(int currentProgressText);
        private event ChangedProgressTextHandler m_ProgressTextChanged;
        public event ChangedProgressTextHandler ProgressTextChanged
        {
            add { m_ProgressTextChanged += value; }
            remove { m_ProgressTextChanged -= value; }
        }
        // UpdateButtons
        public delegate void ChangedUpdateButtonsHandler(bool currentUpdateButtons);
        private event ChangedUpdateButtonsHandler m_UpdateButtonsChanged;
        public event ChangedUpdateButtonsHandler UpdateButtonsChanged
        {
            add { m_UpdateButtonsChanged += value; }
            remove { m_UpdateButtonsChanged -= value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public FormAxelTrack()
        {
            InitializeComponent();
            // pipe client 
            _pipeClient = new PipeClient(); // commment this line to disable pipe comm.
            PipeConnected = (_pipeClient != null);  

            //log
            strAssemblyPath = System.IO.Directory.GetParent(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)).FullName;
            logINIPath = "\\data\\";
            LogINIFullPath = strAssemblyPath + logINIPath; 

            // Initialization
            label_MessageCommunication.ForeColor = Color.Red;
            label_MessageCommunication.Text = string.Format("Disconnected from XPS");
            m_IsPositioner = false;
            m_CommunicationOK = false;
            m_pollingFlag = false;
            m_PollingInterval = POLLING_INTERVALLE_MS; // milliseconds
            m_CurrentGroupStatus = 0;
            for (int i = 0; i < NB_POSITIONERS; i++)
                m_TargetPosition[i] = 0;

            // Events
            if (this != null)
            {
                this.PositionChanged += new ChangedCurrentPositionHandler(CurrentPositionHandlerChanged);
                this.VelocityChanged += new ChangedCurrentVelocityHandler(CurrentVelocityHandlerChanged);
                this.AccelerationChanged += new ChangedCurrentAccelerationHandler(CurrentAccelerationHandlerChanged);
 
                this.GroupStatusChanged += new ChangedCurrentGroupStateHandler(CurrentGroupStateHandlerChanged);
                this.ErrorMessageChanged += new ChangedLabelErrorMessageHandler(ErrorMessageHandlerChanged);
                this.ProgressBarChanged += new ChangedProgressBarHandler(ProgressBarHandlerChanged);
                this.ProgressTextChanged += new ChangedProgressTextHandler(ProgressTextHandlerChanged);
                this.UpdateButtonsChanged += new ChangedUpdateButtonsHandler(UpdateButtonsHandlerChanged);               
            }
        }
        
        private void CurrentPositionHandlerChanged(double[] currentValues)
        {
            string strPosition = currentValues[0].ToString("F2", CultureInfo.CurrentCulture.NumberFormat);
            textBoxPosition.BeginInvoke(
                   new Action(() =>
                   {
                       textBoxPosition.Text = strPosition;
                   }
                ));
        }

        private void CurrentVelocityHandlerChanged(double[] currentValues)
        {
            string strVelocity = currentValues[0].ToString("F2", CultureInfo.CurrentCulture.NumberFormat);
            textBoxVelocity.BeginInvoke(
                   new Action(() =>
                   {
                       textBoxVelocity.Text = strVelocity;
                   }
                ));
        }

        private void CurrentAccelerationHandlerChanged(double[] currentValues)
        {
            string strAcceleration = currentValues[0].ToString("F2", CultureInfo.CurrentCulture.NumberFormat);
            textBoxAcceleration.BeginInvoke(
                   new Action(() =>
                   {
                       textBoxAcceleration.Text = strAcceleration;
                   }
                ));
        }

        private void CurrentGroupStateHandlerChanged(int currentGroupStatus, string strGroupStatusDescription)
        {
            try
            {
                string strStatus = currentGroupStatus.ToString("F0", CultureInfo.CurrentCulture.NumberFormat);                               
                textBoxStatus.BeginInvoke(
                   new Action(() =>
                   {
                       textBoxStatus.Text = strStatus;
                   }
                ));
                label_GroupStatusDescription.BeginInvoke(
                   new Action(() =>
                   {
                       label_GroupStatusDescription.Text = strGroupStatusDescription;
                   }
                ));           
            }
            catch (Exception ex)
            {
                label_GroupStatusDescription.Text = "Exception in CurrentGroupStateHandlerChanged: " + ex.Message; // DEBUG
            }
        }

        private void ErrorMessageHandlerChanged(string Message)
        {            
            label_ErrorMessage.BeginInvoke(
               new Action(() =>
               {
                   label_ErrorMessage.Text = Message;
               }
            ));
        }

        private void ProgressBarHandlerChanged(int currentProgressBar)
        {
            progressBar1.BeginInvoke(
               new Action(() =>
               {
                   progressBar1.Value = currentProgressBar;
               }
            ));
        }
        private void ProgressTextHandlerChanged(int currentProgressText)
        {
            label_progressText.BeginInvoke(
               new Action(() =>
               {
                   label_progressText.Text = "[" + currentProgressText.ToString() + "]";
               }
            ));
        }

        private void UpdateButtonsHandlerChanged(bool currentUpdateButtons)
        {
            buttonStartShuttle.BeginInvoke(
               new Action(() =>
               {
                   buttonStartShuttle.Enabled = !currentUpdateButtons;
               }
            ));
            buttonStopShuttle.BeginInvoke(
               new Action(() =>
               {
                   buttonStopShuttle.Enabled = currentUpdateButtons;
               }
            ));
            buttonMoveTo.BeginInvoke(
               new Action(() =>
               {
                   buttonMoveTo.Enabled = !currentUpdateButtons;
               }
            ));
            textBoxSweepCount.BeginInvoke(
               new Action(() =>
               {
                   textBoxSweepCount.Enabled = !currentUpdateButtons;
               }
            ));
        }
        // end of events handlers

        public void UpdateGroupStatus()
        {
            try
            {
                int lastGroupState = m_CurrentGroupStatus;
                if (m_xpsInterfaceForPolling != null)
                {
                    string errorString = string.Empty;
                    int result = m_xpsInterfaceForPolling.GroupStatusGet(m_GroupName, out m_CurrentGroupStatus, out errorString);
                    if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                    {
                        m_CurrentGroupStatus = 0;
                        if (errorString.Length > 0)
                        {
                            int errorCode = 0;
                            int.TryParse(errorString, out errorCode);
                            m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                            m_ErrorMessageChanged(string.Format("GroupStatusGet ERROR {0}: {1}", result, m_errorDescription));
                        }
                        else
                            m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupStatusGet "));
                    }
                    else
                        result = m_xpsInterfaceForPolling.GroupStatusStringGet(m_CurrentGroupStatus, out m_CurrentGroupStatusDescription, out errorString);

                    if ((m_CurrentGroupStatus != lastGroupState) && m_CurrentGroupStateChanged != null)
                        m_CurrentGroupStateChanged(m_CurrentGroupStatus, m_CurrentGroupStatusDescription);
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in UpdateGroupStatus: " + ex.Message);
            }
        }

        public void UpdateCurrentPosition()
        {
            try
            {
                double lastCurrentPosition = m_CurrentPosition[0];
                if (m_xpsInterfaceForPolling != null)
                {
                    if (m_IsPositioner == true)
                    {
                        string errorString = string.Empty;
                        int result = m_xpsInterfaceForPolling.GroupPositionCurrentGet(m_PositionerName, out m_CurrentPosition, NB_POSITIONERS, out errorString);
                        if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                        {
                            m_CurrentPosition[0] = 0;
                            if (errorString.Length > 0)
                            {
                                int errorCode = 0;
                                int.TryParse(errorString, out errorCode);
                                m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                                m_ErrorMessageChanged(string.Format("GroupPositionCurrentGet ERROR {0}: {1}", result, m_errorDescription));
                            }
                            else
                                m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupPositionCurrentGet "));
                        }
                    }
                    if ((m_CurrentPosition[0] != lastCurrentPosition) && m_CurrentPositionChanged != null)
                        m_CurrentPositionChanged(m_CurrentPosition);
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in UpdateCurrentPosition: " + ex.Message);
            }
        }
        private double LastVelocity, EstimAcceleration, tm;
        public void UpdateCurrentVelocity()
        {
            try
            {
                double lastCurrentVelocity = m_CurrentVelocity[0];
                if (m_xpsInterfaceForPolling != null)
                {
                    if (m_IsPositioner == true)
                    {
                        string errorString = string.Empty;
                        int result = m_xpsInterfaceForPolling.GroupVelocityCurrentGet(m_PositionerName, out m_CurrentVelocity, NB_POSITIONERS, out errorString);
                        if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                        {
                            m_CurrentVelocity[0] = 0;
                            if (errorString.Length > 0)
                            {
                                int errorCode = 0;
                                int.TryParse(errorString, out errorCode);
                                m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                                m_ErrorMessageChanged(string.Format("GroupVelocityCurrentGet ERROR {0}: {1}", result, m_errorDescription));
                            }
                            else
                                m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupVelocityCurrentGet "));
                        }
                    }

                    if ((m_CurrentVelocity[0] != lastCurrentVelocity) && m_CurrentVelocityChanged != null)
                    {
                        m_CurrentVelocityChanged(m_CurrentVelocity);
                        if (DateTime.Now.TimeOfDay.TotalMilliseconds != tm)
                        EstimAcceleration = 1000 * (m_CurrentVelocity[0] - LastVelocity)/(DateTime.Now.TimeOfDay.TotalMilliseconds - tm);
                        LastVelocity = m_CurrentVelocity[0];
                        tm = DateTime.Now.TimeOfDay.TotalMilliseconds;   
                    }                       
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in UpdateCurrentVelocity: " + ex.Message);
            }
        }

        public void UpdateCurrentAcceleration()
        {
            try
            {                
                if (m_xpsInterfaceForPolling != null)
                {
                    if (m_IsPositioner == true)
                    {
                        m_CurrentAcceleration[0] = EstimAcceleration;
                    }

                    if (m_CurrentAccelerationChanged != null)
                        m_CurrentAccelerationChanged(m_CurrentAcceleration);
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in UpdateCurrentAcceleration: " + ex.Message);
            }
        }

        public void UpdateCurrentAll()
        {
            UpdateGroupStatus();
            UpdateCurrentPosition();
            UpdateCurrentVelocity();
            UpdateCurrentAcceleration();
        }

        #region POLLING 
        public void StartPolling()
        {
            try
            {
                if (m_pollingFlag == false)
                {
                    m_pollingFlag = true; // Start polling

                    // Create thread and start it
                    m_PollingThread = new Thread(new ParameterizedThreadStart(poll));
                    m_PollingThread.IsBackground = true;
                    m_PollingThread.Start();
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in StartPolling: " + ex.Message);
            }
        }

        public void StopPolling()
        {
            try
            {
                m_pollingFlag = false; // Stop the polling
                if (m_PollingThread != null)
                    m_PollingThread.Abort();
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in StopPolling: " + ex.Message);
            }
        }
       
        private bool AdjustShuttleFlag = false;
        private const double maxSpeed = 700;
        private const double tol = 5;
        private void AdjustShuttleVelAccel()
        {           
            if (AdjustShuttleFlag) return; // already ON  
            if (sweep >= sweepCount) // the end of sweeps
            {
                StopShuttle();
                return; 
            } 

            AdjustShuttleFlag = true; sweep++;

            m_ProgressBarChanged((int)(100 * sweep / sweepCount));
            m_ProgressTextChanged(sweep);
            string errorString = string.Empty;
            double OffPosition = 0;

            double swing;
            double.TryParse(textBoxShuttleRange.Text, out swing);

            double accel;
            double.TryParse(textBoxShuttleAcceleration.Text, out accel);

            double maxVelo = Math.Sqrt(2 * (swing / 2) * accel);
            double phaseDuration = maxVelo / accel;
            if (maxVelo > maxSpeed)
            {
                MessageBox.Show("Exceeded Maximum Speed of " + maxSpeed.ToString());
                StopShuttle();
                return;
            }
            // call to Axel Boss
            int cyclesLeft = sweepCount - (sweep - 1);
            double d_PollingInterval = m_PollingInterval/1000.0;
            string prms = (4 * phaseDuration).ToString() + ';' + d_PollingInterval.ToString() + ';' + swing.ToString() + ';' + accel.ToString() + ';' + cyclesLeft.ToString();
            if ((cyclesLeft == sweepCount) && (PipeConnected)) // init the acquisition
            {
                _pipeClient.Send("ACQ>" + prms, "XPStrackPipe", 1000);
                //Thread.Sleep(500); // time to reset ADC
                //if (logFlag) m_xpsInterface.GatheringRun(50000, 10, out errorString);
            }

            if (PipeConnected) _pipeClient.Send("ACQ>" + prms, "XPStrackPipe", 2000);

            for (int ph = 1; ph < 5; ph++)
            {
                UpdateCurrentAll();
                double pst = m_CurrentPosition[0];

                double[] velos = new double[NB_POSITIONERS];
                double[] accels = new double[NB_POSITIONERS];

                switch (ph)
                {
                    case 1:
                        velos[0] = maxVelo;
                        OffPosition = Math.Abs(initPos - pst); // at the beginning
                        break;
                    case 2:
                        velos[0] = 0;
                        OffPosition = Math.Abs(initPos + (swing / 2) - pst); // in the middle
                        break;
                    case 3:
                        Thread.Sleep(m_PollingInterval); // for symmetry with the start
                        velos[0] = - maxVelo;
                        OffPosition = Math.Abs(initPos + swing - pst); // at the end of shuttle move
                        break;
                    case 4:
                        velos[0] = 0;
                        OffPosition = Math.Abs(initPos + (swing / 2) - pst); // in the middle on its way back
                        break;
                }
                if (OffPosition > tol) // Communication failure with XPS 
                {
                    m_ErrorMessageChanged(string.Format("Off position ERROR -> pos {0}: off {1}", pst, OffPosition));
                    emergency = true;
                    StopShuttle();
                }

                int result;
                accels[0] = accel;
                result = m_xpsInterface.GroupJogParametersSet(m_GroupName, velos, accels, NB_POSITIONERS, out errorString);
                if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                {
                    if (errorString.Length > 0)
                    {
                        int errorCode = 0;
                        int.TryParse(errorString, out errorCode);
                        m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                        m_ErrorMessageChanged(string.Format("GroupJogParametersSet ERROR {0}: {1} with {2} m/s {3} m/s^2", result, m_errorDescription, velos[0], accels[0]));
                    }
                    else
                        m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupJogParametersSet"));
                }
              /*  if (!ShuttleFlag && (ph == 4))
                {
                    UpdateCurrentAll();
                    break;
                } */                    
            } // ph loop
            // double CyclePeriod, double Distance, double Accel, int CyclesLeft
             AdjustShuttleFlag = false;            
        }

        public void poll(object obj)
        {
            try
            {
                while ((m_pollingFlag == true) && (m_CommunicationOK == true))
                {                                        
                    if (ShuttleFlag) AdjustShuttleVelAccel();
                    else UpdateCurrentAll();

                    // Tempo in relation to the polling frequency
                    Thread.Sleep(m_PollingInterval);
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in poll: " + ex.Message);
            }
        }
        #endregion

        #region Communication/Initiation
        /// <summary>
        /// Socket opening and start polling
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConnectButton(object sender, EventArgs e)
        {
            // Get IP address and Ip port from form front panel
            m_IPAddress = textBox_IPAddress.Text;
            int.TryParse(textBox_IPPort.Text, out m_IPPort);

            m_PositionerName = TextBox_Group.Text;
            int index = m_PositionerName.LastIndexOf('.');
            if (index != -1)
            {
                m_IsPositioner = true;
                m_GroupName = m_PositionerName.Substring(0, index);
                label_ErrorMessage.Text = string.Empty;
            }
            else
            {
                m_IsPositioner = false;
                m_GroupName = m_PositionerName;
                label_ErrorMessage.Text = "Must be a positioner name not a group name";
            }

            label_GroupStatusDescription.Text = string.Empty;
            m_XPSControllerVersion = string.Empty;
            m_errorDescription = string.Empty;

            try
            {
                // Open socket #1 to order
                if (m_xpsInterface == null)
                    m_xpsInterface = new CommandInterfaceXPS.XPS();
                if (m_xpsInterface != null)
                {
                    // Open socket
                    int returnValue = m_xpsInterface.OpenInstrument(m_IPAddress, m_IPPort, DEFAULT_TIMEOUT);
                    if (returnValue == 0)
                    {
                        string errorString = string.Empty;
                        int result = m_xpsInterface.FirmwareVersionGet(out m_XPSControllerVersion, out errorString);
                        if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                        {
                            if (errorString.Length > 0)
                            {
                                int errorCode = 0;
                                int.TryParse(errorString, out errorCode);
                                m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                                m_XPSControllerVersion = string.Format("FirmwareVersionGet ERROR {0}: {1}", result, m_errorDescription);
                            }
                            else
                                m_XPSControllerVersion = string.Format("Communication failure with XPS after FirmwareVersionGet ");
                        }
                        else
                        {
                            label_MessageCommunication.ForeColor = Color.Green;
                            label_MessageCommunication.Text = string.Format("Connected to XPS");
                            m_CommunicationOK = true;
                        }
                    }
                }
                else
                    m_XPSControllerVersion = "XPS instance is NULL";

                // Open socket #2 for polling
                if (m_xpsInterfaceForPolling == null)
                    m_xpsInterfaceForPolling = new CommandInterfaceXPS.XPS();
                if (m_xpsInterfaceForPolling != null)
                {
                    // Open socket
                    int returnValue = m_xpsInterfaceForPolling.OpenInstrument(m_IPAddress, m_IPPort, DEFAULT_TIMEOUT);
                    if (returnValue == 0)
                    {
                        string errorString = string.Empty;
                        int result = m_xpsInterfaceForPolling.FirmwareVersionGet(out m_XPSControllerVersion, out errorString);
                        if (result != CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                            StartPolling();
                    }
                }

                if (m_XPSControllerVersion.Length <= 0)
                    m_XPSControllerVersion = "No detected XPS";

                this.Text = string.Format("Axel Track for Jonas v1.3 - {0}", m_XPSControllerVersion);
            }
            catch (Exception ex)
            {
                label_ErrorMessage.Text = "Exception in ConnectButton: " + ex.Message;
            }
        }

        /// <summary>
        /// Stop polling and Close socket
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            try
            {
                m_CommunicationOK = false;
                m_pollingFlag = false;

                if (m_xpsInterfaceForPolling != null)
                    m_xpsInterfaceForPolling.CloseInstrument();

                if (m_xpsInterface != null)
                    m_xpsInterface.CloseInstrument();
                
                label_MessageCommunication.ForeColor = Color.Red;
                label_MessageCommunication.Text = string.Format("Disconnected from XPS");
                label_ErrorMessage.Text = string.Empty;
                label_GroupStatusDescription.Text = string.Empty;
                m_XPSControllerVersion = string.Empty;
                m_errorDescription = string.Empty;
                this.Text = "XPS Application";
            }
            catch (Exception ex)
            {
                label_ErrorMessage.Text = "Exception in buttonDisconnect_Click: " + ex.Message;
            }
        }

        /// <summary>
        /// Button to perform a GroupInitialize
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonInitialize_Click(object sender, EventArgs e)
        {
            try
            {
                label_ErrorMessage.Text = string.Empty;
                if (m_CommunicationOK == false)
                    label_ErrorMessage.Text = string.Format("Not connected to XPS");

                if (m_xpsInterface != null)
                {
                    string errorString = string.Empty;
                    int result = m_xpsInterface.GroupInitialize(m_GroupName, out errorString);
                    if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                    {
                        if (errorString.Length > 0)
                        {
                            int errorCode = 0;
                            int.TryParse(errorString, out errorCode);
                            m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                            label_ErrorMessage.Text = string.Format("GroupInitialize ERROR {0}: {1}", result, m_errorDescription);
                        }
                        else
                            label_ErrorMessage.Text = string.Format("Communication failure with XPS after GroupInitialize ");
                    }
                }
            }
            catch (Exception ex)
            {
                label_ErrorMessage.Text = "Exception in buttonInitialize_Click: " + ex.Message;
            }
        }

        /// <summary>
        /// Button to perform a group home search
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonHome_Click(object sender, EventArgs e)
        {
            try
            {
                label_ErrorMessage.Text = string.Empty;
                if (m_CommunicationOK == false)
                    label_ErrorMessage.Text = string.Format("Not connected to XPS");

                if (m_xpsInterface != null)
                {
                    string errorString = string.Empty;
                    int result = m_xpsInterface.GroupHomeSearch(m_GroupName, out errorString);

                    //int result = m_xpsInterface.GroupReferencingStart(m_GroupName, out errorString); // arbitrary home (manual position)
                    //result = m_xpsInterface.GroupReferencingStop(m_GroupName, out errorString);

                    if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                    {
                        if (errorString.Length > 0)
                        {
                            int errorCode = 0;
                            int.TryParse(errorString, out errorCode);
                            m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                            label_ErrorMessage.Text = string.Format("GroupHomeSearch ERROR {0}: {1}", result, m_errorDescription);
                        }
                        else
                            label_ErrorMessage.Text = string.Format("Communication failure with XPS after GroupHomeSearch ");
                    }
                }
            }
            catch (Exception ex)
            {
                label_ErrorMessage.Text = "Exception in buttonHome_Click: " + ex.Message;
            }
        }

        /// <summary>
        /// Button to perform a group kill
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonKill_Click(object sender, EventArgs e)
        {
            try
            {
                label_ErrorMessage.Text = string.Empty;
                if (m_CommunicationOK == false)
                    label_ErrorMessage.Text = string.Format("Not connected to XPS");

                if (m_xpsInterface != null)
                {
                    string errorString = string.Empty;
                    int result = m_xpsInterface.GroupKill(m_GroupName, out errorString);
                    if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
                    {
                        if (errorString.Length > 0)
                        {
                            int errorCode = 0;
                            int.TryParse(errorString, out errorCode);
                            m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                            label_ErrorMessage.Text = string.Format("GroupKill ERROR {0}: {1}", result, m_errorDescription);
                        }
                        else
                            label_ErrorMessage.Text = string.Format("Communication failure with XPS after GroupKill ");
                    }
                }
            }
            catch (Exception ex)
            {
                label_ErrorMessage.Text = "Exception in buttonKill_Click: " + ex.Message;
            }
        }

        private void buttonInitiate_Click(object sender, EventArgs e)
        {
            buttonInitiate.Text = "Doing it...";
            buttonInitiate.ForeColor = System.Drawing.Color.DarkGray;
            Application.DoEvents();
            if (m_CommunicationOK) buttonDisconnect_Click(null, null);
            ConnectButton(null, null);
            buttonKill_Click(null, null);
            buttonInitialize_Click(null, null);
            buttonHome_Click(null, null);

            buttonInitiate.Text = "Reset";
            buttonInitiate.ForeColor = System.Drawing.Color.DarkOrange;

            buttonAbort.ForeColor = System.Drawing.Color.Red;
            buttonAbort.Enabled = true;
        }

        private void buttonAbort_Click(object sender, EventArgs e)
        {
            string errorString = string.Empty;
            int result = m_xpsInterface.GroupMoveAbort(m_GroupName, out errorString);
            if (result == CommandInterfaceXPS.XPS.FAILURE) // Communication failure with XPS 
            {
                if (errorString.Length > 0)
                {
                    int errorCode = 0;
                    int.TryParse(errorString, out errorCode);
                    m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                    label_ErrorMessage.Text = string.Format("GroupMoveAbortFast ERROR {0}: {1}", result, m_errorDescription);
                }
                else
                    label_ErrorMessage.Text = string.Format("Communication failure with XPS after GroupMoveAbortFaste ");
            }
        }
        #endregion

        /// <summary>
        /// Button to perform an absolute motion
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonMoveTo_Click(object sender, EventArgs e)
        {
            try
            {
                label_ErrorMessage.Text = string.Empty;
                if (m_CommunicationOK == false)
                    label_ErrorMessage.Text = string.Format("Not connected to XPS");

                if (m_IsPositioner == true)
                {
                    if (sender == buttonMoveTo) double.TryParse(tbMoveToPos.Text, out m_TargetPosition[0]);
                    if (sender == buttonMoveToA) double.TryParse(tbMoveToPosA.Text, out m_TargetPosition[0]);
                    if (sender == buttonMoveToB) double.TryParse(tbMoveToPosB.Text, out m_TargetPosition[0]);
                    if ((m_xpsInterface != null) && (m_CommunicationOK == true))
                    {
                        string errorString = string.Empty; int result = 0;
                        double vel, accel, minJerk, maxJerk;
                        result = m_xpsInterface.PositionerSGammaParametersGet(m_PositionerName, out  vel, out accel, out minJerk, out maxJerk, out errorString);
                        double.TryParse(tbVelocity.Text, out vel); double.TryParse(tbAccel.Text, out accel); 
                        result += m_xpsInterface.PositionerSGammaParametersSet(m_PositionerName, vel, accel, minJerk, maxJerk, out errorString);
                        Thread.Sleep(500);
                        result += m_xpsInterface.GroupMoveAbsolute(m_PositionerName, m_TargetPosition, 1, out errorString);
                        if (result != CommandInterfaceXPS.XPS.SUCCESS) // Communication failure with XPS 
                        {
                            if (errorString.Length > 0)
                            {
                                int errorCode = 0;
                                if(int.TryParse(errorString, out errorCode))
                                {
                                    m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                                    m_ErrorMessageChanged(string.Format("GroupMoveAbsolute ERROR {0}: {1}", result, m_errorDescription));
                               }
                            }
                            else
                                m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupMoveAbsolute "));
                        }
                        result += m_xpsInterface.PositionerSGammaParametersSet(m_PositionerName, vel, accel, minJerk, maxJerk, out errorString);
                    }
                }
            }
            catch (Exception ex)
            {
                m_ErrorMessageChanged("Exception in buttonMoveTo_Click: " + ex.Message);
            }
        }

        private bool fShuttleFlag = false;
        private double initPos = 0;
        public bool ShuttleFlag
        {
            get
            {
                return fShuttleFlag;
            }
            set
            {
                fShuttleFlag = value;

                string errorString = string.Empty;
                int result = 0;
                if (value) // switching ON
                {
                    UpdateCurrentAll();
                    initPos = m_CurrentPosition[0];

                    result = m_xpsInterface.GroupJogModeEnable(m_GroupName, out errorString);
                }
                else // going OFF
                {
                    if (!emergency)
                    {
                    /*    double[] velos = new double[NB_POSITIONERS];
                        double[] accels = new double[NB_POSITIONERS];
                        velos[0] = 0;
                        accels[0] = 100;*/
                        Thread.Sleep(1000);
                        result += m_xpsInterface.GroupJogModeDisable(m_GroupName, out errorString);
                        if (result != CommandInterfaceXPS.XPS.SUCCESS) // Communication failure with XP
                        {
                            if (errorString.Length > 0)
                            {
                                int errorCode = 0;
                                int.TryParse(errorString, out errorCode);
                                m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                                m_ErrorMessageChanged(string.Format("GroupJogMode ERROR {0}: {1}", result, m_errorDescription));
                            }
                            else
                                m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupJogMode "));
                        }
                    }
                }
                m_UpdateButtonsChanged(value);
            }
        } 

        private void buttonStartShuttle_Click(object sender, EventArgs e)
        {
            // some useful funcs: GroupAccelerationCurrentGet, GroupVelocityCurrentGet, GroupJogParametersSet, PositionerAccelerationAutoScaling,
            // PositionerAnalogTrackingPositionParametersSet, PositionerAnalogTrackingVelocityParametersSet,
            // PositionerJogMaximumVelocityAndAccelerationGet, XYZSplineExecution, SetJogAcceleration
            logFlag = checkBoxLog.Checked;
            string errorString = string.Empty;

            // JOG - ON      
            int i; sweepCount = 2;
            if(int.TryParse(textBoxSweepCount.Text, out i)) sweepCount = i;
            sweep = 0;
            ShuttleFlag = true;

            if (logFlag)
            {
                string[] tp = new string[] { 
                    m_PositionerName + ".CurrentPosition", m_PositionerName + ".CurrentVelocity", m_PositionerName + ".CurrentAcceleration",
                    m_PositionerName + ".SetpointPosition", m_PositionerName + ".SetpointVelocity", m_PositionerName + ".SetpointAcceleration",                
                    m_PositionerName + ".FollowingError" };
                int result = m_xpsInterface.GatheringConfigurationSet(tp, out errorString);
                m_xpsInterface.GatheringRun(100000, 10, out errorString);
            }
        }

        bool emergency = false;
        private void StopShuttle()
        {
            int result = 0;
            string errorString = string.Empty;
            if (logFlag)
            {                
                result += m_xpsInterface.GatheringStop(out errorString);
                result += m_xpsInterface.GatheringStopAndSave(out errorString);

                ftp ftpClient = new ftp(@"ftp://192.168.0.254", "Administrator", "Administrator");
                LogINIFullPath = strAssemblyPath + logINIPath + DateTime.Now.ToString("yy-MM-dd_H-mm-ss");
                LogINIFullPath += ".log";
                ftpClient.download("Public/Gathering.dat", LogINIFullPath);
                Thread.Sleep(500);
                if (PipeConnected) _pipeClient.Send("FRF>" + LogINIFullPath, "XPStrackPipe", 1000);

                ftpClient = null;
            }
            if (emergency)
            {
                result += m_xpsInterface.GroupKill(m_GroupName, out errorString); // GroupMoveAbortFast, GroupMoveAbort
                if (result == CommandInterfaceXPS.XPS.SUCCESS) // Communication failure with XP
                {
                    if (errorString.Length > 0)
                    {
                        int errorCode = 0;
                        int.TryParse(errorString, out errorCode);
                        m_xpsInterface.ErrorStringGet(errorCode, out m_errorDescription, out errorString);
                        m_ErrorMessageChanged(string.Format("GroupKill ERROR {0}: {1}", result, m_errorDescription));
                    }
                    else
                        m_ErrorMessageChanged(string.Format("Communication failure with XPS after GroupKill "));
                }
                AdjustShuttleFlag = false;
            }
            do
            {
               Application.DoEvents(); // wait for current cycle to end if in the middle of it
            } while (AdjustShuttleFlag);

            ShuttleFlag = false; // go off jog
            emergency = false;
        }

        private void buttonStopShuttle_Click(object sender, EventArgs e)
        {
            sweep = sweepCount;
        }

        private void picTrain_Click(object sender, EventArgs e)
        {
            MessageBox.Show("     Axel Track v1.2 \n   by Teodor Krastev \nfor Imperial College, London", "About");
        }
     }
}

