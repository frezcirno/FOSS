module CloudStorage.Server.Zk

open System
open System.IO
open System.Net
open System.Text
open System.Threading.Tasks
open CloudStorage.Storage
open FSharp.Control.Tasks
open Google.Protobuf
open org.apache.zookeeper

type LoggingWatcher() =
    inherit Watcher()

    override _.``process``(e: WatchedEvent) =
        new Task(fun () -> printfn $"%s{e.ToString()}")

let private zooKeeper =
    let zk = ZooKeeper("localhost:2181", 15000, LoggingWatcher())

    while zk.getState () <> ZooKeeper.States.CONNECTED do
        if zk.getState () = ZooKeeper.States.CLOSED then
            exit -1

        Task.Delay(1000).Wait()
        printfn "Waiting for zookeeper connection..."

    zk


let private node =
    if zooKeeper.existsAsync(Config.ZK_NODE_PREFIX).Result = null then
        zooKeeper
            .createAsync(Config.ZK_NODE_PREFIX, null, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.PERSISTENT)
            .Wait()

    zooKeeper
        .createAsync(
            Config.ZK_NODE_PREFIX + "/node_",
            null,
            ZooDefs.Ids.OPEN_ACL_UNSAFE,
            CreateMode.EPHEMERAL_SEQUENTIAL
        )
        .Result

///
/// leader节点：
/// follower节点：
/// 所有节点：运行文件上传、下载service
/// 逻辑：
/// 首先接收文件元数据，分布式存入单一Redis
/// 然后
///
/// 文件存储逻辑：
/// 首先调用上报接口，存入文件元数据信息，得到uuid
/// 然后根据uuid传输文件内容
///
type Status = 
| LOOKING // 选举中
| LEADER // 选举完毕，当前节点为leader
| FOLLOWER // 选举完毕，当前节点为follower

//let lookingForLeader () =
//    task {
//        let mutable status = Status.LOOKING
//        try
//            let myHostName = zooKeeper.getSessionId() |> Convert.ToString
//            let tryLeaderInfo = Encoding.ASCII.GetBytes(myHostName)
//            // 需要注意这里创建的是临时节点
//            let! res = zooKeeper.createAsync(nodeForLeaderInfo, tryLeaderInfo, ZooDefs.Ids.OPEN_ACL_UNSAFE, CreateMode.EPHEMERAL)
//            // 如果上一步没有抛异常，说明自己已经是leader了
//            status <- Status.LEADER
//            let logMsg = myHostName + " is leader"
//            Println(logMsg)
//        with e: KeeperException.NodeExistsException
//            // 节点已经存在，说明leader已经被别人注册成功了，自己是follower
//            status <- Status.FOLLOWER
//            try 
//                let leaderInfoBytes = zooKeeper.getData(nodeForLeaderInfo, event -> 
//                    if (event.getType() == Watcher.Event.EventType.NodeDeleted) 
//                        lookingForLeader()
//                    
//                , null)
//                String logMsg = Thread.currentThread().getName() + " is follower, master is " + new String(leaderInfoBytes, "UTF-8")
//                System.out.println(logMsg)
//             catch (KeeperException.NoNodeException e1) 
//                lookingForLeader()
//             catch (KeeperException | InterruptedException | UnsupportedEncodingException e1) 
//                e1.printStackTrace()
//            
//        with e:KeeperException | InterruptedException 
//            e.printStackTrace()
//    }
// 