module CloudStorage.Storage.Config

let private env = System.Environment.GetEnvironmentVariables()

let Oss = {|
    Endpoint = env.["OSSENDPOINT"] :?> string
    AccessKeyId = env.["OSSACCESSKEYID"] :?> string
    AccessKeySecret = env.["OSSACCESSKEYSECRET"] :?> string
    Bucket = "fcirno-test"
|}