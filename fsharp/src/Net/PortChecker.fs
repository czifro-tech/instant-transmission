namespace MUDT.Net

  open System
  open System.Net
  open System.Net.Sockets

  module PortChecker =

    let private checkPort (port:int) =
      let binder = new Socket(AddressFamily.InterNetwork,
                             SocketType.Stream, ProtocolType.Tcp)
      let check =
        try 
          binder.Bind(IPEndPoint(IPAddress.Any, port))
          true
        with 
        | _ -> (); false
      let res = check
      binder.Dispose()
      res

    let isPortOpen (port:int) =
      checkPort port

    let getAvailablePorts() =
      let ports = [| for i in 1024 .. 65535 -> i |]
      ports
      |> Array.filter(isPortOpen)

    let filterAvailablePorts (ports:int[]) =
      ports
      |> Array.filter(isPortOpen)