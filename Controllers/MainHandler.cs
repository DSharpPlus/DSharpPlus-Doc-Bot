using DSharpPlus;
using DSharpPlusDocs.Handlers;
using System;
using System.Threading.Tasks;

namespace DSharpPlusDocs.Controllers
{
    public class MainHandler
    {
        public DiscordClient Client { get; private set; }

        public CommandHandler CommandHandler { get; private set; }
        public QueryHandler QueryHandler { get; private set; }

        public readonly string Prefix = "<@341606460720939008> ";

        public MainHandler(DiscordClient client)
        {
            Client = client;
            CommandHandler = new CommandHandler();
            QueryHandler = new QueryHandler();
        }

        public async Task InitializeEarlyAsync()
        {
            await CommandHandler.InitializeAsync(this);
            QueryHandler.Initialize();
        }

        public MainHandler() => new MainHandler();
    }
}
