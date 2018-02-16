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
                List<(Assembly, string)> translators = new List<(Assembly, string)>();
                List<string> translatorNames = new List<string>();
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string x = dir + @"\pacedntc.dll";
                if (File.Exists(x))
                {
                    Assembly a = Assembly.LoadFile(x);
                    var infoClass = a.GetType("Info");
                    if (infoClass != null)
                    {
                        translators.Add((a, (string)infoClass.GetField("Version", BindingFlags.Static).GetValue(null)));
                        translatorNames.Add("pacedntc");
                    }
                }
                if (translators.Count == 1) translationFunction = translators[0].Item1.GetType("Translator").GetMethod("Translate");
                else if(translators.Count > 1)
                {
                    Console.WriteLine("Select the translator you wish to use");
                    var y = translators[PrintConsoleMenu(translatorNames.ToArray())];
                    translatorVersion = y.Item2;
                    translationFunction = y.Item1.GetType("Translator").GetMethod("Translate");
                }
            }

            //UI Shell
            start:
            Console.Clear();
            Console.WriteLine($"Pacedn shell, Common library \"{pacednl.Info.Version}\", Compiler \"{pacednc.Info.Version}\", Translator \"{translatorVersion}\"");
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
                        Console.WriteLine("process [source]         Compile and translate a source file (full compiling)");
                        Console.WriteLine("translate [translator]   Translate the project");
                        Console.WriteLine("compile [source]         Compile a source file to a library");
                        Console.WriteLine("pack [source]            Compile and export a source file");
                        Console.WriteLine("export [index]           Export a library");
                        Console.WriteLine("import [name] {-s}       Import a library, -s to ignore duplicate symbols");
                        Console.WriteLine("libraries                List all libraries");
                        Console.WriteLine("merge                    Merge all libraries into one");
                        Console.WriteLine("rename [index] [name]    Rename a library");
                        Console.WriteLine("reset                    Resets the project");
                        Console.WriteLine("clear                    Clears the console");
                        break;
                    case "export":
                        if (parts.Count < 2) Console.WriteLine("Too few arguments");
                        else
                        {
                            if (!uint.TryParse(parts[1], out uint index) || index >= Project.Current.Libraries.Count) Console.WriteLine("Invalid library index");
                            else
                            {
                                try
                                {
                                    Project.Current.Libraries[(int)index].Save(Config.FormatLibraryFilename(Project.Current.Libraries[(int)index].Name, null, false));
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"Cannot export library '{e.Message}'");
                                }
                            }
                        }
                        break;
                    case "import":
                        if (parts.Count < 2)
                        {
                            Console.WriteLine("Too few arguments");
                            break;
                        }
                        bool checksymbols = true;
                        if (parts.Count > 2 && parts[2] == "-s") checksymbols = false;
                        Library l = new Library();
                        OperationResult op = l.Read(Config.FormatLibraryFilename(parts[1], null, true));
                        if (!op.IsSuccessful) Console.WriteLine(op.Message);
                        else
                        {
                            op = Project.Current.Import(l, checksymbols);
                            if (!op.IsSuccessful) Console.WriteLine(op.Message);
                        }
                        break;
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
                    case "compile":
                        if (parts.Count == 1) Console.WriteLine("Too few arguments");
                        else
                        {
                            if (!File.Exists(parts[1])) Console.WriteLine("Source file not found");
                            else Compiler.Compile(File.ReadAllText(parts[1]), Path.GetDirectoryName(parts[1]));
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
            Console.SetCursorPosition(0, 0);
            Console.Write("> ");
            Console.Write(items[0]);
            while (true)
            {
                var k = Console.ReadKey(false);
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
