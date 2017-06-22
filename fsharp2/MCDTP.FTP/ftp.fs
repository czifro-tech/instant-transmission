namespace MCDTP.FTP

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.IO.MemoryMappedFile
  open MCDTP.IO.MemoryMappedFile.Partition
  open MCDTP.Net.Sockets
  open MCDTP.Net.Protocol
  open MCDTP.Net.PacketManagement
  open System.Net

  module internal FtpSessionImpl =

    let functionName name =
      "MCDTP.FTP." + name

    let completeSocketConfig or' os port ip c
      (conf:SocketConfiguration) : SocketConfiguration =
      {
        conf with
          onReceive = or'
          onSend = os
          port = port
          ip = ip
          logger1 = c.console
          logger2 = c.network
          parser = c.parser_
          composer = c.composer_
      }

    let packetManagerConfig iS bA rA pA rMA rI fA sA iSN =
      let pm =
        if iS then
          packetManager {
            serverMode
            performOnBuffer bA
            useRecoveryAction rA
            whenPacketDroppedOrRecovered pA
            whenRetransmitMode rMA
            withRetransmitFrequency rI
            onFinished fA
            onSuccess sA
          }
        else
          packetManager {
            clientMode
            performOnBuffer bA
            useRecoveryAction rA
            whenPacketDroppedOrRecovered pA
          }
      { pm with initSeqNum = iSN }

  type internal TransferChannel =
    SocketHandle*PacketManager*PartitionHandle*TransferChannelState
  and internal TransferChannelState = Idle | Transferring | Finished | Success

  type FtpSessionState = Idle | Handshake | Transferring | Finished | Success | Error

  type FtpSession internal () =

    let mutable state = FtpSessionState.Idle
    let stateLock = Sync.createLock()

    let channelsLock = Sync.createLock()

    member __.State
      with get() = state
      and internal set(value) = state <- value
    member internal __.StateLock = stateLock
    member val internal TcpSocketHandle : SocketHandle = null with get,set
    member val internal Config : FtpConfiguration = FtpConfiguration.Instance with get,set
    member val internal MMF : MMF = MMF.Instance with get,set
    member val internal TransferChannels : Map<int,TransferChannel> = Map.empty with get,set
    member val internal Parallelism = 0 with get,set
    member val internal Udps : SocketHandle[] = Array.empty with get,set
    // used for udp connections not in use during session
    member val internal IdleUdps : SocketHandle[] = Array.empty with get,set

    member internal this.ThrowIfNotServer actionName =
      if not <| this.Config.isServer then
        failwithf "This is a server action: %s" actionName

    member internal this.ThrowIfNotClient actionName =
      if this.Config.isServer then
        failwithf "This is a client action: %s" actionName


    member internal this.OnTcpReceive _ (msg:obj*byte[]) =
      let funcName = FtpSessionImpl.functionName "FtpSession.OnTcpReceive"
      let logger = this.TcpSocketHandle.ConsoleLogger
      let packet,payload = msg
      match packet with
      | :? TcpPacket as tcp ->
        match Tcp.parsePacketTypeFromPacket tcp with
        | Specification,x ->
          match x with
          | Request ->
            this.ThrowIfNotClient "Specification Request"
            logger.LogWith(LogLevel.Info,funcName,"Processing Specification Request")
            let portsAsBytes =
              this.Udps
              |> Array.collect(fun u ->
                Conversion.intToBytes u.LocalEP.Port
              )
            let packet =
              { TcpPacket.DefaultInstance with
                  ptype = Tcp.getByte TcpPacketType.Specification
                  subtype = Tcp.getByte TcpPacketSubType.Exchange
                  nPorts = Array.length this.Udps }
            this.TcpSocketHandle.SubmitMessage(packet,portsAsBytes)
          | Exchange ->
            this.ThrowIfNotServer "Specification Exchange"
            logger.LogWith(LogLevel.Info,funcName,"Processing Specification Exchange")
            let portSize = sizeof<System.Int32>
            let block s e =
              payload.[s..e]
            let ports =
              let convertAt i =
                Conversion.bytesToInt (block (i*portSize) (((i*portSize)+portSize)-1))
              [| for i in 0..tcp.nPorts-1 -> convertAt i |]
            this.Parallelism <- min tcp.nPorts this.Parallelism
            let ports = ports |> Array.take this.Parallelism
            this.SetupServerUdps ports
            let packet =
              { TcpPacket.DefaultInstance with
                  ptype = Tcp.getByte TcpPacketType.Specification
                  subtype = Tcp.getByte TcpPacketSubType.Set
                  nPorts = Array.length ports }
            let portsAsBytes =
              this.Udps
              |> Array.collect(fun u ->
                Conversion.intToBytes u.RemoteEP.Port
              )
            this.TcpSocketHandle.SubmitMessage(packet,portsAsBytes)
            this.State <- FtpSessionState.Idle
          | Set ->
            this.ThrowIfNotClient "Specification Set"
            logger.LogWith(LogLevel.Info,funcName,"Processing Specification Set")
            let portSize = sizeof<System.Int32>
            let block s e =
              payload.[s..e]
            let ports =
              let convertAt i =
                Conversion.bytesToInt (block (i*portSize) (((i*portSize)+portSize)-1))
              [| for i in 0..tcp.nPorts-1 -> convertAt i |]
            let udps =
              this.Udps
              |> Array.filter(fun u -> ports |> Array.exists(fun port -> port = u.LocalEP.Port))
              |> Array.sortBy(fun u -> u.LocalEP.Port)
            let idleUdps =
              this.Udps
              |> Array.filter(fun u -> (ports |> Array.exists(fun port -> port = u.LocalEP.Port)) |> not)
              |> Array.sortBy(fun u -> u.LocalEP.Port)
            this.Parallelism <- Array.length udps
            this.Udps <- udps
            this.IdleUdps <- idleUdps
            this.State <- FtpSessionState.Idle
          | _ -> failwith "Unknown packet"
        | Transfer,x ->
          match x with
          | Request ->
            this.ThrowIfNotServer "Transfer Request"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer Request")
            this.PrepareForTransfer()
            let packet =
              { TcpPacket.DefaultInstance with
                  ptype = Tcp.getByte TcpPacketType.Transfer
                  subtype = Tcp.getByte TcpPacketSubType.Prepare }
            this.TcpSocketHandle.SubmitMessage(packet,Array.empty)
          | Prepare ->
            this.ThrowIfNotClient "Transfer Prepare"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer Prepare")
            this.PrepareForTransfer()
            this.BeginTransmission()
            let packet =
              { TcpPacket.DefaultInstance with
                  ptype = Tcp.getByte TcpPacketType.Transfer
                  subtype = Tcp.getByte TcpPacketSubType.Ready }
            this.TcpSocketHandle.SubmitMessage(packet,Array.empty)
            logger.LogWith(LogLevel.Info,funcName,"Started Transmission Process")
            this.State <- FtpSessionState.Transferring
          | Ready ->
            this.ThrowIfNotServer "Transfer Ready"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer Ready")
            this.BeginTransmission()
          | TcpPacketSubType.Finished ->
            this.ThrowIfNotServer "Transfer Finished"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer Finished")
            this.Finished tcp.port
          | PacketLoss ->
            this.ThrowIfNotServer "Transfer PacketLoss"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer PacketLoss")
            this.ReportPacketLoss tcp.port tcp.seqNum
          | PacketAck ->
            this.ThrowIfNotServer "Transfer PacketAck"
            logger.LogWith(LogLevel.Info,funcName,"Processing Transfer PacketAck")
            this.AcknowledgePacket tcp.port tcp.seqNum
          | TcpPacketSubType.Success ->
            this.Success tcp.port
          | _ -> failwith "Unknown packet"
        | TcpPacketType.Unknown,_ -> failwith "Unknown packet"
      | _ -> failwith "Expected a TcpPacket"
      this.TcpSocketHandle.TryAsyncReceive TcpPacket.DefaultSize

    member internal this.OnTcpSend _ (i:int) = ()

    member internal this.UpdateChannel port channel =
      channelsLock
      |> Sync.write(fun () ->
        this.TransferChannels <-
          this.TransferChannels
          |> Map.remove port
          |> Map.add port channel
      )

    member internal this.TrySyncStates tcState fsState =
      let funcName = FtpSessionImpl.functionName "FtpSession.TrySyncStates"
      this.StateLock
      |> Sync.write(fun () ->
        let allChannelsFinished =
          let len =
            this.TransferChannels
            |> Map.toArray
            |> Array.length
          let count =
            this.TransferChannels
            |> Map.toArray
            |> Array.map(fun e ->
              let _,c = e
              c
            )
            |> Array.map(fun channel ->
              let _,_,_,tcState' = channel
              tcState' = tcState
            )
            |> Array.filter(fun b -> b)
            |> Array.length
          len = count
        if allChannelsFinished then
          this.State <- fsState
          if this.State = FtpSessionState.Finished then
            let msg = "FTP Main Transfer Finished"
            this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,msg)
          elif this.State = FtpSessionState.Success then
            let msg = "FTP Transfer Succeeded"
            this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,msg)
      )

    member internal this.OnUdpReceive port (msg:obj*_) =
      let funcName = FtpSessionImpl.functionName "FtpSession.OnUdpReceive"
      let msg' = sprintf "Received on port: %d" port
      this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,msg')
      Async.RunSynchronously (Async.Sleep 1000)
      try
      let sock,pm,part,tcState = this.TransferChannels.[port]
      let packet,_ = msg
      match packet with
      | :? UdpPacket as udp -> pm.AddPacket(udp)
      | _ -> ()
      sock.TryAsyncReceive UdpPacket.DefaultSize
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel
      with
      | ex ->
        let msg = sprintf "Port: %d, Exception: %A" port ex
        failwith msg

    member internal this.OnUdpSend port _ =
      let funcName = FtpSessionImpl.functionName "FtpSession.OnUdpSend"
      let sock,pm,part,tcState = this.TransferChannels.[port]
      let rec tryPullPacket() =
        match pm.TryPullPacket() with
        | Some packet -> Some packet
        | None ->
          // pm's final state on server is retransmit mode
          //  this is a guaranteed base case
          if pm.HasSwitchedToRetransmitMode() then
            this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,"Switched to retransmit mode")
            None
          else
            // in case we caught the system at a global replenish
            //  try again
            tryPullPacket()
      match tryPullPacket() with
      | Some packet -> sock.SubmitMessage(packet,Array.empty)
      | _ -> ()
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel

    member internal this.PMOnReplenish port count =
      let funcName = FtpSessionImpl.functionName "FtpSession.PMOnReplenish"
      let sock,pm,part,tcState = this.TransferChannels.[port]
      let dataOp = part.ReadBytes count
      let channel = sock,pm,part,tcState
      let msg = sprintf "Replenishing for: %A, got: %A" channel dataOp
      sock.ConsoleLogger.LogWith(LogLevel.None,funcName,msg)
      this.UpdateChannel port channel
      match dataOp with
      | Some (pos,data) -> pos,data
      | _ -> -1L,Array.empty

    member internal this.PMOnFlush port data forceFlush =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      part.WriteBytes data |> ignore
      if forceFlush then
        part.TryFlush forceFlush
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel

    member internal this.PMRetransmitMode port (packet:UdpPacket) =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      sock.SubmitMessage (packet,Array.empty)
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel

    member internal this.PMOnFetch port (pos:int64) =
      let size = UdpPacket.PayloadSize
      let sock,pm,part,tcState = this.TransferChannels.[port]
      let data = part.ReadBytesAt(pos,size)
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel
      data

    member internal this.PMOnWrite port data pos =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      if not <| part.TryAmend(pos,data) then
        part.WriteBytesAt(pos,data)
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel

    member internal this.PMReportDroppedPacket port (seqNum:int64) =
      let packet =
        { TcpPacket.DefaultInstance with
            ptype = Tcp.getByte TcpPacketType.Transfer
            subtype = Tcp.getByte TcpPacketSubType.PacketLoss
            seqNum = seqNum
            port = port }
      this.TcpSocketHandle.SubmitMessage (packet,Array.empty)

    member internal this.PMAckPacket port (seqNum:int64) =
      let packet =
        { TcpPacket.DefaultInstance with
            ptype = Tcp.getByte TcpPacketType.Transfer
            subtype = Tcp.getByte TcpPacketSubType.PacketAck
            seqNum = seqNum
            port = port }
      this.TcpSocketHandle.SubmitMessage (packet,Array.empty)

    member internal this.PMOnFinished port _ =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      let channel = sock,pm,part,TransferChannelState.Finished
      if not this.Config.isServer then
        let packet =
          { TcpPacket.DefaultInstance with
              ptype = Tcp.getByte TcpPacketType.Transfer
              subtype = Tcp.getByte TcpPacketSubType.Finished
              port = port }
        this.TcpSocketHandle.SubmitMessage(packet,Array.empty)
      this.UpdateChannel port channel
      this.TrySyncStates TransferChannelState.Finished FtpSessionState.Finished

    member internal this.PMOnSuccess port _ =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      sock.NetworkLogger.SuspendThroughputLogging(LogLevel.Info)
      let channel = sock,pm,part,TransferChannelState.Success
      this.UpdateChannel port channel
      this.TrySyncStates TransferChannelState.Success FtpSessionState.Success

    member internal this.CompletePMConfig port iS iSN =
      let rdp = this.PMReportDroppedPacket port
      let ap = this.PMAckPacket port
      let bA =
        if iS then
          PMAction.Replenish(50,(this.PMOnReplenish port))
        else
          PMAction.Flush(50,(this.PMOnFlush port))
      let rA =
        if iS then
          PMAction.Fetch(this.PMOnFetch port)
        else
          PMAction.Write(this.PMOnWrite port)
      let pA =
        if iS then PMAction.NoAction
        else PMAction.PacketAction(rdp,ap)
      let rMA =
        if iS then
          PMAction.RetransmitModeAction(this.PMRetransmitMode port)
        else PMAction.NoAction
      let rI = 10000
      let fA = PMAction.FinishedAction(this.PMOnFinished port)
        // if not iS then
        //   PMAction.FinishedAction(this.PMOnFinished port)
        // else PMAction.NoAction
      let sA =
        if iS then
          PMAction.SuccessAction(this.PMOnSuccess port)
        else PMAction.NoAction
      FtpSessionImpl.packetManagerConfig iS bA rA pA rMA rI fA sA iSN

    member internal this.PrepareForTransfer() =
      let funcName = FtpSessionImpl.functionName "FtpSession.PrepareForTransfer"
      this.MMF <- MMF.ofConfig this.Config.mmf this.Parallelism
      let partitions =
        this.MMF.partitions
        |> Array.sortBy(fun p -> p.StartPosition)
      if this.Config.isServer then
        partitions
        |> Array.iter(fun p ->
          if not <| p.InitializeBuffer() then
            failwith "Partition failed to initialize"
        )
      let udps =
        this.Udps
        |> Array.sortBy(fun u ->
          if this.Config.isServer then
            u.RemoteEP.Port
          else u.LocalEP.Port
        )
      let ports =
        this.Udps
        |> Array.map(fun u ->
          if this.Config.isServer then
            u.RemoteEP.Port
          else u.LocalEP.Port
        )
      let managers =
        partitions
        |> Array.mapi(fun i p ->
          let u = this.Udps.[i]
          let logger = u.NetworkLogger |> Logger.NetworkLogger
          let port = ports.[i]
          let isServer = this.Config.isServer
          let conf = this.CompletePMConfig port isServer p.StartPosition
          let conf = { conf with logger = logger }
          let manager = PacketManager(conf)
          manager.EndPacket <-
            Some { UdpPacket.EndInstance with
                     seqNum = p.EndPosition }
          manager
        )
      let channels =
        ports
        |> Array.mapi(fun i p ->
          let channel = udps.[i],managers.[i],partitions.[i],TransferChannelState.Idle
          p,channel
        )
        |> Map.ofArray
      this.TransferChannels <- channels
      if this.Config.isServer then
        channels
        |> Map.iter(fun k c ->
          let _,pm,_,_ = c
          pm.InitializeBuffer()
        )
      let msg = sprintf "Prepared channels: %A" this.TransferChannels
      this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,msg)

    member internal this.BeginTransmission() =
      let funcName = FtpSessionImpl.functionName "FtpSession.BeginTransmission"
      let logData = ("Starting up channels",this.TransferChannels)
      this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,logData)
      this.TransferChannels <-
        this.TransferChannels
        |> Map.map(fun k channel ->
          let sock,pm,part,_ = channel
          let actionData = ("Trying to pull packet")
          let successData = ("Successfully initiated channel")
          let errorData = ("Failed to initiate channel")
          let rec tryPullPacket() =
            // pm's final state on server is retransmit mode
            //  this is a guaranteed base case
            if pm.HasSwitchedToRetransmitMode() then
              None
            else
              //sock.ConsoleLogger.LogWith(LogLevel.Info,funcName,actionData)
              match pm.TryPullPacket() with
              | Some packet -> Some packet
              | None ->
                // in case we caught the system at a global replenish
                //  try again
                tryPullPacket()
          let tcState =
            if this.Config.isServer then
              match tryPullPacket() with
              | Some packet -> 
                sock.SubmitMessage(packet,Array.empty)
                sock.ConsoleLogger.LogWith(LogLevel.Info,funcName,successData)
                TransferChannelState.Transferring
              | _ ->
                sock.ConsoleLogger.LogWith(LogLevel.Info,funcName,errorData)
                TransferChannelState.Idle
            else
              sock.TryAsyncReceive UdpPacket.DefaultSize
              TransferChannelState.Transferring
          sock,pm,part,tcState
        )
      this.TrySyncStates TransferChannelState.Transferring FtpSessionState.Transferring
      if this.State = FtpSessionState.Transferring then
        this.TcpSocketHandle.ConsoleLogger.LogWith(LogLevel.Info,funcName,"Started Transmission Process")

    member internal this.ReportPacketLoss port seqNum =
      let sock,pm,part,tcState = this.TransferChannels.[port]
      pm.ReportMissingPacket seqNum
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel

    member internal this.AcknowledgePacket port seqNum =
      try
      let sock,pm,part,tcState = this.TransferChannels.[port]
      pm.AcknowledgePacket seqNum
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel
      with
      | ex ->
        let msg = sprintf "Channels: %A, Port: %d, Exception: %A" this.TransferChannels port ex
        failwith msg

    member internal this.Finished port =
      let funcName = FtpSessionImpl.functionName "FtpSession.Finished"
      let sock,pm,part,tcState = this.TransferChannels.[port]
      sock.NetworkLogger.LogPlainMessage(LogLevel.Info,"Channel finished main transmission")
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel
      this.TrySyncStates TransferChannelState.Finished FtpSessionState.Finished

    member internal this.Success port =
      let funcName = FtpSessionImpl.functionName "FtpSession.Success"
      let sock,pm,part,tcState = this.TransferChannels.[port]
      sock.NetworkLogger.LogPlainMessage(LogLevel.Info,"Channel succeeded")
      sock.NetworkLogger.SuspendThroughputLogging(LogLevel.Info)
      let channel = sock,pm,part,tcState
      this.UpdateChannel port channel
      this.TrySyncStates TransferChannelState.Success FtpSessionState.Success

    member internal this.SetupUdps ports ip
      (toSock:SocketConfiguration->SocketHandle) =
      let ports = ports |> Array.sort
      let udps =
        ports
        |> Array.map(fun port ->
          let or' = this.OnUdpReceive
          let os = this.OnUdpSend
          let c =
            let parser (bytes:byte[]) =
              (bytes |> Udp.Parser.tryParse |> Option.get) :> obj
            let composer (o:obj) =
              (o :?> UdpPacket) |> Udp.Composer.tryCompose |> Option.get
            { this.Config with
                parser_ = parser
                composer_ = composer }
          let conf = FtpSessionImpl.completeSocketConfig or' os port ip c c.udp
          toSock conf
        )
      this.Udps <- udps

    member internal this.SetupClientUdps() =
      let ports =
        [| for i in 0..this.Parallelism-1 -> 0 |]
      this.SetupUdps ports IPAddress.Any SocketHandle.bindUdp

    member internal this.SetupServerUdps ports =
      let ep = this.TcpSocketHandle.Sock.RemoteEndPoint :?> IPEndPoint
      let ip = ep.Address
      this.SetupUdps ports ip SocketHandle.connectUdp

    member this.BeginHandshake isServer =
      if isServer then
        this.ThrowIfNotServer "Server Begin Handshake"
        let packet =
          { TcpPacket.DefaultInstance with
              ptype = Tcp.getByte TcpPacketType.Specification
              subtype = Tcp.getByte TcpPacketSubType.Request }
        this.TcpSocketHandle.SubmitMessage(packet,Array.empty)
      this.TcpSocketHandle.TryAsyncReceive TcpPacket.DefaultSize
      this.State <- FtpSessionState.Handshake
      Async.RunSynchronously (Async.Sleep 2000) |> ignore

    member this.RequestTransfer() =
      this.ThrowIfNotClient "Request Transfer"
      let packet =
        { TcpPacket.DefaultInstance with
            ptype = Tcp.getByte TcpPacketType.Transfer
            subtype = Tcp.getByte TcpPacketSubType.Request }
      this.TcpSocketHandle.SubmitMessage(packet,Array.empty)
      this.State <- FtpSessionState.Transferring

  module Ftp =

    let mutable private listener = null

    let useListener (listener':SocketHandle) =
      listener <- listener'

    let private newSession (config:FtpConfiguration)
      (getSocketHandle:SocketConfiguration->SocketHandle) =
      let session = FtpSession()
      let config =
        let or' = session.OnTcpReceive
        let os = session.OnTcpSend
        let ip = config.tcp.ip
        let port = config.tcp.port
        let c = config
        {
          config with
            tcp = FtpSessionImpl.completeSocketConfig or' os port ip c c.tcp
        }
      let socket = getSocketHandle config.tcp
      session.TcpSocketHandle <- socket
      session.Config <- config
      session.Parallelism <- config.parallelism
      if not config.isServer then
        session.SetupClientUdps()
      session

    let acceptNewSessionFromConfig (config:FtpConfiguration) =
      newSession config listener.AcceptWithConfig

    let connectWithConfig (config:FtpConfiguration) =
      newSession config SocketHandle.connectTcp