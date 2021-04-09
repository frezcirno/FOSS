namespace CloudStorage.Server

module Config = 
    let env = System.Environment.GetEnvironmentVariables()
    let OSSEndpoint = env.["OSSENDPOINT"] :?> string
    let OSSAccessKeyId = env.["OSSACCESSKEYID"] :?> string
    let OSSAccessKeySecret = env.["OSSACCESSKEYSECRET"] :?> string
    let OSSBucket = "fcirno-test"
    let salt = env.["SALT"] :?> string
    let tokensalt = env.["TOKENSALT"] :?> string
    let datasource = "Server=localhost;Database=test;User=root;Password=root"
    let redishost = "localhost"
    let redispass = "root"