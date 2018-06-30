using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SSSerializer;
using SSSerializer.Json;

//Common pace library for c# which implements data structure for reading, writing, pace packages and some other stuff

namespace Pace.CommonLibrary
{
    public static class Info
    {
        public static readonly string Version = "pacednl A-1 with substantially-small-serializer";
    }

    public static class Settings
    {
        public static string PackageDirectory = null;
        public static string PackageFileExtention = ".pacepack";

        public static string FormatPackageFilename(string packagename)
        {
            return PackageDirectory + Path.DirectorySeparatorChar.ToString() + packagename + PackageFileExtention;
        }
        public static bool PackageNameValid(string s)
        {
            return
                !string.IsNullOrEmpty(s) &&
                s.All(x => char.IsLower(x) || x == '-');
        }
    }

    public class Project
    {
        public static Project Current = new Project();

        public Dictionary<string, Package> Packages = new Dictionary<string, Package>();
        public Value EntryPoint
        {
            get
            {
                foreach (var x in Packages)
                {
                    if (x.Value.EntryPoint != null)
                        return x.Value.EntryPoint;
                }
                return null;
            }
        }

        public Symbol GetSymbol(string fullname)
        {
            Tools.ProcessSymbolName(fullname, out var package, out var parts);
            if (package != null && Packages.TryGetValue(package, out var pack)) return Tools.MatchSymbol(parts, pack.Symbols);
            return null;
        }
        public Config GetConfig(string name)
        {
            Tools.ProcessConfigName(name, out var package, out var config);
            if (Packages.TryGetValue(package, out var pack)) return pack.GetConfig(config);
            return null;
        }
        public Value GetConvertion(Type from, Type to)
        {
            foreach (var x in Packages)
            {
                for (int i = 0; i < x.Value.Convertions.Count; i++)
                {
                    if (x.Value.Convertions[i].Item1.Equals(from) && x.Value.Convertions[i].Item2.Equals(to)) return x.Value.Convertions[i].Item3;
                }
            }
            return null;
        }

        public IEnumerable<Symbol> GetAllTopLevelSymbols()
        {
            foreach (var x in Packages)
            {
                for (int i = 0; i < x.Value.Symbols.Count; i++)
                {
                    yield return x.Value.Symbols[i];
                }
            }
        }
        public IEnumerable<Symbol> GetAllTopLevelSymbols(string name)
        {
            foreach (var x in Packages)
            {
                for (int i = 0; i < x.Value.Symbols.Count; i++)
                {
                    if (x.Value.Symbols[i].Name == name) yield return x.Value.Symbols[i];
                }
            }
        }
        public IEnumerable<Config> GetAllConfigs(string name)
        {
            foreach (var x in Packages)
            {
                for (int i = 0; i < x.Value.Configs.Count; i++)
                {
                    if (x.Value.Configs[i].Name == name) yield return x.Value.Configs[i];
                }
            }
        }

        public string Unimport(Package package)
        {
            foreach (var x in Packages)
            {
                if (x.Value.Dependencies.Any(p => ReferenceEquals(p, package))) return "Other packages depend on this package";
            }
            Packages.Remove(package.Name);
            return null;
        }
    }

    public class Package
    {
        public string Name;
        public Value EntryPoint;
        public List<Package> Dependencies = new List<Package>();
        public List<Symbol> Symbols = new List<Symbol>();
        public List<Config> Configs = new List<Config>();
        public List<(Type, Type, Value)> Convertions = new List<(Type, Type, Value)>();

        public void Save()
        {
            File.WriteAllText(Settings.FormatPackageFilename(Name), new JSONWriter().Write(ToNode(), false));
        }
        public static string Load(string name, out Package package) 
        {
            if (Project.Current.Packages.TryGetValue(name, out package)) return null;
            string file = Settings.FormatPackageFilename(name);
            if (!File.Exists(file)) return "Package '" + name + "' not found";
            package = new Package { Name = name };
            Project.Current.Packages.Add(name, package);
            return package.FromNode((ObjectNode)new JSONReader().Read(File.ReadAllText(file))); ;
        }

        public ObjectNode ToNode()
        {
            var node = new ObjectNode();
            node.Items.Add("Dependencies", new ArrayNode(Dependencies.ConvertAll(x => (Node)(StringNode)x.Name)));
            if (EntryPoint != null) node.Items.Add("EntryPoint", EntryPoint.WriteValue());
            node.Items.Add("Symbols", new ArrayNode(Symbols.ConvertAll(x => (Node)x.WriteSymbol())));
            node.Items.Add("Configs", new ArrayNode(Configs.ConvertAll(x => (Node)x.WriteNode())));
            node.Items.Add("Convertions", new ArrayNode(Convertions.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "From", x.Item1.WriteType() }, { "To", x.Item2.WriteType() }, { "Value", x.Item3.WriteValue() } }))));
            return node;
        }
        public string FromNode(ObjectNode node)
        {
            var deparr = ((ArrayNode)node["Dependencies"]);
            for (int i = 0; i < deparr.Items.Count; i++)
            {
                string err = Load((StringNode)deparr.Items[i], out Package pack);
                Dependencies.Add(pack);
                if (err != null) return err;
            }
            Symbols = ((ArrayNode)node["Symbols"]).Items.ConvertAll(x => Symbol.Create((ObjectNode)x, null, this));
            Configs = ((ArrayNode)node["Configs"]).Items.ConvertAll(x => Config.Create((ObjectNode)x, this));
            foreach (var x in Symbols) x.Read();
            foreach(var x in Configs) x.Read();
            var convertions = ((ArrayNode)node["Convertions"]).Items;
            for (int i = 0; i < convertions.Count; i++)
            {
                var obj = (ObjectNode)convertions[i];
                Type from = Type.ReadType((ObjectNode)obj["From"]);
                Type to = Type.ReadType((ObjectNode)obj["To"]);
                if (Project.Current.GetConvertion(from, to) != null)
                    return "Package contains convertions already defined";
                Convertions.Add((Type.ReadType((ObjectNode)obj["From"]), Type.ReadType((ObjectNode)obj["To"]), Value.ReadValue((ObjectNode)obj["Value"])));
            }
            var entrypointnode = node["EntryPoint"];
            if (entrypointnode != null) EntryPoint = Value.ReadValue((ObjectNode)entrypointnode);
            return null;
        }

        public Symbol GetTopLevelSymbol(string name)
        {
            for (int i = 0; i < Symbols.Count; i++)
            {
                if (Symbols[i].Name == name) return Symbols[i];
            }
            return null;
        }
        public Config GetConfig(string name)
        {
            for (int i = 0; i < Configs.Count; i++)
            {
                if (Configs[i].Name == name) return Configs[i];
            }
            return null;
        }

        public bool IdentifierTaken(string s)
        {
            for (int i = 0; i < Symbols.Count; i++)
            {
                if (Symbols[i].Name == s) return true;
            }
            for (int i = 0; i < Configs.Count; i++)
            {
                if (Configs[i].Name == s) return true;
            }
            return false;
        }
    }

    public struct OperationResult 
    {
        public bool IsSuccessful;
        public string Message;

        public static OperationResult Success = new OperationResult { IsSuccessful = true, Message = "Operation completed successfully." };
        public OperationResult(string message)
        {
            IsSuccessful = false;
            Message = message;
        }
    }

    public enum ConvertionType
    {
        Implicit,
        Explicit,
        Automatic
    }

    public abstract class Symbol
    {
        public string Name;
        protected ObjectNode Node;
        public Symbol Parent;
        public Package Package;
        public abstract List<Symbol> Children { get; set; }
        public Dictionary<string, string> Attributes = new Dictionary<string, string>();

        public abstract void Write(ObjectNode node);
        public abstract void Read();

        string fullName;
        public override string ToString()
        {
            if (fullName == null)
            {
                if (Parent == null) fullName = Package.Name + "::" + Name;
                else fullName = Parent + "." + Name;
            }
            return fullName;
        }

        public ObjectNode WriteSymbol()
        {
            var node = new ObjectNode();
            node.Items.Add("Name", (StringNode)Name);
            if (Attributes.Count != 0)
            {
                var arr = new ArrayNode();
                foreach (var x in Attributes)
                {
                    if (x.Value == null) arr.Items.Add(new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)x.Key } }));
                    else arr.Items.Add(new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)x.Key }, { "Value", (StringNode)x.Value } }));
                }
                node.Items.Add("Attributes", arr);
            }
            if (Children != null && Children.Count != 0) node.Items.Add("Children", new ArrayNode(Children.ConvertAll(x => (Node)x.WriteSymbol())));
            Write(node);
            return node;
        }
        public static Symbol Create(ObjectNode node, Symbol parent, Package package)
        {
            Symbol symbol = null;
            switch ((StringNode)node["Kind"])
            {
                case "Element": symbol = new ElementSymbol(); break;
                case "Class": symbol = new ClassSymbol(); break;
                case "Struct": symbol = new StructSymbol(); break;
                case "Variable": symbol = new VariableSymbol(); break;
                case "Property": symbol = new PropertySymbol(); break;
            }
            symbol.Name = (StringNode)node["Name"];
            symbol.Parent = parent;
            symbol.Package = package;
            symbol.Node = node;
            var attributesNode = node["Attributes"];
            if (attributesNode != null)
            {
                var arr = ((ArrayNode)attributesNode).Items;
                for (int i = 0; i < arr.Count; i++)
                {
                    var obj = (ObjectNode)arr[i];
                    var val = obj["Value"];
                    if (val == null) symbol.Attributes.Add((StringNode)obj["Name"], null);
                    else symbol.Attributes.Add((StringNode)obj["Name"], (StringNode)val);
                }
            }
            var childrenNode = node["Children"];
            if (childrenNode != null) symbol.Children = ((ArrayNode)childrenNode).Items.ConvertAll(x => Create((ObjectNode)x, symbol, package));
            return symbol;
        }
    }
    public class ElementSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children { get => c; set => c = value; }
        public Symbol Alternate;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Element");
            if (Alternate != null) node.Items.Add("Alternate", (StringNode)Alternate.ToString());
        }
        public override void Read()
        {
            var alternatenode = Node["Alternate"];
            if (alternatenode != null) Alternate = Project.Current.GetSymbol((StringNode)alternatenode);
            foreach (var x in Children) x.Read();
        }
    }
    public abstract class TypeSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children { get => c; set => c = value; }
        public List<string> Generics = new List<string>();

        protected abstract string TypeSymbolKind { get; }

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Class");
            if (Generics.Count != 0) node.Items.Add("Generics", new ArrayNode(Generics.ConvertAll(x => (Node)(StringNode)x)));
        }
        public override void Read()
        {
            var genericsnode = Node["Generics"];
            if (genericsnode != null) Generics = ((ArrayNode)genericsnode).Items.ConvertAll(x => (string)(StringNode)x);
            foreach (var x in Children) x.Read();
        }
    }
    public class ClassSymbol : TypeSymbol
    {
        protected override string TypeSymbolKind => "Class";
    }
    public class StructSymbol : TypeSymbol
    {
        protected override string TypeSymbolKind => "Struct";
    }
    public enum AccessorType
    {
        None,
        Public,
        Private,
        ImpliedPublic,
        ImpliedPrivate,
    }
    public class VariableSymbol : Symbol
    {
        public override List<Symbol> Children { get => null; set => throw new InvalidOperationException(); }

        public AccessorType Get;
        public AccessorType Set;

        private Type _t;
        public Type Type
        {
            get
            {
                if (_t == null)
                {
                    _t = Value.Type;
                }
                return _t;
            }
            set => _t = value;
        }

        public Value Value;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Variable");
            node.Items.Add("Get", (StringNode)Get.ToString());
            node.Items.Add("Set", (StringNode)Set.ToString());
            node.Items.Add("Type", Type.WriteType());
            node.Items.Add("Value", Value.WriteValue());
        }
        public override void Read()
        {
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), (StringNode)Node["Get"]);
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), (StringNode)Node["Set"]);
            Type = Type.ReadType((ObjectNode)Node["Type"]);
            Value = Value.ReadValue((ObjectNode)Node["Value"]);
        }
    }
    public class PropertySymbol : Symbol
    {
        public override List<Symbol> Children { get => null; set => throw new InvalidOperationException(); }

        public AccessorType Get;
        public AccessorType Set;
        public Value Getter;
        public Value Setter;
        private Type _t;
        public Type Type
        {
            get
            {
                if (_t == null)
                {
                    _t = Getter == null ? Setter?.Type : Getter.Type;
                }
                return _t;
            }
            set => _t = value;
        }

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Property");
            node.Items.Add("Get", (StringNode)Get.ToString());
            node.Items.Add("Set", (StringNode)Set.ToString());
            if (Getter != null) node.Items.Add("Getter", Getter.WriteValue());
            if (Setter != null) node.Items.Add("Setter", Setter.WriteValue());
            node.Items.Add("Type", Type.WriteType());
        }
        public override void Read()
        {
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), (StringNode)Node["Get"]);
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), (StringNode)Node["Set"]);
            if (Get != AccessorType.None) Getter = Value.ReadValue((ObjectNode)Node["Getter"]);
            if (Set != AccessorType.None) Setter = Value.ReadValue((ObjectNode)Node["Setter"]);
            Type = Type.ReadType((ObjectNode)Node["Type"]);
        }
    }

    public class Config
    {
        public string Name;
        public Package Package;
        public Dictionary<string, object> Aliases = new Dictionary<string, object>();
        public List<(string, Type, Value)> UnaryOperators = new List<(string, Type, Value)>();
        public List<(Type, string, Type, Value)> BinaryOperators = new List<(Type, string, Type, Value)>();
        public List<(Type, Type, ConvertionType)> ConvertionTypes = new List<(Type, Type, ConvertionType)>();
        public List<Config> Configs = new List<Config>();
        private ObjectNode Node;

        public ObjectNode WriteNode()
        {
            var node = new ObjectNode();
            node.Items.Add("Name", (StringNode)Name);
            if (Aliases.Count != 0)
            {
                var aliasesnode = new ArrayNode();
                foreach (var x in Aliases)
                {
                    var aliasnode = new ObjectNode();
                    aliasnode.Items.Add("Name", (StringNode)x.Key);
                    if (x.Value is Symbol symbol) aliasnode.Items.Add("Symbol", (StringNode)symbol.ToString());
                    else if (x.Value is Value value) aliasnode.Items.Add("Value", value.WriteValue());
                    else if (x.Value is Type type) aliasnode.Items.Add("Type", type.WriteType());
                    aliasesnode.Items.Add(aliasnode);
                }
                node.Items.Add("Aliases", aliasesnode);
            }
            if (UnaryOperators.Count != 0) node.Items.Add("UnaryOperators", new ArrayNode(UnaryOperators.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "Operator", (StringNode)x.Item1 }, { "OperandType", x.Item2.WriteType() }, { "Value", x.Item3.WriteValue() } }))));
            if (BinaryOperators.Count != 0) node.Items.Add("BinaryOperators", new ArrayNode(BinaryOperators.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "Operator", (StringNode)x.Item2 }, { "LeftOperandType", x.Item1.WriteType() }, { "RightOperandType", x.Item3.WriteType() }, { "Value", x.Item4.WriteValue() } }))));
            if (ConvertionTypes.Count != 0) node.Items.Add("ConvertionTypes", new ArrayNode(ConvertionTypes.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "From", x.Item1.WriteType() }, { "To", x.Item2.WriteType() }, { "ConvertionType", (StringNode)x.Item3.ToString() } }))));
            if (Configs.Count != 0) node.Items.Add("Configs", new ArrayNode(Configs.ConvertAll(x => (Node)(StringNode)x.ToString())));
            return node;
        }
        public static Config Create(ObjectNode node, Package package)
        {
            var config = new Config
            {
                Name = (StringNode)node["Name"],
                Package = package,
                Node = node
            };
            return config;
        }
        public void Read()
        {
            var aliasesnode = Node["Aliases"];
            if (aliasesnode != null)
            {
                var al = (ArrayNode)aliasesnode;
                for (int i = 0; i < al.Items.Count; i++)
                {
                    var aliasnode = (ObjectNode)al.Items[i];
                    if (aliasnode.Items.TryGetValue("Symbol", out var symbol)) Aliases.Add((StringNode)aliasnode["Name"], (string)(StringNode)symbol);
                    else if (aliasnode.Items.TryGetValue("Value", out var value)) Aliases.Add((StringNode)aliasnode["Name"], Value.ReadValue((ObjectNode)value));
                    else if (aliasnode.Items.TryGetValue("Type", out var type)) Aliases.Add((StringNode)aliasnode["Name"], Type.ReadType((ObjectNode)type));
                }
            }
            var unaryopsnode = Node["UnaryOperators"];
            if (unaryopsnode != null) UnaryOperators = ((ArrayNode)unaryopsnode).Items.ConvertAll(x => ((string)(StringNode)((ObjectNode)x)["Operator"], Type.ReadType((ObjectNode)((ObjectNode)x)["OperandType"]), Value.ReadValue((ObjectNode)((ObjectNode)x)["Value"])));
            var binaryopsnode = Node["BinaryOperators"];
            if (binaryopsnode != null) BinaryOperators = ((ArrayNode)binaryopsnode).Items.ConvertAll(x => (Type.ReadType((ObjectNode)((ObjectNode)x)["LeftOperandType"]), (string)(StringNode)((ObjectNode)x)["Operator"], Type.ReadType((ObjectNode)((ObjectNode)x)["RightOperandType"]), Value.ReadValue((ObjectNode)((ObjectNode)x)["Value"])));
            var convertionmodenode = Node["ConvertionTypes"];
            if (convertionmodenode != null) ConvertionTypes = ((ArrayNode)convertionmodenode).Items.ConvertAll(x => (Type.ReadType((ObjectNode)((ObjectNode)x)["From"]), Type.ReadType((ObjectNode)((ObjectNode)x)["To"]), (ConvertionType)Enum.Parse(typeof(ConvertionType), (string)(StringNode)((ObjectNode)x)["ConvertionType"])));
            var configsnode = Node["Configs"];
            if (configsnode != null) Configs = ((ArrayNode)configsnode).Items.ConvertAll(x => Project.Current.GetConfig((StringNode)x));
        }

        public override string ToString()
        {
            return Package.Name + Name;
        }
    }

    public abstract class Type
    {
        public override bool Equals(object obj)
        {
            return obj is Type t && Equals(t);
        }
        public override int GetHashCode()
        {
            return 0;
        }
        public abstract bool Equals(Type t);
        public abstract void Write(ObjectNode node);
        public abstract void Read(ObjectNode node);
        public abstract bool IsRefType { get; }
        public abstract bool IsNullable { get; }
        public abstract void ReplaceAllSubtypes(Func<Type, Type> func);

        public abstract Value GetDefaultValue();

        public ObjectNode WriteType()
        {
            var node = new ObjectNode();
            Write(node);
            return node;
        }
        public static Type ReadType(ObjectNode node)
        {
            Type type = null;
            switch ((StringNode)node["Kind"])
            {
                case "Normal": type = new NormalType(); break;
                case "Function": type = new FunctionType(); break;
                case "Record": type = new RecordType(); break;
                case "Collection": type = new CollectionType(); break;
                case "Nullable": type = new NullableType(); break;
                case "Generic": type = new GenericType(); break;
                case "Object": type = ObjectType.Value; break;
                case "Boolean": type = BooleanType.Value; break;
            }
            type.Read(node);
            return type;
        }
    }
    public class NormalType : Type
    {
        public Symbol Base;
        public bool RefType;
        public List<(string, Type)> Generics = new List<(string, Type)>();

        public override bool IsRefType => RefType;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return new NewValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t is NormalType tt && Base == tt.Base && RefType == tt.RefType;
        }

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Normal");
            node.Items.Add("Base", (StringNode)Base.ToString());
            node.Items.Add("RefType", (StringNode)RefType.ToString());
            if (Generics.Count != 0) node.Items.Add("Generics", new ArrayNode(Generics.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)x.Item1 }, { "Type", x.Item2.WriteType() } }))));
        }
        public override void Read(ObjectNode node)
        {
            Base = Project.Current.GetSymbol((StringNode)node["Base"]);
            RefType = bool.Parse((StringNode)node["RefType"]);
            var genericsnode = node["Generics"];
            if (genericsnode != null) Generics = ((ArrayNode)genericsnode).Items.ConvertAll(x => ((string)(StringNode)((ObjectNode)x)["Name"], ReadType((ObjectNode)((ObjectNode)x)["Type"])));
        }

        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            for (int i = 0; i < Generics.Count; i++)
            {
                var x = func(Generics[i].Item2);
                Generics[i] = (Generics[i].Item1, func(Generics[i].Item2));
            }
        }
        public override string ToString()
        {
            if (Generics.Count == 0) return Base.ToString();
            var sb = new StringBuilder(Base.ToString());
            sb.Append("<");
            sb.Append(Generics[0].Item2.ToString());
            for (int i = 1; i < Generics.Count; i++)
            {
                sb.Append(", ");
                sb.Append(Generics[i].Item2.ToString());
            }
            sb.Append(">");
            return sb.ToString();
        }
    }
    public class FunctionType : Type
    {
        public Type ReturnType;
        public List<(Type type, string name, bool optional)> Parameters = new List<(Type, string, bool)>();
        public List<string> Generics = new List<string>();

        public override bool IsRefType => true;
        public override bool IsNullable => true;
        public override Value GetDefaultValue()
        {
            return new NullValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            if (t is FunctionType tt)
            {
                if (Generics.Count != tt.Generics.Count || Parameters.Count != tt.Parameters.Count) return false;
                if (Generics.Count == 0)
                {
                    return ((ReturnType == null && tt.ReturnType == null) || ReturnType.Equals(tt.ReturnType)) && Tools.ListEquals(Parameters, tt.Parameters);
                }

                bool typeEquals(Type x, Type y)
                {
                    if (x == null && y == null) return true;
                    if (x is GenericType g1 && y is GenericType g2)
                    {
                        int i1 = Generics.IndexOf(g1.Name);
                        if (i1 == -1) return false;
                        int i2 = tt.Generics.IndexOf(g1.Name);
                        return i2 != -1 && i1 == i2;
                    }
                    else return x.Equals(y);
                }

                if (!typeEquals(ReturnType, tt.ReturnType)) return false;
                for (int i = 0; i < Parameters.Count; i++)
                {
                    if (Parameters[i].optional != tt.Parameters[i].optional || !typeEquals(Parameters[i].type, tt.Parameters[i].type))
                        return false;
                }
                return true;
            }
            return false;
        }

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Function");
            if (ReturnType != null) node.Items.Add("ReturnType", ReturnType.WriteType());
            var arr = new ArrayNode();
            for (int i = 0; i < Parameters.Count; i++)
            {
                var obj = new ObjectNode();
                obj.Items.Add("Type", Parameters[i].type.WriteType());
                if (Parameters[i].name != null) obj.Items.Add("Name", (StringNode)Parameters[i].name);
                obj.Items.Add("Optional", (StringNode)Parameters[i].optional.ToString());
                arr.Items.Add(obj);
            }
            node.Items.Add("Parameters", arr);
            if (Generics.Count != 0)
            {
                var genericsarr = new ArrayNode();
                for (int i = 0; i < Generics.Count; i++)
                {
                    genericsarr.Items.Add((StringNode)Generics[i]);
                }
                node.Items.Add("Generics", genericsarr);
            }
        }
        public override void Read(ObjectNode node)
        {
            var returnTypeNode = node["ReturnType"];
            if (returnTypeNode != null) ReturnType = ReadType((ObjectNode)returnTypeNode);
            var paramsnode = (ArrayNode)node["Parameters"];
            for (int i = 0; i < paramsnode.Items.Count; i++)
            {
                var obj = (ObjectNode)paramsnode.Items[i];
                var namenode = obj["Name"];
                if (namenode != null) Parameters.Add((ReadType((ObjectNode)obj["Type"]), (StringNode)namenode, bool.Parse((StringNode)obj["Optional"])));
                else Parameters.Add((ReadType((ObjectNode)obj["Type"]), null, bool.Parse((StringNode)obj["Optional"])));
            }
            var genericsnode = node["Generics"];
            if (genericsnode != null)
            {
                var arr = (ArrayNode)genericsnode;
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    Generics.Add((StringNode)arr.Items[i]);
                }
            }
        }

        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            ReturnType = func(ReturnType);
            for (int i = 0; i < Parameters.Count; i++)
            {
                Parameters[i] = (func(Parameters[i].Item1), Parameters[i].Item2, Parameters[i].Item3);
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("ƒ ");
            if (Generics.Count != 0)
            {
                sb.Append("<");
                sb.Append(Generics[0]);
                for (int i = 1; i < Generics.Count; i++)
                {
                    sb.Append(", ");
                    sb.Append(Generics[i]);
                }
                sb.Append("> ");
            }
            sb.Append("(");
            
            void appendParam((Type, string, bool) x)
            {
                if (x.Item3) sb.Append("optional: ");
                sb.Append(x.Item1.ToString());
                if (x.Item2 != null)
                {
                    sb.Append(' ');
                    sb.Append(x.Item2);
                }
            }

            if(Parameters.Count != 0)
            {
                appendParam(Parameters[0]);
                for (int i = 1; i < Parameters.Count; i++)
                {
                    sb.Append(", ");
                    appendParam(Parameters[i]);
                }
            }
            sb.Append(")");
            if (ReturnType != null)
            {
                sb.Append(" => ");
                sb.Append(ReturnType.ToString());
            }
            return sb.ToString();
        }
    }
    public class RecordType : Type
    {
        public HashSet<(string name, Type type)> Fields = new HashSet<(string, Type)>();

        public override bool IsRefType => false;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            var recordVal = new RecordValue { Fields = new List<(string, Value)>(Fields.Count) };
            foreach(var x in Fields)
            {
                recordVal.Fields.Add((x.name, x.type.GetDefaultValue()));
            }
            return recordVal;
        }
        public override bool Equals(Type t)
        {
            return t is RecordType tt && Fields.SetEquals(tt.Fields);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Record");
            var fieldsnode = new ArrayNode();
            foreach (var (name, type) in Fields) fieldsnode.Items.Add(new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)name }, { "Type", type.WriteType() } }));
            node.Items.Add("Fields", fieldsnode);
        }
        public override void Read(ObjectNode node)
        {
            var fieldsnode = (ArrayNode)node["Fields"];
            for (int i = 0; i < fieldsnode.Items.Count; i++)
            {
                var obj = (ObjectNode)fieldsnode.Items[i];
                Fields.Add(((StringNode)obj["Name"], ReadType((ObjectNode)obj["Type"])));
            }
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            var newset = new HashSet<(string, Type)>();
            foreach(var x in Fields)
            {
                newset.Add((x.Item1, func(x.Item2)));
            }
            Fields = newset;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");
            bool notfirst = false;
            foreach (var x in Fields)
            {
                if (notfirst) sb.Append(", ");
                notfirst = true;
                sb.Append(x.type.ToString());
                sb.Append(' ');
                sb.Append(x.name);
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
    public class CollectionType : Type
    {
        public Type Base;

        public override bool IsRefType => true;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return new NullValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t is CollectionType tt && Base.Equals(tt.Base);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Collection");
            node.Items.Add("Base", Base.WriteType());
        }
        public override void Read(ObjectNode node)
        {
            Base = ReadType((ObjectNode)node["Base"]);
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            Base = func(Base);
        }
        public override string ToString()
        {
            return Base + "[]";
        }
    }
    public class MultiType : Type
    {
        public HashSet<Type> Types = new HashSet<Type>();

        public override bool IsRefType => true;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return new NullValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t is MultiType tt && Types.SetEquals(tt.Types);
        }
        public override void Write(ObjectNode node)
        {
            var typesnode = new ArrayNode();
            foreach (var x in Types) typesnode.Items.Add(x.WriteType());
            node.Items.Add("Types", typesnode);
        }
        public override void Read(ObjectNode node)
        {
            var typesnode = (ArrayNode)node["Types"];
            for (int i = 0; i < typesnode.Items.Count; i++)
            {
                Types.Add(ReadType((ObjectNode)typesnode.Items[i]));
            }
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            var newset = new HashSet<Type>();
            foreach(var x in Types)
            {
                newset.Add(func(x));
            }
            Types = newset;
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("(");
            bool notfirst = false;
            foreach (var x in Types)
            {
                if (notfirst) sb.Append(" | ");
                sb.Append(x.ToString());
                notfirst = true;
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
    public class NullableType : Type
    {
        public Type Base;
        public override bool IsRefType => Base.IsRefType;
        public override bool IsNullable => true;
        public override Value GetDefaultValue()
        {
            return new NullValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t is NullableType tt && Base.Equals(tt.Base);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Nullable");
            node.Items.Add("Base", Base.WriteType());
        }
        public override void Read(ObjectNode node)
        {
            Base = ReadType((ObjectNode)node["Base"]);
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
            Base = func(Base);
        }
        public override string ToString()
        {
            return Base.ToString() + "?";
        }
    }
    public class GenericType : Type
    {
        public string Name;

        public override bool IsRefType => false;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return new DefaultValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t is GenericType tt && tt.Name == Name;

        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Generic");
            node.Items.Add("Name", (StringNode)Name);
        }
        public override void Read(ObjectNode node)
        {
            Name = (StringNode)node["Name"];
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
        }
        public override string ToString()
        {
            return "generic: " + Name;
        }
    }
    public class ObjectType : Type
    {
        public static ObjectType Value = new ObjectType();

        private ObjectType() { }

        public override bool IsRefType => true;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return new NullValue { Type = this };
        }
        public override bool Equals(Type t)
        {
            return t == Value;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Object");
        }
        public override void Read(ObjectNode node)
        {
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
        }
        public override string ToString()
        {
            return "object";
        }
    }
    public class BooleanType : Type
    {
        public static BooleanType Value = new BooleanType();

        private BooleanType() { }

        public override bool IsRefType => false;
        public override bool IsNullable => false;
        public override Value GetDefaultValue()
        {
            return LiteralValue.False;
        }
        public override bool Equals(Type t)
        {
            return t == Value;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Boolean");
        }
        public override void Read(ObjectNode node)
        {
        }
        public override void ReplaceAllSubtypes(Func<Type, Type> func)
        {
        }
        public override string ToString()
        {
            return "bool";
        }
    }

    public abstract class Value
    {
        protected Type _type;
        public abstract Type Type { get; set; }

        public abstract void Write(ObjectNode node);
        public abstract void Read(ObjectNode node);

        public ObjectNode WriteValue()
        {
            var node = new ObjectNode();
            Write(node);
            return node;
        }
        public static Value ReadValue(ObjectNode node)
        {
            Value val = null;
            switch ((StringNode)node["Kind"])
            {
                case "Local": val = new LocalValue(); break;
                case "Symbol": val = new SymbolValue(); break;
                case "Call": val = new CallValue(); break;
                case "Operation": val = new OperationValue(); break;
                case "Procedural": val = new ProceduralValue(); break;
                case "Convert": val = new ConvertValue(); break;
                case "Function": val = new FunctionValue(); break;
                case "Record": val = new RecordValue(); break;
                case "Collection": val = new CollectionValue(); break;
                case "Member": val = new MemberValue(); break;
                case "New": val = new NewValue(); break;
                case "Boxed": val = new BoxedValue(); break;
                case "Literal": val = new LiteralValue(); break;
                case "This": val = ThisValue.Typeless; break;
                case "Default": val = new DefaultValue(); break;
                case "Null": val = new NullValue(); break;
            }
            val.Read(node);
            return val;
        }
    }
    public class LocalValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public string Name;

        public bool Equals(Value v)
        {
            return v is LocalValue vv && vv.Name == Name;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Local");
            node.Items.Add("Type", Type.WriteType());
            node.Items.Add("Name", (StringNode)Name);
        }
        public override void Read(ObjectNode node)
        {
            Type = Type.ReadType((ObjectNode)node["Type"]);
            Name = (StringNode)node["Name"];
        }
        public override string ToString()
        {
            return Name;
        }
    }
    public class SymbolValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    Symbol s = Symbol;
                    _type = s is VariableSymbol v ? v.Type : ((PropertySymbol)s).Type;
                }
                return _type;
            }
            set => _type = value;
        }
        public Symbol Symbol;
        public Value Instance;

        public bool Equals(Value v)
        {
            return v is SymbolValue vv && vv.Symbol == Symbol;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Symbol");
            node.Items.Add("Symbol", (StringNode)Symbol.ToString());
            if (Instance != null) node.Items.Add("Instance", Instance.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Symbol = Project.Current.GetSymbol((StringNode)node["Symbol"]);
            var instancenode = (ObjectNode)node["Instance"];
            if (instancenode != null) Instance = ReadValue(instancenode);
        }
        public override string ToString()
        {
            return Symbol.ToString();
        }
    }
    public class CallValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null) _type = ((FunctionType)Function.Type).ReturnType;
                return _type;
            }
            set
            {
                _type = value;
            }
        }
        public Value Function;
        public List<Value> Parameters = new List<Value>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Call");
            node.Items.Add("Function", Function.WriteValue());
            node.Items.Add("Parameters", new ArrayNode(Parameters.ConvertAll(x => (Node)x.WriteValue())));
        }
        public override void Read(ObjectNode node)
        {
            Function = ReadValue((ObjectNode)node["Function"]);
            Parameters = ((ArrayNode)node["Parameters"]).Items.ConvertAll(x => ReadValue((ObjectNode)x));
        }
        public override string ToString()
        {
            var sb = new StringBuilder(Function.ToString());
            sb.Append("(");

            if (Parameters.Count != 0)
            {
                sb.Append(Parameters[0].ToString());
                for (int i = 1; i < Parameters.Count; i++)
                {
                    sb.Append(", ");
                    sb.Append(Parameters[i].ToString());
                }
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
    public enum OperationType
    {
        Is,
        IsNot,
        IsNull,
        Not,
        And,
        Or,
        Length,
        Iterate,
        Value,
    }
    public class OperationValue : Value
    {
        Type t;
        public override Type Type
        {
            get
            {
                switch (OperationType)
                {
                    case OperationType.Is:
                    case OperationType.IsNot:
                    case OperationType.IsNull:
                    case OperationType.Not:
                    case OperationType.And:
                    case OperationType.Or:
                        return BooleanType.Value;
                    case OperationType.Length:
                        return LiteralValue.IntegerLiteralType;
                    case OperationType.Iterate:
                        return null;
                    case OperationType.Value:
                        return (Values[0].Type as NullableType).Base;
                }
                return t;
            }
            set => t = value;
        }

        public OperationType OperationType;
        public List<Value> Values = new List<Value>();
        public List<Type> Types = new List<Type>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Operation");
            node.Items.Add("Operation", (StringNode)OperationType.ToString());
            if (Values.Count != 0) node.Items.Add("Values", new ArrayNode(Values.ConvertAll(x => (Node)x.WriteValue())));
            if (Types.Count != 0) node.Items.Add("Types", new ArrayNode(Types.ConvertAll(x => (Node)x.WriteType())));
        }
        public override void Read(ObjectNode node)
        {
            OperationType = (OperationType)Enum.Parse(typeof(OperationType), (StringNode)node["Operation"]);
            var valuesnode = node["Values"];
            if (valuesnode != null) Values = ((ArrayNode)valuesnode).Items.ConvertAll(x => ReadValue((ObjectNode)x));
            var typesnode = node["Types"];
            if (typesnode != null) Types = ((ArrayNode)typesnode).Items.ConvertAll(x => Type.ReadType((ObjectNode)x));
        }
        public override string ToString()
        {
            switch (OperationType)
            {
                default: return string.Empty;
                case OperationType.Is: return $"{Values[0]} is {(Values.Count == 1 ? Types[0].ToString() : Values[1].ToString())}";
                case OperationType.IsNot: return $"{Values[0]} is not {(Values.Count == 1 ? Types[0].ToString() : Values[1].ToString())}";
                case OperationType.Not: return $"not {Values[0]}";
                case OperationType.And: return $"{Values[0]} and {Values[1]}";
                case OperationType.Or: return $"{Values[0]} or {Values[1]}";
            }
        }
    }
    public class ProceduralValue : Value
    {
        public Type t;
        public override Type Type
        {
            get => t;
            set => t = value;
        }
        public Instruction Instruction;

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Procedural");
            if (Type != null) node.Items.Add("Type", Type.WriteType());
            node.Items.Add("Instruction", Instruction.WriteInstruction());
        }
        public override void Read(ObjectNode node)
        {
            var typenode = node["Type"];
            if (typenode != null) Type = Type.ReadType((ObjectNode)typenode);
            Instruction = Instruction.ReadInstruction((ObjectNode)node["Instruction"]);
        }

        public override string ToString()
        {
            return "{...}";
        }
    }
    public class ConvertValue : Value
    {
        public Type t;
        public override Type Type
        {
            get => t;
            set => t = value;
        }
        public Value Base;

        public bool Equals(Value v)
        {
            return v is ConvertValue vv && vv.Base.Equals(Base);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Convert");
            node.Items.Add("Type", Type.WriteType());
            node.Items.Add("Base", Base.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Type = Type.ReadType((ObjectNode)node["Type"]);
            Base = ReadValue((ObjectNode)node["Base"]);
        }
        public override string ToString()
        {
            return Base.ToString() + " to " + Type.ToString();
        }
    }
    public class FunctionValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public Value Value;
        public List<(string, Value)> Parameters = new List<(string, Value)>();
        public List<string> Generics = new List<string>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Function");
            node.Items.Add("Type", Type.WriteType());
            if (Generics.Count != 0) node.Items.Add("Generics", new ArrayNode(Generics.ConvertAll(x => (Node)(StringNode)x)));
            var paramsnode = new ArrayNode();
            for (int i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i].Item2 == null) paramsnode.Items.Add(new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)Parameters[i].Item1 } }));
                else paramsnode.Items.Add(new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)Parameters[i].Item1 }, { "DefaultValue", Parameters[i].Item2.WriteValue() } }));
            }
            node.Items.Add("Parameters", paramsnode);
            node.Items.Add("Value", Value.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Type = Type.ReadType((ObjectNode)node["Type"]);
            var genericsnode = node["Generics"];
            if (genericsnode != null) Generics = ((ArrayNode)genericsnode).Items.ConvertAll(x => (string)(StringNode)x);
            var paramsnode = (ArrayNode)node["Parameters"];
            for (int i = 0; i < paramsnode.Items.Count; i++)
            {
                var obj = (ObjectNode)paramsnode.Items[i];
                if (obj.Items.ContainsKey("DefaultValue")) Parameters.Add(((StringNode)obj["Name"], ReadValue((ObjectNode)obj["DefaultValue"])));
                else Parameters.Add(((StringNode)obj["Name"], null));
            }
            Value = ReadValue((ObjectNode)node["Value"]);
        }
        public override string ToString()
        {
            return "ƒ";
        }
    }
    public class RecordValue : Value
    {
        public override Type Type
        {
            get
            {
                if(_type == null)
                {
                    var l = new HashSet<(string, Type)>();
                    for (int i = 0; i < Fields.Count; i++)
                    {
                        l.Add((Fields[i].Item1, Fields[i].Item2.Type));
                    }
                    _type = new RecordType { Fields = l };
                }
                return _type;
            }
            set => _type = value;
        }
        public List<(string, Value)> Fields = new List<(string, Value)>();

        public bool Equals(Value v)
        {
            return v is RecordValue vv && Tools.ListEquals(vv.Fields, Fields);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Record");
            node.Items.Add("Fields", new ArrayNode(Fields.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "Name", (StringNode)x.Item1 }, { "Value", x.Item2.WriteValue() } }))));
        }
        public override void Read(ObjectNode node)
        {
            Fields = ((ArrayNode)node["Fields"]).Items.ConvertAll(x => ((string)(StringNode)((ObjectNode)x)["Name"], ReadValue((ObjectNode)((ObjectNode)x)["Value"])));
        }
        public override string ToString()
        {
            var sb = new StringBuilder("[");

            void WriteField(int i)
            {
                sb.Append(Fields[i].Item1);
                sb.Append(" = ");
                sb.Append(Fields[i].Item2);
            }

            WriteField(0);
            for (int i = 1; i < Fields.Count; i++)
            {
                sb.Append(", ");
                WriteField(i);
            }
            return sb.ToString();
        }
    }
    public class CollectionValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public List<Value> Values = new List<Value>();
        public Value LengthValue => new LiteralValue { LiteralType = LiteralValueType.Integer, Value = Convert.ToString(Values.Count, 16) };

        public bool Equals(Value v)
        {
            return v is CollectionValue vv && Tools.ListEquals(vv.Values, Values);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Collection");
            node.Items.Add("Values", new ArrayNode(Values.ConvertAll(x => (Node)x.WriteValue())));
        }
        public override void Read(ObjectNode node)
        {
            Values = ((ArrayNode)node["Values"]).Items.ConvertAll(x => ReadValue((ObjectNode)x));
        }
        public override string ToString()
        {
            var sb = new StringBuilder("[");
            if (Values.Count != 0)
            {
                sb.Append(Values[0].ToString());
                for (int i = 1; i < Values.Count; i++)
                {
                    sb.Append(", ");
                    sb.Append(Values[i].ToString());
                }
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
    public class MemberValue : Value
    {
        public override Type Type
        {
            get;
            set;
        }
        public Value Base;
        public string Name;

        public bool Equals(Value v)
        {
            return v is MemberValue vv && vv.Base.Equals(Base) && vv.Name == Name;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Member");
            node.Items.Add("Base", Base.WriteValue());
            node.Items.Add("Name", (StringNode)Name);
        }
        public override void Read(ObjectNode node)
        {
            Base = ReadValue((ObjectNode)node["Base"]);
            Name = (StringNode)node["Name"];
        }
        public override string ToString()
        {
            return Base.ToString() + "." + Name;
        }
    }
    public class NewValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public List<(Symbol, Value)> FieldValues = new List<(Symbol, Value)>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"New");
            node.Items.Add("Type", Type.WriteType());
            if (FieldValues.Count != 0) node.Items.Add("FieldValues", new ArrayNode(FieldValues.ConvertAll(x => (Node)new ObjectNode(new Dictionary<string, Node> { { "Symbol", (StringNode)x.Item1.ToString() }, { "Value", x.Item2.WriteValue() } }))));
        }
        public override void Read(ObjectNode node)
        {
            var fieldvaluesnode = node["FieldValues"];
            Type = Type.ReadType((ObjectNode)node["Type"]);
            if (fieldvaluesnode != null) FieldValues = ((ArrayNode)fieldvaluesnode).Items.ConvertAll(x => (Project.Current.GetSymbol((StringNode)((ObjectNode)x)["Symbol"]), ReadValue((ObjectNode)((ObjectNode)x)["Value"])));
        }
        public override string ToString()
        {
            var sb = new StringBuilder("new ");
            sb.Append(Type.ToString());
            if (FieldValues.Count != 0)
            {
                sb.Append(" { ");
                for (int i = 0; i < FieldValues.Count; i++)
                {
                    sb.Append(FieldValues[i].Item1);
                    sb.Append(" = ");
                    sb.Append(FieldValues[i].Item2.ToString());
                }
                sb.Append("}");
            }
            return sb.ToString();
        }
    }
    public class BoxedValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }
        public Value Base;

        public bool Equals(Value v)
        {
            return v is BoxedValue vv && vv.Base.Equals(Base);
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Boxed");
            node.Items.Add("Base", Base.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Base = ReadValue((ObjectNode)node["Base"]);
        }
        public override string ToString()
        {
            return "(" + Base.ToString() + ")?";
        }
    }
    public enum LiteralValueType
    {
        String,
        Integer,
        Fractional,
        Boolean,
    }
    public class LiteralValue : Value
    {
        public static LiteralValue True = new LiteralValue { LiteralType = LiteralValueType.Boolean, Value = "True" };
        public static LiteralValue False = new LiteralValue { LiteralType = LiteralValueType.Boolean, Value = "False" };

        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    switch (LiteralType)
                    {
                        case LiteralValueType.String: _type = StringLiteralType; break;
                        case LiteralValueType.Integer: _type = IntegerLiteralType; break;
                        case LiteralValueType.Fractional: _type = FractionalLiteralType; break;
                        case LiteralValueType.Boolean: _type = BooleanType.Value; break;
                    }
                }
                return _type;
            }
            set => _type = value;
        }

        public static Type StringLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("String", ObjectType.Value)); return t; })();
        public static Type IntegerLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("Integer", ObjectType.Value)); return t; })();
        public static Type FractionalLiteralType = new Func<Type>(() => { var t = new RecordType(); t.Fields.Add(("Numerator", ObjectType.Value)); t.Fields.Add(("Denominator", ObjectType.Value)); return t; })();

        public LiteralValueType LiteralType;
        public string Value;

        public bool Equals(Value v)
        {
            return v is LiteralValue vv && vv.Value == Value;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Literal");
            node.Items.Add("LiteralType", (StringNode)LiteralType.ToString());
            node.Items.Add("Value", (StringNode)Value);
        }
        public override void Read(ObjectNode node)
        {
            LiteralType = (LiteralValueType)Enum.Parse(typeof(LiteralValueType), (StringNode)node["LiteralType"]);
            Value = (StringNode)node["Value"];
        }
        public override string ToString()
        {
            switch (LiteralType)
            {
                case LiteralValueType.String:
                    return "\"" + Value + "\"";
                case LiteralValueType.Integer:
                case LiteralValueType.Fractional:
                    return Value;
                case LiteralValueType.Boolean:
                    return bool.Parse(Value) ? "true" : "false";
            }
            return Value;
        }
    }
    public class ThisValue : Value
    {
        public static ThisValue Typeless = new ThisValue();

        public override Type Type
        {
            get => _type;
            set => _type = value;
        }

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"This");
        }
        public override void Read(ObjectNode node)
        {
        }
        public override string ToString()
        {
            return "this";
        }
    }
    public class DefaultValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Default");
            node.Items.Add("Type", Type.WriteType());
        }
        public override void Read(ObjectNode node)
        {
            Type = Type.ReadType((ObjectNode)node["Type"]);
        }
        public override string ToString()
        {
            return "default: " + Type.ToString();
        }
    }
    public class NullValue : Value
    {
        public override Type Type
        {
            get => _type;
            set => _type = value;
        }

        public static readonly NullValue ObjectNull = new NullValue { Type = ObjectType.Value };

        public bool Equals(Value v)
        {
            return v is NullValue;
        }
        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Null");
        }
        public override void Read(ObjectNode node)
        {
        }
        public override string ToString()
        {
            return "null";
        }
    }

    public abstract class Instruction
    {
        public abstract void Write(ObjectNode node);
        public abstract void Read(ObjectNode node);

        public string File;
        public uint Line, Index;

        public ObjectNode WriteInstruction()
        {
            var node = new ObjectNode();
            Write(node);
            if (File != null) node.Items.Add("File", (StringNode)File);
            if (Line != 0) node.Items.Add("Line", (StringNode)Line.ToString());
            if (Index != 0) node.Items.Add("Index", (StringNode)Index.ToString());
            return node;
        }
        public static Instruction ReadInstruction(ObjectNode node)
        {
            Instruction inst = null;
            switch ((StringNode)node["Kind"])
            {
                case "No-op": inst = NoOpInstruction.Value; break;
                case "Scope": inst = new ScopeInstruction(); break;
                case "Control": inst = new ControlInstruction(); break;
                case "Action": inst = new ActionInstruction(); break;
                case "Return": inst = new ReturnInstruction(); break;
                case "Assign": inst = new AssignInstruction(); break;
                case "If": inst = new IfInstruction(); break;
                case "Else": inst = new ElseInstruction(); break;
                case "Throw": inst = new ThrowInstruction(); break;
                case "Catch": inst = new CatchInstruction(); break;
            }
            var filenode = node["File"];
            var linenode = node["Line"];
            var indexnode = node["Index"];
            if (filenode != null) inst.File = (StringNode)filenode;
            if (linenode != null) inst.Line = uint.Parse((StringNode)linenode);
            if (indexnode != null) inst.Index = uint.Parse((StringNode)indexnode);
            inst.Read(node);
            return inst;
        }
    }
    public class NoOpInstruction : Instruction
    {
        public static NoOpInstruction Value = new NoOpInstruction();

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"No-op");
        }
        public override void Read(ObjectNode node)
        {
        }
    }
    public class ScopeInstruction : Instruction
    {
        public string Name;
        public List<Instruction> Instructions = new List<Instruction>();

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Scope");
            node.Items.Add("Instructions", new ArrayNode(Instructions.ConvertAll(x => (Node)x.WriteInstruction())));
        }
        public override void Read(ObjectNode node)
        {
            Instructions = ((ArrayNode)node["Instructions"]).Items.ConvertAll(x => ReadInstruction((ObjectNode)x));
        }
    }
    public enum ControlInstructionType
    {
        Break,
        Continue,
    }
    public class ControlInstruction : Instruction
    {
        public string Name;
        public ControlInstructionType Type;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Control");
            if (Name != null) node.Items.Add("Name", (StringNode)Name);
            node.Items.Add("Type", (StringNode)Type.ToString());
        }
        public override void Read(ObjectNode node)
        {
            var namenode = node["Name"];
            if (namenode != null) Name = (StringNode)node["Control"];
            Type = (ControlInstructionType)Enum.Parse(typeof(ControlInstructionType), (StringNode)node["Type"]);
        }
    }
    public class ActionInstruction : Instruction
    {
        public Value Value;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Action");
            node.Items.Add("Value", Value.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Value = Value.ReadValue((ObjectNode)node["Value"]);
        }
    }
    public class ReturnInstruction : Instruction
    {
        public Value Value;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Return");
            node.Items.Add("Value", Value.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Value = Value.ReadValue((ObjectNode)node["Value"]);
        }
    }
    public class AssignInstruction : Instruction
    {
        public Value Left;
        public Value Right;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Assign");
            node.Items.Add("Left", Left.WriteValue());
            node.Items.Add("Right", Right.WriteValue());
        }
        public override void Read(ObjectNode node)
        {
            Left = Value.ReadValue((ObjectNode)node["Left"]);
            Right = Value.ReadValue((ObjectNode)node["Right"]);
        }
    }
    public class IfInstruction : Instruction
    {
        public Value Condition;
        public Instruction Instruction;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"If");
            node.Items.Add("Condition", Condition.WriteValue());
            node.Items.Add("Instruction", Instruction.WriteInstruction());
        }
        public override void Read(ObjectNode node)
        {
            Condition = Value.ReadValue((ObjectNode)node["Condition"]);
            Instruction = ReadInstruction((ObjectNode)node["Instruction"]);
        }
    }
    public class ElseInstruction : Instruction
    {
        public Instruction Instruction;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Else");
            node.Items.Add("Instruction", Instruction.WriteInstruction());
        }
        public override void Read(ObjectNode node)
        {
            Instruction = ReadInstruction((ObjectNode)node["Instruction"]);
        }
    }
    public class ThrowInstruction : Instruction
    {
        public string Exception;
        public string Message;

        public override void Write(ObjectNode node)
        {
            node.Items.Add("Kind", (StringNode)"Throw");
            node.Items.Add("Exception", (StringNode)Exception);
            node.Items.Add("Message", (StringNode)Message);
        }
        public override void Read(ObjectNode node)
        {
            Exception = (StringNode)node["Exception"];
            Message = (StringNode)node["Message"];
        }
    }
    public class CatchInstruction : Instruction
    {
        public List<string> Exceptions;
        public Instruction Instruction;

        public override void Write(ObjectNode node)
        {
        }
        public override void Read(ObjectNode node)
        {
        }
    }

    public static class Tools
    {
        public static bool ListEquals<T>(List<T> x, List<T> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
            {
                if (!Equals(x[i], y[i])) return false;
            }
            return true;
        }
        public static void ProcessConfigName(string name, out string packagename, out string rest)
        {
            var nspacei = name.IndexOf("::");
            if (nspacei == -1)
            {
                packagename = null;
                rest = name;
            }
            else
            {
                packagename = name.Remove(nspacei);
                rest = name.Substring(nspacei + 2);
            }
        }
        public static void ProcessSymbolName(string name, out string packagename, out string[] parts)
        {
            ProcessConfigName(name, out packagename, out var rest);
            parts = rest.Split('.');
        }
        public static Symbol MatchSymbol(string[] names, List<Symbol> symbols)
        {
            Symbol s = null;
            for (int i = 0; i < names.Length; i++)
            {
                if (symbols == null) return s;
                for (int i2 = 0; i2 < symbols.Count; i2++)
                {
                    if (symbols[i2].Name == names[i])
                    {
                        s = symbols[i2];
                        symbols = s.Children;
                        break;
                    }
                }
            }
            return s;
        }
    }
}
