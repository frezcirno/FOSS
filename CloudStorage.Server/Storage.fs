module CloudStorage.Server.Storage

open System.IO
open CloudStorage.Common

///
/// Storage Backend
///
let private q = new RabbitMq.Queue "Poster"

let SaveFile (fileHash: string) (fileName: string) (fileLength: int64) (stream: Stream) =
    if Database.File.FileHashExists fileHash then
        true
    else
        let tempPath =
            Path.Join [| Config.TEMP_FILE_PATH
                         fileHash |]

        let os = File.OpenWrite tempPath
        stream.CopyTo os
        os.Dispose()

        /// 发布消息
        let msg =
            RabbitMsg(
                FileHash = fileHash,
                CurLocation = tempPath,
                DstLocation = fileHash,
                DstType = RabbitMsg.Types.DstType.Minio
            )

        q.Send Config.Rabbit.TransQueueName msg
        Database.File.CreateFileMeta fileHash fileName fileLength tempPath

///
/// 获取文件，需要手动 Dispose
///
let LoadFile (fileLoc: string) : Stream =
    if fileLoc.StartsWith Config.TEMP_FILE_PATH then
        upcast File.OpenRead fileLoc
    else
        /// 文件已转移到存储系统
        MyOss.getObject fileLoc
