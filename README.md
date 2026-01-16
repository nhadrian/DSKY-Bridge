# DSKY-Bridge
External api-dsky DSKY device Bridge for REENTRY simulator
Compiled in .NET Framework 8.0
by NHAdrian

This project uses fonts from the DSKY-FONTS project @ https://github.com/ehdorrii/dsky-fonts
This project uses some code from the ReEntryUDP example project @ https://github.com/ReentryGame/ReentryUDP

This application is a one-click single executable, user friendly solution with GUI to connect to an external DSKY replica (https://github.com/Apollo-Simulation-Peripheral-Lab/DSKY).

Key features:
- works for both CM and LM, user selectable
- touch input friendly
- stand-alone executable, no need to install .NET Framework 8.0

Instructions:</br>
0. The local PC must be on the same local network than the DSKY replica, and the DSKY replica must be configured to open bridge connection to the local PC.
1. Run REENTRY game and enable json output in settings, suggested refresh rate is 10Hz.
2. Then fully load into your mission.
3. Run the DSKY-Bridge.exe application. 
4. If the DSKY replica is configured properly, and the REENTRY is running, everything will work automatically. Enjoy!

Usage
The top LCD display print the local IP address to help configuring the DSKY replica.
The two barber pole indicators display the proper REENTRY incoming and outgoing communications. 
The borrom IP address displays the IP address of the DSKY replica, if connected.
The 3-way switch configures the role of the DSKY replica.

<img width="1292" height="818" alt="image" src="https://github.com/user-attachments/assets/c9088b54-0cac-41d4-a159-3d24329997b4" />
