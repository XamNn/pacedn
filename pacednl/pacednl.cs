using pacednl.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

//General pace library for c# which implements data structure for pace library files

namespace pacednl
{
    public static class Info
    {
        public static string Version = "pacednl beta-160218";
    }

    public static class Config
    {
        public static string LibraryDirectory = AppDomain.CurrentDomain.BaseDirectory + @"\libraries";
        public static string LibraryFileExtention = ".pacelib";

        public static string FormatLibraryFilename(string s, string localpath, bool checkexists)
        {
            if (File.Exists(s)) return s;
            if (localpath != null)
            {
                string x = localpath + "\\" + s + LibraryFileExtention;
                if (!checkexists || File.Exists(x)) return x;
            }
            return LibraryDirectory + "\\" + s + LibraryFileExtention;
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

        public List<Profile> Profiles = new List<Profile>();
        public List<Symbol> Symbols = new List<Symbol>();
        public List<Library> Libraries = new List<Library>();

        public Library Merge()
        {
            Libraries.Clear();
            var x = new Library();
            Libraries.Add(x);
            x.Name = "MergedLibrary";
            x.Profiles = Profiles;
            x.Symbols = Symbols;
            return x;
        }
        public OperationResult Import(Library l, bool checkSymbols)
        {
            if (checkSymbols)
            {
                for (int i = 0; i < Symbols.Count; i++)
                {
                    for (int i2 = 0; i2 < l.Symbols.Count; i2++)
                    {
                        if (Symbols[i].Name == l.Symbols[i2].Name)
                            return new OperationResult("Library contains symbols already defined");
                    }
                }
            }
            return OperationResult.Success;
        }
    }

    public class Library
    {
        public string Name;
        public List<Profile> Profiles = new List<Profile>();
        public List<Symbol> Symbols = new List<Symbol>();
        public Dictionary<string, string> Aliases = new Dictionary<string, string>();
        public Procedure EntryPoint;

        public void Save(string file)
        {
            XmlWriter xml = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
            xml.WriteStartElement("PaceLibrary");
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
                xml.WriteEndElement();
            }
            if (Aliases.Count != 0)
            {
                xml.WriteStartElement("Aliases");
                foreach (var x in Aliases)
                {
                    xml.WriteStartElement("Alias");
                    xml.WriteAttributeString("Name", x.Key);
                    xml.WriteAttributeString("Match", x.Value);
                    xml.WriteEndElement();
                }
                xml.WriteEndElement();
            }
            for (int i = 0; i < Profiles.Count; i++)
            {
                Profiles[i].Write(xml);
            }
            xml.WriteEndElement();
            xml.Close();
        }
        
        public OperationResult Read(string file)
        {
            if (!File.Exists(file)) return new OperationResult("File does not exist");
            XmlReader xml = XmlReader.Create(file);
            var x = new Library();
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.Element)
                {
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element)
                        {
                            try
                            {
                                switch (xml.LocalName)
                                {
                                    case "Profile":
                                        Profiles.Add(Profile.Read(xml, false));
                                        break;
                                    case "Symbols":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element)
                                            {
                                                x.Symbols.Add(Symbol.ReadSymbol(xml));
                                            }
                                        }
                                        break;
                                    case "Aliases":
                                        while (xml.Read())
                                        {
                                            if (xml.NodeType == XmlNodeType.EndElement) break;
                                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Alias")
                                            {
                                                Aliases.Add(xml.GetAttribute("Name"), xml.GetAttribute("Match"));
                                                while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) break;
                                            }
                                        }
                                        break;
                                    case "Procedure":
                                        EntryPoint = Procedure.Read(xml);
                                        break;
                                }
                            }
                            catch (Exception e)
                            {
                                return new OperationResult("The following error occured when importing library: " + e.Message);
                            }
                        }
                    }
                }
            }
            return OperationResult.Success;
        }
    }

    public class Profile
    {
        public string Translator;
        public List<string> Dependencies = new List<string>();

        public void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Profile");
            xml.WriteAttributeString("Translator", Translator);
            for (int i = 0; i < Dependencies.Count; i++)
            {
                xml.WriteElementString("Dependency", Dependencies[i]);
            }
        }
        public static Profile Read(XmlReader xml, bool unnamed)
        {
            var x = new Profile();
            if (!unnamed) x.Translator = xml.GetAttribute("Translator");
            while (true)
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if(xml.NodeType == XmlNodeType.Element)
                {
                    switch (xml.LocalName)
                    {
                        case "Dependency":
                            x.Dependencies.Add(xml.Value);
                            break;
                    }
                }
            }
            return x;
        }
    }

    public abstract class Symbol
    {
        public string Name;

        public abstract void Write(XmlWriter xml);
        protected abstract void Read(XmlReader xml);

        public static Symbol ReadSymbol(XmlReader xml)
        {
            Symbol x = null;
            switch (xml.LocalName)
            {
                case "Element": x = new ElementSymbol(); break;
                case "Class": x = new ClassSymbol(); break;
                case "Struct": x = new StructSymbol(); break;
                case "Variable": x = new VariableSymbol(); break;
            }
            x.Read(xml);
            return x;
        }
    }
    public class ElementSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }

    }
    public class ClassSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }
    }
    public class StructSymbol : Symbol
    {
        public List<Symbol> Symbols;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Element");
            for (int i = 0; i < Symbols.Count; i++)
            {
                Symbols[i].Write(xml);
            }
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element) Symbols.Add(ReadSymbol(xml));
            }
        }
    }

    public enum AccessorType
    {
        None,
        Public,
        Private,
    }
    public class VariableSymbol : Symbol
    {
        public AccessorType Get;
        public AccessorType Set;
        public Type Type;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Variable");
            xml.WriteAttributeString("Get", Get.ToString());
            xml.WriteAttributeString("Set", Set.ToString());
            xml.WriteEndElement();
        }
        protected override void Read(XmlReader xml)
        {
            Get = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Get"));
            Set = (AccessorType)Enum.Parse(typeof(AccessorType), xml.GetAttribute("Set"));
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                {
                    Type = Type.ReadType(xml);
                }
            }
        }
    }

    public abstract class Type
    {
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Type ReadType(XmlReader xml)
        {
            throw new NotImplementedException();
        }
    }
    public class NormalType : Type
    {
        public string Base;
        public bool Boxed;
        public bool RefType;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Normal");
            xml.WriteAttributeString("Base", Base);
            xml.WriteAttributeString("Boxed", Boxed.ToString());
            xml.WriteAttributeString("RefType", RefType.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            Boxed = bool.Parse(xml.GetAttribute("Boxed"));
            RefType = bool.Parse(xml.GetAttribute("RefType"));
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) ;
        }
    }
    public class FunctionType : Type
    {
        public Type ReturnType;
        public List<(Type, Value)> Parameters;
        public Procedure Procedure;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Function");
            ReturnType.Write(xml);
            Procedure.Write(xml);
            for (int i = 0; i < Parameters.Count; i++)
            {
                xml.WriteStartElement("Parameter");
                Parameters[i].Item1.Write(xml);
                if (Parameters[i].Item2 != null) Parameters[i].Item2.Write(xml);
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
                    switch (xml.LocalName)
                    {
                        case "Type":
                            ReturnType.Read(xml);
                            break;
                        case "Parameter":
                            {
                                Type t = null;
                                Value v = null;
                                while (xml.Read())
                                {
                                    if (xml.NodeType == XmlNodeType.EndElement) break;
                                    if (xml.NodeType == XmlNodeType.Element)
                                    {
                                        if (xml.LocalName == "Type")
                                        {
                                            t = ReadType(xml);
                                        }
                                        else if(xml.LocalName == "Value")
                                        {
                                            v = Value.ReadValue(xml);
                                        }
                                    }
                                    
                                }
                                break;
                            }
                        case "Procedure":
                            Procedure = Procedure.Read(xml);
                            break;
                    }
                }
            }
        }
    }
    public class RecordType : Type
    {
        public List<(string, Type)> Fields;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
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
                    string n = xml.GetAttribute("Name");
                    while (xml.Read())
                    {
                        if (xml.NodeType == XmlNodeType.EndElement) break;
                        if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Type")
                        {
                            Fields.Add((n, ReadType(xml)));
                            break;
                        }
                    }
                }
            }
        }
    }
    public class ObjectType : Type
    {
        public static ObjectType Value = new ObjectType();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Type");
            xml.WriteAttributeString("Kind", "Object");
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) ;
        }
    }

    public abstract class Value
    {
        public abstract void Write(XmlWriter xml);
        public abstract void Read(XmlReader xml);

        public static Value ReadValue(XmlReader xml)
        {
            Value x = null;
            switch (xml.LocalName)
            {
                case "Local": x = new LocalValue(); break;
                case "Variable": x = new VariableValue(); break;
                case "Function": x = new FunctionValue(); break;
                case "Record": x = new RecordValue(); break;
                case "Member": x = new MemberValue(); break;
                case "Boxed": x = new BoxedValue(); break;
                case "Null": x = NullValue.Value; break;
            }
            x.Read(xml);
            return x;
        }
    }
    public class LocalValue : Value
    {
        public int ID;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("ID", ID.ToString());
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            ID = int.Parse(xml.GetAttribute("ID"));
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) ; 
        }
    }
    public class VariableValue : Value
    {
        public string Base;
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Variable");
            xml.WriteAttributeString("Base", Base);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            Base = xml.GetAttribute("Base");
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) ;
        }
    }
    public class FunctionValue : Value
    {
        public Procedure Procedure;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Function");
            Procedure.Write(xml);
            xml.WriteEndElement();
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Procedure")
                {
                    Procedure = Procedure.Read(xml);
                }
            }
        }
    }
    public class RecordValue : Value
    {
        List<(string, Value)> Values = new List<(string, Value)>();
        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Record");
            for (int i = 0; i < Values.Count; i++)
            {
                xml.WriteStartElement("Field");
                xml.WriteAttributeString("Name", Values[i].Item1);
                Values[i].Item2.Write(xml);
            }
        }
        public override void Read(XmlReader xml)
        {

        }
    }
    public class MemberValue : Value
    {
        public Value Base;
        public string Name;

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Name", Name);
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
    }
    public class BoxedValue : Value
    {
        Value Base;
        public override void Write(XmlWriter xml)
        {
            Base.Write(xml);
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
    }
    public class NullValue : Value
    {
        public static NullValue Value = new NullValue();

        public override void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Value");
            xml.WriteAttributeString("Kind", "Null");
        }
        public override void Read(XmlReader xml)
        {
            while (xml.Read() && xml.NodeType != XmlNodeType.EndElement) ;
        }
    }

    public enum InstructionType
    {
        No_op,
        Scope,
        Break,
        Continue,
        Operation,
        Store,
        Assign,
        Throw,
        If,
        Else,
    }
    public struct Instruction
    {
        public InstructionType Type;
        public object Data;
    }
    public class Procedure
    {
        List<Instruction> Instructions = new List<Instruction>();

        public void Write(XmlWriter xml)
        {
            xml.WriteStartElement("Procedure");
            for (int i = 0; i < Instructions.Count; i++)
            {
                WriteInstruction(Instructions[i], xml);
            }
            xml.WriteEndElement();
        }
        void WriteInstruction(Instruction i, XmlWriter xml)
        {
            switch (i.Type)
            {
                case InstructionType.Scope:
                    {
                        xml.WriteStartElement("Scope");
                        var data = ((string, List<Instruction>))i.Data;
                        xml.WriteAttributeString("ID", data.Item1);
                        for (int x = 0; x < data.Item2.Count; x++)
                        {
                            WriteInstruction(data.Item2[x], xml);
                        }
                        break;
                    }
                case InstructionType.Break:
                    {
                        xml.WriteStartElement("Break");
                        xml.WriteAttributeString("ID", (string)i.Data);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Continue:
                    {
                        xml.WriteStartElement("Continue");
                        xml.WriteAttributeString("ID", (string)i.Data);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Operation:
                    {
                        xml.WriteStartElement("Operation");
                        var data = (Value)i.Data;
                        data.Write(xml);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Store:
                    {
                        xml.WriteStartElement("Store");
                        var data = ((string, Value))i.Data;
                        xml.WriteAttributeString("ID", data.Item1);
                        data.Item2.Write(xml);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Assign:
                    {
                        xml.WriteStartElement("Assign");
                        var data = ((string, Value))i.Data;
                        xml.WriteAttributeString("ID", data.Item1);
                        data.Item2.Write(xml);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.Throw:
                    {
                        xml.WriteStartElement("Throw");
                        var data = ((string, string))i.Data;
                        xml.WriteAttributeString("Exception", data.Item1);
                        xml.WriteAttributeString("Message", data.Item2);
                        xml.WriteEndElement();
                        break;
                    }
                case InstructionType.If:
                    {
                        xml.WriteStartElement("If");
                        var data = ((Value, Instruction))i.Data;
                        data.Item1.Write(xml);
                        WriteInstruction(data.Item2, xml);
                        break;
                    }
                case InstructionType.Else:
                    {
                        xml.WriteStartElement("Else");
                        var data = (Instruction)i.Data;
                        WriteInstruction(data, xml);
                        break;
                    }
            }
        }

        public static Procedure Read(XmlReader xml)
        {
            var x = new Procedure();
            while (xml.Read())
            {
                if (xml.NodeType == XmlNodeType.EndElement) break;
                if (xml.NodeType == XmlNodeType.Element)
                {
                    x.Instructions.Add(ReadInstruction(xml));
                }
            }
            return x;
        }
        static Instruction ReadInstruction(XmlReader xml)
        {
            switch (xml.LocalName)
            {
                case "Scope":
                    {
                        string a = xml.GetAttribute("ID");
                        List<Instruction> b = new List<Instruction>();
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                b.Add(ReadInstruction(xml));
                            }
                        }
                        return new Instruction { Type = InstructionType.Scope, Data = (a, b) };
                    }
                case "Break":
                    {
                        string a = xml.GetAttribute("ID");
                        while (xml.Read()) if (xml.NodeType == XmlNodeType.EndElement) break;
                        return new Instruction { Type = InstructionType.Break, Data = a };
                    }
                case "Continue":
                    {
                        string a = xml.GetAttribute("ID");
                        while (xml.Read()) if (xml.NodeType == XmlNodeType.EndElement) break;
                        return new Instruction { Type = InstructionType.Continue, Data = a };
                    }
                case "Operation":
                    {
                        Value a = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                a =  Value.ReadValue(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.Operation, Data = a };
                    }
                case "Store":
                    {
                        string a = xml.GetAttribute("ID");
                        Value b = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                b = Value.ReadValue(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.Store, Data = (a, b) };
                    }
                case "Throw":
                    {
                        string a = xml.GetAttribute("Exception");
                        string b = xml.GetAttribute("Message");
                        while (xml.Read()) if (xml.NodeType == XmlNodeType.EndElement) break;
                        return new Instruction { Type = InstructionType.Scope, Data = (a, b) };
                    }
                case "If":
                    {
                        Value a = null;
                        Instruction b = new Instruction { Type = InstructionType.No_op, Data = null };
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element)
                            {
                                if (xml.LocalName == "Value") a = Value.ReadValue(xml);
                                else b = ReadInstruction(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.If, Data = (a, b) };
                    }
                case "Else":
                    {
                        Value a = null;
                        while (xml.Read())
                        {
                            if (xml.NodeType == XmlNodeType.EndElement) break;
                            if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "Value")
                            {
                                a = Value.ReadValue(xml);
                            }
                        }
                        return new Instruction { Type = InstructionType.Else, Data = a };
                    }
            }
            return new Instruction { Type = InstructionType.No_op, Data = null };
        }
    }
}
namespace pacednl.Misc
{
    public static class Tools
    {
        public static bool ListEquals<T>(List<T> x, List<T> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++)
            {
                if (!ReferenceEquals(x[i], y[i])) return false;
            }
            return true;
        }
    }
}
