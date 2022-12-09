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

namespace RankSystem
{
    [ApiVersion(2,1)]
    public class RankSystem : TerrariaPlugin
    {
        private IDbConnection _db;
        public static Database dbManager;
        public static Config config;
        public DateTime LastTimelyRun = DateTime.UtcNow;
        private static Timers _timers;
        static HttpClient client = new HttpClient();
        public static List<RPlayer> _players = new List<RPlayer>();

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

            dbManager = new Database(_db);
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            PlayerHooks.PlayerLogout += OnLogout;
            PlayerHooks.PlayerPostLogin += OnLogin;
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            GeneralHooks.ReloadEvent += Reload;
        }

        private void OnUpdate(EventArgs args)
        {

            if (timerCheck == false)
            {
                return;
            }

            if ((DateTime.UtcNow - LastTimelyRun).TotalSeconds >= 5)
                {
                    LastTimelyRun = DateTime.UtcNow;
                    Timers.UpdateTimer();
                    Console.WriteLine("Updating ranks");
            }

            if ((DateTime.UtcNow - LastTimelyRun).TotalMinutes >= 5)
            {
                RankSystem.dbManager.SaveAllPlayers();
            }
    
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= OnLogin;
                PlayerHooks.PlayerLogout -= OnLogout;
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
                GeneralHooks.ReloadEvent -= Reload;

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
            Commands.ChatCommands.Add(new Command("rs.admin", ViewPlayers, "viewplayers")
            {
                HelpText = "view list of all 'RPlayers'"
            });
        }

        private void ViewPlayers(CommandArgs args)
        {
            foreach(RPlayer p in RankSystem._players)
            {
                args.Player.SendMessage(p.name, Color.White);
            }
        }

        private static void Reload(ReloadEventArgs args)
        {
            config = Config.Read();
        }
        private static void Check(CommandArgs args)
        {
            Console.WriteLine("a");
            if (args.Parameters.Count > 0)
            {
                string str = string.Join("", args.Parameters);
                var player = dbManager.GrabOfflinePlayer(str);
                if(player == null)
                {
                    args.Player.SendMessage($"That player does not exist!", Color.IndianRed);
                    return;
                }

                args.Player.SendMessage($"{player.accountName} has played for: {player.TotalTime}", Color.IndianRed);

                if (player.NextGroupName != "")
                {
                    args.Player.SendMessage($"{player.accountName}'s next rank ({player.NextGroupName}) will unlock in: {player.NextRankTime}", Color.Orange);
                    return;
                }

                args.Player.SendMessage($"{player.accountName} is at the final rank!", Color.LightGreen);

                return;

            }
            else
            {
                if (args.Player == TSPlayer.Server)
                {
                    return;
                }

                var p = args.Player;

                var player = PlayerManager.getPlayerFromAccount(p.Account.Name);


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

        private static void OnLogin(PlayerPostLoginEventArgs args)
        {
            var p = args.Player;

            if (dbManager.CheckRankExist(p.Account.Name) == true)
            {
                var e = dbManager.GrabPlayer(p.Account.Name, p.Name);
                _players.Add(e);

            }
            else if(dbManager.CheckRankExist(p.Account.Name) == false)
            {
                RPlayer n = new RPlayer(p.Account.Name);
                _players.Add(n);
                dbManager.InsertPlayer(n);
            }
            else
            {
                Console.WriteLine($"ERROR: {p.Name}'s Playtime could not be loaded!");
            }
        

            RPlayer player = PlayerManager.getPlayerFromAccount(p.Account.Name);


            if (p.Group.Name == config.StartGroup) //starting rank/new player
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(p.Account.Name), config.Groups[0].name); //AutoStarts the player to the config's first rank.


            if (timerCheck == false && _players.Count > 0)
            {
                timerCheck = true;

            }
        }

        private static void OnLogout(PlayerLogoutEventArgs args)
        {
            RPlayer player = PlayerManager.getPlayerFromAccount(args.Player.Account.Name);
            if (player == null)
                return;

            dbManager.SavePlayer(player);
            _players.Remove(_players.First(x => x.accountName == args.Player.Account.Name));

            if (timerCheck == true && TShock.Utils.GetActivePlayerCount() <= 0)
            {
                timerCheck = false;

            }
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
