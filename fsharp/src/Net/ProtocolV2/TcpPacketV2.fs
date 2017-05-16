namespace MUDT.Net.Protocol

  open MUDT.Utilities.TypeUtility

  type TcpPacketV2 =
    {
      ptype        :  byte;
      subtype      :  byte;
      prl          :  byte;
      memLimit     :  int64;
      nPorts       :  int;
      blkSize      :  int64;
      cmd          :  int;
      csumSize     :  int64;
      fnSize       :  int;
      fileSize     :  int64;
    }

    static member DefaultInstance =
      {
        ptype        =  nullByte;
        subtype      =  nullByte;
        prl          =  nullByte;
        memLimit     =  0L;
        nPorts       =  0;
        blkSize      =  0L;
        cmd          =  0;
        csumSize     =  0L;
        fnSize       =  0;
        fileSize     =  0L;
      }

    static member DefaultSize = 15

  type TcpPacketV2Type =
    | Specification
    | Transfer
    | Unknown

  type TcpPacketSubType =
    // Specification
    | Request
    | Exchange
    | Set
    // Transfer
    | Prepare
    | Ready
    | Finished
    | Checksum
    | Success
    // Specification, Transfer, Unknown
    | Unknown

  module Tcp =

    let getByte (enum:System.Object) =
      match enum with
      | :? TcpPacketV2Type as ptype ->
        match ptype with
        | TcpPacketV2Type.Specification -> byte 's'
        | TcpPacketV2Type.Transfer -> byte 't'
        | TcpPacketV2Type.Unknown -> byte 'u'
      | :? TcpPacketSubType as subtype ->
        match subtype with
        | TcpPacketSubType.Request -> byte 'r'
        | TcpPacketSubType.Exchange -> byte 'e'
        | TcpPacketSubType.Set -> byte 's'
        | TcpPacketSubType.Prepare -> byte 'p'
        | TcpPacketSubType.Ready -> byte 'R'
        | TcpPacketSubType.Finished -> byte 'f'
        | TcpPacketSubType.Checksum -> byte 'c'
        | TcpPacketSubType.Success -> byte 'S'
        | TcpPacketSubType.Unknown -> byte 'u'
      | _ -> nullByte

    let private parsePacketType (type':char*char) =
      match type' with
      | 's','r' -> Specification, Request
      | 's','e' -> Specification, Exchange
      | 's','s' -> Specification, Set
      | 't','r' -> Transfer, Request
      | 't','p' -> Transfer, Prepare
      | 't','R' -> Transfer, Ready
      | 't','f' -> Transfer, Finished
      | 't','c' -> Transfer, Checksum
      | 't','S' -> Transfer, Success
      | _       -> TcpPacketV2Type.Unknown, TcpPacketSubType.Unknown

    let parsePacketTypeFromPacket (packet:TcpPacketV2) =
      parsePacketType (packet.ptype |> char, packet.subtype |> char)

    let parsePacketTypeFromRaw (bytes:byte[]) =
      parsePacketType (bytes.[0] |> char, bytes.[1] |> char)