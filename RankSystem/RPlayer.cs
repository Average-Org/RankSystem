using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TShockAPI;

namespace RankSystem
{
    public static class PlayerManager
    {
        public static RPlayer getPlayer(TSPlayer player)
        {
            return RankSystem._players.Find(p => p.name == player.Name);
        }

        public static RPlayer getPlayer(string name)
        {
            return RankSystem._players.Find(p => p.name == name);
        }

        public static RPlayer getPlayer(int id)
        {
            return RankSystem._players.Find(p => p.name == TSPlayer.FindByNameOrID("" + id)[0].Name);
        }

    }

    public class RPlayer
    {
        public TSPlayer tsPlayer { get; set; }
        public string name { get; set; }
        public DateTime firstlogin { get; set; }
        public DateTime lastlogin { get; set; }
        public string Group { get { return tsPlayer.Group.Name; } }
        public RankInfo RankInfo
        {
            get
            {

                if(Group == RankSystem.config.StartGroup)
                {
                    return RankSystem.config.Groups.Values.ElementAt(0);
                }

                if(RankSystem.config.Groups.ContainsKey(Group))
                {
                    return RankSystem.config.Groups.First(g => g.Key == Group).Value;
                }

                return null;
            }
        }
        public int totaltime { get; set; }

        public RPlayer(string name)
        {
            this.name = name;
            this.totaltime = 0;
            this.tsPlayer = TSPlayer.FindByNameOrID(name)[0];
            this.firstlogin = DateTime.Parse(this.tsPlayer.Account.Registered);
            this.lastlogin = DateTime.UtcNow;
        }


        public RPlayer(string name, int time, DateTime firstlogin)
        {
            this.name = name;
            this.totaltime = time;
            this.tsPlayer = TSPlayer.FindByNameOrID(name)[0];
            this.firstlogin = firstlogin;
            this.lastlogin = DateTime.UtcNow;
        }

        public void giveDrops(TSPlayer player)
        {
            foreach (KeyValuePair<int, int> prop in RankInfo.rankUnlocks)
            {
                player.GiveItem(prop.Key, prop.Value, 0);
            }

        }
        public string TotalRegisteredTime
        {
            get
            {
                var ts = DateTime.UtcNow - firstlogin;
                return ts.ElapsedString();
            }
        }

        public string TimePlayed
        {
            get
            {
                var ts = TotalTime;
                return ts;
            }
        }

        public string TotalTime
        {
            get
            {
                var ts = new TimeSpan(0, 0, 0, totaltime);
                return ts.ElapsedString();
            }
        }

        public TimeSpan LastOnline
        {
            get
            {
                var ts = DateTime.UtcNow - lastlogin;

                return ts;
            }
        }

        public string NextGroupName
        {
            get
            {
                if (RankSystem.config.Groups.Keys.ElementAt(RankSystem.config.Groups.Count - 1) == Group)
                    return null;

                if (!ConfigContainsGroup)
                    return null;



                return RankInfo.nextGroup;
            }
        }

        public string NextRankTime
        {
            get
            {
                if (!ConfigContainsGroup)
                    return null;

                if (NextGroupName == "max rank achieved")
                    return null;

                var reqPoints = NextRankInfo.rankCost;

                if (RankSystem.config.doesCurrencyAffectRankTime == true)
                {
                    reqPoints = NextRankInfo.rankCost - ((RankSystem.config.currencyAffect / 100) * (int)Math.Round(SimpleEcon.PlayerManager.GetPlayer(name).balance));
                }

                var ts = new TimeSpan(0, 0, 0, reqPoints - totaltime);

                return ts.ElapsedString();
            }
        }

        public RankInfo NextRankInfo
        {
            get
            {
                return ConfigContainsGroup ? (RankInfo.nextGroup == Group ? new RankInfo("max rank", 0, null) : RankSystem.config.Groups[RankInfo.nextGroup]) : new RankInfo("none", 0, null);
            }
        }

        public string GroupPosition
        {
            get
            {
                if (!ConfigContainsGroup)
                    return null;

                return (RankSystem.config.Groups.Keys.ToList().IndexOf(Group) + 1) + " / " + RankSystem.config.Groups.Keys.Count;
            }
        }

        private static readonly Regex CleanCommandRegex = new Regex(@"^\/?(\w*\w)");

        public bool ConfigContainsGroup
        {


            get {
                if (RankSystem.config.Groups.Keys.Contains(Group))
                {
                    return true;
                }

                if (RankSystem.config.StartGroup == Group)
                {
                    return true;
                }

                return false;
            }
        }
    }
}
