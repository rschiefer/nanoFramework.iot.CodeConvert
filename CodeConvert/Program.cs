using System;
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

                var packagesReferences = new[]
                        {
                            new NugetPackages {
                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Gpio.csproj"" />",
                                NewProjectReferenceString = @"<Reference Include=""System.Device.Gpio""><HintPath>packages\nanoFramework.System.Device.Gpio.1.0.0-preview.38\lib\System.Device.Gpio.dll </HintPath ></Reference > 
<Reference Include=""System.Device.Spi""><HintPath>packages\nanoFramework.System.Device.Spi.1.0.0-preview.30\lib\System.Device.Spi.dll</HintPath ></Reference > ",
                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Gpio"" version=""1.0.0-preview.38"" targetFramework=""netnanoframework10"" />
<package id=""nanoFramework.System.Device.Spi"" version=""1.0.0-preview.30"" targetFramework=""netnanoframework10"" />"
                            },
                            //new NugetPackages {
                            //    OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Gpio.csproj"" />",
                            //    NewProjectReferenceString = @"<Reference Include=""System.Device.Gpio""><HintPath>packages\nanoFramework.System.Device.Gpio.1.0.0-preview.31\lib\System.Device.Gpio.dll </HintPath ></Reference > ",
                            //    PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Gpio"" version=""1.0.0-preview.31"" targetFramework=""netnanoframework10"" />"
                            //},
                            //new NugetPackages {
                            //    OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.Gpio.csproj"" />",
                            //    NewProjectReferenceString = @"<Reference Include=""System.Device.Spi""><HintPath>packages\nanoFramework.System.Device.Spi.1.0.0-preview.28\lib\System.Device.Spi.dll</HintPath ></Reference > ",
                            //    PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.Spi"" version=""1.0.0-preview.28"" targetFramework=""netnanoframework10"" />"
                            //},
                            new NugetPackages {
                                OldProjectReferenceString= @"<ProjectReference Include=""$(MainLibraryPath)System.Device.I2c.csproj"" />",
                                NewProjectReferenceString = @"<Reference Include=""System.Device.I2c""><HintPath>packages\nanoFramework.System.Device.I2c.1.0.1-preview.31\lib\System.Device.I2c.dll</HintPath><Private>True</Private></Reference>",
                                PackageConfigReferenceString = @"<package id=""nanoFramework.System.Device.I2c"" version=""1.0.0-preview.31"" targetFramework=""netnanoframework10"" />"
                            },
                        };
                var projectReplacements = packagesReferences.ToDictionary(x => x.OldProjectReferenceString, x => x.NewProjectReferenceString);
                projectReplacements.Add("BindingTemplateProject", projectName);

                var oldProjectFile = targetDirectoryInfo.GetFiles("*.csproj").FirstOrDefault();
                var oldProjectFileContents = File.ReadAllText(oldProjectFile.FullName);
                var packagesToAdd = packagesReferences.Where(x => oldProjectFileContents.Contains(x.OldProjectReferenceString)).Select(x => x.OldProjectReferenceString).ToArray();
                oldProjectFile.Delete();

                if (packagesToAdd.Any()) {
                    var projectReferencesString = packagesToAdd
                        .Select(x => packagesReferences.FirstOrDefault(r => r.OldProjectReferenceString == x).NewProjectReferenceString)
                                .Aggregate((seed, add) => $"{seed}\n{add}");
                    projectReplacements.Add("<!-- INSERT NEW REFERENCES HERE -->", projectReferencesString);
                }


                foreach (var file in targetDirectoryInfo.GetFiles())
                {
                    if (file.Name.Contains(".nfproj"))
                    {
                        file.MoveTo(file.FullName.Replace("BindingTemplateProject", projectName));

                        file.EditFile(projectReplacements);
                    }
                    if (file.Name == "packages.config" && packagesToAdd.Any())
                    {
                        var packageReferences = packagesReferences
                            .Where(x => packagesToAdd.Any(p => p == x.OldProjectReferenceString))
                            .Select(x => x.PackageConfigReferenceString);
                        if (packageReferences.Any())
                        {
                            var packageReferencesString = packageReferences
                                .Aggregate((seed, add) => $"{seed}\n{add}");
                            file.EditFile(new Dictionary<string, string>
                            {
                                { "<!-- INSERT NEW PACKAGES HERE -->", packageReferencesString },
                            });
                        }
                    }
                    if (file.Name.EndsWith(".cs"))
                    {
                        file.EditFile(new Dictionary<string, string>
                            {
                                { "stackalloc", "new" },
                                { "Span<byte>", "SpanByte" },
                            });
                    }
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
        public static string[] EditFile(this FileInfo sourceFile, Dictionary<string, string> replacements)
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
                        output.WriteLine(line);
                    }
                }

                sourceFile.Delete();
                new FileInfo(tempFilename).MoveTo(sourceFile.FullName);
            }
            return replacedKeys.ToArray();
        }
    }
}
