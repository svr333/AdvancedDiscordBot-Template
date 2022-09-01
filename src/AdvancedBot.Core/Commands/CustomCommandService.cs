using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvancedBot.Core.Commands.Attributes;
using AdvancedBot.Core.Services;
using Discord;
using Discord.Commands;
using Humanizer;

namespace AdvancedBot.Core.Commands
{
    public class CustomCommandService : CommandService
    {
        public PaginatorService Paginator { get; set; }
        private readonly string _documentationUrl;
        private readonly string _sourceRepo;
        private readonly string _contributers;
        private readonly bool _botIsPrivate;

        public CustomCommandService() : base() { }

        public CustomCommandService(CustomCommandServiceConfig config) : base(config)
        {
            _documentationUrl = config.DocumentationUrl;
            _sourceRepo = config.RepositoryUrl;
            _contributers = config.Contributers;
            _botIsPrivate = config.BotInviteIsPrivate;
        }

        public async Task<IUserMessage> SendBotInfoAsync(ICommandContext context)
        {
            var documentation = string.IsNullOrEmpty(_documentationUrl) ? $"N/A" : $"[Click me!]({_documentationUrl})";
            var sourceRepo = string.IsNullOrEmpty(_sourceRepo) ? $"N/A" : $"[Click me!]({_sourceRepo})";
            var botInvite = _botIsPrivate ? $"Bot is private" : $"[Click me!](https://discordapp.com/api/oauth2/authorize?client_id={context.Client.CurrentUser.Id}&permissions=8&scope=bot)";

            var embed = new EmbedBuilder()
            {
                Title = "About the bot",
                Description = $"For a bare list of all commands, execute `!commands`\nFor a bare list of categories, execute `!modules`\n\n" +
                              $"**Documentation:** {documentation}\n\n**Source code:** {sourceRepo}\n\n" +
                              $"**Made possible by:** {_contributers}\n\n**Invite the bot:** {botInvite}",
                ThumbnailUrl = context.Client.CurrentUser.GetAvatarUrl(),
            }
            .WithFooter(context.User.Username, context.User.GetAvatarUrl())
            .Build();

            var message = await context.Channel.SendMessageAsync("", false, embed);
            return message;
        }

        public EmbedBuilder CreateModuleInfoEmbed(ModuleInfo module, string prefix)
        {
            var submodulesField = "";
            var commandsField = "";

            var topModule = module.IsSubmodule && module.Parent.Group != "TopModule"
                            ? $"This category is a subcategory of **{module.Parent.Name}**.\n"
                            : string.Empty;

            var embed = new EmbedBuilder()
            {
                Title = $"Info for category: {module.Name.Transform(To.SentenceCase)}",
                Description = $"{topModule}{module.Summary}\n\n",
                Color = Color.Purple
            }
            .WithFooter($"{"command".ToQuantity(module.Commands.Count)} | {"subcategory".ToQuantity(module.Submodules.Count)}");

            for (int i = 0; i < module.Submodules.Count; i++)
            {
                var currentModule = module.Submodules[i];

                var moduleName = currentModule.Name.Transform(To.SentenceCase);
                var commandCount = "command".ToQuantity(currentModule.Commands.Count);
                var subcategoryCount = "subcategory".ToQuantity(currentModule.Submodules.Count);

                submodulesField += 
                $"**{moduleName}** with {commandCount} and {subcategoryCount}\n" +
                $"{currentModule.Summary}\n\n";
            }

            for (int i = 0; i < module.Commands.Count; i++)
            {
                var currentCommand = module.Commands[i];

                commandsField += $"**{GenerateCommandUsage(currentCommand, prefix)}**\n{currentCommand.Summary}\n\n";
            }

            if (!string.IsNullOrEmpty(submodulesField)) embed.AddField($"Subcategories:", $"{submodulesField}");
            if (!string.IsNullOrEmpty(commandsField)) embed.AddField($"Commands:", commandsField);

            return embed;
        }

        public EmbedBuilder CreateCommandInfoEmbed(CommandInfo command, string prefix)
        {
            return new EmbedBuilder()
            {
                Title = GenerateCommandUsage(command, prefix),
                Description = command.Summary,
                Color = Color.Purple
            }
            .WithFooter("Tip: <> means mandatory, [] means optional");
        }

        public KeyValuePair<ModuleInfo, CommandInfo> AdvancedSearch(string input)
        {
            input = input.ToLower();
            var result = new Dictionary<ModuleInfo, CommandInfo>();

            var allCommandAliases = ListAllCommandAliases();
            var possibleCommand = allCommandAliases.FirstOrDefault(x => x == input);

            var allModuleAlliases = ListAllModuleAliases();
            var possibleModule = allModuleAlliases.FirstOrDefault(x => x == input);

            if (!string.IsNullOrEmpty(possibleModule) || possibleModule == possibleCommand && !string.IsNullOrEmpty(possibleModule))
            {
                var module = Modules.FirstOrDefault(x => x.Aliases.Contains(possibleModule));
                if (module is null) module = Modules.FirstOrDefault(x => x.Name == possibleModule);
                result.Add(module, null);
            }

            else if (!string.IsNullOrEmpty(possibleCommand))
            {
                var cmd = Commands.FirstOrDefault(x => x.Aliases.Contains(possibleCommand));
                result.Add(cmd.Module, cmd);
            }     

            else throw new Exception("Could not find a category or command for the given input.");

            return result.FirstOrDefault();
        }

        public string AllCommandsToString()
            => string.Join(", ", Commands.Select(x => $"{x.Aliases.First()}"));

        public string AllModulesToString()
            => string.Join(", ", Modules.Select(module => $"{module.Aliases.First()}").Where(alias => !string.IsNullOrEmpty(alias)));

        private string[] ListAllCommandAliases()
        {
            var aliases = new List<string>();
            var commands = Commands.ToArray();

            for (int i = 0; i < commands.Length; i++)
            {
                aliases.AddRange(commands[i].Aliases);
            }

            return aliases.ToArray();
        }

        private List<string> ListAllModuleAliases()
        {
            var aliases = new List<string>();
            var modules = Modules.ToList();

            for (int i = 0; i < modules.Count; i++)
            {
                aliases.AddRange(modules[i].Aliases);
                aliases.Add(modules[i].Name);
            }

            return aliases;
        }

        public string FormatCommandName(CommandInfo command)
            => $"{command.Module.Name}_{command.Name}".ToLower();

        public CommandInfo GetCommandInfo(string commandName)
        {
            var searchResult = Search(commandName);
            if (!searchResult.IsSuccess) throw new Exception(searchResult.ErrorReason);

            return searchResult.Commands.OrderBy(x => x.Command.Priority).FirstOrDefault().Command;
        }

        public string GenerateCommandUsage(CommandInfo command, string prefix)
        {
            StringBuilder parameters = new StringBuilder();

            for (int i = 0; i < command.Parameters.Count; i++)
            {
                var pref = command.Parameters[i].IsOptional ? "[" : "<";
                var suff = command.Parameters[i].IsOptional ? "]" : ">";
                
                parameters.Append($"{pref}{command.Parameters[i].Name.Underscore().Dasherize()}{suff} ");
            }
            
            return $"{prefix}{command.Aliases[0]} {parameters}";
        }

        public SlashCommandProperties[] GetDesiredSlashCommands()
        {
            var attributedCommands = GetSlashAttributeCommands();
            var slashCommands = new List<SlashCommandProperties>();

            for (int i = 0; i < attributedCommands.Length; i++)
            {
                /* Retrieve module stack (creating sub commands)
                 * you cannot create a slashcommand with a space in the name */
                var cmd = attributedCommands[i];
                var module = cmd.Module;

                var modules = new List<ModuleInfo>() { module };
                var scb = new SlashCommandBuilder();
                

                // case where module is not categorized and commands are individual
                if (!module.IsSubmodule && string.IsNullOrEmpty(module.Name))
                {
                    scb.WithName(cmd.Name);
                    scb.WithDescription(cmd.Summary);
                }
                // make sure to have every module
                else
                {
                    while (module.IsSubmodule)
                    {
                        module = module.Parent;
                        modules.Add(module);
                    };

                    modules.Reverse();

                    // first module becomes base command name
                    scb.WithName(modules[0].Name);
                    scb.WithDescription(modules[0].Summary);

                    // all in between modules need to be registered as subcommands
                    for (int j = 1; j < modules.Count - 1; j++)
                    {
                        scb.AddOption(modules[j].Name, ApplicationCommandOptionType.SubCommandGroup, modules[j].Summary);
                    }

                    // if command name is empty, the name of module becomes the command
                    if (string.IsNullOrEmpty(cmd.Name))
                    {
                        scb.Options?.Remove(scb.Options?.First());
                    }
                    else // add the command normally
                    {
                        scb.AddOption(cmd.Name, ApplicationCommandOptionType.SubCommand, cmd.Summary);
                    }
                }

                // add all parameters as options
                for (int j = 0; j < cmd.Parameters.Count; j++)
                {
                    var param = cmd.Parameters[j];
                    scb.AddOption(param.Name, ParameterToCommandOptionType(param), param.Summary, param.IsOptional);
                }

                slashCommands.Add(scb.Build());
            }

            return FilterSlashCommands(slashCommands.ToArray());
        }

        private SlashCommandProperties[] FilterSlashCommands(SlashCommandProperties[] commands)
        {
            var filteredCommands = new List<SlashCommandProperties>();

            for (int i = 0; i < commands.Length; i++)
            {
                var cmdName = commands[i].Name.Value;

                // check if we already handled this command
                if (filteredCommands.FirstOrDefault(x => x.Name.Value == cmdName) != null)
                {
                    continue;
                }

                var sharedCommands = commands.Where(x => x.Name.Value == cmdName).ToArray();

                // only one instance of this submodule found, add normally
                if (sharedCommands.Length <= 1)
                {
                    filteredCommands.Add(commands[i]);
                    continue;
                }

                var scb = new SlashCommandBuilder();

                scb.WithName(sharedCommands[0].Name.Value);
                scb.WithDescription(sharedCommands[0].Description.Value);
                scb.AddOption(AddSubmodules(ref scb, sharedCommands));

                filteredCommands.Add(scb.Build());
            }

            return filteredCommands.ToArray();
        }

        private SlashCommandOptionBuilder AddSubmodules(ref SlashCommandBuilder scb, SlashCommandProperties[] cmds)
        {
            for (int i = 0; i < cmds.Length; i++)
            {
                var sharedCommands = cmds.Where(x => x.Name.Value == cmds[i].Name.Value).ToArray();

                if (sharedCommands.Length > 1)
                {
                    scb.AddOption(cmds[i].Name.Value, ApplicationCommandOptionType.SubCommandGroup, cmds[i].Name.Value);
                    var subOption = AddSubmodules(ref scb, sharedCommands);
                }

                scb.AddOption(cmds[i].Name.Value, ApplicationCommandOptionType.SubCommand, cmds[i].Name.Value);
            }
        }

        private CommandInfo[] GetSlashAttributeCommands()
        {
            var slashCommands = new List<CommandInfo>();

            for (int i = 0; i < Modules.Count(); i++)
            {
                var module = Modules.ElementAt(i);

                for (int j = 0; j < module.Commands.Count; j++)
                {
                    var command = module.Commands[j];

                    if (command.Attributes.FirstOrDefault(x => x as SlashCommandAttribute != null) != null)
                    {
                        slashCommands.Add(command);
                    }
                }
            }

            return slashCommands.ToArray();
        }

        private ApplicationCommandOptionType ParameterToCommandOptionType(ParameterInfo param)
        {
            if (param.Type == typeof(double))
            {
                return ApplicationCommandOptionType.Number;
            }
            else if (param.Type == typeof(int))
            {
                return ApplicationCommandOptionType.Integer;
            }
            else if (param.Type == typeof(bool))
            {
                return ApplicationCommandOptionType.Boolean;
            }
            else if (param.Type == typeof(IChannel))
            {
                return ApplicationCommandOptionType.Channel;
            }
            else if (param.Type == typeof(IUser))
            {
                return ApplicationCommandOptionType.User;
            }
            else if (param.Type == typeof(IRole))
            {
                return ApplicationCommandOptionType.Role;
            }
            else
            {
                return ApplicationCommandOptionType.String;
            }
        }
    }
}
