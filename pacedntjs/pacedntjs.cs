using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Pace.CommonLibrary;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)
using _Type = Pace.CommonLibrary.Type;

namespace Pace.Translator
{
    public static class Info
    {
        public static string Version = "pacedntjs experimental 0.3.0";
    }
    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    public class Translator
    {
        public void Translate(string Filename, bool debug)
        {
            string rawstring;
            if (Project.Current.EntryPoint == null) rawstring = string.Empty;
            else
            {

                DebugMode = debug;
                Strings.Add(@"
function PaceException(name,message){this.Name=name;this.Message=message;}");

                //implement call stack if debug is enabled
                if (debug) Strings.Add("var cstack=[];");

                Strings.Add(Evaluate(Project.Current.EntryPoint) + ";");
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < Strings.Count; i++)
                {
                    builder.Append(Strings[i]);
                    builder.Append("\n");
                }

                rawstring = builder.ToString();

                //optioanlly export unoptimized
                //File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "pacedntjs_compiled_unoptimized.html", "<html><script>" + rawstring + "</script></html>");

                //catches all sorts of errors like if no internet or server down
                //in this case we have to use the uncompressed result
                if (!debug)
                {
                    try { rawstring = GoogleClosure.Compress(rawstring); }
                    catch { }
                }
            }
            File.WriteAllText(Filename + ".html", "<html><script>" + rawstring + "</script></html>");
        }

        bool DebugMode;

        static readonly string LocalPrefix = "_local_", MemberPrefix = "_member_";

        List<string> Locals = new List<string>();
        Stack<int> LocalSeparators = new Stack<int>();
        void PushLocals()
        {
            LocalSeparators.Push(Locals.Count);
        }
        void PopLocals()
        {
            int i = LocalSeparators.Pop();
            Locals.RemoveRange(i, Locals.Count - i);
        }

        List<string> Strings = new List<string>();
        List<string> RequiresAdded = new List<string>();
        Dictionary<Symbol, string> ProcessedSymbols = new Dictionary<Symbol, string>();
        uint TempVarId = 0;

        //for debugging
        string currentFilename;
        uint currentLine, currentIndex;

        string FormatSymbolName(string name)
        {
            return "_symbol_" + name.Replace("_", "__").Replace('.', '_');
        }
        void ProcessSymbol(Symbol symbol)
        {
            if(symbol.Attributes.TryGetValue("javascriptrequire", out var requireString))
            {
                var require = requireString.Split(',');
                for (int i = 0; i < require.Length; i++)
                {
                    if (!RequiresAdded.Contains(require[i]))
                    {
                        Strings.Add(Project.Current.DataBank[require[i]]);
                        RequiresAdded.Add(require[i]);
                    }
                }
            }
            if (ProcessedSymbols.ContainsKey(symbol)) return;
            if (symbol.Attributes.ContainsKey("javascriptinline") && symbol.Attributes.TryGetValue("javascriptvalue", out string jsval))
            {
                if (DebugMode && symbol.Attributes.TryGetValue("javascriptdebugvalue", out string dbugval)) jsval = dbugval;
                ProcessedSymbols.Add(symbol, jsval);
                return;
            }
            string jsname = FormatSymbolName(symbol.ToString());
            ProcessedSymbols.Add(symbol, jsname);
            var builder = new StringBuilder();
            if (symbol.Attributes.TryGetValue("javascriptvalue", out jsval))
            {
                if (DebugMode && symbol.Attributes.TryGetValue("javascriptdebugvalue", out string dbugval)) jsval = dbugval;
                builder.Append("var ");
                builder.Append(jsname);
                builder.Append("=");
                builder.Append(jsval);
                builder.Append(";");
            }
            else if (symbol is VariableSymbol varsymbol)
            {
                builder.Append("var ");
                builder.Append(jsname);
                builder.Append("=");
                builder.Append(Evaluate(varsymbol.Value));
                builder.Append(";");
            }
            else if (symbol is PropertySymbol propsymbol)
            {
                if (propsymbol.Get != AccessorType.None)
                {
                    builder.Append("function ");
                    builder.Append(jsname);
                    builder.Append("_get(){return ");
                    builder.Append(Evaluate(propsymbol.Getter));
                    builder.Append("};");
                }
                if (propsymbol.Set != AccessorType.None)
                {
                    builder.Append("function ");
                    builder.Append(jsname);
                    PushLocals();
                    Locals.Add("value");
                    builder.Append("_set(" + LocalPrefix + "value){");
                    builder.Append(Evaluate(propsymbol.Setter));
                    builder.Append(";}");
                    PopLocals();
                }
            }
            else if (symbol is ClassSymbol classSymbol)
            {
                builder.Append("function ");
                builder.Append(FormatSymbolName(classSymbol.ToString()));
                builder.AppendLine("(){");
                ProcessTypeChildren(classSymbol.Children, builder);
                builder.Append("}");
            }
            else if (symbol is StructSymbol structSymbol)
            {
                builder.Append("function ");
                builder.Append(FormatSymbolName(structSymbol.ToString()));
                builder.AppendLine("(){");
                ProcessTypeChildren(structSymbol.Children, builder);
            }
            Strings.Add(builder.ToString());
        }
        void ProcessTypeChildren(List<Symbol> symbols, StringBuilder builder)
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                if (symbols[i] is VariableSymbol varSymbol)
                {
                    builder.Append("this.");
                    builder.Append(FormatSymbolName(varSymbol.ToString()));
                    builder.Append("=");
                    builder.Append(Evaluate((varSymbol.Value)));
                    builder.Append(";");
                }
                else if (symbols[i] is PropertySymbol propSymbol)
                {
                    if (propSymbol.Get != AccessorType.None)
                    {
                        builder.Append("this.");
                        builder.Append(FormatSymbolName(propSymbol.ToString()));
                        builder.Append("_get=function(){return ");
                        builder.Append(Evaluate(propSymbol.Getter));
                        builder.Append(";};");
                    }
                    if (propSymbol.Set != AccessorType.None)
                    {
                        builder.Append("this.");
                        builder.Append(FormatSymbolName(propSymbol.ToString()));
                        builder.Append("_set=function(value){");
                        builder.Append(Evaluate(propSymbol.Setter));
                        builder.Append("};");
                    }
                }
            }
        }
        string Evaluate(Value v, bool propsetteropenparam = false)
        {
            if (v is LocalValue localVal)
            {
                if (!Locals.Contains(localVal.Name))
                {
                    Locals.Add(localVal.Name);
                    return "var " + LocalPrefix + localVal.Name;
                }
                return LocalPrefix + localVal.Name;
            }
            else if (v is SymbolValue symbolval)
            {
                var s = Project.Current.GetSymbol(symbolval.Symbol);
                if (!ProcessedSymbols.TryGetValue(s, out var val))
                {
                    ProcessSymbol(s);
                    val = ProcessedSymbols[s];
                }
                if (s is PropertySymbol prop) return (symbolval.Instance == null ? string.Empty : Evaluate(symbolval.Instance) + ".") + (propsetteropenparam ? val + "_set(" : val + "_get()");
                return val;
            }
            else if (v is CallValue callval)
            {
                StringBuilder builder = new StringBuilder();

                //add location to callstack if debug enabled
                if (DebugMode && currentFilename != null)
                {
                    builder.Append($"(function(){{cstack.push(\"{currentFilename} : {currentLine} : {currentIndex}\");let r=");
                }

                builder.Append(Evaluate(callval.Function));
                builder.Append('(');
                var ft = (FunctionType)callval.Function.Type;
                if (ft.Parameters.Count != 0)
                {
                    string[] paramvals = new string[ft.Parameters.Count];
                    for (int i = 0; i < callval.Parameters.Count; i++)
                    {
                        paramvals[callval.Parameters[i].Item1 == null ? i : ft.Parameters.FindIndex(x => x.Item2 == callval.Parameters[i].Item1)] = Evaluate(callval.Parameters[i].Item2);
                    }
                    builder.Append(paramvals[0]);
                    for (int i = 1; i < paramvals.Length; i++)
                    {
                        builder.Append(",");
                        builder.Append(paramvals[i] == null ? "undefined" : paramvals[i]);
                    }
                }
                builder.Append(")");
                if (DebugMode && currentFilename != null)
                {
                    builder.Append(";");
                    builder.Append("cstack.pop();return r;})()");
                }
                return builder.ToString();
            }
            else if (v is OperationValue operationval)
            {
                switch (operationval.OperationType)
                {
                    case OperationType.Is:
                    case OperationType.IsNot:
                        return "is operation not implemented";
                    case OperationType.Not:
                        return "!(" + Evaluate(operationval.Values[0]) + ")";
                    case OperationType.And:
                        return "(" + Evaluate(operationval.Values[0]) + ")&&(" + Evaluate(operationval.Values[1]) + ")";
                    case OperationType.Or:
                        return "(" + Evaluate(operationval.Values[0]) + ")||(" + Evaluate(operationval.Values[1]) +")";
                }
            }
            else if (v is ProceduralValue procedureval)
            {
                PushLocals();
                var s = "(function(){" + EvaluateInstruction(procedureval.Instruction) + "})()";
                PopLocals();
                return s;
            }
            else if (v is ConvertValue convertval)
            {
                return Evaluate(convertval.Base);
            }
            else if (v is FunctionValue funcval)
            {
                StringBuilder builder = new StringBuilder("function(");
                if (funcval.Parameters.Count != 0)
                {
                    PushLocals();
                    Locals.Add(funcval.Parameters[0].Item1);
                    builder.Append(LocalPrefix);
                    builder.Append(funcval.Parameters[0].Item1);
                    for (int i = 1; i < funcval.Parameters.Count; i++)
                    {
                        Locals.Add(funcval.Parameters[i].Item1);
                        builder.Append(",");
                        builder.Append(LocalPrefix);
                        builder.Append(funcval.Parameters[i].Item1);
                    }
                }
                builder.Append("){");
                var functype = (FunctionType)funcval.Type;
                for (int i = 0; i < functype.Parameters.Count; i++)
                {
                    if (functype.Parameters[i].Item3)
                    {
                        builder.Append("if(");
                        builder.Append(funcval.Parameters[i].Item1);
                        builder.Append("==undefined){");
                        builder.Append(funcval.Parameters[i].Item1);
                        builder.Append("=");
                        builder.Append(Evaluate(funcval.Parameters[i].Item2));
                        builder.Append(";");
                    }
                }
                builder.Append("return ");
                builder.Append(Evaluate(funcval.Value));
                builder.Append(";}");
                if (funcval.Parameters.Count != 0) PopLocals();
                return builder.ToString();
            }
            else if (v is RecordValue recordval)
            {
                StringBuilder builder = new StringBuilder("{");
                builder.Append(recordval.Fields[0].Item1);
                builder.Append("=");
                builder.Append(Evaluate(recordval.Fields[0].Item2));
                for (int i = 0; i < recordval.Fields.Count; i++)
                {
                    builder.Append(",");
                    builder.Append(recordval.Fields[i].Item1);
                    builder.Append("=");
                    builder.Append(Evaluate(recordval.Fields[i].Item2));
                }
                builder.Append("}");
            }
            else if (v is CollectionValue collectionval)
            {
                
            }
            else if (v is LiteralValue literalVal)
            {
                if (literalVal.LiteralType == LiteralValueType.Integer || literalVal.LiteralType == LiteralValueType.String) return "\"" + literalVal.Value + "\"";
                if (literalVal.LiteralType == LiteralValueType.Fractional)
                {
                    int sep = literalVal.Value.IndexOf('/');
                    return $"{{{MemberPrefix}Denominator:\"{literalVal.Value.Remove(sep)}\",{MemberPrefix}Numerator:\"{literalVal.Value.Substring(sep+1)}\"}}";
                }
                if (literalVal.LiteralType == LiteralValueType.Boolean)
                {
                    return literalVal.Value.ToLower();
                }
            }
            else if (v is MemberValue memberVal)
            {
                return Evaluate(memberVal.Base) + "." + MemberPrefix + memberVal.Name;
            }
            else if (v is BoxedValue boxedVal)
            {
                return Evaluate(boxedVal.Base);
            }

            return "&ECODE: value not evaluated";
        }
        Instruction GetInnerInstruction(Instruction inst)
        {
            if (inst is IfInstruction ifinst) return ifinst.Instruction;
            if (inst is ElseInstruction elseinst) return elseinst.Instruction;
            return inst;
        }
        string EvaluateInstruction(Instruction inst)
        {
            currentFilename = inst.File;
            currentLine = inst.Line;
            currentIndex = inst.Index;

            if (inst is NoOpInstruction) return ";";
            if (inst is ScopeInstruction scope)
            {
                StringBuilder builder = new StringBuilder();

                bool isloop = false;
                if (scope.Name != null)
                {
                    builder.Append(scope.Name);
                    builder.Append(":");
                }

                //if scope is unnamed and contains no unnamed continues we dont make it a loop
                //useful optimization, since closure does not handle it
                //and it can even more heavily optimize with this performed
                //most scopes are unnamed and contain no continue anyway
                else
                {
                    for (int i = 0; i < scope.Instructions.Count; i++)
                    {
                        if (GetInnerInstruction(scope.Instructions[i]) is ControlInstruction ci && ci.Type == ControlInstructionType.Continue && ci.Name == null)
                        {
                            isloop = true;
                            break;
                        }
                    }
                }

                if (isloop) builder.Append("for(;;){");
                else builder.Append("{");
                for (int i = 0; i < scope.Instructions.Count; i++)
                {
                    builder.Append(EvaluateInstruction(scope.Instructions[i]));
                }
                if (isloop) builder.Append("break;}");
                else builder.Append("}");
                return builder.ToString();
            }
            if (inst is ControlInstruction control)
            {
                if (control.Type == ControlInstructionType.Break) return "break " + control.Name + ";";
                if (control.Type == ControlInstructionType.Continue) return "continue " + control.Name + ";";
            }
            if (inst is ActionInstruction action)
            {
                return Evaluate(action.Value) + ";";
            }
            if (inst is ReturnInstruction returninst)
            {
                return "return " + Evaluate(returninst.Value) + ";";
            }
            if (inst is AssignInstruction assign)
            {
                return Evaluate(assign.Left, true) + "=" + Evaluate(assign.Right) + ";";
            }
            if (inst is IfInstruction ifinst)
            {
                return "if(" + Evaluate(ifinst.Condition) + ")" + EvaluateInstruction(ifinst.Instruction);
            }
            if (inst is ElseInstruction elseinst)
            {
                return "else " + EvaluateInstruction(elseinst.Instruction);
            }
            if (inst is ThrowInstruction throwinst)
            {
                return "throw new PaceException(" + throwinst.Exception + ",\"" + throwinst.Message + "\");";
            }
            if (inst is CatchInstruction catchinst)
            {
                return "catch(" + TempVarId++.ToString() + "){if";
            }
            return "&ECODE: instruction not evaluated";
        }
    }
}
