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
3. Run the DSKY-Bridge.exe application (if asked, allow windows firewall) 
4. If the DSKY replica is configured properly, and the REENTRY is running, everything will work automatically. Enjoy!

Usage
- Use bottom right corner to resize the window if needed.
- Use the top right corner (over the screw head) to close the app.
- The top LCD display print the local IP address to help configuring the DSKY replica, if you want to change the default, click on the IP address, and choose from the drop-down list.
- The two barber pole indicators display the proper REENTRY incoming and outgoing communications. 
- The borrom IP address displays the IP address of the DSKY replica, if connected.
- The 3-way switch configures the role of the DSKY replica.
- The second DSKY IP address and role switch is only visible when a second device is connected.

Single DSKY connection:
<img width="1292" height="818" alt="image" src="https://github.com/user-attachments/assets/c9088b54-0cac-41d4-a159-3d24329997b4" />

IP selection (override the default choosen):
<img width="1271" height="807" alt="image" src="https://github.com/user-attachments/assets/d0811dff-68d3-4605-84f1-d04c41c5256f" />

Dual DSKY connection with different roles:
<img width="1298" height="1123" alt="image" src="https://github.com/user-attachments/assets/8e69bf98-f2fb-4293-b3ed-97ff868055ac" />

