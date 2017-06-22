namespace MCDTP.Net.PacketManagement

  open MCDTP.Logging
  open MCDTP.Utility
  open MCDTP.Net.Protocol

  type PMAction =
    //       interval   action
    | Flush of int * (byte[]->bool->unit)
    //           interval   action
    | Replenish of int * (int->(int64*byte[]))
    | Write of (byte[]->int64->unit)
    | Fetch of (int64->byte[])
    //                  report          ack
    | PacketAction of (int64->unit) * (int64->unit)
    | RetransmitModeAction of (UdpPacket->unit)
    | FinishedAction of (unit->unit)
    | SuccessAction of (unit->unit)
    | NoAction

  type PacketManagerConfiguration =
    {
      isServer              : bool

      // main buffer
      // server -> Replenish, client -> Flush
      bufferAction          : PMAction

      // ensure late packets don't end up in primary buffer
      // should not be set by user
      initSeqNum            : int64

      // packet recovery
      // server -> Fetch, client -> Write
      recoveryAction        : PMAction
      // server -> NoAction, client -> PacketAction
      packetAction          : PMAction

      // retransmission
      retransmitModeAction  : PMAction
      retransmitInterval    : int

      // not set by user
      // server -> FinishedAction, client -> NoAction
      finishedAction        : PMAction
      // server -> SuccessAction, client -> NoAction
      successAction         : PMAction

      // expected logger is NetworkLogger
      logger                : Logger
    }

    static member Instance =
      {
        isServer              = true
        bufferAction          = PMAction.NoAction
        initSeqNum            = 0L
        recoveryAction        = PMAction.NoAction
        packetAction          = PMAction.NoAction
        retransmitModeAction  = PMAction.NoAction
        retransmitInterval    = 0
        finishedAction        = PMAction.NoAction
        successAction         = PMAction.NoAction
        logger                = Logger.NoLogger
      }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module PacketManagerConfiguration =

    let isServer_ = "isServer"
    let bufferAction_ = "bufferAction"
    let recoveryAction_ = "recoveryAction"
    let packetAction_ = "packetAction"
    let retransmitModeAction_ = "retransmitModeAction"
    let finishedAction_ = "finishedAction"
    let successAction_ = "successAction"
    let retransmitInterval_ = "retransmitInterval"

    let set k (o:obj) p =
      match k with
      | _ when k = isServer_              -> { p with isServer = (o :?> bool) }
      | _ when k = bufferAction_          -> { p with bufferAction = (o :?> PMAction) }
      | _ when k = recoveryAction_        -> { p with recoveryAction = (o :?> PMAction) }
      | _ when k = packetAction_          -> { p with packetAction = (o :?> PMAction) }
      | _ when k = retransmitModeAction_  -> { p with retransmitModeAction = (o:?> PMAction) }
      | _ when k = retransmitInterval_    -> { p with retransmitInterval = (o :?> int) }
      | _ when k = finishedAction_        -> { p with finishedAction = (o :?> PMAction) }
      | _ when k = successAction_         -> { p with successAction = (o :?> PMAction) }
      | _ -> failwithf "Unknown key '%s'" k
