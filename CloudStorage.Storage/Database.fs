module CloudStorage.Storage.Database

open System
open MySqlConnector

let conn =
    use conn = new MySqlConnection(Config.Data.datasource)
    System.Diagnostics.Debug.WriteLine (sprintf  $"Mysql: %d{if conn.Ping() then 1 else 0}")
    conn
