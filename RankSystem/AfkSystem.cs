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

    public static bool HandleAfk(TSPlayer player)
    {
        var isPlayerAfk = IsAfk(player);
        var lastPos = GetLastPosition(player);

        if (lastPos == player.LastNetPosition)
        {
            var currentAfkTime = GetAfkTime(player);
            SetAfkTime(player, currentAfkTime + 1);
        }
        else if (isPlayerAfk && lastPos != player.LastNetPosition)
        {
            SetAfkTime(player, 0);
            ToggleAfk(player);
            TSPlayer.All.SendInfoMessage($"{player.Name} is no longer AFK!");
        }

        SetLastPosition(player);
        var afkTime = GetAfkTime(player);

        if (afkTime >= 25 && !isPlayerAfk)
        {
            ToggleAfk(player);
            TSPlayer.All.SendInfoMessage($"{player.Name} is now AFK!");
            return true;
        }

        return isPlayerAfk;
    }
}