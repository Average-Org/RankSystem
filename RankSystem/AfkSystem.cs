using Microsoft.Xna.Framework;
using TShockAPI;

namespace RankSystem;

public class AfkSystem
{
    public static void ToggleAfk(TSPlayer player) => player.SetData("afk", !player.GetData<bool>("afk"));
    public static void SetAfk(TSPlayer player, bool afk) => player.SetData("afk", afk);
    public static bool IsAfk(TSPlayer player) => player.GetData<bool>("afk");

    public static void SetAfkTime(TSPlayer player, int time) => player.SetData("afkTime", time);
    public static int GetAfkTime(TSPlayer player) => player.GetData<int>("afkTime");

    public static Vector2 GetLastPosition(TSPlayer player) => player.GetData<Vector2>("lastPos");
    public static void SetLastPosition(TSPlayer player) => player.SetData("lastPos", player.LastNetPosition);

    /// <summary>
    /// Determines if a player is AFK and handles the AFK time.
    /// </summary>
    /// <param name="player">TShock player</param>
    /// <returns>True if the player is AFK, false otherwise</returns>
    /// <summary>
    /// Determines if a player is AFK and handles the AFK time. It updates player's AFK status
    /// based on their activity and broadcasts a notification if there's a change in their AFK status.
    /// </summary>
    /// <param name="player">TShock player</param>
    /// <returns>True if the player's AFK status changed to AFK due to timeout, false otherwise</returns>
    public static bool HandleAfk(TSPlayer player)
    {
        bool isPlayerAfk = IsAfk(player); // Initial AFK status
        var lastPos = GetLastPosition(player);
        int currentAfkTime = GetAfkTime(player); // Current AFK time is fetched once to optimize performance

        if (lastPos == player.LastNetPosition)
        {
            SetAfkTime(player, currentAfkTime + 1);
        }
        else
        {
            if (currentAfkTime > 0)
            {
                SetAfkTime(player, 0);
                SetAfk(player, false);
                TSPlayer.All.SendInfoMessage($"{player.Name} is no longer AFK!");
            }
            // Update last known position only if it has changed to avoid unnecessary operations
            SetLastPosition(player);
        }

        // Rechecking AFK time in case it was reset in the above condition
        if (GetAfkTime(player) >= RankSystem.config.afkTime && !isPlayerAfk)
        {
            SetAfk(player, true);
            TSPlayer.All.SendInfoMessage($"{player.Name} is now AFK!");
            return true; // Indicates that the player has just been marked as AFK due to timeout
        }

        return IsAfk(player);
    }

}