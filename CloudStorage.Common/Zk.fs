module CloudStorage.Common.Zk

open System
open System.IO
open System.Threading
open System.Threading.Tasks
open Google.Protobuf
open org.apache.zookeeper

type LoggingWatcher() =
    inherit Watcher()

    override _.``process``(e: WatchedEvent) =
        new Task(fun () -> printfn $"%s{e.ToString()}")

let private zk =
    let zooKeeper =
        ZooKeeper("localhost:2181", 15000, LoggingWatcher())

    while zooKeeper.getState ()
          <> ZooKeeper.States.CONNECTED do
        Task.Delay(500).Wait()
        printfn "Waiting..."

    zooKeeper

let private serverMsg =
    ServerMsg(ZmqHost = "localhost", ZmqPort = "11234", HttpPort = "8000")

let private getBytes (msg: IMessage) =
    use stream = new MemoryStream()
    msg.WriteTo stream
    stream.ToArray()

let private node =
    if zk.existsAsync("/cloud").Result = null then
        zk
            .createAsync("/cloud", null, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT)
            .Wait()

    zk
        .createAsync(
            "/cloud/node_",
            getBytes serverMsg,
            ZooDefs.Ids.OPEN_ACL_UNSAFE,
            CreateMode.EPHEMERAL_SEQUENTIAL
        )
        .Result

let main () =
    new Timer(
        (fun _ ->
            let totalAvailableSpace, totalSize =
                DriveInfo.GetDrives()
                |> Seq.map (fun driveInfo -> driveInfo.AvailableFreeSpace, driveInfo.TotalSize)
                |> Seq.reduce (fun a b -> fst a + fst b, snd a + snd b)

            serverMsg.AvailableSpace <- Convert.ToUInt64 totalAvailableSpace
            serverMsg.TotalSpace <- Convert.ToUInt64 totalSize
            printfn $"Upload %u{totalAvailableSpace} %u{totalSize}"

            zk.setDataAsync(node, getBytes serverMsg).Wait()
            ()),
        null,
        0,
        1000
    )
    |> ignore
