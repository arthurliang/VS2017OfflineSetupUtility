//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

#tool "nuget:?package=NUnit.ConsoleRunner&version=3.8.0"
#tool "nuget:?package=OpenCover&version=4.6.519"
#tool "nuget:?package=ReportGenerator&version=3.1.2"
#tool "nuget:?package=JetBrains.ReSharper.CommandLineTools&version=2018.1.2"
#tool "nuget:?package=ReSharperReports&version=0.4.0"
#addin "nuget:?package=Cake.ReSharperReports&version=0.9.0"
#addin "nuget:?package=Cake.Sonar&version=1.1.0"
#tool "nuget:?package=MSBuild.SonarQube.Runner.Tool&version=4.3.0"


///////////////////////////////////////////////////////////////////////////////
// USER CUSTOM VARIABLES
///////////////////////////////////////////////////////////////////////////////

var defaultTarget = "Default";
var defaultConfiguration = "Release";
var defaultResharperThrow = "True";
var defaultNugetSource = EnvironmentVariable("NUGET_HOST_URL");
var defaultNugetApiKey = EnvironmentVariable("NUGET_HOST_API_KEY");

var nugetPackagesDirectoryPath = "./nugetPackage";
var testReportsDirectoryPath = "./TestResults";
var resharperReportsDirectoryName = "ReSharperReports";

var solutionFilter =
            GetFiles("./packages/**/*.sln") +
            GetFiles("./tools/**/*.sln");

var projectFilter =
            GetFiles("./packages/**/*.csproj") +
            GetFiles("./tools/**/*.csproj");

var unitTestAssemblyPattern = "*.Tests.dll";
var unitTestProjectPattern = "*.Tests.csproj";
var nunitTestResultFile = "TestResult.xml";

var coverResultFile = "CoverResult.xml";
var openCoverCustomFilters = "+[BiBsps]*";

var resharperDupFinderOutputFileName = "dupfinder-output.xml";
var resharperDupFinderHtmlOutputFileName = "dupfinder-output.html";

var resharperInspectCodeOutputFileName = "inspectcode-output.xml";
var resharperInspectCodeHtmlOutputFileName = "inspectcode-output.html";

var sonarQubeUrl = EnvironmentVariable("SONAR_HOST_URL");
var sonarQubeLogin = EnvironmentVariable("SONAR_AUTH_TOKEN");
var sonarQubeOrg = EnvironmentVariable("SONAR_ORGANIZATION");
var sonarQubeProjectKey = "VSOSUTY";
var sonarQubeProjectName = "VS2017OfflineSetupUtility";
var sonarQubeBranchName = EnvironmentVariable("GIT_BRANCH");

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", defaultTarget);
var configuration = Argument("configuration", defaultConfiguration);
var resharperThrowExceptionOnViolations = Argument("resharperThrow", defaultResharperThrow);
var sonarHostUrl = Argument("sonarHostUrl", sonarQubeUrl);
var sonarLogin = Argument("sonarLogin", sonarQubeLogin);
var nugetSource = Argument("nugetSource", defaultNugetSource);
var nugetApiKey = Argument("nugetApiKey", defaultNugetApiKey);

///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var solutions = GetFiles("./**/*.sln")
                .Where(solution => !solutionFilter.Any(f => f.ToString() == solution.ToString()));
var solutionPaths = solutions
                    .Where(solution => !solutionFilter.Any(f => f.ToString() == solution.ToString()))
                    .Select(solution => solution.GetDirectory());

var nugetPackageProjectPaths = GetFiles("./**/*.csproj")
                .Where(proj => !projectFilter.Any(f => f.ToString() == proj.ToString()))
                .Where(proj => !proj.ToString().Contains(@".Tests.csproj"));

var nugetPackagesDirectory = Directory(nugetPackagesDirectoryPath);
var testReportsDirectory = Directory(testReportsDirectoryPath);
var resharperReportsDirectory = testReportsDirectory + Directory(resharperReportsDirectoryName);

var unitTestAssemblies = GetFiles($"./**/bin/{configuration}/**/{unitTestAssemblyPattern}");
var unitTestPorjects = GetFiles($"./**/{unitTestProjectPattern}")
                .Where(proj => !projectFilter.Any(f => f.ToString() == proj.ToString()));

var nunitOutputFile = new FilePath(testReportsDirectoryPath + $"/{nunitTestResultFile}");

var openCoverOutputFile = new FilePath(testReportsDirectoryPath + $"/{coverResultFile}");
var openCoverHtmlOutputDirectory = testReportsDirectoryPath + "/CoverageReport/";
var openCoverFilters = openCoverCustomFilters + @" -[*]GitVersionInformation -[*Tests]* -[nunit*]* -[Moq*]* -[FluentAssertions]*";

var resharperDupFinderOutputFile = resharperReportsDirectory + File(resharperDupFinderOutputFileName);
var resharperInspectCodeOutputFile = resharperReportsDirectory + File(resharperInspectCodeOutputFileName);

var resharperDupFinderHtmlOutputFile = resharperReportsDirectory + File(resharperDupFinderHtmlOutputFileName);
var resharperInspectCodeHtmlOutputFile = resharperReportsDirectory + File(resharperInspectCodeHtmlOutputFileName);


///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    // Executed BEFORE the first task.
    Information("Running tasks...");
    CreateDirectory(testReportsDirectory);
    CreateDirectory(nugetPackagesDirectory);
});

Teardown(context =>
{
    // Executed AFTER the last task.
    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Description("Cleans all directories that are used during the build process.")
    .Does(() =>
{
    // Clean solution directories.
    foreach(var path in solutionPaths)
    {
        Information("Cleaning {0}", path);
        CleanDirectories(path + "/**/bin/" + configuration);
        CleanDirectories(path + "/**/obj/" + configuration);
        CleanDirectories(testReportsDirectory);
        CleanDirectories(nugetPackagesDirectory);
    }
});

Task("Restore")
    .Description("Restores all the NuGet packages that are used by the specified solution.")
    .Does(() =>
{
    // Restore all NuGet packages.
    foreach(var solution in solutions)
    {
        Information("Restoring {0}...", solution);
        NuGetRestore(solution);
    }
});

Task("MsBuild")
    .Description("Builds all the different parts of the project.")
    .Does(() =>
{
    // Build all solutions.
    foreach(var solution in solutions)
    {
        Information("Building {0}", solution);
        MSBuild(solution, settings =>
            settings.SetConfiguration(configuration));

        CopyFiles(GetFiles($"{solution.GetDirectory().ToString()}/**/bin/{configuration}/*.nupkg"), nugetPackagesDirectory);
    }
});

Task("DotnetCoreBuild")
    .Description("Builds all the different parts of the project.")
    .Does(() =>
{
    // Build all solutions.
    foreach(var solution in solutions)
    {
        DotNetCoreBuild(solution.ToString(), new DotNetCoreBuildSettings
        {
            Configuration = configuration,
            ArgumentCustomization = arg => arg.AppendSwitch("/p:DebugType","=","Full")
        });

        CopyFiles(GetFiles($"{solution.GetDirectory().ToString()}/**/bin/{configuration}/*.nupkg"), nugetPackagesDirectory);
    }
});

Task("NUnit")
    .Does(() =>
{
    NUnit3(unitTestAssemblies, new NUnit3Settings {
        Work  = testReportsDirectory
    });
});

Task("OpenCoverWithToolNUnit3")
    .Does(() =>
{
    var openCoverSettings = new OpenCoverSettings()
    {
        Register = "path64",
        SkipAutoProps = true,
        ArgumentCustomization = args => args.Append($"-coverbytest:{unitTestAssemblyPattern}").Append("-mergebyhash")
    };
    
    foreach(var proj in unitTestPorjects)
    {
        try 
        {
            OpenCover(tool => {
                    tool.NUnit3(unitTestAssemblies, new NUnit3Settings {
                        Work  = testReportsDirectory
                        });
                },
                openCoverOutputFile,
                openCoverSettings
                    .WithFilter(openCoverFilters)
            );
        }
        catch(Exception ex)
        {
            Error("There was an error while running the opencover", ex);
        }
    }
    ReportGenerator(openCoverOutputFile, openCoverHtmlOutputDirectory);
});

Task("OpenCoverWithToolDotNetCoreTest")
    .Does(() =>
{
    var openCoverSettings = new OpenCoverSettings()
    {
        OldStyle = true,
        MergeOutput = true,
        Register = "user",
        SkipAutoProps = true,
        ArgumentCustomization = args => args.Append($"-coverbytest:{unitTestAssemblyPattern}").Append("-mergebyhash")
    };
    
    foreach(var proj in unitTestPorjects)
    {
        try 
        {
            OpenCover(tool => {
                        tool.DotNetCoreTest(proj.ToString(), new DotNetCoreTestSettings
                            {
                                Configuration = configuration,
                                NoBuild = true
                            });
                },
                openCoverOutputFile,
                openCoverSettings
                    .WithFilter(openCoverFilters)
            );
        }
        catch(Exception ex)
        {
            Error("There was an error while running the opencover", ex);
        }
    }
    ReportGenerator(openCoverOutputFile, openCoverHtmlOutputDirectory);
});

Task("DupFinder")
    .Does(() =>
{
    var rootDirectoryPath = MakeAbsolute(Context.Environment.WorkingDirectory);

    foreach(var solution in solutions)
    {
        DupFinder(solution, new DupFinderSettings {
            ShowStats = true,
            ShowText = true,
            ExcludePattern = new String[]
            {
                rootDirectoryPath + "/**/*Designer.cs",
            },
            OutputFile = resharperDupFinderOutputFile,
            ThrowExceptionOnFindingDuplicates = resharperThrowExceptionOnViolations == "True"
        });
    }
})
.Finally(() =>
{
    ReSharperReports(resharperDupFinderOutputFile, resharperDupFinderHtmlOutputFile);
});

Task("InspectCode")
    .Does(() =>
{
    var msBuildProperties = new Dictionary<string, string>();
    msBuildProperties.Add("configuration", configuration);
    msBuildProperties.Add("platform", "AnyCPU");

    foreach(var solution in solutions)
    {
        InspectCode(solution, new InspectCodeSettings {
            SolutionWideAnalysis = true,
            MsBuildProperties = msBuildProperties,
            OutputFile = resharperInspectCodeOutputFile,
            ThrowExceptionOnFindingViolations = resharperThrowExceptionOnViolations == "True"
        });
    }
})
.Finally(() =>
{
    ReSharperReports(resharperInspectCodeOutputFile, resharperInspectCodeHtmlOutputFile);
});

Task("Sonar")
    .Does(() =>
{
    var settings = new SonarBeginSettings() {
        Url = sonarQubeUrl,
        Login = sonarQubeLogin,
        // Organization = sonarQubeOrg,
        Key = sonarQubeProjectKey,
        Name = sonarQubeProjectName,
        // Branch = sonarQubeBranchName,
        // NUnitReportsPath = nunitOutputFile.ToString(),
        // OpenCoverReportsPath = openCoverOutputFile.ToString(),
        Verbose = true
    };
    foreach(var solution in solutions)
    {
        Sonar(ctx => ctx.MSBuild(solution), settings);
    }
});

// For .netframework project type
Task("Package")
    .Does(() =>
{
    // Need to filter your project
    foreach(var proj in nugetPackageProjectPaths)
    {
        var nuGetPackSettings = new NuGetPackSettings {
            OutputDirectory = nugetPackagesDirectory,
            ArgumentCustomization = args => args.Append("-Prop Configuration=" + configuration)
        };

        NuGetPack(proj.ToString(), nuGetPackSettings);
    }
});

Task("Deploy")
    .Does(() =>
{
    NuGetPush(GetFiles(nugetPackagesDirectory.ToString() + "/*.nupkg"),
        new NuGetPushSettings {
            Source = nugetSource,
            ApiKey = nugetApiKey
    });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Rebuild");

Task("Rebuild")
    .Description("Rebuilds all the different parts of the project.")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .IsDependentOn("MsBuild");

Task("OpenCoverLocal")
    .IsDependentOn("Rebuild")
    .IsDependentOn("OpenCoverWithToolNUnit3");

Task("SonarLocal")
    //.IsDependentOn("OpenCoverLocal")
    .IsDependentOn("Sonar");

Task("ResharperLocal")
    .IsDependentOn("Restore")
    .IsDependentOn("InspectCode")
    .IsDependentOn("DupFinder");

Task("DeployLocal")
    .IsDependentOn("Rebuild")
    // Uncomment out below for .netframework project type
    .IsDependentOn("Package")
    .IsDependentOn("Deploy");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
