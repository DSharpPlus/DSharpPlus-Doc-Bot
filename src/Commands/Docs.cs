using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;

namespace DSharpPlus.DocBot.Commands
{
    public sealed class Docs : BaseCommandModule
    {
        public IPaginatorService PaginatorService { private get; init; } = null!;
        public IDocumentationService DocumentationService { private get; init; } = null!;

        [Command("docs")]
        [Description("Searches the documentation for a given type.")]
        public async Task DocsAsync(CommandContext context, [Description("The type to search for.")] string searchRequest)
        {
            IEnumerable<Page> pages = DocumentationService.Search(searchRequest);
            int pageCount = pages.Count();
            if (pageCount == 0)
            {
                await context.RespondAsync("No results found.");
            }
            else if (pageCount == 1)
            {
                await context.RespondAsync(pages.First().Content!, pages.First().Embed!);
            }
            else
            {
                Paginator paginator = PaginatorService.CreatePaginator(pages, context.User);
                paginator.CurrentMessage = await context.RespondAsync(paginator.GenerateMessage());
            }
        }
    }
}
