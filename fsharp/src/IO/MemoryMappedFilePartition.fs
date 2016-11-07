namespace MUDT.IO

  open System
  open System.IO

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
      currentPosition : int64;
      length : int;
      bufferSize : int;
      buffer : MemoryStream;
      mode : MemoryMappedFilePartitionMode
    }

    static member NewInstance(fs, pos, len) =
      {
        fileStream = fs;
        position = pos;
        currentPosition = pos;
        length = len;
        bufferSize = 0;
        buffer = null;
        mode = MemoryMappedFilePartitionMode.NotSet
      } 

  module MemoryMappedFilePartition =

    let mutable private config = 
      MemoryMappedFilePartitionConfiguration.NewInstance(null, 0L, 0)

    let startPosition = config.position
    let currentPosition = config.currentPosition

    let buffer = new MemoryStream()
    let file = config.fileStream
    let mutable private tt = 0

    let initPartition =
      buffer.Capacity <- config.bufferSize
      file.Seek(startPosition, SeekOrigin.Begin)

    //let resetPartition
    let t = 0