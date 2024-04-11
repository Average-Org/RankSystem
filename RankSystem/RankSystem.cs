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

            Commands.ChatCommands.Add(new Command("rs.user", Check, "check", "rank", "rankup")
            {
                HelpText = "Displays information about your current and upcoming rank"
            });
            Commands.ChatCommands.Add(new Command("rs.user", Favorite, "favorite")
            {
                HelpText = "Set your favorite rank"
            });
            Commands.ChatCommands.Add(new Command("rs.admin", Admin, "rankadmin")
            {
                HelpText = "RankSystem admin commands"
            });
        }

        private void Admin(CommandArgs args)
        {
            var player = args.Player;
            var subcmd = args.Parameters.ElementAtOrDefault(0);

            if (subcmd == default)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /rankadmin <reset|modifytime>");
                return;
            }


            switch (subcmd)
            {
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
                        player.SendErrorMessage("Something went wrong! If you are a server admin, please check the logs for more information.");
                        TShock.Log.ConsoleError($"Something went wrong while trying to reset a player's rank: {ex.ToString()}");
                    }

                    return;
                }
                case "modifytime":
                {
                    var playerToModify = args.Parameters.ElementAtOrDefault(1);
                    var timeToModify = args.Parameters.ElementAtOrDefault(2);
                    
                    if (playerToModify == null || timeToModify == null)
                    {
                        player.SendErrorMessage("Invalid syntax! Proper syntax: /rankadmin modifytime <player> <time>");
                        return;
                    }
                    
                    if(int.TryParse(timeToModify, out var time) == false)
                    {
                        player.SendErrorMessage($"Invalid syntax! Proper syntax: /rankadmin modifytime {playerToModify} <time>");
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
            }
        }


        private static void Reload(ReloadEventArgs args)
        {
            config = Config.Read();
        }

        private static void Favorite(CommandArgs args)
        {
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
                    $"You have played for: {TimeSpan.FromSeconds(player.TotalTime).ElapsedString()}", Color.IndianRed);


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
                    config.Groups[0].name); //AutoStarts the player to the config's first rank.

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