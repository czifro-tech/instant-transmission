namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol

  [<AutoOpen>]
  module Client =

    type PacketManager with

      member internal this.OnMissingPacketsDetected(seqNums:int64[]) =
        let reporter =
          async {
            let od = this.OnDropped
            do! PacketManagerImpl.asyncReportDroppedPackets seqNums od
          }
        reporter
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore

      member internal this.RecoveredPacket(packet:UdpPacket) =
        let isNew =
          this.RecoveredBufferLock
          |> Sync.write(fun () ->
            if this.RecoveredLookup |> Set.contains packet.seqNum then
              false
            else
              this.RecoveredBuffer <- this.RecoveredBuffer@[packet]
              this.RecoveredLookup <- this.RecoveredLookup |> Set.add packet.seqNum
              true
          )
        if isNew then
          this.OnRecovery packet.seqNum // trigger ack process
          this.RecoveredBufferStateLock
          |> Sync.write(fun () ->
            if this.RecoveredBufferState = BufferState.Idle then
              this.ProcessRecoveredPackets()
          )

      member internal this.ProcessRecoveredPackets() =
        let processor =
          async {
            let nextPacket _ =
              this.RecoveredBufferLock
              |> Sync.write(fun () ->
                match this.RecoveredBuffer with
                | x::xs ->
                  this.RecoveredBuffer <- xs
                  Some x
                | _ -> None
              )
            let mutable packetOption = nextPacket()
            while packetOption.IsSome do
              let loc,data = packetOption.Value.seqNum,packetOption.Value.data
              this.OnWrite data loc
              packetOption <- nextPacket()
            this.RecoveredBufferStateLock
            |> Sync.write(fun () ->
              this.RecoveredBufferState <- BufferState.Idle
            )
          }
        processor
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore

      member this.AddPacket(packet:UdpPacket) =
        if this.Mode <> Client then
          failwith "This is a client only feature"
        this.BufferLock
        |> Sync.write(fun () ->
          if packet.seqNum >= this.BeginSeqNum then
            // gives opportunity for retransmitted packets to be in buffer
            //  if above condition is met
            // ensure no duplicates
            if not <| (this.Buffer |> List.exists(fun p -> p.seqNum = packet.seqNum)) then
              this.Buffer <- this.Buffer@[packet]
            // in case this packet was retransmitted, trigger ack
            if packet |> UdpPacket.IsRetransmissionPacket then
              this.OnRecovery packet.seqNum
          else
            // we assume that a packet is either late or retransmitted
            // This process may take a few ms more than a regular packet
            this.RecoveredPacket(packet)
        )
        this.BufferStateLock
        |> Sync.write(fun () ->
          if List.length this.Buffer > this.ActionThreshold && this.BufferState = BufferState.Idle then
            this.BufferState <- BufferState.Flushing
            this.TryFlush()
        )

      member internal this.TryFlush() =
        let p =
          this.BufferLock
          |> Sync.write(fun () ->
            let temp = this.Buffer
            this.Buffer <- []
            this.BeginSeqNum <- this.MaxSeqNum
            temp
          )
        this.InternalFlush(p)

      member internal this.InternalFlush(packets:UdpPacket list) =
        let flusher =
          async {
            let om = this.OnMissingPacketsDetected
            let of' = this.OnFlush
            do! PacketManagerImpl.asyncFlushPacketsToBuffer packets om of' this.NetworkLogger
            this.BufferStateLock
            |> Sync.write(fun () ->
              this.BufferState <- BufferState.Idle
            )
          }
        flusher
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore