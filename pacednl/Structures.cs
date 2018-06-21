using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SSSerializer
{
    public class Node
    {
    }
    public class StringNode : Node
    {
        public string Value { get; set; }

        public static implicit operator StringNode(string value)
        {
            return new StringNode { Value = value.Replace("\"", "\\\"") };
        }
        public static implicit operator string(StringNode value)
        {
            return value.Value.Replace("\\\"", "\"");
        }
    }
    public class ObjectNode : Node
    {
        public ObjectNode()
        {
            Items = new Dictionary<string, Node>();
        }
        public ObjectNode(Dictionary<string, Node> x)
        {
            Items = x;
        }

        public Dictionary<string, Node> Items { get; set; }
        public Node this[string key]
        {
            get
            {
                if (Items.TryGetValue(key, out var v)) return v;
                return null;
            }
        }
    }
    public class ArrayNode : Node
    {
        public ArrayNode()
        {
            Items = new List<Node>();
        }
        public ArrayNode(List<Node> x)
        {
            Items = x;
        }

        public List<Node> Items { get; set; }
    }
}