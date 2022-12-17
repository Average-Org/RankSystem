using Auxiliary;
using CSF;
using CSF.TShock;
using Appointer.Models;
using System;
using Auxiliary.Configuration;
using static Appointer.Extensions;
using System.Threading.Tasks;
using TShockAPI;

namespace Appointer.Modules
{
    [RequirePermission("tbc.user")]
    internal class UserCommands : TSModuleBase<TSCommandContext>
    {
        [Command("check", "rank", "rankup")]
        public async Task<IResult> CheckRank(string user = "")
        {
            if(user == "")
            {
                var entity = await IModel.GetAsync(GetRequest.Bson<TBCUser>(x => x.AccountName == Context.Player.Account.Name), x => x.AccountName = Context.Player.Account.Name);

                Success($"You currently have: {Extensions.ElapsedString(new TimeSpan(0,0,entity.Playtime))} of playtime.");
                return Info($"You need: [c/90EE90:{Extensions.NextRankCostFormatted(Context.Player.Account)}] left to rank up!");

            }
            else
            {
                var entity = await IModel.GetAsync(GetRequest.Bson<TBCUser>(x => x.AccountName.ToLower() == user.ToLower()));
                var User = TShock.UserAccounts.GetUserAccountByName(user);

                if(entity == null)
                {
                    return Error("Invalid player name!");
                }

                Success($"{user} currently has: {Extensions.ElapsedString(new TimeSpan(0, 0, entity.Playtime))} of playtime.");
                return Info($"They need: [c/90EE90:{Extensions.NextRankCostFormatted(User)}] left to rank up!");
            }
        }
    }
}
