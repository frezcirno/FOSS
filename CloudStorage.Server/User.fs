module CloudStorage.Server.User

open System
open System.IdentityModel.Tokens.Jwt
open System.Security.Claims
open System.Text
open CloudStorage.Common
open Giraffe
open FSharp.Control.Tasks
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Http
open Microsoft.IdentityModel.Tokens

[<CLIMutable>]
type UserRegisterBlock = { username: string; password: string }

/// 用户注册接口
let UserRegister (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<UserRegisterBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            if args.username.Length < 3
               || args.password.Length < 5 then
                return! ArgumentError "Invalid parameter" next ctx
            else
                let enc_password = EncryptPasswd args.password

                if Database.User.UserRegister args.username enc_password then
                    return! okResp "OK" null next ctx
                else
                    return! ServerErrors.serviceUnavailable id next ctx
    }

let BuildToken (username: string) =
    let tokenHandler = JwtSecurityTokenHandler()
    let tokenDescriptor = SecurityTokenDescriptor()

    tokenDescriptor.Subject <-
        ClaimsIdentity(
            [| Claim(JwtRegisteredClaimNames.Aud, "api")
               Claim(JwtRegisteredClaimNames.Iss, "http://7c00h.xyz/cloud")
               Claim(ClaimTypes.Name, username) |],
            JwtBearerDefaults.AuthenticationScheme
        )

    tokenDescriptor.Expires <- DateTime.UtcNow.AddHours(1.0)

    tokenDescriptor.SigningCredentials <-
        SigningCredentials(
            SymmetricSecurityKey(Encoding.ASCII.GetBytes Config.Security.Secret),
            SecurityAlgorithms.HmacSha256Signature
        )

    let securityToken = tokenHandler.CreateToken tokenDescriptor
    let writeToken = tokenHandler.WriteToken securityToken
    writeToken

[<CLIMutable>]
type UserLoginBlock = { username: string; password: string }

/// 用户登录接口
let UserLogin (next: HttpFunc) (ctx: HttpContext) =
    task {
        match! ctx.TryBindFormAsync<UserLoginBlock>() with
        | Error msg -> return! ArgumentError msg next ctx
        | Ok args ->
            let enc_password = EncryptPasswd args.password

            if Database.User.GetUserByUsernameAndUserPwd args.username enc_password then
                let token = BuildToken(args.username)

                if UserUpdateToken args.username token then
                    let ret =
                        {| FileLoc =
                               ctx.Request.Scheme
                               + "://"
                               + ctx.Request.Host.Value
                               + "/"
                           Username = args.username
                           AccessToken = token |}

                    return! okResp "OK" ret next ctx
                else
                    return! ServerErrors.SERVICE_UNAVAILABLE "SERVICE_UNAVAILABLE" next ctx
            else
                return! RequestErrors.FORBIDDEN "Wrong password" next ctx
    }

///// 用户注销接口
//let UserLogout (next: HttpFunc) (ctx: HttpContext) =
//    task {
//        do! ctx.SignOutAsync()
//        return! redirectTo false "/" next ctx
//    }

/// 用户信息查询接口
let UserInfoHandler (next: HttpFunc) (ctx: HttpContext) =
    let username = ctx.User.FindFirstValue ClaimTypes.Name

    match Database.User.GetUserByUsername username with
    | None -> ServerErrors.INTERNAL_ERROR "User not found" next ctx
    | Some user -> okResp "OK" user next ctx
