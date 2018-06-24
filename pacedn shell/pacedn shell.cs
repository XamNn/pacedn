#if !DEBUG
#define trycatch
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Pace.CommonLibrary;
using System.Diagnostics;

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;

//This is a user interface, doesn't have functionality on it's own

namespace pacednShell
{
    class Program
    {
        static readonly string AutoexecFile = FormatFileName($"$bindir{Path.DirectorySeparatorChar}autoexec.pacednscript");
        static readonly string AutoexecDefault =


$@"# these commands will be automatically executed on startup
# comments with '#'

# the package directory
set dir $bindir{Path.DirectorySeparatorChar}packages

# uncomment this if you want to se disable debugging by default
# set debugging false

# the default compiler and translator

set compiler $bindir{Path.DirectorySeparatorChar}pacednc.exe

set translator $bindir{Path.DirectorySeparatorChar}pacedntjs.exe";

        static readonly List<string> PartsWithOne = new List<string>(1) { null };

        static string CompilerName, TranslatorName;
        static string LastPath = null;

        static void Main(string[] args)
        {
            List<string> commands = new List<string>();
            bool noauto = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--noauto")
                {
                    noauto = true;
                }
                else commands.Add(args[i]);
            }
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            if (!noauto)
            {
                if (!File.Exists(AutoexecFile)) File.WriteAllText(AutoexecFile, AutoexecDefault);
                RunScript(AutoexecFile);
            }
            if (commands.Count != 0)
            {
                for (int i = 0; i < commands.Count; i++)
                {
                    RunCommand(commands[i]);
                }
            }
            else Shell();
        }

        static void RunScript(string file)
        {
            foreach (var l in File.ReadAllLines(file))
            {
                if (!l.StartsWith("#")) RunCommand(l);
            }
        }

        static bool PackageNameValid(string s) => s.All(c => char.IsLetter(c));

        static string FormatFileName(string s) => s.Replace("$bindir", AppDomain.CurrentDomain.BaseDirectory);

        static void Shell()
        {
            Console.WriteLine("[Shell] The pacedn project at: https://github.com/XamNn/pacedn. try 'help'");
            while (true)
            {
                Console.Write("pacedn> ");
                RunCommand(Console.ReadLine());
            }
        }
        static void Live()
        {
            StringBuilder InitSection = new StringBuilder();
            StringBuilder ProgramSection = new StringBuilder();
            StringBuilder CurrentBuffer = ProgramSection;

            bool autocompile = true;

            string make()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(InitSection);
                sb.Append("main = {\n");
                sb.Append(ProgramSection);
                sb.Append("}");
                return sb.ToString();
            }

            void doit(string input, StringBuilder buffer, bool beginning = false)
            {
                if (beginning) buffer.Insert(0, input + "\n");
                else buffer.Append(input);
                if (autocompile)
                {
                    var p = Compile(make(), "<live>");
                    if (p == null)
                    {
                        if (beginning) buffer.Remove(0, input.Length + 1);
                        else buffer.Remove(buffer.Length - (input.Length + 1), input.Length + 1);
                        return;
                    }
                }
            }

            while (true)
            {
                Console.Write("pacedn/live> ");
                string input = Console.ReadLine().Trim();
                if (input == "") continue;
                if (input == "exit" || input == ".exit") return;
                else if (input == ".program") CurrentBuffer = ProgramSection;
                else if (input == ".init") CurrentBuffer = InitSection;
                else if (input.StartsWith(".use "))
                {
                    doit("use " + input.Substring(5) + ";", InitSection);
                }
                else if (input.StartsWith(".import "))
                {
                    doit("import" + input.Substring(7) + ";\n", InitSection, true);
                }
                else if (input == ".clear") CurrentBuffer.Clear();
                else if (input == ".pause") autocompile = false;
                else if (input == ".resume") autocompile = true;
                else if (input == ".reset")
                {
                    InitSection.Clear();
                    ProgramSection.Clear();
                }
                else if (input == ".display")
                {
                    Console.WriteLine();
                    Console.WriteLine(make());
                    Console.WriteLine();
                }
                else
                {
                    doit(input, CurrentBuffer);
                }
            }
        }

        static Func<(string, string)[], string, bool, Package> CompilerFunction;
        static Func<string, bool, string> TranslatorFunction;

        static bool Debug = true;

        static Package Compile(string Source, string Filename)
        {
            if (CompilerFunction == null)
            {
                Console.WriteLine("Compiler not loaded");
                return null;
            }
            return CompilerFunction(new[] { (Source, Filename) }, Path.GetFileNameWithoutExtension(Filename), Debug);
        }
        static string Translate(string Filename)
        {
            if (TranslatorFunction == null)
            {
                Console.WriteLine("Translator not loaded");
                return null;
            }
            return TranslatorFunction(Filename, Debug);
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
                        if (command[i - 1] == '\\')
                        {
                            command = command.Remove(i - 1) + command.Substring(i);
                            i--;
                        }
                        else
                        {
                            escaped = false;
                            parts.Add(command.Substring(si, i - si));
                            si = i + 1;
                        }
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
                case "?":
                    Console.WriteLine();
                    Console.WriteLine("API (pacednl):     " + Pace.CommonLibrary.Info.Version);
                    Console.WriteLine("Loaded compiler:   " + CompilerName);
                    Console.WriteLine("Loaded translator: " + TranslatorName);
                    Console.WriteLine();
                    Console.WriteLine("NOTE: when calling from the command line make sure to enclose commands with arguments with quotes '\"'");
                    Console.WriteLine();
                    Console.WriteLine("List of commands:");
                    Console.WriteLine("help                                    Show this list");
                    //Console.WriteLine("process [source] {-e} {-t}              Compile a source file without importing it, -e to export, -t to translate");
                    Console.WriteLine("compile [source] {name}                 Compile a source file to a package and import it");
                    Console.WriteLine("translate {file}                        Translate the project");
                    Console.WriteLine("export [package|*]                      Export a package or all packages");
                    Console.WriteLine("import [name]                           Import a package");
                    //Console.WriteLine("live                                    Interactive pace");
                    Console.WriteLine("packages                                List all packages");
                    Console.WriteLine("symbols {package}                       List all symbols, optionally you can specify a package");
                    //Console.WriteLine("merge                                   Merge all packages into one");
                    Console.WriteLine("rename [package] [new name]             Rename a package");
                    Console.WriteLine("reset                                   Resets the project");
                    Console.WriteLine("run [file]                              Run a script");
                    Console.WriteLine("set compiler [compiler]                 Loads a compatible compiler from an assembly (.dll or .exe)");
                    Console.WriteLine("set translator [translator]             Loads a compatible translator from an assembly (.dll or .exe)");
                    Console.WriteLine("set dir [path]                          Sets the package import/export directory");
                    Console.WriteLine("set debug [true|false]                  Enable or disable debug mode");
                    Console.WriteLine("attribute [symbol] {attribute} {value}  View, edit, or add an attribute of a symbol");
                    Console.WriteLine("exit                                    Exit the shell");
                    Console.WriteLine("- [symbol]                              Examine a symbol, for children type '.*' after the symbol");
                    Console.WriteLine();
                    break;
//                case "process":
//                    {
//                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
//                        else if (parts.Count > 4) Console.WriteLine("Too many arguments");
//                        else
//                        {
//                            bool export = false;
//                            bool translate = false;
//                            if (parts.Count > 2)
//                            {
//                                if (parts[2] == "-e") export = true;
//                                else if (parts[2] == "-t") translate = true;
//                                else
//                                {
//                                    Console.WriteLine("Invalid arguments");
//                                    break;
//                                }
//                            }
//                            if (parts.Count == 4)
//                            {
//                                if (parts[3] == "-e") export = true;
//                                else if (parts[3] == "-t") translate = true;
//                                else
//                                {
//                                    Console.WriteLine("Invalid arguments");
//                                    break;
//                                }
//                            }
//#if trycatch
//                                try
//                                {
//#endif
//                            string infile = parts[1];
//                            string dir = Path.GetDirectoryName(Path.GetFullPath(infile));
//                            string fn = Path.GetFileNameWithoutExtension(infile);
//                            var l = Compile(File.ReadAllText(infile), infile);
//                            if (l != null)
//                            {
//                                if (export) l.Save();
//                                if (translate) Translate(dir + "\\" + fn);
//                                Project.Current.Unimport(l);
//                            }
//#if trycatch
//                                }
//                                catch(Exception e)
//                                {
//                                    Console.Write("Error occured '");
//                                    Console.Write(e.Message);
//                                    Console.WriteLine("'");
//                                }
//#endif
//                        }
//                        break;
//                    }
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
                            string infile = LastPath = parts[1];
                            string outlib = parts.Count == 3 ? parts[2] : Path.GetFileNameWithoutExtension(infile);
                            if (!PackageNameValid(outlib)) Console.WriteLine("Invalid name");
                            else
                            {
                                var l = Compile(File.ReadAllText(infile), infile);
                                if (l != null)
                                {
                                    l.Name = outlib;
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
                case "translate":
                    {
                        if (parts.Count > 2) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                            try
                            {
#endif
                                string outfile = parts.Count == 2 ? LastPath = parts[1] : LastPath ?? "project";
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
                        if (parts.Count == 1) Console.WriteLine("Too few arguments");
                        else if (parts.Count != 2) Console.WriteLine("Too many arguments");
                        else
                        {
                            if (parts[1] == "*")
                            {
                                foreach(var p in Project.Current.Packages)
                                {
                                    p.Value.Save();
                                }
                            }
                            else
                            {
#if trycatch
                                try
                                {
#endif
                                    var p = Project.Current.Packages[parts[1]];
                                    p.Save();
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
                        }
                        break;
                    }
                case "import":
                    {
                        if (parts.Count == 1) Console.WriteLine("Too few arguments");
                        else if (parts.Count != 2) Console.WriteLine("Too many arguments");
                        else
                        {
                            var res = Package.Load(parts[1],out var package);
                            if (res != null) Console.WriteLine("Error occured: " + res);
                            else
                            {
                                if (res != null) Console.WriteLine("Error occured: " + res);
                            }
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
                //case "merge":
                //    Console.Write($"Merging {Project.Current.Packages.Count} packages containing ");
                //    Project.Current.Merge();
                //    Console.WriteLine(Project.Current.Symbols.Count.ToString() + " symbols in total");
                //    break;
                case "rename":
                    if (parts.Count < 3) Console.WriteLine("Too few arguments");
                    else if (!Project.Current.Packages.TryGetValue(parts[1], out var package)) Console.WriteLine("Package not found");
                    else if (!PackageNameValid(parts[2])) Console.WriteLine("Invalid name for a package");
                    else if (Project.Current.Packages.ContainsKey(parts[2])) Console.WriteLine("Package with specified name already imported");
                    else
                    {
                        Project.Current.Packages.Remove(package.Name);
                        package.Name = parts[2];
                        Project.Current.Packages.Add(package.Name, package);
                    }
                    break;
                case "reset":
                    Project.Current.Packages.Clear();
                    break;
                case "run":
                    if (parts.Count == 1) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                    else
                    {
                        if (!Path.HasExtension(parts[1])) parts[1] += ".pacednscript";
                        if (!File.Exists(parts[1])) Console.WriteLine("File not found");
                        else RunScript(Path.HasExtension(parts[1]) ? parts[1] : parts[1]);
                    }
                    break;
                case "set":
                    if (parts.Count < 3) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                    else if (parts[1] == "compiler")
                    {
                        if (parts.Count == 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                        try
                        {
#endif
                            CompilerFunction = (p1, p2, p3) => ((Func<(string, string)[], string, bool, Package>)Delegate.CreateDelegate(typeof(Func<(string, string)[], string, bool, Package>), Activator.CreateInstance(ImportAssembly(FormatFileName(parts[2]), out CompilerName, "Pace.Compiler").GetType("Pace.Compiler.Compiler")), "Compile"))(p1, p2, p3);
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
                    }
                    else if (parts[1] == "translator")
                    {
                        if (parts.Count == 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
#if trycatch
                        try
                        {
#endif
                            TranslatorFunction = (p1, p2) => ((Func<string, bool, string>)Delegate.CreateDelegate(typeof(Func<string, bool, string>), Activator.CreateInstance(ImportAssembly(FormatFileName(parts[2]), out TranslatorName, "Pace.Translator").GetType("Pace.Translator.Translator")), "Translate"))(p1, p2);
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
                    }
                    else if (parts[1] == "dir")
                    {
                        if (parts.Count == 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
                            string dir = FormatFileName(parts[2]);
                            Settings.PackageDirectory = dir;
                            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        }
                    }
                    else if (parts[1] == "debug")
                    {
                        if (parts.Count == 2) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                        else
                        {
                            if (bool.TryParse(parts[2], out var b)) Debug = b;
                            else Console.WriteLine("Invalid arguments");
                        }
                    }
                    else Console.WriteLine("Invalid arguments");
                    break;
                case "attribute":
                    {
                        if (parts.Count == 1) Console.WriteLine("Too few arguments");
                        else if (parts.Count > 4) Console.WriteLine("Too many arguments");
                        else
                        {
                            var symbol = Project.Current.GetSymbol(parts[1]);
                            if (symbol == null)
                            {
                                Console.WriteLine("Symbol not found");
                            }
                            else
                            {
                                if (parts.Count == 2)
                                {
                                    if (symbol.Attributes.Count == 0) Console.WriteLine("Symbol has no attributes");
                                    else
                                    {
                                        Console.WriteLine($"Attributes of symbol '{symbol.ToString()}':");
                                        foreach(var x in symbol.Attributes)
                                        {
                                            Console.WriteLine();
                                            Console.Write(x.Key);
                                            Console.WriteLine(":");
                                            Console.WriteLine(x.Value);
                                        }
                                    }
                                }
                                else
                                {
                                    if (parts.Count == 4)
                                    {
                                        if (symbol.Attributes.ContainsKey(parts[2]))
                                        {
                                            symbol.Attributes[parts[2]] = parts[3];
                                        }
                                        else
                                        {
                                            symbol.Attributes.Add(parts[2], parts[3]);
                                        }
                                    }
                                    else
                                    {
                                        if (symbol.Attributes.ContainsKey(parts[2]))
                                        {
                                            Console.WriteLine($"Attribute '{parts[2]}' of symbol '{symbol.ToString()}':");
                                            Console.WriteLine(symbol.Attributes[parts[2]]);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"The symbol '{symbol.ToString()}' does not contain the attribute '{parts[2]}'");
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    }
                case "exit":
                    Environment.Exit(0);
                    break;
                case "-":
                    if (parts.Count < 2) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                    else
                    {
                        if (parts[1] == "*")
                        {
                            var symbols = Project.Current.GetAllTopLevelSymbols();
                            if (!symbols.Any())
                                Console.WriteLine("No symbols imported");
                            else
                            {
                                Console.WriteLine("\nList of all top-level symbols:");
                                foreach (var s in symbols)
                                {
                                    Console.WriteLine(s.ToString());
                                }
                                Console.WriteLine();
                            }
                            break;
                        }
                        var symbol = Project.Current.GetSymbol(parts[1]);
                        if (symbol == null)
                        {
                            if (parts[1].EndsWith("::*"))
                            {
                                string packagename = parts[1].Remove(parts[1].Length - 3);
                                if (!Project.Current.Packages.TryGetValue(packagename, out var package))
                                    Console.WriteLine("Package not found");
                                else
                                {
                                    if (package.Symbols.Count == 0)
                                        Console.WriteLine("Package '" + packagename + "' contains no symbols");
                                    else
                                    {
                                        Console.WriteLine("\nList of all symbols in package '" + packagename + "':");
                                        for (int i = 0; i < package.Symbols.Count; i++)
                                        {
                                            Console.WriteLine(package.Symbols[i].ToString());
                                        }
                                        Console.WriteLine();
                                    }
                                }
                            }
                            else Console.WriteLine("Symbol not found");
                            break;
                        }
                        Console.WriteLine();
                        Console.Write($"Symbol '{symbol.ToString()}'");
                        if (parts[1].EndsWith(".*"))
                        {
                            Console.WriteLine();
                            if (symbol.Children == null || symbol.Children.Count == 0)
                            {
                                Console.WriteLine("This symbol contains no children");
                            }
                            else
                            {
                                Console.WriteLine("Children:");
                                for (int i = 0; i < symbol.Children.Count; i++)
                                {
                                    Console.WriteLine(symbol.Children[i].Name);
                                }
                            }
                        }
                        else
                        {
                            if (symbol is ElementSymbol element)
                            {
                                Console.WriteLine(": Element");
                            }
                            else if (symbol is ClassSymbol _class)
                            {
                                Console.WriteLine(": Class");
                            }
                            else if (symbol is StructSymbol _struct)
                            {
                                Console.WriteLine(": Struct");
                            }
                            else if (symbol is VariableSymbol variable)
                            {
                                Console.WriteLine(": Variable");
                                Console.WriteLine("Type: " + variable.Type.ToString());
                                Console.WriteLine("Default Value: " + variable.Value.ToString());
                                Console.WriteLine("Getter: " + variable.Get.ToString());
                                Console.WriteLine("Setter: " + variable.Set.ToString());
                            }
                            else if (symbol is PropertySymbol property)
                            {
                                Console.WriteLine(": Property");
                                Console.WriteLine("Type: " + property.Type.ToString());
                                Console.WriteLine("Getter: " + property.Get.ToString());
                                Console.WriteLine("Setter: " + property.Set.ToString());
                            }
                            if (symbol.Attributes.ContainsKey("document"))
                            {
                                Console.WriteLine();
                                Console.WriteLine("Document:");
                                Console.WriteLine(symbol.Attributes["document"]);
                            }
                        }
                        Console.WriteLine();
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
