namespace MCDTP.IO.MemoryMappedFile

  open System.IO
  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.IO.MemoryMappedFile.Partition

  type MMF =
    {
      partitions : PartitionHandle[]
      fileInfo   : FileInfo
    }

    static member Instance =
      {
        partitions = [||]
        fileInfo   = null
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module MMF =

    let private commonFileStreamArgs =
      let options = FileOptions.RandomAccess
      let mode = FileMode.Open
      let fileBufferSize = 64*1024
      mode,fileBufferSize,options

    let private readFileStreamArgs (name:string) =
      let access = FileAccess.Read
      let share = FileShare.Read
      let mode,bufSize,options = commonFileStreamArgs
      name,mode,access,share,bufSize,options

    let private writeFileStreamArgs (name:string) =
      let access = FileAccess.Write
      let share = FileShare.Write
      let mode,bufSize,options = commonFileStreamArgs
      name,mode,access,share,bufSize,options

    let private openRead (name:string) =
      let name,mode,access,share,bufSize,options = readFileStreamArgs name
      new FileStream (name,mode,access,share,bufSize,options)

    let private openWrite name =
      let name,mode,access,share,bufSize,options = writeFileStreamArgs name
      new FileStream (name,mode,access,share,bufSize,options)

    let private ofPartitionConfig (conf:PartitionConfiguration) (fs:FileStream) pos size =
      fs.Seek(pos,SeekOrigin.Begin) |> ignore
      let id = sprintf "-partition:%d" pos
      let console = LoggerConfiguration.appendId id conf.logger
      let conf =
        { conf with
            fs = fs
            startPos = pos
            size = size
            logger = console }
      new PartitionHandle(conf)

    let private toMMFState (config:MMFConfiguration) (file:FileInfo) partitionCount
      (openFs:string->FileStream) partitionSize =
      {
        partitions = [| for i in 0..partitionCount-1 ->
                          let size =
                            if i = partitionCount-1 then
                              partitionSize + (config.fileSize - (partitionSize * (int64 partitionCount)))
                            else partitionSize
                          let fs = openFs config.fileName
                          let startPos = (int64 i) * partitionSize
                          ofPartitionConfig config.partitionConfig.Value fs startPos size
                     |]
        fileInfo   = file
      }

    let private ofConfigWriteOnly (config:MMFConfiguration) partitionCount
      (file:FileInfo) =
      if file.Exists then
        file.Delete()
      use fs = new FileStream(file.FullName, FileMode.Create, FileAccess.Write, FileShare.Write)
      let segSize =
        let limit =
          [|
            config.fileSize;
            int64 System.Int32.MaxValue
          |]
          |> Array.min
        int (limit / 10L)

      while fs.Length < config.fileSize do
        let size =
          if fs.Length + (int64 segSize) > config.fileSize then int (config.fileSize - fs.Length)
          else segSize
        let bytes = Type.nullByteArray size
        fs.Write(bytes,0,(Array.length bytes))
      
      fs.Dispose()

      let partitionSize = config.fileSize / int64 partitionCount
      toMMFState config file partitionCount openWrite partitionSize

    let private ofConfigReadOnly (config:MMFConfiguration) partitionCount
      (file:FileInfo) =
      if not <| file.Exists then failwith "File does not exist"
      else
        let partitionSize = config.fileSize / int64 partitionCount
        toMMFState config file partitionCount openRead partitionSize

    let ofConfig (config:MMFConfiguration) partitionCount =
      let fInfo = FileInfo(config.fileName)
      match config.readOrWrite with
      | Some roe ->
        if roe then
          ofConfigReadOnly config partitionCount fInfo
        else
          ofConfigWriteOnly config partitionCount fInfo
      | _ -> failwith "Need to specify read only or write only with configuration"
