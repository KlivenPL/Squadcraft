using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Squadcraft {
    public static class VPN {
        public static System.Diagnostics.Process openvpnProcess = null;
        public static string localIp;
        public static FileInfo openVpnExeFile;

        public static void InstallDrivers(bool install, Action OnExit) {
            FileInfo setupFile, driverFile;
            if (Environment.Is64BitOperatingSystem) {
                setupFile = Program.GetLocalFile(Program.mainDir + @"openvpn\tapinstallWin64.exe", false);
                driverFile = Program.GetLocalFile(Program.mainDir + @"driver\win64\OemWin2k.inf", false);
            } else {
                setupFile = Program.GetLocalFile(Program.mainDir + @"openvpn\tapinstallWin32.exe", false);
                driverFile = Program.GetLocalFile(Program.mainDir + @"driver\win32\OemWin2k.inf", false);
            }

            string args = "";
            if (install) {
                args = $"install \"{driverFile.FullName}\" tap0901";
            } else {
                args = "remove tap0901";
            }
            Program.RunProgram(setupFile, args, new string[] { "Drivers installed successfully" }, true, OnExit);
        }

        public static System.Diagnostics.Process ConnectToVpn() {
            return Program.RunProgram(openVpnExeFile, $"--config \"{Program.conf.openVpnConfFile}\"", new string[] { "complete", "TAP-Win32" }, false, null);
        }
        public static bool vpnConnected = false;
        public static void Connect() {
            Console.WriteLine("Installing drivers...");
            InstallDrivers(false, null);
            InstallDrivers(true, null);
            Console.WriteLine("Connecting to VPN...");
            openvpnProcess = ConnectToVpn();
            while (vpnConnected == false) {

            }
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Connected to VPN successfully");
            Console.ResetColor();
        }
    }
}
