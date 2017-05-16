namespace MUDT.Utilities

  module SemaphorUtility =

    let makeLock (locker:obj) =
      System.Threading.Monitor.Enter(locker)
      { new System.IDisposable with
          member x.Dispose() =
            System.Threading.Monitor.Exit(locker) }