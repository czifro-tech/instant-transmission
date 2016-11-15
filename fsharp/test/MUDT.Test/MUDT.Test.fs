module MUDT.Test

open System.Security.Cryptography
open NUnit.Framework
open FsUnit
open MUDT
open MUDT.IO
//open MUDT.Cryptography

[<TestFixture>]
type Test() =

  

// [<Test>]
// let ``Example Test`` () =
//     1 |> should equal 1

  [<Test>]
  member x.Test () =
    let md5 = MD5.Create()
    System.Diagnostics.Trace.WriteLine(
      sprintf "Hash: %A" (md5.ComputeHash("hello world"B)))

  [<Test>]
  member x.TestFileChecksum () =
    printfn "Dir: %s" System.Environment.CurrentDirectory//(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile))
    let md5 = MUDT.Cryptography.Md5Helper.create()
    let md5_1 = MUDT.Cryptography.Md5Helper.create()
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
