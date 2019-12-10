###Axel-probe Introduction

Axel Probe is designed as simulator of MotMaster2 application including the quantum (MOT) accelerometer. The main purpose is to test Axel Hub for communication, logging, visualizing and data processing abilities. 
 The software provides:
-	The simulation starts with generation of an acceleration pattern (repeatable). Then Axel Probe would simulate a signal which would come out of MotMaster2 and the MOT experiment and send that signal to Axel Hub
-	A number behavioural patterns are available, as well as adding Gaussian noise and variety of simulated signal disturbances as amplitude variation, etc.
-	In case of active feedback of Raman phase in order to follow the position of a fringe (from the atomic interferometer), Axel Probe will take into account the fed back Raman phase when the signal is simulated. For example: the PID algorithm (with pi flip) could be tested and optimized that way.
-	Most of the intermediate and resulting value of calculation (simulation) are visible in charts and some of them in the communication log. 
