namespace MUDT.Net

  open System
  open System.Net
  open System.Net.Sockets
  open System.Threading


  module Sockets = 

    type Socket with

      member x.AsyncAccept() =
        async {
          return! Async.AwaitTask(x.AcceptAsync())
        }

      member x.AsyncConnect(endPoint:IPEndPoint) =
        async {
          printfn "Connecting to %s" (endPoint.ToString())
          do! Async.AwaitTask(x.ConnectAsync(endPoint))
        }

      member x.AsyncListen(backlog:int) =
        async {
          x.Listen(backlog)
        }

      member x.AsyncBind(endPoint:IPEndPoint) =
        async {
          printfn "Listening on %s" (endPoint.ToString())
          x.Bind(endPoint)
        }

      member x.AsyncSend(bytes:byte[], offset:int, count:int) =
        async {
          let e = new SocketAsyncEventArgs()
          e.SetBuffer(bytes, offset, count)
          e.SocketFlags <- SocketFlags.None
          return x.SendAsync(e)
        }

      member x.AsyncReceive(bytes:byte[], offset:int, count:int, completed) =
        async {
          let e = new SocketAsyncEventArgs()
          e.SetBuffer(bytes, offset, count)
          e.SocketFlags <- SocketFlags.None
          e.add_Completed(completed)
          return x.ReceiveAsync(e)
        }

      member x.OverriddenToString() =
        printfn "Socket::IsBound: %b" x.IsBound
        printfn "Socket::IsConnected: %b" x.Connected
        printfn "Socket::LocalEndPoint: %s" (x.LocalEndPoint.ToString())
        if not <| isNull x.RemoteEndPoint then
          printfn "Socket::RemoteEndPoint: %s" (x.RemoteEndPoint.ToString())