namespace MUDT.Cryptography.Apple

  open System
  open System.Runtime.InteropServices
  open MUDT.Cryptography

  [<AllowNullLiteral>]
  type internal SafeDigestCtxHandle() =
    inherit SafeHandle(IntPtr.Zero, true)
    override x.ReleaseHandle() =
      true
    override x.IsInvalid = true

  module internal AppleCrypto =

    [<DllImport(Libraries.AppleCryptoNative, EntryPoint = "AppleCryptoNative_DigestFree")>]
    extern void DigestFree(IntPtr handle);

    [<DllImport(Libraries.AppleCryptoNative, EntryPoint = "AppleCryptoNative_DigestCreate")>]
    extern SafeDigestCtxHandle DigestCreate(PAL_HashAlgorithm algorithm, int& cbDigest);

    [<DllImport(Libraries.AppleCryptoNative, EntryPoint = "AppleCryptoNative_DigestUpdate")>]
    extern int DigestUpdate(SafeDigestCtxHandle ctx, byte* pbData, int cbData);

    [<DllImport(Libraries.AppleCryptoNative, EntryPoint = "AppleCryptoNative_DigestFinal")>]
    extern int DigestFinal(SafeDigestCtxHandle ctx, byte* pbOutput, int cbOutput);

  type internal AppleSafeDigestCtxHandle() =
    inherit SafeDigestCtxHandle()

    override x.ReleaseHandle() =
      AppleCrypto.DigestFree(x.handle)
      x.SetHandle(x.handle)
      true

    override x.IsInvalid = x.handle = IntPtr.Zero
    