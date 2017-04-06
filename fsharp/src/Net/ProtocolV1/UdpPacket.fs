namespace MUDT.Net.ProtocolV1

  open MUDT.Utilities.ConversionUtility
  open MUDT.Utilities.TypeUtility

  type UdpPacket =
    {
      seqNum: int64;
      dLen: int;
      data: byte[];
    }

    static member DefaultInstance() =
      {
        seqNum = 0L;
        dLen = 0;
        data = [||]
      }

    static member DefaultSize
      with get() = 512

    static member TryParse(bytes:byte[]) =
      if Array.length(bytes) <> UdpPacket.DefaultSize then
        None
      else
        try
          {
            seqNum = bytesToInt64 bytes.[0..7]
            dLen = bytesToInt bytes.[8..11]
            data = bytes.[12..]
          }
          |> Some
        with
        | _ -> None

    static member ToByteArray(packet:UdpPacket) =
      let copyAsBytes (x:System.Object) (offset:int) (bytes:byte[]) =
        x |> getBytes
        |> Array.iteri(fun i b -> bytes.[i+offset] <- b)
        bytes

      UdpPacket.DefaultSize
      |> nullByteArray
      |> copyAsBytes packet.seqNum 0
      |> copyAsBytes packet.dLen 8
      |> copyAsBytes packet.data 12