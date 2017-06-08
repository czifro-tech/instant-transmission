namespace MCDTP.IO.MemoryMappedFile.Partition

  open System.IO
  open MCDTP.IO.MemoryStream
  open MCDTP.Logging
  open MCDTP.Utility

  module internal PartitionHandleImpl =

    let functionName name =
      "MCDTP.IO.MemoryMappedFile.Partition." + name

    let fromBuffer,fromFile = true,false
    let toBuffer,toFile = true,false

    let readFromBuffer count buffer =
      let read,bytes,nBuffer = MemoryStream.asyncRead count buffer |> Async.RunSynchronously
      if read <= 0 then None
      else Some (bytes,nBuffer)
    let writeToBuffer bytes buffer =
      let written,nBuffer = MemoryStream.asyncWrite bytes buffer |> Async.RunSynchronously
      if written <= 0 then None
      else Some buffer
    let readFromFile count (file:FileStream) =
      let bytes = Type.nullByteArray count
      let read = file.Read(bytes, 0, count)
      if read <= 0 then None
      else Some bytes
    let writeToFile bytes (file:FileStream) =
      file.Write(bytes, 0, (Array.length bytes))

    let asyncCopyFromFile (count:int64) (file:FileStream) (logger':ConsoleLogger) (syncWriteToBuffer:byte[]->bool) =
      async {
        let funcName = functionName "PartitionHandleImpl.asyncCopyFromFile"
        try
        if count > (int64 System.Int32.MaxValue) then
          let mutable len = 0L
          let mutable continue' = true
          while len < count && continue' do
            let take = int (min (count - len) (int64 System.Int32.MaxValue))
            let ret = readFromFile take file
            match ret with
            | Some b ->
              len <- len + (int64 (Array.length b))
              if syncWriteToBuffer b then continue' <- false
            | _ ->
              logger'.LogWith(LogLevel.Info,funcName,"File yielded no data, stopping copy process")
              continue' <- false
          return len
        else
          let ret = readFromFile (int count) file
          match ret with
          | Some b ->
            if syncWriteToBuffer b then return (int64 (Array.length b))
            else return 0L
          | _ -> return 0L
        with
        | ex ->
          logger'.Log(funcName+": Failed to copy from file", ex)
          return 0L
      }

    let asyncFlushToFile (count:int64) (file:FileStream) (logger':ConsoleLogger) (syncReadFromBuffer:int->byte[] option) =
      async {
        let funcName = functionName "PartitionHandleImpl.asyncFlushToFile"
        try
        if count > (int64 System.Int32.MaxValue) then
          let mutable len = 0L
          let mutable continue' = true
          while len < count && continue' do
            let take = int (min (count - len) (int64 System.Int32.MaxValue))
            let ret = syncReadFromBuffer take
            match ret with
            | Some b ->
              len <- len + (int64 (Array.length b))
              writeToFile b file
            | _ ->
              logger'.LogWith(LogLevel.Info,funcName,"Buffer yielded no data, stopping flush process")
              continue' <- false
          return len
        else
          let ret = syncReadFromBuffer (int count)
          match ret with
          | Some b ->
            writeToFile b file
            return (int64 (Array.length b))
          | _ -> return 0L
        with
        | ex ->
          logger'.Log(funcName+": Failed to flush buffer", ex)
          return 0L
      }

  type internal BufferState = Idle | Flushing | Replenishing | Amending
  type ReadOrWrite = ReadOnly | WriteOnly

  type PartitionHandle(config:PartitionConfiguration) =

    let consoleLogger =
      match config.logger with
      | ConsoleLogger l -> l
      | _ -> failwith "Partition requires MCDTP.Logging.ConsoleLogger"

    let mutable buffer =
      {
        buffer = []
        logger = consoleLogger
      }

    let readOrWrite =
      match config.readOrWrite with
      | Some roe -> 
        if roe then ReadOrWrite.ReadOnly
        else ReadOrWrite.WriteOnly
      | _ -> failwith "Read or Write specification required"

    let flushThreshold,replenishThreshold = config.flushThreshold,config.replenishThreshold
    let mutable bufferLength = 0L

    let fileStream = config.fs
    let startPosition = config.startPos
    let mutable currentPosition = config.startPos
    let endPosition = config.startPos + config.size
    let size = config.size
    let mutable bytesProcessedCounter = 0L
    let positionLock = Sync.createLock()

    let bufferLock = Sync.createLock()
    let mutable bufferState = BufferState.Idle
    let bufferStateLock = Sync.createLock()

    member this.InitializeBuffer() =
      let funcName = PartitionHandleImpl.functionName "PartionHandle.InitializeBuffer"
      try
      let asyncInit = PartitionHandleImpl.asyncCopyFromFile (replenishThreshold*2L) fileStream consoleLogger this.WriteBytes
      let ret = asyncInit |> Async.RunSynchronously
      currentPosition <- currentPosition + ret
      bytesProcessedCounter <- ret
      consoleLogger.LogWith(LogLevel.Info,funcName,ret)
      ret <> 0L
      with
      | ex ->
        consoleLogger.Log(funcName,ex)
        false

    member this.WriteBytes(bytes:byte[]) =
      let ret =
        bufferLock
        |> Sync.write(fun () ->
          let funcName = PartitionHandleImpl.functionName "PartitionHandle.WriteBytes"
          try
          match PartitionHandleImpl.writeToBuffer bytes buffer with
          | Some nBuffer ->
            buffer <- nBuffer
            bufferLength <- bufferLength + (int64 (Array.length bytes))
            consoleLogger.LogWith(LogLevel.Info,funcName,(bytes,buffer))
            true
          | _ ->
            consoleLogger.Log(funcName,"Failed to write to buffer")
            false
          with
          | ex ->
            consoleLogger.Log(funcName,ex)
            false
        )
      let doTryFlush = bufferLock |> Sync.read(fun () -> bufferLength > flushThreshold)
      if doTryFlush && readOrWrite = ReadOrWrite.WriteOnly then this.TryFlush(false)
      ret

    member this.ReadBytes(count:int) =
      let ret =
        bufferLock
        |> Sync.write(fun () ->
          let funcName = PartitionHandleImpl.functionName "PartitionHandle.ReadBytes"
          try
          match PartitionHandleImpl.readFromBuffer count buffer with
          | Some (bytes,nBuffer) ->
            buffer <- nBuffer
            bufferLength <- bufferLength - (int64 count)
            consoleLogger.LogWith(LogLevel.Info,funcName,(count,bytes,buffer))
            Some bytes
          | _ ->
            consoleLogger.Log(funcName,"Failed to read from buffer")
            None
          with
          | ex ->
            consoleLogger.Log(funcName,ex)
            None
        )
      let doTryReplenish = bufferLock |> Sync.read(fun () -> bufferLength < replenishThreshold)
      if doTryReplenish then this.TryReplenish()
      ret

    member this.TryFlush(force:bool) =
      if readOrWrite = ReadOrWrite.WriteOnly then
        bufferStateLock
        |> Sync.write(fun () ->
          if bufferState = BufferState.Idle && (force || bufferLength > flushThreshold) then
            bufferState <- BufferState.Flushing
            this.Flush()
        )

    member internal this.TryReplenish() =
      if readOrWrite = ReadOrWrite.ReadOnly then
        bufferStateLock
        |> Sync.write(fun () ->
          if bufferState = BufferState.Idle && bufferLength < replenishThreshold then
            bufferState <- BufferState.Replenishing
            this.Replenish()
        )

    member this.TryAmend(pos,bytes) =
      if readOrWrite = ReadOrWrite.WriteOnly then
        bufferStateLock
        |> Sync.write(fun () ->
          if bufferState = BufferState.Idle then
            bufferState <- BufferState.Amending
            let ret = this.Amend(pos,bytes)
            bufferState <- BufferState.Idle
            ret
          else
            let funcName = PartitionHandleImpl.functionName "PartitionHandle.TryAmend"
            let msg = sprintf "Could not amend, buffer state: %A" bufferState
            consoleLogger.LogWith(LogLevel.Info,funcName,msg)
            false
        )
      else failwith "Partition is read only"

    member internal this.Flush() =
      let funcName = PartitionHandleImpl.functionName "PartitionHandle.Flush"
      let flusher =
        async {
          try
          let! ret = PartitionHandleImpl.asyncFlushToFile flushThreshold fileStream consoleLogger this.ReadBytes
          positionLock
          |> Sync.write(fun () ->
            currentPosition <- currentPosition + ret
            bytesProcessedCounter <- bytesProcessedCounter + ret
          )
          with
          | ex ->
            consoleLogger.Log(funcName,"Failed to flush buffer to file")
          bufferStateLock
          |> Sync.write(fun () ->
            bufferState <- BufferState.Idle
          )
        }
      flusher
      |> Async.StartChild
      |> Async.RunSynchronously
      |> ignore

    member internal this.Replenish() =
      let funcName = PartitionHandleImpl.functionName "PartitionHandle.Replenish"
      let replenisher =
        async {
          try
          let! ret = PartitionHandleImpl.asyncCopyFromFile replenishThreshold fileStream consoleLogger this.WriteBytes
          positionLock
          |> Sync.write(fun () ->
            currentPosition <- currentPosition + ret
            bytesProcessedCounter <- bytesProcessedCounter + ret
          )
          with
          | ex ->
            consoleLogger.Log(funcName,"Failed to replenish buffer from file")
          bufferStateLock
          |> Sync.write(fun () ->
            bufferState <- BufferState.Idle
          )
        }
      replenisher
      |> Async.StartChild
      |> Async.RunSynchronously
      |> ignore

    member internal __.Amend(pos:int64,bytes:byte[]) =
      let funcName = PartitionHandleImpl.functionName "PartitionHandle.Amend"
      // Performing a blocking buffer amend
      // Asynchronous updating would invite
      //  potential problems
      // Use MemoryStream to amend buffer
      bufferLock
      |> Sync.write(fun () ->
        try
        if pos > currentPosition then
          let success,nBuffer =
            MemoryStream.asyncAmend pos bytes buffer
            |> Async.RunSynchronously
          if success then
            let msg = sprintf "Successfully amended buffer at %d" pos
            consoleLogger.LogWith(LogLevel.Debug,funcName,msg)
            buffer <- nBuffer
          else
            let msg = "Failed to amend buffer"
            consoleLogger.LogWith(LogLevel.Debug,funcName,msg)
          success
        else
          let msg = "Buffer already flushed"
          consoleLogger.LogWith(LogLevel.Debug,funcName,msg)
          false
        with
        | ex ->
          consoleLogger.Log(funcName + " threw exception",ex)
          false
      )

    member __.Feop() = positionLock |> Sync.read(fun () -> bytesProcessedCounter = size)