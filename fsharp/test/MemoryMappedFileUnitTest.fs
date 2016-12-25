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

    let createConfig (file':FileInfo) =
      let bufferCapacity' = ((file'.Length / 4L) / 100L)
      {
        hashStateConfig = HashStateConfig.BackloggingMd5Config(int(bufferCapacity'/10L))
        directory = file'.Directory.FullName;
        file = file';
        partitionCount = 4
        bufferCapacity = bufferCapacity'
      }
    
    open MUDT.IO.MMFPartition

    [<Fact>]
    let ``Speed Test Non Network File Transfer With No Integrity``() =
      Helper.use4GBMemoryLimit()
      let input = 
        (MemoryMappedFile.tryGetFileInfo("/Users/czifro/Dropbox/Der Doppelganger copy.mp4")).Value
        |> createConfig
        |> MemoryMappedFile.partitionFile true
      let output = 
        (MemoryMappedFile.createFileAsync ("/Users/czifro/.mudt/Der Doppelganger copy.mp4") input.fileInfo.Length)
        |> Async.RunSynchronously |> ignore
        (MemoryMappedFile.tryGetFileInfo("/Users/czifro/.mudt/Der Doppelganger copy.mp4")).Value
        |> createConfig
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
        let! (bytes, ni) = drainBufferAsync i
        i <- ni
        let! no = writeToBufferAsync o bytes
        o <- no
        return! fullFlushBufferAsync o
      }
      let startTime = DateTime.UtcNow
      let asyncHandles = [| for i in 0..3 -> asyncHandle input.partitions.[i] output.partitions.[i] |]
      let results = asyncHandles |> Array.map(fun x -> x |> Async.RunSynchronously) : MMFPartitionState[]
      let endTime = DateTime.UtcNow
      // let pprint (p:MMFPartitionState) = p.PrintInfo()
      // printfn "input partitions:"
      // input.partitions |> Array.iter pprint
      // printfn "output partitions:"
      // output.partitions |> Array.iter pprint
      let size = results |> Array.sumBy(fun x -> x.bytesWrittenCounter)
      let res = String.Join("\n", [|
                                    sprintf "File Name => %s" input.fileInfo.Name;
                                    sprintf "File Size => %d bytes" size;
                                    sprintf "Time taken to create => %d ms" (endTime - startTime).Milliseconds
                                  |])
      printfn "%s" res
