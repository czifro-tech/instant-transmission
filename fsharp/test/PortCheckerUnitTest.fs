namespace MUDT.Test


  open System
  open Xunit
  open MUDT.Net

  module PortCheckerUnitTest =

    let ``Speed Test`` () =

      let startTime = DateTime.UtcNow
      PortChecker.getAvailablePorts() |> ignore
      let endTime = DateTime.UtcNow
      printfn "Port check took %d ms" (endTime - startTime).Milliseconds

    let testRunner op =
      match op with
      | "1" | "x" -> ``Speed Test``()
      | _ -> failwith "Invalid option"