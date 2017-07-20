namespace MCDTP.Test

  open System
  open System.IO
  open System.Text.RegularExpressions

  [<AutoOpen>]
  module Bandwidth =

    type Transaction = DateTime * float

    let interval = TimeSpan(0,0,30)

    let (</>) s1 s2  = Path.Combine(s1,s2)

    let curDir = Directory.GetCurrentDirectory()
    let dataDir = curDir </> ".." </> ".." </> ".data"
    let clientDataDir = dataDir </> "client"
    let serverDataDir = dataDir </> "server"
    let fileExt = ".raw"
    let rawFilePattern = "*" + fileExt
    let outExt = ".dat"

    let dataPattern = @"^\d+:\d+:\d+.\d+\s\d+$"

    let rawFiles dir =
      printfn "%s" dir
      let dir' = DirectoryInfo(dir)
      dir'.GetFiles(rawFilePattern)
      |> Array.map(fun f ->
        match f.Name with
        | x when x.StartsWith("single") -> 0, dir </> f.Name
        | x when x.StartsWith("dual")   -> 1, dir </> f.Name
        | x when x.StartsWith("quad")   -> 2, dir </> f.Name
        | x when x.StartsWith("octa")   -> 3, dir </> f.Name
        | _ -> failwithf "Unsupported: %s" f.Name
      )
      |> Array.sortBy fst
      |> Array.map snd
    let rawClientFiles = rawFiles clientDataDir
    let rawServerFiles = rawFiles serverDataDir

    let filterUdpLines (line:string) =
      let m = Regex.Match(line,dataPattern)
      m.Success

    let toTransaction (line:string) =
      let comps = line.Split(' ')
      DateTime.Parse comps.[0], float comps.[1]

    let parseTrafficDump td =
      td
      |> Array.filter filterUdpLines
      |> Array.map toTransaction

    let computeBps (trans:Transaction[]) =
      let trans =
        if Array.isEmpty trans then
          [| "00:00:00.00000 0" |> toTransaction |]
        else trans
      // let groupBySeconds trans' =
      //   let groupBy (x:int) (t:Tansaction) =
      //     let dt = fst t
      //     if x = 0 then dt.Hour
      //     elif x = 1 then dt.Minute
      //     else dt.Second
      //   let rec recGroupBy level group =
      //     group
      //     |> snd
      //     |> Seq.groupBy (groupBy level)
      //     |> (fun groups ->
      //       if level >= 2 then groups
      //       else
      //         groups
      //         |> Seq.map (recGroupBy (level+1))
      //         |> Seq.collect id
      //     )
      //   (0,trans')
      //   |> recGroupBy 0
      trans
      |> Seq.ofArray
      //|> groupBySeconds
      |> Seq.groupBy(fun t ->
        let dt = fst t
        dt.Ticks / interval.Ticks
      )
      |> Seq.toArray
      |> Array.mapi(fun i group ->
        let trans' = snd group |> Seq.toArray
        let bytesPs =
          (trans'
          |> Array.sumBy snd) / (float interval.Seconds)
          |> (round >> int)
        (i+1)*interval.Seconds,bytesPs
      )

    let showStatistics label (allBps:(_*int)[]) =
      let averageBps =
        allBps
        |> Array.averageBy (snd >> float)
        |> (round >> int)
      let maxBps = allBps |> Array.maxBy snd |> snd

      printfn "\t%s -> Max Bps: %d, Average Bps: %d" label maxBps averageBps
      allBps

    let serializeBps x =
      let time,Bps = x
      sprintf "%d %d" time Bps

    let saveBps file allBps =
      (file,allBps)
      |> File.WriteAllLines

    let loadTrafficDump file =
      file
      |> File.ReadAllLines

    let processTrafficDump (file:string) =
      let label =
        match file with
        | x when x.Contains("/single-") -> "Single"
        | x when x.Contains("/dual-")   -> "Dual"
        | x when x.Contains("/quad-")   -> "Quad"
        | x when x.Contains("/octa-")   -> "Octa"
        | _ -> failwithf "Unsupported: %s" file
      let allBps =
        file
        |> loadTrafficDump
        |> parseTrafficDump
        |> computeBps
        |> showStatistics label
        |> (fun arr ->
          let (<-->) a1 a2 = Array.append a1 a2
          let l = Array.get arr ((Array.length arr)-1) |> fst
          [|0,0|] <--> arr <--> [|l,0|]
        )
        |> Array.map serializeBps
      let file = file.Replace(fileExt,outExt)
      saveBps file allBps

    let processHostData hostFiles =
      hostFiles
      |> Array.iter processTrafficDump

    let ``process`` () =
      [|
        "client",rawClientFiles
        "server",rawServerFiles
      |]
      |> Array.iter(fun host ->
        let name,files = host
        printfn "%s:" name
        processHostData files
      )

    let executeBandwidthTool = ``process``