﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pacednl;

namespace pacetranslator
{
    public static class Info
    {
        public static string Version = "pacedntjs experimental 0.1.3";
    }
    class Program
    {
        static void Main(string[] args)
        {
        }
    }
    public class Translator
    {
        public static void Translate(string filename)
        {
            new Translator(filename);
        }

        StringBuilder output = new StringBuilder();
        int tabs = 0;
        bool linestart = true;

        List<string> locals = new List<string>();
        Stack<int> localseparators = new Stack<int>();

        void LocalsPush()
        {
            localseparators.Push(locals.Count);
        }
        void LocalsPop()
        {
            for (int i = localseparators.Pop(); i < locals.Count; i++)
            {
                locals.RemoveAt(i);
            }
        }

        Translator(string filename)
        {
            output.Append("<!-- Generated by: ");
            output.Append(Info.Version);
            output.Append("-->\n");
            if (Project.Current.EntryPoint != null) {
                output.Append("<!DOCTYPE html><html><script>\n");
                var p = Project.Current.EntryPoint;
                for (int i = 0; i < p.Instructions.Count; i++)
                {
                    WriteInstruction(p.Instructions[i]);
                }
                output.Append("</script></html>");
            }
            File.WriteAllText(filename + (Path.GetFileNameWithoutExtension(filename).Length == Path.GetFileName(filename).Length ? ".html" : string.Empty), output.ToString());
        }

        void Write(string s)
        {
            if (linestart)
            {
                for (int i = 0; i < tabs; i++)
                {
                    output.Append('\t');
                }
                linestart = false;
            }
            output.Append(s);
        }
        void WriteLine(string s)
        {
            Write(s);
            output.AppendLine();
            linestart = true;
        }
        void Escape(string s)
        {
            Write("_");
            Write(s);
        }

        void WriteProcedure(Procedure p, string name, List<string> parameters)
        {
            LocalsPush();
            Write("function ");
            Write(name);
            Write("(");
            if(parameters.Count != 0)
            {
                locals.Add(parameters[0]);
                Escape(parameters[0]);
                for (int i = 1; i < parameters.Count; i++)
                {
                    locals.Add(parameters[i]);
                    Write(",");
                    Escape(parameters[i]);
                }
            }
            WriteLine("){");
            tabs++;
            for (int i = 0; i < p.Instructions.Count; i++)
            {
                WriteInstruction(p.Instructions[i]);
            }
            tabs--;
            WriteLine("}");
            LocalsPop();
        }
        void WriteInstruction(Instruction i)
        {
            string s = string.Empty;
            switch (i.Type)
            {
                case InstructionType.Scope:
                    {
                        var d = ((string, List<Instruction>))i.Data;
                        if(d.Item1 != string.Empty)
                        {
                            Write(d.Item1);
                            WriteLine(":");
                        }
                        WriteLine("while(true){");
                        tabs++;
                        for (int x = 0; x < d.Item2.Count; x++)
                        {
                            WriteInstruction(d.Item2[x]);
                        }
                        WriteLine("break;");
                        tabs--;
                        WriteLine("}");
                        break;
                    }
                case InstructionType.Operation:
                    {
                        WriteValue((Value)i.Data);
                        WriteLine(";");
                        break;
                    }
                case InstructionType.Assign:
                    {
                        var d = ((Value, Value))i.Data;
                        WriteValue(d.Item1);
                        Write("=");
                        WriteValue(d.Item2);
                        WriteLine(";");
                        break;
                    }
                case InstructionType.Break:
                    {
                        var d = (string)i.Data;
                        if(d != string.Empty)
                        {
                            Write("break");
                            Write(" ");
                            Write(d);
                            WriteLine(";");
                        }
                        else
                        {
                            WriteLine("break;");
                        }
                        break;
                    }
                case InstructionType.Continue:
                    {
                        var d = (string)i.Data;
                        if (d != null)
                        {
                            Write("continue");
                            Write(" ");
                            Write(d);
                            WriteLine(";");
                        }
                        else
                        {
                            WriteLine("continue;");
                        }
                        break;
                    }
                case InstructionType.Special:
                    {
                        var d = ((string, string))i.Data;
                        if (d.Item1 != "pacedntjs") break;
                        var parts = d.Item2.Split();
                        if (parts.Length == 0) break;
                        switch (parts[0])
                        {
                            default:
                                break;
                            case "call":
                                {
                                    if (parts.Length != 3) break;
                                    Write(parts[1]);
                                    if (!uint.TryParse(parts[2], out var args)) break;
                                    Write("(");
                                    if(args != 0)
                                    {
                                        Escape("arg0");
                                        for (int x = 1; x < args; x++)
                                        {
                                            Write(",");
                                            Escape("arg");
                                            Write(x.ToString());
                                        }
                                    }
                                    WriteLine(");");
                                    break;
                                }
                        }
                        break;
                    }
            }
        }
        void WriteValue(Value v)
        {
            if(v is LocalValue local)
            {
                if (!locals.Contains(local.Name))
                {
                    Write("var ");
                    locals.Add(local.Name);
                }
                Escape(local.Name);
            }
            else if(v is LiteralValue literal)
            {
                switch (literal.Type)
                {
                    case LiteralValueType.Fractional:
                    case LiteralValueType.Integer:
                        Write(literal.Value);
                        break;
                    case LiteralValueType.String:
                        Write("\"");
                        Write(literal.Value);
                        Write("\"");
                        break;
                }
            }
            else if(v is CallValue call)
            {
                WriteValue(call.Function);
                Write("(");
                if(call.Parameters.Count != 0)
                {
                    WriteValue(call.Parameters[0]);
                    for (int i = 1; i < call.Parameters.Count; i++)
                    {
                        Write(",");
                        WriteValue(call.Parameters[i]);
                    }
                }
                Write(")");
            }
            else if(v is FunctionValue func)
            {
                WriteProcedure(func.Procedure, string.Empty, func.Args);
            }
        }
    }
}
