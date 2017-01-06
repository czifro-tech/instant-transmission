namespace MUDT.Test

  open System
  open System.IO
  open Xunit
  open MUDT.IO
  open MUDT.Diagnostics
  open MUDT.Cryptography

  module MemoryMappedFileUnitTest =

    //[<Fact>]
    let ``Speed Test File Creation`` () =
      Helper.use4GBMemoryLimit()
      let testDir = ((Directory.GetParent("/Users/czifro/Developer")).GetDirectories()) |> Array.find(fun x -> x.Name = ".mudt")
      printfn "Directory: %s" testDir.FullName
      let testFileName = testDir.FullName + "/test" + (string(Array.length (testDir.GetFiles()))) + ".txt"
      let testFileSize = Helper.GBSize
      let startTime = DateTime.UtcNow
      MemoryMappedFile.createFileAsync testFileName (int64(testFileSize)) |> Async.RunSynchronously |> ignore
      let endTime = DateTime.UtcNow
      let res = String.Join("\n", [|
                                    sprintf "File Name => %s" testFileName
                                    sprintf "File Size => 1GB";
                                    sprintf "Time taken to create => %d ms" (endTime - startTime).Milliseconds
                                  |])
      printfn "%s" res

    let createConfig (partitionCount:int) (file':FileInfo) =
      let bufferCapacity' = ((file'.Length / int64(partitionCount)) / 100L)
      {
        hashStateConfig = HashStateConfig.BackloggingMd5Config(int(bufferCapacity'/10L))
        directory = file'.Directory.FullName;
        file = file';
        partitionCount = partitionCount
        bufferCapacity = bufferCapacity'
      }
    
    open MUDT.IO.MMFPartition

    let ``Speed Test Non Network File Transfer With No Integrity``() =
      printf "Enter full path of input file: "
      let if' = Console.ReadLine() // "/Users/czifro/Dropbox/Der Doppelganger copy.mp4"
      printf "Enter full path of output file: "
      let of' = Console.ReadLine() // "/Users/czifro/.mudt/Der Doppelganger copy.mp4"
      let testStart = DateTime.UtcNow
      Helper.use4GBMemoryLimit()
      let partitionCount = 12
      let input = 
        (MemoryMappedFile.tryGetFileInfo(if')).Value
        |> createConfig partitionCount
        |> MemoryMappedFile.partitionFile true
      let output = 
        (MemoryMappedFile.createFileAsync (of') input.fileInfo.Length)
        |> Async.RunSynchronously |> ignore
        (MemoryMappedFile.tryGetFileInfo(of')).Value
        |> createConfig partitionCount
        |> MemoryMappedFile.partitionFile false

      let asyncHandle (_in:MMFPartitionState) (_out:MMFPartitionState) = async {
        let! i' = MMFPartition.initializeReadBufferAsync _in
        let mutable i = i'
        let mutable o = _out
        while not <| feop(i) do
          let! (bytes, ni) = readFromBufferAsync i 500
          i <- ni
          let! no = writeToBufferAsync o bytes
          o <- no
        let! no = fullFlushBufferAsync o
        // dispose of file handle
        i <- dispose i
        o <- dispose no
        return o
      }
      let startTime = DateTime.UtcNow
      let asyncHandles = [| for i in 0..partitionCount-1 -> Async.StartChild(asyncHandle input.partitions.[i] output.partitions.[i]) |]
      let results = { output with partitions = asyncHandles |> Array.map(fun x -> (x |> Async.RunSynchronously) |> Async.RunSynchronously) }
      MemoryMappedFile.finalize results
      let endTime = DateTime.UtcNow
      // let pprint (p:MMFPartitionState) = p.PrintInfo()
      // printfn "input partitions:"
      // input.partitions |> Array.iter pprint
      // printfn "output partitions:"
      // output.partitions |> Array.iter pprint
      let size = results.partitions |> Array.sumBy(fun x -> x.bytesWrittenCounter)
      let testEnd = DateTime.UtcNow
      let res = String.Join("\n", [|
                                    sprintf "File Name => %s" input.fileInfo.Name;
                                    sprintf "File Size => %d bytes" size;
                                    sprintf "Transfer Time => %d ms" (endTime - startTime).Milliseconds
                                    sprintf "Test Time => %d ms" (testEnd - testStart).Milliseconds
                                  |])
      printfn "%s" res


    let testRunner op =
      match op with 
      | "1" -> ``Speed Test File Creation``()
      | "2" -> ``Speed Test Non Network File Transfer With No Integrity``()
      | "x" -> ``Speed Test File Creation``(); ``Speed Test Non Network File Transfer With No Integrity``()
      | _ -> failwith "Invalid option"