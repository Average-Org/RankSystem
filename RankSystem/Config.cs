using System;
using System.Collections.Generic;
using System.IO;
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
        public bool doesCurrencyAffectRankTime { get; set; } = false;
        public int currencyAffect { get; set; } = 1;

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
	}
}
