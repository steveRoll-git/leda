using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var source = new Source("test", """
                                        local g: function(a: string): number, string = function(a: string): number, number end
                                        g()
                                        """);
        source.Parse(new ConsoleReporter());
        Console.WriteLine(Emitter.Emit(source.Tree));
        source.Bind(new ConsoleReporter());
        source.Check(new ConsoleReporter());
    }
}