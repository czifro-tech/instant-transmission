namespace MUDT

  open System
  open System.IO

  module IO =

    type PartitionedFileStream() =

      member x.SetFileName(filename:string) =
        0 |> ignore
      
      member x.SetNumberOfPartitions(partitionCount:int) =
        0 |> ignore

      member x.Open() =
        0 |> ignore

      member x.Open(filename:string, partitionCount:int) =
        x.SetFileName(filename)
        x.SetNumberOfPartitions(partitionCount)
        x.Open()
      