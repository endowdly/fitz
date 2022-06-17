#tool dotnet:?package=GitVersion.Tool&global&version=5.10.3
#addin nuget:?package=Cake.Git&version=2.0.0

// Statics
const string EnvGlobInv = "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT";
const string Configuration = "Release";
const string Project = @".\fitz.fsproj";
const string AssmFile = @".\fitzInfo.cs";
const string AssmVersion = "0.0.1";  // This should not be changed often

// These will largely remain the same as gotz
// To be specific, these are runtime id's
// link -> https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
readonly string[] _platform = { "linux-x64", "linux-arm64", "osx-x64", "win10-x86", "win-arm64" };

// Lazy, so let GitVersion do versioning
readonly string _version = GitVersion().FullSemVer;


///////////////////////////////////////////////////////////////////////////////
// ARGUMENTS
///////////////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", Configuration);
var version = Argument("version", _version);
var assmVersion = Argument("assembly-version", AssmVersion);
var buildDir = Argument("output", "./publish");
var tag = Argument("tag", version);
var notes = Argument("notes", "");

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Build") 
    .IsDependentOn("Clean")
    .Does(() => 
    { 
        var settings = new DotNetBuildSettings
        {
            NoLogo = true,
            Verbosity = DotNetVerbosity.Minimal, 
        };

        DotNetBuild(Project, settings);
    });

Task("TestVersion")
    .Does(() =>
    {
        var tags = GitTags(".");
        var taggedVersion = tags.Any()
            ? tags.Last().FriendlyName.Trim()
            : "<no tags>";

        Information($"Calculated Version: {version}");
        Information($"Tagged Version: {taggedVersion}");

        if (version.Trim() != taggedVersion.Trim()) 
            throw new ArgumentException("Tags must match!"); 

        Information("Versions match!");
    });

Task("Clean") 
    .Does(() =>
    { 
        var binDir = "./bin";
        var objDir = "./obj";

        Information($"Cleaning projects dirs..."); 

        if (DirectoryExists(binDir))
            CleanDirectory(binDir);

        if (DirectoryExists(objDir))
            CleanDirectory(objDir);

        Information($"Cleaning build dir: {buildDir}...");

        if (DirectoryExists(buildDir))
            GetSubDirectories(buildDir)
                .ToList()
                .ForEach(dir => 
                {
                    Information($"Cleaning sub dir: {dir}...");
                    CleanDirectory(dir); 
                }); 
    });

Task("Publish")
    .IsDependentOn("Clean")
    .IsDependentOn("TestVersion")
    .Does(() =>
    { 
        var envGlobInv = EnvironmentVariable(EnvGlobInv, true);
        var mEnvGlobInv = new Dictionary<string, string>
        {
            { EnvGlobInv, envGlobInv.ToString() }
        }; 
        var properties = new Dictionary<string, string>
        {
            { "PublishSingleFile", true.ToString() }, 
        };
        var msBuildSettings = new DotNetMSBuildSettings
        {
            AssemblyVersion = AssmVersion,
            FileVersion = AssmVersion,
            InformationalVersion = version,
        }; 
        var publishSettings = _platform
            .Select(runtime => 
                new DotNetPublishSettings()
                {
                    NoLogo = true,
                    Verbosity = DotNetVerbosity.Minimal,
                    Configuration = Configuration,
                    Runtime = runtime,
                    MSBuildSettings = msBuildSettings,
                    PublishSingleFile = true,
                    SelfContained = true,
                    PublishTrimmed = true,
                    EnvironmentVariables = mEnvGlobInv,
                    OutputDirectory = System.IO.Path.Combine(buildDir, runtime),
                })
            .ToArray(); 

        foreach (var publishSetting in publishSettings)
        {
            DotNetPublish(Project, publishSetting);
        }
    });

Task("Release")
    .IsDependentOn("Publish")
    .Does(() => 
    { 
        static bool filterDebugFiles(IFileSystemInfo f) => 
            !f.Path.FullPath.EndsWith("pdb");

        var fs = GetFiles($"{buildDir}/**/fitz*", new GlobberSettings { FilePredicate = filterDebugFiles }); 
        var args = new ProcessArgumentBuilder()
            .Append("release")
            .Append("create")
            .AppendQuoted(tag)
            .AppendSwitch("--target", "main")
            .AppendSwitchQuoted("--title", $"Release {tag}");

        if (!string.IsNullOrEmpty(notes))
            args
                .AppendSwitchQuoted("--notes", notes);

        foreach (var f in fs)
        {
            var dir = f.GetDirectory();
            var path = f.GetFilenameWithoutExtension().ToString();
            var ext = f.GetExtension();
            var sNewFile = $"{dir.GetDirectoryName()}-{path}{ext}".TrimEnd();
            var newFile = new FilePath(sNewFile); 
            var absNewFile = newFile.MakeAbsolute(dir);

            Information($"Release {f} as {absNewFile.GetFilename()}");
            MoveFile(f.FullPath, absNewFile.FullPath);

            args.AppendQuoted(absNewFile.FullPath);
        } 
        var settings = new ProcessSettings
        {
            Arguments = args, 
        }; 

        Debug("running 'gh' with arguments:");
        Debug(args.Render());

        var returnCode = StartProcess("gh", settings); 
        
        Information($"'gh release' exited with code {returnCode}");
    }); 

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("TestVersion");

RunTarget(target);
