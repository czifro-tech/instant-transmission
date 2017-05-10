namespace MUDT.Test


  open System
  open MUDT.Net

  module PortCheckerUnitTest =

    let ``Speed Test`` () =

      let startTime = DateTime.UtcNow
      PortChecker.getAvailablePorts() |> ignore
      let endTime = DateTime.UtcNow
      printfn "Port check took %d ms" (endTime - startTime).Milliseconds

    let private tests() =
      [|
        ``Speed Test``
      |]
    let testRunner op =
      Helper.testRunner tests () op