module CloudStorage.Server.Test

open System
open System.Threading.Tasks
open FSharp.Control.Tasks

let test () =
    Console.Out.WriteLine("1")

    let AppendStream (os: int) (is: int) : Task<int> =
        task {
            do! Task.Delay(1000)
            printfn "."
            return os + is
        }

    let MergePartsAsync (chunkCount: int) : Task<int> =
        let stream = 0

        let output =
            [ 0 .. chunkCount ]
            |> List.fold
                (fun (os: Task<int>) (index: int) ->
                    os.ContinueWith
                        (fun (task: Task<int>) ->
                            let file = 1
                            (AppendStream task.Result file).Result))
                (Task.FromResult(stream))

        output

    let x = MergePartsAsync 5
    printfn "%d" 123456
    x.Result
