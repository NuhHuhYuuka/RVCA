using SecurityData.Models;
using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using System.IO;

namespace SecurityData.Services
{
    public class DatabaseService
    {
        private readonly string dbPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatApp.db");

        public DatabaseService()
        {
            ExecuteNonQuery(@"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Sender TEXT NOT NULL,
                    Receiver TEXT NOT NULL,
                    Content TEXT,
                    IsFile INTEGER NOT NULL,
                    FileName TEXT,
                    FilePath TEXT,
                    Nonce TEXT,
                    Tag TEXT,
                    TransferId TEXT,
                    Time DATETIME NOT NULL
                )");
        }

        private void ExecuteNonQuery(string sql)
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                new SqliteCommand(sql, conn).ExecuteNonQuery();
            }
        }

        public void SaveMessage(ChatMessage msg)
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                string sql = @"
                    INSERT INTO Messages
                    (Sender, Receiver, Content, IsFile, FileName, FilePath, Nonce, Tag, TransferId, Time)
                    VALUES
                    (@s, @r, @c, @f, @fn, @fp, @n, @t, @tf, datetime('now'))";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@s", msg.Sender);
                    cmd.Parameters.AddWithValue("@r", msg.Receiver);
                    cmd.Parameters.AddWithValue("@c", msg.Content);
                    cmd.Parameters.AddWithValue("@f", msg.IsFile ? 1 : 0);
                    cmd.Parameters.AddWithValue("@fn", (object)msg.FileName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fp", (object)msg.FilePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@n", (object)msg.Nonce ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@t", (object)msg.Tag ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tf", (object)msg.TransferId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<ChatMessage> GetHistory(string peer)
        {
            var list = new List<ChatMessage>();

            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();

                string sql = @"
                    SELECT * FROM Messages
                    WHERE Sender = @peer OR Receiver = @peer
                    ORDER BY Time ASC";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@peer", peer);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ChatMessage
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Sender = reader["Sender"].ToString(),
                                Receiver = reader["Receiver"].ToString(),
                                Content = reader["Content"].ToString(),
                                IsFile = Convert.ToInt32(reader["IsFile"]) == 1,
                                FileName = reader["FileName"]?.ToString(),
                                FilePath = reader["FilePath"]?.ToString(),
                                Nonce = reader["Nonce"]?.ToString(),
                                Tag = reader["Tag"]?.ToString(),
                                TransferId = reader["TransferId"]?.ToString(),
                                Timestamp = Convert.ToDateTime(reader["Time"])
                            });
                        }
                    }
                }
            }

            return list;
        }
        public void UpdateMessage(int id, ChatMessage msg)
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();

                string sql = @"
            UPDATE Messages
            SET Content = @c,
                IsFile = @f,
                FileName = @fn,
                FilePath = @fp,
                Nonce = @n,
                Tag = @t,
                TransferId = @tf
            WHERE Id = @id";

                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@c", msg.Content);
                    cmd.Parameters.AddWithValue("@f", msg.IsFile ? 1 : 0);
                    cmd.Parameters.AddWithValue("@fn", (object)msg.FileName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@fp", (object)msg.FilePath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@n", (object)msg.Nonce ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@t", (object)msg.Tag ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@tf", (object)msg.TransferId ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteMessage(int id)
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();

                using (var cmd = new SqliteCommand("DELETE FROM Messages WHERE Id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void DeleteConversation(string peer)
        {
            using (var conn = new SqliteConnection($"Data Source={dbPath}"))
            {
                conn.Open();
                using (var cmd = new SqliteCommand(
                    "DELETE FROM Messages WHERE Sender = @peer OR Receiver = @peer", conn))
                {
                    cmd.Parameters.AddWithValue("@peer", peer);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}