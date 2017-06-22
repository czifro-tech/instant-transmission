namespace MCDTP.IO.MemoryMappedFile.Partition

  open System.IO
  open MCDTP.IO.MemoryStream
  open MCDTP.Logging

  type PartitionConfiguration =
    {
      // not user set, i.e. not set with builder
      fs                  : FileStream

      // true => read, false => write
      readOrWrite         : bool option

      // not user set, i.e. not set with builder
      startPos            : int64
      size                : int64

      flushThreshold      : int64
      replenishThreshold  : int64
      logger              : LoggerConfiguration
    }

    static member Instance =
      {
        fs                  = null
        readOrWrite         = None
        startPos            = 0L
        size                = 0L
        flushThreshold      = 0L
        replenishThreshold  = 0L
        logger              = loggerConfig.Return()
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module PartitionConfiguration =

    let readOrWrite_ = "readOrWrite"
    let flushThreshold_ = "flushThreshold"
    let replenishThreshold_ = "replenishThreshold"

    let private getReadOrWriteAsString p =
      if p.readOrWrite.Value then "read only" else "write only"

    let set k (v:obj) p =
      match k with
      | _ when k = readOrWrite_         ->
        if p.readOrWrite.IsNone then { p with readOrWrite = Some (v :?> bool) }
        else failwithf "Partition has already been set to %s" (getReadOrWriteAsString p)
      | _ when k = flushThreshold_      -> { p with flushThreshold = (v :?> int64) }
      | _ when k = replenishThreshold_  -> { p with replenishThreshold = (v :?> int64) }
      | _                               -> failwithf "Unknown key '%s'" k

    let attachLogger l p =
      { p with PartitionConfiguration.logger = l }