using System;
using System.Linq;
using System.Timers;
using TShockAPI;

namespace RankSystem
{
    class Timers
    {
        private Timer _uTimer;
        private Timer _bTimer;

        public Timers()
        {
            _uTimer = new Timer(5 * 1000); //update ranks every 5 seconds
            _bTimer = new Timer(5 * 60 * 1000); //backup every 5 min
        }

        public void Start()
        {
            _uTimer.Enabled = true;
            _uTimer.Elapsed += UpdateTimer;

            _bTimer.Enabled = true;
            _bTimer.Elapsed += BackupTimer;
        }

        private static void UpdateTimer(object sender, ElapsedEventArgs args)
        {

            foreach (RPlayer player in RankSystem._players)
            {
                player.totaltime += 5;


                if(player.NextRankTime != null)
                {

                    var reqPoints = player.NextRankInfo.rankCost;

                    if (RankSystem.config.doesCurrencyAffectRankTime == true)
                    {
                        reqPoints = player.NextRankInfo.rankCost - ((RankSystem.config.currencyAffect / 100) * (int)Math.Round(SimpleEcon.PlayerManager.GetPlayer(player.name).balance));
                    }


                    if (player.totaltime >= reqPoints)
                    {
                        TShock.UserAccounts.SetUserGroup(TShock.UserAccounts.GetUserAccountByName(player.name), player.NextGroupName);

                        player.tsPlayer.SendMessage("[c/00ffff:Y][c/00fff7:o][c/00fff0:u] [c/00ffe2:h][c/00ffdb:a][c/00ffd4:v][c/00ffcd:e] [c/00ffbf:r][c/00ffb8:a][c/00ffb1:n][c/00ffaa:k][c/00ffa3:e][c/00ff9c:d] [c/00ff8e:u][c/00ff87:p][c/00ff80:!]", Microsoft.Xna.Framework.Color.White);

                    }
                }




            }
        }

        private static void BackupTimer(object sender, ElapsedEventArgs args)
        {
            RankSystem.dbManager.SaveAllPlayers();
        }
    }
}
