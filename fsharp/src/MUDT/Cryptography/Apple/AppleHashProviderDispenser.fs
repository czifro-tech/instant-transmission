#nowarn "9"
namespace MUDT.Cryptography.Apple

  open System
  open System.Security.Cryptography
  open MUDT.Cryptography
  open MUDT.Cryptography.Apple
  open MUDT.Utilities

  [<Sealed>]
  [<AllowNullLiteral>]
  type internal AppleDigestProvider(algorithm:PAL_HashAlgorithm) =
    inherit HashProvider() with

      let mutable ctx = null : SafeDigestCtxHandle
      let mutable hashSizeInBytes = 0

      do 
        ctx <- AppleCrypto.DigestCreate(algorithm, &hashSizeInBytes)
        if (hashSizeInBytes < 0) then
          ctx.Dispose()
          raise (PlatformNotSupportedException((sprintf "Unknown Algorithm: PAL_HashAlgorithm.%s" (algorithm.ToString()))))
        else
          if ctx.IsInvalid then
            ctx.Dispose()
            raise (CryptographicException())
          
      
      override x.HashSizeInBytes with get() = hashSizeInBytes
      override x.AppendHashDataCore(data:byte[], offset:int, count:int) =
        assert (not <| isNull(data))
        assert (offset >= 0)
        assert (offset < (Array.length data))
        assert (count >= 0)
        assert (((Array.length data) - offset) > count)

        let pData = fixed data
        let pbData = NativeInterop.NativePtr.add pData offset
        let ret = AppleCrypto.DigestUpdate(ctx, pbData, count)

        if ret <> 1 then
          assert (ret = 0) //  (sprintf "DigestUpdate return value %d was not 0 or 1" ret)
          raise (CryptographicException())

      override x.FinalizeHashAndReset() =
        let mutable hash = TypeUtility.nullByteArray x.HashSizeInBytes

        let pHash = fixed hash
        let ret = AppleCrypto.DigestFinal(ctx, pHash, (Array.length hash))

        if ret <> 1 then
          assert (ret = 0) //  (sprintf "DigestUpdate return value %d was not 0 or 1" ret)
          raise (CryptographicException())
        
        hash

      override x.Dispose(disposing:bool) =
        if disposing && (not <| isNull(ctx)) then
          ctx.Dispose()
    

  module internal AppleHashProviderDispenser =
    
    let createHashProvider (alg:HashAlgorithmNames) : HashProvider =
      let provider =
        match alg with
        | HashAlgorithmNames.MD5 -> new AppleDigestProvider(PAL_HashAlgorithm.Md5)
        | _ -> null
      provider :> HashProvider