using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using TShockAPI;

namespace RankSystem
{
    class Timers
    {

        public static async void RankUpdateTimer()
        {
            await Task.Delay(5 * 1000);
            UpdateTimer();
        }

        public static async void BackupThreadTimer()
        {
            await Task.Delay(5 * 60 * 1000);
            BackupTimer();
        }

        private static void UpdateTimer()
        {

            foreach (RPlayer player in RankSystem._players)
            {
                player.totaltime += 5;

                if(TSPlayer.FindByNameOrID(player.name)[0].Active == false) { 
                    RankSystem._players.Remove(player);
                    continue;
                }


                if(player.NextRankTime != null && player.NextGroupName != null && player.ConfigContainsGroup)
                {

                    var reqPoints = player.NextRankInfo.rankCost;

                    if (RankSystem.config.doesCurrencyAffectRankTime == true)
                    {
                        reqPoints = player.NextRankInfo.rankCost - ((RankSystem.config.currencyAffect / 100) * (int)Math.Round(SimpleEcon.PlayerManager.GetPlayer(player.name).balance));
                    }

                        if (player.totaltime > reqPoints)
                        {
                            TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(player.name), player.NextGroupName);

                            if (player.RankInfo.rankUnlocks != null)
                            {
                                player.giveDrops(player.tsPlayer);
                            }



                        player.tsPlayer.SendMessage("[c/00ffff:Y][c/00fff7:o][c/00fff0:u] [c/00ffe2:h][c/00ffdb:a][c/00ffd4:v][c/00ffcd:e] [c/00ffbf:r][c/00ffb8:a][c/00ffb1:n][c/00ffaa:k][c/00ffa3:e][c/00ff9c:d] [c/00ff8e:u][c/00ff87:p][c/00ff80:!]", Microsoft.Xna.Framework.Color.White);

                        }

        
                }



                RankUpdateTimer();
            }
            
        }

        private static void BackupTimer()
        {
            RankSystem.dbManager.SaveAllPlayers();
            BackupThreadTimer();
        }
    }
}
