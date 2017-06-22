namespace MCDTP.FTP

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.IO.MemoryMappedFile
  open MCDTP.IO.MemoryMappedFile.Partition
  open MCDTP.Net.Sockets
  open MCDTP.Net.Protocol
  open MCDTP.Net.PacketManagement

  [<AutoOpen>]
  module Expression =

    type FtpBuilder() =

      member __.Return _ = FtpConfiguration.Instance

      member __.Bind(f1:FtpConfiguration,_) = f1

    type FtpBuilder with

      [<CustomOperation ("useParser", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseParser(f,p) =
        { f with parser_ = p }

      [<CustomOperation ("useComposer", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseComposer(f:FtpConfiguration,c) =
        { f with composer_ = c }

      [<CustomOperation ("configureUdp", MaintainsVariableSpaceUsingBind = true)>]
      member __.ConfigureUdp(f,u) =
        { f with udp = u }

      [<CustomOperation ("configureTcp", MaintainsVariableSpaceUsingBind = true)>]
      member __.ConfigureTcp(f,t) =
        { f with tcp = t }

      [<CustomOperation ("configureMMF", MaintainsVariableSpaceUsingBind = true)>]
      member __.ConfigureMMF(f,m) =
        { f with mmf = m }

      [<CustomOperation ("useConsole", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseConsole(f,c) =
        { f with console = c }

      [<CustomOperation ("monitorNetwork", MaintainsVariableSpaceUsingBind = true)>]
      member __.MonitorNetwork(f,n) =
        { f with network = n }

      [<CustomOperation ("withParallelism", MaintainsVariableSpaceUsingBind = true)>]
      member __.WithParallelism(f,i) =
        { f with parallelism = i }

      [<CustomOperation ("clientMode", MaintainsVariableSpaceUsingBind = true)>]
      member __.ClientMode(f:FtpConfiguration) =
        { f with isServer = false }

      [<CustomOperation ("serverMode", MaintainsVariableSpaceUsingBind = true)>]
      member __.ServerMode(f:FtpConfiguration) =
        { f with isServer = true }

    let ftp = FtpBuilder()