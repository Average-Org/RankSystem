﻿using IL.Terraria;
using NuGet.Protocol;
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
        public static bool hasStarted = false;
        public static bool rankRunning = false;
        public static bool backupRunning = false;

        public static async void RankUpdateTimer()
        {
           if(hasStarted == true && rankRunning == false)
            {
                rankRunning = true;
                await Task.Delay(5 * 1000);
                UpdateTimer();
                return;
            }
            return;
        }

        public static async void BackupThreadTimer()
        {
            if(hasStarted == true && backupRunning == false)
            {
                backupRunning = true;
                await Task.Delay(5 * 60 * 1000);
                BackupTimer();
                return;

            }
            return;
        }

        private static void rankUpUser(RPlayer player)
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


                player.GroupIndex++;
                player.Group = player.NextGroupName;
                player.tsPlayer.SendMessage("[c/00ffff:Y][c/00fff7:o][c/00fff0:u] [c/00ffe2:h][c/00ffdb:a][c/00ffd4:v][c/00ffcd:e] [c/00ffbf:r][c/00ffb8:a][c/00ffb1:n][c/00ffaa:k][c/00ffa3:e][c/00ff9c:d] [c/00ff8e:u][c/00ff87:p][c/00ff80:!]", Microsoft.Xna.Framework.Color.White);

            }
            else
            {
                return;
            }
        }

        private static void UpdateTimer()
        {
            if (TShock.Utils.GetActivePlayerCount() < 1)
            {
                hasStarted = false;
                rankRunning = false;
                backupRunning = false;
                return;
            }

            if (RankSystem._players?.Count < 1)
            {
                return;
            }

            if (hasStarted == false)
            {
                return;
            }

            if (RankSystem._players?.Any() == false)
            {
                return;
            }


            foreach (RPlayer p in RankSystem._players)
            {
                if(p == null)
                {
                    continue;
                }
                var player = PlayerManager.getPlayer(p?.name);
                if(player?.tsPlayer == null)
                {
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

                if (player?.NextGroupName != "")
                {
                    rankUpUser(player);
                }






            }

            rankRunning = false;
            RankUpdateTimer();

            return;

        }

        private static void BackupTimer()
        {
            if (RankSystem._players.Count == 0)
            {
                return;
            }
            if(hasStarted == false)
            {
                return;
            }


            RankSystem.dbManager.SaveAllPlayers();
            backupRunning = false;
            BackupThreadTimer();
        }
    }
}
