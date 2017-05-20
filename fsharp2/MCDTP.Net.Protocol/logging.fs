namespace MCDTP.Net.Protocol

  open MCDTP.Logging

  (* InternalLogging

     This is just a helper module for configuring the shared logger
     that will be used by both Tcp and Udp modules. Internal use only. *)
  module internal InternalLogging =

    let internalLogger =
      let config =
        loggerConfig {
          useConsole
          loggerId "TCP/UDP Parser/Composer"
          logLevel LogLevel.Error
        }
      match Logger.return_ config with
      | ConsoleLogger logger -> logger
      | _ -> failwith "Wrong logger was created!"