namespace MUDT.Net

  open System
  open System.Collections.Generic
  open System.Net
  open System.Net.Sockets
  open MUDT.Net.ProtocolV1
  open MUDT.Net.Sockets
  open MUDT.Collections
  open MUDT.Utilities

  type UdpConnection() =

    let mutable _socket = null : Socket

    let mutable _remoteEP = null : IPEndPoint

    let mutable _parser = (fun x -> None) : byte[] -> UdpPacket option

    let mutable _composer = (fun x -> [||]) : (UdpPacket) -> byte[]

    let mutable _receiveCallback = (fun x -> async { ignore 0 }) : UdpPacket -> Async<unit>

    let _receiveCompleted = 
      EventHandler<SocketAsyncEventArgs>(fun (sender:obj) (e:SocketAsyncEventArgs) -> 
        let packet = _parser(e.Buffer)
        if packet.IsSome then
          _receiveCallback(packet.Value) |> ignore
      )

    let newSocket = 
      new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp)

    do
      _socket <- newSocket
      _socket.ReceiveTimeout <- 1000

    member x.Socket
      with get() = _socket
    member x.RemoteEP
      with get() = _remoteEP
      and set(value) = _remoteEP <- value
    member x.ByteParser
      with get() = _parser
      and set(value) = _parser <- value
    member x.ByteComposer
      with get() = _composer
      and set(value) = _composer <- value
    member x.ReceiveCallback
      with get() = _receiveCallback
      and set(value) = _receiveCallback <- value
    member x.BindAsync(port:int) = 
      async {
        let endPoint = IPEndPoint(IPAddress.Any, port)
        return _socket.Bind(endPoint)
      }

    member x.ConnectAsync(endPoint:IPEndPoint) =
      async {
        _remoteEP <- endPoint
        return _socket.Connect(endPoint)
      }

    member x.SendAsync(packet:UdpPacket) =
      async {
        let bytes = x.ByteComposer packet
        let sent = x.Socket.SendTo(bytes,0,(Array.length bytes),SocketFlags.None,_remoteEP)
        //printfn "Sent count: %d" sent
        ()
      }

    member x.Receive(size:int) =
      let bytes = TypeUtility.nullByteArray size
      let recvd = x.Socket.Receive(bytes,0,size,SocketFlags.None)
      //printfn "Received count: %d" recvd
      bytes

    member x.ReceiveAsync() = 
      async {
        let bytes = TypeUtility.nullByteArray UdpPacket.DefaultSize
        return! x.Socket.AsyncReceive(bytes, 0, UdpPacket.DefaultSize, _receiveCompleted)
      }