module CloudStorage.Server.Redis

open StackExchange.Redis

let redisOption = ConfigurationOptions.Parse Config.redishost

redisOption.Password <- Config.redispass

let redis = ConnectionMultiplexer.Connect(redisOption)

let db = redis.GetDatabase()
db.Ping() |> ignore