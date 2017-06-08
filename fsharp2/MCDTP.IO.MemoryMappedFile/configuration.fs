namespace MCDTP.IO.MemoryMappedFile

  open MCDTP.IO.MemoryMappedFile.Partition

  type MMFConfiguration =
    {
      partitionConfig  : PartitionConfiguration option
      fileName         : string

      // true => read, false => write
      readOrWrite      : bool option

      // not user set
      fileSize         : int64
    }

    static member Instance =
      {
        partitionConfig  = None
        fileName         = System.String.Empty
        readOrWrite      = None
        fileSize         = -1L
      }

  module MMFConfiguration =

    let partitionConfig_ = "partitionConfig"
    let fileName_ = "fileName"
    let readOrWrite_ = "readOrWrite"

    let private getReadOrWriteAsString s =
      if s.readOrWrite.Value then "read only" else "write only"

    let set k (o:obj) m =
      match k with
      | _ when k = partitionConfig_  -> { m with partitionConfig = Some (o :?> PartitionConfiguration) }
      | _ when k = fileName_         -> { m with fileName = (o :?> string) }
      | _ when k = readOrWrite_      ->
        if m.readOrWrite.IsNone then { m with readOrWrite = Some (o :?> bool) }
        else failwithf "MemoryMappedFile has already been set to %s" (getReadOrWriteAsString s)
      | _ -> failwithf "Unknown key '%s'" k