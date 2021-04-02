module CloudStorage.Server.Views

open Giraffe.GiraffeViewEngine

let index =
    html [] [
        head [] [
            title [] [ str "Giraffe Sample" ]
            script [] [ rawText "
                        function updateToken(username, token) {
                            document.querySelectorAll('.username').forEach(x => x.value = username)
                            document.querySelectorAll('.token').forEach(x => x.value = token)
                        }
                        function signin() {
                            let username = document.getElementById('username').value;
                            let password = document.getElementById('password').value;
                            let formData = new FormData();
                            formData.append('username', username);
                            formData.append('password', password);
                            fetch('/user/signin', {
                              method: 'POST',
                              body: formData
                            })
                            .then(response => response.json())
                            .then(response => { console.log(response); return response; })
                            .then(response => response.data)
                            .then(data => updateToken(data.username, data.token))
                            .catch(error => console.error('Error:', error));
                        }" ]
        ]
        body [] [
            h1 [] [
                str "Hello world!"
            ]
            div [] [
                label [  ] [ 
                    str "用户名:"
                    input [ _id "username"; _type "text"; _name "username" ]
                ]
                label [  ] [ 
                    str "密码:"
                    input [ _id "password"; _type "password"; _name "password" ]
                ]
                input [ _type "text"; _class "token"; _disabled ]
                button [ _onclick "signin()" ] [ str "刷新" ]
            ]
            div [] [ a [ _href "/ping" ] [ str "ping pong" ] ]
            div [] [ a [ _href "/time" ] [ str "时间" ] ]
            div [] [ a [ _href "/file/upload" ] [ str "文件上传" ] ]
            div [] [ a [ _href "/file/upload/suc" ] [ str "文件上传成功" ] ]
            div [] [
                form [ _action "/file/upload"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [  ] [
                        str "选择文件:"
                        input [ _type "file"; _name "attach"; _multiple ]
                    ]
                    input [ _hidden; _type "text"; _class "username"; _name "username" ]
                    input [ _hidden; _type "text"; _class "token"; _name "token" ]
                    button [ _type "submit" ] [ str "上传" ]
                ]
            ]
            div [] [
                form [ _action "/file/meta"; _method "GET" ] [
                    label [  ] [
                        str "文件hash:" 
                        input [ _type "text"; _name "filehash" ]
                    ]
                    label [  ] [
                        str "新文件名:" 
                        input [ _type "text"; _name "filename" ]
                    ]
                    input [ _hidden; _type "text"; _class "username"; _name "username" ]
                    input [ _hidden; _type "text"; _class "token"; _name "token" ]
                    button [ _type "submit" ] [ str "查询" ]
                ]
            ]
            div [] [ button [ _onclick "window.location.replace('/file/query?');" ] [ str "近期文件查询" ] ]
            div [] [
                form [ _action "/file/update"; _method "POST" ] [
                    label [  ] [
                        str "文件hash:" 
                        input [ _type "text"; _name "filehash" ]
                    ]
                    label [  ] [
                        str "新文件名:" 
                        input [ _type "text"; _name "filename" ]
                    ]
                    input [ _hidden; _type "text"; _id "op"; _name "op"; _value "O" ]
                    input [ _hidden; _type "text"; _class "username"; _name "username" ]
                    input [ _hidden; _type "text"; _class "token"; _name "token" ]
                    button [ _type "submit" ] [ str "修改" ]
                ]
            ]
            div [] [
                form [ _action "/file/download"; _method "GET" ] [
                    label [  ] [
                        str "文件hash:" 
                        input [ _type "text"; _name "filehash" ]
                    ]
                    input [ _hidden; _type "text"; _class "username"; _name "username" ]
                    input [ _hidden; _type "text"; _class "token"; _name "token" ]
                    button [ _type "submit" ] [ str "下载" ]
                ]
            ]
            div [] [
                form [ _action "/user/signup"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [  ] [ 
                        str "用户名:"
                        input [ _type "text"; _name "username" ]
                    ]
                    label [  ] [ 
                        str "密码:"
                        input [ _type "password"; _name "password" ]
                    ]
                    button [ _type "submit" ] [ str "注册" ]
                ]
            ]
            div [] [
                form [ _action "/user/signin"; _method "POST"; _enctype "multipart/form-data" ] [
                    label [  ] [ 
                        str "用户名:"
                        input [ _type "text"; _name "username" ]
                    ]
                    label [  ] [ 
                        str "密码:"
                        input [ _type "password"; _name "password" ]
                    ]
                    button [ _type "submit" ] [ str "登录" ]
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
                    button [ _type "submit" ] [ str "上传" ]
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
                    button [ _type "submit" ] [ str "注册" ]
                ]
            ]
        ]
    ]
     