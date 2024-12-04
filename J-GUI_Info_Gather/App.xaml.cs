using System;
using System.Windows;
using System.Linq;
using System.Collections.Generic;
using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog;
using Serilog.Events;
using System.IO;


namespace J_GUI_Info_Gather
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hHandle);

        SplashScreen splashScreen = new SplashScreen();
        TS_Integration TS = new TS_Integration();

        public bool testing = false;
        bool silent = false;

        public string testingRegex;
        public int testingTimeout;
        public List<string> testingBuildtypes;

        public bool timeoutActive = false;
        public bool regexActive = false;
        public bool buildtypeActive = false;

        public string regex;
        public int timeout;
        public List<string> buildtypes;

        public string make;
        public string hostname;
        public string model;
        public string enclosure;
        public bool isVM;
        string chassis;

        public string submittedHostname;
        public string submittedBuildtype;

        private void ConfigureLogging()
        {
            // Get the directory of the current executable
            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;

            // Create log filename with current timestamp
            string logFilename = $"J-GUI_Info_Gather-{DateTime.Now:yyyy-MM-dd-HH-mm}.log";
            string logPath;
            if (TS.IsTSEnv())
            {
                logPath = Path.Combine(TS.GetTSVar("_SMSLogLocation"), logFilename);
            }
            else
            {
                logPath = Path.Combine(exeDirectory, logFilename);

            }


            // Configure Serilog
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(logPath,
                    rollingInterval: RollingInterval.Infinite, // Disable rolling
                    buffered: false) // Ensure immediate writes
                .WriteTo.Console()
                .CreateLogger();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ConfigureLogging();

            Log.Information("Application Starting. Version: {Version}, Machine: {MachineName}",
            System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
            Environment.MachineName);


            // Log startup arguments with more detail
            if (e.Args.Length > 0)
            {
                Log.Information("Startup Arguments Received: {ArgumentCount} - {Arguments}",
                    e.Args.Length,
                    string.Join(", ", e.Args.Select((arg, index) => $"[{index}]: {arg}")));
            }
            else
            {
                Log.Debug("No startup arguments provided");
            }

            splashScreen.Show();
            Log.Verbose("Splash screen displayed");

            // Determine target process based on arguments and environment
            string targetProcess = DetermineTargetProcess(e);
            Log.Information("Target Process Determined: {TargetProcess}", targetProcess);


            // Check if in correct session
            if (!IsInCorrectSession(targetProcess))
            {
                RestartInCorrectSession(targetProcess);
                Shutdown();
                return;
            }


            if (e.Args.Length == 0)
            {
                Log.Debug("No arguments provided. Checking environment.");
                if (!TS.IsTSEnv())
                {
                    Log.Information("Executing default startup procedure");
                    splashScreen.Visibility = Visibility.Hidden;
                    DoTheThing();
                }
                else
                {
                    Log.Information("Task Sequence environment detected. Killing TS Progress.");
                    TS.TSProgressKill();
                    splashScreen.Visibility = Visibility.Hidden;
                    DoTheThing();
                }
            }
            else if (e.Args.Length == 1)
            {
                if (e.Args[0].Equals(@"-testing") || e.Args[0].Equals(@"-t"))
                {
                    Log.Debug("Entering testing mode");
                    if (TS.IsTSEnv())
                    {
                        Log.Warning("Testing mode attempted in Task Sequence environment");
                        splashScreen.Visibility = Visibility.Hidden;
                        MessageBox.Show("Task Sequence Environment Detected.\nDon't use the flag '-testing' here.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        Environment.Exit(1);
                    }
                    else
                    {
                        testing = true;
                        splashScreen.Visibility = Visibility.Hidden;
                        Log.Information("Opening test dialog");
                        Test_Dialog test_dialog = new Test_Dialog();
                        test_dialog.ShowDialog();
                     
                        DoTheThing();
                    }

                }
                else if (e.Args[0].Equals(@"-silent") || e.Args[0].Equals(@"-s"))
                {
                    Log.Information("Entering silent mode");
                    silent = true;
                    DefineVariables();
                    SubmitTSVars();
                    Environment.Exit(0);

                }
                else if (e.Args[0].Equals("-help") || e.Args[0].Equals("-h") || e.Args[0].Equals("-?"))
                {
                    splashScreen.Visibility = Visibility.Hidden;
                    Log.Debug("Displaying help information");

                    string helpMessage = "J-GUI: Info Gather\n" +
                    "Provides frontend to get information on your task sequence.\n\n" +
                    "Usage:\n" +
                    "-t, -testing: opens dialog to manually enter options for regex, timeout and buildtypes. (Can only be used outside of a task sequence)\n" +
                    "-s, -silent: submits make, model and chassis with no GUI appearing\n" +
                    "-h, -help, -?: you're looking at it.\n\n" +
                    "Only one argument at a time.\n\n" +
                    "To use in a task sequence, add the variables <JGUI-regex>, <JGUI-timeout> and <JGUI-buildtypes>, with whatever values you want\n" +
                    "regex needs to start with ^ and end with $. timeout needs to be a number. buildtypes are seperated by a comma (with no space)\n\n" +
                    "Hold escape on when hitting submit on the main window to force a failout.\n" +
                    "Hold left shift and left control to set the variable 'override' to true";

                    MessageBox.Show(helpMessage, "J-GUI Help", MessageBoxButton.OK, MessageBoxImage.Information);

                    Environment.Exit(0);

                }
                else
                {
                    splashScreen.Visibility = Visibility.Hidden;
                    MessageBox.Show("Invalid Command Line Arguments.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            }
            else
            {
                Log.Error("Invalid number of arguments: {ArgumentCount}", e.Args.Length);
                splashScreen.Visibility = Visibility.Hidden;
                MessageBox.Show("Invalid Command Line Arguments.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }

        private void HandleRegex()
        {
            if (regexActive)
            {
                Log.Debug("Validating Regex: {Regex}", regex);
                if (!regex.StartsWith("^") && !regex.EndsWith("$"))
                {
                    Log.Error("Invalid regex string: {Regex}", regex);
                    MessageBox.Show("Invalid regex string.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
                else if (isVM)
                {
                    Log.Information("Regex disabled for virtual machine");
                    regexActive = false;
                }
                else
                {
                    Log.Information("Regex validation active");
                    regexActive = true;
                }
            }
        }

        public bool IsValidHostname()
        {
            bool isValid = System.Text.RegularExpressions.Regex.IsMatch(hostname, regex);
            return isValid;
        }

        public void DefineVariables()
        {
            Log.Debug("Defining variables. Testing mode: {TestingMode}", testing);
            if (!testing)
            {
                regex = TS.GetTSVar("JGUI-regex");
                Log.Information("Regex from TS: {Regex}", regex ?? "Not set");
                if (regex != null)
                {
                    regexActive = true;
                }
                try
                {
                    int.TryParse(TS.GetTSVar("JGUI-timeout"), out timeout);
                    if (timeout == 0)
                    {
                        timeoutActive = false;
                        MessageBox.Show($"{timeout}, {timeoutActive}", "Submission Successful", MessageBoxButton.OK);

                    }
                    else
                    {
                        timeoutActive = true;
                        MessageBox.Show($"{timeout}, {timeoutActive}", "Submission Successful", MessageBoxButton.OK);

                    }
                }
                catch
                {
                    MessageBox.Show("Couldn't convert the timeout varaible. Did you enter a number?", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }


                string buildtypeString = TS.GetTSVar("JGUI-buildtypes");
                if (buildtypeString != null)
                {
                    buildtypes = buildtypeString.Split(',').ToList();
                }
            }
            else
            {
                regex = testingRegex;
                timeout = testingTimeout;
                buildtypes = testingBuildtypes;
            }
            Log.Debug("Variables: Regex: {regex}, Timeout: {timeout}, Buildtypes: {buildtypes}", regex, timeout, buildtypes);

            DefineWMIProperties();

            HandleRegex();
            if (regexActive)
            {
                IsValidHostname();
            }
        }

        public void DoTheThing()
        {
            DefineVariables();
            splashScreen.Visibility = Visibility.Hidden;
            MainWindow mainWindow = new MainWindow();
            mainWindow.ShowDialog();
            SubmitTSVars();
            Environment.Exit(0);
        }


        //defines how the app will access local WMI
        public object WMIProperty(string classVal, string propertyVal)
        {
            Log.Debug("Querying WMI: Class = {Class}, Property = {Property}", classVal, propertyVal);
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM " + classVal))
                foreach (ManagementObject obj in searcher.Get())
                {
                    return obj[propertyVal];
                }
            Log.Warning("No result found for WMI query: Class = {Class}, Property = {Property}", classVal, propertyVal);
            return null;
        }
        //gets details from local WMI
        public void DefineWMIProperties()
        {
            Log.Debug("Starting WMI Property Definition");
            model = WMIProperty("Win32_ComputerSystem", "Model").ToString();
            make = WMIProperty("Win32_ComputerSystem", "Manufacturer").ToString();
            Log.Information("Initial WMI Computer System Properties: Make = {Make}, Model = {Model}", make, model);

            if (make.Contains("Microsoft") | model.Contains("Virtual"))
            {
                Log.Warning("Detected Hyper-V Virtual Machine");
                model = "Hyper-V VM";
                isVM = true;
            }
            else if (make.Contains("VMware"))
            {
                Log.Warning("Detected VMware Virtual Machine");
                model = "VMware VM";
                isVM = true;
            }
            else if (model.Contains("VirtualBox"))
            {
                Log.Warning("Detected VirtualBox Virtual Machine");
                model = "VirtualBox VM";
                isVM = true;
            }

            if (isVM)
            {
                hostname = TS.GetTSVar("_SMSTSMachineName");
                if (hostname == null)
                {
                    hostname = WMIProperty("Win32_ComputerSystem", "Name").ToString();
                    Log.Information("VM Hostname retrieved: {Hostname}", hostname);
                }
            }
            else
            {
                hostname = WMIProperty("Win32_SystemEnclosure", "SMBIOSAssetTag").ToString();
                Log.Information("Physical Machine Hostname retrieved: {Hostname}", hostname);
            }


            ushort[] chassisTypes = (ushort[])WMIProperty("Win32_SystemEnclosure", "ChassisTypes");
            Log.Debug("Chassis Types found: {ChassisTypes}", string.Join(", ", chassisTypes));
            foreach (ushort chassisType in chassisTypes)
            {
                enclosure = GetChassisTypeName(chassisType);
                Log.Verbose("Processed Chassis Type: {ChassisType} -> {Enclosure}", chassisType, enclosure);
            }

            if (enclosure.Contains("Desktop"))
            {
                chassis = "Desktop";
                Log.Debug("Chassis categorized as Desktop");
            }
            else if (enclosure.Contains("Mobile"))
            {
                chassis = "Mobile";
                Log.Debug("Chassis categorized as Mobile");
            }
            else if (isVM)
            {
                chassis = "VM";
                enclosure = "Virtual Machine";

                Log.Debug("Chassis categorized as Virtual Machine");
            }

        }
        //convert chassis type number into usable text
        public string GetChassisTypeName(ushort chassisType)
        {
            Log.Debug("Converting Chassis Type: {ChassisType}", chassisType);
            switch (chassisType)
            {
                case 3:
                    return "Desktop - Desktop";
                case 4:
                    return "Desktop - Low Profile";
                case 5:
                    return "Desktop - Pizza Box";
                case 6:
                    return "Desktop - Mini Tower";
                case 7:
                    return "Desktop - Tower";
                case 15:
                    return "Desktop - Space-saving";
                case 16:
                    return "Desktop - Lunch Box";
                case 8:
                    return "Mobile - Portable";
                case 9:
                    return "Mobile - Laptop";
                case 10:
                    return "Mobile - Notebook";
                case 11:
                    return "Mobile - Hand-Held";
                case 12:
                    return "Mobile - Docking Station";
                case 14:
                    return "Mobile - Sub Notebook";
                case 18:
                    return "Mobile - Expansion Chassis";
                case 21:
                    return "Mobile - Peripheral Chassis";
                case 30:
                    return "Mobile - Tablet";
                case 31:
                    return "Mobile - Convertible";
                case 32:
                    return "Mobile - Detachable";
                default:
                    return "Unknown";
            }
        }

        private void SubmitTSVars()
        {
            if (TS.IsTSEnv())
            {
                Log.Information("Submitting Task Sequence Variables: Chassis = {Chassis}, Model = {Model}, Make = {Make}", chassis, model, make);
                TS.SetTSVar("Chassis", chassis);
                TS.SetTSVar("Model", model);
                TS.SetTSVar("Make", make);

                if (!silent)
                {
                    Log.Debug("Submitting Additional Variables in Non-Silent Mode: Buildtype = {Buildtype}, OSDComputerName = {Hostname}", submittedBuildtype, submittedHostname);
                    TS.SetTSVar("Buildtype", submittedBuildtype);
                    TS.SetTSVar("OSDComputerName", submittedHostname);
                }
            }
            else
            {

                Log.Information("Not in Task Sequence Environment. Displaying Submission Details");
                if (silent)
                {
                    MessageBox.Show($"Model: {model}\nMake: {make}\nEnclosure: {enclosure}/{chassis}", "Submission Successful", MessageBoxButton.OK);
                }
                else if (submittedBuildtype == null)
                {
                    MessageBox.Show($"Hostname: {submittedHostname}\nModel: {model}\nMake: {make}\nEnclosure: {enclosure}/{chassis}", "Submission Successful", MessageBoxButton.OK);
                }
                else
                {
                    MessageBox.Show($"Hostname: {submittedHostname}\nModel: {model}\nMake: {make}\nEnclosure: {enclosure}/{chassis}\nBuild Type: {submittedBuildtype}", "Submission Successful", MessageBoxButton.OK);
                }
            }
        }
        private string DetermineTargetProcess(StartupEventArgs e)
        {
            // Default to Explorer
            string targetProcess = "explorer";

            // No arguments, in TS environment
            if (TS.IsTSEnv())
            {
                targetProcess = "TSProgressUI";
            }
            // Single argument is -testing, not in TS environment
            else
            {
                targetProcess = "explorer";
            }

            return targetProcess;
        }

        private bool IsInCorrectSession(string targetProcessName)
        {
            uint targetSessionId = GetSessionIdForProcess(targetProcessName);
            uint currentSessionId;
            ProcessIdToSessionId((uint)Process.GetCurrentProcess().Id, out currentSessionId);
            return targetSessionId == currentSessionId;
        }

        private uint GetSessionIdForProcess(string processName)
        {
            Process[] processes = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(processName));
            if (processes.Length > 0)
            {
                uint sessionId;
                if (ProcessIdToSessionId((uint)processes[0].Id, out sessionId))
                {
                    return sessionId;
                }
            }
            return uint.MaxValue;
        }

        private void RestartInCorrectSession(string targetProcessName)
        {
            string executablePath = Process.GetCurrentProcess().MainModule.FileName;
            ProcessLauncher.LaunchAppInSameSessionAs(targetProcessName + ".exe", executablePath);
        }

        public static class ProcessLauncher
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct STARTUPINFO
            {
                public int cb;
                public string lpReserved;
                public string lpDesktop;
                public string lpTitle;
                public uint dwX;
                public uint dwY;
                public uint dwXSize;
                public uint dwYSize;
                public uint dwXCountChars;
                public uint dwYCountChars;
                public uint dwFillAttribute;
                public uint dwFlags;
                public short wShowWindow;
                public short cbReserved2;
                public IntPtr lpReserved2;
                public IntPtr hStdInput;
                public IntPtr hStdOutput;
                public IntPtr hStdError;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct PROCESS_INFORMATION
            {
                public IntPtr hProcess;
                public IntPtr hThread;
                public uint dwProcessId;
                public uint dwThreadId;
            }

            [DllImport("kernel32.dll")]
            public static extern bool CreateProcess(
                string lpApplicationName,
                string lpCommandLine,
                IntPtr lpProcessAttributes,
                IntPtr lpThreadAttributes,
                bool bInheritHandles,
                uint dwCreationFlags,
                IntPtr lpEnvironment,
                string lpCurrentDirectory,
                ref STARTUPINFO lpStartupInfo,
                out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll")]
            private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

            [DllImport("kernel32.dll")]
            private static extern bool CloseHandle(IntPtr hObject);

            public const uint NORMAL_PRIORITY_CLASS = 0x00000020;
            public const uint CREATE_NEW_CONSOLE = 0x00000010;

            public static void LaunchAppInSameSessionAs(string targetProcessName, string appToLaunch)
            {
                uint targetSessionId = GetSessionIdForProcess(targetProcessName);

                if (targetSessionId == uint.MaxValue)
                {
                    throw new Exception($"Could not find process: {targetProcessName}");
                }

                STARTUPINFO si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(si);

                PROCESS_INFORMATION pi;

                bool success = CreateProcess(
                    null,
                    appToLaunch,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    NORMAL_PRIORITY_CLASS | CREATE_NEW_CONSOLE,
                    IntPtr.Zero,
                    null,
                    ref si,
                    out pi
                );

                if (success)
                {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to start process. Error code: {error}");
                }
            }

            private static uint GetSessionIdForProcess(string processName)
            {
                processName = System.IO.Path.GetFileNameWithoutExtension(processName);
                Process[] processes = Process.GetProcessesByName(processName);

                if (processes.Length > 0)
                {
                    uint sessionId;
                    if (ProcessIdToSessionId((uint)processes[0].Id, out sessionId))
                    {
                        return sessionId;
                    }
                }

                return uint.MaxValue;
            }
        }
    }
}
