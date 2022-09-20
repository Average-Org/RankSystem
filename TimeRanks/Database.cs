using System;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using TShockAPI.DB;

namespace TimeRanks
{
    public class Database
    {
        private readonly IDbConnection _db;

        public Database(IDbConnection db)
        {
            _db = db;

            var sqlCreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            var table = new SqlTable("TimeRanks",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Name", MySqlDbType.VarChar, 50) { Unique = true },
                new SqlColumn("Time", MySqlDbType.Int32),
                new SqlColumn("FirstLogin", MySqlDbType.Text),
                new SqlColumn("LastLogin", MySqlDbType.Text),
                new SqlColumn("TotalTime", MySqlDbType.Int32)
                );
            sqlCreator.EnsureTableStructure(table);
        }

        public bool InsertPlayer(TrPlayer player)
        {
            return _db.Query("INSERT INTO TimeRanks (Name, Time, FirstLogin, Lastlogin, TotalTime)" + "VALUES (@0, @1, @2, @3, @4)", player.name, player.time, player.firstlogin, player.lastlogin, player.totaltime) != 0; 
        }

        public bool DeletePlayer(string player)
        {
            return _db.Query("DELETE FROM TimeRanks WHERE Name = @0", player) != 0;
        }

        public bool SavePlayer(TrPlayer player)
        {
            player.lastlogin = DateTime.UtcNow.ToString("G");
            return _db.Query("UPDATE TimeRanks SET Time = @0, LastLogin = @1, TotalTime = @2 WHERE Name = @3",
                player.time, player.lastlogin, player.totaltime, player.name) != 0;
        }

        public void SaveAllPlayers()
        {
            foreach (
                var player in TimeRanks.Players.Players.Where(player => player.tsPlayer != null && player.tsPlayer.IsLoggedIn)
                )
                SavePlayer(player);
        }

        public void InitialSyncPlayers()
        {
            using (var reader = _db.QueryReader("SELECT * FROM TimeRanks"))
            {
                while (reader.Read())
                {
                    var name = reader.Get<string>("Name");
                    var time = reader.Get<int>("Time");
                    var firstlogin = reader.Get<string>("FirstLogin");
                    var lastlogin = reader.Get<string>("LastLogin");
                    var totaltime = reader.Get<int>("TotalTime");
                    TimeRanks.Players.Add(name, time, firstlogin, lastlogin, totaltime);
                }
            }
        }
    }
}
