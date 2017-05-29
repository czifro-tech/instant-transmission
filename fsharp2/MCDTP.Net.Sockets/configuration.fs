namespace MCDTP.Net.Sockets

  open System.Net
  open MCDTP.Logging

  type SocketConfiguration =
    {
      isTcp      : bool
      port       : int
      ip         : IPAddress
      onReceive  : (obj*byte[])->unit
      onSend     : int->unit
      parser     : byte[]->obj
      composer   : obj->byte[]
      logger1    : Logger
      logger2    : Logger
    }

    static member Instance =
      {
        isTcp      = false
        port       = 0
        ip         = IPAddress.Any
        onReceive  = ignore
        onSend     = ignore
        parser     = fun b -> obj()
        composer   = fun o -> [||]
        // console and network, respectively
        logger1    = Logger.NoLogger
        logger2    = Logger.NoLogger
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module internal SocketConfiguration =

    let isTcp_ = "isTcp"
    let port_ = "port"
    let ip_ = "ip"
    let onReceive_ = "onReceive"
    let onSend_ = "onSend"
    let logger1_ = "logger1"
    let logger2_ = "logger2"

    type internal OnReceive = (obj*byte[])->unit
    and internal OnSend = int->unit
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
      | _ when k = logger1_ -> { c with logger1 = (v :?> Logger) }
      | _ when k = logger2_ -> { c with logger2 = (v :?> Logger) }
      | _ -> failwithf "Unknown key '%s'" k

    let inline setFn (fn:Fn) c =
      match fn with
      | OnReceive or' -> { c with onReceive = or' }
      | OnSend os -> { c with onSend = os }
      | Parser p -> { c with parser = p }
      | Composer c' -> { c with composer = c' }