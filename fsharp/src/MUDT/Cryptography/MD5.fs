namespace MUDT.Cryptography

  open System
  open System.Runtime.InteropServices
  open MUDT.Cryptography

  // type MD5Container =
  //   {
  //     hashProvider: HashProvider;
  //     hash: byte[] -> unit
  //     final: unit -> byte[]
  //   }

  //   static member DefaultInstance() =
  //     let hp = HashProviderDispenser.createHashProvider (HashAlgorithmNames.MD5)
  //     {
  //       hashProvider = hp
  //       hash = (fun b -> hp.AppendHashData(b, 0, (Array.length b)))
  //       final = hp.FinalizeHashAndReset
  //     }

  // type MD5() =
    
  //   let hashProvider = 
  //     HashProviderDispenser.createHashProvider (HashAlgorithmNames.MD5)

  //   member x.Hash(array:byte[]) =
  //     hashProvider.AppendHashData(array, 0, (Array.length array))

  //   member x.Final() =
  //     hashProvider.FinalizeHashAndReset()

  // module Md5Helper =

  //   let create() =
  //     new MD5()