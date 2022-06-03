using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace DSharpPlus.DocBot.Pagination
{
    public static class MenuPaginator
    {
        /// <summary>
        /// Prunes expired pagination sessions.
        /// </summary>
        public static Timer ExpireTimer { get; } = new Timer(TimeSpan.FromSeconds(1).TotalMilliseconds);

        /// <summary>
        /// The current pagination sessions that we're managing.
        /// </summary>
        public static Dictionary<DiscordMessage, MenuIndex> CurrentPaginations { get; } = new();

        /// <summary>
        /// Creates a new instance of our naviagation buttons. Spawns new instances due to references.
        /// </summary>
        public static DiscordButtonComponent[] NavigationComponents => new[] {
            new DiscordButtonComponent(ButtonStyle.Secondary, "first", "⏮"),
            new DiscordButtonComponent(ButtonStyle.Secondary, "previous", "◀"),
            new DiscordButtonComponent(ButtonStyle.Secondary, "cancel", "❌"),
            new DiscordButtonComponent(ButtonStyle.Secondary, "next", "▶"),
            new DiscordButtonComponent(ButtonStyle.Secondary, "last", "⏭")
        };

        static MenuPaginator()
        {
            ExpireTimer.Elapsed += ExpirePaginationsAsync;
            ExpireTimer.Start();
        }

        /// <summary>
        /// Go to the user requested page.
        /// </summary>
        /// <param name="_">The unused DiscordClient.</param>
        /// <param name="eventArgs">The component interaction event args.</param>
        /// <returns>A ✨ Task ✨</returns>
        public static Task PaginateAsync(DiscordClient _, ComponentInteractionCreateEventArgs eventArgs)
        {
            // Attempt to find the pagination session
            (DiscordMessage message, MenuIndex menuIndex) = CurrentPaginations.FirstOrDefault(x => x.Key.Id == eventArgs.Message.Id);

            // None was found
            if (message is null)
            {
                return Task.CompletedTask;
            }
            // It's a different user attempting to hijack someone else's pagination session
            else if (menuIndex.Author != eventArgs.User)
            {
                // TODO: Spawn a new pagination for this user
                return Task.CompletedTask;
            }

            DiscordMessageBuilder messageBuilder = new();
            if (eventArgs.Values.Length == 1)
            {
                // The dropdown was used
                messageBuilder = menuIndex.Set(eventArgs.Values[0]).Message;
            }
            else
            {
                // The navigation buttons were used
                switch (eventArgs.Interaction.Data.CustomId)
                {
                    case "first":
                        messageBuilder = menuIndex.First().Message;
                        break;
                    case "previous":
                        messageBuilder = menuIndex.Previous().Message;
                        break;
                    case "next":
                        messageBuilder = menuIndex.Next().Message;
                        break;
                    case "last":
                        messageBuilder = menuIndex.Last().Message;
                        break;
                    case "cancel":
                        CurrentPaginations.Remove(message);
                        return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder(GetCancelledMessage(menuIndex)));
                    default:
                        // This usually happens when an unfinished feature is implemented
                        throw new ArgumentException("Invalid interaction data.", nameof(eventArgs));
                }
            }

            // Clear the previous components and update the dropdown
            messageBuilder.ClearComponents();
            messageBuilder.AddComponents(NavigationComponents);
            messageBuilder.AddComponents(new DiscordSelectComponent("set", menuIndex.Pages[menuIndex.CurrentIndex].Title, menuIndex.Pages.Select(x => new DiscordSelectComponentOption(x.Title, x.Title))));
            return eventArgs.Interaction.CreateResponseAsync(InteractionResponseType.UpdateMessage, new DiscordInteractionResponseBuilder(messageBuilder));
        }

        /// <summary>
        /// Creates a new pagination session.
        /// </summary>
        /// <param name="author">Who the session belongs to.</param>
        /// <param name="channel">The channel that the session is in.</param>
        /// <param name="pages">The pages to iterate through.</param>
        /// <returns>A ✨ Task ✨</returns>
        public static async Task SendNewPaginationAsync(DiscordUser author, DiscordChannel channel, params MenuPagination[] pages)
        {
            try
            {
                MenuIndex menuIndex = new(author, pages);
                DiscordMessageBuilder messageBuilder = pages[0].Message;
                messageBuilder.ClearComponents();
                messageBuilder.AddComponents(NavigationComponents);
                messageBuilder.AddComponents(new DiscordSelectComponent("set", menuIndex.Pages[menuIndex.CurrentIndex].Title, menuIndex.Pages.Select(x => new DiscordSelectComponentOption(x.Title, x.Title))));

                DiscordMessage message = await channel.SendMessageAsync(messageBuilder);
                CurrentPaginations.Add(message, menuIndex);
            }
            catch (Exception)
            {
                // Any exceptions found here aren't our problem. This could happen when the bot doesn't have send message perms in a guild, in which case we should probably DM the menu instead.
                // TODO: DM the menu when we don't have send permissions in a channel. Shouldn't require anything more than another catch block, remember the docs command has a RequireBotPermission attribute.
                throw;
            }
        }

        /// <summary>
        /// Removes expired pagination sessions from when the user hasn't interacted with them in the past 30 seconds.
        /// </summary>
        /// <param name="sender">The timer.</param>
        /// <param name="e">The timer event args.</param>
        private static async void ExpirePaginationsAsync(object? sender, ElapsedEventArgs e)
        {
            foreach ((DiscordMessage message, MenuIndex menuIndex) in CurrentPaginations.ToArray())
            {
                // Unix timestamps was intended to be used for less memory consumption, however this may or may not have been a 2am micro-optimization.
                if (menuIndex.LastUpdate >= ((DateTimeOffset)DateTime.UtcNow.AddSeconds(30)).ToUnixTimeMilliseconds())
                {
                    CurrentPaginations.Remove(message);
                    await message.ModifyAsync(x => x = GetCancelledMessage(menuIndex));
                }
            }
        }

        /// <summary>
        /// Returns a message builder for a cancelled pagination session. Disables the dropdown and navigation buttons.
        /// </summary>
        /// <param name="menuIndex">Which pagination session to cancel.</param>
        /// <returns>The non-user interactable discord message builder</returns>
        public static DiscordMessageBuilder GetCancelledMessage(MenuIndex menuIndex)
        {
            // Don't change the current page
            DiscordMessageBuilder x = menuIndex.Pages[menuIndex.CurrentIndex].Message;

            // Disable the dropdown and navigation buttons
            x.ClearComponents();
            x.AddComponents(NavigationComponents.ToArray().Select(x => x.Disable()));
            x.AddComponents(new DiscordSelectComponent("set", menuIndex.Pages[menuIndex.CurrentIndex].Title, menuIndex.Pages.Select(x => new DiscordSelectComponentOption(x.Title, x.Title)), true));
            return x;
        }
    }
}
