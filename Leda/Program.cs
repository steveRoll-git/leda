using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        Lexer l = new Lexer(new Source("", "a = [=[abc[]\n1]=]23]=] 456.4"));
        while (!l.ReachedEnd)
        {
            Console.WriteLine(l.ReadToken());
        }
    }
}