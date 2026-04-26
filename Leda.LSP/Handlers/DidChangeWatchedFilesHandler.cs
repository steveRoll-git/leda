using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.Registration;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceWatchedFile;
using EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceWatchedFile.Watch;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using FileSystemWatcher =
    EmmyLua.LanguageServer.Framework.Protocol.Message.WorkspaceWatchedFile.Watch.FileSystemWatcher;

namespace Leda.LSP;

public class DidChangeWatchedFilesHandler(LedaServer server) : DidChangeWatchedFilesHandlerBase
{
    protected override Task Handle(DidChangeWatchedFilesParams request, CancellationToken token)
    {
        foreach (var change in request.Changes)
        {
            if (change.Type == FileChangeType.Created)
            {
                server.AddSource(change.Uri);
            }
            else if (change.Type == FileChangeType.Deleted)
            {
                server.RemoveSource(change.Uri);
            }
        }

        return Task.CompletedTask;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities) { }

    public override void RegisterDynamicCapability(LanguageServer languageServer, ClientCapabilities clientCapabilities)
    {
        var dynamicRegistration = new DidChangeWatchedFilesRegistrationOptions
        {
            Watchers =
            [
                new FileSystemWatcher
                {
                    GlobalPattern = "**/*.leda",
                    Kind = WatchKind.Create | WatchKind.Delete,
                },
            ],
        };

        languageServer.Client.DynamicRegisterCapability(new RegistrationParams
        {
            Registrations =
            [
                new Registration
                {
                    Id = Guid.NewGuid().ToString(),
                    Method = "workspace/didChangeWatchedFiles",
                    RegisterOptions = dynamicRegistration,
                },
            ],
        });
    }
}