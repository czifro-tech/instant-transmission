module MUDT.Test

open NUnit.Framework
open FsUnit
open MUDT
open MUDT.IO
open MUDT.Cryptography

// [<Test>]
// let ``Example Test`` () =
//     1 |> should equal 1

[<Test>]
let testFileChecksum () =
  let md5 = MUDT.Cryptography.MD5()
  let md5_1 = MUDT.Cryptography.MD5()
  let bytes = "==========================This is going to be an extraordinarily long string to test hashing segments==================="B
  let hash1 = md5.Hash(bytes) |> md5.HashFinal
  let offset = (Array.length (bytes)) / 4
  md5_1.Hash(bytes.[0..offset])
  md5_1.Hash(bytes.[offset..(2*offset-1)])
  md5_1.Hash(bytes.[(2*offset)..(3*offset-1)])
  md5_1.Hash(bytes.[(3*offset)..])
  let hash2 = md5.HashFinal()
  printf "hash1: %A" hash1
  printf "hash2: %A" hash2
