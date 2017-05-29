namespace MCDTP.Net.Sockets

  open MCDTP.Logging

  [<AutoOpen>]
  module Expression =

    type SocketBuilder() =
      member __.Return() = SocketConfiguration.Instance

    type SocketBuilder with

      [<CustomOperation ("useTcp", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseTcp(s) = SocketConfiguration.set SocketConfiguration.isTcp_ true s

      [<CustomOperation ("useUdp", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseUdp(s) = SocketConfiguration.set SocketConfiguration.isTcp_ false s

      [<CustomOperation ("connectTo", MaintainsVariableSpaceUsingBind = true)>]
      member __.ConnectTo(s,ip) = SocketConfiguration.set SocketConfiguration.ip_ ip s

      [<CustomOperation ("usingPort", MaintainsVariableSpaceUsingBind = true)>]
      member __.UsingPort(s,p) = SocketConfiguration.set SocketConfiguration.port_ p s

      [<CustomOperation ("onReceive", MaintainsVariableSpaceUsingBind = true)>]
      member __.OnReceive(s,fn) =
        SocketConfiguration.setFn (SocketConfiguration.Fn.OnReceive fn) s

      [<CustomOperation ("onSend", MaintainsVariableSpaceUsingBind = true)>]
      member __.OnSend(s,fn) =
        SocketConfiguration.setFn (SocketConfiguration.Fn.OnSend fn) s

      [<CustomOperation ("useParser", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseParser(s,fn) =
        SocketConfiguration.setFn (SocketConfiguration.Fn.Parser fn) s

      [<CustomOperation ("useComposer", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseComposer(s,fn) =
        SocketConfiguration.setFn (SocketConfiguration.Fn.Composer fn) s

      [<CustomOperation ("attachConsoleLogger", MaintainsVariableSpaceUsingBind = true)>]
      member __.AttachConsoleLogger(s,cl) =
        SocketConfiguration.set SocketConfiguration.logger1_ (Logger.ConsoleLogger cl) s

      [<CustomOperation ("attachNetworkLogger", MaintainsVariableSpaceUsingBind = true)>]
      member __.AttachNetworkLogger(s,nl) =
        SocketConfiguration.set SocketConfiguration.logger2_ (Logger.NetworkLogger nl) s

      [<CustomOperation ("noNetworkLogging", MaintainsVariableSpaceUsingBind = true)>]
      member __.NoNetworkLogging(s) =
        SocketConfiguration.set SocketConfiguration.logger2_ (Logger.NetworkLogger null) s

    let socketHandle = SocketBuilder()