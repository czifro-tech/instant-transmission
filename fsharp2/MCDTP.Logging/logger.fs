namespace MCDTP.Logging

  open System
  open System.IO
  open System.Threading
  open MCDTP.Utility

  [<AutoOpen>]
  module Logging =

    type LogLevel =
      | Info    = 2
      | Debug  = 1
      | Error  = 0
      | None   = -1

    let getLogLevelName (l:LogLevel) =
      match l with
      | LogLevel.Info -> "Info"
      | LogLevel.Debug -> "Debug"
      | LogLevel.Error -> "Error"
      | LogLevel.None -> "None"
      | _ -> failwithf "Invalid log level: %A" l

    type internal Message =
      | ConsoleMessage of LogLevel * string * obj * DateTime
      | ThroughputMessage of LogLevel * int64 * DateTime * DateTime
      | PacketsDroppedMessage of LogLevel * (int*int) * int * DateTime

    [<AllowNullLiteral>]
    type ConsoleLogger() =

      let mutable isRunning = false
      let runningLocker = Sync.createLock()

      let locker = Sync.createLock()
      let mutable messageQueue : Message list = []
      let mutable logLevel = LogLevel.None
      let mutable loggerId = "Console Logger"
      let mutable isDisposed = false
      let isDisposedLocker = Sync.createLock()
      member __.LogLevel
        with get() = logLevel
        and set(value) = logLevel <- value

      member __.LoggerId
        with get() = loggerId
        and set(value) = loggerId <- value

      member this.LogWith(level:LogLevel,msg,x:obj) =
        let disposed = isDisposedLocker |> Sync.read(fun () -> isDisposed)
        if disposed then failwith "Logger has been disposed!"
        let message = ConsoleMessage(level,msg,x,DateTime.UtcNow)
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          locker
          |> Sync.write(fun () ->
            messageQueue <- messageQueue@[message]
          )
          this.TryRunLogger()

      member this.Log(msg,x:obj) =
        this.LogWith(logLevel,msg,x)

      member private this.TryRunLogger() =
        runningLocker
        |> Sync.write(fun () ->
          if not isRunning then
            isRunning <- true
            this.RunLogger()
        )

      member private __.RunLogger() =
        let runner =
          async {
            let rec log() =
              let messageOption =
                locker
                |> Sync.read(fun () ->
                  match messageQueue with
                  | x::xs ->
                    messageQueue <- xs
                    Some x
                  | _ -> None
                )
              match messageOption with
              | Some message ->
                match message with
                | ConsoleMessage (l,msg,o,now) ->
                  let l = getLogLevelName l
                  let m = sprintf "[%s][%s][%s]> Message: %s, Data: %A" l (now.ToString()) loggerId msg o
                  printfn "%s" m
                | _ -> ()
              | _ -> ()
              log() // tail call
            log()
          }
        let child = Async.StartChild(runner)
        child
        |> Async.RunSynchronously
        |> ignore

      interface IDisposable with
        member __.Dispose() =
          let disposer =
            async {
              let notReady() = runningLocker |> Sync.read(fun () -> isRunning )
              while notReady() do
                do! Async.Sleep 250
              runningLocker.Dispose()
              locker.Dispose()
            }
          let child = Async.StartChild(disposer)
          child
          |> Async.RunSynchronously
          |> ignore
          isDisposed <- true

    [<AllowNullLiteral>]
    type NetworkLogger(fileName) =

      let logFileName = Path.Combine(Directory.GetCurrentDirectory(), fileName)
      let mutable logFile = null;
      do
        let fs = File.Create(logFileName)
        logFile <- new StreamWriter(fs)
      let logLevel = LogLevel.None

      let mutable isRunning = false
      let runningLocker = new ReaderWriterLockSlim()

      let locker = Sync.createLock()
      let mutable messageQueue : Message list = []
      let mutable logLevel = LogLevel.None
      let mutable throughputInterval = TimeSpan.MinValue
      let mutable lastThroughputLogEvent = DateTime.MinValue
      let mutable throughput = 0L
      let mutable packetDropCount = 0L
      let mutable isDisposed = false
      let isDisposedLocker = Sync.createLock()
      let disposed() =
        try isDisposedLocker |> Sync.read(fun () -> isDisposed)
        with _ -> true

      member __.ThroughputInterval
        with get() = throughputInterval
        and set(value) = throughputInterval <- value
      member __.LogLevel
        with get() = logLevel
        and set(value) = logLevel <- value

      member this.LogNumberOfBytesWith(level:LogLevel,byteCount:int) =
        if disposed() then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          let now = DateTime.UtcNow
          if lastThroughputLogEvent = DateTime.MinValue then // for our initial call
            lastThroughputLogEvent <- DateTime.UtcNow
            throughput <- throughput + (int64 byteCount)
          elif now - lastThroughputLogEvent > throughputInterval then
            throughput <- throughput + (int64 byteCount)
            let message = ThroughputMessage(level,throughput,lastThroughputLogEvent,now)
            locker
            |> Sync.write(fun () ->
              messageQueue <- messageQueue@[message]
            )
            throughput <- 0L
            lastThroughputLogEvent <- now
          else
            throughput <- throughput + (int64 byteCount)
          this.TryRunLogger()

      member __.SuspendThroughputLogging(level:LogLevel) =
        if disposed() then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          if throughput > 0L then
            let message = ThroughputMessage(logLevel,throughput,lastThroughputLogEvent,DateTime.UtcNow)
            locker
            |> Sync.write(fun () ->
              messageQueue <- messageQueue@[message]
            )
            throughput <- 0L
          lastThroughputLogEvent <- DateTime.MinValue

      member this.LogPacketsDropped(level:LogLevel,range:(int*int),packetSize:int) =
        if disposed() then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          let message = PacketsDroppedMessage(level,range,packetSize,DateTime.UtcNow)
          locker
          |> Sync.write(fun () ->
            messageQueue <- messageQueue@[message]
          )
          this.TryRunLogger()

      member private this.TryRunLogger() =
        runningLocker
        |> Sync.write(fun () ->
          if not isRunning then
            isRunning <- true
            this.RunLogger()
        )

      member private __.RunLogger() =
        let runner =
          async {
            let rec log() =
              let messageOption =
                locker
                |> Sync.read(fun () ->
                  match messageQueue with
                  | x::xs ->
                    messageQueue <- xs
                    Some x
                  | _ -> None
                )
              match messageOption with
              | Some message ->
                match message with
                | PacketsDroppedMessage (l,r,ps,now) ->
                  let l = getLogLevelName l
                  let startSeqNum,endSeqNum = r
                  let packetsDroppedCount = (endSeqNum - startSeqNum) / ps
                  packetDropCount <- packetDropCount + (int64 packetsDroppedCount)
                  let packetsDropped = [| for i in 0..packetsDroppedCount-1 -> (i*ps)+startSeqNum |]
                  let m = sprintf "[%s][Packets Dropped][Time: %s][Sequence Numbers: %A]" l (now.ToString()) packetsDropped
                  logFile.WriteLine(m)
                | ThroughputMessage (l,t,s,e) ->
                  let l = getLogLevelName l
                  let interval = (e - s).ToString()
                  let m = sprintf "[%s][Throughput:%d][Start:%s][End:%s][Interval:%s]" l t (s.ToString()) (e.ToString()) interval
                  logFile.WriteLine(m)
                | _ -> ()
              | None -> ()
              log() // tail call
            log()
            Sync.write(fun () -> isRunning <- false) runningLocker
          }
        let child = Async.StartChild(runner)
        child
        |> Async.RunSynchronously
        |> ignore

      interface IDisposable with
        member __.Dispose() =
          let disposer =
            async {
              let notReady() = runningLocker |> Sync.read(fun () -> isRunning )
              while notReady() do
                do! Async.Sleep 250
              runningLocker.Dispose()
              locker.Dispose()
              isDisposedLocker.Dispose()
            }
          let child = Async.StartChild(disposer)
          child
          |> Async.RunSynchronously
          |> ignore
          isDisposedLocker
          |> Sync.write(fun () -> isDisposed <- true)

    type Logger =
      | ConsoleLogger of ConsoleLogger
      | NetworkLogger of NetworkLogger
      | NoLogger