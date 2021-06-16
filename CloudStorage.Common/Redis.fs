module CloudStorage.Common.Redis

open StackExchange.Redis

let private redisOption = ConfigurationOptions.Parse Config.Redis

let private redisConn =
    ConnectionMultiplexer.Connect(redisOption)

let redis =
    let redis = redisConn.GetDatabase(0)
    printfn "Redis: %f" (redis.Ping().TotalSeconds)
    redis

let redis1 =
    let redis1 = redisConn.GetDatabase(1)
    printfn "Redis1: %f" (redis1.Ping().TotalSeconds)
    redis1
