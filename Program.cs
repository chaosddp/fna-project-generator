
using System;
using System.Text;
using CommandLine;
using System.IO.Compression;

using LibGit2Sharp;
using ICSharpCode.SharpZipLib.BZip2;
using System.Formats.Tar;
;

_ = Parser.Default.ParseArguments<Options>(args)
            .WithParsed(o =>
            {
                var project_name = o.Name.Trim();

                // use current directory name as project name if not specified
                if (string.IsNullOrEmpty(project_name))
                {
                    var cwd = new DirectoryInfo(Environment.CurrentDirectory);

                    project_name = cwd.Name;
                }

                // make sure FNA not exist
                if (!Directory.Exists("FNA"))
                {
                    // clone FNA repo to ./FNA
                    Repository.Clone("https://github.com/FNA-XNA/FNA", "FNA", new CloneOptions { RecurseSubmodules = true });
                }

                // create project directory
                if (!Directory.Exists(project_name))
                {
                    var project_dir = Directory.CreateDirectory(project_name);

                    // write FNAGame.cs, Program.cs, and <project_name>.csproj files, make Content dir
                    File.WriteAllText(Path.Join(project_dir.FullName, "Game.cs"), fnaproj.Resource1.Game_cs, Encoding.UTF8);
                    File.WriteAllText(Path.Join(project_dir.FullName, "Program.cs"), fnaproj.Resource1.Program_cs, Encoding.UTF8);
                    File.WriteAllText(Path.Join(project_dir.FullName, $"{project_name}.csproj"), fnaproj.Resource1.project_csproj, Encoding.UTF8);

                    // solution file
                    var solution_file_content = fnaproj.Resource1.project_sln;

                    solution_file_content = solution_file_content.Replace("<project_name>", project_name);

                    File.WriteAllText($"{project_name}.sln", solution_file_content, Encoding.UTF8);

                    project_dir.CreateSubdirectory("Content");
                }


                // download native libraries
                var native_lib_name = "fnalibs.tar.bz2";
                var native_lib_tar_name = "fnalibs.tar";

                if (!File.Exists(native_lib_name))
                {
                    HttpClient client = new();

                    var resp = client.Send(new HttpRequestMessage(HttpMethod.Get, "https://fna.flibitijibibo.com/archive/fnalibs.tar.bz2"));

                    using var stream = resp.Content.ReadAsStream();
                    using var file = File.Create(native_lib_name);

                    stream.CopyTo(file);
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
            });



public class Options
{
    [Option('n', "name", Required = false, HelpText = "Name of the output project.")]
    public string Name { get; set; } = string.Empty;
}
