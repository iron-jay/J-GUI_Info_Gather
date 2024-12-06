# J-GUI: Info Gather

-------------------

Not happy with either TSGui or UI++, I made my own one.

Introduing: J-GUI: Info Gather

![J-GUI](gui_unlocked.png)

Designed to be run in an MCM Task Sequence, here is a rundown of what it does and how it works:

### Hostname 

Queries the Win32_SystemEnclosure class for the value of SMBIOSAssetTag, and populates the hostname # field.<br>
If this is a VM, it populates from the _MachineName task sequence variable. If that is blank, it'll use the temp MININT- name.<br>
When <JGUI-regex> is defined as a TS variable, it will try and match the name in that text box with the pattern (except on a VM)<br>
If the pattern matches, the box is locked. If not, the box is unlocked.<br>
If the timer is going, it will be suspended, waiting for a correct entry.

### Model 

This value populates from the Model value of Win32_ComputerSystem, and display in the text box.<br>
It will also save the value as the Model Task Sequence variable.

If the build is a VM, it will pull from the Win32_ComputerSystem. If a VM pulls from Win32_SystemEnclosure, the string is to long to be added to a domain.

Holding Left Control and Left Shift while pressing submit will set the TS variable 'Override' to yes.<br>
This doesn't work if the buttons are held and the timer runs out.


### Make 

Grabs the Manufacturer value from Win32_ComputerSystem. It sets this value as the text in the textbox, as well as the Make Task Sequence variable.

If Make contains 'Microsoft' and Model contains 'Virtual', the Model value is overwritten to 'Hyper-V VM.<br>
If Make contains 'VMware', Model is set as 'VMware VM'<br>
If Make contains 'VirtualBox', Model is set as 'VirtualBox VM'<br>

In all these instances, Chassis is also set to 'VM'

### Chassis Type 

Gets ChassisType from Win32_SystemEnclosure, and translates value into plain meaning. Based on the result, it will set the Chassis Task Sequence variable to Mobile or Desktop.


### Build Type 

These radio buttons are populates by buildtypes defined in the variable <JGUI-buildtype>, Whatever is selected will set the BuildType Task Sequence Variable.<br>
This is formatted like so:
```
One,two,three
```

If this TS variable doesn't exist, the option just does not show up.


### Timeout 

If the asset number is valid, a timer will start when the app opens. Once complete, value set will be saved.<br>
The length of the timer is set in the variable <JGUI-timeout>. If no timer is defined, the words won't appear in the GUI, and there will be no timeout.<br>
Pressing the submit button does the exact same thing.

This is prevent task sequences being hung if someone kicks one off, then walks away.


## Usage 

By default, it will search for the variables, and populate the GUI based on that. When running from MCM, you should not need to put in any arguments (maybe other then the silent switch)<br>
If used outside of MCM, a dialog will show the results of submission.<br>
These exist anyway:<br>
-t, -testing: opens dialog to manually enter options for regex, timeout and buildtypes. (Can only be used outside of a task sequence).<br>
-s, -silent: submits make, model and chassis with no GUI appearing<br>
-h, -help, -?: throws up the help dialog<br>

## Testing

Putting in testing mode will open this:<br>
![J-GUI](test_empty.png)

Putting in details:<br>
![J-GUI](test_filled.png)

will fill out the main window like you had options in the task sequence set:<br>
![J-GUI](gui_all_locked.png)





