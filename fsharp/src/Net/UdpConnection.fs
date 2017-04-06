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

    let mutable _parser = (fun x -> None) : byte[] -> UdpPacket option

    let mutable _composer = (fun x -> [||]) : UdpPacket -> byte[]

    let mutable _receiveCallback = (fun x -> async { ignore 0 }) : UdpPacket -> Async<unit> 

    let _receiveCompleted = 
      EventHandler<SocketAsyncEventArgs>(fun (sender:obj) (e:SocketAsyncEventArgs) -> 
        let packet = _parser(e.Buffer)
        if packet.IsSome then
          _receiveCallback(packet.Value) |> ignore
      )

    let newSocket = 
      new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)

    do
      _socket <- newSocket

    member x.Socket
      with get() = _socket
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
        return! _socket.AsyncBind(endPoint)
      }
    member x.SendAsync(packet:UdpPacket) =
      async {
        let bytes = x.ByteComposer packet
        return! x.Socket.AsyncSend(bytes, 0, UdpPacket.DefaultSize)
      }

    member private x.ReceiveCompleted(e:SocketAsyncEventArgs) =
      let packet = x.ByteParser(e.Buffer)
      if packet.IsSome then
        x.ReceiveCallback(packet.Value) |> ignore

    member x.ReceiveAsync() = 
      async {
        let bytes = TypeUtility.nullByteArray UdpPacket.DefaultSize
        return! x.Socket.AsyncReceive(bytes, 0, UdpPacket.DefaultSize, _receiveCompleted)
      }