namespace MCDTP.Logging

  open System
  open System.IO
  open System.Threading

  [<AutoOpen>]
  module Logging =

    type LogLevel =
      | Info    = 2
      | Debug  = 1
      | Error  = 0
      | None   = -1

    type Message =
      | ConsoleMessage of LogLevel * string * obj * DateTime
      | ThroughputMessage of LogLevel * int64 * DateTime * DateTime
      | PacketsDroppedMessage of LogLevel * (int*int) * int * DateTime

    type ConsoleLogger() =

      let mutable isRunning = false
      let runningLocker = new ReaderWriterLockSlim()
      let getIsRunning() =
        runningLocker.EnterReadLock()
        try
          isRunning
        finally
          runningLocker.ExitReadLock()

      let toggleRunning() =
        runningLocker.EnterWriteLock()
        try
          isRunning <- not isRunning
        finally
          runningLocker.ExitWriteLock()

      let locker = new ReaderWriterLockSlim()
      let mutable messageQueue : Message list = []
      let mutable logLevel = LogLevel.None
      let mutable loggerId = "Console Logger"
      let mutable isDisposed = false
      member __.LogLevel
        with get() = logLevel
        and set(value) = logLevel <- value

      member __.LoggerId
        with get() = loggerId
        and set(value) = loggerId <- value

      member this.LogWith(level:LogLevel,msg,x:obj) =
        if isDisposed then failwith "Logger has been disposed!"
        let message = ConsoleMessage(level,msg,x,DateTime.UtcNow)
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          locker.EnterWriteLock()
          try
            messageQueue <- messageQueue@[message]
          finally
            locker.ExitWriteLock()
          if not <| getIsRunning() then
            this.RunLogger()

      member this.Log(msg,x:obj) =
        this.LogWith(logLevel,msg,x)

      member __.RunLogger() =
        let runner =
          async {
            toggleRunning()
            let rec log() =
              locker.EnterReadLock()
              let messageOption =
                try
                  match messageQueue with
                  | x::xs ->
                    messageQueue <- xs
                    Some x
                  | _ -> None
                finally
                  locker.ExitWriteLock()
              match messageOption with
              | Some message ->
                match message with
                | ConsoleMessage (l,msg,o,now) ->
                  let m = sprintf "[%s][%s]> Message: %s, Data: %A" (now.ToString()) loggerId msg o
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
              while getIsRunning() do
                do! Async.Sleep 250
              runningLocker.Dispose()
              locker.Dispose()
            }
          let child = Async.StartChild(disposer)
          child
          |> Async.RunSynchronously
          |> ignore
          isDisposed <- true

    type NetworkLogger(fileName) =

      let logFileName = Path.Combine(Directory.GetCurrentDirectory(), fileName)
      let mutable logFile = null;
      do
        let fs = File.Create(logFileName)
        logFile <- new StreamWriter(fs)
      let logLevel = LogLevel.None

      let mutable isRunning = false
      let runningLocker = new ReaderWriterLockSlim()
      let getIsRunning() =
        runningLocker.EnterReadLock()
        try
          isRunning
        finally
          runningLocker.ExitReadLock()

      let toggleRunning() =
        runningLocker.EnterWriteLock()
        try
          isRunning <- not isRunning
        finally
          runningLocker.ExitWriteLock()

      let locker = new ReaderWriterLockSlim()
      let mutable messageQueue : Message list = []
      let mutable logLevel = LogLevel.None
      let mutable throughputInterval = TimeSpan.MinValue
      let mutable lastThroughputLogEvent = DateTime.MinValue
      let mutable throughput = 0L
      let mutable packetDropCount = 0L
      let mutable isDisposed = false

      member __.ThroughputInterval
        with get() = throughputInterval
        and set(value) = throughputInterval <- value
      member __.LogLevel
        with get() = logLevel
        and set(value) = logLevel <- value

      member this.LogNumberOfBytesWith(level:LogLevel,byteCount:int) =
        if isDisposed then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          let now = DateTime.UtcNow
          if lastThroughputLogEvent = DateTime.MinValue then // for our initial call
            lastThroughputLogEvent <- DateTime.UtcNow
            throughput <- throughput + (int64 byteCount)
          elif now - lastThroughputLogEvent > throughputInterval then
            throughput <- throughput + (int64 byteCount)
            let message = ThroughputMessage(level,throughput,lastThroughputLogEvent,now)
            locker.EnterWriteLock()
            try
              messageQueue <- messageQueue@[message]
            finally
              locker.ExitWriteLock()
            throughput <- 0L
            lastThroughputLogEvent <- now
          else
            throughput <- throughput + (int64 byteCount)
          if not <| getIsRunning() then
            this.RunLogger()

      member __.SuspendThroughputLogging(level:LogLevel) =
        if isDisposed then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          if throughput > 0L then
            let message = ThroughputMessage(logLevel,throughput,lastThroughputLogEvent,DateTime.UtcNow)
            locker.EnterWriteLock()
            try
              messageQueue <- messageQueue@[message]
            finally
              locker.ExitWriteLock()
            throughput <- 0L
          lastThroughputLogEvent <- DateTime.MinValue

      member this.LogPacketsDropped(level:LogLevel,range:(int*int),packetSize:int) =
        if isDisposed then failwith "Logger is disposed!"
        if level <= logLevel && logLevel <> LogLevel.None && level <> LogLevel.None then
          let message = PacketsDroppedMessage(level,range,packetSize,DateTime.UtcNow)
          locker.EnterWriteLock()
          try
            messageQueue <- messageQueue@[message]
          finally
            locker.ExitWriteLock()
          if not <| getIsRunning() then
            this.RunLogger()

      member __.RunLogger() =
        let runner =
          async {
            toggleRunning()
            let rec log() =
              let messageOption =
                locker.EnterWriteLock()
                try
                  match messageQueue with
                  | x::xs -> 
                    messageQueue <- xs
                    Some x
                  | _ -> None
                finally
                  locker.ExitWriteLock()
              match messageOption with
              | Some message ->
                match message with
                | PacketsDroppedMessage (l,r,ps,now) ->
                  if l <= logLevel then
                    let startSeqNum,endSeqNum = r
                    let packetsDroppedCount = (endSeqNum - startSeqNum) / ps
                    packetDropCount <- packetDropCount + (int64 packetsDroppedCount)
                    let packetsDropped = [| for i in 0..packetsDroppedCount-1 -> (i*ps)+startSeqNum |]
                    let m = sprintf "[Packets Dropped][Time: %s][Sequence Numbers: %A]" (now.ToString()) packetsDropped
                    logFile.WriteLine(m)
                | ThroughputMessage (l,t,s,e) ->
                  if l <= logLevel then
                    let interval = (e - s).ToString()
                    let m = sprintf "[Throughput:%d][Start:%s][End:%s][Interval:%s]" t (s.ToString()) (e.ToString()) interval
                    logFile.WriteLine(m)
                | _ -> ()
              | None -> ()
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
              while getIsRunning() do
                do! Async.Sleep 250
              runningLocker.Dispose()
              locker.Dispose()
            }
          let child = Async.StartChild(disposer)
          child
          |> Async.RunSynchronously
          |> ignore
          isDisposed <- true

    type Logger =
      | ConsoleLogger of ConsoleLogger
      | NetworkLogger of NetworkLogger

    type LoggerConfiguration =
      | LoggerConfiguration of Map<string,obj>

      static member configuration_ =
        (fun (LoggerConfiguration x) -> x), (LoggerConfiguration)

      static member empty =
        LoggerConfiguration (Map.empty)

      static member isConsole_ = "isConsole"

      static member loggerId_ = "loggerId"

      static member logLevel_ = "logLevel"

      static member throughputInterval_ = "throughputInterval"