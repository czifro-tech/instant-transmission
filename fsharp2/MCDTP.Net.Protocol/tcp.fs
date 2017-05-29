namespace MCDTP.Net.Protocol

  open MCDTP.Logging
  open MCDTP.Utility

  type TcpPacket =
    {
      ptype        :  byte;
      subtype      :  byte;
      prl          :  byte;
      memLimit     :  int64;
      nPorts       :  int;
      blkSize      :  int64;
      cmd          :  int;
      fnSize       :  int;
      fileSize     :  int64;
    }

    static member DefaultInstance =
      {
        ptype        =  Type.nullByte;
        subtype      =  Type.nullByte;
        prl          =  Type.nullByte;
        memLimit     =  0L;
        nPorts       =  0;
        blkSize      =  0L;
        cmd          =  0;
        fnSize       =  0;
        fileSize     =  0L;
      }

    static member DefaultSize = 15

  type TcpPacketType =
    | Specification
    | Transfer
    | Unknown

  type TcpPacketSubType =
    // Specification
    | Request
    | Exchange1 | Exchange2 | Exchange3
    | Set1 | Set2 | Set3
    // Transfer
    | Prepare
    | Ready
    | Finished
    | Checksum
    | Success
    // Specification, Transfer, Unknown
    | Unknown

  (* Tcp
  
     This module handles the parsing and composing of data sent
     by MCDTP over a TCP socket. *)
  [<RequireQualifiedAccess>]
  module Tcp =

    open InternalLogging

    let getByte (enum:System.Object) =
      let byte =
        match enum with
        | :? TcpPacketType as ptype     ->
          match ptype with
          | TcpPacketType.Specification   -> byte 's'
          | TcpPacketType.Transfer        -> byte 't'
          | TcpPacketType.Unknown         -> byte 'u'
        | :? TcpPacketSubType as subtype  ->
          match subtype with
          | TcpPacketSubType.Request        -> byte 'r'
          | TcpPacketSubType.Exchange1      -> byte 'B'
          | TcpPacketSubType.Exchange2      -> byte 'P'
          | TcpPacketSubType.Exchange3      -> byte 'N'
          | TcpPacketSubType.Set1           -> byte 'b'
          | TcpPacketSubType.Set2
          | TcpPacketSubType.Prepare        -> byte 'p'
          | TcpPacketSubType.Set3           -> byte 'n'
          | TcpPacketSubType.Ready          -> byte 'R'
          | TcpPacketSubType.Finished       -> byte 'f'
          | TcpPacketSubType.Checksum       -> byte 'c'
          | TcpPacketSubType.Success        -> byte 'S'
          | TcpPacketSubType.Unknown        -> byte 'u'
        | _                               -> Type.nullByte
      internalLogger.LogWith(LogLevel.Info,"Tcp.getbyte",(enum,byte))
      byte

    let private parsePacketType (type':char*char) =
      match type' with
      | 's',x ->
        let ptype = Specification
        let subtype =
          match x with
          | 'r' -> Request
          | 'B' -> Exchange1
          | 'P' -> Exchange2
          | 'N' -> Exchange3
          | 'b' -> Set1
          | 'p' -> Set2
          | 'n' -> Set3
          | _   -> Unknown
        if subtype = Unknown then
          TcpPacketType.Unknown, TcpPacketSubType.Unknown
        else ptype, subtype
      | 't',x ->
        let ptype = Transfer
        let subtype =
          match x with
          | 'r' -> Request
          | 'p' -> Prepare
          | 'R' -> Ready
          | 'f' -> Finished
          | 'c' -> Checksum
          | 's' -> Success
          | _   -> Unknown
        if subtype = Unknown then
          TcpPacketType.Unknown, TcpPacketSubType.Unknown
        else ptype, subtype
      | _     -> TcpPacketType.Unknown, TcpPacketSubType.Unknown

    let parsePacketTypeFromPacket (packet:TcpPacket) =
      parsePacketType (packet.ptype |> char, packet.subtype |> char)

    let parsePacketTypeFromRaw (bytes:byte[]) =
      parsePacketType (bytes.[0] |> char, bytes.[1] |> char)

    (* Tcp.ParserImpl
    
       The parsing system for the Tcp module is complex. Splitting
       it into two modules seems to be the cleanest option. This
       is the internal implementation. *)
    module internal ParserImpl =

      let private parseCommonHeader (bytes:byte[]) =
        let packet =
          { TcpPacket.DefaultInstance with
              ptype = bytes.[0];
              subtype = bytes.[1] }
        internalLogger.LogWith(LogLevel.Info,"Tcp.ParserImpl.parseCommonHeader",(bytes,packet))
        Some packet

      let parseSpecsPacket bytes =
        let packetOption = parseCommonHeader bytes
        internalLogger.LogWith(LogLevel.Info,"Tcp.ParserImpl.parseSpecsPacket_1",(bytes,packetOption))
        let packetOption =
          match packetOption with
          | Some packet ->
            match parsePacketTypeFromPacket packet with
            | Specification, x ->
              match x with
              | Request          -> packetOption
              | Exchange1 | Set1 -> Some { packet with blkSize = Conversion.bytesToInt64 bytes.[2..9] }
              | Exchange2 | Set2 -> Some { packet with prl = bytes.[2] }
              | Exchange3 | Set3 -> Some { packet with nPorts = Conversion.bytesToInt bytes.[2..5] }
              | _ -> None
            | _ -> None
          | _ -> None
        internalLogger.LogWith(LogLevel.Info,"Tcp.ParserImpl.parseSpecsPacket_2",(bytes,packetOption))
        packetOption

      let parseTransferPacket bytes =
        let packetOption = parseCommonHeader bytes
        internalLogger.LogWith(LogLevel.Info,"Tcp.ParserImpl.parseTransferPacket_1",(bytes,packetOption))
        let packetOption =
          match packetOption with
          | Some packet ->
            match parsePacketTypeFromPacket packet with
            | Transfer, x ->
              match x with
              | Request  -> Some { packet with fnSize = Conversion.bytesToInt bytes.[2..5] }
              | Prepare  -> Some { packet with fileSize = Conversion.bytesToInt64 bytes.[2..9] }
              | Ready
              | Finished -> packetOption
              | _ -> None
            | _ -> None
          | _ -> None
        internalLogger.LogWith(LogLevel.Info,"Tcp.ParserImpl.parseTransferPacket_2",(bytes,packetOption))
        packetOption

    (* Tcp.Parser
    
       This is the public module in the two part parsing system. *)
    module Parser =

      open ParserImpl

      let tryParse bytes =
        try
        let parserOption =
          match parsePacketTypeFromRaw bytes with
          | Specification, _ -> Some parseSpecsPacket
          | Transfer, _ -> Some parseTransferPacket
          | _ -> None
        match parserOption with
        | Some parser ->
          let packetOption = parser bytes
          internalLogger.LogWith(LogLevel.Info,"Tcp.Parser.tryParse",(bytes,packetOption))
          packetOption
        | _ ->
          internalLogger.Log("[TCP] Failed to parse",bytes)
          None
        with
        | ex ->
          internalLogger.Log("[TCP] Parser threw exception", ex)
          None

    (* Tcp.ComposerImpl
    
       The composing system for the Tcp module is complex. Splitting
       it into two modules seems to be the cleanest option. This
       is the internal implementation. *)
    module internal ComposerImpl =

      let private newRawMessage() = Type.nullByteArray TcpPacket.DefaultSize

      let private insertAsBytes (x:System.Object) (offset:int) (bytes:byte[]) =
        x
        |> Conversion.getBytes
        |> Array.iteri(fun i b -> bytes.[i+offset] <- b)
        bytes

      let private composeCommonHeader (packet:TcpPacket) =
        let bytes =
          newRawMessage()
          |> insertAsBytes packet.ptype 0
          |> insertAsBytes packet.subtype 1
        internalLogger.LogWith(LogLevel.Debug,"Tcp.ComposerImpl.composeCommonHeader",(packet,bytes))
        Some bytes

      let composeSpecsPacket packet =
        let bytesOption = composeCommonHeader packet
        internalLogger.LogWith(LogLevel.Info,"Tcp.ComposerImpl.composeSpecsPacket_1",(packet,bytesOption))
        let bytesOption =
          match bytesOption with
          | Some bytes ->
            match parsePacketTypeFromPacket packet with
            | Specification, x ->
              match x with
              | Request          -> bytesOption
              | Exchange1 | Set1 -> Some (bytes |> insertAsBytes packet.blkSize 2)
              | Exchange2 | Set2 -> Some (bytes |> insertAsBytes packet.prl 2)
              | Exchange3 | Set3 -> Some (bytes |> insertAsBytes packet.nPorts 2)
              | _ -> None
            | _ -> None
          | _ -> None
        internalLogger.LogWith(LogLevel.Debug,"Tcp.ComposerImpl.composeSpecsPacket_2",(packet,bytesOption))
        bytesOption

      let composeTransferPacket packet =
        let bytesOption = composeCommonHeader packet
        internalLogger.LogWith(LogLevel.Info,"Tcp.ComposerImpl.composeTransferPacket_1",(packet,bytesOption))
        let bytesOption =
          match bytesOption with
          | Some bytes ->
            match parsePacketTypeFromPacket packet with
            | Transfer, x ->
              match x with
              | Request  -> Some (bytes |> insertAsBytes packet.fnSize 2)
              | Prepare  -> Some (bytes |> insertAsBytes packet.fileSize 2)
              | Ready
              | Finished -> bytesOption
              | _ -> None
            | _ -> None
          | _ -> None
        internalLogger.LogWith(LogLevel.Debug,"Tcp.ComposerImpl.composeTransferPacket_2",(packet,bytesOption))
        bytesOption

    (* Tcp.Composer
    
       This is the public module in the two part composing system. *)
    module Composer =

      open ComposerImpl

      let tryCompose packet =
        try
        let composerOption =
          match parsePacketTypeFromPacket packet with
          | Specification, _ -> Some composeSpecsPacket
          | Transfer, _ -> Some composeTransferPacket
          | _ -> None
        match composerOption with
        | Some composer ->
          let bytesOption = composer packet
          internalLogger.LogWith(LogLevel.Debug,"Tcp.Composer.tryCompose",(packet,bytesOption))
          bytesOption
        | _ ->
          internalLogger.Log("[TCP] Failed to compose",packet)
          None
        with
        | ex ->
          internalLogger.Log("[TCP] Composer threw exception",ex)
          None