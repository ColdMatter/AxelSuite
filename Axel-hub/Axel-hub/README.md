#### Axel hub Introduction

One way to make a software system more reliable is to distribute resources thru so called (loose coupling), so the central piece of software (MOTmaster) would not be affected by eventual bottleneck in some of visualization or data processing parts (Axel-hub) of the system. 
Axel hub is the principal data visualization, logging and processing hub for the quantum accelerometer. It receives raw data from MotMaster2 thru specially designed fast communication channel, visualize the signal (raw data), chart and log its trend. Another major part of Axel Hub is the control of MEMS measurements via 24 bit ADC (NI 9251).
Axel hub application can run independently from MOTmaster, but its main purpose by design is to work in tandem with MOTmaster providing visualization and some data processing features for the Navigator experiment. For the communication between these two a special communication channel has been written with speed in mind. The communication channel uses customizable part Windows messaging system and combines with JSON type of communication protocol described (as known internally) in (The Book of JaSON). The average transmitting time for a message (command) including interpretation is under 1 ms. For the aimed navigator cycle period of 100 ms, it is a less than 1 percent. 
Visually Axel-hub has 5 panels resizable by splitters. The panels represent different functionalities in groups.   

### Scan panel 
MEMS measurements are controllable by the top left panel. The actual control is on the ADC24 which 24 bit analogue-to-digital converter (National Instruments NI 9251). The user can set continuous or finite measurement with desired sampling frequency. The actual sampling frequency would be one from a list with pre-set frequencies. A buffer size can be set in seconds (Time Limit tab) or in number of points (Size limit tab). The buffer size will be the number of point measured in finite mode or the number of point taken at one shot in continuous mode. In later mode, there are no gaps in time between shots and the visualization is updated with every shot. 
Once the conditions for the measurement are set the user can start measuring by pressing the Start button. 
When Remote tab is active Axel hub acts as visualization/log hub for MOTmaster. In this case when the measurement is initiated by MOTmaster Axel-hub passively will show the data on corresponding chart. In another way to proceed is to execute a procedure (called Jumbo) from Axel hub. The first phase is Jumbo scan where Axel hub requires MOTmaster to make a scan of the interferometric fringes and shows the fringes in the lower panel on the right. Once the scan is finished Axel hub will ask the user to place one or two cursors on the side(s) of best fringe. After that the second phase (Jumbo repeat) will follow the movement of the fringe by the intensity of the signal at side(s) of the fringe. The algorithm of following is proportional:integral:derivative (PID) based. 
The log panel (left bottom) provides flexible ways to show communication command/data flow. In default mode, the log window will list only small (most informative) portion of each command/shot. In verbatim mode the complete data flow will be visible, but this could slow down the Axel-hub performance. 

### Axel-chart (top)
Axel-chart is a (user control) in terms of Visual Studio and it is a visual panel separated in two (the top two charts in the image). During a measurement, the top part shows incoming data, usually a relatively small porting of it. The idea is to be quick and check only for obvious inconsistencies of incoming data flow. When it is used offline, the panel allows the user to browse large data in convenient manner (piece by piece). 
The lower part consists of three charts and panel with tools for file and statistics. The first chart is Overview and it shows the whole spectrum so it could be sluggish if the size is bigger than 10k. Some tools for visual manipulation (and copy the pic) are available on this tab (as on others too). The second tab is Histogram:  it calculates a histogram of the spectrum and optionally the user can fit a Gaussian curve over the data. A useful feature here is Window mode (shown on Axel-hub image) which allow the user to select a portion of the histogram (when histogram is multi-mode) and fit the curve over only the selected part.  

The last tab provides number of features:
-	file operations (Open and Save) including a remark for particular measurement description.
-	Chart options as Xaxis units, and some others, including the maximum number of points kept (depth)
-	Calculating and displaying the current value and dispersion of MEMS measurement as taken for the last (Time slice)
-	Split data is designed to be used two level signal (as from an optical chopper) and split the spectrum in two: upper part and lower part.
-	Extract part, it extracts the visual part of the top chart and creates a new spectrum from it

### Signal panel (middle) contains two charts:
-	The right one shows the optical signal as it comes from photodiode detector of the experiment
-	The left chart provides the trends of N1, N2, N.total, N1.relative, and N2.relative; individually switchable.
-	The user can optionally correct for dark current or background as well as show/hide the standard deviations of the measurement
The last tab (Opt/Stat) provides file operations (Open and Save) for the trends chart series.

### Fringes / Acceleration panel (bottom)

This panel has two tabs with two charts (one each). 
The Fringes tab/chart provides visualization in case of MOTmaster scan, initiated either by MOTmaster (simple scan) or by Axel-hub (jumbo-scan). 
Accel.trend serves similar to Fringes purpose except it is for repeat, respectively repeat initiated by MOTmaster is called (simple repeat) and by Axel-hub: jumbo-repeat.
On the right are proportional.integral.derivative (PID) controller parameters controlling the phase correction extracted from intensity of the signal on the side(s) of the chosen fringe by PID. 
On the last tab (Opt/Stats) the features are: 
-	Jumbo procedure setting, as range and step for fringe scan and number of repeat cycles (negative value for continue)  
-	file operations (Open and Save) for Fringes data 
-	vibrations analysis, use the Navigator system to detect vibrations from the environment, mostly for testing the condition of the experiment 


In conclusion, the software provides:
-	The MEMS (classical) acceleration measurements could be done independently (stay alone) or in synchronization with MotMaster2 data flow. In any case the data can be chart (short term and long term) and histogram or FFT charts could be calculated and drawn in real time. Some tools for off line data processing are available too.   
-	The middle panel provides visualization of raw MotMaster2 data (the signal) and some signal  trends (N1, N2, Ntot, rel.N1 and rel.N2).
-	There are two major operating modes: Simple (Axel Hub is in slave position) and Jumbo (MotMaster2 is in slave position). 
-	In Simple operation mode the bottom panel is used for charting results: in scan mode: Fringes tab or repeat mode: Accel.Trend tab.
-	Another major feature is so called Jumbo mode, in this mode MotMaster2 is under Axel hub control providing first fringes pattern (scan of Raman phase) and then using PID algorithm following a chosen fringe position (pi flip procedure) in order to calculated the quantum acceleration. 
-	Finally, the quantum and the classical acceleration measurements are combined in one result acceleration value. All the accelerations are presented in a chart (Acce.Trend) and in a table. 
-	Optionally some of the intermediate results can be logged for later adjusting the processing parameters. 