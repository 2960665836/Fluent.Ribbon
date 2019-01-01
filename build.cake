//////////////////////////////////////////////////////////////////////
// TOOLS / ADDINS
//////////////////////////////////////////////////////////////////////

#tool GitVersion.CommandLine&version=4.0.0
#tool NUnit.ConsoleRunner&version=3.9.0
#addin Cake.Figlet&version=1.2.0

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
if (string.IsNullOrWhiteSpace(target))
{
    target = "Default";
}

var configuration = Argument("configuration", "Release");
if (string.IsNullOrWhiteSpace(configuration))
{
    configuration = "Release";
}

var verbosity = Argument("verbosity", Verbosity.Normal);
if (string.IsNullOrWhiteSpace(configuration))
{
    verbosity = Verbosity.Normal;
}

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

var local = BuildSystem.IsLocalBuild;

// Set build version
if (local == false
    || verbosity == Verbosity.Verbose)
{
    GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.BuildServer });
}
GitVersion gitVersion = GitVersion(new GitVersionSettings { OutputType = GitVersionOutput.Json });

var isPullRequest = AppVeyor.Environment.PullRequest.IsPullRequest;
var branchName = gitVersion.BranchName;
var isDevelopBranch = StringComparer.OrdinalIgnoreCase.Equals("develop", branchName);
var isReleaseBranch = StringComparer.OrdinalIgnoreCase.Equals("master", branchName);
var isTagged = AppVeyor.Environment.Repository.Tag.IsTag;

// Define directories.
var buildDir = Directory("./bin");
var publishDir = Directory("./Publish");
var solutionFile = File("./Fluent.Ribbon.sln");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(context =>
{
    if (!IsRunningOnWindows())
    {
        throw new NotImplementedException("Fluent.Ribbon will only build on Windows because it's not possible to target WPF and Windows Forms from UNIX.");
    }

    Information(Figlet("Fluent.Ribbon"));

    Information("Informational Version  : {0}", gitVersion.InformationalVersion);
    Information("SemVer Version         : {0}", gitVersion.SemVer);
    Information("AssemblySemVer Version : {0}", gitVersion.AssemblySemVer);
    Information("MajorMinorPatch Version: {0}", gitVersion.MajorMinorPatch);
    Information("NuGet Version          : {0}", gitVersion.NuGetVersion);
    Information("IsLocalBuild           : {0}", local);
    Information("Branch                 : {0}", branchName);
    Information("Configuration          : {0}", configuration);
});

Teardown(ctx =>
{
});

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(buildDir);
    CleanDirectories("./**/obj");
});

Task("Restore")
    .Does(() =>
{
    MSBuild(solutionFile, settings => 
        settings
            .SetConfiguration(configuration)
            .SetVerbosity(Verbosity.Minimal)
            .WithRestore()
            );
});

Task("Build")
    .IsDependentOn("Restore")
    .Does(() =>
{    
    // Use MSBuild
    MSBuild(solutionFile, settings => 
        settings
            .SetMaxCpuCount(0)
            .SetConfiguration(configuration)
            // .SetVerbosity(verbosity)
            .SetVerbosity(Verbosity.Minimal)
            .WithProperty("Version", isReleaseBranch ? gitVersion.MajorMinorPatch : gitVersion.NuGetVersion)
            .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
            .WithProperty("FileVersion", gitVersion.MajorMinorPatch)
            .WithProperty("InformationalVersion", gitVersion.InformationalVersion)
            //.WithProperty("ContinuousIntegrationBuild", local == false ? "true" : "false")
            );
});

Task("EnsurePublishDirectory")
    .Does(() =>
{
    EnsureDirectoryExists(publishDir);
});

Task("Pack")
    .IsDependentOn("EnsurePublishDirectory")
    .Does(() =>
{
    var msBuildSettings = new MSBuildSettings {
        Verbosity = Verbosity.Minimal,
        Configuration = configuration
    };
    var project = "./Fluent.Ribbon/Fluent.Ribbon.csproj";

    MSBuild(project, msBuildSettings
      .WithTarget("pack")
      .WithProperty("PackageOutputPath", MakeAbsolute(publishDir).ToString())
      .WithProperty("RepositoryBranch", branchName)
      .WithProperty("RepositoryCommit", gitVersion.Sha)
      .WithProperty("Version", isReleaseBranch ? gitVersion.MajorMinorPatch : gitVersion.NuGetVersion)
      .WithProperty("AssemblyVersion", gitVersion.AssemblySemVer)
      .WithProperty("FileVersion", gitVersion.AssemblySemFileVer)
      .WithProperty("InformationalVersion", gitVersion.InformationalVersion)
    );
});

Task("Zip")    
    .IsDependentOn("EnsurePublishDirectory")
    .Does(() =>
{
    Zip(buildDir.ToString() + "/Fluent.Ribbon/" + configuration, publishDir.ToString() + "/Fluent.Ribbon-v" + gitVersion.NuGetVersion + ".zip");
    Zip(buildDir.ToString() + "/Fluent.Ribbon.Showcase/" + configuration, publishDir.ToString() + "/Fluent.Ribbon.Showcase-v" + gitVersion.NuGetVersion + ".zip");
});

Task("Tests")    
    .Does(() =>
{
    NUnit3(
        buildDir.ToString() + "/Fluent.Ribbon.Tests/**/" + configuration + "/**/*.Tests.dll"
    );
});

Task("GetCredentials")
    .Does(() => 
{
    // username = EnvironmentVariable("GITHUB_USERNAME_FLUENT_RIBBON");
    // if (string.IsNullOrEmpty(username))
    // {
    //     throw new Exception("The GITHUB_USERNAME_FLUENT_RIBBON environment variable is not defined.");
    // }

    // token = EnvironmentVariable("GITHUB_TOKEN_FLUENT_RIBBON");
    // if (string.IsNullOrEmpty(token))
    // {
    //     throw new Exception("The GITHUB_TOKEN_FLUENT_RIBBON environment variable is not defined.");
    // }
});

Task("CreateReleaseNotes")
    .WithCriteria(() => !isTagged)
    .IsDependentOn("EnsurePublishDirectory")
    .IsDependentOn("GetCredentials")
    .Does(() =>
{
    // GitReleaseManagerCreate(username, token, "fluentribbon", "Fluent.Ribbon", new GitReleaseManagerCreateSettings {
    //     Milestone         = gitVersion.MajorMinorPatch,
    //     Name              = "Fluent.Ribbon" + gitVersion.SemVer,
    //     Prerelease        = false,
    //     TargetCommitish   = "master",
    //     WorkingDirectory  = "./"
    // });
});

Task("ExportReleaseNotes")
    .IsDependentOn("EnsurePublishDirectory")
    .IsDependentOn("GetCredentials")
    .Does(() =>
{
    // GitReleaseManagerExport(username, token, "fluentribbon", "Fluent.Ribbon", publishDir.ToString() + "/releasenotes.md", new GitReleaseManagerExportSettings {
    //     // TagName         = gitVersion.SemVer,
    //     TagName         = "v6.1.0",
    //     TargetDirectory = publishDir,
    //     LogFilePath     = publishDir.ToString() + "/grm.log"
    // });
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Build");

Task("appveyor")
    .IsDependentOn("Build")
    .IsDependentOn("Tests")
    .IsDependentOn("Pack")
    .IsDependentOn("Zip");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
