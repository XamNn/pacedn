using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

//Common pace library for c# which implements data structure for reading, writing, and interpreting pace packages

namespace Pace.CommonLibrary
{
    public static class Info
    {
        public static string Version = "pacednl 0.2.1";
    }

    public static class Settings
    {
        public static string PackageDirectory = null;
        public static string PackageFileExtention = ".pacep";

        public static string FormatPackageFilename(string s, bool checkexists)
        {
            if (File.Exists(s)) return s;
            return PackageDirectory + "\\" + s + PackageFileExtention;
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

    public class Project
    {
        public static Project Current = new Project();

        public List<Symbol> Symbols = new List<Symbol>();
        public List<(Type, Type, Value)> Convertions = new List<(Type, Type, Value)>();
        public List<Config> Configs = new List<Config>();
        public Dictionary<string, Package> Packages = new Dictionary<string, Package>();
        public Value EntryPoint;
        public Dictionary<string, string> DataBank = new Dictionary<string, string>();

        public Package Merge()
        {
            Packages.Clear();
            var x = new Package();
            x.Name = "MergedPackage";
            Packages.Add(x.Name, x);
            x.Symbols = Symbols;
            return x;
        }
        public Symbol GetSymbol(string match)
        {
            var parts = match.Split('.');
            return Tools.MatchSymbol(parts, Symbols);
        }
        public OperationResult Import(string name, string localPath)
        {
            if (Packages.ContainsKey(name)) return OperationResult.Success;
            var file = Settings.FormatPackageFilename(name, true);
            var p = new Package();
            var result = p.Read(file);
            if (!result.IsSuccessful) return result;
            return Import(p);
        }
        public OperationResult Import(Package l)
        {
            if(l.EntryPoint != null)
            {
                if (EntryPoint == null) EntryPoint = l.EntryPoint;
                else return new OperationResult("Entry point already defined");
            }
            for (int i = 0; i < Symbols.Count; i++)
            {
                for (int i2 = 0; i2 < l.Symbols.Count; i2++)
                {
                    if (Symbols[i].Name == l.Symbols[i2].Name)
                        return new OperationResult("Package contains symbols already defined");
                }
            }
            for (int i = 0; i < Convertions.Count; i++)
            {
                for (int i2 = 0; i2 < l.Convertions.Count; i2++)
                {
                    if (Convertions[i].Item1.Equals(l.Convertions[i].Item1) && Convertions[i].Item2.Equals(l.Convertions[i2].Item2))
                    {
                        return new OperationResult("Package contains convertions already defined");
                    }
                }
            }
            for (int i = 0; i < Configs.Count; i++)
            {
                for (int i2 = 0; i2 < l.Configs.Count; i2++)
                {
                    if (Configs[i].Name == l.Configs[i].Name)
                    {
                        return new OperationResult("Package contains configs already defined");
                    }
                }
            }
            for (int i = 0; i < l.Dependencies.Count; i++)
            {
                var res = Import(l.Dependencies[i], null);
            }
            Symbols.AddRange(l.Symbols);
            Convertions.AddRange(l.Convertions);
            Configs.AddRange(l.Configs);
            Packages.Add(l.Name, l);
            foreach(var x in l.DataBank)
            {
                if (!DataBank.ContainsKey(x.Key)) DataBank.Add(x.Key, x.Value);
            }
            return OperationResult.Success;
        }
        public Project Clone()
        {
            return new Project { Packages = new Dictionary<string, Package>(Packages), Symbols = new List<Symbol>(Symbols), Convertions = new List<(Type, Type, Value)>(Convertions), Configs = new List<Config>(Configs), EntryPoint = EntryPoint };
        }
    }

    public enum ConvertionType
    {
        Implicit,
        Explicit,
        Automatic
    }
    public class Package
    {
        public string Name;
        public List<Symbol> Symbols = new List<Symbol>();
        public List<(Type, Type, Value)> Convertions = new List<(Type, Type, Value)>();
        public List<string> Dependencies = new List<string>();
        public List<Config> Configs = new List<Config>();
        public Dictionary<string, string> DataBank = new Dictionary<string, string>();

        public Value EntryPoint;

        public void Save(string file)
        {
            if (!Directory.Exists(Settings.PackageDirectory)) Directory.CreateDirectory(Settings.PackageDirectory);
            XmlWriter xml = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
            xml.WriteStartElement("PacePackage");
            for (int i = 0; i < Dependencies.Count; i++)
            {
                xml.WriteStartElement("Dependency");
                xml.WriteAttributeString("Name", Dependencies[i]);
                xml.WriteEndElement();
            }
            if (EntryPoint != null)
            {
                EntryPoint.Write(xml);
            }
            if (Symbols.Count != 0)
            {
                xml.WriteStartElement("Symbols");
                for (int i = 0; i < Symbols.Count; i++)
                {
                    Symbols[i].Write(xml);
                }
                xml.WriteFullEndElement();
            }
            if (Convertions.Count != 0)
            {
                xml.WriteStartElement("Convertions");
                foreach(var x in Convertions)
                {
                    xml.WriteStartElement("Convertion");
                    xml.WriteStartElement("From");
                    x.Item1.Write(xml);
                    xml.WriteEndElement();
                    xml.WriteStartElement("To");
                    x.Item2.Write(xml);
                    xml.WriteEndElement();
                    x.Item3.Write(xml);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            if (Configs.Count != 0)
            {
                xml.WriteStartElement("Configs");
                for (int i = 0; i < Configs.Count; i++)
                {
                    Configs[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            if (DataBank.Count != 0)
            {
                xml.WriteStartElement("DataBank");
                foreach(var x in DataBank)
                {
                    xml.WriteElementString(x.Key, x.Value);
                }
                xml.WriteEndElement();
            }
            xml.WriteFullEndElement();
            xml.Close();
        }
        
        public OperationResult Read(string file)
        {
            if (!File.Exists(file)) return new OperationResult("File does not exist");
            XmlReader xml = XmlReader.Create(file);
            Name = Path.GetFileNameWithoutExtension(file);
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Element)
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            //try
                            {
                                switch (xml.LocalName)
                                {
                                    case "Dependency":
                                        Dependencies.Add(xml.GetAttribute("Name"));
                                        break;
                                    case "Value":
                                        EntryPoint = Value.ReadValue(xml);
                                        break;
                                    case "Symbols":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element)
                                            {
                                                Symbols.Add(Symbol.ReadSymbol(xml, null));
                                            }
                                        }
                                        break;
                                    case "Convertions":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Convertion")
                                            {
                                                Type from = null, to = null;
                                                Value value = null;
                                                while (xml.Read())
                                                {
                                                    if (xml.NodeType == XmlNodeType.EndElement) break;
                                                    if (xml.NodeType == XmlNodeType.Element)
                                                    {
                                                        if (xml.LocalName == "From")
                                                        {
                                                            while(xml.Read())
                                                            {
                                                                if (xml.NodeType == XmlNodeType.EndElement) break;
                                                                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                                                                {
                                                                    from = Type.ReadType(xml);
                                                                } 
                                                            }
                                                        }
                                                        else if (xml.LocalName == "To")
                                                        {
                                                            while (xml.Read())
                                                            {
                                                                if (xml.NodeType == XmlNodeType.EndElement) break;
                                                                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                                                                {
                                                                    to = Type.ReadType(xml);
                                                                }
                                                            }
                                                        }
                                                        else if (xml.LocalName == "Value")
                                                        {
                                                            value = Value.ReadValue(xml);
                                                        }
                                                    }
                                                }
                                                Convertions.Add((from, to, value));
                                            }
                                        }
                                        break;
                                    case "Configs":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Config")
                                            {
                                                Configs.Add(Config.Read(xml));
                                            }
                                        }
                                        break;
                                    case "DataBank":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element)
                                            {
                                                DataBank.Add(xml.LocalName, xml.ReadElementContentAsString());
                                            }
                                        }
                                        break;
                                }
                            }
                            //catch (Exception e)
                            //{
                            //    return new OperationResult("Cannot import package: " + e.Message);
                            //}
                        }
                    }
                }
            }
            xml.Close();
            return OperationResult.Success;
        }
    }
    public class Config
    {
        public string Name;
        public Dictionary<string, object> Aliases = new Dictionary<string, object>();
        public List<(string, Type, Value)> UnaryOperators = new List<(string, Type, Value)>();
        public List<(Type, string, Type, Value)> BinaryOperators = new List<(Type, string, Type, Value)>();
        public List<(Type, Type, ConvertionType)> ConvertionTypes = new List<(Type, Type, ConvertionType)>();
        public List<string> Configs = new List<string>();

        public void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Config");
            xml.WriteAttributeString("Name", Name);
            foreach(var x in Aliases)
            {
                xml.WriteStartElement("Alias");
                xml.WriteAttributeString("Name", x.Key);
                if (x.Value is string symbol) xml.WriteAttributeString("Symbol", symbol);
                else if (x.Value is Value value) value.Write(xml);
                else if (x.Value is Type type) type.Write(xml);
                xml.WriteEndElement();
            }
            for (int i = 0; i < UnaryOperators.Count; i++)
            {
                xml.WriteStartElement("UnaryOperator");
                xml.WriteAttributeString("Operator", UnaryOperators[i].Item1);
                UnaryOperators[i].Item2.Write(xml);
                UnaryOperators[i].Item3.Write(xml);
                xml.WriteEndElement();
            }
            for (int i = 0; i < BinaryOperators.Count; i++)
            {
                xml.WriteStartElement("BinaryOperator");
                xml.WriteAttributeString("Operator", BinaryOperators[i].Item2);
                BinaryOperators[i].Item1.Write(xml);
                BinaryOperators[i].Item3.Write(xml);
                BinaryOperators[i].Item4.Write(xml);
                xml.WriteEndElement();
            }
            for (int i = 0; i < ConvertionTypes.Count; i++)
            {
                xml.WriteStartElement("ConvertionType");
                xml.WriteAttributeString("Type", ConvertionTypes[i].Item3.ToString());
                xml.WriteStartElement("From");
                ConvertionTypes[i].Item1.Write(xml);
                xml.WriteEndElement();
                xml.WriteStartElement("To");
                ConvertionTypes[i].Item2.Write(xml);
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
            for (int i = 0; i < Configs.Count; i++)
            {
                xml.WriteStartElement("Use");
                xml.WriteAttributeString("Config", Configs[i]);
                xml.WriteEndElement();
            }
            xml.WriteFullEndElement();
        }
        public static Config Read(XmlReader xml)
        {
            var x = new Config { Name = xml.GetAttribute("Name") };
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Alias")
                    {
                        string name = xml.GetAttribute("Name");
                        string symbolname = xml.GetAttribute("Symbol");
                        if (symbolname != null) x.Aliases.Add(name, symbolname);
                        else
                        {
                            while (xml.Read())
                            {
                                if (xml.NodeType == XmlNodeType.EndElement) break;
                                if (xml.NodeType == XmlNodeType.Element)
                                {
                                    if (xml.LocalName == "Value")
                                    {
                                        x.Aliases.Add(name, Value.ReadValue(xml));
                                    }
                                    else if (xml.LocalName == "Type")
                                    {
                                        x.Aliases.Add(name, Type.ReadType(xml));
                                    }
                                }
                            }
                        }
                    }
                    else if (xml.LocalName == "UnaryOperator")
                    {
                        string op = xml.GetAttribute("Operator");
                        Type type = null;
                        Value value = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if (xml.LocalName == "Type")
                                {
                                    type = Type.ReadType(xml);
                                }
                                else if (xml.LocalName == "Value")
                                {
                                    value = Value.ReadValue(xml);
                                }
                            }
                        }
                        x.UnaryOperators.Add((op, type, value));
                    }
                    else if (xml.LocalName == "BinaryOperator")
                    {
                        string op = xml.GetAttribute("Operator");
                        Type type1 = null;
                        Type type2 = null;
                        Value value = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if (xml.LocalName == "Type")
                                {
                                    if (type1 == null)
                                    {
                                        type1 = Type.ReadType(xml);
                                    }
                                    else if (type2 == null)
                                    {
                                        type2 = Type.ReadType(xml);
                                    }
                                }
                                else if(xml.LocalName == "Value")
                                {
                                    value = Value.ReadValue(xml);
                                }
                            }
                        }
                        x.BinaryOperators.Add((type1, op, type2, value));
                    }
                    else if (xml.LocalName == "ConvertionType")
                    {
                        var type = (ConvertionType)Enum.Parse(typeof(ConvertionType), xml.GetAttribute("Type"));
                        Type from = null;
                        Type to = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if (xml.LocalName == "From")
                                {
                                    while (xml.Read())
                                    {
                                        if (xml.NodeType == XmlNodeType.EndElement) break;
                                        if (xml.NodeType == XmlNodeType.Element)
                                        {
                                            from = Type.ReadType(xml);
                                        }
                                    }
                                }
                                else if (xml.LocalName == "To")
                                {
                                    while (xml.Read())
                                    {
                                        if (xml.NodeType == XmlNodeType.EndElement) break;
                                        if (xml.NodeType == XmlNodeType.Element)
                                        {
                                            to = Type.ReadType(xml);
                                        }
                                    }
                                }
                            }
                        }
                        x.ConvertionTypes.Add((from, to, type));
                    }
                    else if (xml.LocalName == "Use")
                    {
                        x.Configs.Add(xml.GetAttribute("Config"));
                    }
                }
            }
            return x;
        }
    }

    public abstract class Symbol
    {
        public string Name;
        public string Parent;
        public abstract List<Symbol> Children { get; }
        public Dictionary<string, string> Attributes = new Dictionary<string, string>();

        public abstract void Write(XmlWriter xml);
        protected abstract void Read(XmlReader xml);

        protected void WriteCommonStuff(XmlWriter xml)
        {
            xml.WriteAttributeString("Name", Name);
            if (Attributes.Count != 0)
            {
                xml.WriteStartElement("Attributes");
                foreach(var x in Attributes)
                {
                    xml.WriteElementString(x.Key, x.Value);
                }
                xml.WriteEndElement();
            }
        }

        public override string ToString()
        {
            if (Parent == null) return Name;
            return Parent + "." + Name;
        }

        public static Symbol ReadSymbol(XmlReader xml, string parent)
        {
            Symbol x = null;
            switch (xml.LocalName)
            {
                case "Element": x = new ElementSymbol(); break;
                case "Class": x = new ClassSymbol(); break;
                case "Struct": x = new StructSymbol(); break;
                case "Variable": x = new VariableSymbol(); break;
                case "Property": x = new PropertySymbol(); break;
            }
            x.Name = xml.GetAttribute("Name");
            x.Parent = parent;
            x.Read(xml);
            return x;
        }
    }
    public class ElementSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children => c;
        public string Alternate;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            if (Alternate != null)
            {
                xml.WriteAttributeString("Alternate", Alternate);
            }
            WriteCommonStuff(xml);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteFullEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Alternate = xml.GetAttribute("Alternate");
            if (xml.IsEmptyElement) return;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Attributes")
                    {
                        if (xml.IsEmptyElement) continue;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Attributes.Add(xml.LocalName, xml.ReadElementContentAsString());
                            }
                        }
                    }
                    else Children.Add(ReadSymbol(xml, ToString()));
                }
            }
        }

    }
    public class ClassSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children => c;
        public List<string> Generics = new List<string>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Class");
            xml.WriteStartAttribute("Generics");
            if (Generics.Count != 0)
            {
                xml.WriteRaw(Generics[0]);
                for (int i = 1; i < Generics.Count; i++)
                {
                    xml.WriteRaw(",");
                    xml.WriteRaw(Generics[i]);
                }
            }
            xml.WriteEndAttribute();
            WriteCommonStuff(xml);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            string generics = xml.GetAttribute("Generics");
            Generics.AddRange(new List<string>(generics.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            if (xml.IsEmptyElement) return;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Attributes")
                    {
                        if (xml.IsEmptyElement) continue;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Attributes.Add(xml.LocalName, xml.ReadElementContentAsString());
                            }
                        }
                    }
                    else Children.Add(ReadSymbol(xml, ToString()));
                }
            }
        }
    }
    public class StructSymbol : Symbol
    {
        public List<Symbol> c = new List<Symbol>();
        public override List<Symbol> Children => c;
        public List<string> Generics = new List<string>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Struct");
            xml.WriteStartAttribute("Generics");
            if (Generics.Count != 0)
            {
                xml.WriteRaw(Generics[0]);
                for (int i = 1; i < Generics.Count; i++)
                {
                    xml.WriteRaw(",");
                    xml.WriteRaw(Generics[i]);
                }
            }
            xml.WriteEndAttribute();
            WriteCommonStuff(xml);
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            string generics = xml.GetAttribute("Generics");
            Generics.AddRange(new List<string>(generics.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            if (xml.IsEmptyElement) return;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Attributes")
                    {
                        if (xml.IsEmptyElement) continue;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Attributes.Add(xml.LocalName, xml.ReadElementContentAsString());
                            }
                        }
                    }
                    else Children.Add(ReadSymbol(xml, ToString()));
                }
            }
        }
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
        public override List<Symbol> Children => null;

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

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Variable");
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            WriteCommonStuff(xml);
            if (Value != null) Value.Write(xml);
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Get"));
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Set"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Value")
                    {
                        Value = Value.ReadValue(xml);
                    }
                    else if (xml.LocalName == "Attributes")
                    {
                        if (xml.IsEmptyElement) continue;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Attributes.Add(xml.LocalName, xml.ReadElementContentAsString());
                            }
                        }
                    }
                }
            }
        }
    }
    public class PropertySymbol : Symbol
    {
        public override List<Symbol> Children => null;

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
                    _t = Getter == null ? Setter.Type : Getter.Type;
                }
                return _t;
            }
            set => _t = value;
        }

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Property");
            WriteCommonStuff(xml);
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            if (Getter != null)
            {
                xml.WriteStartElement("Getter");
                Getter.Write(xml);
                xml.WriteFullEndElement();
            }
            if (Setter != null)
            {
                xml.WriteStartElement("Setter");
                Setter.Write(xml);
                xml.WriteFullEndElement();
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Get"));
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Set"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Getter")
                    {
                        Getter = Value.ReadValue(xml);
                    }
                    if (xml.LocalName == "Setter")
                    {
                        Setter = Value.ReadValue(xml);
                    }
                }
            }
        }
    }
    public abstract class Type
    {
        public override bool Equals(object obj)
        {
            return obj is Type t && Equals(t);
        }
        public abstract bool Equals(Type t);
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);
        public abstract bool IsRefType { get; }

        public static Type ReadType(XmlReader xml)
        {
            Type t = null;
            switch (xml.GetAttribute("Kind"))
            {
                case "Normal": t = new NormalType(); break;
                case "Function": t = new FunctionType(); break;
                case "Record": t = new RecordType(); break;
                case "Collection": t = new CollectionType(); break;
                case "Multi": t = new MultiType(); break;
                case "Boxed": t = new BoxedType(); break;
                case "Generic": t = new GenericType(); break;
                case "Boolean": t = BooleanType.Value; break;
                case "Object": t = ObjectType.Value; break;
            }
            t.Read(xml);
            return t;
        }
    }
    public class NormalType : Type
    {
        public string Base;
        public bool RefType;
        public List<(string, Type)> Generics = new List<(string, Type)>();

        public override bool IsRefType => RefType;
        public override bool Equals(Type t)
        {
            return t is NormalType tt && Base == tt.Base && RefType == tt.RefType;

        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Normal");
            xml.WriteAttributeString("Base", Base);
            xml.WriteAttributeString("RefType", RefType.ToString());
            for (int i = 0; i < Generics.Count; i++)
            {
                xml.WriteStartElement("Generic");
                xml.WriteAttributeString("Name", Generics[i].Item1);
                Generics[i].Item2.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            RefType = bool.Parse(xml.GetAttribute("RefType"));
            if (!xml.IsEmptyElement)
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.EndElement) break;
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Generic")
                    {
                        string name = xml.GetAttribute("Name");
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Generics.Add((name, ReadType(xml)));
                            }
                        }
                    }
                }
            }
        }
        public override string ToString()
        {
            return Base;
        }
    }
    public class FunctionType : Type
    {
        public Type ReturnType;
        public List<(Type, string, bool)> Parameters = new List<(Type, string, bool)>();
        public List<string> Generics = new List<string>();

        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            if (t is FunctionType tt)
            {
                if (Generics.Count != tt.Generics.Count || Parameters.Count != tt.Parameters.Count) return false;
                if (Generics.Count == 0)
                {
                    return ReturnType.Equals(tt.ReturnType) && Tools.ListEquals(Parameters, tt.Parameters);
                }

                bool typeEquals(Type x, Type y)
                {
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
                    if (!typeEquals(Parameters[i].Item1, tt.Parameters[i].Item1))
                        return false;
                }
                return true;
            }
            return false;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Function");
            xml.WriteStartAttribute("Generics");
            if (Generics.Count != 0)
            {
                xml.WriteRaw(Generics[0]);
                for (int i = 1; i < Generics.Count; i++)
                {
                    xml.WriteRaw(",");
                    xml.WriteRaw(Generics[i]);
                }
            }
            xml.WriteEndAttribute();
            if (ReturnType != null)
            {
                ReturnType.Write(xml);
            }
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                if (Parameters[i].Item2 != null) xml.WriteAttributeString("Name", Parameters[i].Item2);
                xml.WriteAttributeString("Optional", Parameters[i].Item3.ToString());
                Parameters[i].Item1.Write(xml);
                xml.WriteFullEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            string generics = xml.GetAttribute("Generics");
            Generics.AddRange(new List<string>(generics.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    switch (xml.LocalName)
                    {
                        case "Type":
                            ReturnType = ReadType(xml);
                            break;
                        case "Parameter":
                            {
                                bool optional = bool.Parse(xml.GetAttribute("Optional"));
                                string name = xml.GetAttribute("Name");
                                while (xml.Read())
                                {
                                    if (xml.NodeType == XmlNodeType.EndElement) break;
                                    if (xml.NodeType == XmlNodeType.Element)
                                    {
                                        if (xml.LocalName == "Type")
                                        {
                                            Parameters.Add((ReadType(xml), name, optional));
                                        }
                                    }
                                }
                                break;
                            }
                    }
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("function ");
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
        public HashSet<(string, Type)> Fields = new HashSet<(string, Type)>();

        public override bool IsRefType => false;
        public override bool Equals(Type t)
        {
            return t is RecordType tt && Fields.SetEquals(tt.Fields);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Record");
            foreach (var x in Fields)
            {
                xml.WriteStartElement("Field");
                xml.WriteAttributeString("Name", x.Item1);
                x.Item2.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Field")
                {
                    string n = xml.GetAttribute("Name");
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                        {
                            Fields.Add((n, ReadType(xml)));
                        }
                    }
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("[");
            bool notfirst = false;
            foreach (var x in Fields)
            {
                if (notfirst) sb.Append(", ");
                notfirst = true;
                sb.Append(x.Item2.ToString());
                sb.Append(' ');
                sb.Append(x.Item1);
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
    public class CollectionType : Type
    {
        public Type Base;

        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            return t is CollectionType tt && Base.Equals(tt.Base);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Collection");
            Base.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Base = ReadType(xml);
                }
            }
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
        public override bool Equals(Type t)
        {
            return t is MultiType tt && Types.SetEquals(tt.Types);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Multi");
            foreach (var x in Types)
            {
                x.Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Types.Add(ReadType(xml));
                }
            }
        }
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("(");
            bool notfirst = false;
            foreach (var x in Types)
            {
                if (notfirst) sb.Append(", ");
                sb.Append(x.ToString());
                notfirst = true;
            }
            sb.Append(")");
            return sb.ToString();
        }
    }
    public class BoxedType : Type
    {
        public Type Base;
        public override bool IsRefType => true;
        public override bool Equals(Type t)
        {
            return t is BoxedType tt && Base.Equals(tt.Base);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Boxed");
            Base.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Base = ReadType(xml);
                }
            }
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
        public override bool Equals(Type t)
        {
            return t is GenericType tt && tt.Name == Name;

        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Generic");
            xml.WriteAttributeString("Name", Name);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
        }
        public override string ToString()
        {
            return "generic type: " + Name;
        }
    }
    public class ObjectType : Type
    {
        public static ObjectType Value = new ObjectType();

        private ObjectType() { }

        public override bool IsRefType => false;
        public override bool Equals(Type t)
        {
            return t == Value;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Object");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
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
        public override bool Equals(Type t)
        {
            return t == Value;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Boolean");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
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

        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Value ReadValue(XmlReader xml)
        {
            Value x = null;
            switch (xml.GetAttribute("Kind"))
            {
                case "Local": x = new LocalValue(); break;
                case "Symbol": x = new SymbolValue(); break;
                case "Call": x = new CallValue(); break;
                case "Operation": x = new OperationValue(); break;
                case "Procedural": x = new ProceduralValue(); break;
                case "Convert": x = new ConvertValue(); break;
                case "Function": x = new FunctionValue(); break;
                case "Record": x = new RecordValue(); break;
                case "Collection": x = new CollectionValue(); break;
                case "Member": x = new MemberValue(); break;
                case "Boxed": x = new BoxedValue(); break;
                case "New": x = new NewValue(); break;
                case "Literal": x = new LiteralValue(); break;
                case "Default": x = new DefaultValue(); break;
                case "Null": x = new DefaultValue(); break;
            }
            x.Read(xml);
            return x;
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Local");
            xml.WriteAttributeString("Name", Name);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
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
                    Symbol s = Project.Current.GetSymbol(Symbol);
                    _type = s is VariableSymbol v ? v.Type : ((PropertySymbol)s).Type;
                }
                return _type;
            }
            set => _type = value;
        }
        public string Symbol;
        public Value Instance;

        public bool Equals(Value v)
        {
            return v is SymbolValue vv && vv.Symbol == Symbol;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Symbol");
            xml.WriteAttributeString("Symbol", Symbol);
            if (Instance != null) Instance.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Symbol = xml.GetAttribute("Symbol");
            if (!xml.IsEmptyElement)
            {
                while (xml.Read())
                {
                    if (xml.NodeType == XmlNodeType.EndElement) break;
                    if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                    {
                        Instance = ReadValue(xml);
                    }
                }
            }
        }
        public override string ToString()
        {
            return Symbol;
        }
    }
    public class CallValue : Value
    {
        public override Type Type
        {
            get => ((FunctionType)Function.Type).ReturnType;
            set { }
        }
        public Value Function;
        public List<(string, Value)> Parameters = new List<(string, Value)>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Call");
            Function.Write(xml);
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                if (Parameters[i].Item1 != null)
                {
                    xml.WriteAttributeString("Name", Parameters[i].Item1);
                }
                Parameters[i].Item2.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if(xml.LocalName == "Value")
                    {
                        Function = ReadValue(xml);
                    }
                    else if(xml.LocalName == "Parameter")
                    {
                        string name = xml.GetAttribute("Name");
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Parameters.Add((name, ReadValue(xml)));
                            }
                        }
                    }
                }
            }
        }
        public override string ToString()
        {
            var sb = new StringBuilder(Function.ToString());
            sb.Append("(");

            void appendParam((string, Value) param)
            {
                if (param.Item1 != null)
                {
                    sb.Append(param.Item1);
                    sb.Append(" = ");
                }
                sb.Append(param.Item2.ToString());
            }

            if (Parameters.Count != 0)
            {
                appendParam(Parameters[0]);
                for (int i = 1; i < Parameters.Count; i++)
                {
                    sb.Append(", ");
                    appendParam(Parameters[1]);
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
        Not,
        And,
        Or,
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
                    case OperationType.Not:
                    case OperationType.And:
                    case OperationType.Or:
                        return BooleanType.Value;
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Operation");
            xml.WriteAttributeString("OperationType", OperationType.ToString());
            if (t != null) t.Write(xml);
            if (Values.Count != 0)
            {
                xml.WriteStartElement("Values");
                for (int i = 0; i < Values.Count; i++)
                {
                    Values[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            if (Types.Count != 0)
            {
                xml.WriteStartElement("Types");
                for (int i = 0; i < Types.Count; i++)
                {
                    Types[i].Write(xml);
                }
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            OperationType = (OperationType)Enum.Parse(typeof(OperationType), xml.GetAttribute("OperationType"));
            if (xml.IsEmptyElement) return;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Type")
                    {
                        t = Type.ReadType(xml);
                    }
                    else if (xml.LocalName == "Values")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                Values.Add(ReadValue(xml));
                            }
                        }
                    }
                    else if (xml.LocalName == "Types")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                            {
                                Types.Add(Type.ReadType(xml));
                            }
                        }
                    }
                }
            }
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Procedural");
            if (Type != null) Type.Write(xml);
            xml.WriteStartElement("Instruction");
            Instruction.Write(xml);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Instruction")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                Instruction = Instruction.ReadInstruction(xml);
                            }
                        }
                    }
                    else if (xml.LocalName == "Type")
                    {
                        Type = Type.ReadType(xml);
                    }
                }
            }
        }

        public override string ToString()
        {
            return "Procedural value of type -> " + Type.ToString();
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Convert");
            Base.Write(xml);
            Type.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Value")
                    {
                        Base = ReadValue(xml);
                    }
                    else if (xml.LocalName == "Type")
                    {
                        t = Type.ReadType(xml);
                    }
                }
            }
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
            get
            {
                if (_type == null)
                {
                    var functionType = new FunctionType { ReturnType = Value.Type };
                    for (int i = 0; i < Parameters.Count; i++)
                    {
                        functionType.Parameters.Add((ObjectType.Value, Parameters[i].Item1, false));
                    }
                    _type = functionType;
                }
                return _type;
            }
            set => _type = value;
        }
        public Value Value;
        public List<(string, Value)> Parameters = new List<(string, Value)>();
        public List<string> Generics = new List<string>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Function");
            xml.WriteStartAttribute("Generics");
            if (Generics.Count != 0)
            {
                xml.WriteRaw(Generics[0]);
                for (int i = 1; i < Generics.Count; i++)
                {
                    xml.WriteRaw(",");
                    xml.WriteRaw(Generics[i]);
                }
            }
            xml.WriteEndAttribute();
            Type.Write(xml);
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                xml.WriteAttributeString("Name", Parameters[i].Item1);
                if (Parameters[i].Item2 != null) Parameters[i].Item2.Write(xml);
                xml.WriteEndElement();
            }
            Value.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            string generics = xml.GetAttribute("Generics");
            Generics.AddRange(new List<string>(generics.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Parameter")
                    {
                        string name = xml.GetAttribute("Name");
                        Value value = null;
                        if (!xml.IsEmptyElement)
                        {
                            while (xml.Read())
                            {
                                if (xml.NodeType == XmlNodeType.EndElement) break;
                                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                                {
                                    value = ReadValue(xml);
                                }
                            }
                        }
                        Parameters.Add((name, value));
                    }
                    else if (xml.LocalName == "Type")
                    {
                        Type = Type.ReadType(xml);
                    }
                    else if (xml.LocalName == "Value")
                    {
                        Value = ReadValue(xml);
                    }
                }
            }
        }
        public override string ToString()
        {
            return "function of type -> " + Type.ToString();
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Record");
            for (int i = 0; i < Fields.Count; i++)
            {
                xml.WriteStartElement("Field");
                xml.WriteAttributeString("Name", Fields[i].Item1);
                Fields[i].Item2.Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Field")
                {
                    string name = xml.GetAttribute("Name");
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                        {
                            Fields.Add((name, ReadValue(xml)));
                        }
                    }
                }
            }
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

        public bool Equals(Value v)
        {
            return v is CollectionValue vv && Tools.ListEquals(vv.Values, Values);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Collection");
            for (int i = 0; i < Values.Count; i++)
            {
                Values[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Values.Add(ReadValue(xml));
                }
            }
        }
        public override string ToString()
        {
            var sb = new StringBuilder(((CollectionType)Type).Base.ToString());
            sb.Append("{");
            if (Values.Count != 0)
            {
                sb.Append(Values[0].ToString());
                for (int i = 0; i < Values.Count; i++)
                {
                    sb.Append(", ");
                    sb.Append(Values[i].ToString());
                }
            }
            sb.Append("}");
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Member");
            xml.WriteAttributeString("Name", Name);
            Base.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Base = ReadValue(xml);
                }
            }
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
        public List<(string, Value)> FieldValues = new List<(string, Value)>();

        public bool Equals(Value v)
        {
            return false;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("New");
            Type.Write(xml);
            for (int i = 0; i < FieldValues.Count; i++)
            {
                xml.WriteStartElement("FieldValue");
                xml.WriteAttributeString("Name", FieldValues[i].Item1);
                FieldValues[i].Item2.Write(xml);
                xml.WriteEndElement();
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "FieldValue")
                {
                    string name = xml.GetAttribute("Name");
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                        {
                            FieldValues.Add((name, ReadValue(xml)));
                        }
                    }
                }
            }
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Boxed");
            Base.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Base = ReadValue(xml);
                }
            }
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
        public static LiteralValue True  = new LiteralValue { LiteralType = LiteralValueType.Boolean, Value = "True" };
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
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Literal");
            xml.WriteAttributeString("Value", Value);
            xml.WriteAttributeString("LiteralType", LiteralType.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Value = xml.GetAttribute("Value");
            LiteralType = (LiteralValueType)Enum.Parse(typeof(LiteralValueType), xml.GetAttribute("LiteralType"));
        }
        public override string ToString()
        {
            return "Literal: " + Value;
        }
    }
    public class DefaultValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = ObjectType.Value;
                }
                return _type;
            }
            set => _type = value;
        }

        public bool Equals(Value v)
        {
            return v is DefaultValue && Type.Equals(v.Type);
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Default");
            _type.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    _type = Type.ReadType(xml);
                }
            }
        }
        public override string ToString()
        {
            return "default of " + _type.ToString();
        }
    }
    public class NullValue : Value
    {
        public override Type Type
        {
            get
            {
                if (_type == null)
                {
                    _type = ObjectType.Value;
                }
                return _type;
            }
            set => _type = value;
        }
        public static NullValue Value = new NullValue();

        public bool Equals(Value v)
        {
            return v == Value;
        }
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Null");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
        }
        public override string ToString()
        {
            return "null";
        }
    }

    public abstract class Instruction
    {
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public string File;
        public uint Line, Index;

        protected void WriteCommonStuff(XmlWriter xml)
        {
            if (File != null) xml.WriteAttributeString("File", File);
            if (Line != 0) xml.WriteAttributeString("Line", Line.ToString());
            if (Index != 0) xml.WriteAttributeString("Index", Line.ToString());
        }

        public static Instruction ReadInstruction(XmlReader xml)
        {
            Instruction x = null;
            switch (xml.LocalName)
            {
                case "Scope": x = new ScopeInstruction(); break;
                case "Control": x = new ControlInstruction(); break;
                case "Action": x = new ActionInstruction(); break;
                case "Return": x = new ReturnInstruction(); break;
                case "Assign": x = new AssignInstruction(); break;
                case "If": x = new IfInstruction(); break;
                case "Else": x = new ElseInstruction(); break;
                case "Throw": x = new ThrowInstruction(); break;
                case "Catch": x = new CatchInstruction(); break;
            }
            x.File = xml.GetAttribute("File");
            var lineAttr = xml.GetAttribute("Line");
            x.Line = lineAttr == null ? 0 : uint.Parse(lineAttr);
            var indexAttr = xml.GetAttribute("Index");
            x.Index = lineAttr == null ? 0 : uint.Parse(indexAttr);
            x.Read(xml);
            return x;
        }
    }
    public class NoOpInstruction : Instruction
    {
        public static NoOpInstruction Value = new NoOpInstruction();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("No-Op");
            WriteCommonStuff(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
        }
    }
    public class ScopeInstruction : Instruction
    {
        public string Name;
        public List<Instruction> Instructions = new List<Instruction>();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Scope");
            xml.WriteAttributeString("Name", Name);
            WriteCommonStuff(xml);
            for (int i = 0; i < Instructions.Count; i++)
            {
                Instructions[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            if (Name == string.Empty) Name = null;
            if (xml.IsEmptyElement) return;
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    Instructions.Add(ReadInstruction(xml));
                }
            }
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

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Control");
            if (Name != null) xml.WriteAttributeString("Name", Name);
            xml.WriteAttributeString("Type", Type.ToString());
            WriteCommonStuff(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Name = xml.GetAttribute("Name");
            Type = (ControlInstructionType)Enum.Parse(typeof(ControlInstructionType), xml.GetAttribute("Type"));
        }
    }
    public class ActionInstruction : Instruction
    {
        public Value Value;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Action");
            WriteCommonStuff(xml);
            Value.Write(xml);
            xml.WriteFullEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Value = Value.ReadValue(xml);
                }
            }
        }
    }
    public class ReturnInstruction : Instruction
    {
        public Value Value;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Return");
            WriteCommonStuff(xml);
            Value.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                {
                    Value = Value.ReadValue(xml);
                }
            }
        }
    }
    public class AssignInstruction : Instruction
    {
        public Value Left;
        public Value Right;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Assign");
            WriteCommonStuff(xml);
            xml.WriteStartElement("Left");
            Left.Write(xml);
            xml.WriteEndElement();
            xml.WriteStartElement("Right");
            Right.Write(xml);
            xml.WriteEndElement();
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Left")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                Left = Value.ReadValue(xml);
                            }
                        }
                    }
                    else if (xml.LocalName == "Right")
                    {
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                Right = Value.ReadValue(xml);
                            }
                        }
                    }
                }
            }
        }
    }
    public class IfInstruction : Instruction
    {
        public Value Condition;
        public Instruction Instruction;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("If");
            WriteCommonStuff(xml);
            Condition.Write(xml);
            Instruction.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    if (xml.LocalName == "Value")
                    {
                        Condition = Value.ReadValue(xml);
                    }
                    else
                    {
                        Instruction = ReadInstruction(xml);
                    }
                }
            }
        }
    }
    public class ElseInstruction : Instruction
    {
        public Instruction Instruction;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Else");
            WriteCommonStuff(xml);
            Instruction.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    Instruction = ReadInstruction(xml);
                }
            }
        }
    }
    public class ThrowInstruction : Instruction
    {
        public string Exception;
        public string Message;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Throw");
            WriteCommonStuff(xml);
            xml.WriteAttributeString("Exception", Exception);
            xml.WriteAttributeString("Message", Message);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Exception = xml.GetAttribute("Exception");
            Message = xml.GetAttribute("Message");
        }
    }
    public class CatchInstruction : Instruction
    {
        public List<string> Exceptions;
        public Instruction Instruction;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Throw");
            WriteCommonStuff(xml);
            xml.WriteAttributeString("Exceptions", string.Join(",", Exceptions));
            Instruction.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Exceptions.AddRange(xml.GetAttribute("Exception").Split(','));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    Instruction = ReadInstruction(xml);
                }
            }
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
