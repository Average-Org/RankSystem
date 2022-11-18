# Average's RankSystem
Support me & this plugin's (along with several others) development on Ko.Fi: [Here!](https://ko-fi.com/averageterraria)

This is a complete rewrite of another plugin I originally updated, but then started implementing features to. Now, I have gone over all the code, and completely re-done almost all of it! The way it works is much more efficient, speedy, and easy for the user to use.

**REQUIRES:** [SimpleEcon](https://github.com/Average-Org/SimpleEcon)

### Features
- Time based rank progression system (unlimited ranks!)
- **REQUIRES:** [SimpleEcon](https://github.com/Average-Org/SimpleEcon) - can utilize economy to boost rank, with a modifier. See config explanation for how this modifier works

## Config Explained
(tshock/RankSystem.json)

```json
{
  "Groups": [
    {
      "name": "vip",
      "info": {
        "nextGroup": "trusted",
        "rankCost": 15000,
        "rankUnlocks": {}
      }
    },
      {
        "name": "trusted",
        "info": {
          "nextGroup": "admin",
          "rankCost": 65000,
          "rankUnlocks": {}
        }
  }
  ],
  "StartGroup": "default",
  "doesCurrencyAffectRankTime": false,
  "currencyAffect": 1
}

```
Very easy and self-explanatory, but here is an explanation of each field regardless.

`StartGroup` essentially gets skipped, your gonna wanna keep this one at default.
`doesCurrencyAffectRankTime` and `currencyAffect` are complementary. If set to true, all of the rank times are also affected by the user's economy level. the `currencyAffect` is a percentage value. For example, if it is set to 5, then 5% of the user's balance adds to the user's playtime (in seconds). If set to 100, then the user balance will add the entire balance (in seconds) to the user's playtime.
`Groups` has three main values. Each group has a `name` such as `"member"`, and `nextGroup` is the next group that will come after the `rankCost` has been achieved. This is the user's playtime in seconds.
 
## Commands List 

| Command        |Description           |Usage  |Permission    |
| ------------- |:-------------:| :-----:| :-----------:|
| /rank    |Shows the user's playtime and next rank info | /rank (optional: `playerName`) (Alias: /check, /rankup | rs.user |
| /rankdelete    |Delete's a user's playtime | /rankdelete `playerName` | se.admin |

## Plugin Dev Implementations
Simply add this plugin as a dependency for yours and you'll be able to use the following:

### Retrieving a user's info
```c#

//Retrieve a user's next rank
RankSystem.PlayerManager.getPlayer(player).NextRankInfo.nextGroup

//Retrieve a user's next rank
RankSystem.PlayerManager.getPlayer(player).NextRankInfo.rankCost;

// and plenty of others within
RankSystem.PlayerManager :D
```
