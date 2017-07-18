namespace MCDTP.Test

  open System
  open System.IO
  open System.Text.RegularExpressions

  [<AutoOpen>]
  module Bandwidth =

    type Transaction = DateTime * float

    let interval = TimeSpan(0,0,10)

    let dir = Directory.GetCurrentDirectory()

    let fileExt = ".raw"
    let outExt = ".dat"

    let pattern = @"^\d+:\d+:\d+.\d+\s\d+$"

    let dumpFiles =
      let directory = DirectoryInfo(dir)
      directory.GetFiles("*"+fileExt)
      |> Array.mapi(fun i fi -> i,fi.Name)
      |> Map.ofArray

    let listFiles _ =
      dumpFiles
      |> Map.iter(fun k v ->
        printfn "\t%d -> %s" k v
      )

    let filterUdpLines (line:string) =
      let m = Regex.Match(line,pattern)
      m.Success

    let toTransaction (line:string) =
      let comps = line.Split(' ')
      DateTime.Parse comps.[0], float comps.[1]

    let parseTrafficDump td =
      td
      |> Array.filter filterUdpLines
      |> Array.map toTransaction

    let computeBps (trans:Transaction[]) =
      let mutable counter = 0.0
      let mutable startTime,_ = trans.[0]
      let mutable updateStartTime = false
      trans
      |> Array.mapi(fun i t ->
        let ts,len = t
        if updateStartTime then
          startTime <- ts
          updateStartTime <- false
        let tl = ts - startTime
        if tl.TotalMilliseconds > interval.TotalMilliseconds then
          let total = counter + len
          let Bps = total / tl.TotalSeconds
          updateStartTime <- true
          counter <- 0.0
          Bps
        else
          counter <- counter + len
          -1.0
      )
      |> Array.filter(fun Bps -> Bps <> -1.0)
      |> Array.mapi(fun i Bps -> i*10,Bps)

    let showAverageBps (allBps:(_*float)[]) =
      let averageBps =
        allBps
        |> Array.averageBy(fun ele ->
          let _,Bps = ele
          Bps
        )

      printfn "Average Bps: %f" averageBps
      allBps

    let serializeBps x =
      let time,Bps = x
      sprintf "%d %f" time Bps

    let saveBps file allBps =
      (file,allBps)
      |> File.WriteAllLines

    let loadTrafficDump file =
      file
      |> File.ReadAllLines

    let processTrafficDump file =
      let allBps =
        file
        |> loadTrafficDump
        |> parseTrafficDump
        |> computeBps
        |> showAverageBps
        |> Array.map serializeBps
      let file = file.Replace(fileExt,outExt)
      saveBps file allBps

    let chooseFile () =
      let mutable selection = ""
      let mutable continueLoop = true
      while continueLoop do
        printfn "Select a file:"
        listFiles()
        printf "|> "
        let k = Console.ReadLine() |> int
        if dumpFiles |> Map.containsKey k |> not then
          printfn "Invalid option"
        else
          selection <- dumpFiles.[k]
          continueLoop <- false
      selection

    let executeBandwidthTool = chooseFile >> processTrafficDump