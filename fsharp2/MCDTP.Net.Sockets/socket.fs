namespace MCDTP.Net.Sockets

  open System
  open System.Net
  open System.Net.Sockets
  open MCDTP.Net.Protocol
  open MCDTP.Logging
  open MCDTP.Utility

  module internal SocketHandleImpl =

    let functionName name =
      "MCDTP.Net.Sockets." + name

    type Args = SocketAsyncEventArgs

    let private newSocket addressFamily socketType protocolType =
      new Socket(addressFamily,socketType,protocolType)

    let newV4Socket socketType protocolType =
      newSocket AddressFamily.InterNetwork socketType protocolType

    let newV4TcpSocket() =
      newV4Socket SocketType.Stream ProtocolType.Tcp

    let newV4UdpSocket() =
      newV4Socket SocketType.Dgram ProtocolType.Udp

    let bind port (socket:Socket) =
      socket.Bind(IPEndPoint(IPAddress.Any, port))

    let listen (socket:Socket) =
      socket.Listen(int SocketOptionName.MaxConnections)
    
    let connect ip port (socket:Socket) =
      let ep = IPEndPoint((ip:IPAddress),port)
      socket.Connect(ep)

    let private payloadSize (packet:TcpPacket) =
      let size = packet.nPorts
      let size = if size > 0 then size else packet.csumSize
      let size = if size > 0 then size else -1
      size

    let private tryGetPayload size (sock:Socket) =
      if size > -1 then
        let payload = Type.nullByteArray size
        let error = SocketError.Success
        ignore <| sock.Receive(payload, 0, size, SocketFlags.None, ref error)
        match error with
        | SocketError.Success -> payload
        | _ -> [||]
      else [||]

    let private asyncDo (op:Args->bool) (callback:Args->unit) (args:Args) =
      async {
        args.add_Completed(EventHandler<_>(fun _ -> callback))
        if not <| op args then callback args
      }

    let asyncReceive size (parser:byte[]->obj) (onReceive:(obj*byte[])->unit)
      (onError:exn->unit) (consoleLogger:ConsoleLogger) (sock:Socket) =
      async {
        let funcName = functionName "SocketHandleImpl.asyncReceive"
        let args' = new Args()
        args'.SetBuffer((Type.nullByteArray size),0,size)
        let callback (args:Args) =
          match args.SocketError with
          | SocketError.Success ->
            let bytes = args.Buffer
            args.Dispose()
            let packet = parser bytes
            // in case the packet is followed by a payload
            //  pull that from the stream
            // Does not apply to UdpPacket(s)
            let payloadLength =
              match packet with
              | :? TcpPacket as t -> payloadSize t
              | _ -> -1
            let payload = tryGetPayload payloadLength sock
            consoleLogger.LogWith(LogLevel.Info,funcName,(size,packet,payload))
            onReceive (packet,payload)
          | e ->
            consoleLogger.Log(funcName,(SocketException (int e)))
            onError (SocketException (int e)) // onError changes state and stores exception
            onReceive (null,[||]) // onReceive handles control flow
        do! asyncDo sock.ReceiveAsync callback args'
      }

    let asyncSend (data:obj*byte[]) (composer:obj->byte[]) (onSend:int->unit)
      (onError:exn->unit) (consoleLogger:ConsoleLogger) (sock:Socket) =
      async {
        let funcName = functionName "SocketHandleImpl.asyncSend"
        let packet,payload = data
        let bytes = payload |> Array.append (composer packet)
        let args' = new Args()
        args'.SetBuffer(bytes,0,Array.length bytes)
        let callback (args:Args) =
          match args.SocketError with
          | SocketError.Success ->
            let sent = args.BytesTransferred
            consoleLogger.LogWith(LogLevel.Info,funcName,(data,sent))
            onSend sent
          | e ->
            consoleLogger.Log(funcName,(SocketException (int e)))
            onError (SocketException (int e)) // onError changes state and stores exception
            onSend -1 // onSend handles control flow
        do! asyncDo sock.SendAsync callback args'
      }

  type internal Message =
    | TcpPacket of TcpPacket * byte[]
    | UdpPacket of UdpPacket * byte[]

  type SocketType = Tcp | Udp | TcpListener | Unset

  type internal ReceiveState = Receiving | Error | Idle
  type internal SendState = Sending | Error | Idle

  type SocketHandle internal () =

    let mutable socketType = SocketType.Unset

    let mutable sock : Socket = null

    let mutable messageQueue : Message list = []
    let messageQueueLock = Sync.createLock()

    let mutable consoleLogger : ConsoleLogger = null
    let mutable networkLogger : NetworkLogger = null

    let mutable onReceive : (obj*byte[])->unit = ignore
    let mutable onSend : int->unit = ignore
    let mutable parser : byte[]->obj = fun b -> obj()
    let mutable composer : obj->byte[] = fun o -> [||]

    let mutable receiveState = ReceiveState.Idle
    let receiveStateLock = Sync.createLock()

    let mutable sendState = SendState.Idle
    let sendStateLock = Sync.createLock()

    let mutable lastReceiveException : exn = null
    let mutable lastSendException : exn = null

    let mutable isDisposed = false
    let isDisposedLock = Sync.createLock()
    let disposed() =
      try
      isDisposedLock
      |> Sync.read(fun () -> isDisposed)
      with _ -> true

    member __.SocketType
      with get() = socketType
      and set(value) = socketType <- value
    member internal __.Sock
      with set(value) = sock <- value
    member internal __.ConsoleLogger
      with set(value) = consoleLogger <- value
    member internal __.NetworkLogger
      with set(value) = networkLogger <- value
    member internal __.OnReceive
      with set(value) = onReceive <- value
    member internal __.OnSend
      with set(value) = onSend <- value
    member internal __.Parser
      with set(value) = parser <- value
    member internal __.Composer
      with set(value) = composer <- value
    member __.LastReceiveException
      with get() =
        receiveStateLock
        |> Sync.write(fun () ->
          receiveState <- ReceiveState.Idle
        )
        let t = lastReceiveException
        lastReceiveException <- null
        t
    member __.LastSendException
      with get() =
        sendStateLock
        |> Sync.write(fun () ->
          sendState <- SendState.Idle
        )
        let t = lastSendException
        lastSendException <- null
        t

    member this.SubmitTcpMessage(msg:obj,payload:byte[]) =
      let funcName = SocketHandleImpl.functionName "SocketHandle.SubmitMessage"
      if disposed() then failwith "SocketHandle has been disposed"
      try
      let exnPending =
        sendStateLock
        |> Sync.read(fun () -> not <| isNull lastSendException )
      if exnPending then
        failwithf "Last Send op failed: %s" (this.LastSendException.ToString())
      let messageOption =
        match msg with
        | :? TcpPacket as tcpPacket -> Some <| Message.TcpPacket(tcpPacket,payload)
        | :? UdpPacket as udpPacket -> Some <| Message.UdpPacket(udpPacket,payload)
        | _ -> None
      match messageOption with
      | Some message ->
        messageQueueLock
        |> Sync.write(fun () ->
          messageQueue <- messageQueue@[message]
        )
        consoleLogger.LogWith(LogLevel.Info,funcName,message)
      | _ ->
        consoleLogger.LogWith(LogLevel.Info,funcName,(msg,payload,"Unknown"))
      this.TrySendNext()
      with
      | ex ->
        consoleLogger.Log(funcName,ex)
        ()

    member internal __.InternalReceiveError(e:exn) =
      if not <| disposed() then
        receiveStateLock
        |> Sync.write(fun () ->
          receiveState <- ReceiveState.Error
          lastReceiveException <- e
        )

    member internal __.InternalSendError(e:exn) =
      if not <| disposed() then
        sendStateLock
        |> Sync.write(fun () ->
          sendState <- SendState.Error
          lastSendException <- e
        )

    member this.TryAsyncReceive size =
      if disposed() then failwith "SocketHandle has been disposed"
      let funcName = SocketHandleImpl.functionName "SocketHandle.TryAsyncReceive"
      // in case this wasn't handled upstream
      // do not need explicit synchronized access,
      //  receive ops are serial
      if not <| isNull lastReceiveException then
        failwithf "Last Receive op failed: %s" (this.LastReceiveException.ToString())
      receiveStateLock
      |> Sync.write(fun () ->
        if receiveState = ReceiveState.Idle then
          this.AsyncReceive size
        else
          consoleLogger.LogWith(LogLevel.Info,funcName,"Receive op already running")
      )

    member internal this.AsyncReceive size =
      let receiver =
        async {
          let oe = this.InternalReceiveError
          let or' = this.InternalOnReceive
          do! SocketHandleImpl.asyncReceive size parser or' oe consoleLogger sock
        }
      receiver
      |> Async.StartChild
      |> Async.RunSynchronously
      |> ignore

    // This is an intermediate step for logging and state changing purposes
    member internal __.InternalOnReceive(data:obj*byte[]) =
      let packet,payload = data
      // if we got null, we got an error
      //  pass it on downstream
      if not <| isNull packet && not <| disposed() then
        let packetSize =
          match packet with
          | :? TcpPacket -> TcpPacket.DefaultSize
          | _            -> UdpPacket.DefaultSize
        networkLogger.LogNumberOfBytesWith(LogLevel.Debug,(packetSize+(Array.length payload)))
        receiveStateLock |> Sync.write(fun () ->
          if receiveState = ReceiveState.Receiving then
            receiveState <- ReceiveState.Idle
        )
      onReceive data

    member internal this.TrySendNext() =
      if not <| disposed() then
        let funcName = SocketHandleImpl.functionName "SocketHandle.TryAsyncReceive"
        sendStateLock
        |> Sync.write(fun () ->
          if sendState = SendState.Idle then
            this.SendNext()
          elif sendState = SendState.Error then
            consoleLogger.LogWith(LogLevel.Info,funcName,"Sender is in Error state")
          else
            consoleLogger.LogWith(LogLevel.Info,funcName,"Sender op already running")
        )

    member internal this.SendNext() =
      let funcName = SocketHandleImpl.functionName "SocketHandle.SendNext"
      let sender =
        async {
          let messageOption =
            messageQueueLock
            |> Sync.write(fun () ->
              match messageQueue with
              | x::xs ->
                messageQueue <- xs
                Some x
              | _ -> None
            )
          match messageOption with
          | Some message ->
            let oe = this.InternalSendError
            let os = this.InternalOnSend
            let p,b =
              match message with
              | UdpPacket (p,b) -> (p :> obj),b
              | TcpPacket (p,b) -> (p :> obj),b

            do! SocketHandleImpl.asyncSend (p,b) composer os oe consoleLogger sock
          | _ ->
            consoleLogger.LogWith(LogLevel.Info,funcName,"Message Queue Empty")
            // double check that queue is still empty and change state to idle if so
            sendStateLock 
            |> Sync.write(fun () ->
              messageQueueLock
              |> Sync.read(fun () ->
                match messageQueue with
                | [] -> sendState <- SendState.Idle
                | _ -> ()
              )
            )
        }
      sender
      |> Async.StartChild
      |> Async.RunSynchronously
      |> ignore

    member internal this.InternalOnSend(sent:int) =
      if sent > 0 then
        networkLogger.LogNumberOfBytesWith(LogLevel.Info,sent)
        sendStateLock |> Sync.write(fun () -> sendState <- SendState.Idle)
      onSend sent
      this.TrySendNext()

    member this.AcceptWithConfig(config:SocketConfiguration) =
      let funcName = SocketHandleImpl.functionName "SocketHandle.Dispose"
      let sh = new SocketHandle()
      sh.ConsoleLogger <-
        match config.logger1 with
        | Logger.ConsoleLogger cl -> cl
        | _ -> failwith "Expected a ConsoleLogger"
      sh.NetworkLogger <-
        match config.logger2 with
        | Logger.NetworkLogger nl -> nl
        | _ -> failwith "Expected a NetworkLogger"
      sh.SocketType <- (if config.isTcp then SocketType.Tcp else SocketType.Udp)
      sh.OnReceive <- config.onReceive
      sh.OnSend <- config.onSend
      sh.Parser <- config.parser
      sh.Composer <- config.composer
      sh.Sock <- sock.Accept()
      consoleLogger.LogWith(LogLevel.Info,funcName,"Accepted new connection")
      sh

    interface IDisposable with

      member this.Dispose() =
        let disposer =
          async {
            let stillSending() =
              try
              sendStateLock
              |> Sync.read(fun () ->
                sendState = SendState.Sending
              )
              with :? System.ObjectDisposedException -> false
            let stillReceiving() =
              try
              receiveStateLock
              |> Sync.read(fun () ->
                receiveState = ReceiveState.Receiving
              )
              with :? System.ObjectDisposedException -> false
            while stillReceiving() && stillSending() do
              if not <| stillReceiving() then
                try receiveStateLock.Dispose() with _ -> ()
              elif not <| stillSending() then
                try sendStateLock.Dispose() with _ -> ()
            isDisposedLock
            |> Sync.write(fun () -> isDisposed <- true)
            sock.Dispose()
            messageQueue <- []
            let funcName = SocketHandleImpl.functionName "SocketHandle.Dispose"
            consoleLogger.LogWith(LogLevel.Info,funcName,"Successfully disposed of SocketHandle")
          }
        disposer
        |> Async.StartChild
        |> Async.RunSynchronously
        |> ignore

    static member NewListener(port,consoleLogger) =
      let sh = new SocketHandle()
      let sock = SocketHandleImpl.newV4TcpSocket()
      SocketHandleImpl.bind port sock
      SocketHandleImpl.listen sock
      sh.SocketType <- SocketType.TcpListener
      sh.Sock <- sock
      sh.ConsoleLogger <- consoleLogger
      sh

    static member ConnectUsingConfig(config:SocketConfiguration) =
      let sh = new SocketHandle()
      sh.ConsoleLogger <-
        match config.logger1 with
        | Logger.ConsoleLogger cl -> cl
        | _ -> failwith "Expected a ConsoleLogger"
      sh.NetworkLogger <-
        match config.logger2 with
        | Logger.NetworkLogger nl -> nl
        | _ -> failwith "Expected a NetworkLogger"
      sh.SocketType <- (if config.isTcp then SocketType.Tcp else SocketType.Udp)
      sh.OnReceive <- config.onReceive
      sh.OnSend <- config.onSend
      sh.Parser <- config.parser
      sh.Composer <- config.composer
      let sock =
        if config.isTcp then SocketHandleImpl.newV4TcpSocket()
        else SocketHandleImpl.newV4UdpSocket()
      sock
      |> SocketHandleImpl.connect config.ip config.port
      sh.Sock <- sock
      sh