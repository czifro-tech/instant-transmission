namespace MUDT.Net.ProtocolV1

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

  type TcpPacketType =
    | Meta
    | Ports
    | Action
    | Checksum
    | ChecksumValidation
    | PacketDropped
    | Ping
    | User
    | Unknown

  module Tcp =


    let parsePacketType (packet:TcpPacket) =
      match packet.ptype |> char with
      | t when t = 'm' -> Meta
      | t when t = 'p' -> Ports
      | t when t = 'a' -> Action
      | t when t = 'd' -> Checksum
      | t when t = 'v' -> ChecksumValidation
      | t when t = 'x' -> PacketDropped
      | t when t = 'P' -> Ping
      | t when t = 'u' -> User
      | _ -> Unknown