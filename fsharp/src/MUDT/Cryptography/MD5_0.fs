namespace MUDT.Cryptography

  open MUDT.Utilities.TypeUtility
  open MUDT.Cryptography

  type MD5State = 
    {
      state : int[];
      count : int[];
      buffer : byte[];
    }

    static member DefaultInstance =
      {
        state = 
          [| 0x67452301; 0xefcdab89; 0x98badcfe; 0x10325476; |]
        count = [| 0; 0; |]
        buffer = nullByteArray 64
      }

  /// <summary>
  /// The following code, as well as above, comes from http://motp.sourceforge.net/MD5.java
  /// The difference between the two is that the MD5State is not
  /// cached by this module. This module simply performs computations on that state
  /// </summary>
  module MD5_0 =

    let t = 0

  type MD5() =
    
    let hashProvider = 
      HashProviderDispenser.createHashProvider (HashAlgorithmNames.MD5)

    member x.Hash(array:byte[]) =
      hashProvider.AppendHashData(array, 0, (Array.length array))

    member x.HashFinal() : byte[] =
      hashProvider.FinalizeHashAndReset()

  module Md5Helper =

    let create() =
      new MD5()