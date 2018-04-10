#if DEBUG
#define STATIC_COMPILER
#define STATIC_TRANSLATOR
#else
#define trycatch
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Pace.CommonLibrary;
using System.Xml;

#if STATIC_COMPILER
using Pace.Compiler;
#endif
#if STATIC_TRANSLATOR
using Pace.Translator;
#endif

using System.Reflection;

//This is the user interface, doesn't have function on it's own

namespace pacednShell
{
    class Program
    {
        static string AutoexecFile = "pacedn-autoexec";
        static string AutoexecDefault =


@"# these commands will be automatically executed on startup
# comments with '#'

# the package directory
set path packages

# the default compiler and translator
# uncomment one and comment others

# set compiler pacednc.exe

# set translator pacedntjs.exe
# set translator pacedntc.exe";


        static void Main(string[] args)
        {
            string singleCommand = null;
            bool noauto = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--noauto")
                {
                    noauto = true;
                }
                else if (args[i] == "c")
                {
                    if (i + 1 == args.Length) Console.WriteLine("Invalid args");
                    else
                    {
                        singleCommand = args[++i];
                    }
                }
            }
            Console.OutputEncoding = Encoding.UTF8;
            if (!noauto)
            {
                if (!File.Exists(AutoexecFile)) File.WriteAllText(AutoexecFile, AutoexecDefault);
                int topi = Console.CursorTop;
                foreach (var l in File.ReadAllLines(AutoexecFile))
                {
                    if (!l.StartsWith("#")) RunCommand(l);
                }
                if (topi != Console.CursorTop)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }
            }
            if (singleCommand != null)
            {
                RunCommand(singleCommand);
            }
            else Shell();
        }

        static bool PackageNameValid(string s) => s.All(c => char.IsLetter(c));

        static bool Clear = true;
        static void Shell()
        {
            while (true)
            {
                if (Clear)
                {
                    Clear = false;
                    Console.Clear();
                    Console.WriteLine("PaceDN Shell, https://github.com/XamNn/pacedn, Samuel Kriikkula © 2018");
                    Console.WriteLine($"Common library: '{Pace.CommonLibrary.Info.Version}'");
                    Console.WriteLine(CompilerType == null ? "Compiler not loaded" : $"Compiler: '{CompilerVersion}'");
                    Console.WriteLine(TranslatorType == null ? "Translator not loaded" : $"Translator: '{TranslatorVersion}'");
                    Console.WriteLine();
                }
                int i = Console.CursorTop;
                Console.Write("> ");
                RunCommand(Console.ReadLine());
                if (Console.CursorTop != i + 1) Console.WriteLine();
            }
        }

        static string CompilerVersion;
        static System.Type CompilerType;
        static MethodInfo CompilerFunction;
        static string TranslatorVersion;
        static System.Type TranslatorType;
        static MethodInfo TranslatorFunction;

        static Package Compile(string Source, string LocalPath)
        {
#if STATIC_COMPILER
            return new Compiler().Compile(Source, LocalPath);
#else
            if (CompilerType == null)
            {
                Console.WriteLine("Compiler not loaded");
                return null;
            }
            return (Package)CompilerFunction.Invoke(Activator.CreateInstance(CompilerType), new object[] { Source, LocalPath });
#endif
        }
        static void Translate(string Filename)
        {
#if STATIC_TRANSLATOR
            new Translator().Translate(Filename);
#else
            if (TranslatorFunction == null)
            {
                Console.WriteLine("Translator not loaded");
                return;
            }
            TranslatorFunction.Invoke(Activator.CreateInstance(TranslatorType), new object[] { Filename });
#endif
        }

        static Assembly ImportAssembly(string file, out string version, string namespacename)
        {
            try
            {
                var s = File.Exists(file);
                var a = Assembly.LoadFile(Path.GetFullPath(file));
                var i = a.GetType(namespacename + ".Info", true);
                version = (string)i.GetField("Version", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                return a;
            }
            catch
            {
                version = null;
                return null;
            }
        }

        static void RunCommand(string command)
        {
            command = command.Trim();
            if (command.Length == 0) return;
            List<string> parts = new List<string>();
            int si = 0;
            bool escaped = false;
            for (int i = 0; i < command.Length; i++)
            {
                if (escaped)
                {
                    if (command[i] == '"')
                    {
                        escaped = false;
                        parts.Add(command.Substring(si, i - si));
                        si = i + 1;
                    }
                }
                else
                {
                    if (char.IsWhiteSpace(command[i]))
                    {
                        if (si == i) si++;
                        else
                        {
                            parts.Add(command.Substring(si, i - si));
                            si = i + 1;
                        }
                    }
                    else if (command[i] == '"')
                    {
                        escaped = true;
                        si = i + 1;
                    }
                }
            }
            if (si != command.Length) parts.Add(command.Substring(si, command.Length - si));
            switch (parts[0].ToLower())
            {
                default:
                    Console.WriteLine("Invalid command");
                    break;

                case "help":
                    Console.WriteLine("List of commands");
                    Console.WriteLine("help                         Show this list");
                    Console.WriteLine("process [source] {-e} {-t}   Compile and optionally translate a source file without importing it, -e to export package, -t to translate");
                    Console.WriteLine("compile [source] {name}      Compile a source file to a package and import it");
                    Console.WriteLine("quickcompile                 A text editor to quickly write code and compile it");
                    Console.WriteLine("translate [file]             Translate the project");
                    Console.WriteLine("export [package] {file}      Export a package");
                    Console.WriteLine("import [name] {-s}           Import a package, -s to ignore duplicate symbols");
                    Console.WriteLine("packages                     List all packages");
                    Console.WriteLine("symbols {package}            List all symbols, optionally you can specify a package");
                    Console.WriteLine("merge                        Merge all packages into one");
                    Console.WriteLine("rename [package] [new name]  Rename a package");
                    Console.WriteLine("reset                        Resets the project");
                    Console.WriteLine("set compiler [compiler]      Loads a compatible compiler from an assembly (.dll or .exe)");
                    Console.WriteLine("set translator [translator]  Loads a compatible translator from an assembly (.dll or .exe)");
                    Console.WriteLine("set path [path]              Sets the package import/export directory");
                    Console.WriteLine("clear                        Clears the console");
                    Console.WriteLine("exit                         Exit the shell");
                    Console.WriteLine("- [symbol]                   Examine a symbol");
                    break;
                case "process":
                    {
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 4) Console.WriteLine("Too many arguments");
                        else
                        {
                            bool export = false;
                            bool translate = false;
                            if (parts.Count > 2)
                            {
                                if (parts[2] == "-e") export = true;
                                else if (parts[2] == "-t") translate = true;
                                else
                                {
                                    Console.WriteLine("Invalid arguments");
                                    break;
                                }
                            }
                            if (parts.Count == 4)
                            {
                                if (parts[3] == "-e") export = true;
                                else if (parts[3] == "-t") translate = true;
                                else
                                {
                                    Console.WriteLine("Invalid arguments");
                                    break;
                                }
                            }
#if trycatch
                                try
                                {
#endif
                            string infile = parts[1];
                            string dir = Path.GetDirectoryName(infile);
                            string fn = Path.GetFileNameWithoutExtension(infile);
                            var l = Compile(File.ReadAllText(infile), dir);
                            if (l != null)
                            {
                                if (export) l.Save(dir + "\\" + fn + Settings.PackageFileExtention);
                                if (translate) TranslatorFunction.Invoke(null, new object[] { dir + "\\" + fn });
                            }
#if trycatch
                                }
                                catch(Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
#endif
                        }
                        break;
                    }
                case "compile":
                    {
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                                try
                                {
#endif
                            string infile = parts[1];
                            string outlib = parts.Count == 3 ? parts[2] : Path.GetFileNameWithoutExtension(infile);
                            if (!PackageNameValid(outlib)) Console.WriteLine("Invalid name");
                            else
                            {
                                var l = Compile(File.ReadAllText(infile), Path.GetDirectoryName(infile));
                                if (l != null)
                                {
                                    l.Name = outlib;
                                    Project.Current.Import(l, true);
                                }
                            }
#if trycatch
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
#endif
                        }
                        break;
                    }
                case "quickcompile":
                    {
                        var sb = new StringBuilder();
                        while (true)
                        {
                            var line = Console.ReadLine();
                            if (line == "done") break;
                            sb.Append(line);
                            sb.Append("\n");
                        }
#if trycatch
                            try
                            {
#endif
                        var l = Compile(sb.ToString(), null);
                        if (l != null) Project.Current.Import(l, true);
#if trycatch
                            }
                            catch (Exception e)
                            {
                                Console.Write("Error occured '");
                                Console.Write(e.Message);
                                Console.WriteLine("'");
                            }
#endif
                        break;
                    }
                case "translate":
                    {
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                                try
                                {
#endif
                            string outfile = parts[1];
                            Translate(outfile);
#if trycatch
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
#endif
                        }
                        break;
                    }
                case "export":
                    {
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                                try
                                {
#endif
                            var p = Project.Current.Packages[parts[1]];
                            string outfile = parts.Count == 3 ? parts[2] : Settings.FormatPackageFilename(p.Name, null, false);
                            p.Save(outfile);
#if trycatch
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
#endif
                        }
                        break;
                    }
                case "import":
                    {
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else if (parts.Count == 3 && parts[2] != "-s") Console.WriteLine("Invalid arguments");
                        else
                        {
                            string infile = null;
                            Package l = new Package();
#if trycatch
                                try
                                {
#endif
                            infile = Settings.FormatPackageFilename(parts[1], Environment.CurrentDirectory, true);
#if trycatch
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
#endif
                            var res = l.Read(infile);
                            if (res.IsSuccessful)
                            {
                                res = Project.Current.Import(l, parts.Count == 3);
                                if (!res.IsSuccessful) Console.WriteLine(res.Message);
                            }
                            else Console.WriteLine(res.Message);
                        }
                        break;
                    }
                case "packages":
                    if (parts.Count != 1) Console.WriteLine("Too many arguments");
                    else if (Project.Current.Packages.Count == 0) Console.WriteLine("No packages imported");
                    else
                    {
                        Console.WriteLine("Name                Symbols");
                        foreach (var l in Project.Current.Packages)
                        {
                            Console.WriteLine(l.Value.Name.PadRight(20) + l.Value.Symbols.Count);
                        }
                    }
                    break;
                case "symbols":
                    if (parts.Count > 2) Console.WriteLine("Too many arguments");
                    else
                    {
                        if (parts.Count == 1)
                        {
                            if (Project.Current.Symbols.Count == 0) Console.WriteLine("No symbols found");
                            else
                            {
                                Console.WriteLine("List of all symbols:");
                                for (int i = 0; i < Project.Current.Symbols.Count; i++)
                                {
                                    Console.WriteLine(Project.Current.Symbols[i].Name);
                                }
                            }
                        }
                        else
                        {
                            if (Project.Current.Packages.ContainsKey(parts[1]))
                            {
                                var l = Project.Current.Packages[parts[1]];
                                if (l.Symbols.Count == 0) Console.WriteLine($"No symbols in the package '{parts[1]}'");
                                else
                                {
                                    Console.WriteLine($"List of all symbols in the package '{parts[1]}':");
                                    for (int i = 0; i < l.Symbols.Count; i++)
                                    {
                                        Console.WriteLine(l.Symbols[i].Name);
                                    }
                                }
                            }
                            else Console.WriteLine("Package not found");
                        }
                    }
                    break;
                case "merge":
                    Console.Write($"Merging {Project.Current.Packages.Count} packages containing ");
                    Project.Current.Merge();
                    Console.WriteLine(Project.Current.Symbols.Count.ToString() + " symbols in total");
                    break;
                case "rename":
                    if (parts.Count < 3) Console.WriteLine("Too few arguments");
                    else
                    {
                        if (!Project.Current.Packages.ContainsKey(parts[1])) Console.WriteLine("Package not found");
                        else
                        {

                        }
                    }
                    break;
                case "reset":
                    Console.WriteLine("Project intialized");
                    Project.Current = new Project();
                    break;
                case "set":
                    if (parts.Count < 3) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                    else if (parts[1] == "compiler")
                    {
#if trycatch
                        try
                        {
#endif
                        var a = ImportAssembly(parts[2], out CompilerVersion, "Pace.Compiler");
                        CompilerType = a.GetType("Pace.Compiler.Compiler");
                        CompilerFunction = CompilerType.GetMethod("Compile");
                        Clear = true;
#if trycatch
                        }
                        catch (Exception e)
                        {
                             Console.Write("Error occured '");
                             Console.Write(e.Message);
                             Console.WriteLine("'");
                        }
#endif
                    }
                    else if (parts[1] == "translator")
                    {
#if trycatch
                        try
                        {
#endif
                        var a = ImportAssembly(parts[2], out TranslatorVersion, "Pace.Translator");
                        TranslatorType = a.GetType("Pace.Translator.Translator");
                        TranslatorFunction = TranslatorType.GetMethod("Translate");
                        Clear = true;
#if trycatch
                        }
                        catch (Exception e)
                        {
                             Console.Write("Error occured '");
                             Console.Write(e.Message);
                             Console.WriteLine("'");
                        }
#endif
                    }
                    else if (parts[1] == "path")
                    {
                        if (Directory.Exists(parts[2])) Settings.PackageDirectory = parts[2];
                        else Console.WriteLine("Directory does not exist");
                    }
                    else Console.WriteLine("Invalid arguments");
                    break;
                case "clear":
                    Clear = true;
                    break;
                case "exit":
                    Environment.Exit(0);
                    break;
                case "-":
                    if (parts.Count < 2) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                    else
                    {
                        var symbol = Project.Current.GetSymbol(parts[1]);
                        if (symbol == null)
                        {
                            Console.WriteLine("Symbol not found");
                            break;
                        }
                        Console.WriteLine($"Symbol '{symbol.ToString()}'");
                        if (symbol is ElementSymbol element)
                        {
                            Console.WriteLine("Element");
                        }
                        else if (symbol is ClassSymbol _class)
                        {
                            Console.WriteLine("Class");
                        }
                        else if (symbol is StructSymbol _struct)
                        {
                            Console.WriteLine("Struct");
                        }
                        else if (symbol is VariableSymbol variable)
                        {
                            Console.WriteLine("Variable");
                            Console.WriteLine("Type: " + variable.Type.ToString());
                            Console.WriteLine("Default Value: " + variable.DefaultValue.ToString());
                            Console.WriteLine("Getter: " + variable.Get.ToString());
                            Console.WriteLine("Setter: " + variable.Set.ToString());
                        }
                        else if (symbol is PropertySymbol property)
                        {
                            Console.WriteLine("Property");
                            Console.WriteLine("Type: " + property.Type.ToString());
                            Console.WriteLine("Getter: " + property.Get.ToString());
                            Console.WriteLine("Setter: " + property.Set.ToString());
                        }
                        if (symbol.Attributes.ContainsKey("document"))
                        {
                            Console.WriteLine("Document:");
                            Console.WriteLine(symbol.Attributes["document"]);
                        }
                        if (symbol.Children != null)
                        {
                            Console.WriteLine("Children:");
                            for (int i = 0; i < symbol.Children.Count; i++)
                            {
                                Console.Write(' ');
                                Console.WriteLine(symbol.Children[i].Name);
                            }
                        }
                    }
                    break;
            }
        }

        static int PrintConsoleMenu(string[] items)
        {
            Console.CursorVisible = false;
            int firstline = Console.CursorTop;
            Console.WriteLine();
            for (int i = 1; i < items.Length; i++)
            {
                Console.Write("  ");
                Console.WriteLine(items[i]);
            }
            int current = 0;
            Console.SetCursorPosition(0,firstline);
            Console.Write("> ");
            Console.Write(items[0]);
            while (true)
            {
                var k = Console.ReadKey(true);
                switch (k.Key)
                {
                    case ConsoleKey.Enter:
                        Console.CursorVisible = true;
                        return current;
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.W:
                        if (current == 0) break;
                        Console.SetCursorPosition(0, firstline + current);
                        Console.Write("  ");
                        Console.Write(items[current]);
                        current -= 1;
                        Console.SetCursorPosition(0, firstline + current);
                        Console.Write("> ");
                        Console.Write(items[current]);
                        break;
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.S:
                        if (current == items.Length - 1) break;
                        Console.SetCursorPosition(0, firstline + current);
                        Console.Write("  ");
                        Console.Write(items[current]);
                        current += 1;
                        Console.SetCursorPosition(0, firstline + current);
                        Console.Write("> ");
                        Console.Write(items[current]);
                        break;
                    case ConsoleKey.Escape:
                        Console.CursorVisible = true;
                        Environment.Exit(0);
                        break;
                }
            }
        }
    }
}
