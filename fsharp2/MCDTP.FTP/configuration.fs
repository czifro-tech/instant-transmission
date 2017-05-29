namespace MCDTP.FTP

  // type FtpConfiguration =
  //   {
  //     consoleLogLevel    : MCDTP.Logging.LogLevel
  //     networkLogLevel    : MCDTP.Logging.LogLevel
  //     parallelism        : int
  //     memoryLimit        : int64
  //   }


  type FtpConfiguration =
    | FtpConfiguration of Map<string,obj>

    static member configuration_ =
      (fun (FtpConfiguration x) -> x), (FtpConfiguration)

    static member empty =
      FtpConfiguration (Map.empty)

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module FtpConfiguration =

    let t = 0