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
            // Step 1: Find solution directory and build MovieManager.Tray as Self-Contained
            string solutionDirectory = Environment.CurrentDirectory;
            
            // Navigate up to find the solution directory
            while (!File.Exists(Path.Combine(solutionDirectory, "MovieManager.sln")))
            {
                var parentDir = Directory.GetParent(solutionDirectory);
                if (parentDir == null)
                {
                    Console.WriteLine("Could not find solution directory containing MovieManager.sln");
                    Console.WriteLine($"Current directory: {solutionDirectory}");
                    return;
                }
                solutionDirectory = parentDir.FullName;
            }
            Console.WriteLine($"Solution directory found: {solutionDirectory}");
            
            // Step 0: Build React Frontend
            Console.WriteLine("Building React Frontend...");
            string webProjectPath = $@"{solutionDirectory}\MovieManager.Web";
            string webSrcPath = $@"{webProjectPath}\src";
            
            // Check if required directories exist
            if (!Directory.Exists(webProjectPath))
            {
                throw new DirectoryNotFoundException($"Web project directory not found: {webProjectPath}");
            }
            
            if (!Directory.Exists(webSrcPath))
            {
                throw new DirectoryNotFoundException($"Web src directory not found: {webSrcPath}");
            }
            
            // Check if npm is installed
            ProcessStartInfo npmCheckInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c npm --version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            Process? npmCheckProcess = Process.Start(npmCheckInfo);
            if (npmCheckProcess == null)
            {
                throw new InvalidOperationException("Failed to start npm version check process.");
            }
            
            npmCheckProcess.WaitForExit();
            if (npmCheckProcess.ExitCode != 0)
            {
                throw new InvalidOperationException("npm is not installed or not available in PATH.");
            }
            
            // Check if node_modules exists
            string nodeModulesPath = Path.Combine(webProjectPath, "node_modules");
            if (!Directory.Exists(nodeModulesPath))
            {
                throw new DirectoryNotFoundException($"node_modules directory not found: {nodeModulesPath}. Please run 'npm install' in the MovieManager.Web directory first.");
            }
            
            // Run npm run build from the Web directory
            ProcessStartInfo npmBuildInfo = new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = "/c npm run build",
                WorkingDirectory = webProjectPath,
                UseShellExecute = false,
                CreateNoWindow = false, // Show output for visibility
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            
            Console.WriteLine($"Running npm run build in: {webProjectPath}");
            Process? npmBuildProcess = Process.Start(npmBuildInfo);
            if (npmBuildProcess == null)
            {
                throw new InvalidOperationException("Failed to start npm build process.");
            }
            
            string buildOutput = npmBuildProcess.StandardOutput.ReadToEnd();
            string buildError = npmBuildProcess.StandardError.ReadToEnd();
            npmBuildProcess.WaitForExit();
            
            if (npmBuildProcess.ExitCode != 0)
            {
                Console.WriteLine("npm build output:");
                Console.WriteLine(buildOutput);
                Console.WriteLine("npm build error:");
                Console.WriteLine(buildError);
                throw new InvalidOperationException("React frontend build failed.");
            }
            
            Console.WriteLine("React frontend built successfully.");
            
            Console.WriteLine("Building self-contained TrayApp...");
            string trayProjectPath = $@"{solutionDirectory}\MovieManager.TrayApp\MovieManager.TrayApp.csproj";
            
            // Use dotnet publish for self-contained deployment
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"publish \"{trayProjectPath}\" --configuration Release --runtime win-x64 --self-contained true /p:PublishSingleFile=false /p:PublishTrimmed=false /p:PublishReadyToRun=false /p:SatelliteResourceLanguages=en%3Bzh-Hans%3Bzh-Hant",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            Process? buildProcess = Process.Start(startInfo);
            if (buildProcess == null)
            {
                Console.WriteLine("Failed to start dotnet publish process.");
                return;
            }
            
            buildOutput = buildProcess.StandardOutput.ReadToEnd();
            buildError = buildProcess.StandardError.ReadToEnd();
            buildProcess.WaitForExit();
            
            if (buildProcess.ExitCode != 0)
            {
                Console.WriteLine("Failed to build TrayApp as self-contained.");
                Console.WriteLine("Output: " + buildOutput);
                Console.WriteLine("Error: " + buildError);
                return;
            }
            Console.WriteLine("TrayApp built successfully as self-contained.");


            // Step 2: Copy build folder to Tray publish folder
            string webBuildFolder = $@"{solutionDirectory}\MovieManager.Web\build";
            string trayPublishFolder = $@"{solutionDirectory}\MovieManager.TrayApp\bin\Release\netcoreapp3.1\win-x64\publish";
            CopyDirectory(webBuildFolder, $@"{trayPublishFolder}\build");

            // Step 3: Copy test lib folder to Tray publish folder
            string testLib = $@"{solutionDirectory}\TestingMovieLib";
            CopyDirectory(testLib, $@"{trayPublishFolder}\TestingMovieLib");

            // Step 4: Update appsettings.json in Tray publish folder
            string appSettingsPath = Path.Combine(trayPublishFolder, "appsettings.json");
            UpdateAppSettings(appSettingsPath, "WebAppDirectory", "build");

            // Step 5: Prepare output folders for new and returning users
            string timestamp = DateTime.Now.ToString("MMddyyyy_hhmmss");
            string? baseOutputPath = Path.GetDirectoryName(trayPublishFolder);
            if (baseOutputPath == null)
            {
                Console.WriteLine("Could not determine base output path.");
                return;
            }

            string newUserFolder = Path.Combine(baseOutputPath, $"MovieManager_NewUser_{timestamp}");
            string returningUserFolder = Path.Combine(baseOutputPath, $"MovieManager_ReturningUser_{timestamp}");

            Console.WriteLine("Creating distribution folders...");
            CopyDirectory(trayPublishFolder, newUserFolder);
            CopyDirectory(trayPublishFolder, returningUserFolder);

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
            string newUserConfig = Path.Combine(newUserFolder, "MovieManager.TrayApp.dll.config");
            string returningUserConfig = Path.Combine(returningUserFolder, "MovieManager.TrayApp.dll.config");

            if (File.Exists(newUserConfig))
                UpdateConfig(newUserConfig, "DatabaseLocation", "MovieDb.db");

            if (File.Exists(returningUserConfig))
                UpdateConfig(returningUserConfig, "DatabaseLocation", "MovieDb.db");

            // Step 9: Clean up temporary publish files in win-x64 directory
            Console.WriteLine("Cleaning up temporary build files...");
            CleanupPublishDirectory(Path.GetDirectoryName(trayPublishFolder));

            Console.WriteLine("Self-Contained Deployment complete:");
            Console.WriteLine($" - NewUser build created at: {newUserFolder}");
            Console.WriteLine($" - ReturningUser build created at: {returningUserFolder}");
            Console.WriteLine();
            Console.WriteLine("NOTE: These builds are self-contained and do not require .NET Core runtime to be installed on target machines.");
            Console.WriteLine("Users can run MovieManager.TrayApp.exe directly without installing any dependencies.");
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

            XmlNode? node = xmlDoc.SelectSingleNode($"//appSettings/add[@key='{key}']");
            if (node != null)
            {
                node.Attributes!["value"]!.Value = value;
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

                XmlNode? appSettingsNode = xmlDoc.SelectSingleNode("//appSettings");
                appSettingsNode?.AppendChild(element);
            }

            xmlDoc.Save(filePath);
        }

        static void CleanupPublishDirectory(string? winx64Directory)
        {
            if (winx64Directory == null || !Directory.Exists(winx64Directory))
                return;

            try
            {
                // Get all items in the win-x64 directory
                var allItems = Directory.GetFileSystemEntries(winx64Directory);
                
                foreach (string item in allItems)
                {
                    string itemName = Path.GetFileName(item);
                    
                    // Keep only the distribution folders (those containing "MovieManager_")
                    if (Directory.Exists(item) && itemName.StartsWith("MovieManager_"))
                    {
                        continue; // Keep distribution folders
                    }
                    
                    // Delete everything else
                    if (Directory.Exists(item))
                    {
                        Directory.Delete(item, true);
                    }
                    else if (File.Exists(item))
                    {
                        File.Delete(item);
                    }
                }
                
                Console.WriteLine($"Cleaned up temporary files in: {winx64Directory}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not clean up all temporary files: {ex.Message}");
            }
        }
    }
}
