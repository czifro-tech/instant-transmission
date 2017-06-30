namespace MCDTP.Test

  module Main =

    let executionOptions =
      [
        "Test Execution",tests
        "Parse Test Results",executeParser
      ]
      |> Map.ofList

    let executionNames =
      [
        0,"quit"
        1,"Test Execution"
        2,"Parse Test Results"
      ]
      |> Map.ofList

    let printExecutionNames _ =
      executionNames
      |> Map.iter(fun k v ->
        printfn "\t%d -> %s" k v
      )

    [<EntryPoint>]
    let main _ =
      let mutable continueLoop = true
      while continueLoop do
        printfn "Choose an execution option:"
        printExecutionNames()
        printf "|> "
        let k = System.Console.ReadLine() |> int
        if executionNames |> Map.containsKey k |> not then
          printfn "Invalid option"
        else
          if executionNames.[k] = "quit" then
            continueLoop <- false
          else
            let op = executionOptions.[executionNames.[k]]
            op()
      0