namespace MUDT.Test

open System
open Xunit
open MUDT.Cryptography
open MUDT.Diagnostics

module HasherUnitTests =
  
  let cacheCost = 40960L

  let doHash data state =
    let startTime = DateTime.UtcNow
    let startMem = CurrentProcessInfo.totalUsedPhysicalMemory()
    let dataHash = (Hasher.computeHash state data) |> Hasher.finalizeHash
    let endMem = CurrentProcessInfo.totalUsedPhysicalMemory()
    let endTime = DateTime.UtcNow
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

  let getValues (op:int) =
    match op with
    | 3 -> Helper.GB, Helper.GBSize
    | 2 -> Helper.MB, Helper.MBSize
    | 1 | _ -> Helper.KB, Helper.KBSize

  let ``Compare Memory Usage`` () =
    let iter = 100
    let data, size = getValues(2)
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

  let private tests() =
    [|
      ``Compare Memory Usage``
    |]

  let testRunner op =
    Helper.testRunner tests () op
