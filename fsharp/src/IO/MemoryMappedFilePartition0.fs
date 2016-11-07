namespace MUDT.IO

  open System
  open System.IO
  open MemoryMappedFileChecksum

  type MemoryMappedFilePartitionErrorCode =
    | Success = 0
    | Failed = 1
    | OpenedInReadMode = 3
    | OpenedInWriteMode = 4

  type MemoryMappedFilePartitionMode =
    | Read = 0
    | Write = 1
    | NotSet = 2

  type MemoryMappedFilePartitionConfiguration =
    {
      fileStream : FileStream;
      position : int64;
      mutable currentPosition : int64;
      length : int;
      mutable bufferSize : int;
      mode : MemoryMappedFilePartitionMode
    }

    static member NewInstance(fs, pos, len) =
      {
        fileStream = fs;
        position = pos;
        currentPosition = pos;
        length = len;
        bufferSize = 0;
        mode = MemoryMappedFilePartitionMode.NotSet
      } 

  type MemoryMappedFilePartition0(config) =

    let mutable _config = config : MemoryMappedFilePartitionConfiguration
    let mutable _bufferSize = 0

    let _memStream = new MemoryStream()

    member x.Configuration 
      with get() = _config
      and private set(value) = _config <- value

    member x.FileStream with get() = x.Configuration.fileStream

    member x.CurrentPosition 
      with get() = x.Configuration.currentPosition
      and set(value) = x.Configuration.currentPosition <- value

    member x.StartPosition with get() = x.Configuration.position

    member x.PartitionLength with get() = x.Configuration.length

    member x.BufferSize 
      with get() = x.Configuration.bufferSize
      and set(value) = x.Configuration.bufferSize <- value

    member x.BufferLength
      with get() = _memStream.Length

    member x.Mode
      with get() = x.Configuration.mode
      and set(value) =
        if value <> MemoryMappedFilePartitionMode.NotSet && 
          x.Configuration.mode = MemoryMappedFilePartitionMode.NotSet then
          x.Configuration <- 
            {
              x.Configuration with
                mode = value
            }

    member x.Init() =
      _memStream.Capacity <- x.BufferSize
      x.FileStream.Seek(x.StartPosition, SeekOrigin.Begin) |> ignore

    member x.ReplenishBufferFromFileAsync() =
      async {
        if x.Mode = MemoryMappedFilePartitionMode.Read then
          let replenishSize =
            let mutable t = int64(x.BufferSize) - _memStream.Length
            if int64(t) < x.FileStream.Length then
              t
            else
              x.FileStream.Length
          try
            do! ((x.FileStream.AsyncRead(int(replenishSize)))
            |> Async.RunSynchronously
            |> computeAndStoreHash x.StartPosition
            |> _memStream.AsyncWrite)
            x.CurrentPosition <- x.CurrentPosition + replenishSize
            return MemoryMappedFilePartitionErrorCode.Success
          with
          | _ -> return MemoryMappedFilePartitionErrorCode.Failed
        else
          return MemoryMappedFilePartitionErrorCode.OpenedInWriteMode
      }

    member x.ReadFromBufferAsync(count:int,callback:byte[]->Async<unit>) =
      async {
        if x.Mode = MemoryMappedFilePartitionMode.Read then
          let size = 
            if int64(count) < _memStream.Length then int64(count) else _memStream.Length
          try
            let! bytes = _memStream.AsyncRead(int(size))
            callback(bytes) |> ignore
            return MemoryMappedFilePartitionErrorCode.Success
          with
          | _ -> return MemoryMappedFilePartitionErrorCode.Failed
        else
          return MemoryMappedFilePartitionErrorCode.OpenedInWriteMode
      }

    member x.WriteToBufferAsync(bytes:byte[]) =
      async {
        if x.Mode = MemoryMappedFilePartitionMode.Write then
          try
            do! _memStream.AsyncWrite bytes
            return MemoryMappedFilePartitionErrorCode.Success
          with
          | _ -> return MemoryMappedFilePartitionErrorCode.Failed
        else
          return MemoryMappedFilePartitionErrorCode.OpenedInReadMode
      }

    member x.PartiallyFlushBufferToFileAsync() = 
      async {
        if x.Mode = MemoryMappedFilePartitionMode.Write then
          let flushSize = int(x.BufferLength / 2L)
          try
            do! ((_memStream.AsyncRead flushSize)
            |> Async.RunSynchronously
            |> computeAndStoreHash x.StartPosition
            |> x.FileStream.AsyncWrite)
            x.CurrentPosition <- x.CurrentPosition + int64(flushSize)
            return MemoryMappedFilePartitionErrorCode.Success
          with
          | _ -> return MemoryMappedFilePartitionErrorCode.Failed
        else
          return MemoryMappedFilePartitionErrorCode.OpenedInReadMode
      }

    member x.FlushBufferToFileAsync() =
      async {
        if x.Mode = MemoryMappedFilePartitionMode.Write then
          let flushSize = _memStream.Length
          try
            do! ((_memStream.AsyncRead(int(_memStream.Length)))
            |> Async.RunSynchronously
            |> computeAndStoreHash x.StartPosition
            |> x.FileStream.AsyncWrite)
            x.CurrentPosition <- x.CurrentPosition + flushSize
            return MemoryMappedFilePartitionErrorCode.Success
          with
          | _ -> return MemoryMappedFilePartitionErrorCode.Failed
        else
          return MemoryMappedFilePartitionErrorCode.OpenedInReadMode
      }

    member x.Reset() =
      x.FileStream.Seek(x.StartPosition - x.CurrentPosition, SeekOrigin.Current) |> ignore
      x.CurrentPosition <- x.StartPosition