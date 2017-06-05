namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol

  [<AutoOpen>]
  module Server =

    type PacketManager with

      member this.TryPullPacket() =
        let pOp =
          this.BufferLock
          |> Sync.write(fun () ->
            match this.Buffer with
            | x::xs ->
              this.Buffer <- xs
              Some x
            | _ -> None
          )
        this.BufferStateLock
        |> Sync.write(fun () ->
          if List.length this.Buffer > this.ActionThreshold
            && this.BufferState = BufferState.Idle && this.Mode = Server then
            this.TryReplenish()
        )
        pOp

      member internal this.TryReplenish() =
        let startSeqNum =
          (this.BufferLock
          |> Sync.read(fun () -> this.Buffer)
          |> List.rev
          |> List.head).seqNum
        this.InternalReplenish(startSeqNum)

      member internal this.InternalOnReplenish(packets:UdpPacket list) =
        this.BufferLock
        |> Sync.write(fun () ->
          this.Buffer <- this.Buffer@packets
        )
        if packets |> List.isEmpty then
          this.Mode <- ServerRetransmitting

      member internal this.InternalReplenish(startSeqNum:int64) =
        let replenisher =
          async {
            let or' = this.InternalOnReplenish
            let fs = this.OnReplenish
            let size = this.ActionThreshold
            do! PacketManagerImpl.asyncReplenishBuffer startSeqNum size fs or'
            this.BufferStateLock
            |> Sync.write(fun () ->
              this.BufferState <- BufferState.Idle
            )
          }
        replenisher
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore

      member this.HasSwitchedToRetransmitMode() =
        this.Mode = ServerRetransmitting