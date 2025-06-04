using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Server;

namespace Leda.LSP;

class Program
{
    static async Task Main(string[] args)
    {
        Debugger.Launch();
        while (!Debugger.IsAttached)
        {
            await Task.Delay(100);
        }

        // TODO support TCP
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();

        var server = LanguageServer.From(input, output);
        server.OnInitialize((_, info) =>
        {
            info.Name = "Leda";
            info.Version = Assembly.GetEntryAssembly()!.GetName().Version?.ToString();
            Console.Error.WriteLine("initialize");
            return Task.CompletedTask;
        });

        server.OnInitialized(async (_) =>
        {
            Console.Error.WriteLine("initialized");
            var r = await server.Client.GetConfiguration(new ConfigurationParams()
            {
                Items = []
            }, CancellationToken.None);

            Console.Error.WriteLine(r);
        });

        server.AddHandler(new TextDocumentHandler(server));
        server.AddHandler(new HoverHandler());

        await server.Run();
    }
}