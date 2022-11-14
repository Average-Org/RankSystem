﻿using System;
using System.Data;
using System.Linq;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using TShockAPI;

namespace RankSystem
{
    public class Database
    {
        private readonly IDbConnection _db;

        public Database(IDbConnection db)
        {
            _db = db;

            var sqlCreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

            var table = new SqlTable("RankSystem",
                new SqlColumn("ID", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("Name", MySqlDbType.VarChar, 50) { Unique = true },
                new SqlColumn("Time", MySqlDbType.Int32),
                new SqlColumn("FirstLogin", MySqlDbType.DateTime),
                new SqlColumn("LastLogin", MySqlDbType.DateTime)
                );
            sqlCreator.EnsureTableStructure(table);
        }

        public bool InsertPlayer(RPlayer player)
        {
            return _db.Query("INSERT INTO RankSystem (Name, Time, FirstLogin, Lastlogin)" + "VALUES (@0, @1, @2, @3)", player.name, player.totaltime, player.firstlogin, player.lastlogin) != 0; 
        }
        

        public bool DeletePlayer(string player)
        {
            return _db.Query("DELETE FROM RankSystem WHERE Name = @0", player) != 0;
        }

        public bool SavePlayer(RPlayer player)
        {
            player.lastlogin = DateTime.UtcNow;
            return _db.Query("UPDATE RankSystem SET Time = @0, LastLogin = @1 WHERE Name = @2",
                player.totaltime, player.lastlogin, player.name) != 0;
        }

        public void SaveAllPlayers()
        {
            foreach (var player in RankSystem._players)
                SavePlayer(player);
        }

        public bool RetrievePlayer(TSPlayer player)
        {
            using (var reader = _db.QueryReader("SELECT * FROM RankSystem WHERE Name = @0", player.Name))
            {
                while (reader.Read())
                {
                    var name = reader.Get<string>("Name");
                    var time = reader.Get<int>("Time");
                    var firstlogin = reader.Get<DateTime>("FirstLogin");
                    var lastlogin = reader.Get<DateTime>("LastLogin");

                    RankSystem._players.Add(new RPlayer(name, time, firstlogin));
                    return true;
                }
                return false;
            }
        }
    }
}
