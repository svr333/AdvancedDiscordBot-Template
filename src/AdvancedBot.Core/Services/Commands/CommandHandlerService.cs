﻿using Discord;
using Discord.Commands;
using Discord.WebSocket;
using AdvancedBot.Core.Extensions;
using AdvancedBot.Core.Services.DataStorage;
using System;
using System.Reflection;
using System.Threading.Tasks;
using AdvancedBot.Core.Commands;
using AdvancedBot.Core.Commands.TypeReaders;
using System.Linq;

namespace AdvancedBot.Core.Services.Commands
{
    public class CommandHandlerService
    {
        private readonly DiscordSocketClient _client;
        private readonly CustomCommandService _commands;
        private readonly IServiceProvider _services;
        private readonly GuildAccountService _accounts;

        public CommandHandlerService(DiscordSocketClient client, CustomCommandService commands, IServiceProvider services, GuildAccountService accounts)
        {
            _commands = commands;
            _client = client;
            _services = services;
            _accounts = accounts;
        }

        public async Task InitializeAsync()
        {
            _commands.AddTypeReader<IUser>(new IUserTypeReader(), true);
            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _services);

            await InitializeSlashCommands();

            _client.SlashCommandExecuted += OnSlashCommandExecuted;
            _client.MessageReceived += OnMessageReceived;
            _commands.CommandExecuted += OnCommandExecuted;
        }

        private async Task InitializeSlashCommands()
        {
            var desiredCommands = _commands.GetDesiredSlashCommands();
            //var existingCommands = (await _client.GetGlobalApplicationCommandsAsync()).ToArray();

            var b = await _client.GetApplicationInfoAsync();

            await _client.BulkOverwriteGlobalApplicationCommandsAsync(desiredCommands);

            // for (int i = 0; i < desiredCommands.Length; i++)
            // {
            //     // only create if nothing similar was found
            //     if (existingCommands.FirstOrDefault(
            //         x => x.Name == desiredCommands[i].Name.GetValueOrDefault()
            //         && x.Description == desiredCommands[i].Description.GetValueOrDefault()
            //         && x.Options.Count == desiredCommands[i].Options.GetValueOrDefault().Count) == null)
            //     {
            //         await _client.CreateGlobalApplicationCommandAsync(desiredCommands[i]);
            //     }
            // }
        }

        private async Task OnSlashCommandExecuted(SocketSlashCommand cmd)
        {
            await cmd.DeferAsync();
            var msg = await cmd.GetOriginalResponseAsync();

            var context = new CommandContext(_client, msg);
            await _commands.ExecuteAsync(context, 0, _services);
        }

        private async Task OnMessageReceived(SocketMessage msg)
        {
            if (!(msg is SocketUserMessage message)) return;
            if (message.Author == _client.CurrentUser) { return; }
            
            if (message.Channel is IPrivateChannel)
            {
                await message.Channel.SendMessageAsync($"I only respond in guilds.");
                return;
            }

            var guildId = (message.Author as SocketGuildUser).Guild.Id;
            var guild = _accounts.GetOrCreateGuildAccount(guildId);

            int argPos = 0;
            if (!message.HasPrefix(_client, out argPos, guild.Prefixes)) 
                return;

            var context = new SocketCommandContext(_client, message);
            var result = await _commands.ExecuteAsync(context, argPos, _services);
        }

        private async Task OnCommandExecuted(Optional<CommandInfo> cmd, ICommandContext ctx, IResult result)
        {
            var guild = _accounts.GetOrCreateGuildAccount(ctx.Guild.Id);

            if (result.IsSuccess) 
            {
                try
                {
                    if (guild.Commands.Find(x => x.Name == _commands.FormatCommandName(cmd.Value)).DeleteOriginalMessage)
                    {
                        await ctx.Message.DeleteAsync();
                    }
                    else await ctx.Message.AddReactionAsync(new Emoji("✅"));
                }
                catch{} // message no longer exists, just ignore

                return;
            }

            if (result.Error == CommandError.UnknownCommand) return;
            else if (result.Error == CommandError.BadArgCount)
            {
                await SendWrongParameterCountMessage(ctx, cmd.Value, guild.DefaultDisplayPrefix);
                return;
            }
            

            switch (result.ErrorReason)
            {
                case "User has insuffient permission to execute command.": break;

                default: await SendDefaultErrorMessage(ctx, cmd.Value, result.ErrorReason);
                break;
            }
        }

        private async Task SendDefaultErrorMessage(ICommandContext ctx, CommandInfo cmd, string error)
        {
            error = error.StartsWith("Could not find file") ? "Requested file not found." : error;

            var embed = new EmbedBuilder()
            {
                Color = Color.DarkOrange,
                Title = $"{error}",
            }
            .Build();
        
            await ctx.Channel.SendMessageAsync("", false, embed);
        }
    
        private async Task SendWrongParameterCountMessage(ICommandContext ctx, CommandInfo command, string prefix)
        {
            var usage = _commands.GenerateCommandUsage(command, prefix);

            var embed = new EmbedBuilder()
            .WithTitle("Wrongly executed, correct example:")
            .WithDescription(usage)
            .WithFooter("Tip: <> means mandatory, [] means optional")
            .WithColor(Color.Red)
            .Build();

            await ctx.Channel.SendMessageAsync("", false, embed);
        }
    }
}
