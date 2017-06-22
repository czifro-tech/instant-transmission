namespace MCDTP.Net.Sockets

  open MCDTP.Logging

  [<AutoOpen>]
  module Expression =

    type SocketBuilder() =
      member __.Return _ = SocketConfiguration.Instance

      member __.Bind (s1:SocketConfiguration,_) = s1

    type SocketBuilder with

      [<CustomOperation ("useTcp", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseTcp(s) = SocketConfiguration.set SocketConfiguration.isTcp_ true s

      [<CustomOperation ("useUdp", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseUdp(s) = SocketConfiguration.set SocketConfiguration.isTcp_ false s

      [<CustomOperation ("connectTo", MaintainsVariableSpaceUsingBind = true)>]
      member __.ConnectTo(s,ip) = SocketConfiguration.set SocketConfiguration.ip_ ip s

      [<CustomOperation ("usingPort", MaintainsVariableSpaceUsingBind = true)>]
      member __.UsingPort(s,p) = SocketConfiguration.set SocketConfiguration.port_ p s

    let socketHandle = SocketBuilder()