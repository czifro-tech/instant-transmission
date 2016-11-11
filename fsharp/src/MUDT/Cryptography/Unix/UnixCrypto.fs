#nowarn "9"
namespace MUDT.Cryptography.Unix

  open System
  open System.Security
  open System.Security.Cryptography
  open System.Runtime.InteropServices
  open MUDT.Cryptography
  open MUDT.Utilities

  [<AllowNullLiteral>]
  type internal SafeEvpMdCtxHandle() =
    inherit SafeHandle(IntPtr.Zero, true)
    override x.ReleaseHandle() =
      true
    override x.IsInvalid = true

  [<Sealed>]
  type OpenSslCryptographicException(errorCode:int, message:string) =
    inherit CryptographicException(message)
    let mutable ec = errorCode

    member x.HResult 
      with get() = ec
      and set(value) = ec <- value

  module internal UnixCrypto =
    
    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpMdCtxCreate")>]
    extern SafeEvpMdCtxHandle EvpMdCtxCreate(IntPtr typ)

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpMdCtxDestroy")>]
    extern void EvpMdCtxDestroy(IntPtr ctx)

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpDigestReset")>]
    extern int EvpDigestReset(SafeEvpMdCtxHandle ctx, IntPtr typ)

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpDigestUpdate")>]
    extern int EvpDigestUpdate(SafeEvpMdCtxHandle ctx, byte* d, int cnt)

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpDigestFinalEx")>]
    extern int EvpDigestFinalEx(SafeEvpMdCtxHandle ctx, byte* md, UInt32& s)

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpMdSize")>]
    extern int EvpMdSize(IntPtr md);

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_EvpMd5")>]
    extern IntPtr EvpMd5()

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_GetMaxMdSize")>]
    extern int private GetMaxMdSize()

    let EVP_MAX_MD_SIZE = GetMaxMdSize()

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_ErrGetErrorAlloc")>]
    extern UInt64 ErrGetErrorAlloc([<MarshalAs(UnmanagedType.Bool)>] bool& isAllocFailure);

    [<DllImport(Libraries.CryptoNative, EntryPoint = "CryptoNative_ErrErrorStringN")>]
    extern void ErrErrorStringN(UInt64 e, byte* buf, int len);

    let ErrErrorString(error:UInt64) =
      let buffer = TypeUtility.nullByteArray 1024
      let buf = fixed buffer
      let deref (ptr:nativeptr<byte>) =
        NativeInterop.NativePtr.toNativeInt(ptr)
        |> NativeInterop.NativePtr.ofNativeInt
        |> NativeInterop.NativePtr.read<IntPtr>
      ErrErrorStringN(error, buf, buffer.Length)
      Marshal.PtrToStringAnsi(deref buf)

    let CreateOpenSslCryptographicException() =
      let mutable isAllocFailure = false
      let mutable error = ErrGetErrorAlloc(&isAllocFailure)
      let mutable lastRead = error
      let mutable lastIsAllocFailure = isAllocFailure

      while lastRead <> 0uL do
        error <- lastRead
        isAllocFailure <- lastIsAllocFailure
        lastRead <- ErrGetErrorAlloc(&lastIsAllocFailure)

      if (error = 0uL) then
        (new CryptographicException() :> SystemException)
      elif isAllocFailure then
        (new OutOfMemoryException() :> SystemException)
      else
        assert (error <= uint64(UInt32.MaxValue))
        (new OpenSslCryptographicException(int(error), ErrErrorString(error)) :> SystemException)

    let CheckValidOpenSslHandle(handle:SafeHandle) =
      if (isNull(handle) || handle.IsInvalid) then
        raise (CreateOpenSslCryptographicException())
  
  [<System.Security.SecurityCritical>]
  [<AllowNullLiteral>]
  type internal UnixSafeEvpMdCtxHandle() =
    inherit SafeEvpMdCtxHandle()
    
    [<System.Security.SecurityCritical>]
    override x.ReleaseHandle() =
      UnixCrypto.EvpMdCtxDestroy(x.handle)
      true

    override x.IsInvalid 
      with get() = x.handle = IntPtr.Zero