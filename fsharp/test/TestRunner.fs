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

  /// Acceptable formats are:
  ///  x || x.x    => run all tests
  ///  \d || \d.x  => run all tests of a specific module
  ///  \d.\d       => run a specific test from a specific module
  let parseArgv argv =
    let doBoundsCheck i l u = if i < l || i > u then failwith "Invalid arg"
    if argv = "x" || argv = "x.x" then "x", "x"
    else
      let dot = Array.tryFindIndex(fun x -> x = '.') (argv.ToCharArray())
      if dot.IsSome then
        let major, minor = int(string(argv.[0..dot.Value-1])), string(argv.[dot.Value+1..])
        doBoundsCheck major 1 (Array.length (testRunners()))
        string(major), minor
      else
        doBoundsCheck (int(argv)) 1 (Array.length (testRunners()))
        argv, "x"

  [<EntryPoint>]
  let main argv =
    printfn "Argv: %A" argv
    let testMod, testOp = parseArgv argv.[0]
    Helper.testRunner testRunners testOp testMod
    0