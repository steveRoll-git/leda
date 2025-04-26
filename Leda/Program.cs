using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var p = new Parser(new("test", "if 1 then a() elseif false then b() end"), new ConsoleReporter());
        var b = (p.ParseBlock());
    }
}