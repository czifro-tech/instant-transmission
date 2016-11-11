#nowarn "9"
namespace MUDT.Cryptography.Unix

  open System
  open System.Security.Cryptography
  open System.Runtime.InteropServices
  open MUDT.Cryptography
  open MUDT.Cryptography.Unix
  open MUDT.Utilities

  [<Sealed>]
  [<AllowNullLiteral>]
  type EvpHashProvider(alg:IntPtr) =
    inherit HashProvider() with

      let mutable algorithmEvp = IntPtr.Zero
      let mutable hashSize = 0
      let mutable ctx = null : SafeEvpMdCtxHandle

      do
        algorithmEvp <- alg
        assert (alg <> IntPtr.Zero)

        hashSize <- UnixCrypto.EvpMdSize(algorithmEvp)
        if hashSize <= 0 || hashSize > UnixCrypto.EVP_MAX_MD_SIZE then
          raise (CryptographicException())
        
        ctx <- UnixCrypto.EvpMdCtxCreate(algorithmEvp)

        UnixCrypto.CheckValidOpenSslHandle(ctx)

      member private x.Check(result:int) =
        let success = 1
        if result <> success then
          assert (result = 0)
          raise (UnixCrypto.CreateOpenSslCryptographicException())

      override x.AppendHashDataCore(data:byte[], offset:int, count:int) =
        let fmd = fixed data
        let md = NativeInterop.NativePtr.add fmd offset
        x.Check(UnixCrypto.EvpDigestUpdate(ctx, md, count))

      override x.FinalizeHashAndReset() =
        let md = NativeInterop.NativePtr.stackalloc<byte> UnixCrypto.EVP_MAX_MD_SIZE
        let mutable length = uint32(UnixCrypto.EVP_MAX_MD_SIZE)
        x.Check(UnixCrypto.EvpDigestFinalEx(ctx, md, &length))
        assert (length = uint32(hashSize))

        x.Check(UnixCrypto.EvpDigestReset(ctx, algorithmEvp))

        let deref (ptr:nativeptr<byte>) =
          NativeInterop.NativePtr.toNativeInt(ptr)
          |> NativeInterop.NativePtr.ofNativeInt
          |> NativeInterop.NativePtr.read<IntPtr>

        let result = TypeUtility.nullByteArray (int(length))
        Marshal.Copy((deref md), result, 0, int(length))
        result

      override x.HashSizeInBytes 
        with get() = hashSize

      override x.Dispose(disposing:bool) =
        if disposing then
          ctx.Dispose()

  module internal UnixHashProviderDispenser =

    let createHashProvider (alg:HashAlgorithmNames) : HashProvider =
      let provider =
        match alg with
        | HashAlgorithmNames.MD5 -> new EvpHashProvider(UnixCrypto.EvpMd5())
        | _ -> null
      provider :> HashProvider