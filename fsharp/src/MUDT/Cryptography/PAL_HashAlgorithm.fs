namespace MUDT.Cryptography

  type internal PAL_HashAlgorithm =
    | Unknown = 0
    | Md5 = 1
    | Sha1 = 2
    | Sha256 = 3
    | Sha384 = 4
    | Sha512 = 5