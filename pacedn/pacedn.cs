using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pacednl;

//This is the general user interface, doesn't have function on it's own

namespace pacedn
{
    class Program
    {
        static void Main(string[] args)
        {
            var x = new Library();
            x.Items.Add()

            //generic shell rather than command line arguments
            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                var words = input.Split();
                if (words.Length == 0) continue;
                switch (words[0].ToLower())
                {
                    default: continue;
                    case "?":
                    case "help":
                        Console.WriteLine("List of commands");
                        break;
                }
            }
        }
    }
}
