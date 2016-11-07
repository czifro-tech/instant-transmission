namespace MUDT.Net

  open System
  open System.Threading
  open MUDT.Diagnostics
  open MUDT.Net.Protocol

  module Remote =

    let private asynchrony =
      Cache.getCachedItem "asynchrony" :?> int

    let startServerAsync (port:int) = 
      let cts = new CancellationTokenSource()
      let server =
        async {
          let tcp = new TcpConnection()
          tcp.ByteComposer <- TcpByteComposer.tcpPacketToByteArray
          tcp.ByteParser <- TcpByteParser.byteArrayToTcpPacket
          do! tcp.ListenAsync(port)

          return 0 |> ignore
        }
      Async.Start(server,cts.Token)
      { new IDisposable with member x.Dispose() = cts.Cancel(); }

    let startClientAsync (port:int) (ip:string) =
      let cts = new CancellationTokenSource()
      let client =
        async {
          let tcp = new TcpConnection()
          tcp.ByteComposer <- TcpByteComposer.tcpPacketToByteArray
          tcp.ByteParser <- TcpByteParser.byteArrayToTcpPacket
          do! tcp.ConnectAsync(ip, port)

          return 0 |> ignore
        }
      Async.Start(client,cts.Token)
      { new IDisposable with member x.Dispose() = cts.Cancel(); }