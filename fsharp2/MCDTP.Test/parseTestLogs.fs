namespace MCDTP.Test

  open System
  open System.IO
  open System.Text.RegularExpressions

  module Parser =

    type IntervaledThroughput = int * int
    type PacketLoss =
      | Client of int64[]
      | Server of int64

    let messageComponentPattern = @"\[[a-zA-Z0-9:.\s]*(\[\|(\d+[L]([;\s]+)?)*\|\])?\]"

    let mutable lines = [||]

    let private extractComponentsFromLine line =
      let comps =
        [|
          for m in Regex.Matches(line,messageComponentPattern) ->
            List.head [ for g in m.Groups -> g.Value ]
        |]
      if Array.isEmpty comps then
        printfn "No matches: %s" line
      comps

    let loadFile file' =
      lines <-
        file'
        |> File.ReadAllLines

    let segregateLines _ =
      let throughputMessages =
        lines
        |> Array.filter(fun s ->
          s.Contains("Throughput")
        )
      let packetsDroppedMessages =
        lines
        |> Array.filter(fun s ->
          s.Contains("Packets Dropped") || s.Contains("Packet Dropped")
        )

      throughputMessages, packetsDroppedMessages

    let parseThroughputLines ls : IntervaledThroughput[] =
      ls
      |> Array.map extractComponentsFromLine
      |> Array.map(fun comps ->
        comps
        |> Array.filter(fun comp -> comp.Contains("Throughput") || comp.Contains("Interval"))
        |> Array.map(fun comp ->
          (comp.Replace("Throughput:","")).Replace("Interval:","")
          |> String.collect(fun c ->
            if c <> '[' && c <> ']' then sprintf "%c" c
            else ""
          )
        )
      )
      |> Array.append [| [| "0"; "00:00:00" |] |]
      |> Array.map(fun comps ->
        let t = (int comps.[0]),(int (TimeSpan.Parse(comps.[1])).TotalSeconds)
        t
      )

    let parsePacketLoss ls =
      ls
      |> Array.map extractComponentsFromLine
      |> Array.map(fun comps ->
        comps
        |> Array.filter(fun comp -> comp.Contains("Sequence Number: ") || comp.Contains("Sequence Numbers: "))
        |> Array.map(fun comp ->
          let comp =
            (comp.Replace("Sequence Number: ","")).Replace("Sequence Numbers: ","")
          let comp = comp.Substring(1)
          let comp = comp.Substring(0,(String.length comp)-1)
          //printfn "%s" comp
          if comp = "" then "|" else comp
        )
        |> fun arr -> Array.get arr 0
      )
      |> Array.map(fun comp ->
        if comp.Contains("|") then
          let comp =
            comp
            |> String.collect(fun c ->
              if c <> '[' && c <> ']' && c <> '|'
                && c <> ' ' && c <> 'L' then
                sprintf "%c" c
              else ""
            )
          if comp = "" then
            PacketLoss.Client(Array.empty)
          else
            let vs =
              comp.Split(';')
              |> Array.map(fun v ->
                Int64.Parse v
              )
            PacketLoss.Client(vs)
        else
          PacketLoss.Server (Int64.Parse comp)
      )

    let parseLines _ =
      let throughputLines, pDropLines = segregateLines ()
      let intervaledThroughputs =
        parseThroughputLines throughputLines
      let packetLosses =
        parsePacketLoss pDropLines
      intervaledThroughputs,packetLosses

    let computeThroughput intervaledThroughputs =
      let mutable time = 0
      intervaledThroughputs
      |> Array.map(fun it ->
        let th,i = it
        if i = 0 then
          th,time
        else
          time <- time + i
          (th / i),time
      )
      |> Array.map(fun out ->
        let th,t = out
        sprintf "%d %d" th t
      )

    let computeTotalPacketLoss packetLosses =
      packetLosses
      |> Array.map(fun pl ->
        match pl with
        | PacketLoss.Client(arr) -> Array.length arr
        | _ -> 1
      )
      |> Array.sum

    let computeResults lses =
      let it,pl = lses
      (computeThroughput it),(computeTotalPacketLoss pl)

    let parseAndCompute = parseLines >> computeResults

    let outputResults fileName res =
      let thRes,plCount = res
      printfn "Total Packet Loss: %d" plCount
      (fileName,thRes)
      |> File.WriteAllLines

    let processFile file =
      let p = parseAndCompute >> (outputResults (file + "-thoughput.dat"))
      loadFile file
      p()

  [<AutoOpen>]
  module TestLogParser =

    open Parser

    let private dir = Directory.GetCurrentDirectory()

    let private fileExt = ".log"

    let private logFiles =
      let directory = DirectoryInfo(dir)
      directory.GetFiles("*udp"+fileExt)
      |> Array.mapi(fun i fi -> i,fi.Name)
      |> Map.ofArray

    let private listFiles _ =
      logFiles
      |> Map.iter(fun k v ->
        printfn "\t%d -> %s" k v
      )

    let private chooseFile () =
      let mutable selection = ""
      let mutable continueLoop = true
      while continueLoop do
        printfn "Select a file:"
        listFiles()
        printf "|> "
        let k = Console.ReadLine() |> int
        if logFiles |> Map.containsKey k |> not then
          printfn "Invalid option"
        else
          selection <- logFiles.[k]
          continueLoop <- false
      selection

    let executeParser = chooseFile >> processFile