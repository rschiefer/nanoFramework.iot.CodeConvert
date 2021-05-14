﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeConvert
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourceDirectory = @"D:\Source\Github\dotnet\iot\src\devices";
            var filePathFilters = new[] { "\\src\\devices\\" };
            var targetProjectTemplateName = "BindingTemplateProject";
            var outputDirectoryPath = "output";

            var outputDirectoryInfo = new DirectoryInfo(outputDirectoryPath);
            if (outputDirectoryInfo.Exists)
            {
                outputDirectoryInfo.Delete(true);
            }

            var targetProjectTemplateDirectory = Directory.GetDirectories("..\\..\\..\\..\\", targetProjectTemplateName, new EnumerationOptions { RecurseSubdirectories = true })
                .Select(x => new DirectoryInfo(x))
                .FirstOrDefault();
            Console.WriteLine($"targetProjectTemplateDirectory={targetProjectTemplateDirectory}");

            var sourceProjectFiles = Directory.GetFiles(sourceDirectory, "*.csproj", new EnumerationOptions { RecurseSubdirectories = true })
                .Where(x => filePathFilters.Any(d => x.Contains(d)))
                .Select(x => new FileInfo(x));
            foreach (var sourceProjectFile in sourceProjectFiles)
            {
                Console.WriteLine($"sourceProjectFile={sourceProjectFile}");
                var projectName = sourceProjectFile.Name.Replace(".csproj", string.Empty);
                var targetDirectory = $"{outputDirectoryPath}\\{projectName}";
                var targetDirectoryInfo = targetProjectTemplateDirectory.CopyDirectory(targetDirectory, new[] { ".user" });
                sourceProjectFile.Directory.CopyDirectory(targetDirectory);

                var nfNugetPackages = new[]
                        {
//                            new NugetPackages {
//                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Gpio.csproj"" />",
//                                NewProjectReferenceString = @"<Reference Include=""System.Device.Gpio""><HintPath>packages\nanoFramework.System.Device.Gpio.1.0.0-preview.38\lib\System.Device.Gpio.dll </HintPath ></Reference > 
//<Reference Include=""System.Device.Spi""><HintPath>packages\nanoFramework.System.Device.Spi.1.0.0-preview.30\lib\System.Device.Spi.dll</HintPath ></Reference > ",
//                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Gpio"" version=""1.0.0-preview.38"" targetFramework=""netnanoframework10"" />
//<package id=""nanoFramework.System.Device.Spi"" version=""1.0.0-preview.30"" targetFramework=""netnanoframework10"" />"
//                            },
                            new NugetPackages {
                                Namespace="System.Device.Gpio",
                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Gpio.csproj"" />",
                                NewProjectReferenceString = @"<Reference Include=""packages\nanoFramework.System.Device.Gpio.1.0.0-preview.31\lib\System.Device.Gpio.dll""></Reference > ",
                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Gpio"" version=""1.0.0-preview.31"" targetFramework=""netnanoframework10"" />"
                            },
                            new NugetPackages {
                                Namespace="System.Device.Spi",
                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Spi.csproj"" />",
                                NewProjectReferenceString = @"<Reference Include=""packages\nanoFramework.System.Device.Spi.1.0.0-preview.28\lib\System.Device.Spi.dll""></Reference > ",
                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Spi"" version=""1.0.0-preview.28"" targetFramework=""netnanoframework10"" />"
                            },
                            new NugetPackages {
                                Namespace="System.Device.I2c",
                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.I2c.csproj"" />",
                                NewProjectReferenceString = @"<Reference Include=""packages\nanoFramework.System.Device.I2c.1.0.1-preview.31\lib\System.Device.I2c.dll""></Reference>",
                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.I2c"" version=""1.0.1-preview.31"" targetFramework=""netnanoframework10"" />"
                            },
                        };

                var searches = nfNugetPackages.ToDictionary(x => x.Namespace, x => false);
                foreach (var file in targetDirectoryInfo.GetFiles("*.cs"))
                {
                    searches = file.EditFile(new Dictionary<string, string>
                        {
                            { "stackalloc", "new" },
                            { "Span<byte>", "SpanByte" },
                        }, searches);
                }

                // PROJECT FILE
                // Search for project references in old project file
                var oldProjectFile = targetDirectoryInfo.GetFiles("*.csproj").FirstOrDefault();
                var oldProjectFileContents = File.ReadAllText(oldProjectFile.FullName);
                var oldProjectReferences = nfNugetPackages.Where(x => oldProjectFileContents.Contains(x.Namespace)).Select(x => x.Namespace).ToArray();
                oldProjectFile.Delete();

                // Rename template project file
                var targetProjectFile = targetDirectoryInfo.GetFiles("*.nfproj").First();
                targetProjectFile.MoveTo(targetProjectFile.FullName.Replace("BindingTemplateProject", projectName));

                // Update project name and references in new project file
                var projectReplacements = new Dictionary<string, string> {
                    {"BindingTemplateProject", projectName }
                };
                var newProjectReferences = new List<string>();
                if (oldProjectReferences.Any())
                {
                    newProjectReferences.AddRange(oldProjectReferences.Select(x => nfNugetPackages.FirstOrDefault(r => r.Namespace == x).NewProjectReferenceString));
                }
                newProjectReferences.AddRange(nfNugetPackages.Where(x => searches.Any(s => s.Value && s.Key == x.Namespace)).Select(x => x.NewProjectReferenceString));
                var newProjectReferencesString = newProjectReferences.Aggregate((seed, add) => $"{seed}\n{add}");
                projectReplacements.Add("<!-- INSERT NEW REFERENCES HERE -->", newProjectReferencesString);
                targetProjectFile.EditFile(projectReplacements);

                // PACKAGES
                // Add nanoFramework nuget packages based on project references and references in the code
                var packagesFile = targetDirectoryInfo.GetFiles("packages.config").First();
                var packageReferences = nfNugetPackages
                    .Where(x => 
                        // references from the old project file
                        oldProjectReferences.Any(p => p == x.Namespace) || 
                        // references in c# files
                        searches.Any(s => s.Value && s.Key == x.Namespace))
                    .Select(x => x.PackageConfigReferenceString);
                if (packageReferences.Any())
                {
                    var packageReferencesString = packageReferences
                        .Aggregate((seed, add) => $"{seed}\n{add}");
                    packagesFile.EditFile(new Dictionary<string, string>
                        {
                            { "<!-- INSERT NEW PACKAGES HERE -->", packageReferencesString },
                        });
                }


                var solutionFileTemplate = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
VisualStudioVersion = 16.0.30413.136
MinimumVisualStudioVersion = 10.0.40219.1
[[ INSERT PROJECTS HERE ]]
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		[[ INSERT BUILD CONFIGURATIONS HERE ]]
	EndGlobalSection
EndGlobal";
                var solutionProjectTemplate = @"Project(""{11A8DD76-328B-46DF-9F39-F559912D0360}"") = ""nanoFrameworkIoT"", ""nanoFrameworkIoT.nfproj"", ""{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}""
EndProject";
                var solutionBuildConfigTemplate = @"{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Debug|Any CPU.Deploy.0 = Debug|Any CPU
		{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Release|Any CPU.Build.0 = Release|Any CPU
		{29BACBB9-C5B6-4BEF-AEEF-9AFE39B678D9}.Release|Any CPU.Deploy.0 = Release|Any CPU";

                var solutionProject = solutionProjectTemplate.Replace("nanoFrameworkIoT", projectName);
                var solutionFileContent = solutionFileTemplate.Replace("[[ INSERT PROJECTS HERE ]]", solutionProject);
                solutionFileContent = solutionFileContent.Replace("[[ INSERT BUILD CONFIGURATIONS HERE ]]", solutionBuildConfigTemplate);
                File.WriteAllText($"{targetDirectoryInfo.FullName}\\{projectName}.sln", solutionFileContent);


            }

            Console.ReadLine();
        }

    }
    public class NugetPackages
    {
        public string OldProjectReferenceString { get; set; }
        public string NewProjectReferenceString { get; set; }
        public string PackageConfigReferenceString { get; set; }
        public string Namespace { get; internal set; }
    }
    public static class DirectoryInfoExtensions
    {
        public static DirectoryInfo CopyDirectory(this DirectoryInfo sourceDirectory, string targetPath, string[] filePathFilters = null)
        {
            if (sourceDirectory.Exists)
            {
                var targetDirectory = Directory.CreateDirectory(targetPath);
                foreach (var file in sourceDirectory.GetFiles().Where(f => filePathFilters == null || filePathFilters.Any(filter => f.FullName.Contains(filter)) == false))
                {
                    file.CopyTo($"{targetDirectory.FullName}\\{file.Name}");
                }
                return targetDirectory;
            }
            return null;
        }
    }
    public static class FileInfoExtensions
    {
        public static Dictionary<string, bool> EditFile(this FileInfo sourceFile, Dictionary<string, string> replacements, Dictionary<string, bool> checkIfFound = null)
        {
            var replacedKeys = new List<string>();
            if (sourceFile.Exists)
            {
                var tempFilename = $"{sourceFile.FullName}.edited";
                using (var input = sourceFile.OpenText())
                using (var output = new StreamWriter(tempFilename))
                {
                    string line;
                    while (null != (line = input.ReadLine()))
                    {
                        foreach (var replacement in replacements)
                        {
                            if (line.Contains(replacement.Key))
                            {
                                line = line.Replace(replacement.Key, replacement.Value);
                                replacedKeys.Add(replacement.Key);
                            }
                        }

                        if (checkIfFound != null)
                        {
                            foreach (var check in checkIfFound)
                            {
                                if (line.Contains(check.Key))
                                {
                                    checkIfFound[check.Key] = line.Contains(check.Key);
                                }
                            }
                        }
                        output.WriteLine(line);
                    }
                }

                sourceFile.Delete();
                new FileInfo(tempFilename).MoveTo(sourceFile.FullName);
            }
            return checkIfFound;
        }
    }
}
