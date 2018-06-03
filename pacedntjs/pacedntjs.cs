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
        public static readonly string Version = "pacedntjs experimental 0.4.0 targetting pacednl A-1";
    }
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0) Console.WriteLine("Package filename expected"); 
            else if (!File.Exists(args[0])) Console.WriteLine("File not found");
            else
            {
                Project.Current.Import(args[0], Path.GetDirectoryName(args[0]));
                new Translator().Translate(args[0], args.Length != 1 && args[1] == "--debug");
            }
        }
    }

    public class Translator
    {
        public string Translate(string Filename, bool debug)
        {
            string rawstring;
            if (Project.Current.EntryPoint == null) rawstring = string.Empty;
            else
            {
                DebugMode = debug;

                //implement call stack if debug is enabled
                if (debug) Strings.Add("var cstack=[];");
                Strings.Add("function PaceThrow(exception,message){this.exception=exception;this.message=message;}");
                Strings.Add("try{(" + Evaluate(Project.Current.EntryPoint) + ");}catch(e){if(e instanceof PaceThrow)alert(\"Exception \"+e.exception+\":\\n\"+e.message" + (debug ? "+\"\\n\\nStack trace:\\n\"+function(){var s=\"\";cstack.forEach(function(x){s+=x;s+=\"\\n\"});return s;}()" : string.Empty) + ");else throw e;}");
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
            Filename += ".html";
            File.WriteAllText(Filename, "<html><script>" + rawstring + "</script></html>");
            return Filename;
        }

        bool DebugMode;

        static readonly string 
            LocalPrefix = "local$", 
            MemberPrefix = "member$", 
            SymbolPrefix = "symbol$",
            GenericDefaultPrefix = "genericdefault$";

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
        Stack<(string, uint, uint)> LocationStack = new Stack<(string, uint, uint)>();

        string FormatSymbolName(string name)
        {
            return SymbolPrefix + name.Replace("_", "__").Replace('.', '_');
        }
        void ProcessSymbol(Symbol symbol)
        {
            if (ProcessedSymbols.ContainsKey(symbol)) return;
            if (symbol is ClassSymbol || symbol is StructSymbol) return;
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
            Strings.Add(builder.ToString());
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
                var symbol = Project.Current.GetSymbol(symbolval.Symbol);
                StringBuilder sb = new StringBuilder();
                if (symbolval.Instance == null)
                {
                    ProcessSymbol(symbol);
                }
                else
                {
                    sb.Append(Evaluate(symbolval.Instance));
                    sb.Append('.');
                }
                sb.Append(FormatSymbolName(symbolval.Symbol));
                if (symbol is PropertySymbol ps)
                {
                    if (propsetteropenparam) sb.Append("_set(");
                    else sb.Append("_get()");
                }
                return sb.ToString();
            }
            else if (v is CallValue callval)
            {
                StringBuilder builder = new StringBuilder();

                //add location to callstack if debug enabled
                bool pushedcstack;
                (var currentFilename, var currentLine, var currentIndex) = LocationStack.Peek();
                if (DebugMode && currentFilename != null)
                {
                    builder.Append($"(function(){{cstack.push(\"{currentFilename} : {currentLine} : {currentIndex}\");let r=");
                    pushedcstack = true;
                    LocationStack.Push((null, 0, 0));
                }
                else pushedcstack = false;

                builder.Append(Evaluate(callval.Function));
                builder.Append('(');
                var ft = (FunctionType)callval.Function.Type;
                if (ft.Parameters.Count != 0)
                {
                    appendparam(callval.Parameters[0]);
                    for (int i = 1; i < callval.Parameters.Count; i++)
                    {
                        builder.Append(",");
                        appendparam(callval.Parameters[i]);
                    }
                    void appendparam(Value param)
                    {
                        builder.Append(param == null ? "undefined" : Evaluate(param));
                    }
                }
                builder.Append(")");
                if (pushedcstack)
                {
                    LocationStack.Pop();
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
                    case OperationType.IsNull:
                        return "(" + Evaluate(operationval.Values[0]) + "==null";
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
                var s = "function(){" + EvaluateInstruction(procedureval.Instruction) + "}()";
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
                        builder.Append(LocalPrefix + funcval.Parameters[i].Item1);
                        builder.Append("===undefined)");
                        builder.Append(LocalPrefix + funcval.Parameters[i].Item1);
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
                builder.Append(MemberPrefix);
                builder.Append(recordval.Fields[0].Item1);
                builder.Append(":");
                builder.Append(Evaluate(recordval.Fields[0].Item2));
                for (int i = 1; i < recordval.Fields.Count; i++)
                {
                    builder.Append(",");
                    builder.Append(recordval.Fields[i].Item1);
                    builder.Append(MemberPrefix);
                    builder.Append(":");
                    builder.Append(Evaluate(recordval.Fields[i].Item2));
                }
                builder.Append("}");
                return builder.ToString();
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
            else if (v is NewValue newVal)
            {
                var normalType = (NormalType)newVal.Type;
                var symbol = Project.Current.GetSymbol(normalType.Base);
                ProcessSymbol(symbol);
                StringBuilder builder = new StringBuilder("function(){let r={};");
                for (int i = 0; i < symbol.Children.Count; i++)
                {
                    if (symbol.Children[i] is VariableSymbol var)
                    {
                        string sym = symbol.Children[i].ToString();
                        Value val = null;
                        for (int i2 = 0; i2 < newVal.FieldValues.Count; i2++)
                        {
                            if (sym == newVal.FieldValues[i2].Item1)
                            {
                                val = newVal.FieldValues[i2].Item2;
                            }
                        }
                        if (val == null) val = var.Value;
                        builder.Append("r.");
                        builder.Append(FormatSymbolName(sym));
                        builder.Append("=function(self){return ");
                        builder.Append(Evaluate(val));
                        builder.Append(";}(this);");
                    }
                }
                for (int i = 0; i < normalType.Generics.Count; i++)
                {
                    builder.Append("r.");
                    builder.Append(GenericDefaultPrefix);
                    builder.Append(normalType.Generics[i].Item1);
                    builder.Append("=function(){return ");
                    builder.Append(Evaluate(normalType.Generics[i].Item2.GetDefaultValue()));
                    builder.Append(";};");
                }
                builder.Append("return r;}()");
                return builder.ToString();
            }
            else if (v is BoxedValue boxedVal)
            {
                return Evaluate(boxedVal.Base);
            }
            else if (v is ThisValue)
            {
                return "self";
            }
            else if (v is DefaultValue def)
            {
                return GenericDefaultPrefix + (def.Type as GenericType).Name + "()";
            }
            else if (v is NullValue)
            {
                return "null";
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
            LocationStack.Push((inst.File, inst.Line, inst.Index));

            string getit()
            {
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
                    return "throw new PaceThrow(\"" + throwinst.Exception + "\",\"" + throwinst.Message + "\");";
                }
                if (inst is CatchInstruction catchinst)
                {
                }
                return "&ECODE: instruction not evaluated";
            }

            var s = getit();
            LocationStack.Pop();
            return s;
        }
    }
}
