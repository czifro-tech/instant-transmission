namespace MUDT.Net.Protocol

  open MUDT.Utilities
  open MUDT.Net.Protocol

  module TcpByteComposer =

    let private createByteArray =
      TypeUtility.nullByteArray TcpPacket.DefaultSize

    let private copyAsBytes (x:System.Object) (offset:int) (bytes:byte[]) =
      x |> ConversionUtility.getBytes
      |> Array.iteri(fun i b -> bytes.[i+offset] <- b)
      bytes

    let composeMeta (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.fileSize 1
      |> copyAsBytes packet.fnLen 9

    let composePorts (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.numPorts 1

    let composeAct (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.action 1

    let composeCSum (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.pNum 1
      |> copyAsBytes packet.dLen 5

    let composeCSVal (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.result 1
      |> copyAsBytes packet.pNum 2

    let composePDrop (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.seqNum 1

    let composePing (packet:TcpPacket) =
      createByteArray
      |> copyAsBytes packet.ptype 0
      |> copyAsBytes packet.timestamp 1

    let tcpPacketToByteArray (packet:TcpPacket) =
      match (packet.ptype |> char) with
      | t when t = TcpPacketType.Meta -> Some (composeMeta packet)
      | t when t = TcpPacketType.Ports -> Some (composePorts packet)
      | t when t = TcpPacketType.Act -> Some (composeAct packet)
      | t when t = TcpPacketType.CSum -> Some (composeCSum packet)
      | t when t = TcpPacketType.CSVal -> Some (composeCSVal packet)
      | t when t = TcpPacketType.PDrop -> Some (composePDrop packet)
      | t when t = TcpPacketType.Ping -> Some (composePing packet)
      | _ -> None