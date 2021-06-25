module CloudStorage.Common.Es

open System.IO
open System.Net
open RestSharp


type Metadata =
    { Name: string
      Version: int
      Size: int64
      Hash: string }

type hit = { Source: Metadata }

type searchResult = { Total: int; Hits: hit [] }

let getMetadata (name: string) (versionId: int) =
    let client =
        RestClient("http://" + Config.Elastic.Server)

    let request =
        RestRequest(sprintf "/metadata/objects/%s_%d/_source" name versionId)

    let res = client.Get request
    if res.StatusCode <> HttpStatusCode.OK then
        ()
    else
        use mem = new MemoryStream(res.RawBytes)
        let bytes = mem.GetBuffer()
        ()
        ///

let SearchLatestVersion (name: string) =
    let client =
        RestClient("http://" + Config.Elastic.Server)

    let request =
        RestRequest("/metadata/_search")
            .AddQueryParameter("q", $"name:%s{name}")
            .AddQueryParameter("size", "1")
            .AddQueryParameter("sort", "version:desc")
    

    let res = client.Get request
    if res.StatusCode <> HttpStatusCode.OK then
        ()
    else
        use mem = new MemoryStream(res.RawBytes)
        ()
        ///
    
let GetMetadata (name: string) (version: int) (size: int64) (hash: string) =
    if version = 0 then
        SearchLatestVersion name
    else
        getMetadata name version
        
//let PutMetadata (name: string) (version: int) (size: int64) (hash: string) =
//    let doc = sprintf 