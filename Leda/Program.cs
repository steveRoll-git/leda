using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", "return 1 .. 2 .. 3"), new ConsoleReporter());
        var b = (p.ParseBlock());
    }
}