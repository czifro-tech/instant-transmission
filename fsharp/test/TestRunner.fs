namespace MUDT.Test

module TestRunner =

  open System
  open System.Linq

  let testRunners() =
    [|
      HasherUnitTests.testRunner
      PortCheckerUnitTest.testRunner
      MemoryMappedFileUnitTest.testRunner
    |]

  let parseArgv argv =
    match argv with
    | "x" | "x.x" -> ("x", "x")
    | "1" | "1.1" | "1.x" -> ("1", "x")
    | "2" | "2.1" | "2.x" -> ("2", "x")
    | "3" | "3.x" -> ("3", "x")
    | "3.1" | "3.2" -> ("3", string(argv.[2]))
    | _ -> failwith "Invalid arg"

  [<EntryPoint>]
  let main argv =
    printfn "Argv: %A" argv
    let testMod, test = parseArgv argv.[0]
    match testMod with
    | "x" -> () |> testRunners |> Array.iter(fun runner -> runner test)
    | _ -> (testRunners()).[int(testMod)-1] test
    0