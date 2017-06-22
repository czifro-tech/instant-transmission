namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol
  open System.Threading

  module internal PacketManagerImpl =

    let private asyncUnpackPackets (packets:UdpPacket list) =
      async {
        let mutable receivedEnd = false
        let data =
          packets
          |> List.map(fun p ->
            let modulo n m = ((n%m)+m)%m
            let data =
              if UdpPacket.IsEndPacket p then
                receivedEnd <- true
                Array.empty
              else p.data |> Array.take p.dLen
            let seqNum =
              if UdpPacket.IsEndPacket p then
                // keep seqNum % UdpPacket.PayloadSize == 0
                let mod' = modulo p.seqNum (int64 UdpPacket.PayloadSize)
                mod' + (p.seqNum - mod')
              else p.seqNum
            seqNum,data
          )
        return data,receivedEnd
      }

    let private asyncFillInMissingData (rawData:(int64*byte[]) list) (netLogger:NetworkLogger) =
      async {
        let mutable nextSeqNum,_ = List.head rawData
        let mutable missingPacketRanges : (int64*int64) list = []
        let data =
          rawData
          |> List.collect(fun seg ->
            let seqNum,data = seg
            if seqNum = nextSeqNum then
              nextSeqNum <- nextSeqNum + int64 UdpPacket.PayloadSize
              data |> Array.toList
            else
              missingPacketRanges <- missingPacketRanges@[(nextSeqNum,seqNum)]
              netLogger.LogPacketsDropped(LogLevel.Error,(nextSeqNum,seqNum),UdpPacket.PayloadSize)
              let fillerData = Type.nullByteArray (int (seqNum-nextSeqNum))
              nextSeqNum <- seqNum + int64 UdpPacket.PayloadSize
              Array.append fillerData data |> Array.toList
          ) |> List.toArray
        return missingPacketRanges,data
      }

    let private asyncCompileMissingPacketsList (ranges:(int64*int64) list) =
      async {
        return
          ranges
          |> List.collect(fun range ->
            let s,e = range
            let count = (e - s) / (int64 UdpPacket.PayloadSize)
            [ for i in 0L..count-1L -> (i*(int64 UdpPacket.PayloadSize)) + s ]
          )
          |> List.toArray
      }

    let asyncFlushPacketsToBuffer (packets:UdpPacket list) force (onMissingPacketsDetected:int64[]->bool->unit)
      (onFlush:byte[]->bool->unit) (netLogger:NetworkLogger) =
      async {
        let! unpackedPackets,receivedEnd = asyncUnpackPackets packets
        let! missingPacketRanges,data = asyncFillInMissingData unpackedPackets netLogger
        let! missingPackets = asyncCompileMissingPacketsList missingPacketRanges
        onMissingPacketsDetected missingPackets receivedEnd
        onFlush data force
      }

    let asyncReportDroppedPackets (seqNums:int64[]) (onDropped:int64->unit) =
      async {
        seqNums |> Array.iter onDropped
      }

    let asyncReplenishBuffer size (fromSource:int->(int64*byte[]))
      (onReplenish:UdpPacket list->unit) =
      // fromSource: is the provided onReplenish from config
      // onReplenish: is an internal function for adding packets
      //                to buffer
      async {
        let newPacket data seqNum' =
          {
            UdpPacket.DefaultInstance with
              seqNum = seqNum'
              dLen = Array.length data
              data = data
          }
        let mutable seqNum,bytes = fromSource (size*UdpPacket.DefaultSize)
        let mutable packets : UdpPacket list = []
        while not <| Array.isEmpty bytes do
          let take = min UdpPacket.PayloadSize (Array.length bytes)
          let chunk = bytes.[..take-1]
          bytes <- bytes.[take..]
          packets <- packets@[newPacket chunk seqNum]
          seqNum <- seqNum + (int64 <| Array.length chunk)
        packets <- packets |> List.sortBy(fun p -> p.seqNum)
        onReplenish packets
      }

    let asyncRetransmit (packets:UdpPacket list) (onRetransmit:UdpPacket->unit) (netLogger:NetworkLogger) =
      async {
        packets
        |> List.iter(fun p ->
          netLogger.LogRetransmittedPacket(LogLevel.Debug,p.seqNum)
          onRetransmit p
        )
      }

    let asyncMoveFromRecoveryToRetransmit (nextSeqOp:unit->int64 option)
      (addToRetransmit:UdpPacket->unit) (onFetch:int64->byte[]) (netLogger:NetworkLogger) =
      async {
        match nextSeqOp() with
        | Some s ->
          let data = onFetch s
          if not <| Array.isEmpty data then
            let packet =
              { UdpPacket.RetransmitInstance with
                  seqNum = s
                  dLen = Array.length data
                  data = data }
            addToRetransmit packet
            let msg = sprintf "Added %d to retransmit queue" s
            netLogger.LogPlainMessage(LogLevel.Info,msg)
          else
            netLogger.LogPlainMessage(LogLevel.Error, "Failed to fetch packet data")
        | _ -> netLogger.LogPlainMessage(LogLevel.Info, "Nothing in recovery")
      }

  type internal BufferState = Idle | Flushing | Replenishing
  type internal PMMode = Client | Server | ServerRetransmitting

  type PacketManager(config:PacketManagerConfiguration) =

    let mutable buffer : UdpPacket list = []
    let bufferLock = Sync.createLock()

    let mutable bufferState = BufferState.Idle
    let mutable sourceEmpty = false
    let bufferStateLock = Sync.createLock()

    //// client specific
    let mutable beginSeqNum = config.initSeqNum
    let mutable maxSeqNum = config.initSeqNum

    let mutable recoveredLookup : Set<int64> = Set.empty
    let mutable recoveredBuffer : UdpPacket list = []
    let recoveredBufferLock = Sync.createLock()

    let mutable recoveredBufferState = BufferState.Idle
    let recoveredBufferStateLock = Sync.createLock()
    ////

    //// server specific
    let mutable recovery : int64 list = []
    let recoveryLock = Sync.createLock()
    // only send first half
    // when an ack is received, packets will
    //  be removed and later packets will
    //  propagate forward
    // pull from recovery, try to keep full
    // preferrably have at least 5 packets
    //  ready for retransmission, and 5 more
    // to replace an acked packets
    let mutable retransmit : UdpPacket list = []
    // Retransmit windowSize / 2 at a time
    // When in retransmit mode
    let mutable windowSize = 10
    let retransmitLock = Sync.createLock()
    let retransmitInterval = config.retransmitInterval
    let mutable timer : Timer = null
    let mutable endPacket : UdpPacket option = None
    let mutable performingAsyncMoveRecoveryToRetransmit = false
    let pamrtrLock = Sync.createLock()
    ////

    let mutable mode = if config.isServer then PMMode.Server else PMMode.Client

    let actionThreshold,onFlush,onReplenish =
      match config.bufferAction with
      | Flush (i,a)     -> i,a,(fun _ -> failwith "Unsupported")
      | Replenish (i,a) -> i,(fun _ _ -> failwith "Unsupported"),a
      | _ -> failwithf "Unsupported buffer action: %A" config.bufferAction

    let onWrite,onFetch =
      match config.recoveryAction with
      | Write a -> a,(fun _ -> [||])
      | Fetch a -> (fun _ _ -> ()),a
      | _ -> failwithf "Unsupported recovery action: %A" config.recoveryAction

    let onDropped,onRecovery =
      match config.packetAction with
      | PacketAction (od,or') -> od,or'
      | NoAction              -> if mode = Client then
                                   failwith "Client requires a packet action"
                                 else ignore,ignore
      | _ -> failwithf "Unsupported packet action: %A" config.packetAction

    let onRetransmitMode =
      match config.retransmitModeAction with
      | RetransmitModeAction orm -> orm
      | NoAction                 -> if mode = Server || mode = ServerRetransmitting then
                                      failwith "Server requires a retransmit mode action"
                                    else ignore
      | _ -> failwithf "Unsupported retransmit mode action: %A" config.retransmitModeAction

    let onFinished =
      match config.finishedAction with
      | FinishedAction of' -> of'
      | NoAction           -> if mode = Server || mode = ServerRetransmitting then
                                failwith "Server requires a finished action"
                              else ignore
      | _ -> failwithf "Unsupported finished action: %A" config.finishedAction

    let onSuccess =
      match config.successAction with
      | SuccessAction os -> os
      | NoAction         -> if mode = Server || mode = ServerRetransmitting then
                              failwith "Server requires a success action"
                            else ignore
      | _ -> failwithf "Unsupported success action: %A" config.successAction

    let networkLogger =
      match config.logger with
      | NetworkLogger l -> l
      | _ -> failwith "Network logger is required"

    member internal __.Buffer
      with get() = buffer
      and set(value) = buffer <- value
    member internal __.BufferLock = bufferLock

    member internal __.BufferState
      with get() = bufferState
      and set(value) = bufferState <- value
    member internal __.SourceEmpty
      with get() = sourceEmpty
      and set(value) = sourceEmpty <- value
    member internal __.BufferStateLock = bufferStateLock

    member internal __.Mode
      with get() = mode
      and set(value) = mode <- value

    member internal __.ActionThreshold = actionThreshold

    member internal __.NetworkLogger = networkLogger

    ///////// client side
    member internal __.BeginSeqNum
      with get() = beginSeqNum
      and set(value) = beginSeqNum <- value
    member internal __.MaxSeqNum
      with get() = maxSeqNum
      and set(value) = maxSeqNum <- value
    member internal __.RecoveredLookup
      with get() = recoveredLookup
      and set(value) = recoveredLookup <- value
    member internal __.RecoveredBuffer
      with get() = recoveredBuffer
      and set(value) = recoveredBuffer <- value
    member internal __.RecoveredBufferLock = recoveredBufferLock
    member internal __.RecoveredBufferState
      with get() = recoveredBufferState
      and set(value) = recoveredBufferState <- value
    member internal __.RecoveredBufferStateLock = recoveredBufferStateLock
    member internal __.OnFlush = onFlush
    member internal __.OnWrite = onWrite
    member internal __.OnDropped = onDropped
    member internal __.OnRecovery = onRecovery
    /////////

    ///////// server side
    member internal __.OnReplenish = onReplenish
    member internal __.OnFinished = onFinished
    member __.EndPacket
      with get() =
        let t = endPacket
        endPacket <- None
        t
      and set(value) = endPacket <- value
        
    /////////

    ///////// server side retransmission/recovery
    member internal __.Recovery
      with get() = recovery
      and set(value) = recovery <- value
    member internal __.RecoveryLock = recoveryLock
    member internal __.WindowSize
      with get() = windowSize
      and set(value) = windowSize <- value
    member internal __.Retransmit
      with get() = retransmit
      and set(value) = retransmit <- value
    member internal __.RetransmitInterval = retransmitInterval
    member internal __.RetransmitLock = retransmitLock
    member internal __.OnFetch = onFetch
    member internal __.Timer
      with get() = timer
      and set(value) = timer <- value
    member internal __.OnRetransmit = onRetransmitMode
    member internal __.OnSuccess = onSuccess
    member internal __.PerformingAsyncMoveRecoveryToRetransmit
      with get() = performingAsyncMoveRecoveryToRetransmit
      and set(value) = performingAsyncMoveRecoveryToRetransmit <- value
    member internal __.PamrtrLock = pamrtrLock
    /////////
