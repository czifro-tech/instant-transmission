namespace MUDT.Net.Protocol

  open MUDT.Utilities.TypeUtility

  type TcpPacket = 
    {
      ptype: byte;
      fileSize: int64;
      fnLen: int;
      numPorts: int;
      action: byte;
      pNum: int;
      dLen: int;
      result: byte;
      seqNum: int64;
      timestamp: int64;
    }
      
    static member DefaultInstance() =
      {
        ptype = nullByte;
        fileSize = 0L;
        fnLen = 0;
        numPorts = 0;
        action = nullByte;
        pNum = 0;
        dLen = 0;
        result = nullByte;
        seqNum = 0L;
        timestamp = 0L;
      }

    static member DefaultSize 
      with get() = 15

  module TcpPacketType =
    let Meta = 'm'
    let Ports = 'p'
    let Act = 'a'
    let CSum = 'd'
    let CSVal = 'v'
    let PDrop = 'x'
    let Ping = 'P'