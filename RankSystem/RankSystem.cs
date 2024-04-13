using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Linq;
using System.Threading;
using TShockAPI;
using TShockAPI.Hooks;
using Terraria;
using TerrariaApi.Server;
using MySql.Data.MySqlClient;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Xna.Framework;
using Terraria.ID;
using System.Timers;
using Timer = System.Timers.Timer;

namespace RankSystem
{
    [ApiVersion(2, 1)]
    public class RankSystem : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database DB;
        public static Config config;
        private static Timers _timers;

        private Timer _timer = new(5000);

        public static bool timerCheck = false;

        public override string Author
        {
            get { return "Average"; }
        }

        public override string Description
        {
            get { return "Automatic rank progression plugin."; }
        }

        public override string Name
        {
            get { return "RankSystem"; }
        }

        public override Version Version
        {
            get { return new Version(1, 0, 4); }
        }

        public RankSystem(Main game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            switch (TShock.Config.Settings.StorageType.ToLower())
            {
                case "sqlite":
                    _db = new SqliteConnection(("Data Source=" + Path.Combine(TShock.SavePath, "RankSystem.sqlite")));
                    break;
                case "mysql":
                    try
                    {
                        var host = TShock.Config.Settings.MySqlHost.Split(':');
                        _db = new MySqlConnection
                        {
                            ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
                                host[0],
                                host.Length == 1 ? "3306" : host[1],
                                TShock.Config.Settings.MySqlDbName,
                                TShock.Config.Settings.MySqlUsername,
                                TShock.Config.Settings.MySqlPassword
                            )
                        };
                    }
                    catch (MySqlException ex)
                    {
                        TShock.Log.Error(ex.ToString());
                        throw new Exception("MySQL not setup correctly.");
                    }

                    break;
                default:
                    throw new Exception("Invalid storage type.");
            }

            DB = new Database(_db);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            PlayerHooks.PlayerPostLogin += OnLogin;
            GeneralHooks.ReloadEvent += Reload;

            _timer.Elapsed += Timers.UpdateTimer;
            _timer.AutoReset = true;
            _timer.Enabled = true;
            _timer.Start();
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnLogin;
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= Reload;
            }

            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs e)
        {
            config = Config.Read();

            if (String.Equals(config.StartGroup, config.Groups[0].name, StringComparison.CurrentCultureIgnoreCase))
            {
                TShock.Log.ConsoleError("[RankSystem] Initialization cancelled due to config error: " +
                                        "StartGroup is same as first rank name");
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                return;
            }

            Commands.ChatCommands.Add(new Command("rs.user", Check, "check", "rank", "rankup", "playtime")
            {
                HelpText = "Displays information about your current and upcoming rank"
            });
            Commands.ChatCommands.Add(new Command("rs.user", Favorite, "favorite", "rankfav")
            {
                HelpText = "Set your favorite rank"
            });
            Commands.ChatCommands.Add(new Command("rs.admin", Admin, "rankadmin", "ra")
            {
                HelpText = "RankSystem admin commands"
            });
            Commands.ChatCommands.Add(new Command("rs.user", Ranks, "ranks", "ranklist")
            {
                HelpText = "Displays a list of available ranks"
            });
            Commands.ChatCommands.Add(new Command("rs.user", Leaderboard, "top", "topplaytime", "leaderboard")
            {
                HelpText = "Displays the top 10 players by playtime"
            });
        }

        private void Leaderboard(CommandArgs args)
        {
            var player = args.Player;

            var topPlayers = DB.GetTopPlayers(10);

            if (topPlayers.Count == 0)
            {
                player.SendErrorMessage("No players found!");
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine("\u2b50 Top 10 Players by Playtime \u2b50");

            foreach (var kvp in topPlayers)
            {
                var usersGroup = TShock.Groups.GetGroupByName(kvp.Key.Group);
                string prefix = string.Empty;

                if (usersGroup != null)
                {
                    prefix = FormatPrefixFromRank(usersGroup);
                }

                var placement = topPlayers.ToList().IndexOf(kvp) + 1;

                // placement color, gold for top 3, silver for 4-6, bronze for 7-10
                Color placementColor = Color.Gold;

                if (placement > 3 && placement <= 6)
                {
                    placementColor = Color.Silver;
                }
                else if (placement > 6)
                {
                    placementColor = Color.Orange;
                }

                sb.AppendLine(
                    $"     [c/{placementColor.Hex3()}:{placement}.] {prefix} [c/{placementColor.Hex3()}:{kvp.Key.Name} with ][c/{Color.Silver.Hex3()}:{TimeSpan.FromSeconds(kvp.Value.TotalTime).ElapsedString()}]");
            }

            player.SendMessage(sb.ToString(), Color.Gold);
        }


        private void Ranks(CommandArgs args)
        {
            var player = args.Player;

            if (config.Groups.Count == 0)
            {
                player.SendErrorMessage("No ranks have been set up!");
                return;
            }

            StringBuilder sb = new();
            int totalWidth = "─────────────────────".Length;
            string ranksText = @"✴ Available Ranks ✴";

            if (args.Player.RealPlayer)
            {
                // center the text and make it look pretty for non console users
                sb.AppendLine($"       {ranksText}");
            }
            else
            {
                sb.AppendLine(ranksText);
            }

            sb.AppendLine("─────────────────────");

            foreach (var group in config.Groups)
            {
                var tshockGroup = TShock.Groups.GetGroupByName(group.name);

                if (tshockGroup == null)
                {
                    TShock.Log.ConsoleError(
                        $"A rank was discovered in the config that does not exist in TShock: {group.name}. This may cause issues!");
                    continue;
                }

                string prefix = FormatPrefixFromRank(tshockGroup);

                sb.AppendLine(
                    $" {prefix} [c/{Color.LightGreen.Hex3()}:───] [c/{Color.Silver.Hex3()}:{TimeSpan.FromSeconds(group.info.rankCost).ElapsedString()}]");
            }

            player.SendMessage(sb.ToString(), Color.Gold);
        }

        private string FormatPrefixFromRank(TShockAPI.Group tshockGroup)
        {
            string prefix = string.IsNullOrWhiteSpace(tshockGroup.Prefix) ? tshockGroup.Name : tshockGroup.Prefix;
            ;
            Color chatColor = new Color(tshockGroup.R, tshockGroup.G, tshockGroup.B);

            bool containedSquareBrackets = false;

            if (prefix.Contains("[c/") is false)
            {
                if (prefix.Contains("[") && prefix.Contains("]"))
                {
                    prefix = prefix.Replace("[", "").Replace("]", "");

                    // also remove trailing spaces
                    prefix = prefix.Trim();
                    containedSquareBrackets = true;
                }

                prefix = $"[c/{chatColor.Hex3()}:{prefix}]";
                if (containedSquareBrackets)
                {
                    prefix = $"[c/{chatColor.Hex3()}:[]{prefix}[c/{chatColor.Hex3()}:]] ";
                }
            }

            return prefix;
        }

        private void DisplayAdminHelp(CommandArgs args)
        {
            var player = args.Player;

            StringBuilder sb = new();
            sb.AppendLine($"[c/{Color.LightGreen.Hex3()}:RankSystem Admin Commands]");
            sb.AppendLine("/rankadmin reset <player> - Reset a player's rank");
            sb.AppendLine("/rankadmin modifytime <player> <time> - Modify a player's playtime");
            sb.AppendLine("/rankadmin addrank <rankName> <rankCost> <nextGroup/none> - Add a rank to the config");
            sb.AppendLine("/rankadmin modifyrank <rank> <property> <value> - Modify a rank's config values");
            sb.AppendLine("/rankadmin autosortranks - Sort the ranks by rank cost");
            
            player.SendMessage(sb.ToString(), Color.Gold);
        }

        private void Admin(CommandArgs args)
        {
            var player = args.Player;
            var subcmd = args.Parameters.ElementAtOrDefault(0);

            if (subcmd == default)
            {
                DisplayAdminHelp(args);
                return;
            }


            switch (subcmd)
            {
                case "r":
                case "reset":
                {
                    var playerToReset = args.Parameters.ElementAtOrDefault(1);

                    if (playerToReset == null)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: /rankadmin reset <player>");
                        return;
                    }

                    var playerInformation = DB.GrabPlayerFromAccountName(playerToReset);

                    if (playerInformation == null)
                    {
                        player.SendErrorMessage("That player does not exist! Maybe they have never logged in?");
                        return;
                    }

                    try
                    {
                        var success = DB.DeletePlayer(playerToReset);
                        if (success)
                        {
                            player.SendSuccessMessage($"You have successfully reset {playerToReset}'s rank!");
                        }
                    }
                    catch (Exception ex)
                    {
                        player.SendErrorMessage(
                            "Something went wrong! If you are a server admin, please check the logs for more information.");
                        TShock.Log.ConsoleError(
                            $"Something went wrong while trying to reset a player's rank: {ex.ToString()}");
                    }

                    return;
                }
                case "autosortranks":
                {
                    try
                    {
                        // sort by rank cost ascending
                        var groups = config.Groups.OrderBy(x => x.info.rankCost).ToList();

                        config.Groups = groups;
                        config.Write();
                    }
                    catch (Exception)
                    {
                        player.SendErrorMessage("Something went wrong! Please check the logs for more information.");
                        return;
                    }

                    player.SendSuccessMessage("You have successfully sorted the ranks by rank cost!");
                    break;
                }
                case "mt":
                case "modifytime":
                {
                    var playerToModify = args.Parameters.ElementAtOrDefault(1);
                    var timeToModify = args.Parameters.ElementAtOrDefault(2);

                    if (playerToModify == null || timeToModify == null)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: /rankadmin modifytime <player> <time>");
                        return;
                    }

                    if (int.TryParse(timeToModify, out var time) == false)
                    {
                        player.SendErrorMessage(
                            $"Invalid syntax! Proper syntax: /rankadmin modifytime {playerToModify} <time>");
                        return;
                    }

                    var playerInformation = DB.GrabPlayerFromAccountName(playerToModify);

                    if (playerInformation == null)
                    {
                        player.SendErrorMessage("That player does not exist! Maybe they have never logged in?");
                        return;
                    }

                    playerInformation.TotalTime = time;
                    DB.SavePlayer(playerInformation);
                    player.SendSuccessMessage($"You have successfully modified {playerToModify}'s playtime to {time}!");
                    return;
                }
                case "ar":
                case "addrank":
                {
                    var rankName = args.Parameters.ElementAtOrDefault(1);
                    var rankCost = args.Parameters.ElementAtOrDefault(2);
                    var nextGroup = args.Parameters.ElementAtOrDefault(3);

                    if (rankName == null || rankCost == null || nextGroup == null)
                    {
                        player.SendErrorMessage(
                            "Invalid syntax! Proper syntax: /rankadmin addrank <rankName> <rankCost> <nextGroup/none>");
                        return;
                    }

                    // check if rank exists in tshock
                    if (TShock.Groups.GetGroupByName(rankName) == null)
                    {
                        player.SendErrorMessage("That rank does not exist in TShock!");
                        return;
                    }

                    if (int.TryParse(rankCost, out var cost) == false)
                    {
                        player.SendErrorMessage(
                            $"You did not enter a valid integer value for the rank cost! Proper syntax: /rankadmin addrank {rankName} <rankCost> <nextGroup/none>");
                        return;
                    }

                    // check if next group exists in tshock
                    if (TShock.Groups.GetGroupByName(nextGroup) == null && nextGroup != "none")
                    {
                        player.SendErrorMessage("That next group does not exist in TShock!");
                        return;
                    }

                    var group = new Group(rankName, new RankInfo(nextGroup, cost, new Dictionary<int, int>()));
                    config.Groups.Add(group);
                    config.Write();
                    player.SendSuccessMessage($"You have successfully added the rank {rankName} to the config!");
                    return;
                }
                case "mr":
                case "modifyrank":
                {
                    // /rankadmin modifyrank <rank> <property> <value>

                    var rank = args.Parameters.ElementAtOrDefault(1);
                    var property = args.Parameters.ElementAtOrDefault(2);
                    var value = args.Parameters.ElementAtOrDefault(3);

                    var validProperties = new[] { "rankCost", "rankUnlocks", "nextGroup" };

                    if (rank == null || property == null || value == null)
                    {
                        player.SendErrorMessage(
                            "Invalid syntax! To modify a rank's config values, use /rankadmin modifyrank <rank> <property> <value>");
                        player.SendInfoMessage(
                            $"Available properties: {string.Join(", ", validProperties)}");
                        return;
                    }

                    var group = config.GetGroup(rank);
                    if (group == null)
                    {
                        player.SendErrorMessage($"Invalid group name: {rank}");
                        return;
                    }

                    if (validProperties.Any(x => string.Equals(x, property, StringComparison.OrdinalIgnoreCase)) ==
                        false)
                    {
                        player.SendErrorMessage($"Invalid property: {property}");
                        player.SendInfoMessage(
                            $"Available properties: {string.Join(", ", validProperties)}");
                        return;
                    }

                    switch (property.ToLower())
                    {
                        case "rankcost":
                        {
                            if (int.TryParse(value, out var rankCost) == false)
                            {
                                player.SendErrorMessage("Invalid value! Value must be an integer.");
                                return;
                            }

                            group.info.rankCost = rankCost;
                            config.Write();
                            player.SendSuccessMessage(
                                $"You have successfully modified {rank}'s rankCost to {rankCost}!");
                            break;
                        }
                        case "nextgroup":
                        {
                            // does group exist in tshock
                            var nextGroup = TShock.Groups.GetGroupByName(value);

                            if (nextGroup == null)
                            {
                                player.SendErrorMessage("Invalid value! Value must be a valid TShock group.");
                                return;
                            }

                            group.info.nextGroup = value;
                            config.Write();
                            player.SendSuccessMessage($"You have successfully modified {rank}'s nextGroup to {value}!");
                            break;
                        }
                        case "rankunlocks":
                        {
                            // for value, the user will input a comma separated list of item ids
                            // if the user wants to remove all items, they can input "none"

                            if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
                            {
                                group.info.rankUnlocks = null;
                                config.Write();
                                player.SendSuccessMessage(
                                    $"You have successfully removed all rank unlocks from {rank}!");
                                return;
                            }

                            var items = value.Split(',');
                            var itemIds = new List<int>();
                            foreach (var item in items)
                            {
                                if (int.TryParse(item, out var itemId) == false)
                                {
                                    player.SendErrorMessage(
                                        $"Invalid value! Value must be a comma separated list of item ids (integer). Example: /rankadmin modifyrank {rank} rankUnlocks 1,2,3 <quantity for all>");
                                    return;
                                }

                                itemIds.Add(itemId);
                            }

                            // see if they gave a quantity
                            var quantityInput = args.Parameters.ElementAtOrDefault(4);

                            if (int.TryParse(quantityInput, out var quantity) == false)
                            {
                                quantity = 1;
                            }

                            group.info.rankUnlocks = new Dictionary<int, int>();
                            foreach (var itemId in itemIds)
                            {
                                group.info.rankUnlocks.Add(itemId, quantity);
                            }

                            config.Write();
                            player.SendSuccessMessage(
                                $"You have successfully modified {rank}'s rankUnlocks to {value}!");
                            break;
                        }
                    }
                    break;
                }
                default:
                {
                    DisplayAdminHelp(args);
                    return;
                }
            }
        }


        private static void Reload(ReloadEventArgs args)
        {
            config = Config.Read();
        }

        internal static bool IsInPathingMode(TSPlayer player)
        {
            if (player.Group.Name == RankSystem.config.StartGroup
                || player.Group.Name == config.EndGroup)
            {
                return true;
            }

            return RankSystem.config.Groups.Any(g => g.name == player.Group.Name);
        }

        private static void Favorite(CommandArgs args)
        {
            if (IsInPathingMode(args.Player) is false)
            {
                args.Player.SendErrorMessage("You are not within the main pathing system!");
                return;
            }

            var playerInformation = args.Player.GetPlaytimeInformation();

            if (playerInformation == null)
            {
                args.Player.SendErrorMessage("Something went wrong!");
                return;
            }

            var favoriteInput = args.Parameters.ElementAtOrDefault(0);
            if (favoriteInput == null)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /favorite <your favorite rank <3>");
                return;
            }

            if (string.Equals(favoriteInput, "none", StringComparison.OrdinalIgnoreCase))
            {
                DB.SetFavorite(args.Player.Account.Name, "");

                // get the user group that they should have based on playtime
                var tempGroup = config.GetClosestGroup(playerInformation.TotalTime);

                if (tempGroup == null)
                {
                    args.Player.SendErrorMessage("Something went wrong!");
                    return;
                }

                // set the user's group
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(args.Player.Account.Name),
                    tempGroup.name);

                args.Player.SendSuccessMessage("Your favorite rank has been removed!");
                return;
            }

            // check if group even exists
            if (config.Groups.Any(x => string.Equals(x.name, favoriteInput, StringComparison.OrdinalIgnoreCase)) ==
                false)
            {
                args.Player.SendErrorMessage("That rank does not exist!");
                return;
            }

            // get config group
            var groupFromConfig = config.Groups.FirstOrDefault(x =>
                string.Equals(x.name, favoriteInput, StringComparison.OrdinalIgnoreCase));

            // check if the group is higher than the player's current group
            var usersGroup = config.GetClosestGroup(playerInformation.TotalTime);
            var usersGroupIndex = config.Groups.IndexOf(usersGroup);

            if (config.Groups.IndexOf(groupFromConfig) > usersGroupIndex)
            {
                args.Player.SendErrorMessage("You can't favorite a rank that is higher than your current rank!");
                return;
            }

            // get the group 
            var group = TShock.Groups.FirstOrDefault(x => x.Name == favoriteInput);

            if (group is null)
            {
                // not a valid tshock group anymore
                args.Player.SendErrorMessage("That rank does not exist!");
                return;
            }

            // set the player's favorite group
            DB.SetFavorite(args.Player.Account.Name, group.Name);

            // set the user's group
            TShock.UserAccounts.SetUserGroup(args.Player.Account, group.Name);
            args.Player.SendSuccessMessage($"Your favorite rank is now: {group.Name}");
            args.Player.SendInfoMessage("Use /favorite none to remove your favorite rank");
        }

        private static void Check(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                string str = string.Join("", args.Parameters);
                var player = DB.GrabPlayerFromAccountName(str);

                if (player == null)
                {
                    args.Player.SendMessage($"That player does not exist!", Color.IndianRed);
                    return;
                }

                args.Player.SendMessage(
                    $"{player.AccountName} has played for: {TimeSpan.FromSeconds(player.TotalTime).ElapsedString()}",
                    Color.IndianRed);

                var nextGroup = config.GetNextGroup(player.TotalTime);
                if (nextGroup != null)
                {
                    args.Player.SendMessage(
                        $"{player.AccountName}'s next rank ({nextGroup.name}) will unlock in: {config.GetTimeTillNextGroup(player.TotalTime)}",
                        Color.Orange);
                    return;
                }

                args.Player.SendMessage($"{player.AccountName} is at the final rank!", Color.LightGreen);

                return;
            }
            else
            {
                if (args.Player == TSPlayer.Server)
                {
                    return;
                }

                var p = args.Player;

                var player = p.GetPlaytimeInformation();
                if (player == null)
                {
                    args.Player.SendErrorMessage("Could not fetch your playtime information!");
                    return;
                }

                if (!DB.HasFavorite(p.Account.Name))
                {
                    if (player.ShouldRankup())
                    {
                        Timers.RankupUser(p);
                    }
                }

                args.Player.SendMessage(
                    $"You have played for: {TimeSpan.FromSeconds(player.TotalTime).ElapsedString()}",
                    Color.IndianRed);


                var newGroup = config.GetNextGroup(player.TotalTime);

                if (newGroup != null)
                {
                    args.Player.SendMessage(
                        $"Your next rank ({newGroup.name}) will unlock in: {config.GetTimeTillNextGroup(player.TotalTime)}",
                        Color.Orange);
                    return;
                }

                args.Player.SendMessage($"You are at the final rank!", Color.LightGreen);

                return;
            }
        }

        private static void OnLogin(PlayerPostLoginEventArgs args)
        {
            var p = args.Player;

            var playtimeInformation = DB.GrabPlayer(p);

            if (playtimeInformation is null)
            {
                return;
            }

            if (p.Group.Name == config.StartGroup) //starting rank/new player
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(p.Account.Name),
                    config.Groups.FirstOrDefault()?.name); //AutoStarts the player to the config's first rank.

            var favorite = playtimeInformation.Favorite;
            if (!string.IsNullOrWhiteSpace(favorite))
            {
                // get the group
                var group = TShock.Groups.FirstOrDefault(x => x.Name == favorite);
                if (group != null)
                {
                    TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(p.Account.Name),
                        group.Name);
                }
            }
        }
    }
}