using System;
using System.Collections.Generic;
using System.Linq;
using TShockAPI;
using TShockAPI.Hooks;
using Terraria;
using TerrariaApi.Server;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Auxiliary.Configuration;
using System.Timers;
using Auxiliary;
using Appointer.Models;
using CSF.TShock;

namespace Appointer
{
    [ApiVersion(2, 1)]
    public class Appointer : TerrariaPlugin
    {
        private Timer _updateTimer;
        private readonly TSCommandFramework _fx;
        private static List<AFKPlayer> afkPlayers;
        public override string Author
            => "Average";

        public override string Description
            => "Automatic rank progression plugin intended to be used by TBC";

        public override string Name
            => "Appointer";

        public override Version Version
            => new Version(1, 0);

        public Appointer(Main game)
            : base(game)
        {
            _fx = new(new()
            {
                DefaultLogLevel = CSF.LogLevel.Warning,
            });
        }

        public override async void Initialize()
        {
            Configuration<AppointerSettings>.Load("Appointer");
            afkPlayers = new List<AFKPlayer>();
            //reloading
            GeneralHooks.ReloadEvent += (x) =>
            {
                Configuration<AppointerSettings>.Load("Appointer");
                x.Player.SendSuccessMessage("Successfully reloaded Appointer!");
            };
            

            #region Timer initialization
            _updateTimer = new(1000)
            {
                AutoReset = true
            };
            _updateTimer.Elapsed += async (_, x)
                => await Update(x);
            _updateTimer.Start();
            #endregion

            await _fx.BuildModulesAsync(typeof(Appointer).Assembly);
        }

        private static async Task Update(ElapsedEventArgs _)
        {
            foreach (TSPlayer plr in TShock.Players)
            {
                if (plr is null || !(plr.Active && plr.IsLoggedIn))
                {
                    continue;
                }
                if (plr.Account is null)
                    continue;

                if (!afkPlayers.Any(x => x.PlayerName == plr.Name)){
                    afkPlayers.Add(new AFKPlayer(plr.Name, plr.LastNetPosition));
                }

                AFKPlayer afkPlayer = afkPlayers.First(x => x.PlayerName == plr.Name);

                if(afkPlayer.isAFK == true && afkPlayer.LastPosition != plr.LastNetPosition)
                {
                    afkPlayer.isAFK = false;
                    afkPlayer.afkTicks = 0;
                    TSPlayer.All.SendInfoMessage($"{plr.Name} is no longer AFK!");
                }

                if (afkPlayer.LastPosition == plr.LastNetPosition)
                {
                    afkPlayer.afkTicks++;
                    if (afkPlayer.isAFK == true && afkPlayer.afkTicks < 1000)
                    {
                        continue;
                    }
                    if(afkPlayer.isAFK == true && afkPlayer.afkTicks >= 1000)
                    {
                        plr.Kick("Kicked for being AFK for too long! (over 15 minutes)", false, false);
                        continue;
                    }
                    if(afkPlayer.afkTicks >= 120)
                    {
                        afkPlayer.isAFK = true;
                        TSPlayer.All.SendInfoMessage($"{plr.Name} is now AFK!");
                        continue;
                    }
                }
                else
                {
                    afkPlayer.afkTicks = 0;
                    if(afkPlayer.isAFK == true)
                    {
                        afkPlayer.isAFK = false;
                        TSPlayer.All.SendInfoMessage($"{plr.Name} is no longer AFK!");
                    }
                }
                afkPlayer.LastPosition = plr.LastNetPosition;

                var entity = await IModel.GetAsync(GetRequest.Bson<TBCUser>(x => x.AccountName == plr.Account.Name), x => x.AccountName = plr.Account.Name);
                
                if (Extensions.NextRankCost(plr.Account).Result < 0)
                {
                    string newGroup = Extensions.NextGroup(plr.Account).Name;
                    plr.Group = TShock.Groups.GetGroupByName(newGroup);
                    TShock.UserAccounts.SetUserGroup(plr.Account, newGroup);
                    TSPlayer.All.SendMessage($"{plr.Name} has ranked up to {Extensions.RemovePrefixOperators(plr.Group.Prefix)}! Congratulations :D", Color.LightGreen);
                }

                entity.Playtime++;

            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {

            }
            base.Dispose(disposing);
        }
    }
}
