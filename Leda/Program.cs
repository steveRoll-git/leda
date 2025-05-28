using Leda.Lang;

namespace Leda;

class Program
{
    static void Main(string[] args)
    {
        var source = new Source("test", """
                                        local f: function = function() end
                                        local a = f()
                                        """);
        source.Parse(new ConsoleReporter());
        Console.WriteLine(Emitter.Emit(source.Tree));
        source.Bind(new ConsoleReporter());
        source.Check(new ConsoleReporter());
    }
}