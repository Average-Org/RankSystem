using System;
using System.Linq;
using TShockAPI;
using System.Timers;
using Microsoft.Xna.Framework;

namespace RankSystem
{
    class Timers
    {
        private static string RankupMessage =>
            "[c/00ffff:Y][c/00fff7:o][c/00fff0:u] [c/00ffe2:h][c/00ffdb:a][c/00ffd4:v][c/00ffcd:e] [c/00ffbf:r][c/00ffb8:a][c/00ffb1:n][c/00ffaa:k][c/00ffa3:e][c/00ff9c:d] [c/00ff8e:u][c/00ff87:p][c/00ff80:!]";

        internal static void RankupUser(TSPlayer player)
        {
            var playtimeInformation = player.GetPlaytimeInformation();
            
            var closestGroup = RankSystem.config.GetClosestGroup(playtimeInformation.TotalTime);

            // user is not in the right group, should be ranked up
            if (closestGroup.name != player.Group.Name)
            {
                try
                {
                    // rank them up to it
                    TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(player.Account.Name),
                        closestGroup.name);
                    
                    if (closestGroup.info.rankUnlocks != null)
                    {
                        RankSystem.config.GiveDrops(closestGroup.info, player);
                    }

                    player.SendMessage(RankupMessage, Color.White);
                }
                catch (Exception ex)
                {
                    TShock.Log.ConsoleError($"Rank System Error: {ex}");
                }

                return;
            }
        }

        public static void UpdateTimer(object sender, ElapsedEventArgs e)
        {
            var loggedInPlayers = TShock.Players.Where(p => p is { Active: true, IsLoggedIn: true }).ToList();
            
            foreach (var player in loggedInPlayers)
            {
                var playtimeInformation = player.GetPlaytimeInformation();
                
                if(playtimeInformation is null)
                {
                    continue;
                }
                
                if (RankSystem.config.useAFKSystem && AfkSystem.HandleAfk(player))
                {
                    continue;
                }
                
                playtimeInformation.TotalTime += 5;
                RankSystem.DB.SavePlayer(playtimeInformation);

                if (RankSystem.DB.HasFavorite(player.Account.Name))
                {
                    continue;
                }
                
                RankupUser(player);
            }
        }
        
    }
}