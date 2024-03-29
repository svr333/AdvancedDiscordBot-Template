﻿using Discord;
using Discord.WebSocket;
using AdvancedBot.Core.Services.DataStorage;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using AdvancedBot.Core.Commands;
using AdvancedBot.Core.Services;
using Discord.Interactions;
using System.Reflection;
using AdvancedBot.Core.Entities;

namespace AdvancedBot.Core
{
    public class BotClient
    {
        private DiscordSocketClient _client;
        private CustomCommandService _commands;
        private IServiceProvider _services;
        private InteractionService _interactions;
        private AccountService _accounts;

        public BotClient(CustomCommandService commands = null, DiscordSocketClient client = null)
        {
            _client = client ?? new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = true,
                MessageCacheSize = 1000
            });

            _commands = commands ?? new CustomCommandService(new CustomCommandServiceConfig
            {
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Info,
                BotInviteIsPrivate = true,
                RepositoryUrl = "https://github.com/svr333/AdvancedBot-Template"
            });

            _interactions = new InteractionService(_client.Rest, new InteractionServiceConfig());
        }

        public async Task InitializeAsync()
        {
            Console.Title = $"Launching Discord Bot...";
            _services = ConfigureServices();
            _accounts = _services.GetRequiredService<AccountService>();

            _client.Ready += OnReadyAsync;
            _interactions.SlashCommandExecuted += OnSlashCommandExecuted;

            _client.Log += LogAsync;
            _commands.Log += LogAsync;

            var token = Environment.GetEnvironmentVariable("Token");

            await Task.Delay(10).ContinueWith(t => _client.LoginAsync(TokenType.Bot, token));
            await _client.StartAsync();

            await Task.Delay(-1);
        }

        private Task LogAsync(LogMessage msg)
        {
            if (msg.Exception != null)
            {
                Console.WriteLine($"{msg.Source}: {msg.Exception.Message}");
            }
            else
            {
                Console.WriteLine($"{msg.Source}: {msg.Message}");
            }

            return Task.CompletedTask;
        }

        private async Task OnReadyAsync()
        {
            Console.Title = $"Running Discord Bot: {_client.CurrentUser.Username}";
            await _client.SetGameAsync("Being a bot");
            Console.WriteLine($"Guild count: {_client.Guilds.Count}");

            await _interactions.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);
            Console.WriteLine($"Modules count: {_interactions.Modules.Count}");
            Console.WriteLine($"SlashCommands count: {_interactions.SlashCommands.Count}");

            #if DEBUG
                Console.WriteLine("Registered all commands to test server");
                await _interactions.RegisterCommandsToGuildAsync(696343127144923158);
            #else
                Console.WriteLine("Registered all commands globally");
                await _interactions.RegisterCommandsGloballyAsync();
            #endif

            _client.InteractionCreated += async (x) =>
            {
                var context = new SocketInteractionContext(_client, x);
                await _interactions.ExecuteCommandAsync(context, _services);
            };
        }

        private async Task OnSlashCommandExecuted(SlashCommandInfo cmd, IInteractionContext context, IResult result)
        {
            if (!result.IsSuccess)
            {
                await context.Interaction.ModifyOriginalResponseAsync(x => x.Content = $"⛔ {result.ErrorReason}");
            }

            var id = context.Interaction.IsDMInteraction ? context.User.Id : context.Guild.Id;
            var acc = _accounts.GetOrCreateAccount(id, !context.Interaction.IsDMInteraction);

            var cmdInfo = acc.CommandInfos.Find(x => x.Name == cmd.Name);

            if (cmdInfo == null)
            {
                acc.CommandInfos.Add(new CommandInfo(cmd.Name));
                cmdInfo = acc.CommandInfos.Find(x => x.Name == cmd.Name);
            }

            cmdInfo.TimesRun++;

            if (!result.IsSuccess)
            {
                cmdInfo.TimesFailed++;
            }

            _accounts.SaveAccount(acc);
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<LiteDBHandler>()
                .AddSingleton<AccountService>()
                .AddSingleton<PaginatorService>()
                .BuildServiceProvider();
        }
    }
}
