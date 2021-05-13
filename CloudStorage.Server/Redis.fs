module CloudStorage.Server.Redis

open System
open System.Collections.Generic
open StackExchange.Redis

let redisOption =
    ConfigurationOptions.Parse Config.Redis.Host

redisOption.Password <- Config.Redis.Pass

let redis =
    ConnectionMultiplexer.Connect(redisOption)

let db = redis.GetDatabase()
db.Ping() |> ignore


/// 刷新用户token
let UserUpdateToken (user_name: string) (user_token: string) : bool =
    db.StringSet(RedisKey(user_name), RedisValue(user_token), TimeSpan.FromHours(1.0))


let UserValidToken (user_name: string) (user_token: string) : bool =
    db.StringGet(RedisKey(user_name)) = RedisValue(user_token)