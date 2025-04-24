using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        Lexer l = new Lexer(new Source("", "a=--asdf!!!!!\n3"));
        while (!l.ReachedEnd)
        {
            Console.WriteLine(l.ReadToken());
        }
    }
}