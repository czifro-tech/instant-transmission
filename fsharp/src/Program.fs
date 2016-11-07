// Learn more about F# at http://fsharp.org

open System
open System.IO
open MUDT.IO

let testFileChecksum =
  let mutable cs1 = 
    {
      MemoryMappedFileChecksumState.DefaultInstance with
        checksum = [||]
    }
  let bytes = "This is going to be an extraordinarily long string to test hashing segments"B
  cs1 <- MemoryMappedFileChecksum.update cs1 bytes
  cs1 <- MemoryMappedFileChecksum.finalAndStore cs1

  let mutable cs2 = 
    {
      MemoryMappedFileChecksumState.DefaultInstance with
        checksum = [||]
    }
  let offset = int((Array.length bytes) / 4)
  let segs = 
    [| 
      bytes.[0..(offset-1)];
      bytes.[offset..(2*offset-1)];
      bytes.[(2*offset)..(3*offset-1)];
      bytes.[(3*offset)..]; 
    |]
  segs |> Array.iter(fun seg -> cs2 <- MemoryMappedFileChecksum.update cs2 seg)
  cs2 <- MemoryMappedFileChecksum.finalAndStore cs2

  printfn "cs1 length %d" (Array.length cs1.checksum)
  printfn "cs2 length %d" (Array.length cs2.checksum)

[<EntryPoint>]
let main argv = 
//   printfn "Hello World!"
//   printfn "%A" argv
  testFileChecksum
  0 // return an integer exit code