namespace MUDT.Diagnostics

  open System
  open System.Collections.Generic

  module Cache =

    let private _cache =
      new Dictionary<string,obj>()

    let cacheItem k v =
      _cache.Add(k, v)

    let tryGetCachedItem k =
      if _cache.ContainsKey(k) then 
        Some _cache.[k]
      else
        None

    let getCachedItem k =
      _cache.[k]