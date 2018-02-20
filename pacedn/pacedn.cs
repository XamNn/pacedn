//#define trycatch

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using pacednl;
using System.Xml;
using pacednc;
using System.Reflection;

//This is the general user interface, doesn't have function on it's own

namespace pacednShell
{
    class Program
    {
        static void Main(string[] args)
        {
            //Look for translators
            string translatorVersion = string.Empty;
            MethodInfo translationFunction = null;
            {
                var l = new List<Assembly>();
                var v = new List<string>();
                var a = ImportTranslator("pacedntc.exe", out var s);
                if (a != null)
                {
                    l.Add(a);
                    v.Add(s);
                }
                a = ImportTranslator("pacedntjs.exe", out s);
                if (a != null)
                {
                    l.Add(a);
                    v.Add(s);
                }
                if (l.Count == 1)
                {
                    translationFunction = l[0].GetType("pacetranslator.Translator").GetMethod("Translate", BindingFlags.Static | BindingFlags.Public);
                    translatorVersion = v[0];
                }
                else if (l.Count > 1)
                {
                    Console.WriteLine("Select the translator you wish to use");
                    var i = PrintConsoleMenu(v.ToArray());
                    translationFunction = l[i].GetType("pacetranslator.Translator").GetMethod("Translate", BindingFlags.Static | BindingFlags.Public);
                    translatorVersion = v[i];
                }
            }

            //UI Shell
            start:
            Console.Clear();
            Console.WriteLine($"Common library \"{pacednl.Info.Version}\", Compiler \"{pacednc.Info.Version}\", Translator \"{translatorVersion}\"");
            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine().Trim();
                if (input == string.Empty) continue;
                List<string> parts = new List<string>();
                int si = 0;
                bool escaped = false;
                for (int i = 0; i < input.Length; i++)
                {
                    if (escaped)
                    {
                        if (input[i] == '"')
                        {
                            escaped = false;
                            parts.Add(input.Substring(si, i - si));
                            si = i + 1;
                        }
                    }
                    else
                    {
                        if (char.IsWhiteSpace(input[i]))
                        {
                            if (si == i) si++;
                            else
                            {
                                parts.Add(input.Substring(si, i - si));
                                si = i + 1;
                            }
                        }
                        else if(input[i] == '"')
                        {
                            escaped = true;
                            si = i + 1;
                        }
                    }
                }
                if(si != input.Length) parts.Add(input.Substring(si, input.Length - si));
                switch (parts[0].ToLower())
                {
                    default:
                        Console.WriteLine("Invalid command");
                        break;

                    case "?":
                    case "help":
                        Console.WriteLine("List of commands");
                        Console.WriteLine("help                     Show this list");
                        Console.WriteLine("process [source] {file}  Compile and translate a source file (full compiling) (reverts)");
                        Console.WriteLine("translate [file]         Translate the project");
                        Console.WriteLine("compile [source] {name}  Compile a source file to a library");
                        Console.WriteLine("pack [source] {file}     Compile and export a source file (reverts)");
                        Console.WriteLine("export [index] {file}    Export a library");
                        Console.WriteLine("import [name] {-s}       Import a library, -s to ignore duplicate symbols");
                        Console.WriteLine("libraries                List all libraries");
                        Console.WriteLine("merge                    Merge all libraries into one");
                        Console.WriteLine("rename [index] [name]    Rename a library");
                        Console.WriteLine("reset                    Resets the project");
                        Console.WriteLine("clear                    Clears the console");
                        break;
                    case "process":
                        {
                            if (parts.Count < 2) Console.WriteLine("Too few arguments");
                            else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                            else
                            {
                                try
                                {
                                    string infile = parts[1];
                                    string indir = Path.GetDirectoryName(infile);
                                    string outfile = parts.Count == 3 ? parts[2] : indir + "\\" + Path.GetFileNameWithoutExtension(infile);
                                    Project p = Project.Current.Clone();
                                    var l = Compiler.Compile(File.ReadAllText(infile), indir);
                                    translationFunction.Invoke(null, new object[] { outfile });
                                    Project.Current = p;
                                }
                                catch(Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
                            }
                            break;
                        }
                    case "translate":
                        {
                            if (parts.Count < 2) Console.WriteLine("Too few arguments");
                            else if (parts.Count > 2) Console.WriteLine("Too many arguments");
                            else
                            {
                                try
                                {
                                    string outfile = parts[1];
                                    translationFunction.Invoke(null, new object[] { outfile });
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
                            }
                            break;
                        }
                    case "compile":
                        {
                            if (parts.Count < 2) Console.WriteLine("Too few arguments");
                            else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                            else
                            {
                                try
                                {
                                    string infile = parts[1];
                                    string outlib = parts.Count == 3 ? parts[2] : Path.GetFileNameWithoutExtension(infile);
                                    if (outlib.Any(c => !char.IsLetter(c))) Console.WriteLine("Invalid name");
                                    else
                                    {
                                        var l = Compiler.Compile(File.ReadAllText(infile), Path.GetDirectoryName(infile));
                                        l.Name = outlib;
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
                            }
                            break;
                        }
                    case "pack":
                        {
                            if (parts.Count < 2) Console.WriteLine("Too few arguments");
                            else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                            else
                            {
                                try
                                {
                                    string infile = parts[1];
                                    string outfile = parts.Count == 3 ? parts[2] : Path.GetDirectoryName(infile) + "\\" + Path.GetFileNameWithoutExtension(infile) + Config.LibraryFileExtention;
                                    Project p = Project.Current.Clone();
                                    var l = Compiler.Compile(File.ReadAllText(infile), Path.GetDirectoryName(infile));
                                    l.Save(outfile);
                                    Project.Current = p;
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
                            }
                            break;
                        }
                    case "export":
                        {
                            if (parts.Count < 2) Console.WriteLine("Too few arguments");
                            else if (parts.Count > 3) Console.WriteLine("Too many arguments");
                            else
                            {
                                try
                                {
                                    int i = int.Parse(parts[1]);
                                    string outfile = parts.Count == 3 ? parts[2] : Config.FormatLibraryFilename(Project.Current.Libraries[i].Name, null, false);
                                    Project.Current.Libraries[i].Save(outfile);
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
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
                                Library l = new Library();
                                try
                                {
                                    infile = Config.FormatLibraryFilename(parts[1], Environment.CurrentDirectory, true);
                                }
                                catch (Exception e)
                                {
                                    Console.Write("Error occured '");
                                    Console.Write(e.Message);
                                    Console.WriteLine("'");
                                }
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
                    case "libraries":
                        if (Project.Current.Libraries.Count == 0) Console.WriteLine("No libraries imported");
                        else
                        {
                            Console.WriteLine("Index\tName                Symbols");
                            for (int i = 0; i < Project.Current.Libraries.Count; i++)
                            {
                                Console.WriteLine(i.ToString() + "\t" + Project.Current.Libraries[i].Name.PadRight(20) + Project.Current.Libraries[i].Symbols.Count);
                            }
                        }
                        break;
                    case "merge":
                        Console.Write($"Merging {Project.Current.Libraries.Count} libraries containing ");
                        Project.Current.Merge();
                        Console.WriteLine(Project.Current.Symbols.Count.ToString() + " symbols in total");
                        break;
                    case "rename":
                        if (parts.Count < 3) Console.WriteLine("Too few arguments");
                        else
                        {
                            if (uint.TryParse(parts[1], out uint index))
                            {
                                if (index >= Project.Current.Libraries.Count) Console.WriteLine("Invalid index");
                                else
                                {
                                    if (parts[2].Any(c => !char.IsLetter(c))) Console.WriteLine("Invalid name");
                                    else
                                    {
                                        Project.Current.Libraries[(int)index].Name = parts[2];
                                    }
                                }
                            }
                            else Console.WriteLine("Invalid index");
                        }
                        break;
                    case "reset":
                        Console.WriteLine("Project intialized");
                        Project.Current = new Project();
                        break;
                    case "clear":
                        Console.Clear();
                        goto start;
                }
            }
        }

        static Assembly ImportTranslator(string file, out string version)
        {
            try
            {
                var s = File.Exists(file);
                var a = Assembly.LoadFile(Path.GetFullPath(file));
                var i = a.GetType("pacetranslator.Info", true);
                version = (string)i.GetField("Version", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                return a;
            }
            catch
            {
                version = null;
                return null;
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
