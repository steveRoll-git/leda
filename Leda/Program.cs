using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var source = new Source("test", """
                                        local g = function(a: number) end
                                        local n = 123
                                        g(n)
                                        """);
        source.Parse(new ConsoleReporter(source));
        Console.WriteLine(Emitter.Emit(source.Tree));
        source.Bind(new ConsoleReporter(source));
        source.Check(new ConsoleReporter(source));
    }
}