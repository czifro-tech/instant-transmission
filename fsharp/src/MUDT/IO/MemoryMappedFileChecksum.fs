namespace MUDT.IO

  open System
  open System.Collections.Generic
  open System.Security.Cryptography
  open MUDT.Diagnostics
  open MUDT.Cryptography

  type MemoryMappedFileChecksumState =
    {
      partitionId : int64; // this will be the starting position of partition
      checksum : byte[];
      md5State : MD5State;
      hashCount : int; // this is the amount that has been hashed since previous MD5.finalState
      maxHashCount : int; // this is the max amount that can be hashed at one time
      backlog : byte[];
      maxBacklogSize : int;
    }

    static member DefaultInstance =
      {
        partitionId = 0L;
        checksum = [||];
        md5State = MD5State.DefaultInstance;
        hashCount = 0;
        maxHashCount = 0;
        backlog = [||];
        maxBacklogSize = 0;
      }

  module MemoryMappedFileChecksum =

    let t = 0 
    // let update (checksumState:MemoryMappedFileChecksumState) (bytes:byte[]) =
    //   {
    //     checksumState with
    //       md5State = MD5_0.updateState checksumState.md5State bytes 0 (Array.length bytes)
    //   }
    
    // let computeAndStore (checksumState:MemoryMappedFileChecksumState) (bytes:byte[]) =
    //   let createHash = 
    //     (MD5_0.updateState checksumState.md5State bytes 0 (Array.length bytes))
    //     |> MD5_0.finalState
    //   {
    //     checksumState with
    //       checksum = Array.append checksumState.checksum createHash
    //   }

    // let backlogAndCompute (checksumState:MemoryMappedFileChecksumState) (bytes:byte[]) =
    //   let bytesLength = Array.length bytes
    //   if ((Array.length checksumState.backlog) + bytesLength) > checksumState.maxBacklogSize then
    //     let lastIndex = checksumState.maxBacklogSize - bytesLength
    //     let buffer = Array.append checksumState.backlog bytes.[0..lastIndex]
    //     let createHash = 
    //       (MD5_0.updateState checksumState.md5State buffer 0 (Array.length buffer))
    //       |> MD5_0.finalState
    //     {
    //       checksumState with
    //        backlog = bytes.[lastIndex..];
    //        checksum = Array.append checksumState.checksum createHash;
    //     }
    //   else
    //     {
    //       checksumState with
    //         backlog = Array.append checksumState.backlog bytes;
    //     }

    // let finalAndStore (checksumState:MemoryMappedFileChecksumState) =
    //   {
    //     checksumState with
    //       checksum = Array.append checksumState.checksum (MD5_0.finalState checksumState.md5State)
    //   }

    // let final (checksumState:MemoryMappedFileChecksumState) =
    //   (finalAndStore checksumState).checksum

    // let private mappedChecksum =
    //   let cacheKey = "mappedChecksum"
    //   let cachedMap = Cache.tryGetCachedItem cacheKey
    //   let mutable map = null : Dictionary<int64,byte[]>
    //   if cachedMap.IsNone then
    //     map <- new Dictionary<int64,byte[]>()
    //     Cache.cacheItem cacheKey map
    //   else
    //     map <- (cachedMap.Value :?> Dictionary<int64,byte[]>)
    //   map
    
    // let private md5 =
    //   let cacheKey = "md5"
    //   let cachedMd5 = Cache.tryGetCachedItem cacheKey
    //   let mutable m = null : MD5
    //   if cachedMd5.IsNone then
    //     m <- MD5.Create()
    //     Cache.cacheItem cacheKey m
    //   else
    //     m <- (cachedMd5.Value :?> MD5)
    //   m

    // let computeAndStoreHash (key:int64) (bytes:byte[]) =
    //   let checksums = mappedChecksum
    //   let hasher = md5
    //   if checksums.ContainsKey(key) then
    //     let checksum = checksums.[key]
    //     checksums.[key] <- 
    //       Array.append checksum (hasher.ComputeHash(bytes))
    //   else
    //     checksums.Add(key, hasher.ComputeHash(bytes))
    //   bytes

    // let tryGetChecksum (key:int64) =
    //   let checksums = mappedChecksum
    //   if checksums.ContainsKey(key) then
    //     Some checksums.[key]
    //   else
    //     None