using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace RankSystem
{
	public class RankInfo
	{
		public readonly string nextGroup;
		public readonly int rankCost;
		public readonly Dictionary<int, int> rankUnlocks;

		public RankInfo(string nextGroup, int rankCost, Dictionary<int, int> rankUnlocks)
		{
			this.nextGroup = nextGroup;
			this.rankCost = rankCost;
			this.rankUnlocks = rankUnlocks;
		}
	}

	public class Config
    {
		public string StartGroup { get; set; } = "default";
        public bool doesCurrencyAffectRankTime { get; set; } = false;
        public int currencyAffect { get; set; } = 1;

        public Dictionary<string, RankInfo> Groups { get; set; } = new Dictionary<string, RankInfo> //new Dictionary<string, RankInfo>();
        {
        };

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
