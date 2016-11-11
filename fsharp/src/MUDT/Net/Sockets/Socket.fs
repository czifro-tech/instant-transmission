namespace MUDT.Net

  open System
  open System.Net
  open System.Net.Sockets
  open System.Threading


  module Sockets = 

    type Socket with

      member x.AsyncAccept(accepted) =
        async {
          let e = new SocketAsyncEventArgs()
          e.Completed.Add(accepted)
          e.Completed.Add(fun arg -> 
            ignore 0 // Add logger here for tracing
          )
          return x.AcceptAsync(e)
        }

      member x.AsyncConnect(endPoint:IPEndPoint) =
        async {
          let e = new SocketAsyncEventArgs()
          e.RemoteEndPoint <- endPoint
          e.Completed.Add(fun arg -> 
            ignore 0 // Add logger here for tracing
          )
          return x.ConnectAsync(e)
        }

      member x.AsyncListen(backlog:int) =
        async {
          x.Listen(backlog)
        }

      member x.AsyncBind(endPoint:IPEndPoint) =
        async {
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
          e.Completed.Add(completed)
          return x.ReceiveAsync(e)
        }