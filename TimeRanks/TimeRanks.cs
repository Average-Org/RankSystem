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
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace TimeRanks //simplified from White's TimeBasedRanks plugin
{
    [ApiVersion(1,17)]
    public class TimeRanks : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database dbManager;
        public static Config config = new Config();
        private static Timers _timers;

        internal static readonly TrPlayers Players = new TrPlayers();

        public override string Author
        {
            get { return "White/Bippity"; }
        }

        public override string Description
        {
            get { return "Simplified Timed-Based Ranks"; }
        }

        public override string Name
        {
            get { return "Timed Ranks"; }
        }

        public override Version Version
        {
            get { return new Version(0, 1); }
        }

        public TimeRanks(Main game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "sqlite":
                    _db = new SqliteConnection(string.Format("uri=file://{0},Version=3",
                        Path.Combine(TShock.SavePath, "TimeRanksData.sqlite")));
                    break;
                case "mysql":
                    try
                    {
                        var host = TShock.Config.MySqlHost.Split(':');
                        _db = new MySqlConnection
                        {
                            ConnectionString = String.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4}",
                            host[0],
                            host.Length == 1 ? "3306" : host[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword
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

            dbManager = new Database(_db);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            PlayerHooks.PlayerPostLogin += PostLogin;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);

                var t = new Thread(delegate()
                    {
                        dbManager.SaveAllPlayers();
                        TShock.Log.ConsoleInfo("Saved players successfully");
                    });
                t.Start();
                t.Join();
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs e)
        {
            var configPath = Path.Combine(TShock.SavePath, "TimeRanks.json");
            (config = Config.Read(configPath)).Write(configPath);

            _timers = new Timers();
            _timers.Start();

            if (config.Groups.Keys.Count > 0) //is this needed?
                if (String.Equals(config.StartGroup, config.Groups.Keys.ToList()[0], StringComparison.CurrentCultureIgnoreCase))
                {
                    TShock.Log.ConsoleError("[TimeRanks] Initialization cancelled due to config error: " + "StartGroup is same as first rank name");
                    ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                    return;
                }
            
            Commands.ChatCommands.Add(new Command("tbr.rank.check", Check, "check", "checktime", "rank")
            {
                HelpText = "Displays information about your current and upcoming rank"
            });
            Commands.ChatCommands.Add(new Command("tbr.rank.admin", Delete, "rankdelete")
            {
                HelpText = "Deletes a player's rank from the database"
            });
            dbManager.InitialSyncPlayers();
        }

        private static void Check(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var str = string.Join(" ", args.Parameters);
                var players = Players.GetListByUsername(str).ToList();
                var tsplayers = TShock.Utils.FindPlayer(str);

                if (tsplayers.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, tsplayers.Select(p => p.Name));

                if (players.Count > 1)
                    TShock.Utils.SendMultipleMatchError(args.Player, players.Select(p => p.name));
                else
                    switch (players.Count)
                    {
                        case 0:
                            args.Player.SendErrorMessage("No player matched your query '{0}'", str);
                            break;
                        case 1:
                            if (players[0] == null)
                            {
                                args.Player.SendErrorMessage("---");
                                return;
                            }

                            args.Player.SendSuccessMessage("{0}'s registration date: " + players[0].firstlogin, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s total registered time: " + players[0].TotalRegisteredTime, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s total time played: " + players[0].TimePlayed, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s total activeness time: " + players[0].TotalTime, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s current rank position: " + players[0].GroupPosition + " (" + players[0].Group + ")", players[0].name);
                            args.Player.SendSuccessMessage("{0}'s next rank: " + players[0].NextGroupName, players[0].name);
                            if (players[0].Online)
                            {
                                args.Player.SendSuccessMessage("{0} was last online: " + players[0].lastlogin + " (" + players[0].LastOnline.ElapsedString() + " ago)", players[0].name);
                            }
                            break;
                    }
            }
            else
            {
                if (args.Player == TSPlayer.Server)
                {
                    args.Player.SendErrorMessage("Sorry, the server doesn't have stats to check");
                    return;
                }
                var player = Players.GetByUsername(args.Player.UserAccountName);
                args.Player.SendSuccessMessage("Your registration date: " + player.firstlogin);
                args.Player.SendSuccessMessage("Your total registered time: " + player.TotalRegisteredTime);
                args.Player.SendSuccessMessage("Your total time played: " + player.TotalTime);
                args.Player.SendSuccessMessage("Your total activeness time: " + player.TimePlayed);
                args.Player.SendSuccessMessage("Your current rank position: " + player.GroupPosition + " (" + player.Group + ")");
                args.Player.SendSuccessMessage("Your next rank: " + player.NextGroupName);
                args.Player.SendSuccessMessage("Next rank in: " + player.NextRankTime);
            }
        }

        private static void OnGreet(GreetPlayerEventArgs args)
        {
            var ply = TShock.Players[args.Who];

            if (ply == null)
                return;
            if (ply.IsLoggedIn)
                PostLogin(new PlayerPostLoginEventArgs(ply));
        }

        private static void OnLeave(LeaveEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
                return;

            var ply = TShock.Players[args.Who];

            if (!ply.IsLoggedIn) return;

            var player = Players.GetByUsername(ply.UserAccountName);
            if (player == null)
                return;

            dbManager.SavePlayer(player);
            player.tsPlayer = null; //removes the player from the initialized database/queue thingy?
        }

        private static void PostLogin(PlayerPostLoginEventArgs args)
        {
            if (args.Player == null)
                return;
            if (args.Player.Name != args.Player.UserAccountName) //returns if player logs in as different name
                return;

            var player = Players.GetByUsername(args.Player.UserAccountName);

            if (player != null)
                player.tsPlayer = args.Player;
            else
            {
                player = new TrPlayer(args.Player.UserAccountName, 0, DateTime.UtcNow.ToString("G"),
                    DateTime.UtcNow.ToString("G"), 0) { tsPlayer = args.Player };
                Players.Add(player);

                if (!dbManager.InsertPlayer(player))
                    TShock.Log.ConsoleError("[TimeRanks] Failed to create storage for {0}.", player.name);
                else
                    TShock.Log.ConsoleInfo("[TimeRanks] Created storage for {0}.", player.name);
            }

            if (args.Player.Group.Name == config.StartGroup && config.Groups.Count > 1) //starting rank/new player
                TShock.Users.SetUserGroup(TShock.Users.GetUserByName(args.Player.UserAccountName), config.Groups.Keys.ToList()[0]); //AutoStarts the player to the config's first rank.
            
            if (player.LastOnline.TotalSeconds > player.RankInfo.derankCost && player.RankInfo.derankCost > 0) //if not a new/starting player and passes inactivity limit. 0 = no limit
            {
                var groupIndex = TimeRanks.config.Groups.Keys.ToList().IndexOf(player.Group) - 1;
                if (groupIndex < 0)
                    return;
                player.time = 0; //resets player's activeness time back to 0, excluding first rank

                var user = TShock.Users.GetUserByName(player.name);

                TShock.Users.SetUserGroup(user, TimeRanks.config.Groups.Keys.ElementAt(groupIndex));
                args.Player.SendInfoMessage("You have been demoted to " + player.Group + " due to inactivity!");
                TShock.Log.ConsoleInfo(user.Name + " has been dropped a rank due to inactivity");
            }
        }

        private static void Delete(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var name = string.Join(" ", args.Parameters);
                if (dbManager.DeletePlayer(name))
                    args.Player.SendSuccessMessage("[TimeRanks] Deleted player: " + name);
                else
                    args.Player.SendErrorMessage("[TimeRanks] Failed to delete player named: " + name);
            }
            else
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rankdelete <player>");
        }
    }
}
