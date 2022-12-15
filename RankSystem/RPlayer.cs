using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TShockAPI;
using static SimpleEcon.PlayerManager;

namespace RankSystem
{
    public static class PlayerManager
    {
        public static RPlayer getPlayer(TSPlayer player)
        {
            if (RankSystem._players.Any() == false)
            {
                return null;
            }
            if (RankSystem._players.Any(p => p.name == player.Name) == false)
            {
                return null;
            }
            return RankSystem._players.Find(p => p.name == player.Name);
        }

        public static RPlayer getPlayer(string name)
        {
            if (RankSystem._players.Any() == false)
            {
                return null;
            }
            if(RankSystem._players.Any(p => p.name == name) == false)
            {
                return null;
            }
            return RankSystem._players.Find(p => p.name == name);
        }

        public static RPlayer getPlayerFromAccount(string name)
        {
            if (!RankSystem._players.Any(x => x.name == name)){
                return null;
            }

            return RankSystem._players.First(p => p.accountName == name);
        }

        public static RPlayer getPlayer(int id)
        {
            if (RankSystem._players.Any() == false)
            {
                return null;
            }
            if (RankSystem._players.Any(p => p.name == TSPlayer.FindByNameOrID("" + id)[0].Name))
            {
                return null;
            }
            return RankSystem._players.Find(p => p.name == TSPlayer.FindByNameOrID("" + id)[0].Name);
        }

    }

    public class RPlayer
    {
        public bool offline { get; set; } = false;
        public TSPlayer tsPlayer { get; set; }
        public string name { get; set; }

        public Vector2 lastPos { get; set; }

        public bool isAFK { get; set; }

        public int afk { get; set; }

        public string accountName { get { if (offline) { return name; } return tsPlayer.Account.Name; } set { } }
        public DateTime firstlogin { get; set; }
        public DateTime lastlogin { get; set; }
        public string Group { get { if (offline) { return TShock.UserAccounts.GetUserAccountByName(accountName).Group; } return tsPlayer.Group.Name; } set { }
        }
        public int GroupIndex
        {
            get
            {
                return RankSystem.config.Groups.FindIndex(x => x.name == Group);
            }
            set { }
        }

        public bool ConfigContainsGroup
        {

            get
            {

                if (RankSystem.config.StartGroup == Group)
                {
                    return true;
                }

                if (RankSystem.config.Groups.Any(x => x.name == Group))
                {
                    return true;
                }


                return false;
            }
        }

        public RankInfo RankInfo
        {
            get
            {

                if(Group == RankSystem.config.StartGroup)
                {
                    return RankSystem.config.Groups[0].info;
                }

                if (!ConfigContainsGroup)
                {
                    return null;
                }

                return RankSystem.config.Groups[GroupIndex].info;
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

        public RPlayer(string name, int time)
        {
            this.name = name;
            this.totaltime = time;
            this.tsPlayer = TSPlayer.FindByNameOrID(name)[0];
            this.firstlogin = DateTime.Parse(this.tsPlayer.Account.Registered);
            this.lastlogin = DateTime.UtcNow;
        }
        public RPlayer(string name, int time, bool offline)
        {
            this.name = name;
            this.totaltime = time;
            this.lastlogin = DateTime.UtcNow;
            if(offline == true)
            {
                this.offline = offline;

            }
            else
            {
                this.tsPlayer = TSPlayer.FindByNameOrID(name)[0];
                this.offline = false;
            }
        }


        public void giveDrops(TSPlayer player)
        {
            if(RankInfo.rankUnlocks.Count == 0)
            {
                return;
            }

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
                if(totaltime == 0)
                {
                    return "0 seconds";
                }
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
                if (RankSystem.config.Groups.Count-1 == GroupIndex)
                    return "";

                if (!ConfigContainsGroup)
                    return "";

                return RankInfo.nextGroup;
            }
        }

        public RankInfo NextRankInfo
        {
            get
            {
                if (!ConfigContainsGroup)
                {
                    return null;
                }

                if (RankSystem.config.Groups.Count-1 == GroupIndex)
                {
                    return null;
                }

                return RankSystem.config.Groups[GroupIndex + 1].info;
            }
        }

        public string NextRankTime
        {
            get
            {
                if (!ConfigContainsGroup)
                    return null;

                if (string.IsNullOrEmpty(NextGroupName))
                    return null;

                var reqPoints = NextRankInfo.rankCost;

                if (RankSystem.config.doesCurrencyAffectRankTime == true)
                {
             
                        reqPoints = NextRankInfo.rankCost - ((RankSystem.config.currencyAffect / 100) * (int)Math.Round(SimpleEcon.PlayerManager.GetPlayerFromAccount(accountName).balance));
                    
                }

                var ts = new TimeSpan(0, 0, 0, reqPoints - totaltime);

                return ts.ElapsedString();
            }
        }


        public string GroupPosition
        {
            get
            {
                if (!ConfigContainsGroup)
                    return null;

                return (GroupIndex + 1 + " / " + RankSystem.config.Groups.Count);
            }
        }

        private static readonly Regex CleanCommandRegex = new Regex(@"^\/?(\w*\w)");


    }
}
