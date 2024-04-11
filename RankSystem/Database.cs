using System;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using TShockAPI;
using NuGet.Protocol;

namespace RankSystem
{
    public class Database
    {
        private readonly IDbConnection _db;

        public Database(IDbConnection db)
        {
            _db = db;

            var sqlCreator = new SqlTableCreator(db,
                db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            var table = new SqlTable("RankSystem",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Name", MySqlDbType.VarChar, 50) { Unique = true },
                new SqlColumn("Time", MySqlDbType.Int32),
                new SqlColumn("LastLogin", MySqlDbType.DateTime),
                new SqlColumn("Favorite", MySqlDbType.Text)
            );

            sqlCreator.EnsureTableStructure(table);
        }

        public bool InsertPlayer(TSPlayer player)
        {
            if (player == null)
            {
                return false;
            }

            if (player.IsLoggedIn is false)
            {
                return false;
            }

            try
            {
                return _db.Query($"INSERT INTO RankSystem (Name, Time) VALUES (@0, @1)", player.Account.Name, 0) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to insert a player into the database: {ex.ToString()}");
                return false;
            }
        }


        public bool DeletePlayer(string player)
        {
            try
            {
                return _db.Query("DELETE FROM RankSystem WHERE Name = @0", player) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to delete a player from the database: {ex.ToString()}");
                return false;
            }
        }

        public bool SetFavorite(string player, string favorite)
        {
            try
            {
                return _db.Query("UPDATE RankSystem SET Favorite = @0 WHERE Name = @1", favorite, player) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to set a favorite rank: {ex.ToString()}");
                return false;
            }
        }

        public bool HasFavorite(string player)
        {
            try
            {
                using (var reader = _db.QueryReader("SELECT * FROM RankSystem WHERE Name = @0", player))
                {
                    while (reader.Read())
                    {
                        return !string.IsNullOrWhiteSpace(reader.Get<string>("Favorite"));
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to check if a player has a favorite rank: {ex.ToString()}");
            }

            return false;
        }

        public bool SavePlayer(PlaytimeInformation player)
        {
            try
            {
                return _db.Query($"UPDATE RankSystem SET Time = @0, LastLogin = @1, Favorite=@2 WHERE Name = @3",
                    player.TotalTime, player.LastLogin, player.Favorite, player.AccountName) != 0;
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to save a player to the database: {ex.ToString()}");
                return false;
            }
        }

        public bool IsPlayerInDatabase(string name)
        {
            try
            {
                using (var reader = _db.QueryReader("SELECT * FROM RankSystem WHERE Name = @0", name))
                {
                    while (reader.Read())
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to check if a player is in the database: {ex.ToString()}");
            }

            return false;
        }

        /// <summary>
        /// Retrieves the player's playtime information from the database.
        /// </summary>
        /// <param name="player">A TShock player object</param>
        /// <returns>A PlaytimeInformation object if successful, otherwise null</returns>
        public PlaytimeInformation GrabPlayer(TSPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            if (player.IsLoggedIn is false)
            {
                return null;
            }

            try
            {
                using var reader = _db.QueryReader(
                    "SELECT Name, Time, LastLogin, Favorite FROM RankSystem WHERE Name = @0",
                    player.Account.Name);

                if (reader.Read())
                {
                    var name = reader.Get<string>("Name");
                    var time = reader.Get<int>("Time");
                    var lastLogin = reader.Get<DateTime>("LastLogin");
                    var favorite = reader.Get<string>("Favorite");

                    return new PlaytimeInformation(name, time, lastLogin, favorite);
                }
                else
                {
                    // The player does not exist in the database, so attempt to insert.
                    if (InsertPlayer(player))
                    {
                        // Try fetching the player's information again after insertion.
                        return GrabPlayer(player);
                    }
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError($"Error grabbing or inserting playtime information for player {player.Name}: {ex}");
            }

            return null;
        }

        public PlaytimeInformation GrabPlayerFromAccountName(string name)
        {
            try
            {
                using var reader = _db.QueryReader("SELECT * FROM RankSystem WHERE Name = @0", name);
                while (reader.Read())
                {
                    var pname = reader.Get<string>("Name");
                    var time = reader.Get<int>("Time");
                    var lastlogin = reader.Get<DateTime>("LastLogin");
                    var favorite = reader.Get<string>("Favorite");

                    return new PlaytimeInformation(pname, time, lastlogin, favorite);
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(
                    $"Something went wrong while trying to grab playtime information information via AccountName: {ex.ToString()}");
                return null;
            }

            return null;
        }
    }
}