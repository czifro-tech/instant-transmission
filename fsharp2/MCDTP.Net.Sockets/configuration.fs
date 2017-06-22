namespace MCDTP.Net.Sockets

  open System.Net
  open MCDTP.Logging

  type SocketConfiguration =
    {
      isTcp      : bool
      port       : int
      ip         : IPAddress
      onReceive  : int->(obj*byte[])->unit
      onSend     : int->int->unit
      parser     : byte[]->obj
      composer   : obj->byte[]
      logger1    : LoggerConfiguration
      logger2    : LoggerConfiguration
    }

    static member Instance =
      {
        isTcp      = false
        port       = 0
        ip         = IPAddress.Any
        onReceive  = fun _ _ -> ()
        onSend     = fun _ _ -> ()
        parser     = fun _ -> obj()
        composer   = fun _ -> [||]
        // console and network, respectively
        logger1    = LoggerConfiguration.empty
        logger2    = LoggerConfiguration.empty
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module internal SocketConfiguration =

    let isTcp_ = "isTcp"
    let port_ = "port"
    let ip_ = "ip"

    type internal OnReceive = int->(obj*byte[])->unit
    and internal OnSend = int->int->unit
    and internal Parser = byte[]->obj
    and internal Composer = obj->byte[]
    type Fn =
      | OnReceive of OnReceive
      | OnSend of OnSend
      | Parser of Parser
      | Composer of Composer

    let set k (v:obj) c =
      match k with
      | _ when k = isTcp_ -> { c with isTcp = (v :?> bool) }
      | _ when k = port_ -> { c with port = (v :?> int) }
      | _ when k = ip_ -> { c with ip = (v :?> IPAddress) }
      | _ -> failwithf "Unknown key '%s'" k