using Pace.CommonLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)

using _Type = Pace.CommonLibrary.Type;

//pacednc compiles source files to Package objects (implemented in pacednl)

namespace Pace.Compiler
{
    public static class Info
    {
        public static string Version = "pacednc experimental 0.2.0 (not functional)";
    }
    static class Program
    {
        public static void Main(string[] args)
        {

        }
    }
    public class Compiler
    {   
        //change these if you want more operator characters or different precedences
        static string LowOperatorChars = @"-+!#¤%£$€´`~:=@?<>\.";
        static string HighOperatorChars = @"*/|&^";

        //change this if you want longer custom operators
        static int OperatorMaxLength = 2;

        static string UnnamedPlaceholder = "$unnamed";
        static StatementPattern[] StatementPatterns;

        Token[] Tokens;
        Package Package;
        string[] Lines;
        Config LocalConfig = new Config();
        List<Config> UsedConfigs = new List<Config>();
        Statement Main;

        public Package Compile(string Source, string LocalPath)
        {

            //Tokenize, see Tokenize function below
            Tokens = Tokenize(Source, new (string, TokenType)[]
            {
                //keywords
                ("element", TokenType.ElementWord),
                ("class", TokenType.ClassWord),
                ("struct", TokenType.StructWord),
                ("return", TokenType.ReturnWord),
                ("null", TokenType.NullWord),
                ("object", TokenType.ObjectWord),
                ("enum", TokenType.EnumWord),
                ("get", TokenType.GetWord),
                ("set", TokenType.SetWord),
                ("alias", TokenType.AliasWord),
                ("if", TokenType.IfWord),
                ("when", TokenType.WhenWord),
                ("then", TokenType.ThenWord),
                ("else", TokenType.ElseWord),
                ("break", TokenType.BreakWord),
                ("continue", TokenType.ContinueWord),
                ("is", TokenType.IsWord),
                ("to", TokenType.ToWord),
                ("import", TokenType.ImportWord),
                ("use", TokenType.UseWord),
                ("main", TokenType.MainWord),
                ("func", TokenType.FuncWord),
                ("public", TokenType.PublicWord),
                ("private", TokenType.PrivateWord),
                ("visible", TokenType.VisibleWord),
                ("this", TokenType.ThisWord),
                ("init", TokenType.InitWord),
                ("clean", TokenType.CleanWord),
                ("for", TokenType.ForWord),
                ("yield", TokenType.YieldWord),
                ("implicit", TokenType.ImplicitWord),
                ("automatic", TokenType.AutomaticWord),
                ("operator", TokenType.OperatorWord),
                ("true", TokenType.TrueWord),
                ("false", TokenType.FalseWord),
                ("not", TokenType.NotWord),
                ("and", TokenType.AndWord),
                ("or", TokenType.OrWord),

                //braces
                ("(", TokenType.PrimaryOpen),
                (")", TokenType.PrimaryClose),
                ("[", TokenType.SecondaryOpen),
                ("]", TokenType.SecondaryClose),
                ("{", TokenType.TertiaryOpen),
                ("}", TokenType.TertiaryClose),

                //reserved operators
                (".", TokenType.Period),
                (";", TokenType.Semicolon),
                (",", TokenType.Comma),
                ("=", TokenType.Equals),
                ("<", TokenType.LeftAngleBracket),
                (">", TokenType.RightAngleBracket),
                ("=>", TokenType.Lambda),
                ("@", TokenType.At),
                ("?", TokenType.QuestionMark),
                ("#", TokenType.Hash),

                //misc
                ("global::", TokenType.Global)
            });

            //!!! optional !!! print tokens to console

            for (int i = 0; i < Tokens.Length; i++)
            {
                Console.WriteLine($"{$"{Tokens[i].Place.Line + 1} : {Tokens[i].Place.Index + 1}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
            }

            //!!! end optional !!!

            //Parse, see StatementPattern struct and NextStatement function
            if (StatementPatterns == null) StatementPatterns = new StatementPattern[]
            {

                //executable statements
                new StatementPattern("=b", StatementType.Scope, null, null, null, null),
                new StatementPattern("t|!=i|=s", StatementType.Label, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, null),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, null),
                new StatementPattern("t|e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null, new object[] { null }),
                new StatementPattern("t|=n|!e", StatementType.Return, new[]{ TokenType.ReturnWord }, null, null, null),
                new StatementPattern("t|=n|!e", StatementType.Yield, new[]{ TokenType.YieldWord }, null, null, null),
                new StatementPattern("t|t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, new[]{ "(", ")" }, null),
                new StatementPattern("t|=n|!t|=s", StatementType.If, new[]{ TokenType.IfWord, TokenType.ThenWord }, null, new[]{ "then" }, null),
                new StatementPattern("t|=s", StatementType.Else, new[]{ TokenType.ElseWord }, null, null, null),

                //field/variable declaration
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.Public }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.SetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.GetWord, TokenType.Lambda }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord }, null, null, new object[] { AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord }, null, null, new object[] { AccessorType.ImpliedPublic, AccessorType.ImpliedPrivate }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.VisibleWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),

                new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, new string[] { "=" }, null),

                //keyword blocks
                new StatementPattern("t|=s", StatementType.Main, new[]{ TokenType.MainWord }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Element, new[]{ TokenType.ElementWord }, null, null, null),
                new StatementPattern("t|=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, new string[] { ">" }, new object[] { (string.Empty, new Place()) }),
                new StatementPattern("t|=i|=b", StatementType.Class, new[]{ TokenType.ClassWord }, null, null, null),
                new StatementPattern("t|t|=l|!=b", StatementType.Class, new[]{ TokenType.ClassWord, TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, null, new string[] { ">" }, new object[] { (string.Empty, new Place()) }),
                new StatementPattern("t|!i|!t|=l|!=b", StatementType.Class, new[]{ TokenType.ClassWord, TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, null, null, null),
                new StatementPattern("t|!=i|!=b", StatementType.Struct, new[]{ TokenType.StructWord }, null, null, null),
                new StatementPattern("t|!=i|=n", StatementType.Enum, new[]{ TokenType.EnumWord }, null, null, null),

                //convertions
                new StatementPattern("t|=n|!t|=p|!t|=n", StatementType.Convertion, new[] { TokenType.ImplicitWord, TokenType.ToWord, TokenType.Equals }, null, null, new object[] { ConvertionType.Implicit }),
                new StatementPattern("t|=n|!t|=p|!t|=n", StatementType.Convertion, new[] { TokenType.AutomaticWord, TokenType.ToWord, TokenType.Equals }, null, null, new object[] { ConvertionType.Automatic }),

                //importing
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, null),
            };

            //match all tokens into statements
            List<Statement> Statements = new List<Statement>();
            {
                int i = 0;
                int lasti = Tokens.Length - 1;
                while (true)
                {
                    if (i == lasti) break;
                    Statements.Add(NextStatement(ref i));
                }
            }

            //create package and other objects
            Package = new CommonLibrary.Package { Name = "CompiledPackage" };
            SymbolStack = new Stack<List<Symbol>>();
            SymbolStack.Push(Project.Current.Symbols);
            SymbolStack.Push(Package.Symbols);
            UsedConfigs.Add(LocalConfig);

            //import and translator specifier statements, must be before any other statements
            //this library depends on these files, 
            //they must exist when translating or importing or doing whatever with this package
            int StatementIndex = 0;
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                if (Statements[StatementIndex].StatementType == StatementType.Import)
                {
                    var n = (((string, Place))Statements[StatementIndex].Data[0]).Item1;
                    Package.Dependencies.Add(n);
                    var l = new Package();
                    var or = l.Read(Settings.FormatPackageFilename(n, LocalPath, true));
                    if (!or.IsSuccessful) Throw(Message.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                    or = Project.Current.Import(l, true);
                    if (!or.IsSuccessful) Throw(Message.OperationResultError1, ThrowType.Error, Tokens[Statements[StatementIndex].Token].Place, or.Message);
                }
                else break;
            }

            //analyze statements
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                AnalyzeGlobalStatement(Statements[StatementIndex]);
            }

            //process pending vars/props
            for (int i = 0; i < PendingSymbols.Count; i++)
            {
                if (PendingSymbols[i].Item1 != null)
                {
                    GenericsPush();
                    for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                    {
                        GenericTypes.Add((PendingSymbols[i].Item1[i2], null));
                    }
                }
                for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage1(PendingSymbols[i].Item2[i2]);
                for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage1(PendingSymbols[i].Item3[i2]);
                if (PendingSymbols[i].Item1 != null) GenericsPop();
            }
            for (int i = 0; i < PendingSymbols.Count; i++)
            {
                if (PendingSymbols[i].Item1 != null)
                {
                    GenericsPush();
                    for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                    {
                        GenericTypes.Add((PendingSymbols[i].Item1[i2], null));
                    }
                }
                for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage2(PendingSymbols[i].Item2[i2]);
                for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage2(PendingSymbols[i].Item3[i2]);
                if (PendingSymbols[i].Item1 != null) GenericsPop();
            }


            if (HasErrors) return null;
            return Package;
        }

        #region errors

        //Errors
        bool HasErrors = false;
        enum Message
        {
            Unknown0,
            CharacterIllegal1,
            TokenIllegal1,
            StatementIllegal0,
            TokenExpected2,
            IdentifierExpected1,
            StringExpected1,
            ValueExpected1,
            TypeExpected1,
            NodeNotAValueOrTypeOrSymbol1,
            IdentifierNotDefined1,
            MemberNotDefined2,
            IdentifierDefined1,
            MemberDefined2,
            PropertyGetterDefined1,
            PropertySetterDefined1,
            ParameterCountIllegal1,
            GenericCountIllegal1,
            ValueNotInvokable1,
            TypeConvertionIllegal2,
            ImplicitConvertionIllegal2,
            ValueNotGettable1,
            ValueNotSettable1,
            PropertyTypeMismatch0,
            CannotDeclareTypelessVariable0,
            CannotAssignTyplessValue0,
            TooFewTypesInMultiType0,
            ValueTypeCycle0,
            FunctionCycle0,
            ElseNotAfterIf0,
            ControlOutsideScope0,
            MultipleMain0,
            OperationResultError1,
            AssignToItself0,
            ConditionAlwaysTrue1,
            ConditionAlwaysFalse1,
        }
        static string GetMessage(Message e, params string[] args)
        {
            switch (e)
            {
                case Message.Unknown0: return "Unknown error occured.";
                case Message.CharacterIllegal1: return $"The character '{args[0]}' does not match any token.";
                case Message.TokenIllegal1: return $"The token '{args[0]}' is not legal in this context.";
                case Message.StatementIllegal0: return "This statement is illegal in this context.";
                case Message.TokenExpected2: return $"Expected '{args[0]}', instead got '{args[1]}'.";
                case Message.IdentifierExpected1: return $"Expected identifier, instead got '{args[0]}'.";
                case Message.StringExpected1: return $"String expected, instead got '{args[0]}'.";
                case Message.ValueExpected1: return $"'{args[0]}' is not a value.";
                case Message.TypeExpected1: return $"'{args[0]}' is not a type.";
                case Message.NodeNotAValueOrTypeOrSymbol1: return $"'{args[0]}' is not a value, type, or symbol.";
                case Message.IdentifierNotDefined1: return $"'{args[0]}' is not defined.";
                case Message.MemberNotDefined2: return $"'{args[0]}' is not defined in '{args[1]}'.";
                case Message.IdentifierDefined1: return $"'{args[0]}' is already defined.";
                case Message.MemberDefined2: return $"'{args[0]}' is already defined in '{args[1]}'.";
                case Message.ParameterCountIllegal1: return $"Wrong amount of parameters, {args[0]} expected.";
                case Message.GenericCountIllegal1: return $"Wrong amount of generics, {args[0]} expected.";
                case Message.ValueNotInvokable1: return $"'{args[0]}' is not invokable.";
                case Message.TypeConvertionIllegal2: return $"Values of type '{args[0]}' cannot be converted to the type '{args[1]}'.";
                case Message.ImplicitConvertionIllegal2: return $"Values of type '{args[0]}' cannot be implicitly converted to the type '{args[1]}'. Try to convert explicitly.";
                case Message.ValueNotGettable1: return $"Cannot get the value of '{args[0]}' in this context.";
                case Message.ValueNotSettable1: return $"Cannot set the value of '{args[0]}' in this context.";
                case Message.PropertyTypeMismatch0: return $"The getter and setter of a property much be the of the same type.";
                case Message.CannotDeclareTypelessVariable0: return $"Variables must have an explicitly specified type.";
                case Message.CannotAssignTyplessValue0: return $"Cannot assign a typeless value to a variable.";
                case Message.TooFewTypesInMultiType0: return "Multitypes must contain at least 2 types.";
                case Message.ValueTypeCycle0: return "A variable of this type in this context causes a cycle.";
                case Message.FunctionCycle0: return "Function cannot return itself.";
                case Message.ElseNotAfterIf0: return "Else statement must be after an if statement.";
                case Message.ControlOutsideScope0: return $"Control statements cannot appear outside scopes.";
                case Message.MultipleMain0: return "Entry point already defined.";
                case Message.OperationResultError1: return $"Operation result error: {args[0]}.";
                case Message.AssignToItself0: return "Assignment to itself.";
                case Message.ConditionAlwaysTrue1: return $"The condition '{args[0]}' always evaluates to true.";
                case Message.ConditionAlwaysFalse1: return $"The condition '{args[0]}' always evaluates to false.";
            }
            return "!!! Error message not defined !!!";
        }
        struct Place
        {
            public ushort Line;
            public ushort Index;
        }
        enum ThrowType
        {
            Fatal,
            Error,
            Warning,
            Message,
        }
        void Throw(Message e, ThrowType tt, Place? p, params string[] args)
        {
            Console.WriteLine();
            ConsoleColor color = Console.ForegroundColor;
            switch (tt)
            {
                case ThrowType.Fatal:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("Fatal error");
                    break;
                case ThrowType.Error:
                    HasErrors = true;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error");
                    break;
                case ThrowType.Warning:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("Warning");
                    break;
                case ThrowType.Message:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Message");
                    break;
            }
            if (p != null)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("├ ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("At " + (p.Value.Line + 1) + " : " + (p.Value.Index + 1));
            }
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write("├ ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(GetMessage(e, args));
            if (p != null)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("| ");
                Console.ForegroundColor = ConsoleColor.White;

                string trimmedLine = Lines[p.Value.Line].TrimStart();
                Console.WriteLine(trimmedLine);

                Console.ForegroundColor = ConsoleColor.Gray;
                Console.Write("| ");
                Console.ForegroundColor = ConsoleColor.White;

                int arrowPos = p.Value.Index - (Lines[p.Value.Line].Length - trimmedLine.Length);
                for (int i = 0; i < arrowPos; i++)
                {
                    Console.Write(' ');	
                }
                Console.WriteLine("↑");
            }
            Console.ForegroundColor = color;
            if (tt == ThrowType.Error)
                HasErrors = true;
            else if (tt == ThrowType.Fatal)
                Environment.Exit((int)e);
        }

        #endregion
        #region lexing

        //Tokens
        enum TokenType : byte
        {
            None,
            EndOfFile,

            //General
            Word,
            LowOperator,
            HighOperator,

            //Reserved operators
            Period,
            Semicolon,
            Comma,
            Equals,
            LeftAngleBracket,
            RightAngleBracket,
            Lambda,
            At,
            QuestionMark,
            Hash,

            //Braces
            PrimaryOpen,
            PrimaryClose,
            SecondaryOpen,
            SecondaryClose,
            TertiaryOpen,
            TertiaryClose,

            //Number literals
            DecInteger,
            DecNonInteger,
            BinInteger,
            HexInteger,

            //String literals
            String,

            //Reserved words
            ElementWord,
            ClassWord,
            StructWord,
            ReturnWord,
            NullWord,
            ObjectWord,
            EnumWord,
            GetWord,
            SetWord,
            AliasWord,
            IfWord,
            WhenWord,
            ThenWord,
            ElseWord,
            BreakWord,
            ContinueWord,
            IsWord,
            ToWord,
            ImportWord,
            UseWord,
            MainWord,
            FuncWord,
            PublicWord,
            PrivateWord,
            VisibleWord,
            ThisWord,
            InitWord,
            CleanWord,
            ForWord,
            YieldWord,
            ImplicitWord,
            AutomaticWord,
            OperatorWord,
            TrueWord,
            FalseWord,
            NotWord,
            AndWord,
            OrWord,

            //Other
            Global, //global::
        }
        struct Token
        {
            public TokenType TokenType;
            public string Match;
            public Place Place;
        }

        bool SubstringEquals(string x, int xindex, string y)
        {
            if (x.Length - xindex < y.Length) return false;
            for (int i = 0; i < y.Length; i++)
            {
                if (y[i] != x[xindex + i]) return false;
            }
            return true;
        }
        bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c);
        }
        bool IsDecChar(char c)
        {
            return char.IsDigit(c);
        }
        bool IsHexChar(char c)
        {
            return char.IsDigit(c) || "abcdefABCDEF".Contains(c);
        }
        bool IsBinChar(char c)
        {
            return c == '0' || c == '1';
        }
        bool IsLowOperatorChar(char c)
        {
            return LowOperatorChars.Contains(c);
        }
        bool IsHighOperatorChar(char c)
        {
            return HighOperatorChars.Contains(c);
        }
        bool IsOperatorChar(char c)
        {
            return IsLowOperatorChar(c) || IsHighOperatorChar(c);
        }

        //This functions matches text with a regular expression
        //if a match is found, it generates a token with the according TokenType
        Token[] Tokenize(string text, (string, TokenType)[] matches)
        {
            Lines = text.Split('\n');
            List<Token> tokenList = new List<Token>();
            for (int line = 0; line < Lines.Length; line++)
            {
                int index = 0;
            start:
                if (index == Lines[line].Length) continue;
                string longestMatch = string.Empty;
                TokenType longestTokenType = TokenType.None;
                int starti = index;
                if (char.IsWhiteSpace(Lines[line][index]))
                {
                    index++;
                    goto start;
                }
                if (Lines[line][index] == '"')
                {
                    int startline = line;
                    int startlineindex = index;
                    index++;
                    while (Lines[line][index] != '"' && (Lines[line].Length == 0 || Lines[line][index - 1] != '\\'))
                    {
                        if (Lines[line].Length == index)
                        {
                            line++;
                            index = 0;
                            if (line == Lines.Length)
                            {
                                Throw(Message.TokenExpected2, ThrowType.Error, new Place { Line = (ushort)line, Index = (ushort)index });
                            }
                        }
                        index++;
                    }
                    string match;
                    if (startline == line)
                    {
                        match = Lines[line].Substring(startlineindex, Lines[line].Length - index);
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder(Lines[startline].Substring(startlineindex));
                        for (int i = startline + 1; i < line; i++)
                        {
                            sb.Append('\n');
                            sb.Append(Lines[i]);
                        }
                        sb.Append(Lines[line].Remove(index));
                        match = sb.ToString();
                    }
                    tokenList.Add(new Token { Match = match, TokenType = TokenType.String, Place = new Place { Line = (ushort)startline, Index = (ushort)startlineindex } });
                    index++;
                    goto start;
                }
                if (index + 1 != Lines[line].Length && Lines[line][index] == '/' && Lines[line][index] == '/')
                {
                    Lines[line] = Lines[line].Remove(index);
                    continue;
                }
                for (int i = 0; i < matches.Length; i++)
                {
                    if (matches[i].Item1.Length > longestMatch.Length && SubstringEquals(Lines[line], index, matches[i].Item1))
                    {
                        longestMatch = matches[i].Item1;
                        longestTokenType = matches[i].Item2;
                    }
                }
                if (IsLowOperatorChar(Lines[line][index]))
                {
                    index++;
                    for (int i = 0; i < OperatorMaxLength; i++)
                    {
                        if (index == Lines[line].Length || !IsOperatorChar(Lines[line][index])) break;
                        index++;
                    }
                    if (index - starti > longestMatch.Length)
                    {
                        longestMatch = Lines[line].Substring(starti, index - starti);
                        longestTokenType = TokenType.LowOperator;
                    }
                    index = starti;
                }
                if (IsHighOperatorChar(Lines[line][index]))
                {
                    index++;
                    for (int i = 1; i < OperatorMaxLength; i++)
                    {
                        if (index == Lines[line].Length || !IsOperatorChar(Lines[line][index])) break;
                    }
                    if (index - starti > longestMatch.Length)
                    {
                        longestMatch = Lines[line].Substring(starti, index - starti);
                        longestTokenType = TokenType.HighOperator;
                    }
                    index = starti;
                }
                if (IsDecChar(Lines[line][index]))
                {
                    index++;
                    while (Lines[line].Length != index && IsBinChar(Lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestMatch.Length)
                    {
                        longestMatch = Lines[line].Substring(starti, index - starti);
                        longestTokenType = TokenType.DecInteger;
                    }
                    index = starti;
                }
                if (IsWordChar(Lines[line][index]))
                {
                    index++;
                    while (Lines[line].Length != index && IsWordChar(Lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestMatch.Length)
                    {
                        longestMatch = Lines[line].Substring(starti, index - starti);
                        longestTokenType = TokenType.Word;
                    }
                    index = starti;
                }
                if (Lines[line].Length > index + 1 && Lines[line][index] == '0' && char.IsLetter(Lines[line][index]))
                {
                    if (Lines[line][index + 1] == 'x' || Lines[line][index + 1] == 'X')
                    {
                        index += 2;
                        while (IsHexChar(Lines[line][index]))
                        {
                            index++;
                            if (Lines[line].Length == index) break;
                        }
                        if (index - starti > longestMatch.Length)
                        {
                            longestMatch = Lines[line].Substring(starti, Lines[line].Length - index);
                            longestTokenType = TokenType.HexInteger;
                        }
                        index = starti;
                    }
                    if (Lines[line][index + 1] == 'b' || Lines[line][index + 1] == 'B')
                    {
                        index += 2;
                        while (IsBinChar(Lines[line][index]))
                        {
                            index++;
                            if (Lines[line].Length == index) break;
                        }
                        if (index - starti > longestMatch.Length)
                        {
                            longestMatch = Lines[line].Substring(starti, Lines[line].Length - index);
                            longestTokenType = TokenType.BinInteger;
                        }
                        index = starti;
                    }
                }
                if (longestTokenType == TokenType.None)
                {
                    Throw(Message.CharacterIllegal1, ThrowType.Error, new Place { Index = (ushort)index, Line = (ushort)line }, Lines[line][index].ToString());
                    index++;
                }
                else
                {
                    tokenList.Add(new Token { Match = longestMatch, TokenType = longestTokenType, Place = new Place { Index = (ushort)index, Line = (ushort)line } });
                    index = starti + longestMatch.Length;
                }
                goto start;
            }
            tokenList.Add(new Token { Match = "END OF FILE", TokenType = TokenType.EndOfFile, Place = new Place { Line = (ushort)(Lines.Length - 1), Index = (ushort)Lines[Lines.Length - 1].Length } });
            return tokenList.ToArray();
        }

        #endregion
        #region parsing

        //Parsing

        //misc helper functions
        void RefNodeList(ref int i, ref List<Node> nl, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (allowEmpties && Tokens[i].TokenType == TokenType.Comma)
                    {
                        nl.Add(null);
                        i++;
                        continue;
                    }
                    nl.Add(NextNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
                }
        }
        List<Node> NodeList(ref int i, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            var x = new List<Node>();
            RefNodeList(ref i, ref x, end, endtokenmatch, allowEmpties);
            return x;
        }
        void RefPrimaryNodeList(ref int i, ref List<Node> nl, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (allowEmpties && Tokens[i].TokenType == TokenType.Comma)
                    {
                        nl.Add(null);
                        i++;
                        continue;
                    }
                    nl.Add(NextPrimaryNode(ref i));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
                }
        }
        List<Node> PrimaryNodeList(ref int i, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            var x = new List<Node>();
            RefPrimaryNodeList(ref i, ref x, end, endtokenmatch, allowEmpties);
            return x;
        }
        void RefDualNodeList(ref int i, ref List<(Node, Node)> nl, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (allowEmpties && Tokens[i].TokenType == TokenType.Comma)
                    {
                        nl.Add((null, null));
                        i++;
                        continue;
                    }
                    nl.Add((NextNode(ref i), NextNode(ref i)));
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
                }
        }
        List<(Node, Node)> DualNodeList(ref int i, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            var x = new List<(Node, Node)>();
            RefDualNodeList(ref i, ref x, end, endtokenmatch, allowEmpties);
            return x;
        }
        void NodeListOrDualNodeList(ref int i, out List<Node> SingleNodeList, out List<(Node, Node)> DualNodeList, TokenType end, string endtokenmatch, bool allowEmpties)
        {
            SingleNodeList = null;
            DualNodeList = null;
            int emptiesToAdd = 0;
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node>();
                return;
            }
            while(Tokens[i].TokenType == TokenType.Comma)
            {
                emptiesToAdd++;
                i++;
            }
            var firstNode = NextNode(ref i);
            if (Tokens[i].TokenType == end)
            {
                i++;
                SingleNodeList = new List<Node>();
                SingleNodeList.Add(firstNode);
                return;
            }
            if (Tokens[i].TokenType == TokenType.Comma)
            {
                i++;
                SingleNodeList = new List<Node>();
                for (int x = 0; x < emptiesToAdd; x++)
                {
                    SingleNodeList.Add(null);
                }
                SingleNodeList.Add(firstNode);
                RefNodeList(ref i, ref SingleNodeList, end, endtokenmatch, allowEmpties);
                return;
            }
            var secondNode = NextNode(ref i);
            DualNodeList = new List<(Node, Node)>();
            for (int x = 0; x < emptiesToAdd;  x++)
            {
                DualNodeList.Add((null, null));
            }
            DualNodeList.Add((firstNode, secondNode));
            if (Tokens[i].TokenType == TokenType.Comma) i++;
            else if (Tokens[i].TokenType == end)
            {
                i++;
                return;
            }
            else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',' expected", Tokens[i].Match);
            RefDualNodeList(ref i, ref DualNodeList, end, endtokenmatch, allowEmpties);
            return;
        }
        void RefIdentifierList(ref int i, ref List<(string, Place)> il, TokenType end, string endtokenmatch)
        {
            if (Tokens[i].TokenType == end) i++;
            else
                while (true)
                {
                    if (Tokens[i].TokenType == TokenType.Word)
                    {
                        il.Add((Tokens[i].Match, Tokens[i].Place));
                        i++;
                    }
                    else Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
                }
        }
        List<(string, Place)> IdentifierList(ref int i, TokenType end, string endtokenmatch)
        {
            List<(string, Place)> x = new List<(string, Place)>();
            RefIdentifierList(ref i, ref x, end, endtokenmatch);
            return x;
        }
        List<Node> GenericList(ref int i)
        {
            if (Tokens[i].TokenType != TokenType.LeftAngleBracket) return null;

            //check to see if it's closed with RightAngleBracket
            //if we encounter a semicolon or a brace that is not opened we stop
            int primaries = 0;
            int secondaries = 0;
            int tertiaries = 0;
            int angles = 0;
            for (int i2 = i + 1; i2 < Tokens.Length; i2++)
            {
                switch (Tokens[i2].TokenType)
                {
                    default:
                        break;
                    case TokenType.Semicolon:
                        return null;
                    case TokenType.RightAngleBracket:
                        if (angles-- == 0) goto genericsClosed;
                        break;
                    case TokenType.PrimaryClose:
                        if (primaries-- == 0) return null;
                        break;
                    case TokenType.SecondaryClose:
                        if (secondaries-- == 0) return null;
                        break;
                    case TokenType.TertiaryClose:
                        if (tertiaries-- == 0) return null;
                        break;
                }
            }
            return null;

        genericsClosed:
            i++;
            return PrimaryNodeList(ref i, TokenType.RightAngleBracket, ">", false);
        }

        //statement parsing
        enum StatementType : byte
        {
            Node,
            DoubleNode,
            VariableDeclaration,
            PropertyDeclaration,

            Scope,
            No_op,

            Element,
            Class,
            Struct,
            Enum,

            Init,
            Clean,

            Convertion,

            Continue,
            Break,
            Return,
            Yield,

            Label,

            Alias,

            Main,
            Initialize,
            Finalize,

            If,
            Else,

            Each,

            Import,
        }
        class Statement
        {
            public StatementType StatementType;
            public int Token;
            public object[] Data; //!!! this array will be reffered as data in the comments below
        }

        //The NextStatement parsing function uses these to parse statements
        //StatementPattern tells the parsing function how to parse a statement of a certain StatementType
        struct StatementPattern
        {
            //Clauses from the format string
            public string[] Clauses;

            //The StatementType for statements that match this StatementPattern
            public StatementType StatementType;

            //'t' clauses will take the tokentype to match from this array in a numeric order
            //the length of this array must match the count of 't' clauses in the format string
            public TokenType[] TokenTypes;

            //'m' clauses will take the total match from this array in a numeric order
            //the length of this array must match the count of 'm' clauses in the format string
            public string[] Matches;

            //contains the first argument for the error thrown from 't' clauses with the throw flag
            public string[] Misc;

            public object[] Data; //these objects will be added to the final statements data, before any data from pattern matching

            //The format string must constist of clauses separated by "|"
            //Clauses must be as follows: "!" (throw flag, throw a compiler error if not a match) "=" (save match to data) [any single character] (the clause
            //Examples of a clause: !t, =s, a
            //Example of a format string: !t|=s|a
            public StatementPattern(string format, StatementType statementType, TokenType[] tokenTypes, string[] matches, string[] misc, object[] data)
            {
                Clauses = format.Split('|');
                StatementType = statementType;
                TokenTypes = tokenTypes;
                Matches = matches;
                Misc = misc;
                Data = data;
            }
        }

        //Statement parsing function
        Statement NextStatement(ref int i)
        {
            //the statement to return
            Statement s = new Statement { Token = i };

            //helper variables for throwing
            Message currentError = Message.Unknown0;
            string[] currentErrorArgs = null;

            //try to match all patterns
            foreach (var p in StatementPatterns)
            {
                //current position in the TokenTypes array
                int tokentypeindex = 0;

                //current position in the matches array
                int matchindex = 0;

                //current position in the Misc array
                int miscindex = 0;

                //data of the final statement
                List<object> data = p.Data == null ? new List<object>() : p.Data.ToList();

                int originali = i;
                foreach (var c in p.Clauses)
                {
                    //flags come before the clause
                    bool dothrow = false;
                    bool save = false;
                    int ci = 0;
                    while (true)
                    {
                        if (c[ci] == '!') dothrow = true;
                        else if (c[ci] == '=') save = true;
                        else break;
                        ci++;
                    }

                    //clause
                    switch (c[ci])
                    {
                        //token, tokentype from the Patterns array (see above)
                        case 't':
                            if (Tokens[i].TokenType != p.TokenTypes[tokentypeindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Message.TokenExpected2;
                                    currentErrorArgs = new[] { p.Misc[miscindex++], Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            i++;
                            break;

                        //match, matches a token with the match from the matches array
                        case 'm':
                            if (Tokens[i].Match != p.Matches[matchindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Message.TokenExpected2;
                                    currentErrorArgs = new[] { p.Matches[matchindex - 1], Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            i++;
                            break;

                        //identfier, matches a token with TokenType.Word
                        case 'i':
                            if (Tokens[i].TokenType != TokenType.Word)
                            {
                                if (dothrow)
                                {
                                    currentError = Message.IdentifierExpected1;
                                    currentErrorArgs = new[] { Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            if (save) data.Add((Tokens[i].Match, Tokens[i].Place));
                            i++;
                            break;

                        //any token
                        case 'a':
                            if (save) data.Add(Tokens[i].Match);
                            i++;
                            break;

                        //node, will always match and throw if invalid
                        case 'n':
                            {
                                var n = NextNode(ref i);
                                if (save) data.Add(n);
                                break;
                            }

                        //primary node, will always match and throw if invalid
                        case 'p':
                            {
                                var n = NextPrimaryNode(ref i);
                                if (save) data.Add(n);
                                break;
                            }

                        //statement, will always match and throw if invalid
                        case 's':
                            {
                                var st = NextStatement(ref i);
                                if (save) data.Add(st);
                                break;
                            }

                        //block, TertiaryOpen and TertiaryClose with multiple Statements inside
                        case 'b':
                            {
                                if (Tokens[i].TokenType == TokenType.TertiaryOpen) i++;
                                else
                                {
                                    if (dothrow)
                                    {
                                        currentError = Message.TokenExpected2;
                                        currentErrorArgs = new[] { "{", Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                List<Statement> stl = new List<Statement>();
                                while (true)
                                {
                                    if (Tokens[i].TokenType == TokenType.TertiaryClose) break;
                                    if (Tokens[i].TokenType == TokenType.EndOfFile)
                                    {
                                        currentError = Message.TokenExpected2;
                                        currentErrorArgs = new[] { "}", Tokens[i].Match };
                                        goto throww;
                                    }
                                    stl.Add(NextStatement(ref i));
                                }
                                i++;
                                if (save) data.Add(stl);
                                break;
                            }

                        //matches a token with TokenType.Semicolon
                        case 'e':
                            {
                                if (Tokens[i].TokenType == TokenType.Semicolon) i++;
                                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose) { }
                                else
                                {
                                    if (dothrow)
                                    {
                                        currentError = Message.TokenExpected2;
                                        currentErrorArgs = new[] { ";", Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                break;
                            }

                        //matches a token with TokenType.String
                        case 'q':
                            {
                                if (Tokens[i].TokenType != TokenType.String)
                                {
                                    if (dothrow)
                                    {
                                        currentError = Message.StringExpected1;
                                        currentErrorArgs = new[] { Tokens[i].Match };
                                        goto throww;
                                    }
                                    goto nextpattern;
                                }
                                if (save) data.Add(Tokens[i].Match);
                                i++;
                                break;
                            }

                        //identifier list, ends with the specified token
                        case 'l':
                            {
                                var l = IdentifierList(ref i, p.TokenTypes[tokentypeindex++], p.Misc[miscindex++]);
                                if (save) data.Add(l);
                                break;
                            }

                        //backtrack, move one token backward
                        case '-':
                            i--;
                            break;
                    }
                }
                //if this is executed it's a match!
                //the first match will be returned
                s.StatementType = p.StatementType;
                s.Data = data.ToArray();
                return s;
                nextpattern:
                i = originali;
            }

            //if no match its a node, declaration, or some of the functions
            {
                if (Tokens[i].TokenType == TokenType.Semicolon)
                {
                    i++;
                    s.StatementType = StatementType.No_op;
                    return s;
                }
                Node n;
                while (true)
                {
                    int t = i;
                    n = NextNode(ref i);
                    if (t == i)
                    {
                        i++;
                        if (i == Tokens.Length - 1) return s;
                    }
                    else break;
                }
                if (Tokens[i].TokenType == TokenType.Semicolon)
                {
                    i++;
                    s.StatementType = StatementType.Node;
                    s.Data = new object[] { n };
                    return s;
                }
                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose)
                {
                    s.StatementType = StatementType.Node;
                    s.Data = new object[] { n };
                    return s;
                }
                Node n2 = NextNode(ref i);
                if (Tokens[i].TokenType == TokenType.Semicolon) i++;
                else if (Tokens[i - 1].TokenType == TokenType.TertiaryClose) { }
                else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, ";", Tokens[i].Match);
                s.StatementType = StatementType.DoubleNode;
                s.Data = new object[] { n, n2 };
                return s;
            }

            //throw with the helper variables
            throww:
            Throw(currentError, ThrowType.Error, Tokens[i].Place, currentErrorArgs);
            s.StatementType = StatementType.No_op;
            return s;
        }

        //nodes 

        enum NodeType : byte
        {
            None,
            Parentheses, //Node                                    //ex: (1 + 1)
            Identifier,                                            //ex: MyVariable
            Literal,                                               //ex: "hello" or 1
            UnaryOperator, //Node                                  //ex: -1
            BinaryOperator, //(Node, Node)                         //ex: 1 * 1
            Assignment, //(Node, Node)                             //ex: x = y
            Procedural, //(Statement)                              //ex: { return }
            Conditional, //(List<(Node, Node)>, Node)              //ex: when x == y then x when x == z then z else y
            Record,                                                //ex: [x = 1, y = 2]
            RecordType,                                            //ex: [int x, int y]
            Collection, //List<Node>                               //ex: {1, 2, 3}
            Function, //(Node, Statement or Node)                  //ex: int(int x, int y) => x - y
            FunctionDeclaration, //Node, (Statement or Node)       //ex: FunctionName(int x, int y) = x - y
            FunctionType, //(Node?, List<Node>)                    //ex: func(int, int)
            MultiType, //List<Node>                                //ex: (x, y)
            ParameterList, //List<(Node, Node)>                    //ex: (int x, int y)
            To, //(Node, Node)                                     //ex: x to MyType
            Is, //(Node, Node)                                     //ex: x is y
            IsNot, //(Node, Node)                                  //ex: x is not y
            Not, //Node                                            //ex: not x
            And, //(Node, Node)                                    //ex: x and y
            Or, //(Node, Node)                                     //ex: x or y
            Xor, //(Node, Node)                                    //ex: x xor y
            Object,                                                //ex: object
            True,                                                  //ex: true
            False,                                                 //ex: false
            Null,                                                  //ex: null
            Global,                                                //ex: global::Element.Variable
        }
        class Node
        {
            public NodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
            public int EndToken;
        }
        string NodeToString(Node n)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Tokens[n.Token].Match);
            for (int i = n.Token + 1; i < n.EndToken; i++)
            {
                sb.Append(' ');
                sb.Append(Tokens[i].Match);
            }
            return sb.ToString();
        }

        //SecondaryNodes are expressions that belong to the parent node (either Node or SecondaryNode)
        enum SecondaryNodeType : byte
        {
            None,
            Member,                       //ex: .MyField
            Call,                         //ex: (1, 2)
            Indexer,                      //ex: [1, 2]
            CollectionTypeSpecifier,      //ex: []
            CollectionValue,              //ex: {1, 2, 3}
            Generics,                     //ex: <int, string>
            ParameterList,                //ex: (int x, int y)
            IndexerFunctionParameterList, //ex: [int index]
            BoxedSpecifier,               //ex: ?
        }
        class SecondaryNode
        {
            public SecondaryNodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
        }

        //Expression trees consist of Nodes and SecondaryNodes in a hierarchy

        //!!! Nodes, PrimaryNodes, SecondaryNode meaning explained
        //Nodes have Node type and can be any of NodeType
        //PrimaryNodes have Node type and can be any of NodeType enum except BinaryOperator, Assignment, and some Functions
        //SecondaryNodes have SecondaryNode type and are all of SecondaryNodeType

        //MyArray[1] + x.y

        //is parsed as (children indented): 

        //+         Node
        //  MyArray PrimaryNode
        //    []    SecondaryNode
        //      1   PrimaryNode
        //  x       PrimaryNode
        //    .y    SecondaryNode

        //The node parsing function
        //unlike the tokenize and statement parsing functions,
        Node NextNode(ref int i)
        {
            //get next primary node
            var n1 = NextPrimaryNode(ref i);
            while (true)
            {
                switch (Tokens[i].TokenType)
                {
                    default:
                        return n1;

                    //When operators have low priority, they take the previous PrimaryNode (n1) and the next PrimaryNode
                    case TokenType.LowOperator:
                    case TokenType.LeftAngleBracket:
                    case TokenType.RightAngleBracket:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //When operators have high priority, they take the previous PrimaryNode (n1) and the next Node
                    case TokenType.HighOperator:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //Assignment operators take the previous PrimaryNode and the next Node
                    case TokenType.Equals:
                        i++;
                        if (n1.NodeType == NodeType.Identifier && n1.Child != null &&
                           (n1.Child.NodeType == SecondaryNodeType.ParameterList || 
                           (n1.Child.NodeType == SecondaryNodeType.Call && ((List<Node>)n1.Child.Data).Count == 0) ||
                           (n1.Child.NodeType == SecondaryNodeType.Generics && n1.Child.Child != null && n1.Child.Child.NodeType == SecondaryNodeType.ParameterList) ||
                           (n1.Child.NodeType == SecondaryNodeType.Generics && n1.Child.Child != null && n1.Child.Child.NodeType == SecondaryNodeType.Call && ((List<Node>)n1.Child.Child.Data).Count == 0)))
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.FunctionDeclaration, Token = n1.Token, EndToken = i };
                        }
                        else
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Assignment, Token = n1.Token, EndToken = i };
                        }
                        break;

                    //value on the left side, type on the right side
                    case TokenType.ToWord:
                        i++;
                        n1 = new Node { Data = (n1, NextPrimaryNode(ref i)), NodeType = NodeType.To, Token = n1.Token, EndToken = i };
                        break;

                    //these work like a binary operators
                    case TokenType.IsWord:
                        i++;
                        if (Tokens[i].TokenType == TokenType.NotWord)
                        {
                            i++;
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.IsNot, Token = n1.Token, EndToken = i };
                        }
                        else
                        {
                            n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Is, Token = n1.Token, EndToken = i };
                        }
                        break;
                    case TokenType.AndWord:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.And, Token = n1.Token, EndToken = i };
                        break;
                    case TokenType.OrWord:
                        i++;
                        n1 = new Node { Data = (n1, NextNode(ref i)), NodeType = NodeType.Or, Token = n1.Token, EndToken = i };
                        break;

                    //Lambda operators take the previous PrimaryNode (node to must be ParameterList) and the next Node or statement if its of types TokenType.Scope or TokenType.Label
                    case TokenType.Lambda:
                        if (n1.NodeType == NodeType.ParameterList || (n1.NodeType == NodeType.Identifier && n1.Child != null && n1.Child.NodeType == SecondaryNodeType.ParameterList))
                        {
                            i++;
                            return new Node { NodeType = NodeType.Function, Data = (n1, NextNode(ref i)), Token = n1.Token, EndToken = i };
                        }
                        else goto default;
                }
            }
        }

        //The primary node parsing function
        //PrimaryNodes use the Node type
        Node NextPrimaryNode(ref int i)
        {
            //the value to return
            var n = new Node { Token = i };

            switch (Tokens[i].TokenType)
            {
                default:
                    Throw(Message.TokenIllegal1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    break;

                //Identifier
                case TokenType.Word:
                    n.NodeType = NodeType.Identifier;
                    i++;
                    break;

                //Parantheses and parameter lists
                case TokenType.PrimaryOpen:
                    {
                        i++;
                        if (Tokens[i].TokenType == TokenType.PrimaryClose)
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = (new List<(Node, Node)>(0));
                        }

                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")", false);
                        if (dual == null)
                        {
                            if (single.Count == 0)
                            {
                                n.NodeType = NodeType.Parentheses;
                                n.Data = single[0];
                            }
                            else
                            {
                                n.NodeType = NodeType.MultiType;
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.ParameterList;
                            n.Data = (new List<Node>(0), dual);
                        }
                        break;
                    }

                //not
                case TokenType.NotWord:
                    n.NodeType = NodeType.Not;
                    i++;
                    n.Data = NextPrimaryNode(ref i);
                    break;

                //literals
                case TokenType.DecInteger:
                case TokenType.DecNonInteger:
                case TokenType.String:
                case TokenType.HexInteger:
                case TokenType.BinInteger:
                    n.NodeType = NodeType.Literal;
                    i++;
                    break;

                //keywords
                case TokenType.NullWord:
                    n.NodeType = NodeType.Null;
                    i++;
                    break;
                case TokenType.TrueWord:
                    n.NodeType = NodeType.True;
                    i++;
                    break;
                case TokenType.FalseWord:
                    n.NodeType = NodeType.False;
                    i++;
                    break;
                case TokenType.ObjectWord:
                    n.NodeType = NodeType.Object;
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                case TokenType.RightAngleBracket:
                    n.NodeType = NodeType.UnaryOperator;
                    i++;
                    n.Data = NextPrimaryNode(ref i);
                    break;

                //might be a function signature
                case TokenType.LeftAngleBracket:
                    {
                        var generics = GenericList(ref i);

                        //if not generics act as unary operator
                        if (generics == null) goto case TokenType.LowOperator;

                        n.NodeType = NodeType.ParameterList;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        n.Data = (generics, DualNodeList(ref i, TokenType.PrimaryClose, ")", false));
                        break;
                    }

                //record
                case TokenType.SecondaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]", false);
                        if (dual == null)
                        {
                            if (single.Count == 0 || single[0].NodeType != NodeType.Assignment)
                            {
                                n.NodeType = NodeType.Collection;
                                n.Data = single;
                            }
                            else
                            {
                                n.NodeType = NodeType.Record;
                                n.Data = single;
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.RecordType;
                            n.Data = dual;
                        }
                        break;
                    }

                //Procedural values are like functions that take no parameters and return a value,
                //this is the value that this node is equal to
                case TokenType.TertiaryOpen:
                case TokenType.At:
                    n.NodeType = NodeType.Procedural;
                    n.Data = NextStatement(ref i);
                    break;

                //conditional values work like this:
                //when (condition) then (value if condition is true)
                //when (other condition) then (value if other condition is true)
                //else (value when all the conditions are false)
                case TokenType.WhenWord:
                    {
                        n.NodeType = NodeType.Conditional;
                        i++;
                        List<(Node, Node)> conditions = new List<(Node, Node)>();
                        Node elseValue;
                        while (true)
                        {
                            var cond = NextNode(ref i);
                            if (Tokens[i].TokenType == TokenType.ThenWord) i++;
                            else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, "then", Tokens[i].Match);
                            conditions.Add((cond, NextNode(ref i)));
                            if (Tokens[i].TokenType == TokenType.WhenWord) continue;
                            if (Tokens[i].TokenType == TokenType.ElseWord)
                            {
                                i++;
                                elseValue = NextNode(ref i);
                                break;
                            }
                            else
                            {
                                Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, "when' or 'else", Tokens[i].Match);
                            }
                        }
                        n.Data = (conditions, elseValue);
                        break;
                    }

                //function type
                case TokenType.FuncWord:
                    {
                        n.NodeType = NodeType.FunctionType;
                        i++;
                        List<(string, Place)> generics = new List<(string, Place)>();
                        if (Tokens[i].TokenType == TokenType.LeftAngleBracket)
                        {
                            i++;
                            RefIdentifierList(ref i, ref generics, TokenType.RightAngleBracket, ">");
                        }
                        Node rettype = null;
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else if (Tokens[i].TokenType == TokenType.LeftAngleBracket)
                        {
                            i++;
                            rettype = (NextNode(ref i));
                            if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                            else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        }
                        else Throw(Message.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        n.Data = (generics, rettype, PrimaryNodeList(ref i, TokenType.PrimaryClose, ")", false));
                        break;
                    }

                //global context
                case TokenType.Global:
                    {
                        n.NodeType = NodeType.Global;
                        i++;
                        n.Data = NextPrimaryNode(ref i);
                        break;
                    }
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            n.EndToken = i;
            return n;
        }

        //SecondaryNode parse function
        bool NextSecondaryNode(ref int i, out SecondaryNode n)
        {
            n = new SecondaryNode { Token = i };

            switch (Tokens[i].TokenType)
            {
                default: return false;

                //member
                case TokenType.Period:
                    {
                        n.NodeType = SecondaryNodeType.Member;
                        i++;
                        n.Data = Tokens[i].Match;
                        if (Tokens[i].TokenType != TokenType.Word)
                        {
                            Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                        }
                        break;
                    }

                //Parentheses and TypedParameterList
                case TokenType.PrimaryOpen:
                    {
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")", true);
                        if (dual == null)
                        {
                            n.NodeType = SecondaryNodeType.Call;
                            n.Data = single;
                            break;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.ParameterList;
                            n.Data = dual;
                            break;
                        }
                    }

                //generic types
                case TokenType.LeftAngleBracket:
                    {
                        var l = GenericList(ref i);
                        if (l == null) return false;
                        n.NodeType = SecondaryNodeType.Generics;
                        n.Data = l;
                        break;
                    }

                //indexer and collection type
                case TokenType.SecondaryOpen:
                    {
                        if (Tokens[i + 1].TokenType == TokenType.SecondaryClose)
                        {
                            n.NodeType = SecondaryNodeType.CollectionTypeSpecifier;
                            i += 2;
                            break;
                        }
                        i++;
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.SecondaryClose, "]", true);
                        if (dual == null)
                        {
                            n.NodeType = SecondaryNodeType.Indexer;
                            n.Data = single;
                        }
                        else
                        {
                            n.NodeType = SecondaryNodeType.IndexerFunctionParameterList;
                            n.Data = dual;
                        }
                        break;
                    }
            }

            //get child if there is
            if (NextSecondaryNode(ref i, out var sn)) n.Child = sn;

            return true;
        }

        #endregion
        #region general semantics

        //semantics

        Stack<List<Symbol>> SymbolStack;

        //the symbol the code is currently 'in' and has access to private symbols inside it
        Symbol Permission;

        //The local variables of the current function or functions
        List<LocalValue> Locals = new List<LocalValue>();

        //the indexes where the first local of a function is
        Stack<int> LocalSeparators = new Stack<int>();

        //the local aliases, first string is the name, second is the Symbol, Value, or _Type
        List<(string, object)> LocalAliases = new List<(string, object)>();

        //same idea as LocalSeparators
        Stack<int> LocalAliasSeparators = new Stack<int>();

        //Generic types when declaring a function
        List<(GenericType, _Type)> GenericTypes = new List<(GenericType, _Type)>();

        //same idea as LocalSeparators, will push and pop both lists above
        Stack<int> GenericSeparators = new Stack<int>();

        //These functions "push" and "pop" (as in a stack) the locals and local aliases
        void LocalsPush()
        {
            LocalSeparators.Push(Locals.Count);
            LocalAliasSeparators.Push(LocalAliases.Count);
        }
        void LocalsPop()
        {
            for (int i = LocalSeparators.Pop(); i < Locals.Count; i++)
            {
                Locals.RemoveAt(i);
            }
            for (int i = LocalAliasSeparators.Pop(); i < LocalAliases.Count; i++)
            {
                LocalAliases.RemoveAt(i);
            }
        }

        void GenericsPush()
        {
            GenericSeparators.Push(GenericTypes.Count);
        }
        void GenericsPop()
        {
            for (int i = GenericSeparators.Pop(); i < GenericTypes.Count; i++)
            {
                GenericTypes.RemoveAt(i);
            }
        }

        bool CanGet(Value v)
        {
            if (v is SymbolValue symbolVal) return CanGet(GetSymbol(symbolVal.Base));
            return true;
        }
        bool CanSet(Value v)
        {
            if (v is LocalValue) return true;
            if (v is SymbolValue symbolVal) return CanSet(GetSymbol(symbolVal.Base));
            return false;
        }

        //These functions return true if the code currently has permission to get or set the variable or property
        //it depends on if the item is private or public, and the current Permission (see above)
        bool CanGet(VariableSymbol s)
        {
            //The item does not have a getter
            if (s.Get == AccessorType.None) return false;

            //publics can be accessed anywhere
            if (s.Get == AccessorType.Public) return true;

            //the permission that we need
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);

            //privates can be accessed only if we have permission
            //first we start with the permission and check if the item is a child
            //then the parent of the permission etc.
            Symbol x = Permission;
            while(x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanGet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent); Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanGet(Symbol s)
        {
            if (s is VariableSymbol var) return CanGet(var);
            if (s is PropertySymbol prop) return CanGet(prop);
            return false;
        }
        bool CanSet(VariableSymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);
            Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent);
            Symbol x = Permission;
            while (x != null)
            {
                if (permissionNeeded == x) return true;
                x = GetSymbol(x.Parent);
            }
            return false;
        }
        bool CanSet(Symbol s)
        {
            if (s is VariableSymbol var) return CanSet(var);
            if (s is PropertySymbol prop) return CanSet(prop);
            return false;
        }
        //used for value type cycle checking
        //if a variable with a value type (not a reference or a "pointer")
        //is the same type as the type being defined, it will cause the type to have undefined size
        bool CausesCycle(_Type thisType, _Type targetType)
        {
            //reference types do not cause cycles
            if (targetType.IsRefType) return false;

            if (thisType is NormalType nt)
            {
                var children = ((StructSymbol)GetSymbol(nt.Base)).Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] is VariableSymbol variable)
                    {
                        if (!variable.Type.IsRefType)
                        {
                            if (variable.Type.Equals(thisType) || CausesCycle(thisType, variable.Type)) return false;
                        }
                    }
                }
            }
            else if (thisType is RecordType rt)
            {
                var fields = rt.Fields;
                for (int i = 0; i < fields.Count; i++)
                {
                    foreach (var f in fields)
                    {
                        if (!f.Item2.IsRefType)
                        {
                            if (f.Item2.Equals(thisType) || CausesCycle(thisType, f.Item2)) return false;
                        }
                    }
                }
            }

            return false;
        }

        //pending symbols
        List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>)> PendingSymbols = new List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>)>();
        Stack<List<PendingVariable>> CurrentPendingVariables = new Stack<List<PendingVariable>>();
        Stack<List<PendingProperty>> CurrentPendingProperties = new Stack<List<PendingProperty>>();

        struct PendingVariable
        {
            public Node TypeNode, ValueNode;
            public SecondaryNode FunctionChildNode;
            public List<(_Type, string, Node)> FunctionParameters;
            public VariableSymbol Symbol;
        }
        struct PendingProperty
        {
            public bool IsSetter;
            public Node TypeNode;
            public Node ValueNode;
            public PropertySymbol Symbol;
        }
        void ProcessPendingVariableStage1(PendingVariable x)
        {
            var type = x.TypeNode == null ? null : NodeToType(x.TypeNode);
            if (x.FunctionChildNode == null)
            {
                if (type == null)
                {
                    Throw(Message.CannotDeclareTypelessVariable0, ThrowType.Error, Tokens[x.ValueNode.Token].Place);
                    x.Symbol.Type = ObjectType.Value;
                }
                else x.Symbol.Type = type;
            }
            else
            {
                ProcessFunctionParameters(x.FunctionChildNode, out var gl, out x.FunctionParameters);
                var functype = new FunctionType { ReturnType = type, Generics = gl };
                for (int i = 0; i < x.FunctionParameters.Count; i++)
                {
                    functype.Parameters.Add((x.FunctionParameters[i].Item1, x.FunctionParameters[i].Item3 != null));
                }
                x.Symbol.Type = functype;
            }
        }
        void ProcessPendingPropertyStage1(PendingProperty x)
        {
            var type = NodeToType(x.TypeNode);
            if (x.Symbol.Type == null) x.Symbol.Type = type;

            //throw if types dont match (getter and setter different types)
            else if (!x.Symbol.Type.Equals(type))
            {
                Throw(Message.PropertyTypeMismatch0, ThrowType.Error, Tokens[x.TypeNode.Token].Place);
            }
        }
        void ProcessPendingVariableStage2(PendingVariable x)
        {
            if (x.FunctionChildNode == null)
            {
                if (x.ValueNode == null) x.Symbol.DefaultValue = NullValue.Value;
                else x.Symbol.DefaultValue = NodeToValue(x.ValueNode, x.Symbol.Type);
            }
            else
            {
                x.Symbol.DefaultValue = ProcessFunctionValue((FunctionType)x.Symbol.Type, x.FunctionParameters, x.ValueNode, true);
            }
        }
        void ProcessPendingPropertyStage2(PendingProperty x)
        {
            throw new NotImplementedException();
        }

        #endregion
        #region node semantics

        //matches an identifier to the corresponding Symbol, _Type, or Value
        object MatchIdentifier(string name)
        {
            //first we check the locals
            for (int i = 0; i < Locals.Count; i++)
            {
                if (Locals[i].Name == name)
                {
                    return Locals[i];
                }
            }

            //local aliases
            for (int i = 0; i < LocalAliases.Count; i++)
            {
                if (LocalAliases[i].Item1 == name) return LocalAliases[i].Item2;
            }

            //then the symbol stack (see above)
            foreach (var sl in SymbolStack)
            {
                for (int i = 0; i < sl.Count; i++)
                {
                    if (sl[i].Name == name) return sl[i];
                }
            }

            //generics
            for (int i = 0; i < GenericTypes.Count; i++)
            {
                if (GenericTypes[i].Item1.Name == name) return GenericTypes[i].Item2 ?? GenericTypes[i].Item1;
            }

            //then the aliases of the libraries that are in use
            for (int i = 0; i < UsedConfigs.Count; i++)
            {
                if (UsedConfigs[i].Aliases.ContainsKey(name))
                {
                    object o = UsedConfigs[i].Aliases[name];
                    if (o is string s) return GetSymbol(s);
                    return o;
                }
            }

            //if nothing, return null
            return null;
        }
        bool LocalNameValid(string s)
        {
            return MatchIdentifier(s) == null;
        }
        bool SymbolNameValid(string s, Symbol nspace)
        {
            return !nspace.Children.Any(x => x.Name == s);
        }
        bool GlobalNameValid(string s)
        {
            return MatchIdentifier(s) == null;
        }

        Symbol GetSymbol(string s)
        {
            var p = s.Split('.');
            var x = Tools.MatchSymbol(p, Package.Symbols);
            if (x == null) return Tools.MatchSymbol(p, Project.Current.Symbols);
            return x;
        }

        //will convert the value to the correctly typed value
        //type can be null, means that Value will be the values own type, or automatically converted
        //all values MUST be convertable to object
        Value ConvertValue(Value value, _Type type, Place? place, bool dothrow = true, bool allowExplicitConvert = false)
        {
            //the value is of correct type
            if (value.Type == type) return value;

            if (type == ObjectType.Value)
            {
                //values of reftypes do not need to be converted
                if (value.Type.IsRefType) return value;

                //if not a reftype, we box
                else return new BoxedValue { Base = value };
            }

            if (value == NullValue.Value && type != null)
            {
                return new ConvertValue { Base = value, Type = type };
            }

            //check the automatic convertions from the libraries in use
            if(type == null)
            {
                for (int i = 0; i < UsedConfigs.Count; i++)
                {
                    if (UsedConfigs[i].ConvertionTypes.ContainsKey((value.Type, type)) && UsedConfigs[i].ConvertionTypes[(value.Type, type)] == ConvertionType.Automatic)
                        return new ConvertValue { Base = value, Type = type };
                }

                //if no automatic convertion, just return the value
                return value;
            }

            //implicit convertions
            for (int i = 0; i < UsedConfigs.Count; i++)
            {
                if (UsedConfigs[i].ConvertionTypes.ContainsKey((value.Type, type)))
                    return new ConvertValue { Base = value, Type = type };
            }

            //look if a convertion exists
            if (Project.Current.Convertions.ContainsKey((value.Type, type)))
            {
                if (allowExplicitConvert)
                {
                    return new ConvertValue { Base = value, Type = type };
                }
                else if (dothrow)
                {
                    Throw(Message.ImplicitConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
                    dothrow = false;
                }
            }

            if (dothrow)
            {
                Throw(Message.TypeConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
            }

            //return null if not convertable
            return NullValue.Value;
        }

        //This function returns the Symbol, Value, or _Type that the Node represents
        //typeContext specifies to what type values should be converted into
        object MatchNode(Node node, bool prioritizeSymbol, _Type typeContext = null, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false)
        {
            //!!! Note: new values should always be run through the ConvertValue function (with the typeContext param)

            //our current value
            Value value = null;

            //current type
            _Type type = null;

            //current symbol
            Symbol symbol = null;

            //the next childnode not processed
            SecondaryNode childNode = node.Child;

            Value MakeValue(Value v)
            {
                return ConvertValue(v, typeContext, Tokens[node.Token].Place, allowExplicitConvert: allowExplicitConvert);
            }

            //get the initial symbol/type/value
            switch (node.NodeType)
            {
                default:
                    Throw(Message.NodeNotAValueOrTypeOrSymbol1, ThrowType.Error, Tokens[node.Token].Place, NodeToString(node));
                    return null;

                //values with global:: at the start will be symbols from the global scope
                case NodeType.Global:
                    {
                        node = (Node)node.Data;
                        if (node.NodeType == NodeType.Identifier)
                        {
                            string name = Tokens[node.Token].Match;
                            for (int i = 0; i < Package.Symbols.Count; i++)
                            {
                                if (Package.Symbols[i].Name == name)
                                {
                                    symbol = Package.Symbols[i];
                                    goto symbolfound;
                                }
                            }
                            symbol = GetSymbol(name);
                            if (symbol != null) goto symbolfound;
                            Throw(Message.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, name);
                            return null;

                            symbolfound:
                            while (childNode != null && childNode.NodeType == SecondaryNodeType.Member)
                            {
                                name = Tokens[childNode.Token + 1].Match;
                                var s = symbol.Children.Find(x => x.Name == name);
                                if (symbol == null)
                                {
                                    Throw(Message.MemberDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, symbol.ToString());
                                    return null;
                                }
                                symbol = s;
                            }
                        }
                        else
                        {
                            Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                            return null;
                        }
                        break;
                    }
                case NodeType.Identifier:
                    {
                        object o = MatchIdentifier(Tokens[node.Token].Match);
                        if (o is Value vv) value = MakeValue(vv);
                        else
                        {
                            if (createLocals)
                            {
                                var local = new LocalValue { Name = Tokens[node.Token].Match };
                                Locals.Add(local);
                                value = local;
                            }
                            else if (o is _Type tt) type = tt;
                            else if (o is Symbol ss)
                            {
                                symbol = ss;
                                while (childNode != null && childNode.NodeType == SecondaryNodeType.Member && symbol is ElementSymbol)
                                {
                                    var s = symbol.Children.Find(x => x.Name == Tokens[childNode.Token + 1].Match);
                                    if (s == null)
                                    {
                                        Throw(Message.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, Tokens[childNode.Token + 1].Match, symbol.ToString());
                                        return null;
                                    }
                                    symbol = s;
                                    childNode = childNode.Child;
                                }
                            }
                            else
                            {
                                Throw(Message.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
                                return null;
                            }
                        }
                        break;
                    }
                case NodeType.Literal:
                    {
                        Token t = Tokens[node.Token];
                        switch (t.TokenType)
                        {
                            case TokenType.String:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.String, Value = t.Match.Substring(1, t.Match.Length - 2) });
                                break;
                            case TokenType.DecInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = t.Match });
                                break;
                            case TokenType.DecNonInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Fractional, Value = t.Match });
                                break;
                            case TokenType.HexInteger:
                                {
                                    BigInteger v = new BigInteger();
                                    BigInteger sixteen = 16;
                                    for (int i = 0; i < t.Match.Length; i++)
                                    {
                                        if (t.Match[i] != '0') v += BigInteger.Pow(sixteen, t.Match.Length - i + 1) * Convert.ToInt32(t.Match[i].ToString(), 16);
                                    }
                                    value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = v.ToString() });
                                    break;
                                }
                            case TokenType.BinInteger:
                                {
                                    BigInteger v = new BigInteger();
                                    BigInteger two = 2;
                                    for (int i = 0; i < t.Match.Length; i++)
                                    {
                                        if (t.Match[i] == '1') v += BigInteger.Pow(two, t.Match.Length - i + 1);
                                    }
                                    value = new LiteralValue { LiteralType = LiteralValueType.Integer, Value = v.ToString() };
                                    break;
                                }
                        }
                        break;
                    }
                case NodeType.Null:
                    value = MakeValue(NullValue.Value);
                    break;
                case NodeType.True:
                    value = MakeValue(LiteralValue.True);
                    break;
                case NodeType.False:
                    value = MakeValue(LiteralValue.False);
                    break;
                case NodeType.Object:
                    type = ObjectType.Value;
                    break;
                case NodeType.Procedural:
                    {
                        if (strictProcedureType) value = StatementToProcedure((Statement)node.Data, typeContext);
                        else value = StatementToProcedure((Statement)node.Data);
                        break;
                    }
                case NodeType.To:
                    {
                        var nodes = ((Node, Node))node.Data;
                        value = NodeToValue(nodes.Item1, NodeToType(nodes.Item2), allowExplicitConvert: true);
                        break;
                    }
                case NodeType.Record:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var recordValue = new RecordValue();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            if (nodeList[i].NodeType == NodeType.Assignment)
                            {
                                var nodes = ((Node, Node))nodeList[i].Data;
                                var name = Tokens[nodes.Item1.Token].Match;
                                if (nodes.Item1.NodeType != NodeType.Identifier)
                                {
                                    Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, name);
                                }
                                recordValue.Values.Add((name, NodeToValue(nodes.Item2, null)));
                            }
                        }
                        break;
                    }
                case NodeType.RecordType:
                    {
                        var nodeList = (List<(Node, Node)>)node.Data;
                        var recordType = new RecordType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            var name = Tokens[nodeList[i].Item2.Token].Match;
                            if (nodeList[i].Item2.NodeType != NodeType.Identifier)
                            {
                                Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                            }
                            if (!recordType.Fields.Add((name, NodeToType(nodeList[i].Item2))))
                            {
                                Throw(Message.IdentifierDefined1, ThrowType.Error, Tokens[nodeList[i].Item2.Token].Place, name);
                            }
                        }
                        break;
                    }
                case NodeType.Collection:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var collection = new CollectionValue();
                        if (nodeList.Count == 1)
                        {
                            var match = MatchNode(nodeList[0], false);
                            if (match is _Type t)
                            {
                                type = new CollectionType { Base = t };
                                break;
                            }
                            if (match is Value v)
                            {
                                collection.Values.Add(v);
                            }
                            else
                            {
                                collection.Values.Add(NullValue.Value);
                                Throw(Message.ValueExpected1, ThrowType.Error, Tokens[nodeList[0].Token].Place, NodeToString(nodeList[0]));
                            }
                        }
                        if (nodeList.Count == 0)
                        {
                            value = new CollectionValue { Type = new CollectionType { Base = ObjectType.Value } };
                            break;
                        }
                        collection.Values.Add(NodeToValue(nodeList[0], null));
                        _Type elementtype = collection.Values[0].Type;
                        for (int i = 1; i < nodeList.Count; i++)
                        {
                            collection.Values.Add(NodeToValue(nodeList[i], elementtype));
                        }
                        collection.Type = new CollectionType { Base = elementtype };
                        value = collection;
                        break;
                    }
                case NodeType.MultiType:
                    {
                        var nodeList = (List<Node>)node.Data;
                        if (nodeList.Count < 2)
                        {
                            Throw(Message.TooFewTypesInMultiType0, ThrowType.Error, Tokens[node.Token].Place);
                            if (nodeList.Count == 1)
                            {
                                return NodeToType(nodeList[0]);
                            }
                            return null;
                        }
                        var multiType = new MultiType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            multiType.Types.Add(NodeToType(nodeList[i]));
                        }
                        type = multiType;
                        break;
                    }
            }

            //convert from symbol to value or type
            //ClassSymbols and StructSymbols can be converted to types
            //VariableSymbols and PropertySymbols can be converted to values
            if(symbol != null)
            {
                if (prioritizeSymbol && childNode == null) return symbol;

                if (symbol is ElementSymbol es && es.Alternate != null) symbol = es.Alternate;

                if (symbol is ClassSymbol classSymbol)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), Boxed = false, RefType = true };

                    //generics
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.Generics)
                    {
                        List<Node> nl = (List<Node>)childNode.Data;
                        for (int i = 0; i < nl.Count; i++)
                        {
                            var t = NodeToType(nl[i]);
                            if (i < classSymbol.Generics.Count)
                                normalType.Generics.Add(classSymbol.Generics[i], t);
                        }
                        if (nl.Count != classSymbol.Generics.Count) Throw(Message.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, classSymbol.Generics.Count.ToString());
                        childNode = childNode.Child;
                    }
                    else if (classSymbol.Generics.Count != 0) return symbol;

                    type = normalType;
                }
                else if (symbol is StructSymbol ss)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), Boxed = false, RefType = true };

                    //see if boxed
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.BoxedSpecifier)
                    {
                        normalType.Boxed = true;
                        normalType.RefType = true;
                        childNode = childNode.Child;
                    }

                    type = normalType;
                }
                else if (symbol is VariableSymbol vs)
                {
                    if (checkCanGet && !CanGet(vs))
                    {
                        Throw(Message.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Base = vs.ToString(), Type = vs.Type });
                }
                else if (symbol is PropertySymbol ps)
                {
                    if (!CanGet(ps))
                    {
                        Throw(Message.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Base = ps.ToString(), Type = ps.Type });
                }
            }

            if (value == null && type == null) return symbol;

            //additions
            if (!ignoreChildren)
            {
                while (childNode != null)
                {
                    if (value != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.Call)
                        {
                            if (value.Type is FunctionType ft)
                            {
                                var callValue = new CallValue { Function = value };
                                List<Node> nl = (List<Node>)childNode.Data;
                                if (nl.Count != ft.Parameters.Count) Throw(Message.ParameterCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, ft.Parameters.Count.ToString());
                                int limit = nl.Count < ft.Parameters.Count ? nl.Count : ft.Parameters.Count;
                                for (int i = 0; i < limit; i++)
                                {
                                    callValue.Parameters.Add(NodeToValue(nl[i], ft.Parameters[i].Item1));
                                }
                                value = MakeValue(callValue);
                            }
                            else
                            {
                                Throw(Message.ValueNotInvokable1, ThrowType.Error, Tokens[childNode.Token].Place, value.ToString());
                            }
                        }
                        else if (childNode.NodeType == SecondaryNodeType.Member)
                        {
                            string name = Tokens[childNode.Token + 1].Match;
                            if (value.Type is NormalType nt)
                            {
                                var variableSymbol = (VariableSymbol)GetSymbol(nt.Base).Children.Find(x => x.Name == name);
                                if (variableSymbol == null)
                                {
                                    Throw(Message.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, nt.Base);
                                }
                                else
                                {
                                    value = ConvertValue(new MemberValue { Base = value, Name = name, Type = variableSymbol.Type }, typeContext, Tokens[node.Token].Place, allowExplicitConvert: allowExplicitConvert);
                                }
                            }
                            else if (value.Type is RecordType rt)
                            {
                                var field = rt.Fields.First(x => x.Item1 == name);
                                if (field.Item1 == null)
                                {
                                    Throw(Message.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, rt.ToString());
                                }
                                else
                                {
                                    value = new MemberValue { Base = value, Name = name, Type = field.Item2 };
                                }
                            }
                            else
                            {
                                Throw(Message.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            }
                        }
                    }
                    else if (type != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.CollectionTypeSpecifier)
                        {
                            type = new CollectionType { Base = type };
                        }
                        else if (childNode.NodeType == SecondaryNodeType.CollectionValue)
                        {
                            value = ConvertValue(new CollectionValue { Type = type }, null, Tokens[node.Token].Place, allowExplicitConvert: allowExplicitConvert);
                        }
                        else
                        {
                            Throw(Message.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            break;
                        }
                    }

                    childNode = childNode.Child;
                }
            }

            return value == null ? (object)type : value;
        }
        Value NodeToValue(Node n, _Type typeContext, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false)
        {
            object o = MatchNode(n, false, typeContext, checkCanGet, createLocals, ignoreChildren, strictProcedureType, allowExplicitConvert);
            if (o is Value v) return v;
            if (o != null) Throw(Message.ValueExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return NullValue.Value;
        }
        _Type NodeToType(Node n)
        {
            object o = MatchNode(n, false);
            if (o is _Type t) return t;
            if (o != null) Throw(Message.TypeExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return ObjectType.Value;
        }

        #endregion
        #region analyzing
        //structural analyzing
        //source file structure like what statements in global context, what inside elements, what inside classes etc.
        //Symbol tree construction to Package.Symbols
        void AnalyzeGlobalStatement(Statement statement)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Message.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.Main:
                    {
                        Main = (Statement)statement.Data[0];
                        break;
                    }
                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1 };
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Message.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        Package.Symbols.Add(symbol);
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek()));
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        SymbolStack.Pop(); break;
                    }
                case StatementType.Alias:
                    {
                        Node n = (Node)statement.Data[0];
                        if(n.NodeType == NodeType.Assignment)
                        {
                            var nodes = ((Node, Node))n.Data;
                            string name = null;
                            if (nodes.Item1.NodeType == NodeType.Identifier)
                            {
                                name = Tokens[nodes.Item1.Token].Match;
                                if (!GlobalNameValid(name))
                                {
                                    Throw(Message.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                    name = null;
                                }
                            }
                            else
                            {
                                Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, Tokens[nodes.Item1.Token].Match);
                            }
                            object value = MatchNode(nodes.Item2, true);
                            if (name != null) LocalConfig.Aliases.Add(name, value);
                        }
                        break;
                    }
            }
        }
        void AnalyzeElementStatement(Statement statement, ElementSymbol element)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Message.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    {
                        AnalyzeVariableDeclaration(statement, element);
                        break;
                    }
                case StatementType.DoubleNode:
                case StatementType.Node:
                    {
                        AnalyzeVariableAssignment(statement, element, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate);
                        break;
                    }
                case StatementType.PropertyDeclaration:
                    {
                        AnalyzeProperty(statement, element);
                        break;
                    }
                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1, Parent = element?.ToString() };
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Message.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek()));
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop();
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        break;
                    }
                case StatementType.Class:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ClassSymbol symbol = new ClassSymbol { Name = name.Item1, Parent = element.ToString() };

                        if (name.Item1 == string.Empty)
                        {
                            symbol.Name = "$alternate";
                            if (element.Alternate == null)
                            {
                                element.Alternate = symbol;
                            }
                            else
                            {
                                Throw(Message.IdentifierDefined1, ThrowType.Error, Tokens[statement.Token].Place, string.Empty);
                            }
                        }
                        else
                        {
                            if (!SymbolNameValid(name.Item1, element))
                            {
                                Throw(Message.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                            }
                            else
                            {
                                element.Children.Add(symbol);
                            }
                        }
                        List<Statement> stl;
                        List<GenericType> localGenericTypes = null;
                        if (statement.Data[1] is List<(string, Place)> generics)
                        {
                            stl = (List<Statement>)statement.Data[2];
                            symbol.Generics.Capacity = generics.Count;
                            localGenericTypes = new List<GenericType>(generics.Count);
                            for (int i = 0; i < generics.Count; i++)
                            {
                                if (!GlobalNameValid(generics[i].Item1))
                                {
                                    Throw(Message.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                                }
                                localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                                symbol.Generics.Add(generics[i].Item1);
                            }
                            GenericsPush();
                            GenericTypes.Capacity += localGenericTypes.Count;
                            for (int i = 0; i < localGenericTypes.Count; i++)
                            {
                                GenericTypes.Add((localGenericTypes[i], null));
                            }
                        }
                        else
                        {
                            stl = (List<Statement>)statement.Data[1];
                        }
                        Permission = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek()));
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeClassStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop();
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        if (symbol.Generics.Count != 0) GenericsPop();
                        Permission = element;
                        break;
                    }
                case StatementType.Struct:
                    {
                        var name = ((string, Place))statement.Data[0];
                        StructSymbol symbol = new StructSymbol { Name = name.Item1, Parent = element.ToString() };
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Message.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        Permission = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek()));
                        SymbolStack.Push(symbol.Children);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeStructStatement(stl[i], symbol);
                        }
                        SymbolStack.Pop();
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        Permission = element;
                        break;
                    }
            }
        }

        //variable and property analyzers are the same in element, class, struct so they are functions
        //the parentType variable
        void AnalyzeVariableDeclaration(Statement statement, Symbol parent)
        {
            AnalyzeVariableAssignment((Statement)statement.Data[2], parent, (AccessorType)statement.Data[0], (AccessorType)statement.Data[1]);
        }
        void AnalyzeVariableAssignment(Statement statement, Symbol parent, AccessorType getType, AccessorType setType, _Type parentType = null)
        {
            Node typeNode;
            Node valueNode;

            if(statement.Data.Length == 1)
            {
                typeNode = null;
                valueNode = (Node)statement.Data[0];
            }
            else
            {
                typeNode = (Node)statement.Data[0];
                valueNode = (Node)statement.Data[1];
            }

            SecondaryNode functionChildNode = null;

            string name = null;

            if (valueNode.NodeType == NodeType.Assignment)
            {
                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.Private;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.Public;
               
                var nodes = ((Node, Node))valueNode.Data;
                valueNode = nodes.Item2;
                if (nodes.Item1.NodeType == NodeType.Identifier)
                {
                    if (nodes.Item1.Child != null)
                    {
                        Throw(Message.TokenIllegal1, ThrowType.Error, Tokens[nodes.Item1.Child.Token].Place, Tokens[nodes.Item1.Child.Token].Match);
                    }
                    name = Tokens[nodes.Item1.Token].Match;
                    if (!SymbolNameValid(name, parent))
                    {
                        Throw(Message.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, parent.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                }
            }
            else if (valueNode.NodeType == NodeType.FunctionDeclaration)
            {
                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.None;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.None;

                var nodes = ((Node, Node))valueNode.Data;
                valueNode = nodes.Item2;
                functionChildNode = nodes.Item1.Child;
                name = Tokens[nodes.Item1.Token].Match;
                if (!SymbolNameValid(name, parent))
                {
                    Throw(Message.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, parent.ToString());
                    name = null;
                }
            }
            else if (valueNode.NodeType == NodeType.Identifier)
            {
                if (getType == AccessorType.ImpliedPrivate) getType = AccessorType.Private;
                else if (getType == AccessorType.ImpliedPublic) getType = AccessorType.Public;
                if (setType == AccessorType.ImpliedPrivate) setType = AccessorType.Private;
                else if (setType == AccessorType.ImpliedPublic) setType = AccessorType.Public;

                if (valueNode.Child != null)
                {
                    Throw(Message.TokenIllegal1, ThrowType.Error, Tokens[valueNode.Child.Token].Place, Tokens[valueNode.Child.Token].Match);
                }
                name = Tokens[valueNode.Token].Match;
                if (!SymbolNameValid(name, parent))
                {
                    Throw(Message.MemberDefined2, ThrowType.Error, Tokens[valueNode.Token].Place, name, parent.ToString());
                    name = null;
                }
                valueNode = null;
            }
            else
            {
                Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[valueNode.Token].Place, Tokens[valueNode.Token].Match);
            }

            var s = new VariableSymbol { Name = name, Parent = parent.ToString(), Get = getType, Set = setType };
            CurrentPendingVariables.Peek().Add(new PendingVariable { Symbol = s, TypeNode = typeNode, ValueNode = valueNode, FunctionChildNode = functionChildNode });

            if (name != null)
            {
                parent.Children.Add(s);
            }
        }
        void AnalyzeProperty(Statement statement, Symbol parent)
        {
            var getType = (AccessorType)statement.Data[0];
            var setType = (AccessorType)statement.Data[1];
            var typeNode = (Node)statement.Data[2];
            var name = ((string, Place))statement.Data[3];
            var valueNode = (Node)statement.Data[4];

            var prop = new PropertySymbol { Get = getType, Set = setType, Name = name.Item1, Parent = parent.ToString() };
            var pendingProp = new PendingProperty { IsSetter = getType == AccessorType.None, TypeNode = typeNode, ValueNode = valueNode };
            var existingSymbol = parent.Children.Find((Symbol x) => x.Name == name.Item1);
            if (existingSymbol == null)
            {
                parent.Children.Add(prop);
                pendingProp.Symbol = prop;
            }
            else if (existingSymbol is PropertySymbol existingProp)
            {
                pendingProp.Symbol = existingProp;
                if (getType == AccessorType.None)
                {
                    if (existingProp.Get == AccessorType.None)
                    {
                        Throw(Message.PropertySetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
                    }
                    else
                    {
                        existingProp.Set = prop.Set;
                        existingProp.Setter = prop.Setter;
                    }
                }
                else if (getType == AccessorType.None)
                {
                    if (existingProp.Set == AccessorType.None)
                    {
                        Throw(Message.PropertyGetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
                    }
                    else
                    {
                        existingProp.Get = prop.Get;
                        existingProp.Getter = prop.Getter;
                    }
                }
            }
            else
            {
                Throw(Message.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, existingSymbol.ToString());
            }

            CurrentPendingProperties.Peek().Add(pendingProp);
        }
        void AnalyzeClassStatement(Statement statement, ClassSymbol _class)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Message.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;
                case StatementType.VariableDeclaration:
                    AnalyzeVariableDeclaration(statement, _class);
                    break;
                case StatementType.DoubleNode:
                case StatementType.Node:
                    AnalyzeVariableAssignment(statement, _class, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _class);
                    break;
            }
        }
        void AnalyzeStructStatement(Statement statement, StructSymbol _struct)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Message.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;
                case StatementType.VariableDeclaration:
                    AnalyzeVariableDeclaration(statement, _struct);
                    break;
                case StatementType.DoubleNode:
                case StatementType.Node:
                    AnalyzeVariableAssignment(statement, _struct, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _struct);
                    break;

            }
        }

        #endregion
        #region procedure semantics
        //used to get the function value from a Node with NodeType.FunctionDeclaration
        void ProcessFunctionParameters(SecondaryNode child, out List<string> generics, out List<(_Type, string, Node)> parameters)
        {
            generics = new List<string>();
            parameters = new List<(_Type, string, Node)>();
            if (child.NodeType == SecondaryNodeType.Generics)
            {
                var genericNodes = (List<Node>)child.Data;
                for (int i = 0; i < genericNodes.Count; i++)
                {
                    string genericName = Tokens[genericNodes[i].Token].Match;
                    if (genericNodes[i].NodeType != NodeType.Identifier)
                    {
                        Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[genericNodes[i].Token].Place, genericName);
                    }
                    generics.Add(genericName);
                }
                child = child.Child;
            }
            if (child.NodeType == SecondaryNodeType.ParameterList)
            {
                var paramNodes = (List<(Node, Node)>)child.Data;
                parameters.Capacity = paramNodes.Count;
                for (int i = 0; i < paramNodes.Count; i++)
                {
                    var type = NodeToType(paramNodes[i].Item1);
                    if (paramNodes[i].Item2.NodeType == NodeType.Assignment)
                    {
                        var nodes = ((Node, Node))paramNodes[i].Item2.Data;
                        string paramName = Tokens[nodes.Item1.Token].Match;
                        if (nodes.Item1.NodeType != NodeType.Identifier) Throw(Message.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, paramName);
                        if (!LocalNameValid(paramName)) Throw(Message.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, paramName);
                    }
                }
            }
        }
        FunctionValue ProcessFunctionValue(FunctionType functype, List<(_Type, string, Node)> parameters, Node valueNode, bool strictRettype)
        {
            var funcvalue = new FunctionValue { Type = functype, Generics = functype.Generics };
            if (functype.Generics.Count != 0)
            {
                GenericsPush();
                for (int i = 0; i < functype.Generics.Count; i++)
                {
                    GenericTypes.Add((new GenericType { Name = functype.Generics[i] }, null));
                }
            }
            if (parameters.Count != 0)
            {
                funcvalue.Parameters.Capacity = parameters.Count;
                LocalsPush();
                for (int i = 0; i < parameters.Count; i++)
                {
                    funcvalue.Parameters.Add((parameters[i].Item2, NodeToValue(parameters[i].Item3, parameters[i].Item1)));
                    Locals.Add(new LocalValue { Name = parameters[i].Item2, Type = parameters[i].Item1 });
                }
            }

            funcvalue.Value = NodeToValue(valueNode, functype.ReturnType, strictProcedureType: strictRettype);
            if (!strictRettype)
            {
                if (funcvalue.Value.Type == functype)
                {
                    Throw(Message.FunctionCycle0, ThrowType.Error, Tokens[valueNode.Token].Place);
                }
                else functype.ReturnType = funcvalue.Value.Type;
            }

            if (parameters.Count != 0) LocalsPop();
            if (functype.Generics.Count != 0) GenericsPop();
            return funcvalue;
        }
        #endregion
    }
}
