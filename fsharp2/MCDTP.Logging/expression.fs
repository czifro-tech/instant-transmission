namespace MCDTP.Logging

  [<AutoOpen>]
  module Expression =

    [<RequireQualifiedAccess>]
    [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
    module Logger =

      let init() =
        LoggerConfiguration.empty

      let set key value (config:LoggerConfiguration) =
        let unwrap,wrap = LoggerConfiguration.configuration_
        let innerConfig = unwrap config
        if Map.containsKey key innerConfig then
          failwith "Logger config field '%s' is already set"
        let innerConfig = Map.add key value innerConfig
        wrap innerConfig

      let ofConfig (config:LoggerConfiguration) =
        let getField key config' =
          match Map.tryFind key config' with
          | Some x -> x
          | _ -> failwith "Missing required field in logger config: '%s'"
        let unwrap,_ = LoggerConfiguration.configuration_
        let innerConfig = unwrap config
        let isConsole = ((getField LoggerConfiguration.isConsole_ innerConfig) :?> bool)
        let loggerId = ((getField LoggerConfiguration.loggerId_ innerConfig) :?> string)
        let logLevel = ((getField LoggerConfiguration.logLevel_ innerConfig) :?> LogLevel)

        if isConsole then
          let consoleLogger = new ConsoleLogger()
          consoleLogger.LoggerId <- loggerId
          consoleLogger.LogLevel <- logLevel
          ConsoleLogger consoleLogger
        else
          let networkLogger = new NetworkLogger(loggerId)
          networkLogger.LogLevel <- logLevel
          networkLogger.ThroughputInterval <- ((getField LoggerConfiguration.throughputInterval_ innerConfig) :?> System.TimeSpan)
          NetworkLogger networkLogger

    type LoggerBuilder() =

      member __.Return _ = Logger.init()

      member __.Bind (l1:LoggerConfiguration,f) = f l1

    type LoggerBuilder with

      [<CustomOperation ("useConsole", MaintainsVariableSpaceUsingBind = true)>]
      member inline __.UseConsole(l) = Logger.set LoggerConfiguration.isConsole_ true l

      [<CustomOperation ("networkOnly", MaintainsVariableSpaceUsingBind = true)>]
      member inline __.NetworkOnly(l) = Logger.set LoggerConfiguration.isConsole_ false l

      [<CustomOperation ("loggerId", MaintainsVariableSpaceUsingBind = true)>]
      member inline __.LoggerId(l,id) = Logger.set LoggerConfiguration.loggerId_ id l

      [<CustomOperation ("logLevel", MaintainsVariableSpaceUsingBind = true)>]
      member inline __.LogLevel(l,ll) = Logger.set LoggerConfiguration.logLevel_ ll l

      [<CustomOperation ("throughputInterval", MaintainsVariableSpaceUsingBind = true)>]
      member inline __.ThroughputInterval(l,i) = Logger.set LoggerConfiguration.throughputInterval_ i l

    let loggerConfig = LoggerBuilder()
