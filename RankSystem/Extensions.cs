using System;
using System.Linq;
using System.Text;
using TShockAPI;

namespace RankSystem
{
    internal static class Extensions
    {
        internal static string ElapsedString(this TimeSpan ts)
        {
            var sb = new StringBuilder();
            if (ts.Days > 0)
                sb.Append(string.Format("{0} day{1}{2}", ts.Days, ts.Days.Suffix(),
                    ts.Hours > 0 || ts.Minutes > 0 || ts.Seconds > 0 ? ", " : ""));

            if (ts.Hours > 0)
                sb.Append(string.Format("{0} hour{1}{2}", ts.Hours, ts.Hours.Suffix(),
                    ts.Minutes > 0 || ts.Seconds > 0 ? ", " : ""));

            if (ts.Minutes > 0)
                sb.Append(
                    string.Format("{0} minute{1}{2}", ts.Minutes, ts.Minutes.Suffix(), ts.Seconds > 0 ? ", " : ""));

            if (ts.Seconds > 0)
                sb.Append(string.Format("{0} second{1}", ts.Seconds, ts.Seconds.Suffix()));

            if (sb.Length == 0)
                return "an unknown period of time";

            return sb.ToString();
        }

        private static string Suffix(this int number)
        {
            return number == 0 || number > 1 ? "s" : "";
        }

        internal static PlaytimeInformation GetPlaytimeInformation(this TSPlayer player)
        {
            if (player is null)
            {
                return null;
            }

            if (player.IsLoggedIn is false)
            {
                return null;
            }

            return RankSystem.DB.GrabPlayer(player);
        }

        internal static bool ConfigContainsGroup(this TSPlayer player)
        {
            if (player.IsLoggedIn)
            {
                var playerGroup = player.Group.Name;

                if (playerGroup == RankSystem.config.StartGroup)
                {
                    return true;
                }

                return RankSystem.config.Groups.Any(x => x.name == player.Group.Name);
            }

            return false;
        }
        
        internal static Group NextGroup(this TSPlayer player)
        {
            if (!player.ConfigContainsGroup())
            {
                return null;
            }
            
            if (player.IsLoggedIn)
            {
                // If the player is in the last group, return an empty string
                if (player.Group.Name == RankSystem.config.EndGroup)
                {
                    return null;
                }
                
                // If the player is in the start group, return the first group
                if (player.Group.Name == RankSystem.config.StartGroup)
                {
                    return RankSystem.config.Groups.FirstOrDefault();
                }
                
                // If the player is in a group that is not the start group, return the next group
                var groupIndex = RankSystem.config.Groups.FindIndex(x => x.name == player.Group.Name);
                
                if (groupIndex == -1)
                {
                    return null;
                }
                
                return RankSystem.config.Groups.ElementAtOrDefault(groupIndex + 1);
            }

            return null;
        }
        
    }
}