using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TShockAPI;

namespace RankSystem
{
	public class RankInfo
	{
		public string nextGroup;
		public int rankCost;
		public Dictionary<int, int> rankUnlocks;

		public RankInfo(string nextGroup, int rankCost, Dictionary<int, int> rankUnlocks)
		{
			this.nextGroup = nextGroup;
			this.rankCost = rankCost;
			this.rankUnlocks = rankUnlocks;
		}
	}

	public class Group
    {
		public string name;
		public RankInfo info;

		public Group(string name, RankInfo info)
        {
			this.name = name;
			this.info = info;
        }
    }

	public class Config
    {
		public string StartGroup { get; set; } = "default";
		public string EndGroup { get; set; } = "endGroup";

		public bool useAFKSystem { get; set; } = true;

       public List<Group> Groups { get; set; } = new List<Group>();	

		public void Write()
		{
			string path = Path.Combine(TShock.SavePath, "RankSystem.json");
			File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
		}
		public static Config Read()
		{
			string filepath = Path.Combine(TShock.SavePath, "RankSystem.json");

			try
			{
				Config config = new Config();

				if (!File.Exists(filepath))
				{
					config.Groups.Add(new Group("vip", new RankInfo("trusted", 10000, new Dictionary<int, int>())));
					File.WriteAllText(filepath, JsonConvert.SerializeObject(config, Formatting.Indented));
				}
				config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(filepath));


				return config;
			}
			catch (Exception ex)
			{
				TShock.Log.ConsoleError(ex.ToString());
				return new Config();
			}
		}
		
		public Group GetGroup(string name)
		{
			return Groups.Find(x => x.name == name);
		}

		public Group GetClosestGroup(int time)
		{
			return Groups.LastOrDefault(x => x.info.rankCost <= time);
		}
		
		public Group GetNextGroup(string name)
		{
			var group = GetGroup(name);
			if (group is null)
			{
				return Groups.ElementAtOrDefault(0);
			}
			var index = Groups.IndexOf(group);

			if (index == Groups.Count - 1)
			{
				return null;
			}

			return Groups.ElementAtOrDefault(index + 1);
		}

		public Group GetNextGroup(int playtime)
		{
			var group = GetClosestGroup(playtime);
			if (group is null)
			{
				return Groups.ElementAtOrDefault(0);
			}
			
			var index = Groups.IndexOf(group);

			if (index == -1)
			{
				return null;
			}

			return Groups.ElementAtOrDefault(index + 1);
		}
		
		public string GetTimeTillNextGroup(int playtime)
		{
			var group = GetClosestGroup(playtime);
			Group nextGroup;

			nextGroup = group is null ? Groups.ElementAtOrDefault(0) : GetNextGroup(group.name);
			
			// return the difference
			return TimeSpan.FromSeconds(nextGroup.info.rankCost - playtime).ElapsedString();
		}
		
		public void GiveDrops(RankInfo rankInfo, TSPlayer player)
		{
			if (rankInfo.rankUnlocks.Count == 0)
			{
				return;
			}

			foreach (KeyValuePair<int, int> prop in rankInfo.rankUnlocks)
			{
				player.GiveItem(prop.Key, prop.Value, 0);
			}
		}
	}
}
