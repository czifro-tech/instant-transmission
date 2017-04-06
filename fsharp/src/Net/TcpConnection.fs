namespace MUDT.Net

  open System
  open System.Collections.Generic
  open System.Net
  open System.Net.Sockets
  open MUDT.Net.ProtocolV1
  open MUDT.Net.Protocol
  open MUDT.Net.Sockets
  open MUDT.Collections
  open MUDT.Utilities

  type TcpConnection() =

    let mutable _socket = null : Socket

    let mutable _parser = (fun x o -> None) : byte[] -> int -> TcpPacketV2 option

    let mutable _composer = (fun x o -> None) : TcpPacketV2 -> int -> byte[] option

    let mutable _receiveCallback = (fun x -> async { ignore 0 }) : TcpPacketV2 -> Async<unit> 

    let _receiveCompleted = 
      EventHandler<SocketAsyncEventArgs>(fun (sender:obj) (e:SocketAsyncEventArgs) -> 
        let packet = _parser(e.Buffer)
        if packet.IsSome then
          _receiveCallback(packet.Value) |> ignore
      )

    let newSocket =
      new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)

    member x.Socket
      with get() = _socket
      and set(value) = _socket <- value

    member x.ByteParser
      with get() = _parser
      and set(value) = _parser <- value

    member x.ByteComposer
      with get() = _composer
      and set(value) = _composer <- value

    member x.ReceiveCallback
      with get() = _receiveCallback
      and set(value) = _receiveCallback <- value

    member x.ConnectAsync(ip:string, port:int) =
      async {
        let! hostInfo = Async.AwaitTask(Dns.GetHostEntryAsync(ip))
        let ipAddresses = hostInfo.AddressList
        let endPoint = IPEndPoint(ipAddresses.[0], port)
        x.Socket <- newSocket
        do! x.Socket.AsyncConnect(endPoint)
      }

    member x.ListenAsync(port:int) = 
      async {
        if not (isNull(x.Socket)) then
          raise (SocketException(SocketError.AddressAlreadyInUse |> int))
        else
          let endPoint = IPEndPoint(IPAddress.Any, port)
          x.Socket <- newSocket
          do! x.Socket.AsyncBind(endPoint)
          do! x.Socket.AsyncListen(SocketOptionName.MaxConnections |> int)
      }

    member x.AcceptAsync() : Async<TcpConnection> =
      async {
        if isNull(x.Socket) then
          raise (ArgumentNullException("Socket"))
        else if not x.Socket.IsBound then
          raise (SocketException(SocketError.NotInitialized |> int))
        let tcp = TcpConnection()
        tcp.Socket <- x.Socket.AsyncAccept()
        |> Async.RunSynchronously
        return tcp
      }

    member x.SendAsync(packet:TcpPacketV2) =
      async {
        let bytes = (x.ByteComposer(packet)).Value
        return! x.Socket.AsyncSend(bytes, 0, TcpPacket.DefaultSize)
      }

    member x.ReceiveAsync() = 
      async {
        let bytes = TypeUtility.nullByteArray TcpPacketV2.DefaultSize
        return! x.Socket.AsyncReceive(bytes, 0, TcpPacketV2.DefaultSize, _receiveCompleted)
      }

    member x.Receive(size:int) =
      let bytes = TypeUtility.nullByteArray size
      let code = x.Socket.Receive(bytes, 0, size, SocketFlags.None)
      if code = int(SocketError.Success) then
        Some bytes
      else
        None