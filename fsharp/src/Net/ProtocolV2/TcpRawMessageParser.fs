namespace MUDT.Net.Protocol

  module internal TcpRawMessageParsers =

    open MUDT.Utilities
    open MUDT.Net.Protocol

    let private parseCommonHeader (bytes:byte[]) =
      {
        TcpPacketV2.DefaultInstance with
          ptype = bytes.[0];
          subtype = bytes.[1];
      }

    let parseSpecsRequest bytes =
      parseCommonHeader bytes

    let parseSpecsPacket op bytes =
      let print packet =
        printfn "Parsing..."
        printfn "op: %d" op
        printfn "packet: %s" (packet.ToString())
        printfn "bytes: %A" bytes
        packet
      let packet = parseCommonHeader bytes
      match (op,char(packet.subtype)) with
        | 1,'s' | 2,'e' -> { packet with blkSize = ConversionUtility.bytesToInt64(bytes.[2..9]) }
        | 1,'e' | 2,'s' -> { packet with prl = bytes.[2] }
        | 3, _          -> { packet with nPorts = ConversionUtility.bytesToInt(bytes.[2..5]) }
        | _             -> TcpPacketV2.DefaultInstance
      //|> print

    let parseTransferRequest bytes =
      {
        parseCommonHeader bytes with
          fnSize = ConversionUtility.bytesToInt(bytes.[2..5])
      }

    let parseTransferPrepare bytes =
      {
        parseCommonHeader bytes with
          fileSize = ConversionUtility.bytesToInt64(bytes.[2..9])
      }

    let parseTransferReady bytes =
      parseCommonHeader bytes

    let parseTransferFinished bytes =
      parseCommonHeader bytes

    let parserTransferChecksum bytes =
      {
        parseCommonHeader bytes with
          csumSize = ConversionUtility.bytesToInt64(bytes.[2..9])
      }

  module TcpRawMessageParser =

    open TcpRawMessageParsers
    let parseRawMessage bytes op =
      if op = -1 then
        printfn "Parsing: %A" bytes
        let tuple = Tcp.parsePacketTypeFromRaw bytes
        printfn "Packet type: %c %c" (Tcp.getByte(fst tuple) |> char) (Tcp.getByte(snd tuple) |> char)
      match Tcp.parsePacketTypeFromRaw bytes with
      | Specification, Request -> Some (parseSpecsRequest bytes)
      | Specification, Exchange | Specification, Set -> Some (parseSpecsPacket op bytes)
      | Transfer, Request -> Some (parseTransferRequest bytes)
      | Transfer, Prepare -> Some (parseTransferPrepare bytes)
      | Transfer, Ready -> Some (parseTransferReady bytes)
      | Transfer, Finished -> Some (parseTransferFinished bytes)
      | Transfer, Checksum -> Some (parserTransferChecksum bytes)
      | _ -> None

