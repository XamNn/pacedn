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

//This is a user interface, doesn't have functionality on it's own

namespace pacednShell
{
    class Program
    {
        static string AutoexecFile = FormatFileName(@"$bindir\autoexec.pacednscript");
        static string AutoexecDefault =


@"# these commands will be automatically executed on startup
# comments with '#'

# the package directory
set dir $bindir\packages

# uncomment this if you want to se disable debugging by default
# set debugging false

# the default compiler and translator
# one compiler and one translator should be uncommented

set compiler $bindir\pacednc.exe

set translator $bindir\pacedntjs.exe
# set translator $bindir\pacedntc.exe";


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
            if (commands.Count == 0)
            {
                ShellStart();
            }
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

        static int? CompilerNameLeft, CompilerNameTop, TranslatorNameLeft, TranslatorNameTop;

        static void UpdateConsole(int left, int top, string data)
        {
            int _left = Console.CursorLeft;
            int _top = Console.CursorTop;
            Console.SetCursorPosition(left, top);
            Console.WriteLine(data);
            //Console.WriteLine(new string(' ', Console.WindowWidth - Console.CursorLeft));
            Console.SetCursorPosition(_left, _top);
        }

        static void ShellStart()
        {
            Console.WriteLine("PaceDN Shell (0.2.1-0.3.0), pacedn software at https://github.com/XamNn/pacedn, by Samuel Kriikkula");
            Console.WriteLine($"Common library: {Pace.CommonLibrary.Info.Version}");
            CompilerNameTop = Console.CursorTop;
            CompilerNameLeft = 16;
            Console.WriteLine("Compiler:       Not Loaded");
            TranslatorNameTop = Console.CursorTop;
            TranslatorNameLeft = 16;
            Console.WriteLine("Translator:     Not Loaded");
            Console.WriteLine();
        }
        static void Shell()
        {
            while (true)
            {
                int i = Console.CursorTop;
                Console.Write("> ");
                RunCommand(Console.ReadLine());
                if (Console.CursorTop != i + 1) Console.WriteLine();
            }
        }

        static System.Type CompilerType;
        static MethodInfo CompilerFunction;
        static System.Type TranslatorType;
        static MethodInfo TranslatorFunction;

        static bool Debug = true;

        static Package Compile(string Source, string Filename)
        {
#if STATIC_COMPILER
            return new Compiler().Compile(Source, Filename, Debug);
#else
            if (CompilerType == null)
            {
                Console.WriteLine("Compiler not loaded");
                return null;
            }
            return (Package)CompilerFunction.Invoke(Activator.CreateInstance(CompilerType), new object[] { Source, Filename, Debug });
#endif
        }
        static void Translate(string Filename)
        {
#if STATIC_TRANSLATOR
            new Translator().Translate(Filename, Debug);
#else
            if (TranslatorFunction == null)
            {
                Console.WriteLine("Translator not loaded");
                return;
            }
            TranslatorFunction.Invoke(Activator.CreateInstance(TranslatorType), new object[] { Filename, Debug });
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
                    Console.WriteLine("List of commands");
                    Console.WriteLine("help                                    Show this list");
                    Console.WriteLine("process [source] {-e} {-t}              Compile and optionally translate a source file without importing it, -e to export package, -t to translate");
                    Console.WriteLine("compile [source] {name}                 Compile a source file to a package and import it");
                    Console.WriteLine("quickcompile                            A text editor to quickly write code and compile it");
                    Console.WriteLine("translate [file]                        Translate the project");
                    Console.WriteLine("export [package] {file}                 Export a package");
                    Console.WriteLine("import [name] {-s}                      Import a package");
                    Console.WriteLine("packages                                List all packages");
                    Console.WriteLine("symbols {package}                       List all symbols, optionally you can specify a package");
                    Console.WriteLine("merge                                   Merge all packages into one");
                    Console.WriteLine("rename [package] [new name]             Rename a package");
                    Console.WriteLine("reset                                   Resets the project");
                    Console.WriteLine("run [file]                              Run a script");
                    Console.WriteLine("set compiler [compiler]                 Loads a compatible compiler from an assembly (.dll or .exe)");
                    Console.WriteLine("set translator [translator]             Loads a compatible translator from an assembly (.dll or .exe)");
                    Console.WriteLine("set dir [path]                          Sets the package import/export directory");
                    Console.WriteLine("set debug [true|false]                  Enable or disable debug mode");
                    Console.WriteLine("attribute [symbol] attribute {value}    View, edit, or add an attribute of a symbol");
                    Console.WriteLine("exit                                    Exit the shell");
                    Console.WriteLine("- [symbol]                              Examine a symbol");
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
                            string dir = Path.GetDirectoryName(Path.GetFullPath(infile));
                            string fn = Path.GetFileNameWithoutExtension(infile);
                            var l = Compile(File.ReadAllText(infile), infile);
                            if (l != null)
                            {
                                if (export) l.Save(Settings.FormatPackageFilename(fn, false));
                                if (translate)
                                {
                                    var p = Project.Current.Clone();
                                    Project.Current.Import(l);
                                    Translate(dir + "\\" + fn);
                                    Project.Current = p;
                                }
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
                                var l = Compile(File.ReadAllText(infile), infile);
                                if (l != null)
                                {
                                    l.Name = outlib;
                                    Project.Current.Import(l);
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
                        var l = Compile(sb.ToString(), "quickcompiled");
                        if (l != null) Project.Current.Import(l);
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
                            string outfile = parts.Count == 3 ? parts[2] : Settings.FormatPackageFilename(p.Name, false);
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
                        if (parts.Count == 1) Console.WriteLine("Too few arguments");
                        else if (parts.Count != 2) Console.WriteLine("Too many arguments");
                        else
                        {
                            var res = Project.Current.Import(parts[1], Environment.CurrentDirectory);
                            if (!res.IsSuccessful) Console.WriteLine("Error occured: " + res.Message);
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
                    Project.Current = new Project();
                    break;
                case "run":
                    if (parts.Count == 1) Console.WriteLine("Too few arguments");
                    else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                    else if (!File.Exists(parts[1])) Console.WriteLine("File not found");
                    else RunScript(parts[1]);
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
                            var a = ImportAssembly(FormatFileName(parts[2]), out var name, "Pace.Compiler");
                            CompilerType = a.GetType("Pace.Compiler.Compiler");
                            CompilerFunction = CompilerType.GetMethod("Compile");
                            if (CompilerNameLeft != null) UpdateConsole(CompilerNameLeft.Value, CompilerNameTop.Value, name);
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
                            var a = ImportAssembly(FormatFileName(parts[2]), out var name, "Pace.Translator");
                            TranslatorType = a.GetType("Pace.Translator.Translator");
                            TranslatorFunction = TranslatorType.GetMethod("Translate");
                            if (TranslatorNameLeft != null) UpdateConsole(TranslatorNameLeft.Value, TranslatorNameTop.Value, name);
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
                case "bank":
                    if (parts.Count == 1) Console.WriteLine("Too few arguments");
                    else
                    {
                        if (parts[1] == "add")
                        {
                            if (parts.Count < 4) Console.WriteLine("Too few arguments");
                            else
                            {
                                string name = parts[2];
#if trycatch
                                try
                                {
#endif
                                    Project.Current.DataBank.Add(name, parts[3]);
#if trycatch
                                }
                                catch(Exception e)
                                {
                                    Console.WriteLine("Error occured: " + e.Message);
                                }
#endif
                            }
                        }
                        else if (parts[1] == "show")
                        {
                            if (parts.Count == 2) Console.WriteLine("Too few arguments");
#if trycatch
                            try
                            {
#endif
                                Console.WriteLine(Project.Current.DataBank[parts[2]]);
#if trycatch
                            }
                            catch(Exception e)
                            {
                                Console.WriteLine("Error occured: " + e.Message);
                            }
#endif
                        }
                        else if (parts[1] == "embed")
                        {
                            if (parts.Count == 2) Console.WriteLine("Too few arguments");
                            if (!Project.Current.Packages.ContainsKey(parts[3]))
                            {
                                Console.WriteLine("Package not found");
                            }
                            else if (!Project.Current.DataBank.ContainsKey(parts[2]))
                            {
                                Console.WriteLine("Data address not found");
                            }
                            else
                            {
                                if (!Project.Current.Packages[parts[3]].DataBank.ContainsKey(parts[2]))
                                    Project.Current.Packages[parts[3]].DataBank.Add(parts[2], Project.Current.DataBank[parts[2]]);
                            }
                        }
                        else Console.WriteLine("Invalid arguments");
                    }
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
                        if (parts[1].EndsWith(".*"))
                        {
                            if (symbol.Children == null)
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
                                Console.WriteLine("Default Value: " + variable.Value.ToString());
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
                                Console.WriteLine();
                                Console.WriteLine("Document:");
                                Console.WriteLine(symbol.Attributes["document"]);
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
