namespace MCDTP.Logging

  type LoggerConfiguration =
    | LoggerConfiguration of Map<string,obj>

    static member configuration_ =
      (fun (LoggerConfiguration x) -> x), (LoggerConfiguration)

    static member empty =
      LoggerConfiguration (Map.empty)

    static member isConsole_ = "isConsole"

    static member loggerId_ = "loggerId"

    static member logLevel_ = "logLevel"

    static member throughputInterval_ = "throughputInterval"