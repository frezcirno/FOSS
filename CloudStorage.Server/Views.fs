module CloudStorage.Server.Views

open Giraffe.GiraffeViewEngine

let index =
    html [] [
        head [] [
            title [] [ str "Giraffe Sample" ]
        ]
        body [] [
            h1 [] [
                Text "Hello world!"
            ]
            div [] [
                button [ _onclick "" ] [ Text "刷新" ]
            ]
            div [] [ a [ _href "/ping" ] [ Text "ping pong" ] ]
            div [] [ a [ _href "/time" ] [ Text "时间" ] ]
            div [] [ a [ _href "/file/upload" ] [ Text "文件上传" ] ]
            div [] [ a [ _href "/file/upload/suc" ] [ Text "文件上传成功" ] ]
            div [] [
                form [ _action "/file/upload"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [ _for "myfile" ] [ str "选择文件:" ]
                    input [ _type "file"; _id "myfile"; _multiple ]
                    button [ _type "submit" ] [ Text "上传" ]
                ]
            ]
            div [] [
                form [ _action "/file/meta"; _method "GET" ] [
                    label [ _for "filehash" ] [ str "文件hash:" ]
                    input [ _type "text"; _id "filehash" ]
                    button [ _type "submit" ] [ Text "查询" ]
                ]
            ]
            div [] [ a [ _href "/file/query" ] [ Text "近期文件查询" ] ]
            div [] [
                form [ _action "/file/update"; _method "POST" ] [
                    label [ _for "filehash" ] [ str "文件hash:" ]
                    input [ _type "text"; _id "filehash"; _name "filehash" ]
                    label [ _for "filename" ] [ str "新文件名:" ]
                    input [ _type "text"; _id "filename"; _name "filename" ]
                    input [ _hidden; _type "text"; _id "op"; _name "op"; _value "O" ]
                    button [ _type "submit" ] [ Text "修改" ]
                ]
            ]
            div [] [
                form [ _action "/file/download"; _method "GET" ] [
                    input [ _type "text"; _id "filehash"; _name "filehash" ]
                    button [ _type "submit" ] [ Text "下载" ]
                ]
            ]
            div [] [
                form [ _action "/user/signup"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [ _for "username" ] [ str "用户名:" ]
                    input [ _type "text"; _id "username"; _name "username" ]
                    label [ _for "password" ] [ str "密码:" ]
                    input [ _type "password"; _id "password"; _name "password" ]
                    button [ _type "submit" ] [ Text "注册" ]
                ]
            ]
            div [] [
                form [ _action "/user/signin"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [ _for "username" ] [ str "用户名:" ]
                    input [ _type "text"; _id "username"; _name "username" ]
                    label [ _for "password" ] [ str "密码:" ]
                    input [ _type "password"; _id "password"; _name "password" ]
                    button [ _type "submit" ] [ Text "登录" ]
                ]
            ]
        ]
    ]
 
let upload =
    html [] [
        head [] [
            title [] [ str "Giraffe Sample" ]
            script [ _src "" ] []
        ]
        body [] [
            div [] [
                form [ _action "/file/upload"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [ _for "myfile" ] [ str "Select a file:" ]
                    input [ _type "file"; _id "myfile"; _name "attach"; _multiple ]
                    button [ _type "submit" ] [ Text "上传" ]
                ]
            ]
        ]
    ]


let signup =
    html [] [
        head [] [
            title [] [ str "Giraffe Sample" ]
            script [ _src "" ] []
        ]
        body [] [
            div [] [
                form [ _action "/user/signup"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [ _for "username" ] [ str "用户名:" ]
                    input [ _type "text"; _id "username"; _name "username" ]
                    label [ _for "password" ] [ str "密码:" ]
                    input [ _type "password"; _id "password"; _name "password" ]
                    button [ _type "submit" ] [ Text "注册" ]
                ]
            ]
        ]
    ]
     