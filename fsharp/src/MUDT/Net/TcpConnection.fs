namespace MUDT.Net

  open System
  open System.Collections.Generic
  open System.Net
  open System.Net.Sockets
  open MUDT.Net.Protocol
  open MUDT.Net.Sockets
  open MUDT.Collections
  open MUDT.Utilities

  type TcpConnection() =

    let mutable _socket = null : Socket

    let mutable _parser = (fun x -> None) : byte[] -> TcpPacket option

    let mutable _composer = (fun x -> None) : TcpPacket -> byte[] option

    let mutable _receiveCallback = (fun x -> async { ignore 0 }) : TcpPacket -> Async<unit> 

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
        return! x.Socket.AsyncConnect(endPoint)
      }

    member x.ListenAsync(port:int) = 
      async {
        if not <| isNull(x.Socket) then
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
        else if not <| x.Socket.IsBound then
          raise (SocketException(SocketError.NotInitialized |> int))
        let tcp = TcpConnection()
        let accepted (args:SocketAsyncEventArgs) =
          tcp.Socket <- args.AcceptSocket
        let! ret = x.Socket.AsyncAccept(accepted)
        if not <| ret then
          raise (SocketException(SocketError.SocketError |> int))
        return tcp
      }

    member x.SendAsync(packet:TcpPacket) =
      async {
        let bytes = x.ByteComposer(packet).Value
        return! x.Socket.AsyncSend(bytes, 0, TcpPacket.DefaultSize)
      }

    member private x.ReceiveCompleted(e:SocketAsyncEventArgs) =
      let packet = x.ByteParser(e.Buffer)
      if packet.IsSome then
        x.ReceiveCallback(packet.Value) |> ignore

    member x.ReceiveAsync() = 
      async {
        let bytes = TypeUtility.nullByteArray TcpPacket.DefaultSize
        return! x.Socket.AsyncReceive(bytes, 0, TcpPacket.DefaultSize, x.ReceiveCompleted)
      }

    member x.Receive(size:int) =
      let bytes = TypeUtility.nullByteArray size
      let code = x.Socket.Receive(bytes, 0, size, SocketFlags.None)
      if code = int(SocketError.Success) then
        Some bytes
      else
        None