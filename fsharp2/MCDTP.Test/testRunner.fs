namespace MCDTP.Test

  [<AutoOpen>]
  module TestRunner =

    open System

    let testRunners =
      [
        "networkTest",NetworkTest.testRunner
        "quit",ignore
      ]
      |> Map.ofList

    let printTestRunners() =
      testRunners
      |> Map.iter(fun k _ ->
        printfn "\t%s" k
      )

    let hasKey (key:string) =
      testRunners
      |> Map.exists(fun k _ ->
        (k.ToLower()) = (key.ToLower())
      )

    let tests _ =
      let mutable continueLoop = true
      while continueLoop do
        printfn "Choose a test module:"
        printTestRunners()
        printf "|> "
        let key = Console.ReadLine()
        if key = "quit" then
          continueLoop <- false
        else
          if hasKey key then
            let testRunner = testRunners |> Map.find key
            testRunner()
          else
            printfn "No such test module"