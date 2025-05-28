using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var source = new Source("test", """
                                        local f = function(n: number, s: string) end
                                        local a = f(123, {})
                                        """);
        source.Parse(new ConsoleReporter());
        Console.WriteLine(Emitter.Emit(source.Tree));
        source.Bind(new ConsoleReporter());
        source.Check(new ConsoleReporter());
    }
}