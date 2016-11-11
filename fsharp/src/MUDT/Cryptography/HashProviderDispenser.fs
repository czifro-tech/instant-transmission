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

    let createHashProvider (alg:HashAlgorithmNames) = 
      let os = 
        RuntimeInformation.OSDescription
        |> OSPlatform.Create
      match os with
      | x when isOSX -> 
        printf "Using Apple MD5"
        Apple.AppleHashProviderDispenser.createHashProvider (alg)
      | x when isLinux -> 
        printf "Using Unix MD5"
        Unix.UnixHashProviderDispenser.createHashProvider (alg)
      | _ -> failwithf "OS not supported: %s" (os.ToString())