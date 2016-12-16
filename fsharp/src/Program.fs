// // Learn more about F# at http://fsharp.org
// namespace MUDT

// module Main =

// open System
// open System.IO
// open MUDT.Cryptography

// let testHasher() =
//   let mutable ihState1 = Hasher.createIHHashState(10)
//   let mutable ihState2 = Hasher.createIHHashState(10)
//   let data = "==========================This is going to be an extraordinarily long string to test hashing segments==================="B
//   let hash1 = (Hasher.computeHash ihState1 data) |> Hasher.finalizeHash
//   let offset = (Array.length data) / 4
//   ihState2 <- Hasher.computeHash ihState2 data.[0..offset-1]
//   ihState2 <- Hasher.computeHash ihState2 data.[offset..2*offset-1]
//   ihState2 <- Hasher.computeHash ihState2 data.[2*offset..3*offset-1]
//   ihState2 <- Hasher.computeHash ihState2 data.[3*offset..4*offset-1]
//   let hash2 = Hasher.finalizeHash ihState2

//   //printfn "hash1: %A" hash1
//   //printfn "hash2: %A" hash2
//   Array.iter2(fun a b -> if a <> b then failwith "Hashes do not match") hash1 hash2
//   printfn "Hashes matched"
  

// [<EntryPoint>]
// let main argv = 
//     testHasher()
//     0 // return an integer exit code