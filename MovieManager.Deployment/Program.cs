using System;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Xml;

namespace MovieManager.Deployment
{
    class Program
    {
        static void Main()
        {
            // Step 1: Build MovieManager.Tray
            string solutionDirectory = Environment.CurrentDirectory;
            for (int i = 0; i < 4; i++)
            {
                solutionDirectory = Directory.GetParent(solutionDirectory).FullName;
            }
            string trayProjectPath = $@"{solutionDirectory}\MovieManager.TrayApp\MovieManager.TrayApp.csproj";
            string msBuildPath = @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = msBuildPath,
                Arguments = $"{trayProjectPath} /p:Configuration=Release /p:Platform=\"Any CPU\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process buildProcess = Process.Start(startInfo);
            buildProcess.WaitForExit();


            // Step 2: Copy build folder to Tray build folder
            string webBuildFolder = $@"{solutionDirectory}\MovieManager.Web\build";
            string trayBuildFolder = $@"{solutionDirectory}\MovieManager.TrayApp\bin\Any CPU\Release\netcoreapp3.1";
            CopyDirectory(webBuildFolder, $@"{trayBuildFolder}\build");

            // Step 3: Copy test lib folder to Tray build folder
            string testLib = $@"{solutionDirectory}\TestingMovieLib";
            CopyDirectory(testLib, $@"{trayBuildFolder}\TestingMovieLib");

            // Step 4: Update appsettings.json in Tray build folder
            string appSettingsPath = Path.Combine(trayBuildFolder, "appsettings.json");
            UpdateAppSettings(appSettingsPath, "WebAppDirectory", "build");

            // Step 5: Prepare output folders for new and returning users
            string timestamp = DateTime.Now.ToString("MMddyyyy_hhmmss");
            string baseOutputPath = Path.GetDirectoryName(trayBuildFolder);

            string newUserFolder = Path.Combine(baseOutputPath, $"MovieManager_NewUser_{timestamp}");
            string returningUserFolder = Path.Combine(baseOutputPath, $"MovieManager_ReturningUser_{timestamp}");

            CopyDirectory(trayBuildFolder, newUserFolder);
            CopyDirectory(trayBuildFolder, returningUserFolder);

            // Step 6: Add DB for NewUser
            string dbSourcePath = $@"{solutionDirectory}\MovieManager.DB\MovieDb_Clean.db";
            string dbDestPathNewUser = Path.Combine(newUserFolder, "MovieDb.db");
            File.Copy(dbSourcePath, dbDestPathNewUser, true);

            // Step 7: Ensure ReturningUser has NO DB
            string dbDestPathReturningUser = Path.Combine(returningUserFolder, "MovieDb.db");
            if (File.Exists(dbDestPathReturningUser))
            {
                File.Delete(dbDestPathReturningUser);
            }

            // Step 8: Fix App.config in both copies
            string newUserConfig = Path.Combine(newUserFolder, "App.config");
            string returningUserConfig = Path.Combine(returningUserFolder, "App.config");

            if (File.Exists(newUserConfig))
                UpdateConfig(newUserConfig, "DatabaseLocation", "MovieDb.db");

            if (File.Exists(returningUserConfig))
                UpdateConfig(returningUserConfig, "DatabaseLocation", "MovieDb.db");

            Console.WriteLine("Deployment complete:");
            Console.WriteLine($" - NewUser build created at: {newUserFolder}");
            Console.WriteLine($" - ReturningUser build created at: {returningUserFolder}");
        }

        static void CopyDirectory(string sourceDir, string targetDir)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDir);
            FileSystemInfo[] fileSystemInfos = dir.GetFileSystemInfos();

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            foreach (FileSystemInfo fileSystemInfo in fileSystemInfos)
            {
                string sourcePath = fileSystemInfo.FullName;
                string targetPath = Path.Combine(targetDir, fileSystemInfo.Name);

                if (fileSystemInfo is DirectoryInfo)
                {
                    CopyDirectory(sourcePath, targetPath);
                }
                else
                {
                    File.Copy(sourcePath, targetPath, true);
                }
            }
        }

        static void UpdateAppSettings(string filePath, string key, string value)
        {
            string json = File.ReadAllText(filePath);
            JObject jObject = JObject.Parse(json);
            jObject["AppSettings"][key] = value;
            File.WriteAllText(filePath, jObject.ToString());
        }

        static void UpdateConfig(string filePath, string key, string value)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.Load(filePath);

            XmlNode node = xmlDoc.SelectSingleNode($"//appSettings/add[@key='{key}']");
            if (node != null)
            {
                node.Attributes["value"].Value = value;
            }
            else
            {
                XmlElement element = xmlDoc.CreateElement("add");
                XmlAttribute attributeKey = xmlDoc.CreateAttribute("key");
                attributeKey.Value = key;
                element.Attributes.Append(attributeKey);

                XmlAttribute attributeValue = xmlDoc.CreateAttribute("value");
                attributeValue.Value = value;
                element.Attributes.Append(attributeValue);

                XmlNode appSettingsNode = xmlDoc.SelectSingleNode("//appSettings");
                appSettingsNode.AppendChild(element);
            }

            xmlDoc.Save(filePath);
        }
    }
}
