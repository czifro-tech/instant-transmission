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
      payloadSize  :  int64;
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
        payloadSize  =  0L;
        fnSize       =  0;
        fileSize     =  0L;
      }

    static member DefaultSize = 15

  type TcpPacketType =
    | Specification
    | Command
    | Transfer
    | Unknown

  type TcpPacketSubType =
    | Request
    | Exchange
    | Set
    | Output
    | Prepare
    | Ready
    | Unknown

  module Tcp =

    let getByte (enum:System.Object) =
      match enum with
      | :? TcpPacketType as ptype ->
        match ptype with
        | TcpPacketType.Specification -> byte 's'
        | TcpPacketType.Command -> byte 'c'
        | TcpPacketType.Transfer -> byte 't'
        | TcpPacketType.Unknown -> byte 'u'
      | :? TcpPacketSubType as subtype ->
        match subtype with
        | TcpPacketSubType.Request -> byte 'r'
        | TcpPacketSubType.Exchange -> byte 'e'
        | TcpPacketSubType.Set -> byte 's'
        | TcpPacketSubType.Output -> byte 'o'
        | TcpPacketSubType.Prepare -> byte 'p'
        | TcpPacketSubType.Ready -> byte 'R'
        | TcpPacketSubType.Unknown -> byte 'u'
      | _ -> nullByte

    let private parsePacketType (type':char*char) =
      match type' with
      | 's','r' -> Specification, Request
      | 's','e' -> Specification, Exchange
      | 's','s' -> Specification, Set
      | 'c','o' -> Command, Output
      | 'c', _  -> Command, Unknown
      | 't','r' -> Transfer, Request
      | 't','p' -> Transfer, Prepare
      | 't','R' -> Transfer, Ready
      | _       -> TcpPacketType.Unknown, TcpPacketSubType.Unknown

    let parsePacketTypeFromPacket (packet:TcpPacketV2) =
      parsePacketType (packet.ptype |> char, packet.subtype |> char)

    let parsePacketTypeFromRaw (bytes:byte[]) =
      parsePacketType (bytes.[0] |> char, bytes.[1] |> char)