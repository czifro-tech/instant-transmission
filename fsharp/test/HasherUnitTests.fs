namespace MUDT.Test

  open System
  open System.Security.Cryptography
  open System.IO
  open MUDT.Cryptography
  open MUDT.Diagnostics
  open MUDT.IO
  open MUDT.Test

  module HasherUnitTests =
  
    let cacheCost = 40960L

    let printChecksum checksum =
      checksum
      |> Array.iter(fun c ->
        printfn "Checksum: %s" (c.ToString())
      )

    let doHash data state =
      let startTime = DateTime.UtcNow
      let startMem = CurrentProcessInfo.totalUsedPhysicalMemory()
      let _,dataHash = (Hasher.computeHash state data) |> Hasher.finalizeHash
      let endMem = CurrentProcessInfo.totalUsedPhysicalMemory()
      let endTime = DateTime.UtcNow
      //printChecksum dataHash
      (abs((startMem - endMem) - cacheCost)), (endTime - startTime).Milliseconds, (Array.length dataHash)*16

    let doIncrementalHashing(data:byte[],size:int) =
      let ih = Hasher.createIHHashState(size/100)
      //printfn "Incremental Hashing..."
      doHash data ih

    let doBacklogHashing(data:byte[],size:int) =
      let backlog = Hasher.createMd5HashState true (size/100)
      //printfn "Backlog Hashing..."
      doHash data backlog

    let doMd5Hashing(data:byte[],size:int) =
      let md5 = Hasher.createMd5HashState false size
      //printfn "MD5 Hashing..."
      doHash data md5

    let getValues (op:int) =
      match op with
      | 3 -> Helper.GB, Helper.GBSize
      | 2 -> Helper.MB, Helper.MBSize
      | 1 | _ -> Helper.KB, Helper.KBSize

    let ``Compare Memory Usage`` () =
      printfn "Running ``Compare Memory Usage``..."
      let iter = 100
      let data, size = getValues(3)
      let usageComparison(i) =
        let ih = doIncrementalHashing(data, size)
        let backlog = doBacklogHashing(data, size)
        let md5 = doMd5Hashing(data, size)
        printfn "finished iteration %d..." i
        ih, backlog, md5
      let sumTuples tuples =
        let fold (acc:((int64*int*int)*(int64*int*int)*(int64*int*int))) tuple =
          let innerFold (a:(int64*int*int)) b =
            let a1, a2, a3 = a
            let b1, b2, b3 = b
            (a1 + b1, a2 + b2, a3 + b3)
          let mutable accIhRes, accBacklogRes, accMd5Res = acc
          let ihRes, backlogRes, md5Res = tuple
          accIhRes <- innerFold accIhRes ihRes
          accBacklogRes <- innerFold accBacklogRes backlogRes
          accMd5Res <- innerFold accMd5Res md5Res
          accIhRes, accBacklogRes, accMd5Res
        let c = (0L,0,0)
        tuples |> Array.fold(fold) (c,c,c)
      let sumIh, sumBacklog, sumMd5 = 
        [| for i in 1..iter -> usageComparison(i) |] |> sumTuples
      let calcAverage (sum:(int64*int*int)) =
        let mem, time, len = sum
        mem / int64(iter), time / iter, len / iter
      let avgIhMem, avgIhTime, avgIhLen = calcAverage sumIh
      let avgBacklogMem, avgBacklogTime, _ = calcAverage sumBacklog
      let avgMd5Mem, avgMd5Time, _ = calcAverage sumMd5
      let res = String.Join("\n", [|
                                    sprintf "Data Length => 1GB";
                                    sprintf "Hash Length => %d bytes" avgIhLen;
                                    sprintf "Number of iterations => %d" iter;
                                    sprintf "Incremental Hash Memory Usage => %d" avgIhMem;
                                    sprintf "Incremental Hash Time => %d ms" avgIhTime;
                                    sprintf "Backlog Hash Memory Usage => %d" avgBacklogMem;
                                    sprintf "Backlog Hash Time => %d ms" avgBacklogTime;
                                    sprintf "MD5 Hash Memory Usage => %d" avgMd5Mem;
                                    sprintf "MD5 Hash Time => %d ms" avgMd5Time;
                                  |])
      printfn "%s" res

    let ``Speed and Memory Test``() =
      printfn "Running ``Speed and Memory Test``..."
      let iter = 100
      let data, size = getValues(2)
      let algs : HashAlgorithm[] = 
        [|
          MD5.Create();
          SHA1.Create();
          SHA256.Create();
          SHA384.Create();
          SHA512.Create();
        |]

      let simulator () : (int64*int*int)[] =
        let run (alg:HashAlgorithm) : (int64*int*int)=
          let st = DateTime.UtcNow
          let sm = CurrentProcessInfo.totalUsedPhysicalMemory()
          let hr = alg.ComputeHash(data)
          let em = CurrentProcessInfo.totalUsedPhysicalMemory()
          let et = DateTime.UtcNow
          (abs((sm - em) - cacheCost)), (et - st).Milliseconds, (Array.length hr)
        algs |> Array.map(run)
      let results = [| for i in 0..iter-1 -> simulator() |]
      let computeAvgs () =
        let fold (acc:(int64*int*int)[]) (a:(int64*int*int)[]) =
          let add a1 b1 =
            let a'1, a'2, a'3 = a1
            let b'1, b'2, b'3 = b1
            (a'1 + b'1), (a'2 + b'2), (a'3 + b'3)
          acc |> Array.map2(add) a
        results.[1..] 
        |> Array.fold(fold) results.[0]
        |> Array.map(fun (x:int64*int*int) -> 
          let i64, i, i' = x; 
          [|i64 / int64(iter); int64(i / iter); int64(i' / iter)|])
      let avgs = computeAvgs()
      let res = String.Join("\n", [|
                                    sprintf "Data Size => 1MB"
                                    sprintf "MD5    Time: %d, Memory Usage: %d, Hash Length: %d" avgs.[0].[1] avgs.[0].[0] avgs.[0].[2];
                                    sprintf "SHA1   Time: %d, Memory Usage: %d, Hash Length: %d" avgs.[1].[1] avgs.[1].[0] avgs.[1].[2];
                                    sprintf "SHA256 Time: %d, Memory Usage: %d, Hash Length: %d" avgs.[2].[1] avgs.[2].[0] avgs.[2].[2];
                                    sprintf "SHA384 Time: %d, Memory Usage: %d, Hash Length: %d" avgs.[3].[1] avgs.[3].[0] avgs.[3].[2];
                                    sprintf "SHA512 Time: %d, Memory Usage: %d, Hash Length: %d" avgs.[1].[1] avgs.[1].[0] avgs.[1].[2];
                                  |])
      printfn "%s" res

    let ``Matching Hash Test`` () =
      let fileName = "Der Doppelganger copy.mp4"
      let if' =  "/Users/czifro/Dropbox/" + fileName
      Helper.use4GBMemoryLimit()
      let file' = (MemoryMappedFile.tryGetFileInfo(if')).Value
      let bufferCapacity = int ((file'.Length / 8L) / 100L)
      let hashBlockSize = int ((file'.Length / 8L) / 8L)
      let mutable hashState1 = Hasher.createMd5HashState true hashBlockSize
      let mutable hashState2 = Hasher.createMd5HashState true hashBlockSize
      use stream = file'.OpenRead()
      printfn "Hashing File..."
      while stream.Position < stream.Length-1L do
        let buffer = MUDT.Utilities.TypeUtility.nullByteArray bufferCapacity
        let ret = stream.Read(buffer,0,bufferCapacity)
        printfn "Hashing %d bytes" ret
        hashState1 <- Hasher.computeHash hashState1 (buffer |> Array.take ret)
        hashState2 <- Hasher.computeHash hashState2 (buffer |> Array.take ret)
      let hashState1,checksum1 = Hasher.finalizeHash hashState1
      let hashState2,checksum2 = Hasher.finalizeHash hashState2
      let mismatch = Hasher.compareHash checksum1 checksum2
      printfn "checksum1: %A" checksum1
      printfn "checksum2: %A" checksum2
      printfn "mismatch: %A" mismatch


    let private tests() =
      [|
        ``Compare Memory Usage``;
        ``Speed and Memory Test``;
        ``Matching Hash Test``;
      |]

    let testRunner op =
      Helper.testRunner tests () op
