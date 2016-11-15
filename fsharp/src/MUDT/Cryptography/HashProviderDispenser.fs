namespace MUDT.Cryptography

  open System
  open System.Runtime.InteropServices
  open MUDT.Cryptography

  module internal HashProviderDispenser =

    let private isWindows =
      RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
    let private isOSX = 
      RuntimeInformation.IsOSPlatform(OSPlatform.OSX)

    let private isLinux =
      RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
    
    let private tryCreate alg =
      let os = 
        RuntimeInformation.OSDescription
        |> OSPlatform.Create
      match os with
      | x when isOSX -> 
        printfn "Using Apple MD5"
        printfn "Exists: %b" (System.IO.File.Exists(Libraries.AppleCryptoNative))
        Apple.AppleHashProviderDispenser.createHashProvider (alg)
      | x when isLinux -> 
        printfn "Using Unix MD5"
        Unix.UnixHashProviderDispenser.createHashProvider (alg)
      | _ -> failwithf "OS not supported: %s" (os.ToString())

    let createHashProvider (alg:HashAlgorithmNames) =
      try 
        //Environment.
        printfn "Trying to create an MD5 hasher"
        //Libraries.SetDllDirectory(Libraries.NativeDir) |> ignore
        tryCreate alg
      with
      | :? System.DllNotFoundException ->
        printfn "Could not locate native lib, changing dll directory and trying again"
        //Libraries.SetDllDirectory(Libraries.NativeDir) |> ignore
        tryCreate alg