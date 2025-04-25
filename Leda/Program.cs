using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", "thing = (1 + ) * 3) a()"), new ConsoleReporter());
        var b = (p.ParseBlock());
    }
}