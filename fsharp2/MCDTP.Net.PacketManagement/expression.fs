namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility

  [<AutoOpen>]
  module Expression =

    type PacketManagerBuilder() =

      member __.Return() = PacketManagerConfiguration.Instance

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

      [<CustomOperation ("attachLogger", MaintainsVariableSpaceUsingBind = true)>]
      member __.AttachLogger(p,l:Logger) =
        PacketManagerConfiguration.set PacketManagerConfiguration.logger_ l p

    let packetManager = PacketManagerBuilder()