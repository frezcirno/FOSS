module CloudStorage.Storage.ApiServer.ObjectStream

open System.IO
open System.Threading.Tasks
open RestSharp
open System.Net
open FSharp.Control.Tasks

let GetStream (server: string) (object: string) : Stream =
    let client = RestClient("http://" + server)
    let request = RestRequest("/object/" + object)
    let res = client.Get request
    upcast new MemoryStream(res.RawBytes)

let PutStream (server: string) (object: string) (data: Stream) : Task<bool> =
    task {
        let client = RestClient("http://" + server)

        use mem = new MemoryStream()
        do! data.CopyToAsync mem
        let bytes = mem.ToArray()

        let request =
            RestRequest("/object/" + object)
                .AddParameter("application/x-msdownload", bytes, ParameterType.RequestBody)

        let res = client.Put request
        return res.StatusCode = HttpStatusCode.OK
    }
