namespace MCDTP.Test

  open System
  open System.Net
  open System.IO
  open MCDTP.Logging
  open MCDTP.Net.Protocol
  open MCDTP.Net.Sockets
  open MCDTP.Net.PacketManagement
  open MCDTP.IO.MemoryMappedFile
  open MCDTP.IO.MemoryMappedFile.Partition
  open MCDTP.FTP

  module NetworkTest =

    let parallelismServer = 1
    let parallelismClient = 1

    let memoryLimitServer = 4L * 1000L * 1000L * 1000L // GB
    let memoryLimitClient = 4L * 1000L * 1000L * 1000L // GB

    let port = 50003

    let fileNames =
      [|
        "Der Doppelganger copy.mp4"
        "TeamContactInfo.txt"
        "rockyou.txt"
        "32_new.txt"
      |]

    let getSourceFileName i =
      "/Users/czifro/Dropbox/" + fileNames.[i]

    let getDestinationFileName i =
      "/Users/czifro/.mcdtp/" + fileNames.[i]

    let file = FileInfo(getSourceFileName 2)

    let private loggerConfigs id =
      let console =
        loggerConfig {
          useConsole
          loggerId id
          logLevel LogLevel.Info
        }
      let network =
        loggerConfig {
          networkOnly
          loggerId id
          logLevel LogLevel.Info
          throughputInterval (TimeSpan(0,0,5))
        }
      console,network

    let modifyLoggerConfig ipString config =
      let id = sprintf "-%s:%d" ipString port
      LoggerConfiguration.appendId id config

    let getConsoleFromConfig config =
      match Logger.ofConfig config with
      | ConsoleLogger cl -> cl
      | _ -> null

    let parser (x:_[]) = (Tcp.Parser.tryParse x |> Option.get) :> obj
    let composer (x:obj) =
      match Tcp.Composer.tryCompose (x :?> TcpPacket) with
      | Some arr -> arr
      | _ -> Array.empty

    let ``Server Performance Test``() =
      let console,network = loggerConfigs "server-perf-test"
      let listener =
        console
        |> modifyLoggerConfig (IPAddress.Loopback.ToString())
        |> getConsoleFromConfig
        |> SocketHandle.newListener port
      Ftp.useListener listener
      let tcp =
        socketHandle {
          useTcp
        }
      let udp =
        socketHandle {
          useUdp
        }
      let partitionConfig =
        partition {
          replenishThreshold (memoryLimitServer / (int64 parallelismServer) / 10L)
          isReadOnly
        }
        |> PartitionConfiguration.attachLogger console
      let mmfConfig =
        mmf {
          usePartitionConfig partitionConfig
          handleFile (getSourceFileName 2)
          isReadOnly
        }
        |> MMFConfiguration.setSize file.Length
      let ftpConfig =
        ftp {
          serverMode
          useConsole console
          monitorNetwork network
          configureUdp udp
          configureTcp tcp
          useParser parser
          useComposer composer
          configureMMF mmfConfig
          withParallelism parallelismServer
        }

      let session = Ftp.acceptNewSessionFromConfig ftpConfig
      session.BeginHandshake true
      while session.State <> FtpSessionState.Success do
        Threading.Thread.Sleep 5000
      ()

    let ``Client Performance Test``() =
      let console,network = loggerConfigs "client-perf-test"
      let tcp =
        socketHandle {
          useTcp
          connectTo IPAddress.Loopback
          usingPort port
        }
      let udp =
        socketHandle {
          useUdp
        }
      let partitionConfig =
        partition {
          flushThreshold (memoryLimitClient / (int64 parallelismClient) / 10L)
          isWriteOnly
        }
        |> PartitionConfiguration.attachLogger console
      let mmfConfig =
        mmf {
          usePartitionConfig partitionConfig
          handleFile (getDestinationFileName 2)
          isWriteOnly
        }
        |> MMFConfiguration.setSize file.Length
      let ftpConfig =
        ftp {
          clientMode
          useConsole console
          monitorNetwork network
          configureUdp udp
          configureTcp tcp
          useParser parser
          useComposer composer
          configureMMF mmfConfig
          withParallelism parallelismClient
        }

      let session = Ftp.connectWithConfig ftpConfig
      session.BeginHandshake false
      while session.State = FtpSessionState.Handshake do
        Threading.Thread.Sleep 500
      session.RequestTransfer()
      while session.State <> FtpSessionState.Success do
        Threading.Thread.Sleep 5000
      ()

    let private tests =
      [
        "``Server Performance Test``",``Server Performance Test``
        "``Client Performance Test``",``Client Performance Test``
        "quit",ignore
      ]
      |> Map.ofList

    let private testNames =
      [
        0,"``Server Performance Test``"
        1,"``Client Performance Test``"
        2,"quit"
      ]
      |> Map.ofList

    let private printTestNames() =
      testNames
      |> Map.iter(fun k v ->
        printfn "\t%d -> %s" k v
      )

    let private runTest name =
      printfn "Running %s..." name
      let testOp = tests |> Map.tryFind name
      match testOp with
      | Some test ->
        let st = DateTime.UtcNow
        test()
        let et = DateTime.UtcNow
        let duration = et - st
        printfn "Test took %s" (duration.ToString())
      | _ -> failwithf "Test \"%s\" does not exist" name

    let testRunner() =
      let mutable continueLoop = true
      while continueLoop do
        printfn "Choose a test:"
        printTestNames()
        printf "|> "
        let k = Console.ReadLine() |> int
        if testNames |> Map.containsKey k |> not then
          printfn "Invalid option"
        else
          let k = testNames.[k]
          if k <> "quit" then
            runTest k
          else
            continueLoop <- false
      ()