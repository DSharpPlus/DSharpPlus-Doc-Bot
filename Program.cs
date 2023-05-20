// This file is part of the DSharpPlus project.
//
// Copyright (c) 2015 Mike Santiago
// Copyright (c) 2016-2022 DSharpPlus Contributors
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlusDocs.Controllers;
using Microsoft.Extensions.Logging;

namespace DSharpPlusDocs
{
    public class Program : IDisposable
    {
        private static DiscordClient _client;
        private static MainHandler _mainHandler;

        public static async Task Main()
        {
            _client = new DiscordClient(new DiscordConfiguration
            {
                MinimumLogLevel = LogLevel.Debug,
                MessageCacheSize = 1024,
                TokenType = TokenType.Bot,
                Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN")
            });

            _client.Ready += (client, eventArgs) =>
            {
                Console.WriteLine("Connected!");
                return Task.CompletedTask;
            };

            _mainHandler = new MainHandler(_client);
            await _mainHandler.InitializeEarlyAsync();

            await _client.ConnectAsync();
            await Task.Delay(-1);
        }

        public void Dispose()
        {
            _client.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
