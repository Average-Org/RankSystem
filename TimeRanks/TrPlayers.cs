using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TShockAPI;

namespace TimeRanks
{
    public class TrPlayers
    {
        public readonly List<TrPlayer> _players = new List<TrPlayer>();

        public void Add(string name, int time, string firstlogin, string lastlogin, int totaltime, string lastRewardUsed)
        {
            _players.Add(new TrPlayer(name, time, firstlogin, lastlogin, totaltime, lastRewardUsed));
        }
        public void Add(TrPlayer player)
        {
            _players.Add(player);
        }

        public TrPlayer GetByUsername(string username)
        {
            return _players.FirstOrDefault(p => p.name == username);
        }

        public IEnumerable<TrPlayer> GetListByUsername(string username)
        {
            return _players.Where(p => p.name.ToLowerInvariant().Contains(username.ToLowerInvariant()));
        }

        public IEnumerable<TrPlayer> Players { get { return _players; } }

        public IEnumerable<TrPlayer> Offline { get { return _players.Where(p => !p.Online); } }

        public IEnumerable<TrPlayer> Online { get { return _players.Where(p => p.Online); } }
    }

    public class TrPlayer
    {
        public TSPlayer tsPlayer;
        public bool Online { get { return tsPlayer != null; } }
        public readonly string name;
        public readonly string firstlogin;
        public string lastlogin;
        public string lastRewardUsed;
        public string Group
        {
            get
            {
                return !Online ? TShock.UserAccounts.GetUserAccountByName(name).Group : tsPlayer.Group.Name;
            }
        }
        public RankInfo RankInfo
        {
            get
            {
                return ConfigContainsGroup ? (Group == TimeRanks.config.StartGroup ? new RankInfo(TimeRanks.config.Groups.Keys.ElementAt(0), 0, 0) : TimeRanks.config.Groups.First(g => g.Key == Group).Value) : new RankInfo("none", 0, 0);
            }
        }
        public int time;
        public int totaltime;

        public TrPlayer(string name, int time, string first, string last, int totaltime, string lastRewardUsed)
        {
            this.time = totaltime;
            this.name = name;
            firstlogin = first;
            lastlogin = last;
            this.totaltime = totaltime;
            this.lastRewardUsed = lastRewardUsed;
        }

        public string TotalRegisteredTime
        {
            get
            {
                DateTime reg;
                DateTime.TryParse(firstlogin, out reg);

                var ts = DateTime.UtcNow - reg;
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
                DateTime last;
                DateTime.TryParse(lastlogin, out last);

                var ts = DateTime.UtcNow - last;

                return ts;
            }
        }

        public string NextGroupName
        {
            get
            {
                if (RankInfo.nextGroup == Group)
                    return "max rank achieved";

                if (!ConfigContainsGroup)
                    return "group is not part of the ranking system";

                return RankInfo.nextGroup;
            }
        }

        public string NextRankTime
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "group is not part of the ranking system";

                if (NextGroupName == "max rank achieved")
                    return "max rank achieved";

                var reqPoints = NextRankInfo.rankCost;
                var ts = new TimeSpan(0, 0, 0, reqPoints - time);

                return ts.ElapsedString();
            }
        }

        public RankInfo NextRankInfo
        {
            get
            {
                return ConfigContainsGroup ? (RankInfo.nextGroup == Group ? new RankInfo("max rank", 0, TimeRanks.config.Groups.Values.ElementAt(TimeRanks.config.Groups.Values.Count - 1).derankCost) : TimeRanks.config.Groups[RankInfo.nextGroup]) : new RankInfo("none", 0, 0);
            }
        }

        public string GroupPosition
        {
            get
            {
                if (!ConfigContainsGroup)
                    return "group is not part of the ranking system";

                return (TimeRanks.config.Groups.Keys.ToList().IndexOf(Group) + 1) + " / " + TimeRanks.config.Groups.Keys.Count;
            }
        }

        private static readonly Regex CleanCommandRegex = new Regex(@"^\/?(\w*\w)");

        public bool ConfigContainsGroup
        {
            get { return TimeRanks.config.Groups.Keys.Contains(Group); }
        }
    }
}
