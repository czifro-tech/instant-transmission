namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol
  open System.Threading

  [<AutoOpen>]
  module Recovery =

    type PacketManager with

      member this.ReportMissingPacket(seqNum:int64) =
        let isNew =
          this.RecoveryLock
          |> Sync.write(fun () ->
            if not <| (this.Recovery |> List.exists(fun s -> s = seqNum)) then
              this.Recovery <- this.Recovery@[seqNum]
              true
            else
              false
          )
        if isNew then
          this.NetworkLogger.LogPacketDropped(LogLevel.Error,seqNum)
          if this.Timer |> isNull then
            let cb = TimerCallback(this.RetransmitCallback)
            // Using only a due time, periodic is self managed
            this.Timer <- new Timer(cb,null,this.RetransmitInterval,Timeout.Infinite)

      member this.AcknowledgePacket(seqNum:int64) =
        let inRecovery =
          this.RecoveryLock
          |> Sync.read(fun () -> this.Recovery |> List.exists(fun s -> s = seqNum))
        let inRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> this.Retransmit |> List.exists(fun p -> p.seqNum = seqNum))

        if inRecovery then
          this.RecoveryLock
          |> Sync.write(fun () ->
            this.Recovery <- this.Recovery |> List.filter(fun s -> s <> seqNum)
          )
        elif inRetransmit then
          this.AckInRetransmit(seqNum)
        this.NetworkLogger.LogPacketRecovered(LogLevel.Info,seqNum)
      
      member internal this.AckInRetransmit(seqNum:int64) =
        let ack =
          async {
            this.RetransmitLock
            |> Sync.write(fun () ->
              this.Retransmit <- this.Retransmit |> List.filter(fun p -> p.seqNum <> seqNum)
            )
            do! this.AsyncTryMoveFromRecoveryToRetransmit()
          }
        ack
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore

      member internal this.AsyncTryMoveFromRecoveryToRetransmit() =
        async {
          let nso = this.TryGetNextRecoverySeq
          let atr = this.AddToRetransmit
          let of' = this.OnFetch
          let nl = this.NetworkLogger
          let continueMove() =
            this.RetransmitLock
            |> Sync.read(fun () -> List.length this.Retransmit < this.WindowSize)
          while continueMove() do
            do! PacketManagerImpl.asyncMoveFromRecoveryToRetransmit nso atr of' nl
          this.PamrtrLock
          |> Sync.write(fun () ->
            this.PerformingAsyncMoveRecoveryToRetransmit <- false
          )
        }

      member internal this.TryGetNextRecoverySeq() =
        this.RecoveryLock
        |> Sync.write(fun () ->
          this.Recovery |> List.tryHead
        )

      member internal this.AddToRetransmit(packet:UdpPacket) =
        this.RetransmitLock
        |> Sync.write(fun () ->
          if not <| (this.Retransmit |> List.exists(fun p -> p.seqNum = packet.seqNum)) then
            this.Retransmit <- this.Retransmit@[packet]
        )
        this.RecoveryLock
        |> Sync.write(fun () ->
          this.Recovery <- this.Recovery |> List.filter(fun s -> s <> packet.seqNum)
        )

      member internal this.RetransmitCallback _ =
        this.Timer.Change(Timeout.Infinite,Timeout.Infinite) |> ignore
        let stillInRecovery =
          this.RecoveryLock
          |> Sync.read(fun () -> not <| List.isEmpty this.Recovery)
        let stillInRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> not <| List.isEmpty this.Retransmit)
        if this.Mode = Server then
          let retransmit =
            this.RetransmitLock
            |> Sync.read(fun () ->
              let take = min (this.WindowSize/2) (List.length this.Retransmit)
              this.Retransmit |> List.take take
            )
          if not <| List.isEmpty retransmit then
            if retransmit |> List.head |> UdpPacket.IsEndPacket &&
                this.Mode <> ServerRetransmitting then
              this.Mode <- ServerRetransmitting
              let message = "Popped end packet, switching to retransmit mode"
              this.NetworkLogger.LogPlainMessage(LogLevel.Info,message)
            retransmit
            |> List.iter(fun p ->
              this.NetworkLogger.LogRetransmittedPacket(LogLevel.Info,p.seqNum)
            )
            this.BufferLock
            |> Sync.write(fun () ->
              this.Buffer <- retransmit@this.Buffer
            )
          else
            if not <| stillInRecovery && not <| stillInRetransmit then
              this.NetworkLogger.LogPlainMessage(LogLevel.Info,"Nothing to retransmit")
        elif this.Mode = ServerRetransmitting then
          let retransmit =
            this.RetransmitLock
            |> Sync.read(fun () ->
              let take = min (this.WindowSize/2) (List.length this.Retransmit)
              this.Retransmit |> List.take take
            )
          if not <| List.isEmpty retransmit then
            let retransmitter =
              async {
                do! PacketManagerImpl.asyncRetransmit retransmit this.OnRetransmit this.NetworkLogger
              }
            retransmitter
            |> Async.StartChild
            |> Async.RunSynchronously
            |> ignore
          else
            if not <| stillInRecovery && not <| stillInRetransmit then
              this.NetworkLogger.LogPlainMessage(LogLevel.Info,"Nothing to retransmit")
        else
          failwith "This is a server feature"
        let doAddToRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> List.length this.Retransmit < this.WindowSize)
        let continueRetransmission = stillInRecovery || stillInRetransmit
        if stillInRecovery && doAddToRetransmit then
          this.PamrtrLock
          |> Sync.write(fun () ->
            if not <| this.PerformingAsyncMoveRecoveryToRetransmit then
              this.PerformingAsyncMoveRecoveryToRetransmit <- true
              this.AsyncTryMoveFromRecoveryToRetransmit()
              |> Async.StartChild
              |> Async.RunSynchronously
              |> ignore
          )

        // If we are in Server mode, we need to continue
        // Otherwise, we continue until we have nothing left
        // Using only a due time, periodic is self managed
        if continueRetransmission || not <| this.HasSwitchedToRetransmitMode() then
          this.Timer.Change(this.RetransmitInterval,Timeout.Infinite) |> ignore
        else
          this.OnSuccess()
          this.Timer <- null

      member this.HasSwitchedToRetransmitMode() =
        this.Mode = ServerRetransmitting