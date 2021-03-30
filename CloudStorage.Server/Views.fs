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
            div [] [ a [ _href "/ping" ] [ Text "ping pong" ] ]
            div [] [ a [ _href "/time" ] [ Text "时间" ] ]
            div [] [ a [ _href "/file/upload" ] [ Text "文件上传" ] ]
            div [] [ a [ _href "/file/upload/suc" ] [ Text "文件上传成功" ] ]
            div [] [
                form [ _action "/file/meta"; _method "GET" ] [
                    input [ _type "text"; _id "filehash"; _name "filehash" ]
                    button [ _type "submit" ] [ Text "查询" ]
                ]
            ]
            div [] [ a [ _href "/file/query" ] [ Text "近期文件查询" ] ]
            div [] [
                form [ _action "/file/update"; _method "POST" ] [
                    input [ _type "text"; _id "filehash"; _name "filehash" ]
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
     