module CloudStorage.Server.Redis

open StackExchange.Redis

let private redisOption = ConfigurationOptions.Parse Config.Redis

let private redisConn =
    ConnectionMultiplexer.Connect(redisOption)

let redis = redisConn.GetDatabase()

redis.Ping() |> ignore
