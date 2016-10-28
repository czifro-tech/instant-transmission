namespace MUDT.Net.Protocol

  type UdpPacket =
    {
      seqNum: int64;
      dLen: int;
      data: byte[];
    }

    static member T() =
      {
        seqNum = 0L;
        dLen = 0;
        data = [||]
      }