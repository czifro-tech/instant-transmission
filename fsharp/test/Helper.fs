namespace MUDT.Test


  open System
  open MUDT.Diagnostics

  module Helper =

    let createData(size:int) =
      let rand = new Random(5);
      [| for i in 0..size-1 -> byte(rand.Next()) |]

    let KBSize = 1024;
    let MBSize = KBSize * 1000;
    let GBSize = MBSize * 1000;

    let KB = createData(KBSize)
    let MB = createData(MBSize)
    let GB = createData(GBSize)

    /// This is used for MMF
    let use4GBMemoryLimit() =
      CurrentProcessInfo.setMemoryLimit(int64(GBSize) * 4L)

    let timeMemTickTock() =
      DateTime.UtcNow, CurrentProcessInfo.totalUsedPhysicalMemory()

    let testRunner tests testArg op =
      match op with
      | "x" -> () |> tests |> Array.iter(fun test -> test testArg)
      | _ -> 
        if Array.length (() |> tests) < int(op) then
          failwith "Invalid option"
        (tests()).[int(op)-1] testArg