# Simple Distributed Object Storage System + NetDisk Backend

## Overview

A simple distributed object storage system, and a netdisk backend based on it. 

This project is almost implemented in `FSharp`.

## Structure

- `CloudStorage.Common`: Common Component
- `CloudStorage.Message`: Protobuf-Generated Classes
- `CloudStorage.Server`: NetDisk Backend
- `CloudStorage.Storage.ApiServer`: Api Server of the OSS 
- `CloudStorage.Storage.DataServer`: Data Server of the OSS

## Usage

1. Install RabbitMQ, Redis, MySQL

2. Change configurations in `CloudStorage.Common/Config.fs`

3. Start

    ```bash
    # Start Data Servers
    LISTEN_ADDRESS=localhost:9991 dotnet ./data/CloudStorage.Storage.DataServer.dll --server.urls "http://0.0.0.0:9991" > data1.out
    LISTEN_ADDRESS=localhost:9992 dotnet ./data/CloudStorage.Storage.DataServer.dll --server.urls "http://0.0.0.0:9992" > data2.out
    LISTEN_ADDRESS=localhost:9993 dotnet ./data/CloudStorage.Storage.DataServer.dll --server.urls "http://0.0.0.0:9993" > data3.out

    # Start Api Servers
    dotnet ./api/CloudStorage.Storage.ApiServer.dll --server.urls "http://localhost:8881" > api1.out
    dotnet ./api/CloudStorage.Storage.ApiServer.dll --server.urls "http://localhost:8882" > api2.out
    dotnet ./api/CloudStorage.Storage.ApiServer.dll --server.urls "http://localhost:8883" > api3.out

    # (Optional) Change the OSS location in CloudStorage.Common/Config.fs and recompile
    # Start Netdisk backend servers
    dotnet ./server/CloudStorage.Server.dll --server.urls "http://0.0.0.0:8000" > serv.out
    ```

## API 

- `GET <oss_data_server>/object/<object_name>` - Get object
- `PUT <oss_data_server>/object/<object_name>` - Put object

- `GET <oss_api_server>/object/<object_name>` - Get object
- `PUT <oss_api_server>/object/<object_name>` - Put object
- `GET <oss_api_server>/locate/<object_name>` - Locate object

- `GET <netdisk_backend_server>/user/signup` - Sign up
- `GET <netdisk_backend_server>/user/signin` - Sign in
- `GET <netdisk_backend_server>/user/info` - Retrieve my user info
- `GET <netdisk_backend_server>/file/upload` - Upload a file
- `GET <netdisk_backend_server>/file/meta` - Get file meta
- `GET <netdisk_backend_server>/file/recent` - Retrieve recent files
- `GET <netdisk_backend_server>/file/download` - Download a file
- `GET <netdisk_backend_server>/file/update` - Update a file (currently, just rename)
- `GET <netdisk_backend_server>/file/delete` - Delete a file
- `GET <netdisk_backend_server>/file/fastupload` - Try fast upload a file
- `GET <netdisk_backend_server>/file/mpupload/init` - Init a multi-part upload task
- `GET <netdisk_backend_server>/file/mpupload/uppart` - Upload a file part
- `GET <netdisk_backend_server>/file/mpupload/complete` - Commit an upload task
- `GET <netdisk_backend_server>/file/mpupload/cancel` - Abort an upload task
- `GET <netdisk_backend_server>/file/mpupload/status` - Check the status of an upload task