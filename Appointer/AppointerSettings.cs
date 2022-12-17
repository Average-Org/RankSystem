using Auxiliary.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using TShockAPI;
using System.Text.Json.Serialization;

namespace Appointer
{
	public class Group : JsonAttribute
    {
		[JsonPropertyName("Name")]
		public string Name { get; set; }

		[JsonPropertyName("NextRank")]
		public string NextRank { get; set; }
		
		[JsonPropertyName("Cost")]
		public int Cost { get; set; }

		[JsonConstructor]
		public Group(string name, string nextRank, int cost)
		{
			Name = name;
			NextRank = nextRank;
			Cost = cost;
		}
    }

	public class AppointerSettings : ISettings
    {
		[JsonPropertyName("StartGroup")]
		public string StartGroup { get; set; } = "default";

        [JsonPropertyName("DoesCurrencyAffectRankTime")]
        public bool DoesCurrencyAffectRankTime { get; set; } = false;

		[JsonPropertyName("CurrencyMultiplier")]
        public int CurrencyMultiplier { get; set; } = 1;

		[JsonPropertyName("UseAFKSystem")]
		public bool UseAFKSystem { get; set; } = true;

		[JsonPropertyName("Groups")]
		public List<Group> Groups { get; set; } = new List<Group>() { new Group("default", "next", 1000) };
	}
}
