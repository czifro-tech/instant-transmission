namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility

  [<AutoOpen>]
  module Expression =

    type PacketManagerBuilder() =

      member __.Return _ = PacketManagerConfiguration.Instance

      member __.Bind (p1:PacketManagerConfiguration,_) = p1

    type PacketManagerBuilder with

      [<CustomOperation ("clientMode", MaintainsVariableSpaceUsingBind = true)>]
      member __.ClientMode(p) =
        PacketManagerConfiguration.set PacketManagerConfiguration.isServer_ false p

      [<CustomOperation ("serverMode", MaintainsVariableSpaceUsingBind = true)>]
      member __.ServerMode(p) =
        PacketManagerConfiguration.set PacketManagerConfiguration.isServer_ true p

      [<CustomOperation ("performOnBuffer", MaintainsVariableSpaceUsingBind = true)>]
      member __.PerformOnBuffer(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.bufferAction_ a p

      [<CustomOperation ("useRecoveryAction", MaintainsVariableSpaceUsingBind = true)>]
      member __.UseRecoveryAction(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.recoveryAction_ a p

      [<CustomOperation ("whenPacketDroppedOrRecovered", MaintainsVariableSpaceUsingBind = true)>]
      member __.WhenPacketDroppedDetection(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.packetAction_ a p

      [<CustomOperation ("whenRetransmitMode", MaintainsVariableSpaceUsingBind = true)>]
      member __.WhenRetransmitMode(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.retransmitModeAction_ a p

      [<CustomOperation ("withRetransmitFrequency", MaintainsVariableSpaceUsingBind = true)>]
      member __.RetransmitFrequency(p,i:int) =
        PacketManagerConfiguration.set PacketManagerConfiguration.retransmitInterval_ i p

      [<CustomOperation ("onFinished", MaintainsVariableSpaceUsingBind = true)>]
      member __.OnFinished(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.finishedAction_ a p

      [<CustomOperation ("onSuccess", MaintainsVariableSpaceUsingBind = true)>]
      member __.OnSuccess(p,a:PMAction) =
        PacketManagerConfiguration.set PacketManagerConfiguration.successAction_ a p

    let packetManager = PacketManagerBuilder()