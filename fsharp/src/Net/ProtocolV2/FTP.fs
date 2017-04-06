namespace MUDT.Net.Protocol

  open System
  open MUDT.Net
  open MUDT.Net.ProtocolV1
  open MUDT.Net.Protocol
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

    let composePacketBlock packets (composer:TcpPacketV2->int->byte[]) =
      packets
      |> Array.mapi(fun i p ->
        composer p i
      )
      |> Array.concat


  module FTP =

    let configureFTPState mmfc ftpType udpCount =
      let tcp = TcpConnection()
      let udp = [| for i in 1..udpCount-1 -> UdpConnection() |]
      tcp.ByteComposer <- TcpRawMessageComposer.composeRawMessage
      tcp.ByteParser <- TcpRawMessageParser.parseRawMessage
      udp 
      |> Array.iter(fun x ->
        x.ByteComposer <- UdpPacket.ToByteArray
        x.ByteParser <- UdpPacket.TryParse
      )
      { FTPState.ftpType = ftpType; server = tcp; client = tcp; udp = udp; mmfc = mmfc; parallelism = udpCount; }

    let waitForConnectionAsync state port =
      async {
        do! state.server.ListenAsync(port)
        let! client = state.server.AcceptAsync()
        return { state with client = client }
      }

    let connectAsync state ip port =
      async {
        do! state.client.ConnectAsync(ip, port)
      }

    let doClientHandshakeAsync state ports =
      async {

      }

    let doServerHandshakeAsync state _ =
      async {
        // specs request
        let packet =
          {
            TcpPacketV2.DefaultInstance with
              ptype = Tcp.getByte TcpPacketType.Specification;
              subtype = Tcp.getByte TcpPacketSubType.Request
          }
        let! res = state.client.SendAsync(packet)
        // specs response, 3 packets
        let bytes = state.client.Receive(TcpPacketV2.DefaultSize*3)
        let packets = FTPHelper.parsePacketBlock bytes.Value 3 state.client.ByteParser
        let successful = (not (Array.exists (fun p -> p.IsNone) packets))
        // if all packets parse successfully, read in ports
        if successful then
          let bytes = state.client.Receive(packets.[2].Value.numPorts*sizeof<Int32>)
          // parse ports, but filter out unusable ones and take min(clientPrl,serverPrl)
          let ports =
            [| 
              for i in 0..((Array.length (bytes.Value)) / sizeof<Int32>) ->
                ConversionUtility.bytesToInt(bytes.Value.[i*sizeof<Int32>..((i*sizeof<Int32>)+sizeof<int32>)-1])
            |]
            |> PortChecker.filterAvailablePorts
            |> Array.take (min(state.parallelism,packet.Value.prl))
          let portBytes = ports |> Array.collect ConversionUtility.intToBytes
          let minBlkSize = 
            let localBlkSize =
              if state.mmfc.hashStateConfig.doBacklogging then
                state.mmfc.hashStateConfig.useBacklogLimit
              else state.mmfc.hashStateConfig.useIncrementalLimit
            min(localBlkSize, packets.[1].Value.blkSize)
          let res =
            [|
              { TcpPacketV2.DefaultInstance with 
                  ptype = Tcp.getByte TcpPacketType.Specification;
                  subtype = Tcp.getByte TcpPacketSubType.Set;
                  blkSize = minBlkSize };
              { TcpPacketV2.DefaultInstance with 
                  ptype = Tcp.getByte TcpPacketType.Specification;
                  subtype = Tcp.getByte TcpPacketSubType.Set;
                  prl = min(packets.[0].Value.prl,state.parallelism) };
              { TcpPacketV2.DefaultInstance with 
                  ptype = Tcp.getByte TcpPacketType.Specification;
                  subtype = Tcp.getByte TcpPacketSubType.Set;
                  numPorts = min(packets.[0].Value.prl,state.parallelism) };
            |]
            |> Array.map(fun p ->
              state.client.SendAsync(p)
            )
          res |> Array.iter(fun r -> Async.RunSynchronously r)

      }

    let handshakeAsync state ports =
      async {
        let handshake =
          match state.ftpType with
          | Receiver -> doClientHandshakeAsync
          | Sender -> doServerHandshakeAsync
        do! handshake state ports
      }
