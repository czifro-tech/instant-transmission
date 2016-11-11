namespace MUDT.Diagnostics

  open System.Diagnostics
  open MUDT.Diagnostics

  module CurrentProcessInfo =
    let private currentProcess =
      let cacheKey = "current-process"
      let cachedProcess = Cache.tryGetCachedItem cacheKey
      let mutable proc = null : Process
      if (cachedProcess.IsNone) then
        proc <- Process.GetCurrentProcess()
        Cache.cacheItem cacheKey proc
      else
        proc <- (cachedProcess.Value :?> Process)
      proc
    
    let totalAvailableMemory =
      currentProcess.VirtualMemorySize64

    let totalUsedMemory =
      currentProcess.PeakVirtualMemorySize64

    let totalFreeMemory =
      totalAvailableMemory - totalUsedMemory