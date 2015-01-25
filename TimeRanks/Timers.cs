using System;
using System.Linq;
using System.Timers;
using TShockAPI;

namespace TimeRanks
{
    class Timers
    {
        private readonly Timer _uTimer;
        private readonly Timer _bTimer;

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
            foreach (var player in TimeRanks.Players.Online)
            {
                player.time += 5; //adds 5 seconds to player's activeness time
                player.totaltime += 5; //adds 5 seconds to player's total time played

                if (player.tsPlayer == null)
                    continue;

                if (player.time < TimeRanks.config.Groups[player.Group].rankCost)
                    continue;

                if (player.NextGroupName == player.Group)
                    continue;

                TShock.Users.SetUserGroup(TShock.Users.GetUserByName(player.name), player.NextGroupName);

                player.tsPlayer.SendWarningMessage("You have ranked up!");
                player.tsPlayer.SendWarningMessage("Your current rank position: " + player.GroupPosition + " (" + player.Group + ")");
                player.tsPlayer.SendWarningMessage("Your next rank: " + player.NextGroupName);
                player.tsPlayer.SendWarningMessage("Next rank in: " + player.NextRankTime);
            }
        }

        private static void BackupTimer(object sender, ElapsedEventArgs args)
        {
            TimeRanks.dbManager.SaveAllPlayers();
        }
    }
}
