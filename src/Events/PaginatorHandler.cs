using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.DocBot.Interfaces;
using DSharpPlus.DocBot.Types;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;

namespace DSharpPlus.DocBot.Events
{
    public sealed class PaginatorHandler
    {
        public static async Task PaginateAsync(DiscordClient client, ComponentInteractionCreateEventArgs eventArgs)
        {
            IPaginatorService paginatorService = client.GetCommandsNext().Services.GetRequiredService<IPaginatorService>();

            string? componentId = eventArgs.Message.Components.FirstOrDefault()?.Components.FirstOrDefault()?.CustomId;
            if (componentId == null || !Ulid.TryParse(componentId.Split('-')[0], out Ulid id))
            {
                return;
            }

            Paginator? paginator = paginatorService.GetPaginator(id);
            if (paginator == null)
            {
                return;
            }
            else if (paginator.Author.Id != eventArgs.User.Id)
            {
                paginator = paginatorService.CreatePaginator(paginator, eventArgs.User, paginator.CurrentMessage);
                paginator.Interaction = eventArgs.Interaction;
                DiscordMessageBuilder? response = HandlePagination(paginator, eventArgs, paginatorService);
                if (response != null)
                {
                    await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder(response).AsEphemeral());
                }
            }
            else
            {
                paginator.CurrentMessage = eventArgs.Message;
                paginator.Interaction = eventArgs.Interaction;
                DiscordMessageBuilder? response = HandlePagination(paginator, eventArgs, paginatorService);
                if (response != null)
                {
                    await eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new(response));
                }
            }
        }

        private static DiscordMessageBuilder? HandlePagination(Paginator paginator, ComponentInteractionCreateEventArgs eventArgs, IPaginatorService paginatorService)
        {
            if (eventArgs.Values.Length != 0)
            {
                string instruction = string.Join('-', eventArgs.Values[0].Split('-').Skip(1));
                return instruction switch
                {
                    "select-next" => paginator.GotoPage(paginator.GetNextSection()),
                    "select-previous" => paginator.GotoPage(paginator.GetPreviousSection()),
                    _ when int.TryParse(instruction, NumberStyles.Number, CultureInfo.InvariantCulture, out int pageNumber) => paginator.GotoPage(pageNumber),
                    _ => null
                };
            }
            else
            {
                string instruction = eventArgs.Interaction.Data.CustomId.Split('-')[1];
                // If the instruction is cancel and the message is either invoked by the user OR ephemeral, cancel the paginator.
                if (instruction == "cancel" && (eventArgs.User.Id == eventArgs.Message.Reference?.Message.Author.Id || (eventArgs.Message.Flags?.HasMessageFlag(MessageFlags.Ephemeral) ?? false)))
                {
                    DiscordMessageBuilder response = paginator.Cancel();
                    paginatorService.RemovePaginatorAsync(paginator.Id, false).GetAwaiter().GetResult();
                    return response;
                }

                return instruction switch
                {
                    "first" => paginator.FirstPage(),
                    "last" => paginator.LastPage(),
                    "next" => paginator.NextPage(),
                    "previous" => paginator.PreviousPage(),
                    _ => null,
                };
            }
        }
    }
}
