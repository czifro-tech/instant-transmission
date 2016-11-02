namespace MUDT.IO

  open System
  open System.Collections.Generic
  open System.Security.Cryptography
  open MUDT.Diagnostics

  module MemoryMappedFileChecksum =

    let private mappedChecksum =
      let cacheKey = "mappedChecksum"
      let cachedMap = Cache.tryGetCachedItem cacheKey
      let mutable map = null : Dictionary<int64,byte[]>
      if cachedMap.IsNone then
        map <- new Dictionary<int64,byte[]>()
        Cache.cacheItem cacheKey map
      else
        map <- (cachedMap.Value :?> Dictionary<int64,byte[]>)
      map
    
    let private md5 =
      let cacheKey = "md5"
      let cachedMd5 = Cache.tryGetCachedItem cacheKey
      let mutable m = null : MD5
      if cachedMd5.IsNone then
        m <- MD5.Create()
        Cache.cacheItem cacheKey m
      else
        m <- (cachedMd5.Value :?> MD5)
      m

    let computeAndStoreHash (key:int64) (bytes:byte[]) =
      let checksums = mappedChecksum
      let hasher = md5
      if checksums.ContainsKey(key) then
        let checksum = checksums.[key]
        checksums.[key] <- 
          Array.append checksum (hasher.ComputeHash(bytes))
      else
        checksums.Add(key, hasher.ComputeHash(bytes))
      bytes

    let tryGetChecksum (key:int64) =
      let checksums = mappedChecksum
      if checksums.ContainsKey(key) then
        Some checksums.[key]
      else
        None