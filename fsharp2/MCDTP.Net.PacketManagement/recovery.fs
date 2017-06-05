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
            this.Timer <- new Timer(cb,null,this.RetransmitInterval,Timeout.Infinite)

      member this.AcknowlegdePacket(seqNum:int64) =
        let inRecovery =
          this.RecoveryLock
          |> Sync.read(fun () -> this.Recovery |> List.exists(fun s -> s = seqNum))
        let inRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> this.Retransmit |> List.exists(fun p -> p.seqNum = seqNum))

        if inRecovery then
          this.RecoveryLock
          |> Sync.write(fun () ->
            this.Recovery <- this.Recovery |> List.except (seq { for s in [|seqNum|] -> s })
          )
        elif inRetransmit then
          this.AckInRetransmit(seqNum)
      
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
        }

      member internal this.TryGetNextRecoverySeq() =
        this.RecoveryLock
        |> Sync.write(fun () ->
          let hOp = this.Recovery |> List.tryHead
          match hOp with
          | Some h -> this.Recovery <- this.Recovery |> List.tail
          | _ -> ()
          hOp
        )

      member internal this.AddToRetransmit(packet:UdpPacket) =
        this.RetransmitLock
        |> Sync.write(fun () ->
          this.Retransmit <- this.Retransmit@[packet]
        )

      member internal this.RetransmitCallback _ =
        this.Timer.Change(Timeout.Infinite,Timeout.Infinite) |> ignore

        if this.Mode = Server then
          let retransmit =
            this.RetransmitLock
            |> Sync.read(fun () ->
              let take = min (this.WindowSize/2) (List.length this.Retransmit)
              this.Retransmit |> List.take (this.WindowSize / 2)
            )
          if not <| List.isEmpty retransmit then
            retransmit
            |> List.iter(fun p ->
              this.NetworkLogger.LogRetransmittedPacket(LogLevel.Debug,p.seqNum)
            )
            this.BufferLock
            |> Sync.write(fun () ->
              this.Buffer <- retransmit@this.Buffer
            )
          else
            this.NetworkLogger.LogPlainMessage(LogLevel.Info,"Nothing to retransmit")
        elif this.Mode = ServerRetransmitting then
          let retransmit =
            this.RetransmitLock
            |> Sync.read(fun () ->
              let take = min (this.WindowSize/2) (List.length this.Retransmit)
              this.Retransmit |> List.take (this.WindowSize / 2)
            )
          if List.isEmpty retransmit then
            let retransmitter =
              async {
                do! PacketManagerImpl.asyncRetransmit retransmit this.OnRetransmit this.NetworkLogger
              }
            retransmitter
            |> Async.StartChild
            |> Async.RunSynchronously
            |> ignore
          else
            this.NetworkLogger.LogPlainMessage(LogLevel.Info,"Nothing to retransmit")
        else
          failwith "This is a server feature"
        let stillInRecovery =
          this.RecoveryLock
          |> Sync.read(fun () -> not <| List.isEmpty this.Recovery)
        let stillInRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> not <| List.isEmpty this.Retransmit)
        let doAddToRetransmit =
          this.RetransmitLock
          |> Sync.read(fun () -> List.length this.Retransmit < this.WindowSize)
        let continueRetransmission = stillInRecovery || stillInRetransmit
        if stillInRecovery && doAddToRetransmit then
          this.AsyncTryMoveFromRecoveryToRetransmit()
          |> Async.StartChild
          |> Async.RunSynchronously
          |> ignore

        // If we need to continue, only set dueTime.
        // No need to set periodic since we stop
        //  the time at the beginning of this function
        if continueRetransmission then
          this.Timer.Change(this.RetransmitInterval,Timeout.Infinite) |> ignore