open System
open System.IO
open System.Threading.Tasks

let (</>) f s = Path.Combine(f,s)

let printf fmt = Microsoft.FSharp.Core.Printf.printf fmt
let printfn fmt = Microsoft.FSharp.Core.Printf.printfn fmt
let failwithf fmt = Microsoft.FSharp.Core.Printf.failwithf fmt

let currentDirectory = __SOURCE_DIRECTORY__

let clientDataDirectory = currentDirectory </> "client"
let serverDataDirectory = currentDirectory </> "server"

type InputFile = InputFile of string
type OutputFile = OutputFile of string
type FileProcessTask = FileProcessTask of InputFile *  OutputFile

let createFileProcessTask directory inputName outputName =
  let input = InputFile (directory </> inputName)
  let output = OutputFile (directory </> outputName)
  FileProcessTask (input, output)

let createClientFileProcessTask inputName outputName =
  createFileProcessTask clientDataDirectory inputName  outputName

let createServerFileProcessTask inputName outputName =
  createFileProcessTask serverDataDirectory inputName outputName

let (<*>) f s = f,s

let array2ToTuple2 (arr:_[]) = arr.[0] <*> arr.[1]

let awaitTaskWithResult (task:Task<_>) =
  async {
    let awaiter = task.GetAwaiter()
    return awaiter.GetResult()
  }

let asyncReadCsv filename =
  async {
    use reader = (filename |> File.OpenText)
    let! fileData = reader.ReadToEndAsync() |> awaitTaskWithResult
    return (
      fileData.Split('\n')
      |> Array.map(fun line -> line.Split(',') |> array2ToTuple2)
    )
  }

let asyncSaveAsCsv filename (fileData:(string*string)[]) =
  async {
    let data =
      fileData
      |> Array.map(fun (col1,col2) -> sprintf "%s,%s" col1 col2)
      |> String.concat "\n"
    use writer = new StreamWriter(filename |> File.OpenWrite)
    return! writer.WriteAsync(data) |> Async.AwaitTask
  }

let groupRecordsBySecond (data:((DateTime)*int)[]) =
  let (++) f s = Array.append f s
  data
  |> Array.groupBy(fun (timeStamp,_) -> timeStamp)
  |> Array.fold(fun acc (timeStamp, data) ->
    if acc |> Array.isEmpty then [|(timeStamp, data)|]
    else
      let noThroughput t = [|t,0|]
      let (timeStamp',_) = Array.last acc
      let interval = (timeStamp - timeStamp').TotalSeconds |> int
      if interval = 1 then acc ++ [|(timeStamp, data)|]
      else
        let missingData =
          [|for i in 1..(interval-1) ->
            (timeStamp'.AddSeconds(float i), noThroughput (timeStamp'.AddSeconds(float i)))|]
        acc ++ missingData ++ [|(timeStamp, data)|]
  ) Array.empty

let computePerSecondThroughput (data:(DateTime*int)[]) =
  data
  |> groupRecordsBySecond
  // |> (fun x -> printfn "%A" x; x)
  // |> (fun x -> printfn "%A" (Array.get x 0); x)
  // |> (fun x -> printfn "%A" (Array.length x); x)
  |> Array.mapi(fun i (_,data) ->
    (i + 1), (data |> Array.sumBy snd)
  )

let processData (rawData:(string*string)[]) =
  let start = DateTime.MinValue
  rawData
  |> Array.map(fun (rawTimeStamp, rawBytesSent) ->
    let hms = (rawTimeStamp.Split('.')).[0].Split(':')
    let h,m,s = (int hms.[0]) - 5, int hms.[1], int hms.[2]
    let h = if h < 0 then h + 24 else h
    let t = (((start.AddHours(float h)).AddMinutes(float m)).AddSeconds(float s))
    // printfn "%A" (t.Ticks)
    (t, (int rawBytesSent))
  )
  |> computePerSecondThroughput
  |> Array.map(fun (second, throughput) -> (string second),(string throughput))

let normalizeBytesSent bytesSent (data:(DateTime*int)[]) =
  data
  |> Array.filter (snd >> ((=) bytesSent))

let asyncPerformFileProcessTask (task:FileProcessTask) =
  async {
    let (FileProcessTask (input, output)) = task
    let (InputFile input'),(OutputFile output') = input, output
    let! fileData = asyncReadCsv input'
    return! (
      fileData
      |> processData
      |> asyncSaveAsCsv output'
    )
  }

let fileProcessTasks =
  [
    // createClientFileProcessTask "sc-packet-capture.csv" "sc-throughput-per-second.csv"
    createClientFileProcessTask "dc-packet-capture.csv" "dc-throughput-per-second.csv"
    createClientFileProcessTask "qc-packet-capture.csv" "qc-throughput-per-second.csv"
    // createClientFileProcessTask "oc-packet-capture.csv" "oc-throughput-per-second.csv"
    createServerFileProcessTask "sc-packet-capture.csv" "sc-throughput-per-second.csv"
    createServerFileProcessTask "dc-packet-capture.csv" "dc-throughput-per-second.csv"
    createServerFileProcessTask "qc-packet-capture.csv" "qc-throughput-per-second.csv"
    // createServerFileProcessTask "oc-packet-capture.csv" "oc-throughput-per-second.csv"
  ]

fileProcessTasks
|> List.iter (asyncPerformFileProcessTask >> Async.RunSynchronously)