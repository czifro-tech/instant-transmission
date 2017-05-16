namespace MUDT.Net.Protocol

  open System
  open System.Net
  open MUDT.Net
  open MUDT.Net.ProtocolV1
  open MUDT.Net.Protocol
  open MUDT.Net.PacketBuffer
  open MUDT.IO
  open MUDT.Utilities

  type FTPType = 
    | Receiver 
    | Sender

  type FTPState =
    {
      ftpType : FTPType;
      server : TcpConnection;
      client : TcpConnection;
      udp : UdpConnection[];
      mmfc : MemoryMappedFileStateConfig;
      parallelism : int;
    }

  module internal FTPHelper =

    let parsePacketBlock bytes count (parser:byte[]->int->TcpPacketV2 option) =
      let len = Array.length bytes
      let blk = len / count;
      [|
        for i in 0..count-1 -> (parser bytes.[i*blk..((i*blk)+blk)-1] (i+1))
      |]

    let composePacketBlock (composer:TcpPacketV2->int->byte[] option) packets =
      packets
      |> Array.mapi(fun i p ->
        let bytesOption = composer p (i+1)
        if bytesOption.IsNone then [||] else bytesOption.Value
      )
      |> Array.collect(id)

    let updateStateFromHandshake (state:FTPState) (u:int*int*int[]) offset isClient =
      printfn "updating state with %A" u
      let blkSize,parallelism,ports = u
      let nHashStateConfig = 
        {
          state.mmfc.hashStateConfig with
            useBacklogLimit = if state.mmfc.hashStateConfig.doBacklogging then blkSize else 0;
            useIncrementalLimit = if state.mmfc.hashStateConfig.doIncremental then blkSize else 0;
        }
      let nMmfc = { state.mmfc with hashStateConfig = nHashStateConfig }
      let udp =
        state.udp
        |> Array.mapi(fun i conn ->
          if isClient then
            conn.Socket.Bind(IPEndPoint(IPAddress.IPv6Any, ports.[i]))
          else
            conn.RemoteEP <- IPEndPoint((state.client.Socket.RemoteEndPoint :?> IPEndPoint).Address, ports.[i])
          conn
        )
        |> Array.sortBy(fun (u:UdpConnection) ->
          let ep = if isClient then (u.Socket.LocalEndPoint :?> IPEndPoint) else u.RemoteEP
          ep.Port
        )
      { state with mmfc = nMmfc; parallelism = parallelism; udp = udp }

    let calculateBlkSize state =
      if state.mmfc.hashStateConfig.doBacklogging then
        state.mmfc.hashStateConfig.useBacklogLimit |> int64
      else state.mmfc.hashStateConfig.useIncrementalLimit |> int64

    let parsePorts (portBytes:byte[]) nPorts =
      let int32Size = sizeof<Int32>
      [|
        for i in 0..nPorts-1 ->
          ConversionUtility.bytesToInt(portBytes.[i*int32Size..((i*int32Size)+int32Size)-1])
      |]

    let createSpecsPackets isClient subtype blkSize prl nPorts =
      let packet =
        {
          TcpPacketV2.DefaultInstance with
            ptype = Tcp.getByte TcpPacketV2Type.Specification;
            subtype = subtype;
        }
      let blkSizePacket = { packet with blkSize = blkSize }
      let prlPacket = { packet with prl = prl }
      let nPortsPacket = { packet with nPorts = nPorts }
      if isClient then [| prlPacket; blkSizePacket; nPortsPacket |]
      else [| blkSizePacket; prlPacket; nPortsPacket |]

    let sendAndWait (append:byte[]) (conn:TcpConnection) (bytes:byte[]) =
      append
      |> Array.append bytes
      |> conn.SendAsync
      |> (Async.RunSynchronously >> ignore)



  module FTP =

    let configureFTPState mmfc ftpType udpCount =
      let tcp = TcpConnection()
      let udp = [| for i in 0..udpCount-1 -> UdpConnection() |]
      tcp.ByteComposer <- TcpRawMessageComposer.composeRawMessage
      tcp.ByteParser <- TcpRawMessageParser.parseRawMessage
      udp 
      |> Array.iter(fun x ->
        x.ByteComposer <- UdpPacket.ToByteArray
        x.ByteParser <- UdpPacket.TryParse
      )
      { FTPState.ftpType = ftpType; server = tcp; client = tcp;
        udp = udp; mmfc = mmfc; parallelism = udpCount; }

    let waitForConnectionAsync state port =
      async {
        printfn "Waiting for connection..."
        do! state.server.ListenAsync(port)
        let! client = state.server.AcceptAsync()
        return { state with client = client }
      }

    let connectAsync state ip port =
      async {
        do! Async.Sleep 1000
        let server = state.server
        do! server.ConnectAsync(ip, port)
        return { state with server = server }
      }

    let private doClientHandshakeAsync state ports =
      async {
        state.server.ByteParser <- TcpRawMessageParser.parseRawMessage
        state.server.ByteComposer <- TcpRawMessageComposer.composeRawMessage
        printfn "Set parser and composer"
        // receive specs request
        let bytesOption = state.server.Receive(TcpPacketV2.DefaultSize)
        let packetOption = state.server.ByteParser bytesOption.Value 0
        if packetOption.IsNone then return None
        else
          printfn "Client: Received specs request..."
          let localBlkSize = FTPHelper.calculateBlkSize state
          let subtype = Tcp.getByte TcpPacketSubType.Exchange
          let parallelism = byte state.parallelism
          let portBytes = ports |> Array.collect ConversionUtility.intToBytes
          // send specs
          FTPHelper.createSpecsPackets true subtype localBlkSize parallelism (Array.length ports)
          // |> Array.map(fun p ->
          //   printfn "Packet: %s" (p.ToString())
          //   p
          // )
          |> FTPHelper.composePacketBlock state.server.ByteComposer
          |> FTPHelper.sendAndWait portBytes state.server
          printfn "Client: Sent specs..."
          // receive specs to use
          let bytes = state.server.Receive(TcpPacketV2.DefaultSize*3)
          let packets = FTPHelper.parsePacketBlock bytes.Value 3 state.server.ByteParser
          printfn "Client: Received specs to use..."
          let successful = (not (Array.exists (fun (p:TcpPacketV2 option) -> p.IsNone) packets))
          if not successful then return None
          else
            let portBytes = (state.server.Receive(packets.[2].Value.nPorts*sizeof<Int32>)).Value
            let ports = FTPHelper.parsePorts portBytes packets.[2].Value.nPorts
            let minBlkSize = min localBlkSize packets.[0].Value.blkSize
            let parallelism = min state.parallelism (int packets.[1].Value.prl)
            return Some (FTPHelper.updateStateFromHandshake state (int minBlkSize,parallelism,ports) 0 true)
      }

    let private doServerHandshakeAsync state _ =
      async {
        // specs request
        let packet =
          {
            TcpPacketV2.DefaultInstance with
              ptype = Tcp.getByte TcpPacketV2Type.Specification;
              subtype = Tcp.getByte TcpPacketSubType.Request
          }
        let! res = state.client.SendAsync((state.client.ByteComposer packet 0).Value)
        printfn "Server: Sent specs request..."
        // specs response, 3 packets
        let bytesOption = state.client.Receive(TcpPacketV2.DefaultSize*3)
        let packets = FTPHelper.parsePacketBlock bytesOption.Value 3 state.client.ByteParser
        let successful = (not (Array.exists (fun (p:TcpPacketV2 option) -> p.IsNone) packets))
        // if all packets parse successfully, read in ports
        if successful then
          // packets
          // |> Array.iter(fun p ->
          //   printfn "Packet: %s" ((p.Value).ToString())
          // )
          printfn "Server: Received specs..."
          let portBytes = (state.client.Receive(packets.[2].Value.nPorts*sizeof<Int32>)).Value
          // parse ports, but filter out unusable ones and take min(clientPrl,serverPrl)
          let ports =
            FTPHelper.parsePorts portBytes packets.[2].Value.nPorts
            |> PortChecker.filterAvailablePorts
            |> Array.take (min state.parallelism (int packets.[0].Value.prl))
          let portBytes = ports |> Array.collect ConversionUtility.intToBytes
          let minBlkSize = 
            let localBlkSize = FTPHelper.calculateBlkSize state
            min localBlkSize packets.[1].Value.blkSize
          let subtype = Tcp.getByte TcpPacketSubType.Set
          let parallelism = min packets.[0].Value.prl (byte state.parallelism)
          let nPorts = min (int packets.[0].Value.prl) state.parallelism
          FTPHelper.createSpecsPackets false subtype minBlkSize parallelism nPorts
          |> FTPHelper.composePacketBlock state.client.ByteComposer
          |> FTPHelper.sendAndWait portBytes state.client
          printfn "Server: Sent specs to use..."
          let v = (int minBlkSize,(min (int packets.[0].Value.prl) state.parallelism),ports)
          let nState = FTPHelper.updateStateFromHandshake state v 1 false
          return Some state
        else return None
      }

    let handshakeAsync state ports =
      async {
        let handshake =
          match state.ftpType with
          | Receiver -> doClientHandshakeAsync
          | Sender -> doServerHandshakeAsync
        return! handshake state ports
      }

    open MUDT.Cryptography

    let receiveFileAsync state (doRetransmit:bool) =
      async {
        let mmfState = MemoryMappedFile.partitionFile false state.mmfc
        let bytes = state.server.Receive TcpPacketV2.DefaultSize |> Option.get
        let tcpPacket = state.server.ByteParser bytes 0
        if tcpPacket.IsNone ||
          Tcp.parsePacketTypeFromPacket tcpPacket.Value <> (TcpPacketV2Type.Transfer,TcpPacketSubType.Prepare) then return None
        else
          let rwl = new System.Threading.ReaderWriterLockSlim()
          let mutable continueLooping = true
          let getLoopCondition debug =
            if debug || not continueLooping then
              printfn "Checking loop condition..."
            rwl.EnterReadLock()
            try
              if debug || not continueLooping then
                printfn "continueLooping: %b" continueLooping
                printfn "IsReadLockHeld: %b" rwl.IsReadLockHeld
                printfn "IsWriteLockHeld: %b" rwl.IsWriteLockHeld
              continueLooping
            finally
              rwl.ExitReadLock()
          let setLoopCondition v =
            rwl.EnterWriteLock()
            try
              printfn "continueLooping: %b" continueLooping
              printfn "IsReadLockHeld: %b" rwl.IsReadLockHeld
              printfn "IsWriteLockHeld: %b" rwl.IsWriteLockHeld
              continueLooping <- v
            finally
              rwl.ExitWriteLock()
          //printfn "Test getting loop condition: %b" (getLoopCondition true)
          let receivePartitionAsync (getLoopCondition':bool->bool) (partition:MMFPartitionState) (connection:UdpConnection) =
            async {
              let mutable p = partition
              let mutable c = connection
              printfn "Partition Start: %d, Socket Port: %d" p.startPosition ((c.Socket.LocalEndPoint :?> IPEndPoint).Port)
              //printfn "Partition State: %s" (p.ToString())
              let ds,pc,sn,pd = UdpPacket.DefaultSize,UdpPacket.PacketCompare,UdpPacket.GetSeqNum,UdpPacket.GetPacketData
              let mutable pbState = PacketBufferState.New<UdpPacket> 20 ds p.startPosition pc sn pd
              let copying bytes = p <- MMFPartition.writeToBufferAsync p bytes |> Async.RunSynchronously
              let asyncReceive =
                async {
                  return c.Receive UdpPacket.DefaultSize
                }
              let notEndOfPartition() = not <| MMFPartition.feop p
              let continueLooping'() = getLoopCondition' false
              while notEndOfPartition() && continueLooping'() do
                // receive data
                // only wait a couple secs in case
                //  there is nothing left but buffer contains data
                let bytesOption : byte[] option =
                  try
                    Some <| Async.RunSynchronously asyncReceive
                  with
                  | _ -> 
                    printfn "Timeout Occurred"
                    //printfn "Partition State: %s" (p.ToString())
                    None
                // if we didn't get anything flush buffer,
                //  perhaps we'll reach eop
                if bytesOption.IsNone then
                  //printfn "Got nothing"
                  pbState <- PacketBuffer.tryCopyTo copying true pbState
                  let! np = MMFPartition.fullFlushBufferAsync p
                  p <- np
                else
                  let packetOption = UdpPacket.TryParse bytesOption.Value
                  if packetOption.IsNone then
                    pbState <- PacketBuffer.tryCopyTo copying true pbState
                    let! np = MMFPartition.fullFlushBufferAsync p
                    p <- np
                  else
                    //printfn "Received: %s" (packetOption.Value.ToString())
                    pbState <- PacketBuffer.push packetOption.Value pbState
                    pbState <- PacketBuffer.tryCopyTo copying false pbState
              printfn "Finished receiving..."
              printfn "Partition Start: %d, Socket Port: %d" p.startPosition ((c.Socket.LocalEndPoint :?> IPEndPoint).Port)
              if p.bytesWrittenCounter < p.partitionSize then
                let len = p.partitionSize - p.bytesWrittenCounter
                p <- MMFPartition.writeToBufferAsync p (TypeUtility.nullByteArray (int len)) |> Async.RunSynchronously
              //printfn "Partition State: %s" (p.ToString())
              pbState <- PacketBuffer.tryCopyTo copying true pbState
              let! np = MMFPartition.fullFlushBufferAsync p
              p <- np
              return p
            }
          let receivePartitionAsyncHandles = 
            Array.map2 (receivePartitionAsync <| getLoopCondition) mmfState.partitions state.udp
            |> Array.map Async.StartChild
          // pause flow for a brief moment to allow nested async to run
          do! Async.Sleep 100
          let receivePartitionAsyncHandles_ = // get the tasks running
            receivePartitionAsyncHandles
            |> Array.map Async.RunSynchronously
          do! Async.Sleep 3000
          printfn "Started Receivers, notifying server..."

          let readyPacket =
            { TcpPacketV2.DefaultInstance with
                ptype = Tcp.getByte TcpPacketV2Type.Transfer; subtype = Tcp.getByte TcpPacketSubType.Ready }
          state.server.ByteComposer readyPacket 0
          |> Option.get
          |> FTPHelper.sendAndWait [||] state.server

          printfn "Waiting for server to finish..."
          let bytes = (state.server.Receive(TcpPacketV2.DefaultSize)).Value
          let packet = (state.server.ByteParser bytes 0).Value
          if packet.ptype <> Tcp.getByte TcpPacketV2Type.Transfer && packet.subtype <> Tcp.getByte TcpPacketSubType.Finished then
            printfn "Wrong message..."
            setLoopCondition false
            do! Async.Sleep 500
            let nPartitions =
              receivePartitionAsyncHandles_
              |> Array.map Async.RunSynchronously
            let mmfState = { mmfState with partitions = nPartitions }
            MemoryMappedFile.finalize mmfState
            return None
          else
            printfn "Server finished, terminating receive..."
            setLoopCondition false
            do! Async.Sleep 500
            let nPartitions =
              receivePartitionAsyncHandles_
              |> Array.map Async.RunSynchronously
            let mmfState = { mmfState with partitions = nPartitions }

            let mmfState,checksum = MemoryMappedFile.checksum mmfState
            printfn "Checksum Count: %d" (Array.length checksum)
            printfn "Checksum: %A" checksum
            let checksumBytes = Hasher.serialize checksum
            printfn "Serialized Checksum Size: %d bytes" ((Array.length checksum) * 24)
            printfn "Serialized Checksum: %A" checksumBytes
            let packet =
              { TcpPacketV2.DefaultInstance with
                  ptype = Tcp.getByte TcpPacketV2Type.Transfer;
                  subtype = Tcp.getByte TcpPacketSubType.Checksum;
                  csumSize = int64(Array.length checksumBytes) }

            // state.server.ByteComposer packet 0
            // |> Option.get
            // |> FTPHelper.sendAndWait checksumBytes state.server
            checksumBytes
            |> Array.append (state.server.ByteComposer packet 0).Value
            |> (fun arr ->
              printfn "Bytes: %A" arr
              arr
            )
            |> state.server.SendAsync
            |> Async.RunSynchronously
            |> (fun c -> printfn "SocketError: %s" (System.Net.Sockets.SocketException(int c)).Message)

            MemoryMappedFile.finalize mmfState
            return Some (mmfState,state)
      }

    let sendFileAsync state (doRetransmit:bool) =
      async {
        let state = 
          {
            state with
              udp = 
                state.udp
                |> Array.sortBy(fun u -> u.RemoteEP.Port)
          }
        let mmfState = MemoryMappedFile.partitionFile true state.mmfc
        let prepareBuffersAsync =
          async {
            let partitionStates =
              mmfState.partitions
              |> Array.map (MMFPartition.initializeReadBufferAsync >> Async.RunSynchronously)
            return { mmfState with partitions = partitionStates }
          }
        //let newMmfStateAsyncHandle = prepareBuffersAsync |> Async.StartChild
        // pause flow for a brief moment to allow nested async to run
        //do! Async.Sleep 100
        //let! newMmfStateAsyncHandle = newMmfStateAsyncHandle
        // pause flow for a brief moment to allow nested async to run
        //do! Async.Sleep 200
        // tell client to prepare
        let preparePacket =
          { TcpPacketV2.DefaultInstance with 
              ptype = Tcp.getByte TcpPacketV2Type.Transfer; subtype = Tcp.getByte TcpPacketSubType.Prepare }
        state.client.ByteComposer preparePacket 0
        |> Option.get
        |> FTPHelper.sendAndWait [||] state.client
        let bytes = (state.client.Receive(TcpPacketV2.DefaultSize)).Value
        let packet = state.client.ByteParser bytes 0
        if packet.IsNone ||
          Tcp.parsePacketTypeFromPacket packet.Value <> (TcpPacketV2Type.Transfer,TcpPacketSubType.Ready) then return None
        else
          // begin sending file
          let! mmfState = prepareBuffersAsync
          let sendPartitionAsync (partition:MMFPartitionState) (connection:UdpConnection) =
            async {
              let mutable p = partition
              let mutable c = connection
              printfn "Partition Start: %d, Socket Port: %d" p.startPosition (c.RemoteEP.Port)
              let mutable seqNum = p.startPosition
              while not <| MMFPartition.feop p do
                let! (bytes,np) = MMFPartition.readFromBufferAsync p UdpPacket.PayloadSize
                p <- np
                let packet = 
                  { seqNum = seqNum; dLen = (Array.length bytes);
                    data = bytes}
                //printfn "Sending: %s" (packet.ToString())
                seqNum <- seqNum + int64(Array.length bytes)
                let! _ = c.SendAsync(packet)
                ()
              return p
            }
        
          let sendPartitionAsyncHandles = 
            Array.map2 sendPartitionAsync mmfState.partitions state.udp
            |> Array.map Async.StartChild
          // pause flow for a brief moment to allow nested async to run
          do! Async.Sleep 100
          let sendPartitionAsyncHandles =
            sendPartitionAsyncHandles
            |> Array.map Async.RunSynchronously
          do! Async.Sleep 100
          let nPartitions =
            sendPartitionAsyncHandles
            |> Array.map Async.RunSynchronously
          let mmfState = { mmfState with partitions = nPartitions }
          printfn "Finished initial transmission..."
          let packet =
            {
              TcpPacketV2.DefaultInstance with
                ptype = Tcp.getByte TcpPacketV2Type.Transfer;
                subtype = Tcp.getByte TcpPacketSubType.Finished;
            }
          state.client.ByteComposer packet 0
          |> Option.get
          |> FTPHelper.sendAndWait [||] state.client
          printfn "Waiting for checksum..."
          let bytes = (state.client.Receive(TcpPacketV2.DefaultSize)).Value
          let packet = (state.client.ByteParser bytes 0).Value
          printfn "Checksum packet header: %s" (packet.ToString())
          let checksumBytes = (state.client.Receive(int packet.csumSize)).Value
          printfn "Serialized Checksum Size: %d bytes" (Array.length checksumBytes)
          printfn "Serialized Checksum: %A" checksumBytes
          let checksum = Hasher.deserialize checksumBytes
          printfn "Checksum Count: %d" (Array.length checksum)
          printfn "Checksum: %A" checksum
          let mmfState,localChecksum = MemoryMappedFile.checksum mmfState
          printfn "Local Checksum Count: %d" (Array.length localChecksum)
          printfn "Local Checksum: %A" localChecksum
          let mismatch = Hasher.compareHash checksum localChecksum
          printfn "Mismatch: %A" mismatch

          return Some (mmfState,state)
      }
