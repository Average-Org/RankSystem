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

namespace TimeRanks //simplified from White's TimeBasedRanks plugin
{
    [ApiVersion(2,1)]
    public class TimeRanks : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database dbManager;
        public static Config config = new Config();
        private static Timers _timers;
        static HttpClient client = new HttpClient();


        public static readonly TrPlayers Players = new TrPlayers();

        public override string Author
        {
            get { return "Average"; }
        }

        public override string Description
        {
            get { return "Rank progression system based on user playtime"; }
        }

        public override string Name
        {
            get { return "TimeRanks"; }
        }

        public override Version Version
        {
            get { return new Version(1, 1, 0); }
        }

        public TimeRanks(Main game)
            : base(game)
        {
        }

        public override void Initialize()
        {
            switch (TShock.Config.Settings.StorageType.ToLower())
            {
                case "sqlite":
                    _db = new SqliteConnection(("Data Source=" + Path.Combine(TShock.SavePath, "TimeRanksData.sqlite")));
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
            Commands.ChatCommands.Add(new Command("tbr.rank.admin", Reload, "rreload")
            {
                HelpText = "Reloads the TimeRanks plugin."
            });
            Commands.ChatCommands.Add(new Command("tbr.vote", Reward, "reward")
            {
                HelpText = "Rewards the player for voting."
            });
            dbManager.InitialSyncPlayers();
        }

        private static void Reload(CommandArgs args)
        {
            var configPath = Path.Combine(TShock.SavePath, "TimeRanks.json");
            (config = Config.Read(configPath)).Write(configPath);
            args.Player.SendSuccessMessage("TimeRanks configuration file has been reloaded!");
        }

        public void givePlaytime(TSPlayer player, int time)
        {
            Players.GetByUsername(player.Name).totaltime += 1500;
        }

        private static void Check(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var str = string.Join(" ", args.Parameters);
                var players = Players.GetListByUsername(str).ToList();
                var tsplayers = TShock.UserAccounts.GetUserAccountsByName(str);

                if (tsplayers.Count > 1)
                    TShock.Utils.SendLogs(args.Player.Account.Name, Microsoft.Xna.Framework.Color.Blue);

                if (players.Count > 1)
                    TShock.Utils.SendLogs(args.Player.Account.Name, Microsoft.Xna.Framework.Color.Blue);
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

                            args.Player.SendSuccessMessage("{0}'s registration date: (for {1})" + players[0].firstlogin, players[0].name, players[0].TotalRegisteredTime);
                            args.Player.SendSuccessMessage("{0}'s total time played: " + players[0].TimePlayed, players[0].name);
                            args.Player.SendSuccessMessage("{0}'s current rank position: " + players[0].GroupPosition + " (" + players[0].Group + ")", players[0].name);
                            args.Player.SendSuccessMessage("{0}'s next rank: " + players[0].NextGroupName + " will unlock in... {1}", players[0].name, players[0].NextRankTime);
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
                    args.Player.SendErrorMessage("You cannot check the server user's playtime! Sorry :(");
                    return;
                }
                var player = Players.GetByUsername(args.Player.Account.Name);
                args.Player.SendSuccessMessage("Your registration date: " + player.firstlogin + " (for " + player.TotalRegisteredTime + ")");
                args.Player.SendSuccessMessage("Your total time played: " + player.TotalTime);
                args.Player.SendSuccessMessage("Your current rank position: " + player.GroupPosition + " (" + player.Group + ")");
                args.Player.SendSuccessMessage("Your next rank: " + player.NextGroupName + " will be unlocked in... " + player.NextRankTime);
            }
        }

        private static void Reward(CommandArgs args)
        {

            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("You must be logged in first!");
                return;
            }

            string playerName = args.Player.Account.Name;


            if (Players.GetByUsername(args.Player.Name).lastRewardUsed != null) { 
                DateTime now = DateTime.Now;
                DateTime then = DateTime.Parse(Players.GetByUsername(args.Player.Name).lastRewardUsed);

                if (now.Subtract(then).TotalHours >= 24)
                {

                }
                else
                {
                    args.Player.SendErrorMessage("You have already claimed your reward for today!");
                    return;
                }

            }


            if(checkifPlayerVoted(args.Player).Result == true)
            {
                Players.GetByUsername(args.Player.Name).totaltime += 3600;
                TSPlayer.All.SendMessage(args.Player.Name + " has voted for us and received one hour of playtime added to their account! Use /tvote to get the same reward!", Microsoft.Xna.Framework.Color.Aqua);
                Players.GetByUsername(args.Player.Name).lastRewardUsed = DateTime.Now.ToString();
            }

        }

        public static async Task<bool> checkifPlayerVoted(TSPlayer player)
        {
            bool hasVoted = false;

            string voteUrl = "http://terraria-servers.com/api/?object=votes&element=claim&key=" + config.voteApiKey + "&username=" + player.Name;

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    using (HttpResponseMessage res = await client.GetAsync(voteUrl))
                    {
                        using (HttpContent content = res.Content)
                        {
                            var data = await content.ReadAsStringAsync();

                            if(data != null)
                            {
                                if (data == "1")
                                {
                                    hasVoted = true;
                                    return hasVoted;
                                }
                                else
                                {
                                    hasVoted = false;
                                    return hasVoted;
                                }
                            }
                            else
                            {
                                Console.WriteLine("No data");
                            }
                        }
                    }
                }
            }catch (Exception ex)
            {
                Console.WriteLine("Exception!!!!");
                Console.Write(ex);
                return hasVoted;
            }

            return hasVoted;

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

            var player = Players.GetByUsername(ply.Account.Name);
            if (player == null)
                return;

            dbManager.SavePlayer(player);
            player.tsPlayer = null; //removes the player from the initialized database/queue thingy?
        }

        private static void checkUserForRankup(PlayerPostLoginEventArgs args)
        {
            var player = Players.GetByUsername(args.Player.Account.Name);
            if (!player.ConfigContainsGroup) {
                return;
            }

            var user = TShock.UserAccounts.GetUserAccountByName(player.name);
            var groupIndex = TimeRanks.config.Groups.Keys.ToList().IndexOf(player.Group) + 1;

            if (player.totaltime >= player.NextRankInfo.rankCost)
            {
                TShock.UserAccounts.SetUserGroup(user, TimeRanks.config.Groups.Keys.ElementAt(groupIndex));
                checkUserForRankup(args);
            }
            else
            {
                return;
            }


        }

        private static void PostLogin(PlayerPostLoginEventArgs args)
        {
            if (args.Player == null)
                return;
            if (args.Player.Name != args.Player.Account.Name) //returns if player logs in as different name
                return;

            var player = Players.GetByUsername(args.Player.Account.Name);

            if (player != null)
                player.tsPlayer = args.Player;
            else
            {
                player = new TrPlayer(args.Player.Account.Name, 0, DateTime.UtcNow.ToString("G"),
                    DateTime.UtcNow.ToString("G"), 0, null) { tsPlayer = args.Player };
                Players.Add(player);

                if (!dbManager.InsertPlayer(player))
                    TShock.Log.ConsoleError("[TimeRanks] Failed to create storage for {0}.", player.name);
                else
                    TShock.Log.ConsoleInfo("[TimeRanks] Created storage for {0}.", player.name);
            }

            if (args.Player.Group.Name == config.StartGroup && config.Groups.Count > 1) //starting rank/new player
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(args.Player.Account.Name), config.Groups.Keys.ToList()[0]); //AutoStarts the player to the config's first rank.

            if (player.ConfigContainsGroup)
            {
                checkUserForRankup(args);
            }
            else
            {
                return;
            }

            if (checkifPlayerVoted(args.Player).Result == true)
            {

                if (Players.GetByUsername(args.Player.Name).lastRewardUsed != null)
                {
                    DateTime now = DateTime.Now;
                    DateTime then = DateTime.Parse(Players.GetByUsername(args.Player.Name).lastRewardUsed);

                    if (now.Subtract(then).TotalHours >= 24)
                    {
                        Players.GetByUsername(args.Player.Name).totaltime += 3600;
                        TSPlayer.All.SendMessage(args.Player.Name + " has voted for us and received one hour of playtime added to their account! Use /vote to get the same reward!", Microsoft.Xna.Framework.Color.Aqua);
                    }
                    else
                    {
                        return;
                    }

                }
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
