namespace MCDTP.FTP

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.IO.MemoryMappedFile
  open MCDTP.IO.MemoryMappedFile.Partition
  open MCDTP.Net.Sockets
  open MCDTP.Net.Protocol
  open MCDTP.Net.PacketManagement

  type FtpConfiguration =
    {
      parser_            : byte[]->obj
      composer_          : obj->byte[]

      console           : LoggerConfiguration
      network           : LoggerConfiguration

      udp               : SocketConfiguration
      tcp               : SocketConfiguration

      mmf               : MMFConfiguration

      parallelism       : int

      isServer          : bool
    }

    static member Instance =
      {
        parser_            = fun _ -> null
        composer_          = fun _ -> [||]
        console           = loggerConfig.Return()
        network           = loggerConfig.Return()
        udp               = socketHandle.Return()
        tcp               = socketHandle.Return()
        mmf               = mmf.Return()
        parallelism       = 0
        isServer          = true
      }