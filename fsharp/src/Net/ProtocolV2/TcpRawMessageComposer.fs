namespace MUDT.Net.Protocol

  module internal TcpRawMessageComposers =

    open MUDT.Utilities
    open MUDT.Net.Protocol

    let rawMessage() = TypeUtility.nullByteArray TcpPacketV2.DefaultSize

    let insertAsBytes (x:System.Object) (offset:int) (bytes:byte[]) =
      x 
      |> ConversionUtility.getBytes
      |> Array.iteri(fun i b -> bytes.[i+offset] <- b)
      bytes

    let private composeCommonHeader (packet:TcpPacketV2) =
      rawMessage()
      |> insertAsBytes packet.ptype 0
      |> insertAsBytes packet.subtype 1

    let composeSpecsRequest packet =
      composeCommonHeader packet
      
    let getOpField op packet =
      match op with
        | 1,'s' | 2,'e' -> packet.blkSize :> System.Object
        | 1,'e' | 2,'s' -> packet.prl :> System.Object
        | 3, _          -> packet.nPorts :> System.Object
        | _             -> TypeUtility.nullByte :> System.Object

    let composeSpecsExchange op packet =
      let print bytes =
        printfn "Composing..."
        printfn "op: %d" op
        printfn "packet: %s" (packet.ToString())
        printfn "bytes: %A" bytes
        bytes
      composeCommonHeader packet
      |> insertAsBytes (getOpField (op,'e') packet) 2
      //|> print

    let composeSpecsSet op packet =
      composeCommonHeader packet
      |> insertAsBytes (getOpField (op,'s') packet) 2

    let composeTransferRequest packet =
      composeCommonHeader packet
      |> insertAsBytes packet.fnSize 2

    let composeTransferPrepare packet =
      composeCommonHeader packet
      |> insertAsBytes packet.fileSize 2

    let composeTransferReady packet =
      composeCommonHeader packet

    let composeTransferFinished packet =
      let print bytes =
        printfn "bytes: %A" bytes
        bytes
      composeCommonHeader packet
      //|> print

    let composeTransferChecksum packet =
      composeCommonHeader packet
      |> insertAsBytes packet.csumSize 2

  module TcpRawMessageComposer =

    open TcpRawMessageComposers

    let composeRawMessage packet op =
      if op = -1 then
        printfn "Composing: %s" (packet.ToString())
        let tuple = Tcp.parsePacketTypeFromPacket packet
        printfn "Packet type: %c %c" (Tcp.getByte(fst tuple) |> char) (Tcp.getByte(snd tuple) |> char)
      match Tcp.parsePacketTypeFromPacket packet with
      | Specification, Request -> Some (composeSpecsRequest packet)
      | Specification, Exchange -> Some (composeSpecsExchange op packet)
      | Specification, Set -> Some (composeSpecsSet op packet)
      | Transfer, Request -> Some (composeTransferRequest packet)
      | Transfer, Prepare -> Some (composeTransferPrepare packet)
      | Transfer, Ready -> Some (composeTransferReady packet)
      | Transfer, Finished -> Some (composeTransferFinished packet)
      | Transfer, Checksum -> Some (composeTransferChecksum packet)
      | _ -> None
      