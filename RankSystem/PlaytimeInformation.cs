using System;

namespace RankSystem;

public class PlaytimeInformation
{
    public string AccountName { get; set; }
    public int TotalTime { get; set; }
    public DateTime LastLogin { get; set; }
    public string Favorite { get; set; }
    
    public PlaytimeInformation(string accountName, int totalTime, DateTime lastLogin, string favorite)
    {
        AccountName = accountName;
        TotalTime = totalTime;
        LastLogin = lastLogin;
        Favorite = favorite;
    }
    
    public bool ShouldRankup()
    {
        var nextGroup = RankSystem.config.GetNextGroup(TotalTime);
        
        if (nextGroup == null)
        {
            return false;
        }
        
        return TotalTime >= nextGroup.info.rankCost;
    }
}