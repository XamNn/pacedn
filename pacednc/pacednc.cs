using Pace.CommonLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Linq;

//NOTE!! What is "_Type" in this file, is the same as "Type" in pacednl.cs (this is because of ambiguity with System.Type)
using _Type = Pace.CommonLibrary.Type;

//pacednc compiles source files to Package objects (implemented in pacednl)

namespace Pace.Compiler
{
    public static class Info
    {
        public static string Version = "pacednc experimental 0.3.0";
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
        //changing wont break anything but might prevent from using operators defined in some other package
        static string LowOperatorChars = @"*/|&^";
        static string HighOperatorChars = @"-+!#¤%£$€´`~:=@?<>\.";

        static string IndexerGetterName = "Indexer_get";
        static string IndexerSetterName = "Indexer_set";

        static StatementPattern[] StatementPatterns;

        static Place EmptyPlace = new Place();
        static Dictionary<string, string> EmptyAttributes = new Dictionary<string, string>();

        //non-reserved keywords, change these if you want
        static string 
            elementSpecifier = "element", 
            classSpecifier = "class", 
            structSpecifier = "struct", 
            configSpecifier = "config", 
            implicitConvertionWord = "implicit", 
            automaticConvertionWord = "automatic",
            attributesStart = "<attributes>",
            attributesEnd = "</attributes>";

        Token[] Tokens;
        Package Package;
        string[] Lines;
        Config LocalConfig = new Config();
        Node Main;
        List<Config> UsedConfigs = new List<Config>();

        bool DebugMode;

        void UseConfig(Config config)
        {
            if (UsedConfigs.Contains(config)) return;
            UsedConfigs.Add(config);
            for (int i = 0; i < config.Configs.Count; i++)
            {
                Config useConfig = null;
                for (int i2 = 0; i2 < Project.Current.Configs.Count; i2++)
                {
                    if (Project.Current.Configs[i2].Name == config.Configs[i])
                    {
                        useConfig = Project.Current.Configs[i2];
                        break;
                    }
                }
                if (useConfig == null)
                {
                    for (int i2 = 0; i2 < Package.Configs.Count; i2++)
                    {
                        if (Package.Configs[i2].Name == config.Configs[i2])
                        {
                            useConfig = Package.Configs[i2];
                            break;
                        }
                    }
                }
                UseConfig(useConfig);
            }
        }

        public Package Compile(string Source, string Filename, bool Debug)
        {
            DebugMode = Debug;

            //Tokenize, see Tokenize function below
            Tokenize(Source, Filename, new (string, TokenType)[]
            {
                //keywords, feel free to change these if you like!
                ("type", TokenType.TypeWord),
                ("return", TokenType.ReturnWord),
                ("null", TokenType.NullWord),
                ("default", TokenType.DefaultWord),
                ("object", TokenType.ObjectWord),
                ("bool", TokenType.BoolWord),
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
                ("import", TokenType.ImportWord),
                ("use", TokenType.UseWord),
                ("config", TokenType.ConfigWord),
                ("convertion", TokenType.ConvertionWord),
                ("operator", TokenType.OperatorWord),
                ("main", TokenType.MainWord),
                ("func", TokenType.FuncWord),
                ("public", TokenType.PublicWord),
                ("private", TokenType.PrivateWord),
                ("visible", TokenType.VisibleWord),
                ("this", TokenType.ThisWord),
                ("new", TokenType.NewWord),
                ("for", TokenType.ForWord),
                ("yield", TokenType.YieldWord),
                ("true", TokenType.TrueWord),
                ("false", TokenType.FalseWord),
                ("not", TokenType.NotWord),
                ("and", TokenType.AndWord),
                ("or", TokenType.OrWord),
                ("throw", TokenType.ThrowWord),
                ("catch", TokenType.CatchWord),

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
                (":", TokenType.Colon),
                ("<", TokenType.LeftAngleBracket),
                (">", TokenType.RightAngleBracket),
                ("=>", TokenType.Lambda),
                ("==>", TokenType.DoubleLambda),
                ("@", TokenType.At),
                ("?", TokenType.QuestionMark),

                //misc
                ("global::", TokenType.Global)
            });

            //!!! optional !!! print tokens to console

            //for (int i = 0; i < Tokens.Length; i++)
            //{
            //    Console.WriteLine($"{$"{Tokens[i].Place.Line + 1} : {Tokens[i].Place.Index + 1}".PadRight(10)} {Tokens[i].TokenType.ToString().PadRight(20)} {Tokens[i].Match}");
            //}

            //!!! end optional !!!

            //Parse, see StatementPattern struct and NextStatement function
            if (StatementPatterns == null) StatementPatterns = new StatementPattern[]
            {

                //executable statements
                new StatementPattern("=b", StatementType.Scope, null, null, null, new object[] { ((string)null, EmptyPlace) }),
                new StatementPattern("t|!=i|=b", StatementType.Scope, new[]{ TokenType.At }, null, null, null),
                new StatementPattern("t|!e", StatementType.Break, new[]{ TokenType.BreakWord }, null, null, new object[] { ((string)null, EmptyPlace),
                new StatementPattern("t|!=i|t|!e", StatementType.Break, new[]{ TokenType.At, TokenType.BreakWord }, null, null, null),}),
                new StatementPattern("t|!e", StatementType.Continue, new[]{ TokenType.ContinueWord }, null, null, new object[] { ((string)null, EmptyPlace) }),
                new StatementPattern("t|!=i|t|!e", StatementType.Scope, new[]{ TokenType.At, TokenType.ContinueWord }, null, null, null),
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
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=n|=i|t|=n", StatementType.PropertyDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Private }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Public, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord, TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.None }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord, TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord, TokenType.SetWord }, null, null, new object[] { AccessorType.Private, AccessorType.Private }),
                new StatementPattern("t|=n|=i|t|=s", StatementType.PropertyDeclaration, new[] { TokenType.SetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=n|=i|t|=s", StatementType.PropertyDeclaration, new[] { TokenType.GetWord, TokenType.DoubleLambda }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PrivateWord }, null, null, new object[] { AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.PublicWord }, null, null, new object[] { AccessorType.ImpliedPublic, AccessorType.ImpliedPrivate }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.GetWord }, null, null, new object[] { AccessorType.Private, AccessorType.None }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.SetWord }, null, null, new object[] { AccessorType.None, AccessorType.Public }),
                new StatementPattern("t|=s", StatementType.VariableDeclaration, new[] { TokenType.VisibleWord }, null, null, new object[] { AccessorType.Public, AccessorType.Private }),

                new StatementPattern("t|!=i|!t|=n|!e", StatementType.Alias, new[] { TokenType.AliasWord, TokenType.Equals }, null, new string[] { "=" }, null),

                //keyword blocks
                new StatementPattern("m|=i|=b", StatementType.Element, null, new string[] { elementSpecifier }, null, null),
                new StatementPattern("m|=b", StatementType.Class, null, new string[] { classSpecifier }, null, new object[] { (string.Empty, EmptyPlace) }),
                new StatementPattern("m|=i|=b", StatementType.Class, null, new string[] { classSpecifier }, null, null),
                new StatementPattern("m|t|=l|!=b", StatementType.Class, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { classSpecifier }, new string[] { ">" }, new object[] { (string.Empty, EmptyPlace) }),
                new StatementPattern("m|!=i|!t|=l|!=b", StatementType.Class, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { classSpecifier }, new string[] { ">" }, null),
                new StatementPattern("m|=b", StatementType.Struct, null, new string[] { structSpecifier }, null, new object[] { (string.Empty, EmptyPlace) }),
                new StatementPattern("m|=i|=b", StatementType.Struct, null, new string[] { structSpecifier }, null, null),
                new StatementPattern("m|t|=l|!=b", StatementType.Struct, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { structSpecifier }, new string[] { ">" }, new object[] { (string.Empty, EmptyPlace) }),
                new StatementPattern("m|!=i|!t|=l|!=b", StatementType.Struct, new[]{ TokenType.LeftAngleBracket, TokenType.RightAngleBracket }, new string[] { structSpecifier }, new string[] { ">" }, null),
                new StatementPattern("m|!=i|!=b", StatementType.Config, null, new string[] { configSpecifier }, null, null),
                new StatementPattern("t|!t|=n|!e", StatementType.Main, new[]{ TokenType.MainWord, TokenType.Equals }, null, new string[] { "=" }, null),

                //config statements
                new StatementPattern("t|=n|!e", StatementType.OperatorDeclaration, new TokenType[] { TokenType.OperatorWord }, null, null, new object[] { null }),
                new StatementPattern("m|t|=p|!e", StatementType.ConvertionModeDefinition, new[] { TokenType.ConvertionWord, TokenType.Colon }, new string[] { implicitConvertionWord }, null, new object[] { ConvertionType.Implicit }),
                new StatementPattern("m|t|=p|!e", StatementType.ConvertionModeDefinition, new[] { TokenType.ConvertionWord, TokenType.Colon }, new string[] { automaticConvertionWord }, null, new object[] { ConvertionType.Automatic }),

                //convertions
                new StatementPattern("t|=p|!t|=n|!e", StatementType.ConvertionDeclaration, new TokenType[] { TokenType.ConvertionWord, TokenType.Equals }, null, new string[] { "=" }, null),

                //importing
                new StatementPattern("t|!=i|!e", StatementType.Import, new[] { TokenType.ImportWord }, null, null, null),
                new StatementPattern("t|!=i|!e", StatementType.Use, new[] { TokenType.UseWord }, null, null, null),

                //throws
                new StatementPattern("t|!=i|!=q|!e", StatementType.Throw, new TokenType[] { TokenType.ThrowWord }, null, null, null),
                new StatementPattern("t|=l|!t|=s", StatementType.Catch, new TokenType[] { TokenType.CatchWord, TokenType.ThenWord }, null, null, null),
                new StatementPattern("t|!t|!=l|!t|=s", StatementType.Catch, new TokenType[] { TokenType.CatchWord, TokenType.PrimaryOpen, TokenType.PrimaryClose }, null, null, null),

                //attributes
                new StatementPattern("t|=s", StatementType.Attributes, new[] { TokenType.Attributes }, null, null, null)
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

            //create package
            Package = new Package { Name = "CompiledPackage" };
            UsedConfigs.Add(LocalConfig);

            //import statements, must be before any other statements
            //this package depends on these packages
            int StatementIndex = 0;
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                if (Statements[StatementIndex].StatementType == StatementType.Import)
                {
                    var name = ((string, Place))Statements[StatementIndex].Data[0];
                    Package.Dependencies.Add(name.Item1);
                    var opresult = Project.Current.Import(name.Item1, null);
                    if (!opresult.IsSuccessful) Throw(Text.OperationResultError1, ThrowType.Error, name.Item2, opresult.Message);
                }
                else break;
            }

            //analyze statements
            //elements, configs, etc
            for (; StatementIndex < Statements.Count; StatementIndex++)
            {
                AnalyzeGlobalStatement(Statements[StatementIndex], null);
            }

            //process pending stuff
            //things like variables, properties, convertions, and configs "pend" until all types are defined
            //since they might use types defined later in the file
            //variables and properties pend once again because the values might need to know the types of some vars/props
            for (int i = 0; i < PendingSymbols.Count; i++)
            {
                if (PendingSymbols[i].Item1 != null)
                {
                    GenericsPush();
                    for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                    {
                        GenericTypes.Add(PendingSymbols[i].Item1[i2]);
                    }
                }
                CurrentSymbol = PendingSymbols[i].Item4;
                for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage1(PendingSymbols[i].Item2[i2]);
                for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage1(PendingSymbols[i].Item3[i2]);
                CurrentSymbol = null;
                if (PendingSymbols[i].Item1 != null) GenericsPop();
            }
            for (int i = 0; i < PendingConvertions.Count; i++)
            {
                ProcessPendingConvertion(PendingConvertions[i]);
            }
            for (int i = 0; i < PendingConfigs.Count; i++)
            {
                for (int i2 = 0; i2 < PendingConfigs[i].Item1.Count; i2++)
                {
                    AnalyzeConfigStatement(PendingConfigs[i].Item1[i2], PendingConfigs[i].Item2);
                }
            }
            for (int i = 0; i < PendingSymbols.Count; i++)
            {
                if (PendingSymbols[i].Item1 != null)
                {
                    GenericsPush();
                    for (int i2 = 0; i2 < PendingSymbols[i].Item1.Count; i2++)
                    {
                        GenericTypes.Add(PendingSymbols[i].Item1[i2]);
                    }
                }
                CurrentSymbol = PendingSymbols[i].Item4;
                for (int i2 = 0; i2 < PendingSymbols[i].Item2.Count; i2++) ProcessPendingVariableStage2(PendingSymbols[i].Item2[i2]);
                for (int i2 = 0; i2 < PendingSymbols[i].Item3.Count; i2++) ProcessPendingPropertyStage2(PendingSymbols[i].Item3[i2]);
                CurrentSymbol = null;
                if (PendingSymbols[i].Item1 != null) GenericsPop();
            }

            //main is processed last
            if (Main != null) Package.EntryPoint = NodeToValue(Main, null, strictProcedureType: true);

            //return if compilation is successful
            if (HasErrors) return null;
            return Package;
        }

        #region errors

        //Errors
        bool HasErrors = false;
        enum Text
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
            TooManyParameters0,
            MissingParameter1,
            DuplicateParameter1,
            PositionalParameterAfterNamed0,
            IndexerIllegal0,
            GenericCountIllegal1,
            ParameterCountIllegal1,
            ValueNotInvokable1,
            TypeConvertionIllegal2,
            ImplicitConvertionIllegal2,
            ValueNotGettable1,
            ValueNotSettable1,
            PropertyTypeMismatch0,
            CannotDeclareTypelessVariable0,
            CannotAssignTyplessValue0,
            TooFewTypesInMultiType0,
            ImplicitlyTypedFunctionIllegal0,
            RefTypeCannotBox1,
            ValueTypeCycle0,
            FunctionCycle0,
            XmlParsingError1,
            OperatorValueIllegal0,
            AttributesNotValidForItem0,
            MultipleMain0,
            OperationResultError1,
            AssignToItself0,
            ConditionAlwaysTrue1,
            ConditionAlwaysFalse1,
            SymbolDeprecated1,
            SymbolDeprecatedMessage2,
        }
        static string GetMessage(Text e, params string[] args)
        {
            switch (e)
            {
                case Text.Unknown0: return "Unknown error occured.";
                case Text.CharacterIllegal1: return $"The character '{args[0]}' does not match any token.";
                case Text.TokenIllegal1: return $"The token '{args[0]}' is not legal in this context.";
                case Text.StatementIllegal0: return "This statement is illegal in this context.";
                case Text.TokenExpected2: return $"Expected '{args[0]}', instead got '{args[1]}'.";
                case Text.IdentifierExpected1: return $"Expected identifier, instead got '{args[0]}'.";
                case Text.StringExpected1: return $"String expected, instead got '{args[0]}'.";
                case Text.ValueExpected1: return $"'{args[0]}' is not a value.";
                case Text.TypeExpected1: return $"'{args[0]}' is not a type.";
                case Text.NodeNotAValueOrTypeOrSymbol1: return $"'{args[0]}' is not a value, type, or symbol.";
                case Text.IdentifierNotDefined1: return $"'{args[0]}' is not defined.";
                case Text.MemberNotDefined2: return $"'{args[0]}' is not defined in '{args[1]}'.";
                case Text.IdentifierDefined1: return $"'{args[0]}' is already defined.";
                case Text.MemberDefined2: return $"'{args[0]}' is already defined in '{args[1]}'.";
                case Text.PropertyGetterDefined1: return $"Getter already defined for the property '{args[0]}'.";
                case Text.PropertySetterDefined1: return $"Setter already defined for the property '{args[0]}'.";
                case Text.TooManyParameters0: return "Too many parameters in function call.";
                case Text.MissingParameter1: return $"The required parameter '{args[0]}' is missing a value.";
                case Text.DuplicateParameter1: return $"The parameter '{args[0]}' is already specified.";
                case Text.PositionalParameterAfterNamed0: return "Positional parameters must appear before any named parameters.";
                case Text.IndexerIllegal0: return "Illegal indexer.";
                case Text.GenericCountIllegal1: return $"Wrong amount of generics, {args[0]} expected.";
                case Text.ParameterCountIllegal1: return $"Wrong amount of parameters, {args[0]} expected.";
                case Text.ValueNotInvokable1: return $"'{args[0]}' is not invokable.";
                case Text.TypeConvertionIllegal2: return $"Values of type '{args[0]}' cannot be converted to the type '{args[1]}'.";
                case Text.ImplicitConvertionIllegal2: return $"Values of type '{args[0]}' cannot be implicitly converted to the type '{args[1]}'. Try to convert explicitly.";
                case Text.ValueNotGettable1: return $"Cannot get the value of '{args[0]}' in this context. You might lack permission.";
                case Text.ValueNotSettable1: return $"Cannot set the value of '{args[0]}' in this context. You might lack permission, or the value might be constant.";
                case Text.PropertyTypeMismatch0: return $"The getter and setter of a property much be the of the same type.";
                case Text.CannotDeclareTypelessVariable0: return $"Variables must have an explicitly specified type.";
                case Text.CannotAssignTyplessValue0: return $"Cannot assign a typeless value to a variable.";
                case Text.TooFewTypesInMultiType0: return "Multitypes must contain at least 2 types.";
                case Text.ImplicitlyTypedFunctionIllegal0: return "Implicitly typed function illegal in this context.";
                case Text.RefTypeCannotBox1: return "Reference types cannot be boxed";
                case Text.ValueTypeCycle0: return "A variable of this type in this context causes a cycle.";
                case Text.FunctionCycle0: return "Function cannot return itself.";
                case Text.XmlParsingError1: return $"Xml parsing error '{args[0]}'.";
                case Text.OperatorValueIllegal0: return "This value is not compatible with the operator.";
                case Text.AttributesNotValidForItem0: return "Attributes are not valid for this item.";
                case Text.MultipleMain0: return "Entry point already defined.";
                case Text.OperationResultError1: return $"Operation result error: {args[0]}.";
                case Text.AssignToItself0: return "Assignment to itself.";
                case Text.ConditionAlwaysTrue1: return $"The condition '{args[0]}' always evaluates to true.";
                case Text.ConditionAlwaysFalse1: return $"The condition '{args[0]}' always evaluates to false.";
                case Text.SymbolDeprecated1: return $"'{args[0]}' is deprecated.";
                case Text.SymbolDeprecatedMessage2: return $"'{args[0]}' is deprecated '{args[1]}'.";
            }
            return "!!! Error message not defined !!!";
        }
        struct Place
        {
            public string File;
            public ushort Line;
            public ushort Index;
        }
        bool PlaceBefore(Place x, Place y)
        {
            return x.Line < y.Line || (x.Line == y.Line && x.Index < y.Index);
        }
        enum ThrowType
        {
            Fatal,
            Error,
            Warning,
            Message,
        }
        void Throw(Text e, ThrowType tt, Place? p, params string[] args)
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
                    Console.ForegroundColor = ConsoleColor.Yellow;
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

                string trimmedLine = p.Value.Line < Lines.Length ? Lines[p.Value.Line].TrimStart() : string.Empty;
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

            //breakpoint on the next line for debugging errors
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
            Colon,
            LeftAngleBracket,
            RightAngleBracket,
            Lambda,
            DoubleLambda,
            At,
            QuestionMark,

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
            TypeWord,
            ReturnWord,
            DefaultWord,
            NullWord,
            ObjectWord,
            BoolWord,
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
            ImportWord,
            UseWord,
            ConfigWord,
            OperatorWord,
            ConvertionWord,
            MainWord,
            FuncWord,
            PublicWord,
            PrivateWord,
            VisibleWord,
            ThisWord,
            NewWord,
            ForWord,
            YieldWord,
            TrueWord,
            FalseWord,
            NotWord,
            AndWord,
            OrWord,
            ThrowWord,
            CatchWord,

            //Other
            Global, //global::
            Attributes, //<attributes> stuff </attributes>
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
        bool SubstringEquals(StringBuilder x, int xindex, string y)
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
            return char.IsLetterOrDigit(c) || c == '_';
        }
        bool IsWordStartChar(char c)
        {
            return char.IsLetter(c);
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
        void Tokenize(string text, string filename, (string, TokenType)[] matches)
        {
            List<Token> tokens = new List<Token>();
            Lines = text.Split('\n');
            bool inComment = false;
            StringBuilder currentString = null;
            Place currentStringPlace = EmptyPlace;
            StringBuilder currentAttributes = null;
            Place currentAttributesPlace = EmptyPlace;
            for (int line = 0; line < Lines.Length; line++)
            {
                int index = 0;

                start:;
                int longestToken = 0;

                Place GetPlace()
                {
                    return new Place { Index = (ushort)(index), Line = (ushort)(line), File = filename };
                }

                if (inComment)
                {
                    while (true)
                    {
                        if (index + 2 >= Lines[line].Length)
                        {
                            goto nextline;
                        }
                        if (Lines[line][index] == '-' && Lines[line][index + 1] == '-' && Lines[line][index + 2] == '>')
                        {
                            inComment = false;
                            index += 3;
                            goto start;
                        }
                        index++;
                    }
                }
                if (currentString != null)
                {
                    while (true)
                    {
                        if (Lines[line][index] == '"')
                        {
                            tokens.Add(new Token { Match = currentString.ToString(), Place = currentStringPlace, TokenType = TokenType.String });
                            currentString = null;
                            index++;
                            goto start;
                        }
                        else
                        {
                            currentString.Append(Lines[line][index]);
                            index++;
                            if (index == Lines[line].Length) goto nextline;
                        }
                    }
                }
                if (currentAttributes != null)
                {
                    while (true)
                    {
                        if (SubstringEquals(Lines[line], index, attributesEnd))
                        {
                            tokens.Add(new Token { Match = currentAttributes.ToString(), Place = currentAttributesPlace, TokenType = TokenType.Attributes });
                            currentAttributes = null;
                            index += attributesEnd.Length;
                            goto start;
                        }
                        else
                        {
                            //carriage returns fuck up stuff, best to get rid of them while we can
                            if (Lines[line][index] != '\r')
                            {
                                currentAttributes.Append(Lines[line][index]);
                            }
                            index++;
                            if (index == Lines[line].Length) goto nextline;
                        }
                    }
                }
                if (index == Lines[line].Length) continue;
                if (char.IsWhiteSpace(Lines[line][index]))
                {
                    index++;
                    goto start;
                }
                if (Lines[line].Length + 1 != Lines[line].Length && Lines[line][index] == '/' && Lines[line][index + 1] == '/') continue;
                if (index + 3 < Lines[line].Length && Lines[line][index] == '<' && Lines[line][index + 1] == '!' && Lines[line][index + 2] == '-' && Lines[line][index + 3] == '-')
                {
                    inComment = true;
                    goto start;
                }
                if (Lines[line][index] == '"')
                {
                    currentStringPlace = GetPlace();
                    index++;
                    currentString = new StringBuilder();
                    goto start;
                }
                if (SubstringEquals(Lines[line], index, attributesStart))
                {
                    currentAttributesPlace = GetPlace();
                    index += attributesStart.Length;
                    currentAttributes = new StringBuilder();
                    goto start;
                }
                TokenType longestTokenType = TokenType.None;
                string longestMatch = null;
                for (int i = 0; i < matches.Length; i++)
                {
                    if (SubstringEquals(Lines[line], index, matches[i].Item1))
                    {
                        if (longestToken < matches[i].Item1.Length)
                        {
                            longestToken = matches[i].Item1.Length;
                            longestTokenType = matches[i].Item2;
                            longestMatch = matches[i].Item1;
                        }
                    }
                }
                if (IsWordStartChar(Lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (Lines[line].Length != index && IsWordChar(Lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.Word });
                        goto start;
                    }
                    index = starti;
                }
                else if (IsLowOperatorChar(Lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (Lines[line].Length != index && IsOperatorChar(Lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.LowOperator });
                        goto start;
                    }
                    index = starti;
                }
                else if (IsHighOperatorChar(Lines[line][index]))
                {
                    int starti = index;
                    index++;
                    while (Lines[line].Length != index && IsOperatorChar(Lines[line][index]))
                    {
                        index++;
                    }
                    if (index - starti > longestToken)
                    {
                        tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.HighOperator });
                        goto start;
                    }
                    index = starti;
                }
                else if (Lines[line][index] == '\'')
                {
                    index++;
                    int starti = index;
                    while (Lines[line].Length != index && Lines[line][index] != '\'')
                    {
                        if (!IsWordChar(Lines[line][index]))
                        {
                            Throw(Text.CharacterIllegal1, ThrowType.Error, GetPlace(), Lines[line][index].ToString());
                        }
                        index++;
                    }
                    index++;
                    tokens.Add(new Token { TokenType = TokenType.LowOperator, Match = Lines[line].Substring(starti, index - starti - 1), Place = new Place() { Index = (ushort)(starti), Line = (ushort)(line), File = filename } });
                    goto start;
                }

                if (longestToken == 0)
                {
                    if (IsDecChar(Lines[line][index]))
                    {
                        if (Lines[line][index] == '0' && index + 1 != Lines[line].Length)
                        {
                            if (Lines[line][index + 1] == 'x' || Lines[line][index + 1] == 'X')
                            {
                                index += 2;
                                int starti = index;
                                while (Lines[line].Length != index && IsHexChar(Lines[line][index]))
                                {
                                    if (Lines[line].Length == index) break;
                                    index++;
                                }
                                if (starti != index)
                                {
                                    tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.HexInteger });
                                    goto start;
                                }
                            }
                            else if (Lines[line][index + 1] == 'b' || Lines[line][index + 1] == 'B')
                            {
                                index += 2;
                                int starti = index;
                                while (Lines[line].Length != index && IsBinChar(Lines[line][index]))
                                {
                                    if (Lines[line].Length == index) break;
                                    index++;
                                }
                                if (starti != index)
                                {
                                    tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line }, TokenType = TokenType.HexInteger });
                                    goto start;
                                }
                            }
                        }
                        {
                            int starti = index;
                            index++;
                            while (Lines[line].Length != index && IsDecChar(Lines[line][index]))
                            {
                                if (Lines[line].Length == index) break;
                                index++;
                            }
                            if (index + 2 < Lines[line].Length && Lines[line][index] == '.' && IsDecChar(Lines[line][index + 1]))
                            {
                                index += 2;
                                while (Lines[line].Length != index && IsDecChar(Lines[line][index]))
                                {
                                    index++;
                                }
                                tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.DecNonInteger });
                                goto start;
                            }
                            else
                            {
                                tokens.Add(new Token { Match = Lines[line].Substring(starti, index - starti), Place = new Place { Index = (ushort)(starti), Line = (ushort)line, File = filename }, TokenType = TokenType.DecInteger });
                                goto start;
                            }
                        }
                    }
                    Throw(Text.CharacterIllegal1, ThrowType.Error, GetPlace(), Lines[line][index].ToString());
                    index++;
                }
                else
                {
                    tokens.Add(new Token { Place = GetPlace(), Match = longestMatch, TokenType = longestTokenType });
                    index += longestToken;
                }
                goto start;
                nextline:;
            }
            tokens.Add(new Token { Place = new Place { Line = (ushort)Lines.Length, Index = Lines.Length == 0 ? (ushort)0 : (ushort)Lines[Lines.Length - 1].Length, File = filename }, Match = "END OF FILE", TokenType = TokenType.EndOfFile });
            Tokens = tokens.ToArray();
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
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
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
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ",' or '" + endtokenmatch, Tokens[i].Match); break; }
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
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
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
            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',' expected", Tokens[i].Match);
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
                    else Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                    if (Tokens[i].TokenType == TokenType.Comma) i++;
                    else if (Tokens[i].TokenType == end) { i++; break; }
                    else { Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, endtokenmatch + "' or ',", Tokens[i].Match); break; }
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
            Leton,
            Enum,
            Initializer,
            Continue,
            Break,
            Return,
            Yield,
            Label,
            Alias,
            Main,
            If,
            Else,
            For,
            Import,
            Use,
            ConvertionDeclaration,
            Config,
            OperatorDeclaration,
            ConvertionModeDefinition,
            Throw,
            Catch,
            Attributes,
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
            Text currentError = Text.Unknown0;
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
                                    currentError = Text.TokenExpected2;
                                    currentErrorArgs = new[] { p.Misc[miscindex++], Tokens[i].Match };
                                    goto throww;
                                }
                                goto nextpattern;
                            }
                            else if (save) data.Add(Tokens[i].Match);
                            i++;
                            break;

                        //match, matches a token with the match from the matches array
                        case 'm':
                            if (Tokens[i].Match != p.Matches[matchindex++])
                            {
                                if (dothrow)
                                {
                                    currentError = Text.TokenExpected2;
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
                                    currentError = Text.IdentifierExpected1;
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.TokenExpected2;
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
                                        currentError = Text.StringExpected1;
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

                        //dual node list
                        case 'd':
                            {
                                var l = DualNodeList(ref i, p.TokenTypes[tokentypeindex++], p.Misc[miscindex++], false);
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
                else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, ";", Tokens[i].Match);
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
            TypedFunction, //List<(Node, Node)>                    //ex: func(int x, int y) = x + y
            UntypedFunction, //List<Node>                          //ex: func(x, y) = x + y
            FunctionType, //List<Node>                             //ex: func(int, int) => 
            Record,                                                //ex: [x = 1, y = 2]
            RecordType,                                            //ex: [int x, int y]
            Collection, //List<Node>                               //ex: {1, 2, 3}
            FunctionDeclaration, //Node, (Statement or Node)       //ex: FunctionName(int x, int y) = x - y
            MultiType, //List<Node>                                //ex: (x, y)
            Init, //(Node, List<Node)                              //ex: init MyClass
            Convertion, //(Node, Node)                             //ex: x:MyType
            Is, //(Node, Node)                                     //ex: x is y
            IsNot, //(Node, Node)                                  //ex: x is not y
            Not, //Node                                            //ex: not x
            And, //(Node, Node)                                    //ex: x and y
            Or, //(Node, Node)                                     //ex: x or y
            Xor, //(Node, Node)                                    //ex: x xor y
            Object,                                                //ex: object
            Bool,                                                  //ex: bool
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
            Generics,                     //ex: <int, string>
            ParameterList,                //ex: (int x, int y)
            BoxedSpecifier,               //ex: ?
        }
        class SecondaryNode
        {
            public SecondaryNodeType NodeType;
            public object Data;
            public SecondaryNode Child;
            public int Token;
        }

        SecondaryNode GetLastChild(SecondaryNode n)
        {
            if (n.Child == null) return n;
            return GetLastChild(n.Child);
        }
        void RemoveLastChild(SecondaryNode n)
        {
            if (n.Child.Child == null) n.Child = null;
            else RemoveLastChild(n.Child);
        }
        void RemoveLastChild(Node n)
        {
            if (n.Child.Child == null) n.Child = null;
            else RemoveLastChild(n.Child);
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
        Node NextNode(ref int i, bool ignoreEquals = false)
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
                        n1 = new Node { Data = (n1, Tokens[i++].Match, NextPrimaryNode(ref i)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //When operators have high priority, they take the previous PrimaryNode (n1) and the next Node
                    case TokenType.HighOperator:
                        n1 = new Node { Data = (n1, Tokens[i++].Match, NextNode(ref i, true)), NodeType = NodeType.BinaryOperator, Token = n1.Token, EndToken = i };
                        break;

                    //Assignment operators take the previous PrimaryNode and the next Node
                    case TokenType.Equals:
                        if (ignoreEquals) goto default;
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
                    Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
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
                        var nodes = NodeList(ref i, TokenType.PrimaryClose, ")", false);
                        if (nodes.Count == 0)
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[i - 1].Place, Tokens[i - 2].Match);
                        }
                        else if (nodes.Count == 1)
                        {
                            n.NodeType = NodeType.Parentheses;
                            n.Data = nodes[0];
                        }
                        else
                        {
                            n.NodeType = NodeType.MultiType;
                            n.Data = nodes;
                        }
                        break;
                    }

                //not
                case TokenType.NotWord:
                    n.NodeType = NodeType.Not;
                    i++;
                    n.Data = NextNode(ref i);
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
                case TokenType.BoolWord:
                    n.NodeType = NodeType.Bool;
                    i++;
                    break;

                //unary operator, doesnt matter if high or low
                case TokenType.LowOperator:
                case TokenType.HighOperator:
                case TokenType.LeftAngleBracket:
                case TokenType.RightAngleBracket:
                    n.NodeType = NodeType.UnaryOperator;
                    n.Data = (Tokens[i++].Match, NextPrimaryNode(ref i));
                    break;

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

                //init
                case TokenType.NewWord:
                    {
                        n.NodeType = NodeType.Init;
                        i++;
                        var node = NextPrimaryNode(ref i);
                        List<Node> nodes;
                        if (Tokens[i].TokenType == TokenType.TertiaryOpen)
                        {
                            i++;
                            nodes = NodeList(ref i, TokenType.TertiaryClose, "}", false);
                        }
                        else
                        {
                            nodes = null;
                        }
                        n.Data = (node, nodes);
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
                            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, "then", Tokens[i].Match);
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
                                Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, "when' or 'else", Tokens[i].Match);
                            }
                        }
                        n.Data = (conditions, elseValue);
                        break;
                    }

                //function type and value
                case TokenType.FuncWord:
                    {
                        i++;
                        var generics = GenericList(ref i);
                        if (Tokens[i].TokenType == TokenType.PrimaryOpen) i++;
                        else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, "(", Tokens[i].Match);
                        NodeListOrDualNodeList(ref i, out var single, out var dual, TokenType.PrimaryClose, ")", false);
                        if (dual == null)
                        {
                            if (Tokens[i].TokenType == TokenType.Lambda)
                            {
                                i++;
                                n.NodeType = NodeType.FunctionType;
                                n.Data = (generics, single, NextPrimaryNode(ref i));
                            }
                            else if (Tokens[i].TokenType == TokenType.Equals)
                            {
                                i++;
                                n.NodeType = NodeType.UntypedFunction;
                                n.Data = (generics, single, NextNode(ref i));
                            }
                            else
                            {
                                n.NodeType = NodeType.FunctionType;
                                n.Data = (generics, single, (Node)null);
                            }
                        }
                        else
                        {
                            n.NodeType = NodeType.TypedFunction;
                            if (Tokens[i].TokenType == TokenType.Equals) i++;
                            else Throw(Text.TokenExpected2, ThrowType.Error, Tokens[i].Place, "=", Tokens[i].Match);
                            n.Data = (dual, NextNode(ref i));
                        }
                        break;
                    }

                //global context specifier
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

            //convertion if there is
            if (Tokens[i].TokenType == TokenType.Colon)
            {
                i++;
                n = new Node { Data = (n, NextPrimaryNode(ref i)), NodeType = NodeType.Convertion, Token = n.Token, EndToken = i };
            }

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
                        if (Tokens[i].TokenType != TokenType.Word)
                        {
                            Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[i].Place, Tokens[i].Match);
                        }
                        i++;
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
                        var nodes = NodeList(ref i, TokenType.SecondaryClose, "]", false);
                        n.NodeType = SecondaryNodeType.Indexer;
                        n.Data = nodes;
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

        Symbol CurrentSymbol;

        List<LocalValue> Locals = new List<LocalValue>();
        Stack<int> LocalSeparators = new Stack<int>();

        void LocalsPush()
        {
            LocalSeparators.Push(Locals.Count);
        }
        void LocalsPop()
        {
            int i = LocalSeparators.Pop();
            Locals.RemoveRange(i, Locals.Count - i);
        }

        List<GenericType> GenericTypes = new List<GenericType>();
        Stack<int> GenericSeparators = new Stack<int>();

        //These functions "push" and "pop" (as in a stack) the generics
        void GenericsPush()
        {
            GenericSeparators.Push(GenericTypes.Count);
        }
        void GenericsPop()
        {
            int i = GenericSeparators.Pop();
            GenericTypes.RemoveRange(i, GenericTypes.Count - i);
        }

        //the generics which types are known
        List<(GenericType, _Type)> KnownGenericTypes = new List<(GenericType, _Type)>();
        Stack<int> KnownGenericSeparators = new Stack<int>();

        void KnownGenericsPush()
        {
            GenericSeparators.Push(GenericTypes.Count);
        }
        void KnownGenericsPop()
        {
            int i = KnownGenericSeparators.Pop();
            KnownGenericTypes.RemoveRange(i, KnownGenericTypes.Count - 1);
        }

        bool CanGet(Value v)
        {
            if (v is SymbolValue symbolVal) return CanGet(GetSymbol(symbolVal.Symbol));
            return true;
        }
        bool CanSet(Value v)
        {
            if (v is SymbolValue symbolVal) return CanSet(GetSymbol(symbolVal.Symbol));
            if (v is MemberValue) return true;
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
            if (CurrentSymbol != null)
            {
                Symbol x = CurrentSymbol;
                while (true)
                {
                    if (permissionNeeded == x) return true;
                    if (x.Parent == null) break;
                    x = GetSymbol(x.Parent);
                }
            }
            return false;
        }
        bool CanGet(PropertySymbol s)
        {
            if (s.Get == AccessorType.None) return false;
            if (s.Get == AccessorType.Public) return true;
            Symbol permissionNeeded = GetSymbol(s.Parent);
            if (!(permissionNeeded is ElementSymbol es)) permissionNeeded = GetSymbol(permissionNeeded.Parent); Symbol x = CurrentSymbol;
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
            Symbol x = CurrentSymbol;
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
            Symbol x = CurrentSymbol;
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
        //if a variable with a value type (not a reference type like a class or function)
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

        //pending stuff
        List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>, Symbol)> PendingSymbols = new List<(List<GenericType>, List<PendingVariable>, List<PendingProperty>, Symbol)>();
        Stack<List<PendingVariable>> CurrentPendingVariables = new Stack<List<PendingVariable>>();
        Stack<List<PendingProperty>> CurrentPendingProperties = new Stack<List<PendingProperty>>();
        List<PendingConvertion> PendingConvertions = new List<PendingConvertion>();
        List<(List<Statement>, Config)> PendingConfigs = new List<(List<Statement>, Config)>();

        class PendingVariable
        {
            public Node TypeNode, ValueNode;
            public SecondaryNode FunctionChildNode;
            public List<(_Type, string, Node)> FunctionParameters;
            public VariableSymbol Symbol;
        }
        class PendingProperty
        {
            public bool IsSetter;
            public Node TypeNode;
            public Node ValueNode;
            public PropertySymbol Symbol;
        }
        struct PendingConvertion
        {
            public Node To, From, Value;
            public List<string> Generics;
        }

        //Stage1 resolves the type
        void ProcessPendingVariableStage1(PendingVariable x)
        {
            if (x.FunctionChildNode == null)
            {
                if (x.TypeNode == null)
                {
                    Throw(Text.CannotDeclareTypelessVariable0, ThrowType.Error, Tokens[x.ValueNode.Token].Place);
                    x.Symbol.Type = ObjectType.Value;
                }
                else x.Symbol.Type = NodeToType(x.TypeNode);
            }
            else
            {
                ProcessFunctionParameters(x.FunctionChildNode, out var gl, out x.FunctionParameters, out var genericsPushed);
                var functype = new FunctionType { ReturnType = x.TypeNode == null ? null : NodeToType(x.TypeNode), Generics = gl };
                for (int i = 0; i < x.FunctionParameters.Count; i++)
                {
                    functype.Parameters.Add((x.FunctionParameters[i].Item1, x.FunctionParameters[i].Item2, x.FunctionParameters[i].Item3 != null));
                }
                x.Symbol.Type = functype;
                if (genericsPushed) GenericsPop();
            }
        }
        void ProcessPendingPropertyStage1(PendingProperty x)
        {
            var type = NodeToType(x.TypeNode);
            if (x.Symbol.Type == null) x.Symbol.Type = type;

            //throw if types dont match (getter and setter different types)
            else if (!x.Symbol.Type.Equals(type))
            {
                Throw(Text.PropertyTypeMismatch0, ThrowType.Error, Tokens[x.TypeNode.Token].Place);
            }
        }

        //Stage2 resolves the value
        void ProcessPendingVariableStage2(PendingVariable x)
        {
            if (x.FunctionChildNode == null)
            {
                if (x.ValueNode == null) x.Symbol.Value = new DefaultValue { Type = x.Symbol.Type };
                else x.Symbol.Value = NodeToValue(x.ValueNode, x.Symbol.Type);
            }
            else
            {
                x.Symbol.Value = ProcessFunctionValue((FunctionType)x.Symbol.Type, x.FunctionParameters, x.ValueNode, true, false);
            }
        }
        void ProcessPendingPropertyStage2(PendingProperty x)
        {
            throw new NotImplementedException();
        }

        void ProcessPendingConvertion(PendingConvertion x)
        {
            var fromType = NodeToType(x.From);
            var toType = NodeToType(x.To);
            var funcType = new FunctionType { Parameters = new List<(_Type, string, bool)>(1) { (fromType, null, false) }, ReturnType = toType, Generics = x.Generics ?? new List<string>(0) };
            Package.Convertions.Add((NodeToType(x.From), toType, NodeToValue(x.Value, funcType)));
        }

        #endregion
        #region node semantics

        void ThrowIfNotIdentifier(Node n)
        {
            if (n.NodeType != NodeType.Identifier)
            {
                Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[n.Token].Place, Tokens[n.Token].Match);
            }
            if (n.Child != null)
            {
                Throw(Text.TokenExpected2, ThrowType.Error, Tokens[n.Child.Token].Place, Tokens[n.Child.Token].Match);
            }
        }

        //matches an identifier to the corresponding Symbol, _Type, or Value
        object MatchIdentifier(string name)
        {
            //generics
            for (int i = 0; i < GenericTypes.Count; i++)
            {
                if (GenericTypes[i].Name == name) return GenericTypes[i];
            }

            //locals
            for (int i = 0; i < Locals.Count; i++)
            {
                if (Locals[i].Name == name)
                {
                    return Locals[i];
                }
            }

            //first CurrentSymbols children, then its parents children etc
            if (CurrentSymbol != null)
            {
                Symbol symbol = CurrentSymbol;
                while (true)
                {
                    for (int i = 0; i < symbol.Children.Count; i++)
                    {
                        if (symbol.Children[i].Name == name) return symbol.Children[i];
                    }
                    if (symbol.Parent == null) break;
                    symbol = GetSymbol(symbol.Parent);
                }
            }

            //symbols from this package
            for (int i = 0; i < Package.Symbols.Count; i++)
            {
                if (Package.Symbols[i].Name == name) return Package.Symbols[i];
            }

            //symbols from the project
            for (int i = 0; i < Project.Current.Symbols.Count; i++)
            {
                if (Project.Current.Symbols[i].Name == name) return Project.Current.Symbols[i];
            }

            //then the aliases of the configs that are in use
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
            if (type == null)
            {
                //check the automatic convertions from used configs
                for (int i = 0; i < UsedConfigs.Count; i++)
                {
                    for (int i2 = 0; i2 < UsedConfigs[i].ConvertionTypes.Count; i2++)
                    {
                        if (UsedConfigs[i].ConvertionTypes[i2].Item3 == ConvertionType.Automatic && UsedConfigs[i].ConvertionTypes[i2].Item1.Equals(value.Type))
                        {
                            for (int i3 = 0; i3 < Project.Current.Convertions.Count; i3++)
                            {
                                if (Project.Current.Convertions[i3].Item1.Equals(value.Type) && Project.Current.Convertions[i3].Item2.Equals(UsedConfigs[i].ConvertionTypes[i2].Item2))
                                    return new CallValue { Function = Project.Current.Convertions[i3].Item3, Parameters = new List<(string, Value)>(1) { (null, value) } };
                            }
                            for (int i3 = 0; i3 < Package.Convertions.Count; i3++)
                            {
                                if (Package.Convertions[i3].Item1.Equals(value.Type) && Package.Convertions[i3].Item2.Equals(UsedConfigs[i].ConvertionTypes[i2].Item2))
                                    return new CallValue { Function = Project.Current.Convertions[i3].Item3, Parameters = new List<(string, Value)>(1) { (null, value) } };
                            }
                        }
                    }
                }

                //if no automatic convertion, just return the value
                return value;
            }

            if (type is GenericType)
            {
                for (int i = 0; i < KnownGenericTypes.Count; i++)
                {
                    if (KnownGenericTypes[i].Item1.Equals(type)) type = KnownGenericTypes[i].Item2;
                }
            }

            //the value is of correct type
            if (value.Type.Equals(type)) return value;

            //if value is null
            if (value == NullValue.Value)
            {
                if (type.IsRefType) return value;
            }

            if (type == ObjectType.Value)
            {
                //values of reftypes do not need to be converted
                if (value.Type.IsRefType) return value;

                //if not a reftype, we box
                else return new BoxedValue { Base = value, Type = value.Type };
            }

            //implicit convertions
            for (int i = 0; i < UsedConfigs.Count; i++)
            {
                for (int i2 = 0; i2 < UsedConfigs[i].ConvertionTypes.Count; i2++)
                {
                    if (UsedConfigs[i].ConvertionTypes[i2].Item1.Equals(value.Type) && UsedConfigs[i].ConvertionTypes[i2].Item2.Equals(type))
                    {
                        for (int i3 = 0; i3 < Project.Current.Convertions.Count; i3++)
                        {
                            if (Project.Current.Convertions[i3].Item1.Equals(value.Type) && Project.Current.Convertions[i3].Item2.Equals(type))
                                return new CallValue { Function = Project.Current.Convertions[i3].Item3, Parameters = new List<(string, Value)>(1) { (null, value) } };
                        }
                        for (int i3 = 0; i3 < Package.Convertions.Count; i3++)
                        {
                            if (Package.Convertions[i3].Item1.Equals(value.Type) && Package.Convertions[i3].Item2.Equals(type))
                                return new CallValue { Function = Project.Current.Convertions[i3].Item3, Parameters = new List<(string, Value)>(1) { (null, value) } };
                        }
                    }
                }
            }

            {
                Value v = null;
                for (int i = 0; i < Project.Current.Convertions.Count; i++)
                {
                    if (Project.Current.Convertions[i].Item1.Equals(value.Type) && Project.Current.Convertions[i].Item2.Equals(type))
                        v = Project.Current.Convertions[i].Item3;
                }
                for (int i = 0; i < Package.Convertions.Count; i++)
                {
                    if (Package.Convertions[i].Item1.Equals(value.Type) && Package.Convertions[i].Item2.Equals(type))
                        v = Package.Convertions[i].Item3;
                }
                if (v != null)
                {
                    if (allowExplicitConvert)
                    {
                        return new CallValue { Function = v, Parameters = new List<(string, Value)>(1) { (null, value) } };
                    }
                    else if (dothrow)
                    {
                        Throw(Text.ImplicitConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
                        dothrow = false;
                    }
                }
            }

            if (dothrow)
            {
                Throw(Text.TypeConvertionIllegal2, ThrowType.Error, place, value.Type.ToString(), type.ToString());
            }

            //return default if not convertable
            return new DefaultValue { Type = type };
        }

        class IndexerSetTemporaryValue : Value
        {
            public CallValue Call;

            public override _Type Type { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
            public override void Read(XmlReader xml) => throw new NotImplementedException();
            public override void Write(XmlWriter xml) => throw new NotImplementedException();
        }

        //This function returns the Symbol, Value, or _Type that the Node represents
        //typeContext specifies to what type values should be converted into
        object MatchNode(Node node, bool prioritizeSymbol, _Type typeContext = null, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false, bool setIndexer = false, bool allowGenericlessTypes = false)
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

            //how many times known generics pushed
            bool knownGenericsPushed = false;

            void addKnownGenerics(_Type t)
            {
                if (t is NormalType normalType)
                {
                    if (normalType.Generics.Count != 0)
                    {
                        if (knownGenericsPushed)
                        {
                            KnownGenericsPop();
                        }
                        else
                        {
                            knownGenericsPushed = true;
                        }
                        KnownGenericsPush();
                        for (int i = 0; i < normalType.Generics.Count; i++)
                        {
                            KnownGenericTypes.Add((new GenericType { Name = normalType.Generics[i].Item1 }, normalType.Generics[i].Item2));
                        }
                    }
                }
            }

            Value MakeValue(Value v)
            {
                var val = ConvertValue(v, childNode == null ? typeContext : null, Tokens[node.Token].Place, allowExplicitConvert: allowExplicitConvert);
                addKnownGenerics(val.Type);
                return val;
            }

            //get the initial symbol/type/value
            switch (node.NodeType)
            {
                default:
                    Throw(Text.NodeNotAValueOrTypeOrSymbol1, ThrowType.Error, Tokens[node.Token].Place, NodeToString(node));
                    return null;

                case NodeType.Parentheses: return MakeValue(NodeToValue((Node)node.Data, null, checkCanGet, createLocals, ignoreChildren, strictProcedureType, false, setIndexer));

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
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, name);
                            return null;

                            symbolfound:
                            while (childNode != null && childNode.NodeType == SecondaryNodeType.Member)
                            {
                                name = Tokens[childNode.Token + 1].Match;
                                var s = symbol.Children.Find(x => x.Name == name);
                                if (symbol == null)
                                {
                                    Throw(Text.MemberDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, symbol.ToString());
                                    return null;
                                }
                                symbol = s;
                            }
                        }
                        else
                        {
                            Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
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
                            if (o is _Type tt) type = tt;
                            else if (o is Symbol ss)
                            {
                                symbol = ss;
                                while (childNode != null && childNode.NodeType == SecondaryNodeType.Member && symbol is ElementSymbol)
                                {
                                    var s = symbol.Children.Find(x => x.Name == Tokens[childNode.Token + 1].Match);
                                    if (s == null)
                                    {
                                        Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, Tokens[childNode.Token + 1].Match, symbol.ToString());
                                        return null;
                                    }
                                    symbol = s;
                                    childNode = childNode.Child;
                                }
                            }
                            else if (createLocals)
                            {
                                var local = new LocalValue { Name = Tokens[node.Token].Match };
                                Locals.Add(local);
                                value = local;
                            }
                            else
                            {
                                Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, Tokens[node.Token].Match);
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
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.String, Value = t.Match });
                                break;
                            case TokenType.DecInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = BigInteger.Parse(t.Match).ToString("x") });
                                break;
                            case TokenType.DecNonInteger:
                                {
                                    int sep = t.Match.IndexOf('.');
                                    BigInteger numerator = BigInteger.Pow(10, t.Match.Length - sep - 1);
                                    BigInteger denominator = BigInteger.Parse(t.Match.Remove(sep) + t.Match.Substring(sep + 1));
                                    BigInteger divisor = BigInteger.GreatestCommonDivisor(numerator, denominator);
                                    numerator = numerator / divisor;
                                    denominator = denominator / divisor;
                                    value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Fractional, Value = denominator.ToString("x") + "/" + numerator.ToString("x") });
                                    break;
                                }
                            case TokenType.HexInteger:
                                value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = t.Match.Substring(2) });
                                break;
                            case TokenType.BinInteger:
                                {
                                    //make sure string can be divided into chunks of 8
                                    var sb = new StringBuilder(t.Match);
                                    sb.Remove(0, 2);
                                    var rem = sb.Length % 8;
                                    if (rem != 0) sb.Append('0', rem);
                                    var result = new StringBuilder(sb.Length / 4);
                                    for (int i = 0; i < sb.Length; i += 8)
                                    {
                                        result.Append(Convert.ToByte(sb.ToString(i, 8), 2).ToString("x"));
                                    }
                                    value = MakeValue(new LiteralValue { LiteralType = LiteralValueType.Integer, Value = result.ToString() });
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
                case NodeType.Bool:
                    type = BooleanType.Value;
                    break;
                case NodeType.Procedural:
                    {
                        if (strictProcedureType) value = ScopeToProcedure((Statement)node.Data, typeContext);
                        else value = ScopeToProcedure((Statement)node.Data);
                        break;
                    }
                case NodeType.Convertion:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var converttype = NodeToType(nodes.Item2);
                        value = MakeValue(NodeToValue(nodes.Item1, NodeToType(nodes.Item2), allowExplicitConvert: true));
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
                                string name = Tokens[nodes.Item1.Token].Match;
                                ThrowIfNotIdentifier(nodes.Item1);
                                if (!recordValue.Fields.TrueForAll(x => x.Item1 != name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                }
                                recordValue.Fields.Add((name, NodeToValue(nodes.Item2, null)));
                            }
                        }
                        value = recordValue;
                        break;
                    }
                case NodeType.RecordType:
                    {
                        var nodeList = (List<(Node, Node)>)node.Data;
                        var recordType = new RecordType();
                        for (int i = 0; i < nodeList.Count; i++)
                        {
                            var name = Tokens[nodeList[i].Item2.Token].Match;
                            ThrowIfNotIdentifier(nodeList[i].Item2);
                            if (!recordType.Fields.Add((name, NodeToType(nodeList[i].Item1))))
                            {
                                Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[nodeList[i].Item2.Token].Place, name);
                            }
                        }
                        type = recordType;
                        break;
                    }
                case NodeType.Init:
                    {
                        var data = ((Node, List<Node>))node.Data;
                        var newType = NodeToType(data.Item1);
                        if (data.Item2 != null && data.Item2.Count != 0)
                        {
                            if (newType is NormalType normalType)
                            {
                                var newValue = new NewValue { Type = newType };
                                var newSymbol = GetSymbol(normalType.Base);
                                newValue.FieldValues.Capacity = data.Item2.Count;
                                for (int i = 0; i < data.Item2.Count; i++)
                                {
                                    if (data.Item2[i].NodeType == NodeType.Assignment)
                                    {
                                        var nodes = ((Node, Node))data.Item2[i].Data;
                                        string name = Tokens[nodes.Item1.Token].Match;
                                        ThrowIfNotIdentifier(nodes.Item1);
                                        var symbolIndex = newSymbol.Children.FindIndex(x => x.Name == name);
                                        if (symbolIndex == -1)
                                        {
                                            Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, normalType.Base);
                                        }
                                        else
                                        {
                                            var fieldSymbol = newSymbol.Children[symbolIndex];
                                            if (!CanSet(fieldSymbol))
                                            {
                                                Throw(Text.ValueNotSettable1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
                                            }
                                            newValue.FieldValues.Add((fieldSymbol.ToString(), NodeToValue(nodes.Item2, fieldSymbol is VariableSymbol vs ? vs.Type : ((PropertySymbol)fieldSymbol).Type)));
                                        }
                                    }
                                    else
                                    {
                                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item2[i].Token].Place, Tokens[data.Item2[i].Token].Match);
                                    }
                                }
                                value = newValue;
                            }
                            else if (newType is CollectionType collectionType)
                            {
                                var collectionValue = new CollectionValue { Type = collectionType };
                                collectionValue.Values.Capacity = data.Item2.Count;
                                for (int i = 0; i < data.Item2.Count; i++)
                                {
                                    collectionValue.Values.Add(NodeToValue(data.Item2[i], collectionType.Base));
                                }
                                value = collectionValue;
                            }
                            else
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item1.Token].Place, Tokens[data.Item1.Token].Match);
                                value = typeContext == null ? (Value)NullValue.Value : new DefaultValue { Type = typeContext };
                            }
                        }
                        else
                        {
                            if (newType is NormalType normalType)
                            {
                                value = new NewValue { Type = newType };
                            }
                            else if (newType is CollectionType collectionType)
                            {
                                value = new CollectionValue { Type = collectionType };
                            }
                            else
                            {
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[data.Item1.Token].Place, Tokens[data.Item1.Token].Match);
                                value = typeContext == null ? (Value)NullValue.Value : new DefaultValue { Type = typeContext };
                            }
                        }
                        break;
                    }
                case NodeType.Collection:
                    {
                        var nodeList = (List<Node>)node.Data;
                        var collection = new CollectionValue();
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
                            Throw(Text.TooFewTypesInMultiType0, ThrowType.Error, Tokens[node.Token].Place);
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
                case NodeType.FunctionType:
                    {
                        var data = ((List<Node>, List<Node>, Node))node.Data;
                        var funcType = new FunctionType();
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcType.Generics.Add(name);
                            }
                        }
                        for (int i = 0; i < data.Item2.Count; i++)
                        {
                            funcType.Parameters.Add((NodeToType(data.Item2[i]), null, false));
                        }
                        if (data.Item3 != null) funcType.ReturnType = NodeToType(data.Item3);
                        if (data.Item1 != null) GenericsPop();
                        type = funcType;
                        break;
                    }
                case NodeType.TypedFunction:
                    {
                        var data = ((List<Node>, List<(Node, Node)>, Node))node.Data;
                        var funcType = new FunctionType();
                        var funcValue = new FunctionValue { Type = funcType };
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcType.Generics.Add(name);
                                funcValue.Generics.Add(name);
                            }
                        }
                        for (int i = 0; i < data.Item2.Count; i++)
                        {
                            var nodes = ((Node, Node))data.Item2[i];
                            string name = Tokens[nodes.Item2.Token].Match;
                            if (!LocalNameValid(name))
                            {
                                Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                            }
                            funcType.Parameters.Add((NodeToType(nodes.Item1), null, false));
                            funcValue.Parameters.Add((name, null));
                        }
                        funcValue.Value = NodeToValue(data.Item3, null);
                        funcType.ReturnType = funcValue.Value.Type;
                        if (data.Item1 != null) GenericsPop();
                        value = funcValue;
                        break;
                    }
                case NodeType.UntypedFunction:
                    {
                        var data = ((List<Node>, List<Node>, Node))node.Data;
                        FunctionType funcType;
                        FunctionValue funcValue;
                        if (typeContext == null)
                        {
                            if (data.Item2.Count == 0)
                            {
                                funcType = new FunctionType();
                                funcValue = new FunctionValue { Type = funcType };
                                if (data.Item1 != null)
                                {
                                    GenericsPush();
                                    for (int i = 0; i < data.Item1.Count; i++)
                                    {
                                        string name = Tokens[data.Item1[i].Token].Match;
                                        ThrowIfNotIdentifier(data.Item1[i]);
                                        if (!LocalNameValid(name))
                                        {
                                            Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                        }
                                        GenericTypes.Add(new GenericType { Name = name });
                                        funcValue.Generics.Add(name);
                                        //we dont need to add generics to funcType because there are no parameters
                                    }
                                }
                                funcValue.Value = NodeToValue(data.Item3, null);
                                funcType.ReturnType = funcValue.Value.Type;
                                value = funcValue;
                                break;
                            }
                            funcType = new FunctionType();
                            Throw(Text.ImplicitlyTypedFunctionIllegal0, ThrowType.Error, Tokens[node.Token].Place);
                        }
                        else if (typeContext is FunctionType)
                        {
                            funcType = (FunctionType)typeContext;
                        }
                        else
                        {
                            funcType = new FunctionType();
                            Throw(Text.ImplicitlyTypedFunctionIllegal0, ThrowType.Error, Tokens[node.Token].Place);
                        }
                        funcValue = new FunctionValue { Type = funcType };
                        if (data.Item1 != null)
                        {
                            GenericsPush();
                            if (data.Item1.Count != funcType.Generics.Count)
                            {
                                Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[node.Token + 1].Place, funcType.Generics.Count.ToString());
                            }
                            for (int i = 0; i < data.Item1.Count; i++)
                            {
                                string name = Tokens[data.Item1[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item1[i]);
                                if (!LocalNameValid(name))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[data.Item1[i].Token].Place, name);
                                }
                                GenericTypes.Add(new GenericType { Name = name });
                                funcValue.Generics.Add(name);
                            }
                        }
                        if (data.Item2.Count != funcType.Parameters.Count)
                        {
                            Throw(Text.ParameterCountIllegal1, ThrowType.Error, Tokens[node.Token].Place, funcType.Parameters.Count.ToString());
                        }
                        int limit = data.Item2.Count < funcType.Parameters.Count ? data.Item2.Count : funcType.Parameters.Count;
                        if (limit != 0)
                        {
                            LocalsPush();
                            for (int i = 0; i < limit; i++)
                            {
                                string name = Tokens[data.Item2[i].Token].Match;
                                ThrowIfNotIdentifier(data.Item2[i]);
                                funcValue.Parameters.Add((name, null));
                                Locals.Add(new LocalValue { Name = name, Type = funcType.Parameters[i].Item1 });
                            }
                        }
                        funcValue.Value = NodeToValue(data.Item3, funcType.ReturnType, strictProcedureType: true);
                        if (limit != 0) LocalsPop();
                        value = funcValue;
                        break;
                    }
                case NodeType.Not:
                    {
                        var cond = NodeToValue((Node)node.Data, BooleanType.Value);
                        value = new OperationValue { OperationType = OperationType.Not, Values = new List<Value>(1) { cond } };
                        break;
                    }
                case NodeType.And:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var cond1 = NodeToValue(nodes.Item1, BooleanType.Value);
                        var cond2 = NodeToValue(nodes.Item2, BooleanType.Value);
                        value = new OperationValue { OperationType = OperationType.And, Values = new List<Value>(2) { cond1, cond2 } };
                        break;
                    }
                case NodeType.Or:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var cond1 = NodeToValue(nodes.Item1, BooleanType.Value);
                        var cond2 = NodeToValue(nodes.Item2, BooleanType.Value);
                        value = new OperationValue { OperationType = OperationType.Or, Values = new List<Value>(2) { cond1, cond2 } };
                        break;
                    }
                case NodeType.Is:
                    {
                        var nodes = ((Node, Node))node.Data;
                        var _type = NodeToType(nodes.Item2);
                        var _value = NodeToValue(nodes.Item1, type);
                        value = new OperationValue { OperationType = OperationType.Is, Types = new List<_Type>(1) { _type }, Values = new List<Value>(1) { _value } };
                        break;
                    }
                case NodeType.UnaryOperator:
                    {
                        var data = ((string, Node))node.Data;
                        var operand = NodeToValue(data.Item2, null);
                        for (int i = 0; i < UsedConfigs.Count; i++)
                        {
                            for (int i2 = 0; i2 < UsedConfigs[i].UnaryOperators.Count; i2++)
                            {
                                if (UsedConfigs[i].UnaryOperators[i2].Item1 == data.Item1 && UsedConfigs[i].UnaryOperators[i2].Item2.Equals(operand.Type))
                                {
                                    value = MakeValue(new CallValue { Function = UsedConfigs[i].UnaryOperators[i2].Item3, Parameters = new List<(string, Value)>(1) { (null, operand) } });
                                    break;
                                }
                            }
                        }
                        if (value == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, data.Item1 + " " + operand.Type.ToString());
                        }
                        break;
                    }
                case NodeType.BinaryOperator:
                    {
                        var data = ((Node, string, Node))node.Data;
                        var operand1 = NodeToValue(data.Item1, null);
                        var operand2 = NodeToValue(data.Item3, null);
                        for (int i = 0; i < UsedConfigs.Count; i++)
                        {
                            for (int i2 = 0; i2 < UsedConfigs[i].BinaryOperators.Count; i2++)
                            {
                                if (UsedConfigs[i].BinaryOperators[i2].Item2 == data.Item2 && UsedConfigs[i].BinaryOperators[i2].Item1.Equals(operand1.Type) && UsedConfigs[i].BinaryOperators[i2].Item3.Equals(operand2.Type))
                                {
                                    value = MakeValue(new CallValue { Function = UsedConfigs[i].BinaryOperators[i2].Item4, Parameters = new List<(string, Value)>(1) { (null, operand1), (null, operand2) } });
                                    break;
                                }
                            }
                        }
                        if (value == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[node.Token].Place, operand1.Type.ToString() + " " + data.Item2 + " " + operand2.Type.ToString());
                        }
                        break;
                    }

                //conditionals are implemented as procedural values with a bunch of if statements
                case NodeType.Conditional:
                    {
                        var scope = new ScopeInstruction();
                        var data = ((List<(Node, Node)>, Node))node.Data;
                        _Type condType = null;
                        for (int i = 0; i < data.Item1.Count; i++)
                        {
                            var val = NodeToValue(data.Item1[i].Item2, condType);
                            if (condType == null) condType = val.Type;
                            scope.Instructions.Add(new IfInstruction { Condition = NodeToValue(data.Item1[i].Item1, BooleanType.Value), Instruction = new ReturnInstruction { Value = val }, File = Tokens[data.Item1[i].Item1.Token].Place.File, Line = Tokens[data.Item1[i].Item1.Token].Place.Line, Index = Tokens[data.Item1[i].Item1.Token].Place.Index });
                        }
                        scope.Instructions.Add(new ReturnInstruction { Value = NodeToValue(data.Item2, condType), File = Tokens[data.Item2.Token].Place.File, Line = Tokens[data.Item2.Token].Place.Line, Index = Tokens[data.Item2.Token].Place.Index });
                        value = new ProceduralValue { Instruction = scope };
                        break;
                    }
            }

            //convert symbol to value or type
            //ClassSymbols and StructSymbols can be converted to types
            //VariableSymbols and PropertySymbols can be converted to values
            if(symbol != null)
            {
                //throw a warning if symbol is deprecated
                if(symbol.Attributes.TryGetValue("deprecated", out string deprmessage))
                {
                    if (deprmessage == null) Throw(Text.SymbolDeprecated1, ThrowType.Warning, Tokens[node.Token].Place, symbol.ToString());
                    else Throw(Text.SymbolDeprecatedMessage2, ThrowType.Warning, Tokens[node.Token].Place, symbol.ToString(), deprmessage);
                }

                if (prioritizeSymbol && childNode == null) return symbol;

                if (symbol is ElementSymbol es && es.Alternate != null) symbol = GetSymbol(es.Alternate);

                if (symbol is ClassSymbol classSymbol)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), RefType = true };

                    //generics
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.Generics)
                    {
                        List<Node> nl = (List<Node>)childNode.Data;
                        GenericsPush();
                        for (int i = 0; i < nl.Count; i++)
                        {
                            var t = NodeToType(nl[i]);
                            if (i < classSymbol.Generics.Count)
                            {
                                normalType.Generics.Add((classSymbol.Generics[i], t));
                            }
                        }
                        if (nl.Count != classSymbol.Generics.Count) Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, classSymbol.Generics.Count.ToString());
                        childNode = childNode.Child;
                    }
                    else if (classSymbol.Generics.Count != 0) return symbol;

                    type = normalType;
                }
                else if (symbol is StructSymbol structSymbol)
                {
                    var normalType = new NormalType { Base = symbol.ToString(), RefType = true };

                    //generics
                    if (childNode != null && childNode.NodeType == SecondaryNodeType.Generics)
                    {
                        List<Node> nl = (List<Node>)childNode.Data;
                        GenericsPush();
                        for (int i = 0; i < nl.Count; i++)
                        {
                            var t = NodeToType(nl[i]);
                            if (i < structSymbol.Generics.Count)
                            {
                                normalType.Generics.Add((structSymbol.Generics[i], t));
                            }
                        }
                        if (nl.Count != structSymbol.Generics.Count) Throw(Text.GenericCountIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, structSymbol.Generics.Count.ToString());
                        childNode = childNode.Child;
                    }

                    type = normalType;
                }
                else if (symbol is VariableSymbol vs)
                {
                    if (checkCanGet && !CanGet(vs))
                    {
                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Symbol = vs.ToString(), Type = vs.Type });
                }
                else if (symbol is PropertySymbol ps)
                {
                    if (!CanGet(ps))
                    {
                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[node.Token].Place, symbol.ToString());
                    }
                    value = MakeValue(new SymbolValue { Symbol = ps.ToString(), Type = ps.Type });
                }
            }

            if (value == null && type == null) return symbol;
            else symbol = null;

            //additions
            if (!ignoreChildren)
            {
                while (childNode != null)
                {
                    if (value != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.Call || childNode.NodeType == SecondaryNodeType.Indexer)
                        {
                            FunctionType funcType = null;
                            if (childNode.NodeType == SecondaryNodeType.Call)
                            {
                                if (value.Type is FunctionType ft)
                                {
                                    funcType = ft;
                                }
                                else
                                {
                                    Throw(Text.ValueNotInvokable1, ThrowType.Error, Tokens[childNode.Token].Place, value.ToString());
                                    funcType = new FunctionType();
                                }
                            }
                            else if (childNode.NodeType == SecondaryNodeType.Indexer)
                            {
                                if (value.Type is NormalType nt)
                                {
                                    var typeSymbol = GetSymbol(nt.Base);
                                    string nameToFind = setIndexer && childNode.Child == null ? IndexerSetterName : IndexerGetterName;
                                    int childIndex = typeSymbol.Children.FindIndex(x => x.Name == nameToFind);
                                    if (childIndex != -1)
                                    {
                                        var indexerSymbol = typeSymbol.Children[childIndex];
                                        if (indexerSymbol is VariableSymbol vs && vs.Type is FunctionType ft && ft.Parameters.Count != 0)
                                        {
                                            value = new SymbolValue { Symbol = vs.ToString(), Instance = value, Type = vs.Type };
                                            if (setIndexer && childNode.Child == null)
                                            {
                                                var newParams = new List<(_Type, string, bool)>(ft.Parameters.Count - 1);
                                                for (int i = 0; i < newParams.Capacity; i++)
                                                {
                                                    newParams.Add(ft.Parameters[i]);
                                                }
                                                funcType = new FunctionType { Generics = ft.Generics, Parameters = newParams, ReturnType = ft.ReturnType };
                                            }
                                            else funcType = ft;
                                            goto nothrow;
                                        }
                                    }
                                }
                                Throw(Text.IndexerIllegal0, ThrowType.Error, Tokens[childNode.Token].Place);
                                funcType = new FunctionType();
                                nothrow:;
                            }
                            var callValue = new CallValue { Function = value };
                            List<Node> paramNodes = (List<Node>)childNode.Data;
                            int paramIndex = 0;
                            bool positionalParams = true;
                            while (true)
                            {
                                if (paramIndex == paramNodes.Count) break;
                                if (paramIndex == funcType.Parameters.Count && positionalParams)
                                {
                                    Throw(Text.TooManyParameters0, ThrowType.Error, Tokens[paramNodes[paramIndex].Token].Place);
                                    break;
                                }
                                if (paramNodes[paramIndex] == null) continue;
                                if (paramNodes[paramIndex].NodeType == NodeType.Assignment)
                                {
                                    positionalParams = false;
                                    var nodes = ((Node, Node))paramNodes[paramIndex].Data;
                                    string name = Tokens[nodes.Item1.Token].Match;
                                    ThrowIfNotIdentifier(nodes.Item1);
                                    int actualParamIndex = funcType.Parameters.FindIndex(x => x.Item2 == name);
                                    if (actualParamIndex == -1)
                                    {
                                        Throw(Text.IdentifierNotDefined1, ThrowType.Error, Tokens[paramNodes[paramIndex].Token].Place, name);
                                    }
                                    else if (!callValue.Parameters.TrueForAll(x => x.Item1 != name))
                                    {
                                        Throw(Text.DuplicateParameter1, ThrowType.Error, Tokens[paramNodes[paramIndex].Token].Place, name);
                                    }
                                    callValue.Parameters.Add((name, NodeToValue(nodes.Item2, actualParamIndex == -1 ? null : funcType.Parameters[actualParamIndex].Item1)));
                                }
                                else
                                {
                                    if (positionalParams)
                                    {
                                        callValue.Parameters.Add((funcType.Parameters[paramIndex].Item2, NodeToValue(paramNodes[paramIndex], funcType.Parameters[paramIndex].Item1)));
                                    }
                                    else
                                    {
                                        Throw(Text.PositionalParameterAfterNamed0, ThrowType.Error, Tokens[paramNodes[paramIndex].Token].Place);
                                        NodeToValue(paramNodes[paramIndex], null);
                                    }
                                }
                                paramIndex++;
                            }
                            //check that all the required parameters have input
                            for (int i = 0; i < funcType.Parameters.Count; i++)
                            {
                                if (callValue.Parameters.TrueForAll(x => x.Item1 != funcType.Parameters[i].Item2))
                                {
                                    if (!funcType.Parameters[i].Item3)
                                    {
                                        Throw(Text.MissingParameter1, ThrowType.Error, Tokens[childNode.Token].Place, funcType.Parameters[i].Item2 ?? "Unnamed parameter at position: " + (i + 1).ToString());
                                    }
                                }
                            }
                            childNode = childNode.Child;
                            if (setIndexer && childNode == null)
                            {
                                return new IndexerSetTemporaryValue { Call = callValue };
                            }
                            else
                            {
                                value = MakeValue(callValue);
                            }
                        }
                        else if (childNode.NodeType == SecondaryNodeType.Member)
                        {
                            string name = Tokens[childNode.Token + 1].Match;
                            if (value.Type is NormalType nt)
                            {
                                var baseSymbol = GetSymbol(nt.Base);
                                var memberIndex = baseSymbol.Children.FindIndex(x => x.Name == name);
                                if (memberIndex == -1)
                                {
                                    Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, nt.Base);
                                    childNode = childNode.Child;
                                }
                                else
                                {
                                    childNode = childNode.Child;
                                    var memberSymbol = baseSymbol.Children[memberIndex];
                                    if (!CanGet(memberSymbol))
                                    {
                                        Throw(Text.ValueNotGettable1, ThrowType.Error, Tokens[childNode.Token + 1].Place, memberSymbol.ToString());
                                    }
                                    value = MakeValue(new SymbolValue { Symbol = memberSymbol.ToString(), Instance = value, Type = memberSymbol is VariableSymbol vs ? vs.Type : ((PropertySymbol)memberSymbol).Type });
                                }
                            }
                            else if (value.Type is RecordType rt)
                            {
                                var field = rt.Fields.First(x => x.Item1 == name);
                                if (field.Item1 == null)
                                {
                                    Throw(Text.MemberNotDefined2, ThrowType.Error, Tokens[childNode.Token + 1].Place, name, rt.ToString());
                                    childNode = childNode.Child;
                                }
                                else
                                {
                                    childNode = childNode.Child;
                                    value = MakeValue(new MemberValue { Base = value, Name = name, Type = field.Item2 });
                                }
                            }
                            else
                            {
                                childNode = childNode.Child;
                                Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            }
                        }
                        else if (childNode.NodeType == SecondaryNodeType.Generics && value is FunctionValue funcValue)
                        {
                            //function generics are handled by returning a new function with the set types
                            var genericNodes = (List<Node>)childNode.Data;
                            var generics = new List<_Type>(genericNodes.Count);
                            var newFunc = new FunctionValue();

                            childNode = childNode.Child;

                            _Type resolveType(_Type t)
                            {
                                if (t is GenericType genericType)
                                {
                                    for (int i = 0; i < funcValue.Generics.Count; i++)
                                    {
                                        if (funcValue.Generics[i] == genericType.Name)
                                        {
                                            if (i < generics.Count)
                                            {
                                                return generics[i];
                                            }
                                            else
                                            {
                                                return ObjectType.Value;
                                            }
                                        }
                                    }
                                }
                                return t;
                            }
                        }
                    }
                    else if (type != null)
                    {
                        if (childNode.NodeType == SecondaryNodeType.CollectionTypeSpecifier)
                        {
                            type = new CollectionType { Base = type };
                        }
                        else if (childNode.NodeType == SecondaryNodeType.BoxedSpecifier)
                        {
                            if (type.IsRefType)
                            {
                                Throw(Text.RefTypeCannotBox1, ThrowType.Error, Tokens[childNode.Token].Place, type.ToString());
                            }
                            type = new BoxedType { Base = type };
                        }
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[childNode.Token].Place, Tokens[childNode.Token].Match);
                            break;
                        }
                        childNode = childNode.Child;
                    }
                }
            }

            if (knownGenericsPushed) KnownGenericsPop();
            return value ?? (object)type;
        }
        Value NodeToValue(Node n, _Type typeContext, bool checkCanGet = true, bool createLocals = false, bool ignoreChildren = false, bool strictProcedureType = false, bool allowExplicitConvert = false, bool setIndexer = false)
        {
            object o = MatchNode(n, false, typeContext, checkCanGet, createLocals, ignoreChildren, strictProcedureType, allowExplicitConvert, setIndexer);
            if (o is Value v) return v;
            if (o != null) Throw(Text.ValueExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return typeContext == null ? (Value)NullValue.Value : new DefaultValue { Type = typeContext };
        }
        _Type NodeToType(Node n)
        {
            object o = MatchNode(n, false);
            if (o is _Type t) return t;
            if (o != null) Throw(Text.TypeExpected1, ThrowType.Error, Tokens[n.Token].Place, NodeToString(n));
            return ObjectType.Value;
        }

        #endregion
        #region analyzing

        void ThrowAttributesNotValid(Statement st)
        {
            Throw(Text.AttributesNotValidForItem0, ThrowType.Error, Tokens[st.Token].Place);
        }

        Dictionary<string, string> ParseAttributes(Token t)
        {
            var dict = new Dictionary<string, string>();
            using (var stringReader = new System.IO.StringReader(t.Match))
            using (var xml = XmlReader.Create(stringReader, new XmlReaderSettings { ConformanceLevel = ConformanceLevel.Fragment }))
            {
                try
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            if (!dict.ContainsKey(xml.LocalName))
                            {
                                dict.Add(xml.LocalName, xml.IsEmptyElement ? null : xml.ReadElementContentAsString());
                            }
                            else xml.ReadElementContentAsString();
                        }
                    }
                }
                catch (Exception e)
                {
                    Throw(Text.XmlParsingError1, ThrowType.Error, t.Place, e.Message);
                }
            }
            return dict;
        }

        //structural analyzing
        //source file structure like what statements in global context, what inside elements, what inside classes etc.
        //Symbol tree construction to Package.Symbols
        void AnalyzeGlobalStatement(Statement statement, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                default:
                    if (attributes != null) ThrowAttributesNotValid(statement);
                    AnalyzeConfigStatement(statement, LocalConfig);
                    break;;

                case StatementType.Main:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        if (Main != null)
                        {
                            Throw(Text.MultipleMain0, ThrowType.Error, Tokens[statement.Token].Place);
                        }
                        Main = (Node)statement.Data[0];
                        break;
                    }

                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1, Attributes = attributes ?? EmptyAttributes };
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        Package.Symbols.Add(symbol);
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        break;
                    }
                case StatementType.Class:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ClassSymbol symbol = new ClassSymbol { Name = name.Item1, Attributes = attributes ?? EmptyAttributes };

                        if (name.Item1 == string.Empty)
                        {
                            Throw(Text.IdentifierExpected1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        else
                        {
                            Package.Symbols.Add(symbol);
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
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                                }
                                localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                                symbol.Generics.Add(generics[i].Item1);
                            }
                            GenericsPush();
                            GenericTypes.Capacity += localGenericTypes.Count;
                            for (int i = 0; i < localGenericTypes.Count; i++)
                            {
                                GenericTypes.Add(localGenericTypes[i]);
                            }
                        }
                        else
                        {
                            stl = (List<Statement>)statement.Data[1];
                        }
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeClassStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        if (symbol.Generics.Count != 0) GenericsPop();
                        break;
                    }
                case StatementType.Struct:
                    {
                        var name = ((string, Place))statement.Data[0];
                        StructSymbol symbol = new StructSymbol { Name = name.Item1, Attributes = attributes ?? EmptyAttributes };

                        if (name.Item1 == string.Empty)
                        {
                            Throw(Text.IdentifierExpected1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        else
                        {
                            Package.Symbols.Add(symbol);
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
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                                }
                                localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                                symbol.Generics.Add(generics[i].Item1);
                            }
                            GenericsPush();
                            GenericTypes.Capacity += localGenericTypes.Count;
                            for (int i = 0; i < localGenericTypes.Count; i++)
                            {
                                GenericTypes.Add(localGenericTypes[i]);
                            }
                        }
                        else
                        {
                            stl = (List<Statement>)statement.Data[1];
                        }
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeStructStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        if (symbol.Generics.Count != 0) GenericsPop();
                        break;
                    }
                case StatementType.Use:
                    {
                        var name = ((string, Place))statement.Data[0];
                        Config c = null;
                        for (int i = 0; i < Project.Current.Configs.Count; i++)
                        {
                            if (Project.Current.Configs[i].Name == name.Item1)
                            {
                                c = Project.Current.Configs[i];
                                break;
                            }
                        }
                        if (c == null)
                        {
                            for (int i = 0; i < Package.Configs.Count; i++)
                            {
                                if (Package.Configs[i].Name == name.Item1)
                                {
                                    c = Package.Configs[i];
                                    break;
                                }
                            }
                        }
                        if (c == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        else UseConfig(c);
                        break;
                    }
                case StatementType.Config:
                    {
                        var name = ((string, Place))statement.Data[0];
                        var statements = (List<Statement>)statement.Data[1];
                        if (!Project.Current.Configs.TrueForAll(x => x.Name != name.Item1) || !Package.Configs.TrueForAll(x => x.Name != name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        var config = new Config { Name = name.Item1 };
                        Package.Configs.Add(config);
                        PendingConfigs.Add((statements, config));
                        break;
                    }
                case StatementType.ConvertionDeclaration:
                    {
                        var node = (Node)statement.Data[0];
                        if (node.NodeType == NodeType.Convertion)
                        {
                            var nodes = ((Node, Node))node.Data;
                            PendingConvertions.Add(new PendingConvertion { From = nodes.Item1, To = nodes.Item2, Value = (Node)statement.Data[1] });
                        }
                        else
                        {
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, ":", Tokens[node.EndToken].Match);
                        }
                        break;
                    }
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        var attr = ParseAttributes(Tokens[statement.Token]);
                        AnalyzeGlobalStatement((Statement)statement.Data[0], attr);
                        break;
                    }
            }
        }
        void AnalyzeElementStatement(Statement statement, ElementSymbol element, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    {
                        AnalyzeVariableDeclaration(statement, element, attributes);
                        break;
                    }
                case StatementType.DoubleNode:
                case StatementType.Node:
                    {
                        AnalyzeVariableAssignment(statement, element, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate, attributes);
                        break;
                    }
                case StatementType.PropertyDeclaration:
                    {
                        AnalyzeProperty(statement, element, attributes);
                        break;
                    }
                case StatementType.Element:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ElementSymbol symbol = new ElementSymbol { Name = name.Item1, Parent = element?.ToString(), Attributes = attributes ?? EmptyAttributes };
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
                        }
                        List<Statement> stl = (List<Statement>)statement.Data[1];
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((null, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeElementStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        break;
                    }
                case StatementType.Class:
                    {
                        var name = ((string, Place))statement.Data[0];
                        ClassSymbol symbol = new ClassSymbol { Name = name.Item1, Parent = element.ToString(), Attributes = attributes ?? EmptyAttributes };

                        if (name.Item1 == string.Empty)
                        {
                            symbol.Name = "class";
                            element.Alternate = symbol.ToString();
                        }
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
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
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                                }
                                localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                                symbol.Generics.Add(generics[i].Item1);
                            }
                            GenericsPush();
                            GenericTypes.Capacity += localGenericTypes.Count;
                            for (int i = 0; i < localGenericTypes.Count; i++)
                            {
                                GenericTypes.Add(localGenericTypes[i]);
                            }
                        }
                        else
                        {
                            stl = (List<Statement>)statement.Data[1];
                        }
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeClassStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        if (symbol.Generics.Count != 0) GenericsPop();
                        CurrentSymbol = element;
                        break;
                    }
                case StatementType.Struct:
                    {
                        var name = ((string, Place))statement.Data[0];
                        StructSymbol symbol = new StructSymbol { Name = name.Item1, Parent = element.ToString(), Attributes = attributes ?? EmptyAttributes };

                        if (name.Item1 == string.Empty)
                        {
                            symbol.Name = "struct";
                            element.Alternate = symbol.ToString();
                        }
                        if (!SymbolNameValid(name.Item1, element))
                        {
                            Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, element.ToString());
                        }
                        else
                        {
                            element.Children.Add(symbol);
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
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, generics[i].Item2, generics[i].Item1);
                                }
                                localGenericTypes.Add(new GenericType { Name = generics[i].Item1 });
                                symbol.Generics.Add(generics[i].Item1);
                            }
                            GenericsPush();
                            GenericTypes.Capacity += localGenericTypes.Count;
                            for (int i = 0; i < localGenericTypes.Count; i++)
                            {
                                GenericTypes.Add(localGenericTypes[i]);
                            }
                        }
                        else
                        {
                            stl = (List<Statement>)statement.Data[1];
                        }
                        CurrentSymbol = symbol;
                        CurrentPendingVariables.Push(new List<PendingVariable>());
                        CurrentPendingProperties.Push(new List<PendingProperty>());
                        PendingSymbols.Add((localGenericTypes, CurrentPendingVariables.Peek(), CurrentPendingProperties.Peek(), symbol));
                        for (int i = 0; i < stl.Count; i++)
                        {
                            AnalyzeStructStatement(stl[i], symbol, null);
                        }
                        CurrentPendingVariables.Pop();
                        CurrentPendingProperties.Pop();
                        if (symbol.Generics.Count != 0) GenericsPop();
                        CurrentSymbol = element;
                        break;
                    }
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        AnalyzeElementStatement((Statement)statement.Data[0], element, ParseAttributes(Tokens[statement.Token]));
                        break;
                    }
            }
        }
        void AnalyzeClassStatement(Statement statement, ClassSymbol _class, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    AnalyzeVariableDeclaration(statement, _class, attributes);
                    break;
                case StatementType.DoubleNode:
                case StatementType.Node:
                    AnalyzeVariableAssignment(statement, _class, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate, attributes);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _class, attributes);
                    break;
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        AnalyzeClassStatement((Statement)statement.Data[0], _class, ParseAttributes(Tokens[statement.Token]));
                        break;
                    }
            }
        }
        void AnalyzeStructStatement(Statement statement, StructSymbol _struct, Dictionary<string, string> attributes)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.VariableDeclaration:
                    AnalyzeVariableDeclaration(statement, _struct, attributes);
                    break;
                case StatementType.DoubleNode:
                case StatementType.Node:
                    AnalyzeVariableAssignment(statement, _struct, AccessorType.ImpliedPrivate, AccessorType.ImpliedPrivate, attributes);
                    break;
                case StatementType.PropertyDeclaration:
                    AnalyzeProperty(statement, _struct, attributes);
                    break;
                case StatementType.Attributes:
                    {
                        if (attributes != null) ThrowAttributesNotValid(statement);
                        AnalyzeStructStatement((Statement)statement.Data[0], _struct, ParseAttributes(Tokens[statement.Token]));
                        break;
                    }
            }
        }
        void AnalyzeConfigStatement(Statement statement, Config config)
        {
            switch (statement.StatementType)
            {
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    break;

                case StatementType.Use:
                    {
                        var name = ((string, Place))statement.Data[0];
                        Config c = null;
                        for (int i = 0; i < Project.Current.Configs.Count; i++)
                        {
                            if (Project.Current.Configs[i].Name == name.Item1) c = Project.Current.Configs[i];
                        }
                        if (c == null)
                        {
                            for (int i = 0; i < Package.Configs.Count; i++)
                            {
                                if (Package.Configs[i].Name == name.Item1) c = Package.Configs[i];
                            }
                        }
                        if (c == null)
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        else
                        {
                            config.Configs.Add(name.Item1);
                        }
                        break;
                    }
                case StatementType.Alias:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (!GlobalNameValid(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        var target = MatchNode((Node)statement.Data[1], true);
                        config.Aliases.Add(name.Item1, target is Symbol s ? s.ToString() : target);
                        break;
                    }
                case StatementType.OperatorDeclaration:
                    {
                        var generics = (List<string>)statement.Data[0];
                        var node = (Node)statement.Data[1];
                        (Node, Node) nodes;
                        if (node.NodeType == NodeType.Assignment)
                        {
                            nodes = ((Node, Node))node.Data;
                        }
                        else
                        {
                            nodes = (node, null);
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, "=", Tokens[node.EndToken].Match);
                        }
                        if (nodes.Item1.NodeType == NodeType.UnaryOperator)
                        {
                            var data = ((string, Node))nodes.Item1.Data;
                            var operand = NodeToType(data.Item2);
                            for (int i = 0; i < config.UnaryOperators.Count; i++)
                            {
                                if (config.UnaryOperators[i].Item1 == data.Item1 && config.UnaryOperators[i].Item2.Equals(operand))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[node.Token].Place, data.Item1 + operand.ToString());
                                }
                            }
                            if (nodes.Item2 != null)
                            {
                                var value = NodeToValue(nodes.Item2, null);
                                if (!(value.Type is FunctionType funcType && funcType.Parameters.Count == 1 && funcType.Parameters[0].Item1.Equals(operand)))
                                {
                                    Throw(Text.OperatorValueIllegal0, ThrowType.Error, Tokens[nodes.Item2.Token].Place, Tokens[nodes.Item2.Token].Match);
                                }
                                config.UnaryOperators.Add((data.Item1, operand, value));
                            }
                        }
                        else if (nodes.Item1.NodeType == NodeType.BinaryOperator)
                        {
                            var data = ((Node, string, Node))nodes.Item1.Data;
                            var operand1 = NodeToType(data.Item1);
                            var operand2 = NodeToType(data.Item3);
                            for (int i = 0; i < config.BinaryOperators.Count; i++)
                            {
                                if (config.BinaryOperators[i].Item2 == data.Item2 && config.BinaryOperators[i].Item1.Equals(operand1) && config.BinaryOperators[i].Item3.Equals(operand2))
                                {
                                    Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[node.Token].Place, operand1.ToString() + " " + data.Item2 + " " + operand2.ToString());
                                }
                            }
                            if (nodes.Item2 != null)
                            {
                                var value = NodeToValue(nodes.Item2, null);
                                if (!(value.Type is FunctionType funcType && funcType.Parameters.Count == 2 && funcType.Parameters[0].Item1.Equals(operand1) && funcType.Parameters[1].Item1.Equals(operand2)))
                                {
                                    Throw(Text.OperatorValueIllegal0, ThrowType.Error, Tokens[nodes.Item2.Token].Place, Tokens[nodes.Item2.Token].Match);
                                }
                                config.BinaryOperators.Add((operand1, data.Item2, operand2, value));
                            }
                        }
                        else
                        {
                            Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[statement.Token].Place, Tokens[statement.Token].Match);
                        }
                        break;
                    }
                case StatementType.ConvertionModeDefinition:
                    {
                        var node = (Node)statement.Data[1];
                        if (node.NodeType == NodeType.Convertion)
                        {
                            var nodes = ((Node, Node))node.Data;
                            _Type fromType = NodeToType(nodes.Item1), toType = NodeToType(nodes.Item2);
                            if (!Project.Current.Convertions.Any(x => x.Item1.Equals(fromType) && x.Item2.Equals(toType)) && !Package.Convertions.Any(x => x.Item1.Equals(fromType) && x.Item2.Equals(toType)))
                            {
                                Throw(Text.TypeConvertionIllegal2, ThrowType.Error, Tokens[statement.Token].Place, fromType.ToString(), toType.ToString());
                            }
                            else config.ConvertionTypes.Add((fromType, toType, (ConvertionType)statement.Data[0]));
                            break;
                        }
                        else
                        {
                            Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.EndToken].Place, ":", Tokens[node.EndToken].Match);
                        }
                        break;
                    }
            }
        }

        //variable and property analyzers are the same in element, class, struct so they are functions
        //the parentType variable
        void AnalyzeVariableDeclaration(Statement statement, Symbol parent, Dictionary<string, string> attributes)
        {
            AnalyzeVariableAssignment((Statement)statement.Data[2], parent, (AccessorType)statement.Data[0], (AccessorType)statement.Data[1], attributes);
        }
        void AnalyzeVariableAssignment(Statement statement, Symbol parent, AccessorType getType, AccessorType setType, Dictionary<string, string> attributes, _Type parentType = null)
        {
            Node typeNode;
            Node valueNode;

            if (statement.Data.Length == 1)
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
                        Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[nodes.Item1.Child.Token].Place, Tokens[nodes.Item1.Child.Token].Match);
                    }
                    name = Tokens[nodes.Item1.Token].Match;
                    if (!SymbolNameValid(name, parent))
                    {
                        Throw(Text.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, parent.ToString());
                        name = null;
                    }
                }
                else
                {
                    Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name);
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
                    Throw(Text.MemberDefined2, ThrowType.Error, Tokens[nodes.Item1.Token].Place, name, parent.ToString());
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
                    Throw(Text.TokenIllegal1, ThrowType.Error, Tokens[valueNode.Child.Token].Place, Tokens[valueNode.Child.Token].Match);
                }
                name = Tokens[valueNode.Token].Match;
                if (!SymbolNameValid(name, parent))
                {
                    Throw(Text.MemberDefined2, ThrowType.Error, Tokens[valueNode.Token].Place, name, parent.ToString());
                    name = null;
                }
                valueNode = null;
            }
            else
            {
                Throw(Text.IdentifierExpected1, ThrowType.Error, Tokens[valueNode.Token].Place, Tokens[valueNode.Token].Match);
            }

            var s = new VariableSymbol { Name = name, Parent = parent.ToString(), Get = getType, Set = setType, Attributes = attributes ?? EmptyAttributes };
            CurrentPendingVariables.Peek().Add(new PendingVariable { Symbol = s, TypeNode = typeNode, ValueNode = valueNode, FunctionChildNode = functionChildNode });

            if (name != null)
            {
                parent.Children.Add(s);
            }
        }
        void AnalyzeProperty(Statement statement, Symbol parent, Dictionary<string, string> attributes)
        {
            var getType = (AccessorType)statement.Data[0];
            var setType = (AccessorType)statement.Data[1];
            var typeNode = (Node)statement.Data[2];
            var name = ((string, Place))statement.Data[3];
            var valueNode = (Node)statement.Data[4];

            var prop = new PropertySymbol { Get = getType, Set = setType, Name = name.Item1, Parent = parent.ToString(), Attributes = attributes ?? EmptyAttributes };
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
                        Throw(Text.PropertySetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
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
                        Throw(Text.PropertyGetterDefined1, ThrowType.Error, Tokens[statement.Token].Place, existingProp.ToString());
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
                Throw(Text.MemberDefined2, ThrowType.Error, name.Item2, name.Item1, existingSymbol.ToString());
            }

            CurrentPendingProperties.Peek().Add(pendingProp);
        }

        #endregion
        #region procedure semantics
        //used to get the function value from a Node with NodeType.FunctionDeclaration
        void ProcessFunctionParameters(SecondaryNode child, out List<string> generics, out List<(_Type, string, Node)> parameters, out bool genericsPushed)
        {
            generics = new List<string>();
            parameters = new List<(_Type, string, Node)>();
            if (child.NodeType == SecondaryNodeType.Generics)
            {
                genericsPushed = true;
                GenericsPush();
                var genericNodes = (List<Node>)child.Data;
                for (int i = 0; i < genericNodes.Count; i++)
                {
                    string genericName = Tokens[genericNodes[i].Token].Match;
                    ThrowIfNotIdentifier(genericNodes[i]);
                    if (!LocalNameValid(genericName) || generics.Contains(genericName))
                    {
                        Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[genericNodes[i].Token].Place, genericName);
                    }
                    generics.Add(genericName);
                    GenericTypes.Add(new GenericType { Name = genericName });
                }
                child = child.Child;
            }
            else
            {
                genericsPushed = false;
            }
            var paramNodes = child.NodeType == SecondaryNodeType.Call ? new List<(Node, Node)>() : (List<(Node, Node)>)child.Data;
            parameters.Capacity = paramNodes.Count;
            for (int i = 0; i < paramNodes.Count; i++)
            {
                var type = NodeToType(paramNodes[i].Item1);
                if (paramNodes[i].Item2.NodeType == NodeType.Assignment)
                {
                    var nodes = ((Node, Node))paramNodes[i].Item2.Data;
                    string paramName = Tokens[nodes.Item1.Token].Match;
                    ThrowIfNotIdentifier(nodes.Item1);
                    if (!LocalNameValid(paramName))
                    {
                        Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, paramName);
                    }
                    parameters.Add((type, paramName, nodes.Item2));
                }
                else
                {
                    string paramName = Tokens[paramNodes[i].Item2.Token].Match;
                    ThrowIfNotIdentifier(paramNodes[i].Item2);
                    if (!LocalNameValid(paramName))
                    {
                        Throw(Text.IdentifierDefined1, ThrowType.Error, Tokens[paramNodes[i].Item2.Token].Place, paramName);
                    }
                    parameters.Add((type, paramName, null));
                }
            }
        }
        FunctionValue ProcessFunctionValue(FunctionType functype, List<(_Type, string, Node)> parameters, Node valueNode, bool strictRettype, bool dontPushGenerics)
        {
            var funcvalue = new FunctionValue { Type = functype, Generics = functype.Generics };
            if (!dontPushGenerics && functype.Generics.Count != 0)
            {
                GenericsPush();
                for (int i = 0; i < functype.Generics.Count; i++)
                {
                    GenericTypes.Add(new GenericType { Name = functype.Generics[i] });
                }
            }
            if (parameters.Count != 0)
            {
                funcvalue.Parameters.Capacity = parameters.Count;
                LocalsPush();
                for (int i = 0; i < parameters.Count; i++)
                {
                    funcvalue.Parameters.Add((parameters[i].Item2, parameters[i].Item3 == null ? null : NodeToValue(parameters[i].Item3, parameters[i].Item1)));
                    Locals.Add(new LocalValue { Name = parameters[i].Item2, Type = parameters[i].Item1 });
                }
            }

            funcvalue.Value = NodeToValue(valueNode, functype.ReturnType, strictProcedureType: strictRettype);
            if (!strictRettype)
            {
                if (funcvalue.Value.Type == functype)
                {
                    Throw(Text.FunctionCycle0, ThrowType.Error, Tokens[valueNode.Token].Place);
                }
                else functype.ReturnType = funcvalue.Value.Type;
            }

            if (parameters.Count != 0) LocalsPop();
            if (functype.Generics.Count != 0) GenericsPop();
            return funcvalue;
        }

        class ProcedureInfo
        {
            public bool ReturnTypeDetermined;
            public _Type ReturnType;
            public bool Recurring;
            public Value AssignedToValue; //used to check if local function is recursive
            public Stack<List<(LocalValue, _Type)>> OldLocalTypes = new Stack<List<(LocalValue, _Type)>>();
            public bool LastWasIf;
            public bool? LastIfCondition;
            public List<string> Scopes = new List<string>();
        }

        List<ProcedureInfo> Procedures = new List<ProcedureInfo>();
        ProcedureInfo CurrentProcedure => Procedures[Procedures.Count - 1];

        ProceduralValue ScopeToProcedure(Statement statement)
        {
            Procedures.Add(new ProcedureInfo());
            LocalsPush();
            var value = new ProceduralValue { Instruction = StatementToInstruction(statement), Type = Procedures[Procedures.Count - 1].ReturnType };
            LocalsPop();
            Procedures.RemoveAt(Procedures.Count - 1);
            return value;
        }
        ProceduralValue ScopeToProcedure(Statement statement, _Type rettype)
        {
            Procedures.Add(new ProcedureInfo { ReturnTypeDetermined = true, ReturnType = rettype });
            LocalsPush();
            var value = new ProceduralValue { Instruction = StatementToInstruction(statement), Type = rettype };
            LocalsPop();
            Procedures.RemoveAt(Procedures.Count - 1);
            return value;
        }

        _Type CombineTypes(_Type x, _Type y)
        {
            if (x.Equals(y)) return x;
            HashSet<_Type> types = new HashSet<_Type>();
            if (x is MultiType mt) types.UnionWith(mt.Types);
            else types.Add(x);
            if (y is MultiType mt2) types.UnionWith(mt2.Types);
            else types.Add(y);
            return new MultiType { Types = types };
        }

        List<(LocalValue, _Type)> CreateLocalTypeList()
        {
            List<(LocalValue, _Type)> list = new List<(LocalValue, _Type)>();
            for (int i = 0; i < Locals.Count; i++)
            {
                list.Add((Locals[i], Locals[i].Type));
            }
            return list;
        }
        void PushLocalTypes()
        {
            CurrentProcedure.OldLocalTypes.Push(CreateLocalTypeList());
        }

        void PopLocalTypes()
        {
            var l = CurrentProcedure.OldLocalTypes.Pop();
            for (int i = 0; i < l.Count; i++)
            {
                l[i].Item1.Type = CombineTypes(l[i].Item1.Type, l[i].Item2);
            }
        }

        bool? ConditionIsConstant(Value cond)
        {
            if (cond == LiteralValue.True) return true;
            if (cond == LiteralValue.False) return false;
            if (cond is OperationValue op && op.OperationType == OperationType.Is && op.Values[0] is LocalValue local)
            {
                if (op.Types[0].Equals(local.Type)) return true;
                if (local.Type is MultiType && !(op.Types[0] is MultiType)) return null;
                return false;
            }
            return null;
        }

        Instruction StatementToInstruction(Statement statement)
        {
            Instruction makeInst(Instruction inst)
            {
                if (DebugMode)
                {
                    inst.File = Tokens[statement.Token].Place.File;
                    inst.Line = Tokens[statement.Token].Place.Line + (uint)1;
                    inst.Index = Tokens[statement.Token].Place.Index + (uint)1;
                }
                return inst;
            }
            if (CurrentProcedure.LastWasIf)
            {
                CurrentProcedure.LastWasIf = false;
                if (statement.StatementType == StatementType.Else)
                {
                    if (CurrentProcedure.LastIfCondition.HasValue)
                    {
                        if (CurrentProcedure.LastIfCondition.Value)
                        {
                            return makeInst(StatementToInstruction((Statement)statement.Data[0]));
                        }
                        return NoOpInstruction.Value;
                    }
                    //copy the local types before if
                    var l = CurrentProcedure.OldLocalTypes.Pop();
                    for (int i = 0; i < l.Count; i++)
                    {
                        l[i].Item1.Type = l[i].Item2;
                    }
                    for (int i = 0; i < Locals.Count; i++)
                    {
                        if (l.TrueForAll(x => x.Item1 != Locals[i])) Locals.RemoveAt(i);
                    }
                    PushLocalTypes();
                    var inst = makeInst(StatementToInstruction((Statement)statement.Data[0]));
                    PopLocalTypes();
                    return makeInst(new ElseInstruction { Instruction = inst });
                }
                PopLocalTypes();
            }

            switch (statement.StatementType)
            {
                default:
                    Throw(Text.StatementIllegal0, ThrowType.Error, Tokens[statement.Token].Place);
                    return NoOpInstruction.Value;
                case StatementType.Node:
                    {
                        Node node = (Node)statement.Data[0];
                        switch (node.NodeType)
                        {
                            default:
                                {
                                    var value = NodeToValue(node, null);
                                    return makeInst(new ActionInstruction { Value = value });
                                }
                            case NodeType.Assignment:
                                {
                                    var nodes = ((Node, Node))node.Data;
                                    Value left = NodeToValue(nodes.Item1, null, checkCanGet: false, createLocals: true, setIndexer: true);
                                    Value right;
                                    if (left is LocalValue local)
                                    {
                                        right = NodeToValue(nodes.Item2, null);
                                        local.Type = right.Type;
                                    }
                                    else
                                    {
                                        if (left is IndexerSetTemporaryValue indexerSet)
                                        {
                                            var funcType = (FunctionType)indexerSet.Call.Function.Type;
                                            indexerSet.Call.Parameters.Add((null, NodeToValue(nodes.Item2, funcType.Parameters[funcType.Parameters.Count - 1].Item1)));
                                            return makeInst(new ActionInstruction { Value = indexerSet.Call });
                                        }
                                        right = NodeToValue(nodes.Item2, left.Type);
                                        if (!CanSet(left))
                                        {
                                            Throw(Text.ValueNotSettable1, ThrowType.Error, Tokens[nodes.Item1.Token].Place, left.ToString());
                                        }
                                    }
                                    return makeInst(new AssignInstruction { Left = left, Right = right });
                                }
                            case NodeType.FunctionDeclaration:
                                {
                                    var nodes = ((Node, Node))node.Data;
                                    var left = NodeToValue(nodes.Item1, null, checkCanGet: false, createLocals: true, ignoreChildren: true);
                                    ProcessFunctionParameters(nodes.Item1.Child, out var generics, out var parameters, out var genericsPushed);
                                    var functype = new FunctionType();
                                    functype.Generics = generics;
                                    for (int i = 0; i < parameters.Count; i++)
                                    {
                                        functype.Parameters.Add((parameters[i].Item1, parameters[i].Item2, parameters[i].Item3 != null));
                                    }
                                    if (left is LocalValue local)
                                    {
                                        local.Type = functype;
                                    }
                                    var right = ProcessFunctionValue(functype, parameters, nodes.Item2, false, true);
                                    if (genericsPushed) GenericsPop();
                                    return makeInst(new AssignInstruction { Left = left, Right = right });
                                }
                        }
                    }
                case StatementType.Scope:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        CurrentProcedure.Scopes.Add(name.Item1);
                        var stl = (List<Statement>)statement.Data[1];
                        var instl = new List<Instruction>(stl.Count);
                        for (int i = 0; i < stl.Count; i++)
                        {
                            instl.Add(StatementToInstruction(stl[i]));
                        }
                        CurrentProcedure.Scopes.RemoveAt(CurrentProcedure.Scopes.Count - 1);
                        return makeInst(new ScopeInstruction { Instructions = instl, Name = name.Item1 });
                    }
                case StatementType.If:
                    {
                        var node = (Node)statement.Data[0];
                        var cond = NodeToValue(node, BooleanType.Value);
                        var x = ConditionIsConstant(cond);
                        PushLocalTypes();
                        var inst = StatementToInstruction((Statement)statement.Data[1]);
                        CurrentProcedure.LastWasIf = true;
                        if (x.HasValue)
                        {
                            if (x.Value)
                            {
                                Throw(Text.ConditionAlwaysTrue1, ThrowType.Warning, Tokens[node.Token].Place, cond.ToString());
                                return inst;
                            }
                            Throw(Text.ConditionAlwaysFalse1, ThrowType.Warning, Tokens[node.Token].Place, cond.ToString());
                            return NoOpInstruction.Value;
                        }
                        return makeInst(new IfInstruction { Condition = cond, Instruction = inst });
                    }
                case StatementType.Return:
                    {
                        var node = (Node)statement.Data[0];
                        if (node == null)
                        {
                            if (CurrentProcedure.ReturnTypeDetermined)
                            {
                                if (CurrentProcedure.ReturnType != null)
                                {
                                    Throw(Text.ValueExpected1, ThrowType.Error, Tokens[statement.Token + 1].Place, Tokens[statement.Token + 1].Match);
                                }
                            }
                            else
                            {
                                CurrentProcedure.ReturnTypeDetermined = true;
                                CurrentProcedure.ReturnType = null;
                            }
                            return makeInst(new ReturnInstruction());
                        }
                        else
                        {
                            var value = NodeToValue(node, CurrentProcedure.ReturnType);
                            if (CurrentProcedure.ReturnTypeDetermined)
                            {
                                if (CurrentProcedure.ReturnType == null)
                                {
                                    Throw(Text.TokenExpected2, ThrowType.Error, Tokens[node.Token].Place, ";", Tokens[node.Token].Match);
                                }
                            }
                            else
                            {
                                CurrentProcedure.ReturnTypeDetermined = true;
                                CurrentProcedure.ReturnType = value.Type;
                            }
                            return makeInst(new ReturnInstruction { Value = value });
                        }
                    }
                case StatementType.Break:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && !CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        return makeInst(new ControlInstruction { Name = name.Item1, Type = ControlInstructionType.Break });
                    }
                case StatementType.Continue:
                    {
                        var name = ((string, Place))statement.Data[0];
                        if (name.Item1 != null && !CurrentProcedure.Scopes.Contains(name.Item1))
                        {
                            Throw(Text.IdentifierNotDefined1, ThrowType.Error, name.Item2, name.Item1);
                        }
                        return makeInst(new ControlInstruction { Name = name.Item1, Type = ControlInstructionType.Continue });
                    }
                case StatementType.Throw:
                    {
                        string messageMatch = (string)statement.Data[1];
                        return makeInst(new ThrowInstruction { Exception = (((string, Place))statement.Data[0]).Item1, Message = messageMatch.Substring(1, messageMatch.Length - 2) });
                    }
                case StatementType.Catch:
                    {
                        return makeInst(new CatchInstruction { Exceptions = ((List<(string, Place)>)statement.Data[0]).ConvertAll<string>(x => x.Item1), Instruction = StatementToInstruction((Statement)statement.Data[1]) });
                    }
            }
        }

        #endregion
    }
}
