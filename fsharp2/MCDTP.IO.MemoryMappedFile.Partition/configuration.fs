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
      logger              : Logger
    }

    static member Instance =
      {
        fs                  = null
        readOrWrite         = None
        startPos            = 0L
        size                = 0L
        flushThreshold      = 0L
        replenishThreshold  = 0L
        logger              = Logger.NoLogger
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module PartitionConfiguration =

    let readOrWrite_ = "readOrWrite"
    let flushThreshold_ = "flushThreshold"
    let replenishThreshold_ = "replenishThreshold"
    let logger_ = "logger"

    let private getReadOrWriteAsString s =
      if s.readOrWrite.Value then "read only" else "write only"

    let set k (v:obj) s =
      match k with
      | _ when k = readOrWrite_         ->
        if s.readOrWrite.IsNone then { s with readOrWrite = Some (v :?> bool) }
        else failwithf "Partition has already been set to %s" (getReadOrWriteAsString s)
      | _ when k = flushThreshold_      -> { s with flushThreshold = (v :?> int64) }
      | _ when k = replenishThreshold_  -> { s with replenishThreshold = (v :?> int64) }
      | _ when k = logger_              -> { s with logger = (v :?> Logger)}
      | _                               -> failwithf "Unknown key '%s'" k
