namespace MUDT.IO

  open System
  open System.IO
  open System.Threading
  open Microsoft.Win32.SafeHandles
  open MUDT.IO
  open MUDT.Cryptography
  open MUDT.Diagnostics
  open MUDT.Utilities
  
  type MemoryMappedFileStateConfig =
    {
      hashStateConfig : HashStateConfig;
      directory : string;
      file : FileInfo;
      partitionCount : int;
      bufferCapacity : int64;
    }

  type MemoryMappedFileState = 
    {
      partitions : MMFPartitionState[];
      fileInfo : FileInfo;
    }

  module MemoryMappedFile =

    let tryGetFileInfo (fileName:string) =
      let info = FileInfo(fileName)
      if info.Exists then
       Some info
      else
       None

    let private padFileAsync (info:FileInfo) (len:int64) =
      async {
        use fs = new FileStream(info.FullName, FileMode.Create, FileAccess.Write, FileShare.Write)
        let segSize = 
          let limit = 
            [| 
              len; // this is the preferred limit
              CurrentProcessInfo.getMemoryLimit(); // this is so we don't exceed memory limit
              int64(Int32.MaxValue); // this is so we do not reduce performance
            |] |> Array.min
          int(limit / 10L) // segments will be 1/10 the limit

        //printfn "Segment Size: %d" segSize
        
        while fs.Length < len do
          let size = if (fs.Length + int64(segSize)) > len then int(len - fs.Length) else segSize
          let bytes = [| for i in 0..size-1 -> byte('x') |]//size |> TypeUtility.nullByteArray
          do! bytes |> fs.AsyncWrite
        fs.Dispose()
        return info
      }

    let createFileAsync (fileName:string) (fileLength:int64) =
      async {
        let info = FileInfo(fileName)
        if not <| info.Exists then
          return! padFileAsync info fileLength
        else
          return info
      }

    let partitionFile (isRead:bool) (config:MemoryMappedFileStateConfig) =
      let partitionLength = config.file.Length / int64(config.partitionCount)
      // chances are division won't be even
      // determine remainder and add to last partition
      let remaining = config.file.Length - (partitionLength * int64(config.partitionCount))
      let finalPartitionLength = partitionLength + remaining
      let sharedLock' = ref (new ReaderWriterLockSlim())
      let fileBufferSize = 64 * 1024//int(config.bufferCapacity) / 2
      // let noBuffering : FileOptions = LanguagePrimitives.EnumOfValue 0x20000000
      // let fileOptions = noBuffering ||| FileOptions.RandomAccess ||| FileOptions.WriteThrough

      let openFile _config startPos' len' (open':unit->FileStream) =
        let config' = {
          hashStateConfig = _config.hashStateConfig;
          fs = open'();
          sharedLock = sharedLock';
          startPos = startPos';
          size = len';
          bufferCapacity = config.bufferCapacity
        }
        MMFPartition.createMMFPartitionState config'
      let read() =
        FileStreamHelper.getPlatformSpecificFileStream config.file.FullName FileAccess.Read fileBufferSize
        //new FileStream(config.file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, fileBufferSize, fileOptions)
      let write() =
        FileStreamHelper.getPlatformSpecificFileStream config.file.FullName FileAccess.Write fileBufferSize
        //new FileStream(config.file.FullName, FileMode.Open, FileAccess.Write, FileShare.Write, fileBufferSize, fileOptions)
      {
        partitions = [| for i in 0..config.partitionCount-1 -> 
                          let startPos = int64(i) * partitionLength
                          let len = if i = config.partitionCount-1 then finalPartitionLength else partitionLength
                          if isRead then
                            openFile config startPos len read
                          else 
                            openFile config startPos len write
                      |];
        fileInfo = config.file
      }

    let finalize (state:MemoryMappedFileState) =
      for i in 0..(Array.length state.partitions-1) do
        if not <| state.partitions.[i].isDisposed then
          state.partitions.[i] <- MMFPartition.dispose state.partitions.[i]

      let mutable loop = true
      
      while loop do
        try
          use fs = new FileStream(state.fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.None)
          printfn "Lock released..."
          loop <- false
          fs.Dispose()
        with
        | _ -> printfn "Waiting for lock to release..."