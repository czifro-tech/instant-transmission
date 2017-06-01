namespace MCDTP.IO.MemoryMappedFile.Partition

  open System.IO
  open MCDTP.IO.MemoryStream
  open MCDTP.Logging

  [<AutoOpen>]
  module Expression =

    type PartitionBuilder() =

      member __.Return _ = PartitionConfiguration.Instance

    type PartitionBuilder with

      [<CustomOperation ("startAt", MaintainsVariableSpaceUsingBind = true)>]
      member __.StartAt (p,sp:int64) = PartitionConfiguration.set PartitionConfiguration.startPos_ sp p

      [<CustomOperation ("size", MaintainsVariableSpaceUsingBind = true)>]
      member __.Size (p,s:int64) = PartitionConfiguration.set PartitionConfiguration.size_ s p

      [<CustomOperation ("flushThreshold", MaintainsVariableSpaceUsingBind = true)>]
      member __.FlushThreshold (p,t:int64) = PartitionConfiguration.set PartitionConfiguration.flushThreshold_ t p

      [<CustomOperation ("replenishThreshold", MaintainsVariableSpaceUsingBind = true)>]
      member __.ReplenishThreshold (p,t:int64) = PartitionConfiguration.set PartitionConfiguration.replenishThreshold_ t p

      [<CustomOperation ("usingFileStream", MaintainsVariableSpaceUsingBind = true)>]
      member __.UsingFileStream (p,fs) = PartitionConfiguration.set PartitionConfiguration.fs_ fs p

    let partition = PartitionBuilder()
