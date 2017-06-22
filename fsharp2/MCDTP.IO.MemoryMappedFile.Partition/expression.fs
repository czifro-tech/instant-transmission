namespace MCDTP.IO.MemoryMappedFile.Partition

  open System.IO
  open MCDTP.IO.MemoryStream
  open MCDTP.Logging

  [<AutoOpen>]
  module Expression =

    type PartitionBuilder() =

      member __.Return _ = PartitionConfiguration.Instance

    type PartitionBuilder with

      [<CustomOperation ("flushThreshold", MaintainsVariableSpaceUsingBind = true)>]
      member __.FlushThreshold (p,t:int64) = PartitionConfiguration.set PartitionConfiguration.flushThreshold_ t p

      [<CustomOperation ("replenishThreshold", MaintainsVariableSpaceUsingBind = true)>]
      member __.ReplenishThreshold (p,t:int64) = PartitionConfiguration.set PartitionConfiguration.replenishThreshold_ t p

      [<CustomOperation ("isReadOnly", MaintainsVariableSpaceUsingBind = true)>]
      member __.IsReadOnly(p) = PartitionConfiguration.set PartitionConfiguration.readOrWrite_ true p

      [<CustomOperation ("isWriteOnly", MaintainsVariableSpaceUsingBind = true)>]
      member __.IsWriteOnly(p) = PartitionConfiguration.set PartitionConfiguration.readOrWrite_ false p

    let partition = PartitionBuilder()
