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

namespace RankSystem
{
    [ApiVersion(2,1)]
    public class RankSystem : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database dbManager;
        public static Config config;
        private static Timers _timers;
        static HttpClient client = new HttpClient();
        public static List<RPlayer> _players = new List<RPlayer>();

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
            get { return new Version(1, 0, 1); }
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

            dbManager = new Database(_db);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            GeneralHooks.ReloadEvent += Reload;
            PlayerHooks.PlayerPostLogin += PostLogin;
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGreetPlayer.Deregister(this, OnGreet);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= Reload;
                PlayerHooks.PlayerPostLogin -= PostLogin;

                dbManager.SaveAllPlayers();
             
            }
            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs e)
        {
            config = Config.Read();

            if (String.Equals(config.StartGroup, config.Groups[0].name, StringComparison.CurrentCultureIgnoreCase))
            {
                TShock.Log.ConsoleError("[RankSystem] Initialization cancelled due to config error: " + "StartGroup is same as first rank name");
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                return;
            }
            
            Commands.ChatCommands.Add(new Command("rs.user", Check, "check", "rank", "rankup")
            {
                HelpText = "Displays information about your current and upcoming rank"
            });
            Commands.ChatCommands.Add(new Command("rs.admin", Delete, "rankdelete")
            {
                HelpText = "Deletes a player's rank from the database"
            });
        }

        private static void Reload(ReloadEventArgs args)
        {
            config = Config.Read();
        }
        private static void Check(CommandArgs args)
        {

            if (args.Player == TSPlayer.Server)
            {
                return;
            }
            if (args.Parameters.Count > 0)
            {
                var str = string.Join(" ", args.Parameters);
                var player = PlayerManager.getPlayer(str);
                var tsplayers = TShock.UserAccounts.GetUserAccountsByName(str);
                Console.WriteLine(player.GroupIndex);

                if (player == null)
                {
                    args.Player.SendErrorMessage("Invalid player!");
                    return;
                }
                if(player.NextGroupName == null)
                {
                    return;
                }
                args.Player.SendMessage($"{player.name} has played for: {player.TotalTime}", Color.IndianRed);


                var newGroup = player.NextGroupName;
                if(player.NextGroupName != "")
                {
                    args.Player.SendMessage($"{player.name}'s next rank ({player.NextGroupName}) will unlock in: {player.NextRankTime}", Color.Orange);
                    return;
                }

                args.Player.SendMessage($"{player.name} is at the final rank!", Color.LightGreen);
                return;

            }
            else
            {
                var player = PlayerManager.getPlayer(args.Player);
                args.Player.SendMessage($"You have played for: {player.TotalTime}", Color.IndianRed);

                var newGroup = player.NextGroupName;
                if (player.NextGroupName != "")
                {
                    args.Player.SendMessage($"Your next rank ({player.NextGroupName}) will unlock in: {player.NextRankTime}", Color.Orange);
                    return;
                }

                args.Player.SendMessage($"You are at the final rank!", Color.LightGreen);
                return;
            }

        }

        private static void OnGreet(GreetPlayerEventArgs args)
        {
            if (TShock.Players[args.Who] == null)
            {
                return;
            }
            var p = TShock.Players[args.Who];

            if (p == null)
                return;
            if (p.Name != p.Account.Name) //returns if player logs in as different name
                return;
            if (p.Active == false)
            {
                return;
            }
            if (p.IsLoggedIn == false)
            {
                return;
            }


            if (dbManager.CheckRankExist(p) == false)
            {
                var n = new RPlayer(p.Name);
                _players.Add(n);
                dbManager.InsertPlayer(n);
            }
            else
            {
                var e = dbManager.GrabPlayer(p);
                _players.Add(e);

            }


            var player = PlayerManager.getPlayer(p.Name);



            if (p.Group.Name == config.StartGroup) //starting rank/new player
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(p.Account.Name), config.Groups[0].name); //AutoStarts the player to the config's first rank.


            if (Timers.hasStarted == false && _players.Count > 0)
            {
                Timers.hasStarted = true;
                Timers.RankUpdateTimer();
                Timers.BackupThreadTimer();
            }



        }

        private static void OnLeave(LeaveEventArgs args)
        {
            var ply = TShock.Players[args.Who];

            if (!ply.IsLoggedIn) return;

            var player = PlayerManager.getPlayer(ply.Name);
            if (player == null)
                return;

            dbManager.SavePlayer(player);
            _players.Remove(_players.FirstOrDefault(x => x.name == player.name));

            if (Timers.hasStarted == true && TShock.Utils.GetActivePlayerCount() < 1)
            {
                _players.Clear();
                Timers.hasStarted = false;
            }
        }


        private static void PostLogin(PlayerPostLoginEventArgs args)
        {

        }

        private static void Delete(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                var name = string.Join(" ", args.Parameters);
                if (dbManager.DeletePlayer(name))
                    args.Player.SendSuccessMessage("[RankSystem] Deleted player: " + name);
                else
                    args.Player.SendErrorMessage("[RankSystem] Failed to delete player named: " + name);
            }
            else
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: /rankdelete <player>");
        }
    }
}
