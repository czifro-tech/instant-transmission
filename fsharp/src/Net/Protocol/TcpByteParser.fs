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

    let parseCSum (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          pNum = ConversionUtility.bytesToInt bytes.[1..4];
          dLen = ConversionUtility.bytesToInt bytes.[5..9];
      }

    let parseCSVal (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          result = bytes.[1];
          pNum = ConversionUtility.bytesToInt bytes.[2..6];
      }

    let parsePDrop (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0]
          seqNum = ConversionUtility.bytesToInt64 bytes.[1..8];
      }

    let parserPing (bytes:byte[]) =
      {
        TcpPacket.DefaultInstance() with
          ptype = bytes.[0];
          timestamp = ConversionUtility.bytesToInt64 bytes.[1..8];
      }

    let byteArrayToTcpPacket (bytes:byte[]) =
      match (bytes.[0] |> char) with
      | t when t = TcpPacketType.Meta -> Some (parseMeta bytes)
      | t when t = TcpPacketType.Ports -> Some (parserPorts bytes)
      | t when t = TcpPacketType.Act -> Some (parseAct bytes)
      | t when t = TcpPacketType.CSum -> Some (parseCSum bytes)
      | t when t = TcpPacketType.CSVal -> Some (parseCSVal bytes)
      | t when t = TcpPacketType.PDrop -> Some (parsePDrop bytes)
      | t when t = TcpPacketType.Ping -> Some (parserPing bytes)
      | _ -> None