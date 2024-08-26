
using System;
using System.Text;
using System.Xml;
using System.Diagnostics;

using CommandLine;
using LibGit2Sharp;
using ICSharpCode.SharpZipLib.BZip2;
using System.Formats.Tar;
using System.Runtime.InteropServices;



_ = Parser.Default.ParseArguments<NewOptions, CleanOptions>(args)
            .MapResult(
                (NewOptions opts) => CreateProject(opts),
                (CleanOptions opts) => CleanProjects(opts),
                errs => 1
            );

int CleanProjects(CleanOptions o)
{
    var nez_path = Path.Join(Environment.CurrentDirectory, "Nez");

    if (Directory.Exists(nez_path))
    {
        Console.WriteLine("Cleaning Nez repo...");

        foreach (var project in new[] { "Nez.FarseerPhysics", "Nez.Portable", "Nez.Persistence" })
        {
            var project_path = Path.Join(nez_path, project);

            if (Directory.Exists(project_path))
            {
                // delete bin and obj directories

                var obj_path = Path.Combine(project_path, "obj");

                if (Directory.Exists(obj_path))
                {
                    Directory.Delete(obj_path, true);
                }

                var bin_path = Path.Combine(project_path, "bin");

                if (Directory.Exists(bin_path))
                {
                    Directory.Delete(bin_path, true);
                }
            }
        }
    }

    return 0;
}

int CreateProject(NewOptions o)
{
    var project_name = o.Name.Trim();

    // use current directory name as project name if not specified
    if (string.IsNullOrEmpty(project_name))
    {
        var cwd = new DirectoryInfo(Environment.CurrentDirectory);

        project_name = cwd.Name;
    }

    Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
    Console.WriteLine($"Project name: {project_name}");
    Console.WriteLine($"Nez: {o.Nez}");
    Console.WriteLine($"Align SDK: {o.AlignSdk}");


    // make sure FNA not exist
    if (!Directory.Exists("FNA"))
    {
        Console.WriteLine("Cloning FNA repo...");

        // clone FNA repo to ./FNA
        Repository.Clone("https://github.com/FNA-XNA/FNA", "FNA", new CloneOptions { RecurseSubmodules = true });
    }

    // make sure NEZ not exist
    if (!Directory.Exists("Nez"))
    {
        Console.WriteLine("Cloning Nez repo...");

        // clone Nez repo to ./Nez
        Repository.Clone("https://github.com/prime31/Nez", "Nez", new CloneOptions { RecurseSubmodules = true });
    }

    // create project directory
    if (!Directory.Exists(project_name))
    {
        var project_dir = Directory.CreateDirectory(project_name);

        // write Game1.cs, Program.cs, and <project_name>.csproj files, make Content dir
        var game_content = String.Empty;

        if (o.Nez)
        {
            Console.WriteLine("Using Nez game template...");

            game_content = fnaproj.Resource1.Game_cs_nez;
        }
        else
        {
            Console.WriteLine("Using default game template...");

            game_content = fnaproj.Resource1.Game_cs_default;
        }

        // use project name as default namespace
        game_content = game_content.Replace("<namespace>", project_name);

        File.WriteAllText(Path.Join(project_dir.FullName, "Game1.cs"), game_content, Encoding.UTF8);

        var program_content = fnaproj.Resource1.Program_cs.Replace("<namespace>", project_name);

        var game_project_file = Path.Join(project_dir.FullName, $"{project_name}.csproj");

        File.WriteAllText(Path.Join(project_dir.FullName, "Program.cs"), program_content, Encoding.UTF8);
        File.WriteAllText(game_project_file, fnaproj.Resource1.project_csproj, Encoding.UTF8);

        project_dir.CreateSubdirectory("Content");

        var dotnet = new DotnetCliWrapper();
        var sdk_version = dotnet.GetSdkVersion(game_project_file);

        // 1. create solution
        dotnet.CreateSolution(string.Empty);

        // 2. add FNA project to solution
        var fna_project = "FNA/FNA.Core.csproj";

        dotnet.AddProjectToSolution(fna_project);
        dotnet.AddProjectToSolution(game_project_file);


        // 3. add Nez project to solution
        if (o.Nez)
        {
            Console.WriteLine("Adding Nez project to solution...");

            // 3.1 add Nez core project to solution
            var nez_core = "Nez/Nez.Portable/Nez.FNA.Core.csproj";

            dotnet.AddProjectToSolution(nez_core);
            dotnet.AddReference(game_project_file, nez_core);

            // 3.2 add Nez persistant proejct to solution
            var nez_persistence = "Nez/Nez.Persistence/Nez.FNA.Core.Persistence.csproj";

            dotnet.AddProjectToSolution(nez_persistence);
            dotnet.AddReference(game_project_file, nez_persistence);

            // 3.3 add Nez Farseer phsyics project to solution
            var nez_farseer = "Nez/Nez.FarseerPhysics/Nez.FNA.Core.FarseerPhysics.csproj";

            dotnet.AddProjectToSolution(nez_farseer);
            dotnet.AddReference(game_project_file, nez_farseer);


            if (o.AlignSdk)
            {
                Console.WriteLine($"Aligning SDK version to {sdk_version}");

                dotnet.SetSdkVersion(nez_core, sdk_version);
                dotnet.SetSdkVersion(nez_persistence, sdk_version);
                dotnet.SetSdkVersion(nez_farseer, sdk_version);
            }
        }
    }
    else
    {
        Console.WriteLine("Project already exists, skip.");
    }


    // download native libraries
    var native_lib_name = "fnalibs.tar.bz2";
    var native_lib_tar_name = "fnalibs.tar";

    if (!File.Exists(native_lib_name))
    {
        Console.WriteLine("Downloading native libraries...");

        HttpClient client = new();

        var resp = client.Send(new HttpRequestMessage(HttpMethod.Get, "https://fna.flibitijibibo.com/archive/fnalibs.tar.bz2"));

        using var stream = resp.Content.ReadAsStream();
        using var file = File.Create(native_lib_name);

        stream.CopyTo(file);
    }
    else
    {
        Console.WriteLine("Native libraries already downloaded, skip.");
    }

    var native_lib_path = "fnalibs";

    if (!Path.Exists(native_lib_path))
    {
        Directory.CreateDirectory(native_lib_path);
    }

    if (!File.Exists(native_lib_tar_name))
    {
        // extract to a tarball
        BZip2.Decompress(File.OpenRead(native_lib_name), File.Create(native_lib_tar_name), true);

    }

    // extract to project folder
    TarFile.ExtractToDirectory(native_lib_tar_name, native_lib_path, true);

    return 0;
}

[Verb("new", HelpText = "Create a new game project using FNA.")]
public class NewOptions
{
    [Option('n', "name", Required = false, HelpText = "Name of the output project.")]
    public string Name { get; set; } = string.Empty;

    [Option('z', "nez", Required = false, HelpText = "Use Nez instead of FNA.")]
    public bool Nez { get; set; } = false;

    [Option('a', "align", Required = false, HelpText = "Align the SDK to the default version.")]
    public bool AlignSdk { get; set; } = false;
}

[Verb("clean", HelpText = "Clean the project by deleting obj and bin folder.")]
public class CleanOptions
{
}

public class DotnetCliWrapper
{
    public DotnetCliWrapper()
    { }

    public void CreateSolution(string solution_name)
    {
        Process? proc;

        if (string.IsNullOrEmpty(solution_name))
        {
            proc = StartProcess("new sln");
        }
        else
        {
            proc = StartProcess($"new sln --name {solution_name}");
        }

        ArgumentNullException.ThrowIfNull(proc);

        proc.WaitForExit();
    }

    public void AddProjectToSolution(string project_path)
    {
        var proc = StartProcess($"sln add {project_path}");

        ArgumentNullException.ThrowIfNull(proc);

        proc.WaitForExit();
    }

    public void AddReference(string project_path, string reference_path)
    {
        var proc = StartProcess($"add {project_path} reference {reference_path}");

        ArgumentNullException.ThrowIfNull(proc);

        proc.WaitForExit();
    }

    public string GetSdkVersion(string project_file)
    {
        var doc = new XmlDocument();

        doc.Load(project_file);

        var node = doc.SelectSingleNode("/Project/PropertyGroup/TargetFramework");

        return node!.InnerText;
    }

    public void SetSdkVersion(string project_file, string version)
    {
        var doc = new XmlDocument();

        doc.Load(project_file);

        var node = doc.SelectSingleNode("/Project/PropertyGroup/TargetFramework");

        node!.InnerText = version;

        doc.Save(project_file);
    }

    private Process? StartProcess(string args)
    {
        ProcessStartInfo start_info = new()
        {
            FileName = "dotnet",
            Arguments = args,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        return Process.Start(start_info);
    }

}