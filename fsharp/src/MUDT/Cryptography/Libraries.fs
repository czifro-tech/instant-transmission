namespace MUDT.Cryptography

  open System.Runtime.InteropServices

  module internal Libraries =

    [<DllImport("kernel32", CharSet = CharSet.Auto, SetLastError = true)>]
    extern bool SetDllDirectory(string lpPathName);

    let private rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)

    let NativeDir = 
    //   let rootPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
      sprintf "%s%c%s" rootPath System.IO.Path.DirectorySeparatorChar ".native" 

    // let private unixCrypto =
    //   sprintf "%s%cSystem.Security.Cryptography.Native.OpenSsl.dylib" nativeDir System.IO.Path.DirectorySeparatorChar

    [<Literal>]
    let CryptoNative = "System.Security.Cryptography.Native.OpenSsl"
      

    [<Literal>]
    let AppleCryptoNative = "System.Security.Cryptography.Native.Apple"