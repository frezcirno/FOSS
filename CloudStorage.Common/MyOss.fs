module CloudStorage.Common.MyOss

open System.IO
open System.Net
open System.Net
open System.Threading.Tasks
open FSharp.Control.Tasks
open RestSharp


let putObjectAsync (key: string) (stream: Stream) : Task<bool> =
    task {
        stream.Seek(0L, SeekOrigin.Begin) |> ignore

        use mem = new MemoryStream(int stream.Length)
        stream.CopyTo mem
        let bytes = mem.GetBuffer()

        let client =
            RestClient("http://" + Config.MyOss.Server)

        let request =
            RestRequest("/objects/" + key)
                .AddParameter("*/*", bytes, ParameterType.RequestBody)

        let res = client.Put request
        return res.StatusCode = HttpStatusCode.OK
    }

let putObject (key: string) (stream: Stream) = (putObjectAsync key stream).Wait()

let getObjectAsync (key: string) : Task<Stream> =
    task {
        let client =
            RestClient("http://" + Config.MyOss.Server)

        let request = RestRequest("/objects/" + key)
        let res = client.Get request
        let ms = new MemoryStream(res.RawBytes)
        return upcast ms
    }

let getObject (key: string) : Stream = (getObjectAsync key).Result
