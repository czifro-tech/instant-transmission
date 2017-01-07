namespace MUDT.Test

module TestRunner =

  open System
  open System.Text.RegularExpressions

  let testRunners() =
    [|
      HasherUnitTests.testRunner
      PortCheckerUnitTest.testRunner
      MemoryMappedFileUnitTest.testRunner
    |]

  let parseArgv argv =
    let doBoundsCheck i l u = if i < l || i > u then failwith "Invalid arg"
    if argv = "x" || argv = "x.x" then "x", "x"
    else
      let dot = Array.tryFindIndex(fun x -> x = '.') (argv.ToCharArray())
      if dot.IsSome then
        let major, minor = int(string(argv.[0..dot.Value-1])), string(argv.[dot.Value+1..])
        doBoundsCheck major 1 3
        string(major), minor
      else
        doBoundsCheck (int(argv)) 1 3
        argv, "x"

  [<EntryPoint>]
  let main argv =
    printfn "Argv: %A" argv
    let testMod, testOp = parseArgv argv.[0]
    Helper.testRunner testRunners testOp testMod
    0