###Axel tilt Introduction

Axel tilt is an application to control the tilt of a platform the navigator experiment sits on. Tilting the platform simulates acceleration of the experiment by using a component of earth acceleration: the resulting acceleration is proportional to the angle of the tilt. The actual mechanical control is implemented via two step motors with micro steps moving one side of the platform up and down. 
The software provides:
-	Means to adjust initial horizontal orientation of the platform, which is saved and retrieved after start.
-	Tilting manually the platform to a target tilt/acceleration or gradually.
-	Tilting the platform following pre-programmed patterns of behaviour in repeated manner.
-	The application can be connected to another application Axel Show in order to report its current position on demand
