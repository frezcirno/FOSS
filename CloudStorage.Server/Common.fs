namespace CloudStorage.Server

[<AutoOpen>]
module Common =

    open System
    open Microsoft.AspNetCore.Authentication.Cookies
    open Microsoft.AspNetCore.Authentication.JwtBearer
    open Giraffe
    open StackExchange.Redis
    open CloudStorage.Common


    let ArgumentError (err: string) = RequestErrors.BAD_REQUEST err

    let jsonResp (code: int) (msg: string) (obj: obj) =
        if obj = null then
            json <| Utils.ResponseBrief code msg
        else
            json <| Utils.Response code msg obj

    let okResp (msg: string) (obj: obj) = jsonResp 0 msg obj

    ///
    /// Authentication
    ///
    /// 刷新用户token
    let UserUpdateToken (user_name: string) (user_token: string) : bool =
        Redis.redis.StringSet(RedisKey(user_name), RedisValue(user_token), TimeSpan.FromHours(1.0))

    let UserValidToken (user_name: string) (user_token: string) : bool =
        Redis.redis.StringGet(RedisKey(user_name)) = RedisValue(user_token)

    let EncryptPasswd =
        Utils.flip (+) Config.Security.Salt
        >> Utils.StringSha1

    let notLoggedIn : HttpHandler =
        RequestErrors.UNAUTHORIZED "Cookie" "SAFE Realm" "You must be logged in."

    let jwtAuthorized : HttpHandler =
        requiresAuthentication (challenge JwtBearerDefaults.AuthenticationScheme)

    let cookieAuthorized : HttpHandler =
        requiresAuthentication (challenge CookieAuthenticationDefaults.AuthenticationScheme)
