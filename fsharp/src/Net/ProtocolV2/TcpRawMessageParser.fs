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
      let packet = parseCommonHeader bytes
      match (op,char(packet.subtype)) with
        | 1,'s'         -> { packet with blkSize = ConverstionUtility.bytesToInt64(bytes.[2..9]) }
        | 1,'e' | 2,'s' -> { packet with prl = bytes.[2] }
        | 2,'e'         -> { packet with memLimit = ConversionUtility.bytesToInt64(bytes.[2..9]) }
        | 3, _          -> { packet with nPorts = ConversionUtility.bytesToInt(bytes.[2..5]) }
        | _             -> TcpPacketV2.DefaultInstance

    let parseCommand (bytes:byte[]) =
      {
        TcpPacketV2.DefaultInstance with
          ptype = bytes.[0];
          cmd = ConversionUtility.bytesToInt([| bytes.[1] |]);
      }

    let parseCommandResponse bytes =
      {
        parseCommonHeader bytes with
          payloadSize = ConversionUtility.bytesToInt64(bytes.[2..9])
      }

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

  module TcpRawMessageParser =

    open TcpRawMessageParsers
    let parseRawMessage bytes op =
      match Tcp.parsePacketTypeFromRaw bytes with
      | Specification, Request -> Some (parseSpecsRequest bytes)
      | Specification, Exchange | Specification, Set -> Some (parseSpecsPacket op bytes)
      | Command, Output -> Some (parseCommandResponse bytes)
      | Command, _ -> Some (parseCommand bytes)
      | Transfer, Request -> Some (parseTransferRequest bytes)
      | Transfer, Prepare -> Some (parseTransferPrepare bytes)
      | Transfer, Ready -> Some (parseTransferReady bytes)
      | _ -> None

