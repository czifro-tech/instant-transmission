namespace MUDT.Test

open System
open Xunit
open MUDT.Cryptography
open MUDT.Diagnostics

module HasherUnitTests =
  
  let cacheCost = 40960L
  let createDefaultIHHashState() =
    Hasher.createIHHashState(10)

  let smallData = 1024 // 1KB
  let mediumData = smallData * 1000 // 1MB
  let largeData = mediumData * 1000 // 1GB 

  let createData (size:int) =
    let rand = new Random(5); // seed to create same data
    [| for i in 0..size -> byte (rand.Next()) |]

  let staticSmallData = createData(smallData)
  let staticMediumData = createData(mediumData)
  let staticLargeData = createData(largeData)

  let doHash data state =
    let startTime = DateTime.UtcNow
    let startMem = CurrentProcessInfo.totalUsedPhysicalMemory()
    let dataHash = (Hasher.computeHash state data) |> Hasher.finalizeHash
    let endMem = CurrentProcessInfo.totalUsedPhysicalMemory()
    let endTime = DateTime.UtcNow
    // printfn "Hash length: %d" (Array.length dataHash)
    // printfn "Memory used: %d bytes" (abs(startMem - endMem))
    (abs((startMem - endMem) - cacheCost)), (endTime - startTime).Milliseconds, (Array.length dataHash)

  let doIncrementalHashing(data:byte[],size:int) =
    let ih = Hasher.createIHHashState(size/100)
    doHash data ih

  let doBacklogHashing(data:byte[],size:int) =
    let backlog = Hasher.createMd5HashState true (size/100)
    doHash data backlog

  let doMd5Hashing(data:byte[],size:int) =
    let md5 = Hasher.createMd5HashState false size
    doHash data md5

  [<Fact>]
  let ``Compare Memory Usage`` () =
    let iter = 100
    let data, size = staticMediumData, mediumData
    let usageComparison(i) =
      let ih = doIncrementalHashing(data, size)
      let backlog = doBacklogHashing(data, size)
      let md5 = doMd5Hashing(data, size)
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
      [| for i in 0..iter -> usageComparison(i) |] |> sumTuples
    let calcAverage (sum:(int64*int*int)) =
      let mem, time, len = sum
      mem / int64(iter), time / iter, len / iter
    let avgIhMem, avgIhTime, avgIhLen = calcAverage sumIh
    let avgBacklogMem, avgBacklogTime, _ = calcAverage sumBacklog
    let avgMd5Mem, avgMd5Time, _ = calcAverage sumMd5
    let res = String.Join("\n", [|
                                  sprintf "Data Length => 1MB";
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
    Assert.Equal(true, true)
    // CurrentProcessInfo.printProcessMemoryInfo()
    // let data = createData(mediumData)
    // CurrentProcessInfo.printProcessMemoryInfo()


  [<Fact>]
  let ``Test at once verses in stages should not match`` () =
    let ih1 = createDefaultIHHashState()
    let ih2 = createDefaultIHHashState()
    let data = createData(smallData)
    let hash1 = (Hasher.computeHash ih1 data) |> Hasher.finalizeHash
    let doHash2 (d:byte[]) s e (state:HashState) =
       d.[s..e]
       |> Hasher.computeHash state
    let offset = (Array.length data) / 4
    let hash2 = 
      ih2
      |> doHash2 data (0) (offset-1)
      |> doHash2 data (offset) (2*offset-1)
      |> doHash2 data (2*offset) (2*offset-1)
      |> doHash2 data (3*offset) (4*offset-1)
      |> Hasher.finalizeHash

    let mutable dontMatch = false
    if (Array.length hash1) = (Array.length hash2) then
      Array.iter2(fun a b -> if a <> b then dontMatch <- true) hash1 hash2
    else
      dontMatch <- true
    Assert.Equal(true, dontMatch)
