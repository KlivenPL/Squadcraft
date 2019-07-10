using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Squadcraft {
    static class Program {

        public static Configuration conf;
        public static DirectoryInfo mainDir;
        public static FileInfo confFile;

        public static string serverIp = "";


        static void Main(string[] args) {
            mainDir = GetLocalDir(AppDomain.CurrentDomain.BaseDirectory, false);
            confFile = new FileInfo(mainDir.FullName + @"conf\conf.json");
            if (confFile.Exists == false) {
                Configuration.CreateConfigFile(confFile.FullName);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Generated conf.json file in \\conf folder. Please configure it and run Squadcraft again.");
                Console.ReadLine();
                Exit();
            }
            conf = Configuration.LoadConfig(confFile.FullName);
            conf.Init();

            VPN.openVpnExeFile = GetLocalFile(mainDir.FullName + @"openvpn\openvpn.exe", false);

            Database.Response dbResponse = Database.Response.unexpectedError;
            string responseStr = Database.GetResponse(out dbResponse);
            Action postResponseAction = null;

            switch (dbResponse) {
                case Database.Response.hostedReadyToJoin:
                    Console.WriteLine(responseStr.Split(':')[1].Substring(1) + responseStr.Split(':')[2]);
                    serverIp = responseStr.Split(':')[2];
                    postResponseAction += () => {
                        VPN.Connect();
                        bool run = true;
                        Console.CancelKeyPress += (x, d) => run = false;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Server is hosted at: {serverIp}");
                        Console.ResetColor();
                        Console.WriteLine("Press escape to exit.");
                        while (Console.ReadKey(true).Key != ConsoleKey.Escape) {
                        }
                        Console.WriteLine("Exitting...");
                        Exit();
                    };
                    break;
                case Database.Response.hostedInvalidKey:
                    ErrorExit($"Your join key {conf.serverJoinSecretKey} is invalid!");
                    break;
                case Database.Response.invalidHostKey:
                    ErrorExit($"Your host key {conf.serverHostSecretKey} is invalid!\nMake sure you are allowed to host this server.");
                    break;
                case Database.Response.readyToHost:
                    Console.WriteLine("Server is ready to host...");
                    postResponseAction += () => {
                        StartHosting();
                    };
                    break;
                case Database.Response.hostUnexpectedError:
                    ErrorExit("Unexpected response from database.");
                    break;
                case Database.Response.hostUpdate:
                    Console.WriteLine("Server is ready to host...");
                    postResponseAction += () => {
                        StartHosting();
                    };
                    break;
                case Database.Response.serverDoesNotExist:
                    ErrorExit($"Server {conf.serverName} does not exist.");
                    break;
                case Database.Response.unexpectedError:
                    ErrorExit("Unexpected response from database.");
                    break;
                case Database.Response.sessionNotClosedProperly:
                    string previousHoster = responseStr.Split('*')[1].Split('*')[0];
                    if (previousHoster == conf.gitUsername) {
                        ErrorExit("Previous session was not closed properly. Trying to fix that...", false);
                        postResponseAction += () => {
                            StartHosting(true);
                        };
                    } else {
                        ErrorExit($"Previous session was not closed properly by {previousHoster}.\nHe must run Squadcraft again before you can continue.");
                    }
                    break;
                default:
                    ErrorExit("Unexpected response from database.");
                    break;
            }

            Thread.Sleep(1500);
            postResponseAction();

        }

        public static bool updateDatabaseTask = false;
        static void StartHosting(bool repair = false) {
            conf.gitLocalDir = Program.GetLocalDir(conf.gitLocalPath, false);
            Git.GitInit(conf.gitLocalDir, repair);
            conf.serverStartFile = GetLocalFile(conf.serverStartFilePath, false);
            VPN.Connect();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Program.serverIp = VPN.localIp;
            Console.WriteLine("The Server is running on: " + serverIp);
            Console.ResetColor();
            updateDatabaseTask = true;
            new Task(() => {
                while (updateDatabaseTask) {
                    Database.Response dbResponse = Database.Response.unexpectedError;
                    string responseStr = Database.GetResponse(out dbResponse);
                    Thread.Sleep(30000);
                }
            }).Start();
            RunProgram(conf.serverStartFile, "", null, true, null, true);
            updateDatabaseTask = false;
            if (Git.CommitAllChanges($"Update by {conf.gitUsername} at {DateTime.Now.ToString()}")) {
                Git.PushCommits(conf.gitRemotePath);
                Console.WriteLine("Project updated successfully");
            }
            {
                Database.Response dbResponse = Database.Response.unexpectedError;
                string responseStr = Database.GetResponse(out dbResponse, true);
                if (dbResponse == Database.Response.closedSuccessfully)
                    Console.WriteLine("Session closed successfully.");
                else
                    ErrorExit("Could not close session. Please run Squadcraft again later.", false);
            }
            Exit();
        }


        public static DirectoryInfo GetLocalDir(string path, bool create) {
            DirectoryInfo dir = new DirectoryInfo(path);
            if (dir.Exists)
                return dir;
            if (create) {
                dir.Create();
                return dir;
            } else {
                ErrorExit($"Directory {path} does not exits.");
                return null;
            }
        }

        public static FileInfo GetLocalFile(string path, bool create) {
            FileInfo file = null;
            try {
                file = new FileInfo(path);
            } catch (Exception e) {
                ErrorExit($"Path: {path} " + e.Message);
                return null;
            }
            if (file.Exists)
                return file;
            if (create) {
                file.Create();
                return file;
            } else {
                ErrorExit($"File {path} does not exits.");
                return null;
            }
        }

        public static void ErrorExit(string message, bool exit = true) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("ERROR: " + message);
            if (exit) {
                Console.ReadKey();
                Exit();
            }
            Console.ResetColor();
        }

        public static void Exit() {
            if (VPN.openvpnProcess != null && IsProcessRunning(VPN.openvpnProcess))
                VPN.openvpnProcess.Kill();
            VPN.InstallDrivers(false, null);
            Environment.Exit(1);
        }

        public static bool IsProcessRunning(Process process) {
            if (process == null)
                throw new ArgumentNullException("process");

            try {
                Process.GetProcessById(process.Id);
            } catch (ArgumentException) {
                return false;
            }
            return true;
        }

        public static Process RunProgram(FileInfo file, string args, string[] outputFilters, bool waitForExitOnMainThread, Action OnExit, bool external = false) {
            Process process = new Process();
            ProcessStartInfo startInfo = new ProcessStartInfo();
            process.StartInfo = startInfo;
            if (external == false) {
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
                startInfo.UseShellExecute = false;
            }
            startInfo.FileName = file.FullName;
            startInfo.Arguments = args;
            startInfo.Verb = "runas";
            startInfo.ErrorDialog = false;
            if (external == false) {
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.RedirectStandardInput = true;

                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
                    // Prepend line numbers to each line of the output.
                    if (!string.IsNullOrEmpty(e.Data)) {
                        if (outputFilters == null)
                            Console.WriteLine(e.Data);
                        else
                            for (int i = 0; i < outputFilters.Length; i++) {
                                if (e.Data.ToLower().Contains(outputFilters[i].ToLower())) {
                                    if (e.Data.Contains("Notified TAP-Win32 driver to set a DHCP IP/netmask of ")) {
                                        Console.ForegroundColor = ConsoleColor.Cyan;
                                        VPN.localIp = e.Data.Split(new string[] { "Notified TAP-Win32 driver to set a DHCP IP/netmask of " }, StringSplitOptions.RemoveEmptyEntries)[1].Split('/')[0];
                                        Console.WriteLine("Your VPN IP is: " + VPN.localIp);
                                        Console.ResetColor();
                                    }
                                    try {
                                        if (process.ProcessName.ToLower().Contains("openvpn")) {
                                            if (i == 0) {
                                                VPN.vpnConnected = true;
                                                break;
                                            }
                                        }
                                    } catch { }
                                    Console.WriteLine(e.Data);
                                    break;
                                }
                            }
                    }
                });
                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data)) {
                        ErrorExit(e.Data, false);
                    }
                });
            }
            if (waitForExitOnMainThread == false) {
                ThreadStart ths = new ThreadStart(() => {
                    process.Start();
                    if (external == false) {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                    }
                    // process.WaitForExit();
                    //if (OnExit != null)
                    // OnExit();
                });
                Thread th = new Thread(ths);
                th.Start();
            } else {
                process.Start();
                if (external == false) {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                }
                process.WaitForExit();
                if (OnExit != null)
                    OnExit();
            }

            return process;

            /*string toWrite = "";
            /*if (showOutput && !string.IsNullOrWhiteSpace(toWrite = process.StandardOutput.ReadToEnd()))
                Console.WriteLine(toWrite);
            if (!string.IsNullOrWhiteSpace(toWrite = process.StandardError.ReadToEnd()))
                Program.ErrorExit(toWrite, false);*/


        }

    }
}
