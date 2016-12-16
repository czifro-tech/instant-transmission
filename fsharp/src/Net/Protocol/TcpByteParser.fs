namespace MUDT.Net.Protocol

  open MUDT.Utilities
  open MUDT.Net.Protocol

  module TcpByteParser =

    let parseMeta (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          fileSize = ConversionUtility.bytesToInt64 bytes.[1..8];
          fnLen = ConversionUtility.bytesToInt bytes.[9..13];
      }

    let parserPorts (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          numPorts = ConversionUtility.bytesToInt bytes.[1..4];
      }

    let parseAct (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          action = bytes.[1];
      }

    let parseChecksum (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          pNum = ConversionUtility.bytesToInt bytes.[1..4];
          dLen = ConversionUtility.bytesToInt bytes.[5..9];
      }

    let parseChecksumValidation (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          result = bytes.[1];
          pNum = ConversionUtility.bytesToInt bytes.[2..6];
      }

    let parsePacketDropped (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0]
          seqNum = ConversionUtility.bytesToInt64 bytes.[1..8];
      }

    let parsePing (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          timestamp = ConversionUtility.bytesToInt64 bytes.[1..8];
      }

    let byteArrayToTcpPacket (bytes:byte[]) =
      match Tcp.parsePacketType { TcpPacket.DefaultInstance() with TcpPacket.ptype = bytes.[0] } with
      | Meta -> Some (parseMeta bytes)
      | Ports -> Some (parserPorts bytes)
      | Action -> Some (parseAct bytes)
      | Checksum -> Some (parseChecksum bytes)
      | ChecksumValidation -> Some (parseChecksumValidation bytes)
      | PacketDropped -> Some (parsePacketDropped bytes)
      | Ping -> Some (parsePing bytes)
      | _ -> None