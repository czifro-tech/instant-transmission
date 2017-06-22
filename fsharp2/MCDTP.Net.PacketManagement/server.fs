namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol
  open System.Threading

  [<AutoOpen>]
  module Server =

    type PacketManager with

      member this.InitializeBuffer() =
        let initializer =
          async {
            let or' = this.InternalOnReplenish
            let fs = this.OnReplenish
            let size = this.ActionThreshold*2
            do! PacketManagerImpl.asyncReplenishBuffer size fs or'
            this.BufferStateLock
            |> Sync.write(fun () ->
              this.BufferState <- BufferState.Idle
            )
          }
        initializer
        |> Async.RunSynchronously

      member this.TryPullPacket() =
        let pOp =
          this.BufferLock
          |> Sync.write(fun () ->
            match this.Buffer with
            | x::xs ->
              this.Buffer <- xs
              let msg = sprintf "Popped packet from buffer: %A" x
              this.NetworkLogger.LogPlainMessage(LogLevel.None,msg)
              Some x
            | _ -> None
          )
        this.BufferStateLock
        |> Sync.write(fun () ->
          let doTryReplenish =
            (pOp.IsNone && this.BufferState = BufferState.Idle)
              || (List.length this.Buffer > this.ActionThreshold
                  && this.BufferState = BufferState.Idle && this.Mode = Server)
          if doTryReplenish then
            this.BufferState <- BufferState.Replenishing
            this.TryReplenish()
        )
        pOp

      member internal this.TryReplenish() =
        this.InternalReplenish()

      member internal this.InternalOnReplenish(packets:UdpPacket list) =
        if packets |> List.isEmpty then
          if this.Timer |> isNull then
            let cb = TimerCallback(this.RetransmitCallback)
            // Using only a due time, periodic is self managed
            this.Timer <- new Timer(cb,null,this.RetransmitInterval,Timeout.Infinite)
          let packetOp = this.EndPacket
          if packetOp.IsSome then
            let message = "Buffered end packet"
            this.NetworkLogger.LogPlainMessage(LogLevel.Info,message)
            this.RetransmitLock
            |> Sync.write(fun () ->
              this.Retransmit <- [packetOp.Value]@this.Retransmit
            )
        else
          this.BufferLock
          |> Sync.write(fun () ->
            this.Buffer <- this.Buffer@packets
          )
          let message = sprintf "Loaded %d packets" (List.length packets)
          this.NetworkLogger.LogPlainMessage(LogLevel.Info,message)

      member internal this.InternalReplenish () =
        let replenisher =
          async {
            let or' = this.InternalOnReplenish
            let fs = this.OnReplenish
            let size = this.ActionThreshold
            do! PacketManagerImpl.asyncReplenishBuffer size fs or'
            this.BufferStateLock
            |> Sync.write(fun () ->
              this.BufferState <- BufferState.Idle
            )
          }
        replenisher
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore