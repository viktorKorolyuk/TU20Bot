using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.DependencyInjection;
using TU20Bot.Configuration;

namespace TU20Bot {
    public class Handler {
        // Config
        private const char prefix = '-';
        private const bool showStackTrace = true;

        private readonly Client client;
        
        private readonly CommandService commands;
        private readonly ServiceProvider services;

        private readonly Random random = new Random();

        // Called by Discord.Net when it wants to log something.
        private static Task log(LogMessage message) {
            Console.WriteLine(message.Message);
            return Task.CompletedTask;
        }
        
        // Called by Discord.Net when the bot receives a message.
        private async Task messageReceived(SocketMessage message) {
            if (!(message is SocketUserMessage userMessage)) return;

            var prefixStart = 0;

            if (userMessage.HasCharPrefix(prefix, ref prefixStart)) {
                // Create Context and Execute Commands
                var context = new SocketCommandContext(client, userMessage);
                var result = await commands.ExecuteAsync(context, prefixStart, services);
                
                // Handle any errors.
                if (!result.IsSuccess && result.Error != CommandError.UnknownCommand) {
                    if (showStackTrace && result.Error == CommandError.Exception 
                            && result is ExecuteResult execution) {
                        await userMessage.Channel.SendMessageAsync(
                            $"```\n{execution.Exception.Message}\n\n{execution.Exception.StackTrace}\n```");
                    } else {
                        await userMessage.Channel.SendMessageAsync(
                            "Halt We've hit an error.\n```\n{result.ErrorReason}\n```");
                    }
                }
            }
        }

        private Task userLeft(SocketGuildUser user) {
            client.config.logs.Add(new LogEntry {
                logEvent = LogEvent.UserLeave,
                id = user.Id,
                name = user.Username,
                discriminator = user.DiscriminatorValue,
                time = DateTime.UtcNow
            });

            return Task.CompletedTask;
        }
        
        // Called when a user joins the server.
        private async Task userJoined(SocketGuildUser user) {
            client.config.logs.Add(new LogEntry {
                logEvent = LogEvent.UserJoin,
                id = user.Id,
                name = user.Username,
                discriminator = user.DiscriminatorValue,
                time = DateTime.UtcNow
            });
            
            var channel = (IMessageChannel) client.GetChannel(client.config.welcomeChannelId);

            var greetings = client.config.welcomeMessages;

            // Send welcome message.
            await channel.SendMessageAsync(greetings[random.Next(0, greetings.Count)] + $" <@{user.Id}>");
        }

        // Initializes the Message Handler, subscribe to events, etc.
        public async Task init() {
            client.Log += log;
            client.UserLeft += userLeft;
            client.UserJoined += userJoined;
            client.MessageReceived += messageReceived;

            await commands.AddModulesAsync(Assembly.GetEntryAssembly(), services);
        }

        public Handler(Client client) {
            this.client = client;
            
            commands = new CommandService();
            services = new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(commands)
                .BuildServiceProvider();
        }
    }
}
