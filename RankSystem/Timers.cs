using IL.Terraria;
using NuGet.Protocol;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TShockAPI;
using TShockAPI.CLI;
using SimpleEcon;
using System.IO;
using System.Reflection;

namespace RankSystem
{
    class Timers
    {

        private static void rankUpUser(RPlayer player)
        {
            var reqPoints = player.NextRankInfo.rankCost;

            if (RankSystem.config.doesCurrencyAffectRankTime == true)
            {
              
                    reqPoints = player.NextRankInfo.rankCost - ((RankSystem.config.currencyAffect / 100) * (int)Math.Round(SimpleEcon.PlayerManager.GetPlayer(player.accountName).balance));
                
            }

            if (player.totaltime > reqPoints)
            {
                TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(player.name), player.NextGroupName);

                if (player.RankInfo.rankUnlocks != null)
                {
                    player.giveDrops(player.tsPlayer);
                }


                player.GroupIndex++;
                player.Group = player.NextGroupName;
                player.tsPlayer.SendMessage("[c/00ffff:Y][c/00fff7:o][c/00fff0:u] [c/00ffe2:h][c/00ffdb:a][c/00ffd4:v][c/00ffcd:e] [c/00ffbf:r][c/00ffb8:a][c/00ffb1:n][c/00ffaa:k][c/00ffa3:e][c/00ff9c:d] [c/00ff8e:u][c/00ff87:p][c/00ff80:!]", Microsoft.Xna.Framework.Color.White);

            }
            else
            {
                return;
            }
        }

        public static void UpdateTimer()
        {
            if (TShock.Utils.GetActivePlayerCount() == 0)
            {
                return;
            }

            if (RankSystem._players.Count == 0)
            {
                return;
            }

            if (RankSystem._players.Any() == false)
            {
                return;
            }


            foreach (RPlayer p in RankSystem._players)
            {
                if(p == null)
                {
                    RankSystem._players.Remove(p);
                    continue;
                }
                var player = p;
                if(player.tsPlayer.Active == false || player.tsPlayer.IsLoggedIn == false || player.tsPlayer == null)
                {
                    RankSystem._players.Remove(p);
                    continue;
                }

                player.totaltime += 5;


                if (!player.ConfigContainsGroup)
                {
                    continue;
                }

                if (player?.NextGroupName == null)
                {
                    continue;
                }

                if (player?.tsPlayer?.Group?.Name == RankSystem.config.EndGroup)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(player.NextGroupName) != true)
                {
                    rankUpUser(player);
                }
                else
                {
                    continue;
                }






            }
            return;

        }

    }
}
