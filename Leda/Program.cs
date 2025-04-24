using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var l = new Lexer(new Source("test", "local myThing = \"wow\nasdf"), new ConsoleReporter());
        while (!l.ReachedEnd)
        {
            l.ReadToken();
        }
    }
}