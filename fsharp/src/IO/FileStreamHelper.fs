namespace MUDT.IO

  open System
  open System.IO
  open System.Runtime.InteropServices
  open Microsoft.Win32.SafeHandles
  open MUDT.IO
  open MUDT.Diagnostics.CurrentProcessInfo

  [<Flags>]
  type internal OpenFlags =
    // Access modes (mutually exclusive
    | O_RDONLY      = 0x0000
    | O_WRONLY      = 0x0001
    | O_RDWR        = 0x0002
  
    // Flags (combinable)
    | O_CLOEXEC     = 0x0010
    | O_CREAT       = 0x0020
    | O_EXCL        = 0x0040
    | O_TRUNC       = 0x0080
    | O_SYNC        = 0x0100

    // Platform Specific
    // | O_DIRECT   = 0x2000 // Linux no buffering
    // | O_NONBLOCK = 0x3000 // OSX no buffering

  module internal OpenFlagsHelper =
    let createFrom (mode:FileMode) (access:FileAccess) (options:FileOptions) =
      let fromMode =
        match mode with
        // | FileMode.Open ->
        | FileMode.Append | FileMode.OpenOrCreate -> OpenFlags.O_CREAT
        | FileMode.Create -> OpenFlags.O_CREAT ||| OpenFlags.O_TRUNC
        | FileMode.CreateNew -> OpenFlags.O_CREAT ||| OpenFlags.O_EXCL
        | FileMode.Truncate -> OpenFlags.O_TRUNC

      let fromAccess =
        match access with
        | FileAccess.Read -> OpenFlags.O_RDONLY
        | FileAccess.ReadWrite -> OpenFlags.O_RDWR
        | FileAccess.Write -> OpenFlags.O_WRONLY
        
      let fromOptions =
        let defaultFlag : OpenFlags = LanguagePrimitives.EnumOfValue 0
        let zeroOp : FileOptions = LanguagePrimitives.EnumOfValue 0
        if (options &&& FileOptions.WriteThrough) <> zeroOp then OpenFlags.O_SYNC
        else defaultFlag

      fromMode ||| fromAccess ||| fromOptions

  [<Flags>]
  type internal Permissions =
    | S_IRUSR = 0x100
    | S_IWUSR = 0x80
    | S_IXUSR = 0x40
    | S_IRWXU = 0x1c0

    | S_IRGRP = 0x20
    | S_IWGRP = 0x10
    | S_IXGRP = 0x8
    | S_IRWXG = 0x38

    | S_IROTH = 0x4
    | S_IWOTH = 0x2
    | S_IXOTH = 0x1
    | S_IRWXO = 0x7

    | Mask    = 0x1ff 


  type internal FileAdvice =
    | POSIX_FADV_NORMAL       = 0    (* no special advice, the default value *)
    | POSIX_FADV_RANDOM       = 1    (* random I/O access *)
    | POSIX_FADV_SEQUENTIAL   = 2    (* sequential I/O access *)
    | POSIX_FADV_WILLNEED     = 3    (* will need specified pages *)
    | POSIX_FADV_DONTNEED     = 4    (* don't need the specified pages *)
    | POSIX_FADV_NOREUSE      = 5    (* data will only be acessed once *)

  module internal FileAdviceHelper =
    let createFrom (options:FileOptions) =
      match options with 
      | FileOptions.RandomAccess -> FileAdvice.POSIX_FADV_RANDOM
      | FileOptions.SequentialScan -> FileAdvice.POSIX_FADV_SEQUENTIAL
      | _ -> FileAdvice.POSIX_FADV_NORMAL

  type internal Error =
    | SUCCESS = 0 // Currently only thing supported by dotnet core

  [<Struct>]
  type internal ErrorInfo = 
    {
      error : Error;
      rawError : int;
    }

    static member Default =
      {
        error = Error.SUCCESS;
        rawError = 0;
      }

  type internal LockOperations =
    | LOCK_SH = 1 (* shared lock *)
    | LOCK_EX = 2 (* exclusive lock *)
    | LOCK_NB = 4 (* don't block when locking *)
    | LOCK_UN = 8 (* unlock *)

  module internal FileStreamHelper =

    [<Literal>]
    let AppleNative = "MUDT.Native.Apple"

    [<LiteralAttribute>]
    let LinuxNative = "MUDT.Native.Linux"

    // platform specific
    [<DllImport(AppleNative, EntryPoint = "MUDTNative_Open", SetLastError = true)>]
    extern SafeFileHandle OpenApple(string filename, OpenFlags flags, int mode);

    [<DllImport(LinuxNative, EntryPoint = "MUDTNative_Open", SetLastError = true)>]
    extern SafeFileHandle OpenLinux(string filename, OpenFlags flags, int mode);

    // common, just use Linux native lib for common functions
    //  alternative is have duplicate stubs that use platform
    //  specific libs that call common functions
    // That does not make sense, so this will do.
    [<DllImport(LinuxNative, EntryPoint = "MUDTNative_PosixFAdvise", SetLastError = false)>]
    extern int PosixFAdvise(SafeFileHandle fd, Int64 offset, Int64 length, FileAdvice advice);

    [<DllImport(LinuxNative, EntryPoint = "MUDTNative_FLock", SetLastError = false)>]
    extern int FLock(SafeFileHandle fd, LockOperations operation);

    // Currently *nix implementation does nothing
    let private checkFileCall(result:int64) =
      result

    let private openHandle (filename:string) (flags:OpenFlags) (mode:int) =
      match getPlatform() with
      | MacOS -> Some (OpenApple(filename, flags, mode))
      | Linux -> Some (OpenLinux(filename, flags, mode))
      | _ -> None

    let private commonOpenPermissions = 
      Permissions.S_IRWXU ||| Permissions.S_IRGRP ||| 
        Permissions.S_IROTH ||| Permissions.S_IWGRP ||| Permissions.S_IWOTH

    let private commonFileOptions : FileOptions =
      FileOptions.RandomAccess ||| FileOptions.Asynchronous

    let private commonOpenFlags (fileOptions:FileOptions) (access:FileAccess) =

      let flags = OpenFlagsHelper.createFrom FileMode.OpenOrCreate access commonFileOptions

      match getPlatform() with
      | MacOS -> flags ||| LanguagePrimitives.EnumOfValue 0x3000
      | Linux -> flags ||| LanguagePrimitives.EnumOfValue 0x2000
      
    let private init (handle:SafeFileHandle) (share:FileShare) =
      let lockOperations =
        if share = FileShare.None then LockOperations.LOCK_EX
        else LockOperations.LOCK_SH

      if FLock(handle, lockOperations ||| LockOperations.LOCK_NB) < 0 then
        raise (Exception("Failed to establish lock"))
      checkFileCall(int64(PosixFAdvise(handle, 0L, 0L, FileAdviceHelper.createFrom(commonFileOptions)))) |> ignore

    let private openFromHandle (handleOption:SafeFileHandle option) (share:FileShare) (bufferSize:int) =
      if handleOption.IsNone then
        raise (Exception("Platform not supported"))
      else
        init handleOption.Value share
        new FileStream(handleOption.Value, FileAccess.Read, bufferSize, true)

    let private openFileInReadMode (filename:string) (bufferSize:int) =
      let openFlags = commonOpenFlags commonFileOptions FileAccess.Read

      let handleOption = openHandle filename openFlags (LanguagePrimitives.EnumToValue commonOpenPermissions)
      openFromHandle handleOption FileShare.Read bufferSize

    let private openFileInWriteMode (filename:string) (bufferSize:int) =
      let openFlags = commonOpenFlags commonFileOptions FileAccess.Write

      let handleOption = openHandle filename openFlags (LanguagePrimitives.EnumToValue commonOpenPermissions)
      openFromHandle handleOption FileShare.Write bufferSize

    let private getUnixFileStream (filename:string) (access:FileAccess) (bufferSize:int) =
      match access with
      | FileAccess.Write -> openFileInWriteMode filename bufferSize
      | _ -> openFileInReadMode filename bufferSize

    let getPlatformSpecificFileStream (filename:string) (access:FileAccess) (bufferSize:int) =
      match getPlatform() with
      | Windows ->
        let fileOptions = commonFileOptions ||| LanguagePrimitives.EnumOfValue 0x20000000 // no buffering on Windows
        let share = if access = FileAccess.Read then FileShare.Read else FileShare.Write
        new FileStream(filename, FileMode.OpenOrCreate, access, share, bufferSize, fileOptions)
      | _ -> getUnixFileStream filename access bufferSize

