namespace MUDT.Net

  open MUDT.Collections
  open MUDT.Utilities

  module PacketBuffer =

    type ComparablePacket<'a>(packet:'a,packetCompare:'a->'a->int) =
      member __.Packet = packet
      interface System.IComparable with
        member __.CompareTo(o:obj) =
          packetCompare packet (o:?>ComparablePacket<'a>).Packet
      
      override __.Equals(o:obj) =
        (packetCompare packet (o:?>ComparablePacket<'a>).Packet) = 0

      override __.GetHashCode() =
        (System.Guid.NewGuid()).GetHashCode()

    type PacketBufferState<'a> =
      {
        buffer         : IPriorityQueue<ComparablePacket<'a>>;
        capacity       : int;
        nextSeqNum     : int64;
        packetSize     : int;
        packetCompare  : 'a->'a->int;
        seqNum         : 'a->int64;
        packetData     : 'a->byte[]
      }

      static member New<'a> capacity packetSize startSeqNum
        (packetCompare:'a->'a->int) (seqNum:'a->int64) (packetData:'a->byte[]) =
        {
          buffer         = PriorityQueue.empty<ComparablePacket<'a>> false;
          capacity       = capacity;
          nextSeqNum     = startSeqNum;
          packetSize     = packetSize;
          packetCompare  = packetCompare;
          seqNum         = seqNum;
          packetData     = packetData;
        }

    let push packet state =
      let seqNum = state.seqNum packet
      if seqNum < state.nextSeqNum then
        printfn "Packet %d arrived too late!" seqNum
        state
      else
        { state with buffer = PriorityQueue.insert (new ComparablePacket<'a>(packet,state.packetCompare)) state.buffer }

    let tryPop state =
      let ret = PriorityQueue.tryPop state.buffer
      if ret.IsNone then None
      else
        let comparablePacket,buffer = ret.Value
        let packet = comparablePacket.Packet
        let packetData = state.packetData packet
        let seqNum = state.seqNum packet
        let bytes =
          //printfn "Expected: %d, Current: %d" state.nextSeqNum seqNum
          if state.nextSeqNum = seqNum then
            packetData
          else
            printfn "Packet dropped in range %d-%d" state.nextSeqNum seqNum
            (packetData) |> Array.append (TypeUtility.nullByteArray (int (state.nextSeqNum - seqNum)))
        Some (bytes,{ state with buffer = buffer; nextSeqNum = seqNum + int64(Array.length bytes) })

    let tryCopyTo (copying:byte[]->unit) force state =
      let mutable len = PriorityQueue.length state.buffer
      //printfn "Packet Buffer Length: %d" len
      let mutable state = state
      if state.capacity < len || force then
        let mutable bytes : byte[] = [||]
        while len > 0 do
          let bytes',nState = (tryPop state).Value
          state <- nState
          bytes <- bytes' |> Array.append bytes
          len <- len - 1
        if not <| Array.isEmpty bytes then
          copying bytes
        state
      else
        state