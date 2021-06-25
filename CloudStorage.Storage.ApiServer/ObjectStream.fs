module CloudStorage.Storage.ApiServer.ObjectStream

open System.IO
open RestSharp


let GetStream (server: string) (object: string) : Stream =
    let client = RestClient("http://" + server)
    let request = RestRequest("/objects/" + object)
    let res = client.Get request
    upcast new MemoryStream(res.RawBytes)

let PutStream (server: string) (object: string) (data: Stream) =
    let client = RestClient("http://" + server)
    let request = RestRequest("/objects/" + object)
    use mem = new MemoryStream(int data.Length)
    data.CopyTo mem
    let bytes = mem.GetBuffer()

    request.AddParameter("*/*", bytes, ParameterType.RequestBody)
    |> ignore

    let res = client.Put request
    new MemoryStream(res.RawBytes)
