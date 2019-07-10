using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Squadcraft {
    [Serializable]
    class Configuration {

        public string serverName = "", serverJoinSecretKey = "", serverHostSecretKey = "", serverStartFilePath = @"C:\path\to\server_start.exe/bat";
        public string hostStateResponseUrl = "http://yourPage.com/hostState.php";
        public string gitEmail = "git@email.com", gitUsername = "yourGitUsername", gitPassword = "pAsSwOrD";
        public string gitRemotePath = "https://github.com/yourName/your_git_project.git";
        public string gitLocalPath = @"C:\your_git_project_local_path\";
        //public string gitRepoName {
        //    get {
        //        string s = gitRemotePath;
        //        while (s.Contains('/'))
        //            s.Substring(s.IndexOf('/') + 1);
        //        return s.Split(new string[] { ".git" }, StringSplitOptions.RemoveEmptyEntries)[0];
        //    }
        //}

        [NonSerialized]
        public FileInfo openVpnConfFile;
        [NonSerialized]
        public DirectoryInfo gitLocalDir;
        [NonSerialized]
        public FileInfo serverStartFile;

        public void Init() {
            string defOvpnFilePath = Program.mainDir + @"conf\OVPN_FILE_NOT_FOUND.ovpn";
            foreach (FileInfo file in new DirectoryInfo(Program.mainDir.FullName + @"\conf").GetFiles()) {
                if (file.Extension == ".ovpn") {
                    defOvpnFilePath = file.FullName;
                    break;
                }
            }
            openVpnConfFile = Program.GetLocalFile(defOvpnFilePath, false);
        }

        public static Configuration LoadConfig(string path) {
            FileInfo configFile = Program.GetLocalFile(path, false);
            string configFileStr = null;
            try {
                using (TextReader textReader = new StreamReader(configFile.FullName)) {
                    configFileStr = textReader.ReadToEnd();
                    textReader.Close();
                }
            } catch (Exception ex) {
                Program.ErrorExit("Could not read config file: " + ex.Message);
                return null;
            }
            try {
                return JsonConvert.DeserializeObject<Configuration>(configFileStr);
            }catch(Exception e) {
                Program.ErrorExit("Could not parse configuration file: " + e.Message);
                return null;
            }
        }

        public static void CreateConfigFile(string path) {
            string configJson = JsonConvert.SerializeObject(new Configuration(), Formatting.Indented);
            try {
                using (TextWriter tw = new StreamWriter(path)) {
                    tw.Write(configJson);
                    tw.Close();
                }
            } catch (Exception ex) {
                Program.ErrorExit("Could not create configuration file: " + ex.Message);
            }
        }
    }
}
